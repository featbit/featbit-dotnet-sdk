using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace FeatBit.Sdk.Server.DependencyInjection;

/// <summary>
/// The FbClientHostedService performs the following tasks:
/// <list type="number">
/// <item>
///     <description>
///     The <see cref="FbClientHostedService(IFbClient)"/> constructor ensures that the FbClient is created before the application starts.
///     </description>
/// </item>
/// <item>
///     <description>
///     The <see cref="StopAsync"/> method closes the FbClient when the application host is performing a graceful shutdown.
///     </description>
/// </item>
/// </list>
/// </summary>
public class FbClientHostedService : IHostedService
{
    private readonly IFbClient _fbClient;

    public FbClientHostedService(IFbClient fbClient)
    {
        _fbClient = fbClient;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _fbClient.CloseAsync();
    }
}