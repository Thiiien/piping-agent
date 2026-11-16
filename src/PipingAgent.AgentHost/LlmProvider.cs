using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PipingAgent.Tools;

namespace PipingAgent.AgentHost;

/// <summary>Một lượt trả lời của mô hình đã được chuẩn hoá, độc lập nhà cung cấp.</summary>
public sealed class LlmTurn
{
    public string? Text { get; set; }

    public List<LlmToolCall> ToolCalls { get; } = new();

    /// <summary><c>stop</c>, <c>tool_calls</c>, <c>length</c>, <c>content_filter</c>, hoặc giá trị lạ của nhà cung cấp.</summary>
    public string FinishReason { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedTokens { get; set; }

    /// <summary>Thông điệp trợ lý nguyên dạng của nhà cung cấp, nối lại vào lịch sử để giữ tiền tố ổn định.</summary>
    public JsonElement RawAssistantMessage { get; set; }
}

public sealed class LlmToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ArgumentsJson { get; set; } = "{}";
}

/// <summary>Lớp trừu tượng nhà cung cấp, mục 8.7: gửi một lượt hội thoại kèm công cụ. Vòng lặp agent không biết nhà cung cấp nào.</summary>
public interface ILlmProvider
{
    string Model { get; }

    Task<LlmTurn> CompleteAsync(IReadOnlyList<object> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellation);

    /// <summary>Thông điệp lịch sử cho kết quả công cụ, theo định dạng của nhà cung cấp.</summary>
    object ToolResultMessage(LlmToolCall call, string resultJson);
}

/// <summary>
/// Bộ kết nối cho mọi điểm cuối kiểu OpenAI chat completions: Hermes proxy, Ollama, LM Studio, cổng nội bộ. Gọi hàm theo
/// chuẩn <c>tools</c> và <c>tool_calls</c>. Khoá truy cập chỉ nằm trong tiêu đề, không vào nhật ký.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly int _maxOutputTokens;
    private readonly double _temperature;
    private readonly Action<string>? _log;

    public OpenAiCompatibleProvider(string endpoint, string model, string apiKey, int maxOutputTokens, double temperature, int timeoutSec, Action<string>? log = null)
    {
        _endpoint = endpoint.TrimEnd('/');
        Model = model;
        _maxOutputTokens = maxOutputTokens;
        _temperature = temperature;
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(30, timeoutSec)) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrEmpty(apiKey) ? "none" : apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PipingAgent/0.1");
    }

    public string Model { get; }

    public async Task<LlmTurn> CompleteAsync(IReadOnlyList<object> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellation)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = Model,
            ["messages"] = messages,
            ["temperature"] = _temperature,
            ["max_tokens"] = _maxOutputTokens,
        };
        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(t => t.ToFunctionSpec()).ToList();
            body["tool_choice"] = "auto";
        }

        var json = JsonSerializer.Serialize(body, ToolJson.Options);
        const int maxRetries = 3;
        var attempt = 0;

        while (true)
        {
            attempt++;
            using var content = new StringContent(json, new UTF8Encoding(false), "application/json");
            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(_endpoint + "/chat/completions", content, cancellation).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (attempt <= maxRetries && !cancellation.IsCancellationRequested)
                {
                    var delaySec = Math.Pow(2, attempt); // 2s, 4s, 8s
                    _log?.Invoke($"Lỗi kết nối HTTP: {ex.Message}. Đang thử lại lần {attempt}/{maxRetries} sau {delaySec}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), cancellation).ConfigureAwait(false);
                    continue;
                }

                var hint = _endpoint.Contains("8645")
                    ? " Không kết nối được Hermes Proxy tại 127.0.0.1:8645. Hãy kiểm tra ứng dụng Hermes và đảm bảo Hermes Proxy đang chạy."
                    : string.Empty;
                throw new InvalidOperationException($"Không kết nối được tới điểm cuối {_endpoint}: {ex.Message}.{hint}", ex);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
                var statusCode = (int)response.StatusCode;

                // Các mã lỗi tạm thời: 429 (Too Many Requests / Upstream Capacity), 502, 503, 504
                if ((statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504) && attempt <= maxRetries && !cancellation.IsCancellationRequested)
                {
                    var delaySec = Math.Pow(2, attempt); // 2s, 4s, 8s
                    if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                    {
                        delaySec = Math.Max(1.0, Math.Min(30.0, response.Headers.RetryAfter.Delta.Value.TotalSeconds));
                    }

                    _log?.Invoke($"Mô hình trả về HTTP {statusCode} ({Trim(text, 120)}). Đang tự động thử lại lần {attempt}/{maxRetries} sau {delaySec}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), cancellation).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var capacityNotice = statusCode == 429
                        ? " (Máy chủ mô hình đang quá tải lưu lượng tạm thời. Vui lòng thử lại sau vài giây hoặc chọn mô hình khác trong PA_AGENTCONFIG)"
                        : string.Empty;
                    throw new InvalidOperationException($"Mô hình trả về HTTP {statusCode}: {Trim(text, 500)}{capacityNotice}");
                }

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var choice = root.GetProperty("choices")[0];
                var message = choice.GetProperty("message");
                var turn = new LlmTurn
                {
                    FinishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() ?? string.Empty : string.Empty,
                    RawAssistantMessage = message.Clone(),
                };
                if (message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String) turn.Text = c.GetString();
                if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in calls.EnumerateArray())
                    {
                        var fn = call.GetProperty("function");
                        turn.ToolCalls.Add(new LlmToolCall
                        {
                            Id = call.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                            Name = fn.GetProperty("name").GetString() ?? string.Empty,
                            ArgumentsJson = fn.TryGetProperty("arguments", out var args) ? (args.ValueKind == JsonValueKind.String ? args.GetString() ?? "{}" : args.GetRawText()) : "{}",
                        });
                    }
                }

                if (root.TryGetProperty("usage", out var usage))
                {
                    turn.InputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                    turn.OutputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                    if (usage.TryGetProperty("prompt_tokens_details", out var d) && d.TryGetProperty("cached_tokens", out var cached)) turn.CachedTokens = cached.GetInt32();
                }

                return turn;
            }
        }
    }

    public object ToolResultMessage(LlmToolCall call, string resultJson) => new { role = "tool", tool_call_id = call.Id, content = resultJson };

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
