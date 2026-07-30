using NUnit.Framework;
using YiSunSin;

public class SpawnCurveTests
{
    [Test]
    public void StartsAtBaseInterval()
    {
        Assert.AreEqual(2f, SpawnCurve.IntervalAt(0f));
    }

    [Test]
    public void DecaysFivePercentEvery15Seconds()
    {
        Assert.AreEqual(1.9f, SpawnCurve.IntervalAt(15f), 1e-4f);
    }

    [Test]
    public void NeverDropsBelowFloor()
    {
        Assert.AreEqual(0.3f, SpawnCurve.IntervalAt(100000f));
    }
}
