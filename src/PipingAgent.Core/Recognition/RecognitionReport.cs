using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Báo cáo chữ của một lần nhận diện, dùng chung cho dòng lệnh và dòng lệnh AutoCAD. S2-04 và S2-18.</summary>
    public static class RecognitionReport
    {
        public static string ToText(RecognitionResult result, int maxLayers = 15, int maxIssues = 20)
        {
            var w = new StringWriter(CultureInfo.InvariantCulture);
            Write(result, w, maxLayers, maxIssues);
            return w.ToString();
        }

        public static void Write(RecognitionResult result, TextWriter w, int maxLayers = 15, int maxIssues = 20)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (w == null) throw new ArgumentNullException(nameof(w));
            var r = result.Classification.Report;
            var m = result.Model;
            w.WriteLine(string.Format(CultureInfo.InvariantCulture, "Phân loại: {0} đối tượng, {1} chưa phân loại ({2:P1}), ngưỡng {3:P0}{4}. Thời gian {5:0.00} s.",
                r.Total, r.Unclassified, r.UnclassifiedRatio, r.WarningThreshold, r.ExceedsThreshold ? ", VƯỢT" : string.Empty, result.Elapsed.TotalSeconds));
            if (result.Cancelled) w.WriteLine("ĐÃ HỦY giữa chừng, kết quả không đầy đủ.");

            w.WriteLine("Theo vai trò:");
            foreach (var kv in r.ByRole.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
            {
                w.WriteLine($"  {kv.Key,-18} {kv.Value,7}");
            }

            var unclassifiedLayers = r.Layers.Where(l => l.Unclassified > 0).Take(maxLayers).ToList();
            if (unclassifiedLayers.Count > 0)
            {
                w.WriteLine("Chưa phân loại theo layer:");
                foreach (var l in unclassifiedLayers)
                {
                    var types = string.Join(", ", l.UnclassifiedByType.OrderByDescending(t => t.Value).Take(4).Select(t => $"{t.Key} {t.Value}"));
                    w.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,-16} {1,6}/{2,-6} {3,6:P0}  {4}", l.Layer, l.Unclassified, l.Total, l.UnclassifiedRatio, types));
                }
            }

            w.WriteLine($"Nhãn: {result.TagReads.Count} chữ trên layer nhãn, {result.TagReads.Count(t => t.Matched)} khớp mẫu, {result.TagReads.Count(t => t.Matched && t.Tag!.IsElevationOnly)} là cao độ rời.");
            w.WriteLine($"Làm sạch: {result.RunBuild.Cleanup}.");
            var att = result.TagAttach.Attachments;
            w.WriteLine($"Gắn nhãn: {att.Count(a => a.Attached)}/{att.Count(a => a.Tag.Matched)} nhãn đã gắn. " +
                $"Đường dẫn {att.Count(a => a.Attached && a.Method == AttachMethods.Leader)}, nằm sát {att.Count(a => a.Attached && a.Method == AttachMethods.Adjacent)}, gần nhất {att.Count(a => a.Attached && a.Method == AttachMethods.Nearest)}.");
            w.WriteLine($"Nối qua block: {result.Bridge}. Khép khe hở: {result.GapsClosed}. Lan truyền: {result.Propagation}.");
            w.WriteLine($"Mô hình: {m.Lines.Count} tuyến, {m.Runs.Count} đoạn chạy, {m.Runs.Count(x => x.TagRef != null)} có nhãn, {m.Runs.Count(x => x.Elevation.Datum != ElevationDatum.Unknown)} có cao độ.");
            w.WriteLine($"Đồ thị: {result.Graph}.");
            w.WriteLine($"Cấu kiện: {result.Components}. Thiết bị: {result.Equipment}. Đầu chờ: {result.Nozzles}. Kết cấu: {result.Structures}. Bộ luật: {result.Rules}.");
            if (result.Model.Decisions.Count > 0) w.WriteLine($"Quyết định: {result.Decisions}.");

            if (m.Issues.Count > 0)
            {
                w.WriteLine($"Vấn đề: {m.Issues.Count}, theo luật: " + string.Join(", ", m.Issues.GroupBy(i => i.RuleId).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}")));
                foreach (var i in m.Issues.OrderBy(i => i.Severity).ThenBy(i => i.Id, StringComparer.Ordinal).Take(maxIssues))
                {
                    w.WriteLine($"  [{SeverityText(i.Severity)}] {i.RuleId} {i.Message}");
                }

                if (m.Issues.Count > maxIssues) w.WriteLine($"  ... còn {m.Issues.Count - maxIssues} vấn đề.");
            }
        }

        public static string SeverityText(Severity s)
        {
            switch (s)
            {
                case Severity.Blocker: return "CHẶN";
                case Severity.Warning: return "CẢNH BÁO";
                default: return "THÔNG TIN";
            }
        }

        public static string RoleText(string role)
        {
            switch (role)
            {
                case EntityRoles.PipeCenterline: return "Đường tâm ống";
                case EntityRoles.PipeTag: return "Nhãn ống";
                case EntityRoles.PipeTagLeader: return "Đường dẫn nhãn";
                case EntityRoles.RiserSymbol: return "Ký hiệu ống đứng";
                case EntityRoles.FlowArrow: return "Mũi tên dòng chảy";
                case EntityRoles.InlineComponent: return "Cấu kiện";
                case EntityRoles.ComponentTag: return "Chú thích cấu kiện";
                case EntityRoles.Legend: return "Chú giải";
                case EntityRoles.Equipment: return "Thiết bị";
                case EntityRoles.EquipmentTag: return "Nhãn thiết bị";
                case EntityRoles.Structure: return "Kết cấu";
                case EntityRoles.StructureTag: return "Nhãn kết cấu";
                case EntityRoles.Dimension: return "Kích thước";
                case EntityRoles.Annotation: return "Ghi chú";
                case EntityRoles.TitleBlock: return "Khung tên";
                case EntityRoles.Ignore: return "Bỏ qua";
                case EntityRoles.Unclassified: return "Chưa phân loại";
                default: return role;
            }
        }
    }
}
