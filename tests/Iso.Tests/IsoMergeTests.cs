using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Hosting;
using PipingAgent.Iso;
using Xunit;

namespace Iso.Tests;

public class IsoMergeTests
{
    private static PipeCatalog? LoadCatalog()
    {
        var catalogDir = Path.Combine(AppContext.BaseDirectory, "catalog");
        return Directory.Exists(catalogDir) ? CatalogFiles.LoadDirectory(catalogDir) : null;
    }

    [Fact]
    public void Merge_CombinesTwoSheets_WithPrefixesAndStubs()
    {
        var dwg1 = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "3E204-1-250A-SUS-Sheet1.DWG");
        var dwg2 = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "3E204-1-350A-SUS_02-Sheet1.DWG");
        Assert.True(dwg1 != null && dwg2 != null, "DWG test fixtures must exist.");

        var catalog = LoadCatalog();
        var run1 = IsoPipeline.Run(dwg1!, catalog);
        var run2 = IsoPipeline.Run(dwg2!, catalog);

        var sheets = new List<MergeSheet>
        {
            new MergeSheet { Name = "250A", Model = run1.Model, Offset = new Vector3(0, 0, 0), OffsetBy = "tờ gốc" },
            new MergeSheet { Name = "350A", Model = run2.Model, Offset = new Vector3(-3000, 266, 0), OffsetBy = "người dùng chỉ định" },
        };

        var stubs = new List<ReferenceStub>
        {
            new ReferenceStub { Tag = "3E204-1-600A-SUS", At = new Point3(0, 0, 5605), Axis = "E", Dn = 600, HalfLength = 1500 },
        };

        var origPoint = run2.Model.Runs[0].PlanPoints[0];

        var merged = IsoMerge.Merge(sheets, stubs, "3E204-1-250A+350A");

        Assert.NotNull(merged);
        Assert.Contains(merged.Lines, l => l.Id.StartsWith("s1-"));
        Assert.Contains(merged.Lines, l => l.Id.StartsWith("s2-"));
        Assert.Contains(merged.Lines, l => l.Id.StartsWith("ref-line-"));

        Assert.Contains(merged.Runs, r => r.Id.StartsWith("s1-"));
        Assert.Contains(merged.Runs, r => r.Id.StartsWith("s2-"));
        Assert.Contains(merged.Runs, r => r.Id.StartsWith("ref-run-"));

        // Kiểm tra toạ độ của sheet 2 đã được dịch theo Offset (-3000, 266, 0)
        var s2Run = merged.Runs.First(r => r.Id.StartsWith("s2-"));
        Assert.Equal(origPoint.X - 3000, s2Run.PlanPoints[0].X, precision: 2);
        Assert.Equal(origPoint.Y + 266, s2Run.PlanPoints[0].Y, precision: 2);

        // Kiểm tra tham chiếu stub
        var stubRun = merged.Runs.First(r => r.Id.StartsWith("ref-run-"));
        Assert.Equal(600, stubRun.Dn);
        Assert.NotNull(stubRun.Prov);
        Assert.Equal(0.3, stubRun.Prov.Confidence);

        // Kiểm tra báo cáo kiểm kê gộp
        var report = IsoMerge.Report(sheets, stubs, merged, "3E204-1-250A+350A.dwg");
        Assert.NotEmpty(report);
        Assert.Contains("Kiểm kê gộp nhiều tờ", report);
        Assert.Contains("250A", report);
        Assert.Contains("350A", report);
        Assert.Contains("3E204-1-600A-SUS", report);
        Assert.Contains("Ống tham chiếu vẽ thêm", report);
    }
}
