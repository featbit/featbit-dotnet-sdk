namespace FeatBit.Sdk.Server.Model;

public class FbUserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing")]
    public void ValueOfReturnsNullWhenAttributeDoesNotExist(string property)
    {
        var user = FbUser.Builder("user-key").Build();

        Assert.Null(user.ValueOf(property));
    }

    [Fact]
    public void ValueOfReturnsEmptyStringWhenAttributeExistsWithEmptyValue()
    {
        var user = FbUser.Builder("user-key")
            .Custom("empty", string.Empty)
            .Build();

        Assert.Equal(string.Empty, user.ValueOf("empty"));
    }
}
