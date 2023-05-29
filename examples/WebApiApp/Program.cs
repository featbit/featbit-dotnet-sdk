using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFeatBit(options =>
{
    options.EnvSecret = "V_QtCb3oQkSWeSD7sya1ug2ExZ3qAofkazo0VHUeKkng";
    options.StartWaitTime = TimeSpan.FromSeconds(3);
    options.LoggerFactory = LoggerFactory.Create(x => x.AddConsole());
});

var app = builder.Build();

app.MapGet("/variation-detail/{flagKey}", (IFbClient fbClient, string flagKey, string fallbackValue) =>
{
    var user = FbUser.Builder("tester-id").Name("tester").Build();

    return fbClient.StringVariationDetail(flagKey, user, defaultValue: fallbackValue);
});

app.Run();