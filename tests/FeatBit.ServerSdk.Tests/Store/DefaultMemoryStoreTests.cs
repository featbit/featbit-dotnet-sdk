using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Store;

public class DefaultMemoryStoreTests
{
    [Fact]
    public void FindUsesStableSnapshotWhenPredicateUpdatesStore()
    {
        var store = new DefaultMemoryStore();
        var first = new FeatureFlagBuilder().Key("first").Version(1).Build();
        var second = new FeatureFlagBuilder().Key("second").Version(2).Build();
        var added = new FeatureFlagBuilder().Key("added").Version(3).Build();
        store.Populate(new[] { first, second });

        var result = store.Find<FeatureFlag>(_ =>
        {
            store.Upsert(added);
            return true;
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
        Assert.Same(added, store.Get<FeatureFlag>(added.StoreKey));
        Assert.Equal(3, store.Version());
    }

    [Fact]
    public async Task ConcurrentUpsertsDoNotLoseItems()
    {
        var store = new DefaultMemoryStore();
        var writers = Enumerable.Range(1, 4).Select(writer => Task.Run(() =>
        {
            for (var i = 1; i <= 100; i++)
            {
                Assert.True(store.Upsert(new FeatureFlagBuilder()
                    .Key($"{writer}-{i}").Version(i).Build()));
            }
        }));

        await Task.WhenAll(writers);

        Assert.Equal(400, store.Find<FeatureFlag>(_ => true).Count);
        Assert.Equal(100, store.Version());
        Assert.False(store.Populated);
    }

    [Fact]
    public async Task ConcurrentReadsAndWritesCompleteSuccessfully()
    {
        var store = new DefaultMemoryStore();
        var initial = new FeatureFlagBuilder().Key("flag").Build();
        store.Populate(new[] { initial });
        using var start = new System.Threading.ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var i = 1; i <= 1000; i++)
            {
                var flag = new FeatureFlagBuilder().Key("flag").Version(i).Build();
                store.Upsert(flag);
                store.Upsert(new FeatureFlagBuilder().Key($"flag-{i}").Version(i).Build());
                if (i % 10 == 0)
                {
                    store.Populate(new[] { flag });
                }
            }
        });
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 1000; i++)
            {
                Assert.True(store.Populated);
                Assert.NotNull(store.Get<FeatureFlag>(initial.StoreKey));
                Assert.NotEmpty(store.Find<FeatureFlag>(item => true));
                Assert.InRange(store.Version(), 0, 1000);
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(readers.Append(writer));

        Assert.Equal(1000, store.Version());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopulateDuplicateIdentifiersKeepsLatestVersion(bool newestFirst)
    {
        var id = Guid.NewGuid();
        var newestFlag = new FeatureFlagBuilder().Key("duplicate").Version(2).Build();
        var olderFlag = new FeatureFlagBuilder().Key("duplicate").Version(1).Build();
        var newestSegment = new SegmentBuilder().Id(id).Version(2).Build();
        var olderSegment = new SegmentBuilder().Id(id).Version(1).Build();
        var other = new FeatureFlagBuilder().Key("other").Build();
        var store = new DefaultMemoryStore();

        store.Populate(newestFirst
            ? new StorableObject[] { newestFlag, olderFlag, newestSegment, olderSegment, other }
            : new StorableObject[] { olderFlag, newestFlag, olderSegment, newestSegment, other });

        Assert.True(store.Populated);
        Assert.Same(newestFlag, store.Get<FeatureFlag>(newestFlag.StoreKey));
        Assert.Same(newestSegment, store.Get<Segment>(newestSegment.StoreKey));
        Assert.Same(other, store.Get<FeatureFlag>(other.StoreKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopulateDuplicateIdentifiersWithEqualVersionsKeepsLast(bool segment)
    {
        var id = Guid.NewGuid();
        StorableObject first = segment
            ? new SegmentBuilder().Id(id).Version(2).Build()
            : new FeatureFlagBuilder().Key("duplicate").Version(2).Build();
        StorableObject second = segment
            ? new SegmentBuilder().Id(id).Version(2).Build()
            : new FeatureFlagBuilder().Key("duplicate").Version(2).Build();
        var store = new DefaultMemoryStore();

        store.Populate(new[] { first, second });

        Assert.Same(second, store.Get<StorableObject>(first.StoreKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpsertEqualVersionReplacesExistingItem(bool segment)
    {
        var id = Guid.NewGuid();
        StorableObject existing = segment
            ? new SegmentBuilder().Id(id).Version(2).Included("old-user").Build()
            : new FeatureFlagBuilder().Key("flag").Version(2).VariationType("boolean").Build();
        StorableObject incoming = segment
            ? new SegmentBuilder().Id(id).Version(2).Included("new-user").Build()
            : new FeatureFlagBuilder().Key("flag").Version(2).VariationType("string").Build();
        var store = new DefaultMemoryStore();
        store.Populate(new[] { existing });

        Assert.True(store.Upsert(incoming));

        Assert.Same(incoming, store.Get<StorableObject>(incoming.StoreKey));
        Assert.Equal(2, store.Version());
    }

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
    public void GetArchivedItemReturnsNull()
    {
        var store = new DefaultMemoryStore();

        StorableObject archivedFlag = new FeatureFlagBuilder().IsArchived(true).Build();
        StorableObject archivedSegment = new SegmentBuilder().IsArchived(true).Build();
        store.Populate(new[] { archivedFlag, archivedSegment });

        var r1 = store.Get<FeatureFlag>(archivedFlag.StoreKey);
        var r2 = store.Get<Segment>(archivedSegment.StoreKey);

        Assert.Null(r1);
        Assert.Null(r2);
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
