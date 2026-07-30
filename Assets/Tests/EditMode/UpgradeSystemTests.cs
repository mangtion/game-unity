using System.Collections.Generic;
using NUnit.Framework;
using YiSunSin;

public class UpgradeSystemTests
{
    [Test]
    public void Pool_Has5Upgrades_EachMaxing5Stacks()
    {
        Assert.AreEqual(5, UpgradeSystem.Pool.Length);
        foreach (var u in UpgradeSystem.Pool)
        {
            Assert.AreEqual(5, u.MaxStacks);
            Assert.IsNotEmpty(u.Id);
            Assert.IsNotEmpty(u.Name);
            Assert.IsNotEmpty(u.Description);
        }
    }

    [Test]
    public void PickChoices_Returns3Unique_WhenNoneMaxed()
    {
        var values = new Queue<float>(new[] { 0f, 0f, 0f });
        var choices = UpgradeSystem.PickChoices(new Dictionary<string, int>(), () => values.Dequeue(), 3);
        Assert.AreEqual(3, choices.Count);
        var ids = new HashSet<string>();
        foreach (var c in choices) ids.Add(c.Id);
        Assert.AreEqual(3, ids.Count);
    }

    [Test]
    public void PickChoices_ExcludesMaxedUpgrades()
    {
        string maxedId = UpgradeSystem.Pool[0].Id;
        var owned = new Dictionary<string, int> { { maxedId, 5 } };
        var choices = UpgradeSystem.PickChoices(owned, () => 0f, 3);
        foreach (var c in choices) Assert.AreNotEqual(maxedId, c.Id);
    }

    [Test]
    public void PickChoices_ReturnsFewerThanCount_WhenPoolSmaller()
    {
        var owned = new Dictionary<string, int>();
        for (int i = 1; i < UpgradeSystem.Pool.Length; i++)
            owned[UpgradeSystem.Pool[i].Id] = 5;

        var choices = UpgradeSystem.PickChoices(owned, () => 0f, 3);
        Assert.AreEqual(1, choices.Count);
        Assert.AreEqual(UpgradeSystem.Pool[0].Id, choices[0].Id);
    }
}
