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

    [Theory]
    [InlineData("123", 123)]
    [InlineData("123.4", 0)]
    [InlineData("v123", 0)]
    public void IntConverter(string value, int expected)
    {
        _ = ValueConverters.Int(value, out var converted);

        Assert.Equal(expected, converted);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("123.45", 123.45)]
    [InlineData("v123.4", 0)]
    public void FloatConverter(string value, float expected)
    {
        _ = ValueConverters.Float(value, out var converted);

        Assert.Equal(expected, converted);
    }
}