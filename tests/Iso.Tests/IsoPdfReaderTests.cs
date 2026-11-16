using PipingAgent.Iso;
using Xunit;

namespace Iso.Tests;

public class IsoPdfReaderTests
{
    [Fact]
    public void Read_VectorPdf_ExtractsPageAndEntities()
    {
        var pdfPath = IsoTestHelper.FindRepoFile("docs", "dataTemplate", "335-PA-286.pdf");
        Assert.True(pdfPath != null, "Sample PDF file should exist.");

        var pages = IsoPdfReader.Read(pdfPath!);
        Assert.NotEmpty(pages);
        var page = pages[0];
        Assert.Equal(1, page.Number);
        Assert.NotEmpty(page.Entities);
        Assert.True(page.Width > 0);
        Assert.True(page.Height > 0);
    }
}
