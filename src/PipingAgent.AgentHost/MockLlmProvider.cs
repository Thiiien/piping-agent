using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PipingAgent.Tools;

namespace PipingAgent.AgentHost
{
    /// <summary>
    /// Bộ giả lập LLM có kịch bản định sẵn (deterministic canned responses), S8-01 và S8-07.
    /// Dùng cho kiểm thử tự động độc lập 100% offline, không tốn chi phí gọi mô hình và không phụ thuộc mạng.
    /// </summary>
    public sealed class MockLlmProvider : ILlmProvider
    {
        private readonly Queue<LlmTurn> _scriptedTurns = new();

        public MockLlmProvider(string model = "mock-model")
        {
            Model = model;
        }

        public string Model { get; }

        /// <summary>Danh sách tất cả yêu cầu đã nhận, dùng để kiểm tra assertions trong kiểm thử.</summary>
        public List<(IReadOnlyList<object> Messages, IReadOnlyList<ToolDefinition> Tools)> Requests { get; } = new();

        public void EnqueueTurn(LlmTurn turn)
        {
            _scriptedTurns.Enqueue(turn);
        }

        public void EnqueueText(string text, string finishReason = "stop")
        {
            _scriptedTurns.Enqueue(new LlmTurn
            {
                Text = text,
                FinishReason = finishReason,
                InputTokens = 100,
                OutputTokens = 50,
            });
        }

        public void EnqueueToolCall(string toolName, object arguments, string? callId = null, string finishReason = "tool_calls")
        {
            var call = new LlmToolCall
            {
                Id = callId ?? "call_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = toolName,
                ArgumentsJson = arguments is string s ? s : JsonSerializer.Serialize(arguments, ToolJson.Options),
            };

            var turn = new LlmTurn
            {
                FinishReason = finishReason,
                InputTokens = 120,
                OutputTokens = 30,
            };
            turn.ToolCalls.Add(call);
            _scriptedTurns.Enqueue(turn);
        }

        public Task<LlmTurn> CompleteAsync(IReadOnlyList<object> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellation)
        {
            Requests.Add((messages, tools));
            if (_scriptedTurns.Count > 0)
            {
                return Task.FromResult(_scriptedTurns.Dequeue());
            }

            // Mặc định trả lời stop nếu hết kịch bản
            return Task.FromResult(new LlmTurn
            {
                Text = "Hoàn tất phản hồi mặc định.",
                FinishReason = "stop",
                InputTokens = 50,
                OutputTokens = 20,
            });
        }

        public object ToolResultMessage(LlmToolCall call, string resultJson)
        {
            return new
            {
                role = "tool",
                tool_call_id = call.Id,
                content = resultJson,
            };
        }
    }
}
