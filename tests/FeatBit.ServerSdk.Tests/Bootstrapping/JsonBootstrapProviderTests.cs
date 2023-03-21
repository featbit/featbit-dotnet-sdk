using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Bootstrapping;

[UsesVerify]
public class JsonBootstrapProviderTests
{
    [Fact]
    public async Task UseValidJson()
    {
        var provider = new JsonBootstrapProvider(TestData.BootstrapJson);

        var dataSet = provider.DataSet();
        await Verify(dataSet);
    }

    [Fact]
    public void UseInvalidJson()
    {
        Assert.ThrowsAny<Exception>(() => new JsonBootstrapProvider("{"));
    }

    [Fact]
    public async Task PopulateStore()
    {
        var provider = new JsonBootstrapProvider(TestData.BootstrapJson);
        var store = new DefaultMemoryStore();

        provider.Populate(store);

        Assert.True(store.Populated);

        var flag = store.Get<FeatureFlag>("ff_example-flag");
        var segment = store.Get<Segment>("segment_0779d76b-afc6-4886-ab65-af8c004273ad");

        await Verify(new { flag, segment });
    }
}