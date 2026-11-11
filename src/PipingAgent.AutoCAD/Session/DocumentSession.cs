using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using System.IO;
using System.Text;
using PipingAgent.Core.Decisions;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Trạng thái của Piping Agent cho một bản vẽ đang mở, S2-15. Mỗi tài liệu một bản, giữ trong <see cref="SessionManager"/>,
    /// không có biến tĩnh toàn cục theo quy tắc 4 mục 6.12.3. Đóng bản vẽ thì bỏ cả phiên.
    /// </summary>
    public sealed class DocumentSession : IDisposable
    {
        public DocumentSession(Document document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Highlight = new HighlightService(document);
        }

        public Document Document { get; }

        public string DrawingName => System.IO.Path.GetFileName(Document.Name);

        public HighlightService Highlight { get; }

        /// <summary>Hệ số từ đơn vị bản vẽ sang mm của lần quét gần nhất.</summary>
        public double ScaleToMm { get; set; } = 1.0;

        public string? ProfilePath { get; set; }

        public ConventionProfile? Profile { get; set; }

        public List<ProfileError> ProfileErrors { get; } = new List<ProfileError>();

        public RecognitionResult? LastResult { get; set; }

        public IReadOnlyList<SourceEntity>? LastEntities { get; set; }

        /// <summary>IR đến từ tờ isometric (PA_ISO): toạ độ tờ là giấy, không tham chiếu bản 2D làm nền khi dựng vào bản mới.</summary>
        public bool FromIsometric { get; set; }

        /// <summary>Hộp bao theo handle của lần quét gần nhất, mm, để phóng tới vấn đề.</summary>
        public Dictionary<string, Box2> BoundsByHandle { get; } = new Dictionary<string, Box2>(StringComparer.OrdinalIgnoreCase);

        public string? LastFingerprint { get; set; }

        /// <summary>Quyết định của người dùng cho bản vẽ này, S5-05. Đọc từ tệp phiên khi mở, ghi sau mỗi thay đổi.</summary>
        public List<Decision> Decisions { get; } = new List<Decision>();

        /// <summary>Đường dẫn tệp phiên cạnh bản vẽ, null khi bản vẽ chưa lưu.</summary>
        public string? SessionPath => File.Exists(Document.Name) ? SessionFile.PathFor(Document.Name) : null;

        public string? SessionLoadError { get; private set; }

        /// <summary>Đọc tệp phiên nếu có. Tệp hỏng thì ghi lỗi và bắt đầu phiên trống, không làm hỏng lệnh.</summary>
        public void LoadSessionFile()
        {
            var path = SessionPath;
            if (path == null || !File.Exists(path)) return;
            try
            {
                var s = SessionFile.Deserialize(File.ReadAllText(path, Encoding.UTF8));
                Decisions.Clear();
                Decisions.AddRange(s.Decisions);
                if (!string.IsNullOrEmpty(s.ProfilePath) && File.Exists(s.ProfilePath)) ProfilePath = s.ProfilePath;
            }
            catch (System.Exception ex)
            {
                SessionLoadError = ex.Message;
            }
        }

        /// <summary>Ghi tệp phiên, S5-06. Ghi vào tệp tạm rồi đổi tên để tắt đột ngột không để lại tệp dở.</summary>
        public bool SaveSessionFile(bool includeModel = true)
        {
            var path = SessionPath;
            if (path == null) return false;
            var s = new SessionFile
            {
                Drawing = DrawingName,
                Fingerprint = LastFingerprint,
                ProfileId = Profile?.Id,
                ProfileVersion = Profile?.Version,
                ProfilePath = ProfilePath,
                SavedAt = SessionFile.Now(),
                Decisions = Decisions.ToList(),
                Model = includeModel ? LastResult?.Model : null,
            };
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, SessionFile.Serialize(s), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }

        /// <summary>Thêm một quyết định và lưu ngay.</summary>
        public void AddDecision(Decision decision)
        {
            Decisions.Add(decision);
            SaveSessionFile(includeModel: false);
        }

        public void StoreScan(IReadOnlyList<SourceEntity> entities, RecognitionResult result, double scaleToMm, string? fingerprint)
        {
            LastEntities = entities;
            LastResult = result;
            ScaleToMm = scaleToMm;
            LastFingerprint = fingerprint;
            FromIsometric = false;
            BoundsByHandle.Clear();
            foreach (var e in entities)
            {
                if (e.Bounds.HasValue && e.BlockPath.Count == 0) BoundsByHandle[e.Handle] = e.Bounds.Value;
            }
        }

        /// <summary>Nạp IR của tờ isometric (PA_ISO, PA_BUILDIR): không có đối tượng 2D nào đi kèm và không gắn nền 2D khi dựng.</summary>
        public void StoreIso(PipingModel model)
        {
            StoreScan(new List<SourceEntity>(), new RecognitionResult { Model = model }, 1.0, model.Source.Fingerprint);
            FromIsometric = true;
        }

        public void Dispose()
        {
            Highlight.Dispose();
        }
    }
}
