using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PipingAgent.Hosting
{
    /// <summary>Một thông điệp JSON-RPC 2.0, mỗi dòng một thông điệp trên kênh. Dùng chung cho plug-in và Agent Host, S7-04.</summary>
    public sealed class RpcMessage
    {
        public string Jsonrpc { get; set; } = "2.0";

        public JsonElement? Id { get; set; }

        public string? Method { get; set; }

        public JsonElement? Params { get; set; }

        public JsonElement? Result { get; set; }

        public RpcError? Error { get; set; }

        public bool IsRequest => Method != null && Id.HasValue;

        public bool IsNotification => Method != null && !Id.HasValue;

        public bool IsResponse => Method == null && Id.HasValue;
    }

    public sealed class RpcError
    {
        public int Code { get; set; }

        public string Message { get; set; } = string.Empty;

        public JsonElement? Data { get; set; }
    }

    public sealed class RpcException : Exception
    {
        public RpcException(int code, string message) : base(message)
        {
            Code = code;
        }

        public int Code { get; }
    }

    /// <summary>
    /// Đầu nối JSON-RPC hai chiều trên một luồng byte: gửi yêu cầu và chờ trả lời, đồng thời nhận yêu cầu của bên kia.
    /// Plug-in dùng <see cref="PumpUntil"/> để xử lý yêu cầu gọi công cụ ngay trên luồng đang chờ, tức luồng chính của AutoCAD.
    /// </summary>
    public sealed class JsonRpcPeer : IDisposable
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly Stream _stream;
        private readonly StreamReader _reader;
        private readonly object _writeLock = new object();
        private readonly ConcurrentDictionary<long, TaskCompletionSource<RpcMessage>> _pending = new ConcurrentDictionary<long, TaskCompletionSource<RpcMessage>>();
        private long _nextId;

        public JsonRpcPeer(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _reader = new StreamReader(stream, new UTF8Encoding(false), false, 1 << 16, leaveOpen: true);
        }

        /// <summary>Xử lý một yêu cầu của bên kia, trả kết quả hoặc ném <see cref="RpcException"/>.</summary>
        public Func<string, JsonElement?, JsonElement?>? RequestHandler { get; set; }

        /// <summary>
        /// Bộ xử lý bất đồng bộ cho Agent Host: yêu cầu chạy trên luồng khác để vòng đọc vẫn nhận được trả lời
        /// của các yêu cầu mà bộ xử lý gửi ngược lại, ví dụ tool.call trong lúc session.start đang chạy.
        /// </summary>
        public Func<string, JsonElement?, Task<JsonElement?>>? AsyncRequestHandler { get; set; }

        public Action<string, JsonElement?>? NotificationHandler { get; set; }

        public static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value, Options);

        public static T? FromElement<T>(JsonElement? e) => e.HasValue ? JsonSerializer.Deserialize<T>(e.Value.GetRawText(), Options) : default;

        public void Notify(string method, object? parameters)
        {
            Write(new RpcMessage { Method = method, Params = parameters == null ? (JsonElement?)null : ToElement(parameters) });
        }

        /// <summary>Gửi yêu cầu và trả về thẻ chờ. Phải có vòng đọc chạy, bằng <see cref="PumpUntil"/> hoặc <see cref="RunAsync"/>.</summary>
        public Task<RpcMessage> RequestAsync(string method, object? parameters)
        {
            var id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<RpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;
            Write(new RpcMessage { Id = ToElement(id), Method = method, Params = parameters == null ? (JsonElement?)null : ToElement(parameters) });
            return tcs.Task;
        }

        /// <summary>
        /// Gửi yêu cầu rồi đọc và xử lý thông điệp đến ngay trên luồng này cho tới khi có trả lời hoặc hết giờ.
        /// Yêu cầu của bên kia đến trong lúc chờ được chạy bằng <see cref="RequestHandler"/> trên chính luồng này.
        /// </summary>
        public RpcMessage RequestAndPump(string method, object? parameters, TimeSpan timeout)
        {
            var task = RequestAsync(method, parameters);
            var deadline = DateTime.UtcNow + timeout;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException($"Không nhận được trả lời cho {method} sau {timeout.TotalSeconds:0} giây.");
                var line = ReadLineWithTimeout(deadline);
                if (line == null) throw new IOException("Kênh đã đóng.");
                Dispatch(line);
            }

            var reply = task.GetAwaiter().GetResult();
            if (reply.Error != null) throw new RpcException(reply.Error.Code, reply.Error.Message);
            return reply;
        }

        /// <summary>Vòng đọc bất đồng bộ cho Agent Host. Kết thúc khi kênh đóng.</summary>
        public async Task RunAsync(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                if (line.Length == 0) continue;
                try
                {
                    Dispatch(line);
                }
                catch (Exception ex)
                {
                    Notify("event.error", new { message = ex.Message });
                }
            }

            foreach (var p in _pending.Values) p.TrySetException(new IOException("Kênh đã đóng."));
        }

        private string? ReadLineWithTimeout(DateTime deadline)
        {
            var task = _reader.ReadLineAsync();
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Hết giờ chờ thông điệp.");
                task.Wait(200);
            }

            return task.GetAwaiter().GetResult();
        }

        private void Dispatch(string line)
        {
            var msg = JsonSerializer.Deserialize<RpcMessage>(line, Options);
            if (msg == null) return;
            if (msg.IsResponse)
            {
                if (msg.Id!.Value.TryGetInt64(out var id) && _pending.TryRemove(id, out var tcs)) tcs.TrySetResult(msg);
                return;
            }

            if (msg.IsNotification)
            {
                NotificationHandler?.Invoke(msg.Method!, msg.Params);
                return;
            }

            if (msg.IsRequest)
            {
                if (RequestHandler == null && AsyncRequestHandler != null)
                {
                    var id = msg.Id;
                    var method = msg.Method!;
                    var p = msg.Params;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await AsyncRequestHandler(method, p).ConfigureAwait(false);
                            Write(new RpcMessage { Id = id, Result = result ?? ToElement(new { }) });
                        }
                        catch (RpcException ex)
                        {
                            Write(new RpcMessage { Id = id, Error = new RpcError { Code = ex.Code, Message = ex.Message } });
                        }
                        catch (Exception ex)
                        {
                            Write(new RpcMessage { Id = id, Error = new RpcError { Code = -32000, Message = ex.Message } });
                        }
                    });
                    return;
                }

                try
                {
                    if (RequestHandler == null) throw new RpcException(-32601, "Không có bộ xử lý yêu cầu.");
                    var result = RequestHandler(msg.Method!, msg.Params);
                    Write(new RpcMessage { Id = msg.Id, Result = result ?? ToElement(new { }) });
                }
                catch (RpcException ex)
                {
                    Write(new RpcMessage { Id = msg.Id, Error = new RpcError { Code = ex.Code, Message = ex.Message } });
                }
                catch (Exception ex)
                {
                    Write(new RpcMessage { Id = msg.Id, Error = new RpcError { Code = -32000, Message = ex.Message } });
                }
            }
        }

        private void Write(RpcMessage msg)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, Options) + "\n");
            lock (_writeLock)
            {
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
