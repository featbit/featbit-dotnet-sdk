using System.Text.Json;
using FeatBit.Sdk.Server.Bootstrapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Options;

[UsesVerify]
public class FbOptionsBuilderTests
{
    [Fact]
    public async Task HasDefaultValues()
    {
        var builder = new FbOptionsBuilder("secret");
        var options = builder.Build();

        Assert.IsType<NullLoggerFactory>(options.LoggerFactory);
        Assert.IsType<NullBootstrapProvider>(options.BootstrapProvider);

        await Verify(options)
            // max flush worker depends on the number of processors available on the machine
            .ScrubMember<FbOptions>(x => x.MaxFlushWorker)
            .IgnoreMembers<FbOptions>(x => x.LoggerFactory, x => x.BootstrapProvider);
    }

    [Fact]
    public void NoSecretProvided()
    {
        var options = new FbOptionsBuilder().Build();

        Assert.Equal(string.Empty, options.EnvSecret);
    }

    [Fact]
    public void SetBootstrapProvider()
    {
        var data = new
        {
            messageType = "data-sync",
            data = new
            {
                eventType = "full",
                featureFlags = Array.Empty<object>(),
                segments = Array.Empty<object>()
            }
        };

        var json = JsonSerializer.Serialize(data);

        _ = new FbOptionsBuilder()
            .Offline(true)
            .UseJsonBootstrapProvider(json)
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => new FbOptionsBuilder()
                .UseJsonBootstrapProvider(json)
                .Build()
        );
    }
}