// See https://aka.ms/new-console-template for more information

using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;
using Serilog;

// Set secret to your FeatBit SDK secret.
const string secret = "";
if (string.IsNullOrWhiteSpace(secret))
{
    Console.WriteLine("Please edit Program.cs to set secret to your FeatBit SDK secret first. Exiting...");
    Environment.Exit(1);
}

// Creates a new client to connect to FeatBit with a custom option.

// init serilog
Log.Logger = new LoggerConfiguration()
    // use debug logs when troubleshooting
    .MinimumLevel.Debug()
    .WriteTo.File("featbit-logs.txt")
    .CreateLogger();

var serilogLoggerFactory = LoggerFactory.Create(opt => opt.AddSerilog());
var options = new FbOptionsBuilder(secret)
    .Streaming(new Uri("wss://app-eval.featbit.co"))
    .Event(new Uri("https://app-eval.featbit.co"))
    .LoggerFactory(serilogLoggerFactory)
    .Build();

var client = new FbClient(options);
if (!client.Initialized)
{
    Console.WriteLine("FbClient failed to initialize. Exiting...");
    Environment.Exit(-1);
}

while (true)
{
    Console.WriteLine("Please input userKey/flagKey, for example 'user-id/use-new-algorithm'. Input 'exit' to exit.");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if (input == "exit")
    {
        Console.WriteLine("Exiting, please wait...");
        break;
    }

    var keys = input.Split('/');
    if (keys.Length != 2)
    {
        Console.WriteLine();
        continue;
    }

    var userKey = keys[0];
    var flagKey = keys[1];

    var user = FbUser.Builder(userKey).Build();

    var detail = client.StringVariationDetail(flagKey, user, "fallback");
    Console.WriteLine($"Kind: {detail.Kind}, Reason: {detail.Reason}, Value: {detail.Value}");
    Console.WriteLine();
}

// Shuts down the client to ensure all pending events are sent.
await client.CloseAsync();

Environment.Exit(1);