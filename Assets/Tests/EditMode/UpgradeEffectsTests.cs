using System.Collections.Generic;
using NUnit.Framework;
using YiSunSin;

public class UpgradeEffectsTests
{
    [Test]
    public void ZeroStacks_ReturnsBaseValues()
    {
        var stacks = new Dictionary<string, int>();
        Assert.AreEqual(10f, UpgradeEffects.EffectiveWeaponDamage(stacks));
        Assert.AreEqual(0.5f, UpgradeEffects.EffectiveFireInterval(stacks));
        Assert.AreEqual(200f, UpgradeEffects.EffectiveMoveSpeed(stacks));
        Assert.AreEqual(100f, UpgradeEffects.EffectiveMaxHp(stacks));
        Assert.AreEqual(40f, UpgradeEffects.EffectivePickupRadius(stacks));
    }

    [Test]
    public void WeaponDamage_Adds20PercentPerStack()
    {
        Assert.AreEqual(12f, UpgradeEffects.EffectiveWeaponDamage(new Dictionary<string, int> { { "weaponDamage", 1 } }), 1e-4f);
        Assert.AreEqual(20f, UpgradeEffects.EffectiveWeaponDamage(new Dictionary<string, int> { { "weaponDamage", 5 } }), 1e-4f);
    }

    [Test]
    public void FireRate_Reduces10PercentCompounding()
    {
        float interval = UpgradeEffects.EffectiveFireInterval(new Dictionary<string, int> { { "fireRate", 1 } });
        Assert.AreEqual(0.45f, interval, 1e-4f);
    }

    [Test]
    public void MaxHp_AddsFlat20PerStack()
    {
        Assert.AreEqual(120f, UpgradeEffects.EffectiveMaxHp(new Dictionary<string, int> { { "maxHp", 1 } }));
        Assert.AreEqual(200f, UpgradeEffects.EffectiveMaxHp(new Dictionary<string, int> { { "maxHp", 5 } }));
    }

    [Test]
    public void PickupRadius_Adds20PercentPerStack()
    {
        Assert.AreEqual(48f, UpgradeEffects.EffectivePickupRadius(new Dictionary<string, int> { { "pickupRadius", 1 } }), 1e-4f);
    }

    [Test]
    public void MoveSpeed_Adds10PercentPerStack()
    {
        Assert.AreEqual(220f, UpgradeEffects.EffectiveMoveSpeed(new Dictionary<string, int> { { "moveSpeed", 1 } }), 1e-4f);
    }
}
