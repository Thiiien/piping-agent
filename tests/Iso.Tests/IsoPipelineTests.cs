using System;
using System.IO;
using System.Linq;
using PipingAgent.Core.Catalog;
using PipingAgent.Hosting;
using PipingAgent.Iso;
using Xunit;

namespace Iso.Tests;

public class IsoPipelineTests
{
    private static PipeCatalog? LoadCatalog()
    {
        var catalogDir = Path.Combine(AppContext.BaseDirectory, "catalog");
        return Directory.Exists(catalogDir) ? CatalogFiles.LoadDirectory(catalogDir) : null;
    }

    [Fact]
    public void Pipeline_RunsOnVectorPdf_335PA286()
    {
        var pdfPath = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "335-PA-286.pdf");
        Assert.True(pdfPath != null, "335-PA-286.pdf should exist.");

        var catalog = LoadCatalog();
        var result = IsoPipeline.Run(pdfPath!, catalog);

        Assert.NotNull(result);
        Assert.False(result.IsDwg);
        Assert.Contains("335-PA-286", result.Analysis.LineTag);
        Assert.Equal(350, result.Analysis.Dn);
        Assert.NotEmpty(result.Analysis.Anchors);
        Assert.NotEmpty(result.Analysis.Segments);
        Assert.NotEmpty(result.Model.Segments);
        Assert.NotEmpty(result.Model.Components);
        Assert.NotEmpty(result.Model.Nodes);

        // Kiểm tra khép kín: mọi neo và cao độ khép kín dưới 5mm
        var closureChecks = result.Extraction.Checks.Where(c => c.Group == "Khép kín").ToList();
        Assert.NotEmpty(closureChecks);
        foreach (var c in closureChecks)
        {
            Assert.Equal("ok", c.Status);
        }
    }

    [Fact]
    public void Pipeline_RunsOnIsogenDwg_250ASheet1()
    {
        var dwgPath = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "3E204-1-250A-SUS-Sheet1.DWG");
        Assert.True(dwgPath != null, "3E204-1-250A-SUS-Sheet1.DWG should exist.");

        var catalog = LoadCatalog();
        var result = IsoPipeline.Run(dwgPath!, catalog);

        Assert.NotNull(result);
        Assert.True(result.IsDwg);
        Assert.True(result.Isogen);
        Assert.Equal("3E204-1-250A-SUS", result.Analysis.LineTag);
        Assert.Equal(250, result.Analysis.Dn);
        Assert.True(result.Extraction.Complete, "Bản vẽ 250A phải đủ để dựng (Complete = true).");
        Assert.NotEmpty(result.Analysis.Bom);

        // Có các linh kiện: co, mặt bích, van bướm, côn thu
        Assert.Contains(result.Model.Components, c => c.Type == "valve_butterfly" && c.Dn == 250);
        Assert.Contains(result.Model.Components, c => c.Type == "reducer" && c.Dn == 350);
        Assert.Contains(result.Model.Components, c => c.Type == "flange");
    }

    [Fact]
    public void Pipeline_RunsOnIsogenDwg_350ASheet1_WithSkewedOffset()
    {
        var dwgPath = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "3E204-1-350A-SUS_02-Sheet1.DWG");
        Assert.True(dwgPath != null, "3E204-1-350A-SUS_02-Sheet1.DWG should exist.");

        var catalog = LoadCatalog();
        var result = IsoPipeline.Run(dwgPath!, catalog);

        Assert.NotNull(result);
        Assert.True(result.IsDwg);
        Assert.True(result.Isogen);
        Assert.Equal("3E204-1-350A-SUS_02", result.Analysis.LineTag);
        Assert.Equal(350, result.Analysis.Dn);
        Assert.True(result.Extraction.Complete, "Bản vẽ 350A phải đủ để dựng (Complete = true).");

        // Đoạn lệch 45 độ: có đoạn ống 761mm (lệch ngang 538mm x 538mm)
        Assert.Contains(result.Model.Segments, s => Math.Abs(s.Length - 761) < 2);
        // Có co 45 độ trong BOM
        Assert.Contains(result.Analysis.Bom, b => b.Type == "elbow" && b.Description.Contains("45 DEG"));

        // Van bướm có hướng xoay tay van WEST theo ghi chú ORIENT HANDLE WEST
        var valve = result.Model.Components.FirstOrDefault(c => c.Type == "valve_butterfly");
        Assert.NotNull(valve);
        Assert.NotNull(valve!.Attributes);
        Assert.True(valve.Attributes.ContainsKey("handle_orient"));
        Assert.Equal("WEST", valve.Attributes["handle_orient"].Value);
        Assert.Equal("-1,0,0", valve.Attributes["branch"].Value);
    }
}
