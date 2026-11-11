using System;
using System.Collections.Generic;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;

namespace PipingAgent.Core.Rules
{
    /// <summary>Định nghĩa một luật kiểm tra: mã, mức mặc định, mô tả. Danh mục ở mục 9.3 của tài liệu dự án.</summary>
    public sealed class RuleDefinition
    {
        public RuleDefinition(string id, Severity defaultSeverity, string description, int phase)
        {
            Id = id;
            DefaultSeverity = defaultSeverity;
            Description = description;
            Phase = phase;
        }

        public string Id { get; }

        public Severity DefaultSeverity { get; }

        public string Description { get; }

        public int Phase { get; }
    }

    /// <summary>Danh mục luật và cách tra mức áp dụng: hồ sơ quy ước ghi đè được mức mặc định, kể cả tắt luật.</summary>
    public static class RuleCatalog
    {
        private static readonly Dictionary<string, RuleDefinition> Rules = Build();

        public static IReadOnlyCollection<RuleDefinition> All => Rules.Values;

        public static RuleDefinition? Find(string id) => Rules.TryGetValue(id, out var r) ? r : null;

        /// <summary>Mức áp dụng của luật theo hồ sơ. Null nếu luật bị tắt.</summary>
        public static Severity? Resolve(ConventionProfile? profile, string ruleId)
        {
            if (profile != null && profile.Rules.TryGetValue(ruleId, out var level))
            {
                switch (level)
                {
                    case "off": return null;
                    case "blocker": return Severity.Blocker;
                    case "warning": return Severity.Warning;
                    case "info": return Severity.Info;
                }
            }

            return Find(ruleId)?.DefaultSeverity ?? Severity.Warning;
        }

        private static Dictionary<string, RuleDefinition> Build()
        {
            var list = new[]
            {
                new RuleDefinition("R-TAG-01", Severity.Blocker, "Đoạn chạy không có nhãn và không thừa hưởng được từ đoạn nối tiếp", 1),
                new RuleDefinition("R-TAG-02", Severity.Warning, "Một nhãn có từ hai đoạn chạy ứng viên với điểm tin cậy gần nhau", 1),
                new RuleDefinition("R-TAG-03", Severity.Warning, "Văn bản trên layer nhãn không khớp mẫu nhãn nào", 1),
                new RuleDefinition("R-TAG-04", Severity.Warning, "Nhãn không gắn được với đoạn chạy nào trong bán kính tìm kiếm", 1),
                new RuleDefinition("R-TAG-05", Severity.Blocker, "Một đoạn chạy nhận hai nhãn mâu thuẫn nhau", 1),
                new RuleDefinition("R-TAG-06", Severity.Blocker, "DN đọc được không có trong danh mục", 1),
                new RuleDefinition("R-CON-01", Severity.Warning, "Đầu ống hở, không phải đầu chờ thiết bị và không phải điểm nối tiếp", 1),
                new RuleDefinition("R-CON-02", Severity.Warning, "Hai đầu ống cách nhau một khe nhỏ, lớn hơn dung sai bắt điểm nhưng nhỏ hơn ngưỡng nghi ngờ", 1),
                new RuleDefinition("R-CON-03", Severity.Warning, "Hai đoạn ống cùng cao độ cắt nhau trên mặt bằng mà không có nút", 1),
                new RuleDefinition("R-CON-04", Severity.Blocker, "Các đoạn ống tại một nút có DN khác nhau mà nút không phải côn thu hoặc tê thu", 1),
                new RuleDefinition("R-CON-05", Severity.Warning, "Đầu ống kết thúc gần thiết bị nhưng không chạm biên thiết bị", 1),
                new RuleDefinition("R-CON-06", Severity.Info, "Cùng số hiệu tuyến nhưng gồm nhiều phần không nối với nhau", 2),
                new RuleDefinition("R-GEO-01", Severity.Warning, "Góc đổi hướng không thuộc tập góc cho phép trong hồ sơ", 1),
                new RuleDefinition("R-GEO-02", Severity.Blocker, "Đoạn ống ngắn hơn tổng phần bị phụ kiện hai đầu chiếm chỗ", 1),
                new RuleDefinition("R-GEO-03", Severity.Warning, "Đoạn thẳng còn lại giữa hai phụ kiện ngắn hơn mức tối thiểu cấu hình", 2),
                new RuleDefinition("R-GEO-04", Severity.Blocker, "Cấu kiện nối tiếp chồng lên nhau hoặc chồng lên phụ kiện ở đầu đoạn", 1),
                new RuleDefinition("R-GEO-05", Severity.Blocker, "Đoạn ống có chiều dài bằng không hoặc trùng hoàn toàn với đoạn khác", 1),
                new RuleDefinition("R-GEO-06", Severity.Warning, "Cấu kiện nối tiếp không nằm trên đoạn ống nào trong dung sai", 1),
                new RuleDefinition("R-ELV-01", Severity.Info, "Có đổi cao độ nhưng không có ký hiệu ống đứng tại điểm đó", 1),
                new RuleDefinition("R-ELV-02", Severity.Warning, "Có ký hiệu ống đứng nhưng không có đổi cao độ tương ứng", 1),
                new RuleDefinition("R-ELV-03", Severity.Warning, "Cao độ nằm ngoài khoảng hợp lý khai báo trong hồ sơ", 1),
                new RuleDefinition("R-ELV-04", Severity.Warning, "Ống chạy trên giá đỡ nhưng đáy ống lệch mặt trên kết cấu quá dung sai", 2),
                new RuleDefinition("R-ELV-05", Severity.Blocker, "Ngữ nghĩa cao độ không xác định: không rõ là tâm, đáy hay đỉnh ống", 1),
                new RuleDefinition("R-CLH-01", Severity.Warning, "Ống giao với ống của tuyến khác", 2),
                new RuleDefinition("R-CLH-02", Severity.Warning, "Ống giao với thiết bị, trừ tại đầu chờ", 2),
                new RuleDefinition("R-CLH-03", Severity.Warning, "Ống giao với kết cấu", 2),
                new RuleDefinition("R-CMP-01", Severity.Warning, "Chú thích cấu kiện không gắn được với cấu kiện nào trong bán kính tìm kiếm", 2),
                new RuleDefinition("R-CMP-02", Severity.Warning, "Một chú thích có từ hai cấu kiện ứng viên với điểm tin cậy gần nhau", 2),
                new RuleDefinition("R-CMP-03", Severity.Warning, "DN ghi trên chú thích cấu kiện khác DN của đoạn ống chứa nó", 2),
                new RuleDefinition("R-EQP-01", Severity.Info, "Thiết bị không có số hiệu", 1),
                new RuleDefinition("R-EQP-02", Severity.Warning, "Thiết bị thiếu kích thước cần để dựng", 1),
                new RuleDefinition("R-EQP-03", Severity.Warning, "DN của ống khác DN của đầu chờ thiết bị mà nó nối vào", 2),
                new RuleDefinition("R-EQP-04", Severity.Warning, "Loại thiết bị chưa có định nghĩa trong thư viện thiết bị, dựng tạm bằng hộp bao", 1),
                new RuleDefinition("R-CAT-01", Severity.Blocker, "Không tìm thấy phụ kiện trong danh mục cho DN và tiêu chuẩn này", 1),
                new RuleDefinition("R-CAT-02", Severity.Warning, "Block trên layer cấu kiện chưa được ánh xạ trong hồ sơ", 1),
                new RuleDefinition("R-CAT-03", Severity.Info, "Danh mục ống và phụ kiện chưa được chuyên gia ký xác nhận", 1),
                new RuleDefinition("R-GEN-01", Severity.Warning, "Tỷ lệ đối tượng chưa phân loại vượt ngưỡng cấu hình", 1),
                new RuleDefinition("R-GEN-02", Severity.Blocker, "Không xác định được đơn vị bản vẽ", 1),
                new RuleDefinition("R-DEC-01", Severity.Warning, "Quyết định của người dùng không còn khớp đối tượng nào", 1),
            };
            var d = new Dictionary<string, RuleDefinition>(StringComparer.Ordinal);
            foreach (var r in list) d[r.Id] = r;
            return d;
        }
    }
}
