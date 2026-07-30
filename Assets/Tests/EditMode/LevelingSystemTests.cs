using NUnit.Framework;
using YiSunSin;

public class LevelingSystemTests
{
    [Test]
    public void ConfigValues_MatchSpec()
    {
        Assert.AreEqual(100f, GameConfig.Player.MaxHp);
        Assert.AreEqual(200f, GameConfig.Player.MoveSpeed);
        Assert.AreEqual(0.5f, GameConfig.Player.InvincibilityDuration);
        Assert.AreEqual(10f, GameConfig.Weapon.Damage);
        Assert.AreEqual(0.5f, GameConfig.Weapon.FireInterval);
        Assert.AreEqual(500f, GameConfig.Weapon.ProjectileSpeed);
        Assert.AreEqual(400f, GameConfig.Weapon.ProjectileRange);
        Assert.AreEqual(20f, GameConfig.EnemyBasic.Hp);
        Assert.AreEqual(80f, GameConfig.EnemyBasic.Speed);
        Assert.AreEqual(10f, GameConfig.EnemyBasic.ContactDamage);
        Assert.AreEqual(10f, GameConfig.EnemyFast.Hp);
        Assert.AreEqual(150f, GameConfig.EnemyFast.Speed);
        Assert.AreEqual(5f, GameConfig.EnemyFast.ContactDamage);
        Assert.AreEqual(500f, GameConfig.Boss.Hp);
        Assert.AreEqual(240f, GameConfig.BossSpawnTime);
        Assert.AreEqual(50f, GameConfig.BossXpValue);
        Assert.AreEqual(300f, GameConfig.WinTime);
        Assert.AreEqual(1600f, GameConfig.ArenaWidth);
        Assert.AreEqual(900f, GameConfig.ArenaHeight);
    }

    [Test]
    public void XpForLevel_Is10TimesLevel()
    {
        Assert.AreEqual(10, LevelingSystem.XpForLevel(1));
        Assert.AreEqual(40, LevelingSystem.XpForLevel(4));
    }

    [Test]
    public void ApplyXp_NoLevelUp_UnderThreshold()
    {
        var result = LevelingSystem.ApplyXp(1, 0f, 5f);
        Assert.AreEqual(1, result.Level);
        Assert.AreEqual(5f, result.Xp);
        Assert.IsFalse(result.LeveledUp);
        Assert.AreEqual(0, result.LevelsGained);
    }

    [Test]
    public void ApplyXp_SingleLevelUp_CarriesRemainder()
    {
        var result = LevelingSystem.ApplyXp(1, 8f, 5f);
        Assert.AreEqual(2, result.Level);
        Assert.AreEqual(3f, result.Xp);
        Assert.IsTrue(result.LeveledUp);
        Assert.AreEqual(1, result.LevelsGained);
    }

    [Test]
    public void ApplyXp_CascadesMultipleLevels()
    {
        // 1->2 costs 10 (25 left), 2->3 costs 20 (5 left), 3->4 costs 30 (5 < 30, stop)
        var result = LevelingSystem.ApplyXp(1, 0f, 35f);
        Assert.AreEqual(3, result.Level);
        Assert.AreEqual(5f, result.Xp);
        Assert.AreEqual(2, result.LevelsGained);
    }
}
