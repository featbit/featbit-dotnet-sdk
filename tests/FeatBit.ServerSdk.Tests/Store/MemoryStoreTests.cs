using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Store;

public class MemoryStoreTests
{
    [Fact]
    public void CheckPopulated()
    {
        var store = new DefaultMemoryStore();
        Assert.False(store.Populated);

        store.Populate(Array.Empty<StorableObject>());

        Assert.True(store.Populated);
    }

    [Fact]
    public void GetFeatureFlag()
    {
        var store = new DefaultMemoryStore();
        var flag = new FeatureFlagBuilder().Build();
        store.Populate(new[] { flag });

        var result = store.Get<FeatureFlag>(flag.StoreKey);
        Assert.NotNull(result);
        Assert.Same(flag, result);
    }

    [Fact]
    public void GetSegment()
    {
        var store = new DefaultMemoryStore();
        var segment = new SegmentBuilder().Build();
        store.Populate(new[] { segment });

        var result = store.Get<Segment>(segment.StoreKey);
        Assert.NotNull(result);
        Assert.Same(segment, result);
    }

    [Fact]
    public void GetNonExistingItem()
    {
        var store = new DefaultMemoryStore();

        var result = store.Get<object>("nope");
        Assert.Null(result);
    }

    [Fact]
    public void FindObjects()
    {
        var store = new DefaultMemoryStore();

        StorableObject flag1 = new FeatureFlagBuilder()
            .Key("f1")
            .Build();
        StorableObject flag2 = new FeatureFlagBuilder()
            .Key("f2")
            .Build();
        StorableObject segment1 = new SegmentBuilder()
            .Id(Guid.NewGuid())
            .Build();
        StorableObject segment2 = new SegmentBuilder()
            .Id(Guid.NewGuid())
            .Build();

        store.Populate(new[] { flag1, flag2, segment1, segment2 });

        var flags = store.Find<FeatureFlag>(x => x.StoreKey.StartsWith(StoreKeys.FlagPrefix));
        var segments = store.Find<Segment>(x => x.StoreKey.StartsWith(StoreKeys.SegmentPrefix));

        Assert.Equal(2, flags.Count);
        Assert.Equal(2, segments.Count);
    }

    [Fact]
    public void UpsertFeatureFlag()
    {
        const string flagKey = "hello";

        var store = new DefaultMemoryStore();
        var flag = new FeatureFlagBuilder()
            .Key(flagKey)
            .VariationType("boolean")
            .Version(1)
            .Build();

        // insert
        var insertResult = store.Upsert(flag);
        Assert.True(insertResult);

        var inserted = store.Get<FeatureFlag>(flag.StoreKey);
        Assert.NotNull(inserted);
        Assert.Same(flag, inserted);

        // update
        var updatedFlag = new FeatureFlagBuilder()
            .Key(flagKey)
            .VariationType("json")
            .Version(2)
            .Build();
        var updatedResult = store.Upsert(updatedFlag);
        Assert.True(updatedResult);

        var updated = store.Get<FeatureFlag>(flag.StoreKey);
        Assert.Equal("json", updated.VariationType);
        Assert.Equal(2, updated.Version);

        // update with old version data (store's data won't change)
        var oldFlag = new FeatureFlagBuilder()
            .Key(flagKey)
            .VariationType("string")
            .Version(0)
            .Build();

        var updateUsingOldFlag = store.Upsert(oldFlag);
        Assert.False(updateUsingOldFlag);

        var origin = store.Get<FeatureFlag>(flag.StoreKey);
        Assert.Equal("json", origin.VariationType);
        Assert.Equal(2, origin.Version);
    }

    [Fact]
    public void GetVersion()
    {
        var store = new DefaultMemoryStore();
        Assert.Equal(0, store.Version());

        const string flagKey = "hello";

        // insert
        var flag = new FeatureFlagBuilder()
            .Key(flagKey)
            .VariationType("boolean")
            .Version(123)
            .Build();
        store.Populate(new[] { flag });
        Assert.Equal(123, store.Version());

        // update
        var updatedFlag = new FeatureFlagBuilder()
            .Key(flagKey)
            .VariationType("json")
            .Version(456)
            .Build();
        store.Upsert(updatedFlag);
        Assert.Equal(456, store.Version());
    }
}