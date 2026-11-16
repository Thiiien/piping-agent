using System.Collections.Generic;
using PipingAgent.Core.Source;
using PipingAgent.Iso;
using Xunit;

namespace Iso.Tests;

public class IsoDxfReaderTests
{
    [Fact]
    public void LooksLikeIsogen_ReturnsTrue_WhenIsogenLayersPresent()
    {
        var entities = new List<SourceEntity>
        {
            new SourceEntity { Layer = "PIPE", Kind = SourceKind.Line },
            new SourceEntity { Layer = "DIMLINES", Kind = SourceKind.Line },
            new SourceEntity { Layer = "ISOTEXT", Kind = SourceKind.Text },
        };

        Assert.True(IsoDxfReader.LooksLikeIsogen(entities));
    }

    [Fact]
    public void LooksLikeIsogen_ReturnsFalse_When2DOrEmptyLayers()
    {
        var entities = new List<SourceEntity>
        {
            new SourceEntity { Layer = "0_PIPE_CENTER", Kind = SourceKind.Line },
            new SourceEntity { Layer = "0_EQUIPMENT", Kind = SourceKind.Line },
            new SourceEntity { Layer = "0_TEXT", Kind = SourceKind.Text },
        };

        Assert.False(IsoDxfReader.LooksLikeIsogen(entities));
        Assert.False(IsoDxfReader.LooksLikeIsogen(new List<SourceEntity>()));
    }

    [Fact]
    public void Read_RealDwg_ExtractsEntitiesAndSetsDimensions()
    {
        var dwgPath = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "3E204-1-250A-SUS-Sheet1.DWG");
        Assert.True(dwgPath != null, "Sample DWG file should exist.");

        var page = IsoDxfReader.Read(dwgPath!);
        Assert.NotNull(page);
        Assert.Equal(1, page.Number);
        Assert.NotEmpty(page.Entities);
        Assert.True(page.Width > 0);
        Assert.True(page.Height > 0);
        Assert.True(IsoDxfReader.LooksLikeIsogen(page.Entities));
    }
}
