using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Profile;

namespace PipingAgent.Hosting
{
    /// <summary>Đọc hồ sơ quy ước từ đĩa. Hồ sơ cha nằm cùng thư mục, tên <c>&lt;id&gt;.json</c>.</summary>
    public static class ProfileFiles
    {
        public static ProfileLoadResult Load(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
            {
                var missing = new ProfileLoadResult();
                missing.Errors.Add(new ProfileError("$", $"Không tìm thấy tệp hồ sơ: {path}"));
                return missing;
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            return ProfileLoader.Load(File.ReadAllText(path, Encoding.UTF8), id =>
            {
                var parent = Path.Combine(dir, id + ".json");
                return File.Exists(parent) ? File.ReadAllText(parent, Encoding.UTF8) : null;
            });
        }

        /// <summary>Các tệp hồ sơ trong một thư mục, bỏ hồ sơ nền có tên bắt đầu bằng <c>base-</c>.</summary>
        public static List<string> List(string directory)
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return Directory.GetFiles(directory, "*.json")
                .Where(p => !Path.GetFileName(p).StartsWith("base-", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>Nạp danh mục ống và phụ kiện từ một thư mục, mọi tệp <c>*.json</c>.</summary>
    public static class CatalogFiles
    {
        public static PipeCatalog LoadDirectory(string directory)
        {
            var catalog = new PipeCatalog();
            if (!Directory.Exists(directory)) return catalog;
            foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                catalog.AddJson(File.ReadAllText(path, Encoding.UTF8));
            }

            return catalog;
        }
    }
}

namespace PipingAgent.Hosting
{
    /// <summary>Nạp thư viện thiết bị theo ba lớp thư mục: mặc định, công ty, dự án. Lớp sau ghi đè lớp trước theo id.</summary>
    public static class EquipmentFiles
    {
        public static PipingAgent.Core.Equipment.EquipmentLibrary LoadLayers(params string?[] directories)
        {
            var library = new PipingAgent.Core.Equipment.EquipmentLibrary();
            var names = new[] { "default", "company", "project" };
            for (var i = 0; i < directories.Length; i++)
            {
                var dir = directories[i];
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) continue;
                var layer = i < names.Length ? names[i] : "layer" + i;
                foreach (var path in System.IO.Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    library.AddJson(System.IO.File.ReadAllText(path, Encoding.UTF8), layer, System.IO.Path.GetFileName(path));
                }
            }

            return library;
        }
    }
}
