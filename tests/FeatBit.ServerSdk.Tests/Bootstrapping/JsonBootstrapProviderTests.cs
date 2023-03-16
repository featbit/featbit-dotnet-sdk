namespace FeatBit.Sdk.Server.Bootstrapping;

[UsesVerify]
public class JsonBootstrapProviderTests
{
    [Fact]
    public async Task UseValidJson()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, @"Bootstrapping\featbit-bootstrap.json")
        );

        var provider = new JsonBootstrapProvider(json);

        var dataSet = provider.DataSet();
        await Verify(dataSet);
    }

    [Fact]
    public void UseInvalidJson()
    {
        Assert.ThrowsAny<Exception>(() => new JsonBootstrapProvider("{"));
    }
}