using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Profile;
using PipingAgent.Hosting;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Giữ một <see cref="DocumentSession"/> cho mỗi bản vẽ đang mở, S2-15. Lắng nghe sự kiện đóng và chuyển bản vẽ
    /// để bỏ phiên cũ và báo cho bảng điều khiển đổi dữ liệu. Tạo một bản ở <see cref="PluginApp"/>, huỷ khi plug-in thoát.
    /// </summary>
    public sealed class SessionManager : IDisposable
    {
        private readonly Dictionary<Document, DocumentSession> _sessions = new Dictionary<Document, DocumentSession>();
        private PipeCatalog? _catalog;

        public SessionManager()
        {
            var dm = AcApp.DocumentManager;
            dm.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            dm.DocumentActivated += OnDocumentActivated;
        }

        /// <summary>Bản vẽ đang hoạt động đổi. Bảng điều khiển đăng ký để hiện dữ liệu của phiên tương ứng.</summary>
        public event EventHandler<DocumentSession?>? ActiveSessionChanged;

        public DocumentSession? Active
        {
            get
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                return doc == null ? null : GetOrCreate(doc);
            }
        }

        public DocumentSession GetOrCreate(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!_sessions.TryGetValue(document, out var session))
            {
                session = new DocumentSession(document) { ProfilePath = ProfileStore.LastProfilePath() };
                session.LoadSessionFile();
                _sessions[document] = session;
            }

            return session;
        }

        public DocumentSession? Find(Document document) => _sessions.TryGetValue(document, out var s) ? s : null;

        public int Count => _sessions.Count;

        /// <summary>Danh mục ống nạp một lần cho cả tiến trình, vì không phụ thuộc bản vẽ.</summary>
        public PipeCatalog Catalog => _catalog ??= CatalogFiles.LoadDirectory(ProfileStore.CatalogDirectory);

        private PipingAgent.Core.Equipment.EquipmentLibrary? _equipment;

        /// <summary>Thư viện thiết bị ba lớp: đi kèm plug-in, công ty ở %PROGRAMDATA%, dự án ở %LOCALAPPDATA%.</summary>
        public PipingAgent.Core.Equipment.EquipmentLibrary Equipment => _equipment ??= EquipmentFiles.LoadLayers(
            ProfileStore.EquipmentDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PipingAgent", "equipment"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipingAgent", "equipment"));

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (_sessions.TryGetValue(e.Document, out var session))
            {
                _sessions.Remove(e.Document);
                session.Dispose();
                PluginApp.Log?.Info("session_closed", new Dictionary<string, object?> { ["drawing"] = session.DrawingName, ["open"] = _sessions.Count });
            }
        }

        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            ActiveSessionChanged?.Invoke(this, e.Document == null ? null : GetOrCreate(e.Document));
        }

        public void Dispose()
        {
            var dm = AcApp.DocumentManager;
            dm.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            dm.DocumentActivated -= OnDocumentActivated;
            foreach (var s in _sessions.Values) s.Dispose();
            _sessions.Clear();
        }
    }

    /// <summary>Tìm hồ sơ quy ước và danh mục đi kèm plug-in, nhớ hồ sơ đã chọn lần trước.</summary>
    public static class ProfileStore
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipingAgent", "settings.json");

        public static string PluginDirectory => Path.GetDirectoryName(typeof(ProfileStore).Assembly.Location) ?? ".";

        public static string ProfileDirectory => Path.Combine(PluginDirectory, "profiles");

        public static string CatalogDirectory => Path.Combine(PluginDirectory, "catalog");

        public static string EquipmentDirectory => Path.Combine(PluginDirectory, "equipment");

        /// <summary>Các tệp hồ sơ đi kèm, bỏ hồ sơ nền chỉ dùng để kế thừa.</summary>
        public static List<string> AvailableProfiles()
        {
            return ProfileFiles.List(ProfileDirectory);
        }

        public static string? LastProfilePath()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    using (var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath, Encoding.UTF8)))
                    {
                        if (doc.RootElement.TryGetProperty("lastProfile", out var p) && p.ValueKind == JsonValueKind.String)
                        {
                            var path = p.GetString();
                            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Tệp cài đặt hỏng thì coi như chưa có.
            }

            return AvailableProfiles().FirstOrDefault();
        }

        public static void RememberProfile(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { lastProfile = path }), Encoding.UTF8);
            }
            catch (System.Exception)
            {
                // Không ghi được cài đặt thì lần sau chọn lại, không phải lỗi của lệnh.
            }
        }

        public static ProfileLoadResult Load(string path) => ProfileFiles.Load(path);
    }
}
