using System.IO;
using System.Linq;
using System.Text;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Hosting
{
    /// <summary>Đọc tệp IR từ đĩa, là ranh giới tin cậy: ngoài cú pháp còn kiểm tra định danh và tham chiếu bằng <see cref="ModelValidator"/>.</summary>
    public static class IrFiles
    {
        public static PipingModel Load(string path)
        {
            var model = IrJson.Deserialize(File.ReadAllText(path, Encoding.UTF8));
            var errors = ModelValidator.Validate(model).Where(f => f.IsError).ToList();
            if (errors.Count > 0)
            {
                throw new IrFormatException($"IR {Path.GetFileName(path)} không hợp lệ: " + string.Join("; ", errors.Take(5).Select(e => e.Path + " " + e.Message)));
            }

            return model;
        }
    }
}
