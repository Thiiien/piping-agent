using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PipingAgent.Core.Ir
{
    /// <summary>Ai đã quyết định một giá trị.</summary>
    public enum DecidedBy
    {
        Rule,
        Agent,
        User,
    }

    /// <summary>Nguồn gốc của một phần tử nhận diện được. Có trên mọi thực thể để truy vết theo FR-ING-07.</summary>
    public sealed class Provenance
    {
        /// <summary>Định danh các đối tượng nguồn 2D, tức handle của bản vẽ.</summary>
        public List<string> Sources { get; set; } = new List<string>();

        /// <summary>Mã quy tắc nhận diện đã tạo ra phần tử.</summary>
        public string? Rule { get; set; }

        /// <summary>Điểm tin cậy từ 0 đến 1.</summary>
        public double Confidence { get; set; } = 1.0;

        public DecidedBy DecidedBy { get; set; } = DecidedBy.Rule;

        /// <summary>Đúng nếu phần tử được suy ra, không có đối tượng nguồn trực tiếp. Ví dụ ống đứng.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Inferred { get; set; }
    }

    /// <summary>Nhãn đã gắn vào một đoạn chạy hoặc cấu kiện, và cách gắn.</summary>
    public sealed class TagRef
    {
        /// <summary>Nguyên văn chữ trên bản vẽ sau khi gộp các mảnh.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Cách gắn: <c>block_attribute</c>, <c>leader</c>, <c>adjacent</c>, <c>nearest</c>, <c>user</c>. Xem mục 7.9.3.</summary>
        public string By { get; set; } = string.Empty;

        /// <summary>Handle của đối tượng chữ chính.</summary>
        public string? SourceHandle { get; set; }

        /// <summary>Handle của các đối tượng chữ phụ đã gộp vào, nếu nhãn bị cắt làm nhiều mảnh.</summary>
        public List<string>? MergedHandles { get; set; }

        public Geometry.Point2? Position { get; set; }

        /// <summary>Điểm tin cậy của việc gắn nhãn, 0 đến 1. Null nghĩa là chưa chấm, coi như 1.</summary>
        public double? Confidence { get; set; }
    }
}
