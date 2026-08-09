using System.Text;
using System.Text.Json;

namespace FeatBit.Sdk.Server;

public static class TestData
{
    public static readonly byte[] FullDataSet = Encoding.UTF8.GetBytes(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataSynchronizer", "full-data-set.json"))
    );

    public static readonly byte[] PatchDataSet = Encoding.UTF8.GetBytes(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataSynchronizer", "patch-data-set.json"))
    );

    public static readonly byte[] SegmentPatchDataSet = JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "data-sync",
        data = new
        {
            eventType = "patch",
            featureFlags = Array.Empty<object>(),
            segments = new[]
            {
                new
                {
                    id = "3e2a29b9-1f58-4e5d-8f0f-0248b806d75c",
                    included = new[] { "segment-member" },
                    excluded = Array.Empty<string>(),
                    rules = Array.Empty<object>(),
                    isArchived = false
                }
            }
        }
    });

    public static readonly byte[] EmptyFullDataSet = JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "data-sync",
        data = new
        {
            eventType = "full",
            featureFlags = Array.Empty<object>(),
            segments = Array.Empty<object>(),
        }
    });

    public static readonly string BootstrapJson = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Bootstrapping", "featbit-bootstrap.json")
    );
}
