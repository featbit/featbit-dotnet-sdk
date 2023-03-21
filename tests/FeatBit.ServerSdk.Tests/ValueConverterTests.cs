namespace FeatBit.Sdk.Server;

public class ValueConverterTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    public void BoolConverter(string value, bool expected)
    {
        _ = ValueConverters.Bool(value, out var converted);

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void StringConverter()
    {
        var success = ValueConverters.String("hello", out var converted);

        Assert.True(success);
        Assert.Equal("hello", converted);
    }
}