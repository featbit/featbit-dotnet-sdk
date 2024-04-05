namespace FeatBit.Sdk.Server;

public class UriTests
{
    // According to the documentation: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-6.0#system-uri-ctor(system-uri-system-string)
    // if the relative part of baseUri is to be preserved in the constructed Uri, 
    // the baseUri has relative parts (like /api), then the relative part must be terminated with a slash, (like /api/) 
    [Theory]
    [InlineData("https://contoso.com/featbit/", "relative", "https://contoso.com/featbit/relative")]
    [InlineData("https://contoso.com/featbit/", "relative?type=server", "https://contoso.com/featbit/relative?type=server")]
    [InlineData("https://contoso.com", "relative", "https://contoso.com/relative")]
    [InlineData("https://contoso.com", "/relative", "https://contoso.com/relative")]
    [InlineData("https://contoso.com/", "/relative", "https://contoso.com/relative")]
    public void CreateUri(string @base, string relative, string expected)
    {
        var baseUri = new Uri(@base);
        var uri = new Uri(baseUri, relative);

        Assert.Equal(expected, uri.ToString());
    }
}