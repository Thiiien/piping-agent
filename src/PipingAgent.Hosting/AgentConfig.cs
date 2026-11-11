using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipingAgent.Hosting
{
    /// <summary>
    /// Cấu hình kết nối mô hình ngôn ngữ, S7-07 và S7-09: ba cấp máy, người dùng, dự án; cấp sau ghi đè cấp trước theo từng trường.
    /// Khoá truy cập không nằm trong tệp ở dạng đọc được: lưu bằng DPAPI theo người dùng Windows, hoặc tên biến môi trường.
    /// </summary>
    public sealed class AgentConfig
    {
        public const string FileName = "agent.json";

        /// <summary><c>openai-compatible</c> hoặc <c>off</c>.</summary>
        public string? Provider { get; set; }

        /// <summary>Điểm cuối gốc, ví dụ <c>http://127.0.0.1:8645/v1</c> của Hermes proxy.</summary>
        public string? Endpoint { get; set; }

        public string? Model { get; set; }

        /// <summary>Khoá đã mã hoá bằng DPAPI người dùng hiện tại, base64. Chỉ giải mã được trên cùng tài khoản Windows.</summary>
        public string? ApiKeyProtected { get; set; }

        /// <summary>Tên biến môi trường chứa khoá, thay cho <see cref="ApiKeyProtected"/>.</summary>
        public string? ApiKeyEnv { get; set; }

        public int? MaxSteps { get; set; }

        public int? MaxOutputTokens { get; set; }

        public int? TimeoutSec { get; set; }

        public double? Temperature { get; set; }

        /// <summary>Đường dẫn tệp thực thi Agent Host, mặc định thư mục <c>agenthost</c> cạnh plug-in.</summary>
        public string? HostExePath { get; set; }

        [JsonIgnore]
        public List<string> LoadedFrom { get; } = new List<string>();

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static string MachinePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PipingAgent", FileName);

        public static string UserPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipingAgent", FileName);

        public static string ProjectPath(string? drawingPath) =>
            string.IsNullOrEmpty(drawingPath) ? string.Empty : Path.Combine(Path.GetDirectoryName(drawingPath) ?? ".", "pipingagent." + FileName);

        /// <summary>Đọc ba cấp và gộp. Tệp thiếu thì bỏ qua, tệp hỏng thì ghi vào <see cref="LoadedFrom"/> kèm chữ lỗi và bỏ qua.</summary>
        public static AgentConfig Load(string? drawingPath = null)
        {
            var merged = Defaults();
            foreach (var path in new[] { MachinePath, UserPath, ProjectPath(drawingPath) })
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                try
                {
                    var layer = JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(path, Encoding.UTF8), Options);
                    if (layer == null) continue;
                    merged.Merge(layer);
                    merged.LoadedFrom.Add(path);
                }
                catch (Exception ex)
                {
                    merged.LoadedFrom.Add(path + " (lỗi: " + ex.Message + ")");
                }
            }

            return merged;
        }

        public static AgentConfig Defaults() => new AgentConfig
        {
            Provider = "off",
            Endpoint = "http://127.0.0.1:8645/v1",
            Model = "anthropic/claude-sonnet-5.5",
            MaxSteps = 20,
            MaxOutputTokens = 4000,
            TimeoutSec = 180,
            Temperature = 0,
        };

        public void Merge(AgentConfig other)
        {
            if (other.Provider != null) Provider = other.Provider;
            if (other.Endpoint != null) Endpoint = other.Endpoint;
            if (other.Model != null) Model = other.Model;
            if (other.ApiKeyProtected != null) ApiKeyProtected = other.ApiKeyProtected;
            if (other.ApiKeyEnv != null) ApiKeyEnv = other.ApiKeyEnv;
            if (other.MaxSteps.HasValue) MaxSteps = other.MaxSteps;
            if (other.MaxOutputTokens.HasValue) MaxOutputTokens = other.MaxOutputTokens;
            if (other.TimeoutSec.HasValue) TimeoutSec = other.TimeoutSec;
            if (other.Temperature.HasValue) Temperature = other.Temperature;
            if (other.HostExePath != null) HostExePath = other.HostExePath;
        }

        public bool Enabled => string.Equals(Provider, "openai-compatible", StringComparison.OrdinalIgnoreCase);

        /// <summary>Khoá ở dạng rõ, chỉ giữ trong bộ nhớ. Không ghi vào nhật ký.</summary>
        public string ResolveApiKey()
        {
            if (!string.IsNullOrEmpty(ApiKeyEnv))
            {
                var v = Environment.GetEnvironmentVariable(ApiKeyEnv);
                if (!string.IsNullOrEmpty(v)) return v!;
            }

            if (!string.IsNullOrEmpty(ApiKeyProtected))
            {
                try
                {
                    return Unprotect(ApiKeyProtected!);
                }
                catch (CryptographicException)
                {
                    return string.Empty;
                }
            }

            return string.Empty;
        }

        public static string Protect(string plain) =>
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain ?? string.Empty), Entropy, DataProtectionScope.CurrentUser));

        public static string Unprotect(string protectedBase64) =>
            Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser));

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PipingAgent.agent.json.v1");

        /// <summary>Ghi một cấp. Chỉ ghi các trường có giá trị.</summary>
        public static void Save(string path, AgentConfig layer)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(layer, Options), new UTF8Encoding(false));
        }

        public static AgentConfig? ReadLayer(string path) =>
            File.Exists(path) ? JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(path, Encoding.UTF8), Options) : null;

        public string Describe() =>
            $"provider={Provider}, endpoint={Endpoint}, model={Model}, key={(string.IsNullOrEmpty(ApiKeyProtected) && string.IsNullOrEmpty(ApiKeyEnv) ? "không" : "có")}, maxSteps={MaxSteps}, nguồn: {(LoadedFrom.Count == 0 ? "mặc định" : string.Join("; ", LoadedFrom))}";
    }
}
