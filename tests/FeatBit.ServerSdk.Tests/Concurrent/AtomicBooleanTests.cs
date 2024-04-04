namespace FeatBit.Sdk.Server.Concurrent;

public class AtomicBooleanTests
{
    [Fact]
    public void SetInitialValue()
    {
        var trueBoolean = new AtomicBoolean(true);
        Assert.True(trueBoolean.Value);

        var falseBoolean = new AtomicBoolean(false);
        Assert.False(falseBoolean.Value);
    }

    [Fact]
    public void CompareAndSetWhenMatch()
    {
        var atomicBoolean = new AtomicBoolean(true);

        var newValueSet = atomicBoolean.CompareAndSet(true, false);

        Assert.True(newValueSet);
        Assert.False(atomicBoolean.Value);
    }

    [Fact]
    public void CompareAndSetWhenNotMatch()
    {
        var atomicBoolean = new AtomicBoolean(true);

        var newValueSet = atomicBoolean.CompareAndSet(false, false);

        Assert.False(newValueSet);
        Assert.True(atomicBoolean.Value);
    }

    [Fact]
    public void GetAndSet()
    {
        var atomicBoolean = new AtomicBoolean(true);

        var oldValue = atomicBoolean.GetAndSet(false);

        Assert.False(atomicBoolean.Value);
        Assert.True(oldValue);
    }

    [Fact]
    public void CastToBool()
    {
        var atomicBoolean = new AtomicBoolean(true);
        bool boolValue = atomicBoolean;
        Assert.True(boolValue);
    }
}