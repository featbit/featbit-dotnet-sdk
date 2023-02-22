# FeatBit Server-Side SDK for .NET

## Introduction

This is the .NET Server-Side SDK for the 100% open-source feature flags management
platform [FeatBit](https://github.com/featbit/featbit).

The FeatBit Server-Side SDK for .NET is designed primarily for use in multi-user systems such as web servers and
applications. It is not intended for use in desktop and embedded systems applications.

## Supported .NET versions

This version of the SDK is built for the following targets:

- .NET 6.0: runs on .NET 6.0 and above (including higher major versions).
- .NET Core 3.1: runs on .NET Core 3.1+.
- .NET Framework 4.6.2: runs on .NET Framework 4.6.2 and above.
- .NET Standard 2.0/2.1: runs in any project that is targeted to .NET Standard 2.x rather than to a specific runtime
  platform.

The .NET build tools should automatically load the most appropriate build of the SDK for whatever platform your
application or library is targeted to.

> **_NOTE:_** This SDK requires the `System.Text.Json` API to be available, which is included in the runtime for .NET
> Core 3.1 and later versions, but not on other platforms, so on other platforms the SDK brings
> in `System.Text.Json` as a NuGet package dependency.

## Get Started

### Installation

The latest stable version is available on [NuGet](https://www.nuget.org/packages/FeatBit.ServerSdk/).

```sh
dotnet add package FeatBit.ServerSdk
```

Use the `--version` option to specify
a [preview version](https://www.nuget.org/packages/FeatBit.ServerSdk/absoluteLatest) to install.

### Basic usage

The following code demonstrates basic usage of FeatBit.ServerSdk.

```cs
using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;

// Set secret to your FeatBit SDK secret.
const string secret = "<replace-with-your-env-secret>";

// Creates a new client instance that connects to FeatBit with the default option.
var client = new FbClient(secret);
if (!client.Initialized)
{
    Console.WriteLine("FbClient failed to initialize. Exiting...");
}
else
{
    Console.WriteLine("FbClient successfully initialized!");

    // flag to be evaluated
    const string flagKey = "game-runner";

    // create a user
    var user = FbUser.Builder("anonymous").Build();

    // evaluate a boolean flag for a given user
    var boolVariation = client.BoolVariation(flagKey, user, defaultValue: false);
    Console.WriteLine($"flag '{flagKey}' returns {boolVariation} for user {user.Key}");

    // evaluate a boolean flag for a given user with evaluation detail
    var boolVariationDetail = client.BoolVariationDetail(flagKey, user, defaultValue: false);
    Console.WriteLine(
        $"flag '{flagKey}' returns {boolVariationDetail.Value} for user {user.Key}. " +
        $"Reason Kind: {boolVariationDetail.Kind}, Reason Description: {boolVariationDetail.Reason}"
    );
}
```

### Examples

- [Console App](https://github.com/featbit/dotnet-server-sdk/blob/main/examples/ConsoleApp/Program.cs)

## Data Synchronization

We use websocket to make the local data synchronized with the FeatBit server, and then store them in memory by
default. Whenever there is any change to a feature flag or its related data, this change will be pushed to the SDK and
the average synchronization time is less than 100 ms. Be aware the websocket connection may be interrupted due to
internet outage, but it will be resumed automatically once the problem is gone.

## What's Next

- add feature flag insights support
- support offline mode & bootstrapping
- asp.net core integration & examples

## Getting support

- If you have a specific question about using this sdk, we encourage you
  to [ask it in our slack](https://join.slack.com/t/featbit/shared_invite/zt-1ew5e2vbb-x6Apan1xZOaYMnFzqZkGNQ).
- If you encounter a bug or would like to request a
  feature, [submit an issue](https://github.com/featbit/dotnet-server-sdk/issues/new).
