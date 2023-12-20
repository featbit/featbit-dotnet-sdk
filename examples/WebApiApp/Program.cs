using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var consoleLogger = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));

builder.Services.AddFeatBit(options =>
{
    options.LoggerFactory = consoleLogger;
    options.EnvSecret = "replace-with-your-env-secret";
    options.StartWaitTime = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

// curl -X GET --location "http://localhost:5014/variation-detail/game-runner?fallbackValue=lol"
app.MapGet("/variation-detail/{flagKey}", (IFbClient fbClient, string flagKey, string fallbackValue) =>
{
    var user = FbUser.Builder("tester-id").Name("tester").Build();

    return fbClient.StringVariationDetail(flagKey, user, defaultValue: fallbackValue);
});

app.Run();