using FeatBit.Sdk.Server;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApiApp;

public class FeatBitHealthCheck : IHealthCheck
{
    private readonly IFbClient _fbClient;

    public FeatBitHealthCheck(IFbClient fbClient)
    {
        _fbClient = fbClient;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _fbClient.Status;

        var result = status switch
        {
            FbClientStatus.Ready => HealthCheckResult.Healthy(),
            FbClientStatus.Stale => HealthCheckResult.Degraded(),
            _ => HealthCheckResult.Unhealthy()
        };

        return Task.FromResult(result);
    }
}