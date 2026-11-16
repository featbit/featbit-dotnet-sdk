using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.DataSynchronizer;

public class DataSyncProcessorTests
{
    [Fact]
    public void FullReplacesStoreAndReportsBothCategories()
    {
        var store = new DefaultMemoryStore();
        store.Populate(new[] { new FeatureFlagBuilder().Key("old").Build() });
        var flag = new FeatureFlagBuilder().Key("new").Build();
        var processor = new DataSyncProcessor(store, NullLogger.Instance);

        var result = processor.Process(new DataSet
        {
            EventType = DataSet.Full,
            FeatureFlags = new[] { flag },
            Segments = Array.Empty<Segment>()
        });

        Assert.True(result.Applied);
        Assert.Equal(DataChangeKind.Full, result.Change.Kind);
        Assert.True(result.Change.FeatureFlagsChanged);
        Assert.True(result.Change.SegmentsChanged);
        Assert.Null(store.Get<FeatureFlag>("ff_old"));
        Assert.Same(flag, store.Get<FeatureFlag>(flag.StoreKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PatchReportsOnlyChangedCategory(bool segment)
    {
        var store = new DefaultMemoryStore();
        var processor = new DataSyncProcessor(store, NullLogger.Instance);
        var data = new DataSet
        {
            EventType = DataSet.Patch,
            FeatureFlags = segment ? Array.Empty<FeatureFlag>() : new[] { new FeatureFlagBuilder().Key("flag").Build() },
            Segments = segment ? new[] { new SegmentBuilder().Id(Guid.NewGuid()).Build() } : Array.Empty<Segment>()
        };

        var result = processor.Process(data);

        Assert.True(result.Applied);
        Assert.Equal(DataChangeKind.Patch, result.Change.Kind);
        Assert.Equal(!segment, result.Change.FeatureFlagsChanged);
        Assert.Equal(segment, result.Change.SegmentsChanged);
        Assert.Single(store.Find<StorableObject>(_ => true));
    }

    [Fact]
    public void OlderPatchSucceedsWithoutChanges()
    {
        var store = new DefaultMemoryStore();
        var existing = new FeatureFlagBuilder().Key("flag").Version(2).Build();
        store.Populate(new[] { existing });
        var processor = new DataSyncProcessor(store, NullLogger.Instance);

        var result = processor.Process(new DataSet
        {
            EventType = DataSet.Patch,
            FeatureFlags = new[] { new FeatureFlagBuilder().Key("flag").Version(1).Build() },
            Segments = Array.Empty<Segment>()
        });

        Assert.True(result.Applied);
        Assert.Null(result.Change);
        Assert.Same(existing, store.Get<FeatureFlag>(existing.StoreKey));
    }

    [Fact]
    public void UnknownEventDoesNotWriteToStore()
    {
        var store = new Mock<IMemoryStore>(MockBehavior.Strict);
        var processor = new DataSyncProcessor(store.Object, NullLogger.Instance);

        var result = processor.Process(new DataSet { EventType = "unknown" });

        Assert.False(result.Applied);
        Assert.Null(result.Change);
        Assert.Empty(store.Invocations);
    }
}
