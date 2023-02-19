// See https://aka.ms/new-console-template for more information

using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;

// Set secret to your FeatBit SDK secret.
const string secret = "ZLOel0piWk-KNx7KaAx1mQhBtR81DkXE6f_bEXdmrp-A";
if (string.IsNullOrWhiteSpace(secret))
{
    Console.WriteLine("Please edit Program.cs to set secret to your FeatBit SDK secret first ");
    Environment.Exit(1);
}

var client = new FbClient(secret);
if (!client.Initialized)
{
    Console.WriteLine("FbClient failed to initialize. Exiting...");
    Environment.Exit(-1);
}

Console.WriteLine("FbClient successfully initialized!");

while (true)
{
    Console.WriteLine("Please input {userKey}/{flagKey}, for example 'user-id/use-new-algorithm'. Input 'exit' to exit.");
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

// Shuts down the client
await client.CloseAsync();

Environment.Exit(1);