using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using PipingAgent.Core;
using PipingAgent.Tools;

namespace Architecture.Tests;

/// <summary>
/// Kiểm tra tự động cho NFR-MNT-09 và các quy tắc ở mục 6.12.3 của docs/TAI_LIEU_DU_AN.md:
/// lõi và lớp công cụ không tham chiếu CAD, giao diện, mạng, và không tự đọc tệp hay registry.
/// Muốn thêm một thư viện vào lõi thì phải sửa danh sách cho phép ở đây, tức là phải có chủ ý.
/// </summary>
public class IsolationTests
{
    private static readonly string[] ForbiddenNamespaces =
    {
        "Autodesk",              // AutoCAD, Plant 3D
        "Ingr",                  // Smart 3D
        "Intergraph",
        "System.Windows",        // WPF, Windows Forms
        "System.Net",            // Mạng
        "Microsoft.Win32",       // Registry
    };

    // Lõi không tự đọc tệp. Ứng dụng chủ truyền dữ liệu vào qua Stream hoặc TextReader.
    private static readonly string[] ForbiddenTypes =
    {
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileStream",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
    };

    public static TheoryData<string, string[]> IsolatedAssemblies => new()
    {
        // System.Text.Json là thư viện bên thứ ba duy nhất của lõi, quyết định ngày 10/11/2026, xem csproj của lõi.
        // System.Memory là gói nền mà System.Text.Json kéo theo trên .NET Standard 2.0 (Span), không phải thư viện mới.
        { typeof(CoreInfo).Assembly.Location, new[] { "netstandard", "System.Text.Json", "System.Memory" } },
        { typeof(ToolsInfo).Assembly.Location, new[] { "netstandard", "System.Text.Json", "System.Memory", "PipingAgent.Core" } },
    };

    public static TheoryData<string, string[], string[]> IsolatedProjects => new()
    {
        { "src/PipingAgent.Core/PipingAgent.Core.csproj", new[] { "System.Text.Json" }, Array.Empty<string>() },
        { "src/PipingAgent.Tools/PipingAgent.Tools.csproj", new[] { "System.Text.Json" }, new[] { "PipingAgent.Core" } },
    };

    [Theory]
    [MemberData(nameof(IsolatedAssemblies))]
    public void Assembly_references_only_allowed_assemblies(string assemblyPath, string[] allowed)
    {
        using var metadata = OpenMetadata(assemblyPath, out var reader);

        var unexpected = reader.AssemblyReferences
            .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
            .Where(name => !allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(unexpected.Length == 0,
            $"{Path.GetFileName(assemblyPath)} tham chiếu thư viện ngoài danh sách cho phép: {string.Join(", ", unexpected)}");
    }

    [Theory]
    [MemberData(nameof(IsolatedAssemblies))]
    public void Assembly_uses_no_cad_ui_network_or_file_types(string assemblyPath, string[] allowed)
    {
        _ = allowed;
        using var metadata = OpenMetadata(assemblyPath, out var reader);

        var violations = new List<string>();
        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);
            var ns = reader.GetString(reference.Namespace);
            var fullName = ns.Length == 0 ? reader.GetString(reference.Name) : ns + "." + reader.GetString(reference.Name);

            var badNamespace = ForbiddenNamespaces.Any(prefix =>
                ns.Equals(prefix, StringComparison.Ordinal) || ns.StartsWith(prefix + ".", StringComparison.Ordinal));
            if (badNamespace || ForbiddenTypes.Contains(fullName, StringComparer.Ordinal))
            {
                violations.Add(fullName);
            }
        }

        Assert.True(violations.Count == 0,
            $"{Path.GetFileName(assemblyPath)} dùng kiểu bị cấm: {string.Join(", ", violations.Distinct())}");
    }

    [Theory]
    [MemberData(nameof(IsolatedProjects))]
    public void Project_file_declares_only_allowed_dependencies(string relativePath, string[] allowedPackages, string[] allowedProjects)
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), relativePath));

        var packages = project.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(name => !allowedPackages.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var projects = project.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
            .Where(name => !allowedProjects.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(packages.Length == 0 && projects.Length == 0,
            $"{relativePath} khai báo phụ thuộc ngoài danh sách cho phép: {string.Join(", ", packages.Concat(projects))}");
    }

    private static PEReader OpenMetadata(string assemblyPath, out MetadataReader reader)
    {
        var pe = new PEReader(File.OpenRead(assemblyPath));
        reader = pe.GetMetadataReader();
        return pe;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PipingAgent.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Không tìm thấy PipingAgent.sln.");
    }
}
