using System.Text;

namespace FeatBit.Sdk.Server;

public static class TestData
{
    public static readonly byte[] FullDataSet = Encoding.UTF8.GetBytes(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataSynchronizer", "full-data-set.json"))
    );

    public static readonly byte[] PatchDataSet = Encoding.UTF8.GetBytes(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataSynchronizer", "patch-data-set.json"))
    );

    public static readonly string BootstrapJson = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Bootstrapping", "featbit-bootstrap.json")
    );
}