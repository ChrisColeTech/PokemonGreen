using Xunit;

public class CliConventionsTests
{
    [Theory]
    [InlineData("dae", "dae")]
    [InlineData("DAE", "dae")]
    [InlineData(" obj ", "obj")]
    public void TryNormalizeFormat_AcceptsSupportedFormats(string input, string expected)
    {
        bool ok = CliConventions.TryNormalizeFormat(input, out string normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fbx")]
    public void TryNormalizeFormat_RejectsUnsupportedFormats(string input)
    {
        bool ok = CliConventions.TryNormalizeFormat(input, out string normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void AggregateExitCode_ReturnsSuccess_WhenNoFailures()
    {
        int code = CliConventions.AggregateExitCode(successCount: 3, partialCount: 0, fatalCount: 0);
        Assert.Equal(CliConventions.ExitSuccess, code);
    }

    [Fact]
    public void AggregateExitCode_ReturnsPartial_WhenMixedResults()
    {
        int code = CliConventions.AggregateExitCode(successCount: 2, partialCount: 1, fatalCount: 1);
        Assert.Equal(CliConventions.ExitPartial, code);
    }

    [Fact]
    public void AggregateExitCode_ReturnsFatal_WhenEverythingFailedFatally()
    {
        int code = CliConventions.AggregateExitCode(successCount: 0, partialCount: 0, fatalCount: 2);
        Assert.Equal(CliConventions.ExitFatal, code);
    }
}
