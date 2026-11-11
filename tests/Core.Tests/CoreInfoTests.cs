using PipingAgent.Core;

namespace Core.Tests;

public class CoreInfoTests
{
    [Fact]
    public void Core_uses_millimetres_and_a_versioned_schema()
    {
        Assert.Equal("mm", CoreInfo.Units);
        Assert.Matches(@"^\d+\.\d+$", CoreInfo.IrSchemaVersion);
    }
}
