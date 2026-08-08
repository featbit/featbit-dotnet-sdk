using FeatBit.Sdk.Server.DependencyInjection;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.DependencyInjection;

namespace FeatBit.Sdk.Server.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void RegistersTheDataChangeNotifierAsTheSameClientInstance()
    {
        var services = new ServiceCollection();
        services.AddFeatBit(new FbOptionsBuilder().Offline(true).Build());

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IFbClient>();
        var notifier = serviceProvider.GetRequiredService<IFbClientDataChangeNotifier>();

        Assert.Same(client, notifier);
    }
}
