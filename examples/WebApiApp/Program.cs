using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.DependencyInjection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using WebApiApp;

var builder = WebApplication.CreateBuilder(args);

// Set secret to your FeatBit SDK secret.
const string secret = "";
if (string.IsNullOrWhiteSpace(secret))
{
    Console.WriteLine("Please edit Program.cs to set secret to your FeatBit SDK secret first. Exiting...");
    Environment.Exit(1);
}

// Note that by default, the FeatBit SDK will use the default logger factory provided by ASP.NET Core.
builder.Services.AddFeatBit(options =>
{
    options.EnvSecret = secret;
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