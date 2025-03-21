using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.DependencyInjection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using WebApiApp;

var builder = WebApplication.CreateBuilder(args);

// Note that by default, the FeatBit SDK will use the default logger factory provided by ASP.NET Core.
builder.Services.AddFeatBit(options =>
{
    options.EnvSecret = "<replace-with-your-env-secret>";
    options.StreamingUri = new Uri("wss://app-eval.featbit.co");
    options.EventUri = new Uri("https://app-eval.featbit.co");
    options.StartWaitTime = TimeSpan.FromSeconds(3);
});

builder.Services.AddHealthChecks()
    .AddCheck<FeatBitHealthCheck>("FeatBit");

var app = builder.Build();

// curl -X GET --location http://localhost:5014/healthz
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// curl -X GET --location http://localhost:5014/variation-detail/game-runner?fallbackValue=lol
app.MapGet("/variation-detail/{flagKey}", (IFbClient fbClient, string flagKey, string fallbackValue) =>
{
    var user = FbUser.Builder("tester-id").Name("tester").Build();

    return fbClient.StringVariationDetail(flagKey, user, defaultValue: fallbackValue);
});

app.Run();