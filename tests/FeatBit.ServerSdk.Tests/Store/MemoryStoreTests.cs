using System;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Store;

public class MemoryStoreTests
{
    [Fact]
    public void CheckPopulated()
    {
        var store = new DefaultMemoryStore();
        Assert.False(store.Populated);

        store.Populate(Array.Empty<IStorableObject>());

        Assert.True(store.Populated);
    }

    [Fact]
    public void GetFeatureFlag()
    {
        var store = new DefaultMemoryStore();
        var flag = new FeatureFlagBuilder("hello-world").Build();
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
}