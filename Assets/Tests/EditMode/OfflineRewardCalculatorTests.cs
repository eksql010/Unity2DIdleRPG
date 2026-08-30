using System;
using NUnit.Framework;

/// <summary>
/// OfflineRewardCalculator 순수 계산 로직 검증 (기획서 6.1 / 6.2).
/// </summary>
public class OfflineRewardCalculatorTests
{
    private static OfflineRewardConfig MakeConfig()
    {
        return new OfflineRewardConfig
        {
            killsPerMinute = 60f,   // 초당 1킬 -> 계산이 단순해짐
            expPerKill = 10,
            goldPerKill = 5,
            maxOfflineHours = 8f,
        };
    }

    [Test]
    public void Calculate_NormalElapsed_GivesProportionalReward()
    {
        OfflineRewardConfig config = MakeConfig();
        DateTime logout = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime now = logout.AddMinutes(10); // 600초 -> 600킬

        OfflineRewardResult r = OfflineRewardCalculator.Calculate(logout, now, config);

        Assert.AreEqual(600.0, r.elapsedSeconds, 0.001);
        Assert.AreEqual(600.0, r.cappedSeconds, 0.001);
        Assert.IsFalse(r.wasCapped);
        Assert.AreEqual(6000, r.gainedExp); // 600킬 * 10
        Assert.AreEqual(3000, r.gainedGold); // 600킬 * 5
        Assert.IsTrue(r.HasReward);
    }

    [Test]
    public void Calculate_ExceedsCap_ClampsToMaxOfflineHours()
    {
        OfflineRewardConfig config = MakeConfig();
        DateTime logout = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime now = logout.AddHours(30); // 30시간이지만 8시간 캡

        OfflineRewardResult r = OfflineRewardCalculator.Calculate(logout, now, config);

        Assert.AreEqual(30.0 * 3600.0, r.elapsedSeconds, 0.001);
        Assert.AreEqual(8.0 * 3600.0, r.cappedSeconds, 0.001);
        Assert.IsTrue(r.wasCapped);
        // 8시간 * 3600초 * 초당 1킬 = 28800킬
        Assert.AreEqual(28800 * 10, r.gainedExp);
        Assert.AreEqual(28800 * 5, r.gainedGold);
    }

    [Test]
    public void Calculate_TimeRolledBack_GivesNoReward()
    {
        OfflineRewardConfig config = MakeConfig();
        DateTime logout = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        DateTime now = logout.AddHours(-3); // 현재가 저장된 시각보다 과거

        OfflineRewardResult r = OfflineRewardCalculator.Calculate(logout, now, config);

        Assert.AreEqual(0.0, r.elapsedSeconds);
        Assert.AreEqual(0, r.gainedExp);
        Assert.AreEqual(0, r.gainedGold);
        Assert.IsFalse(r.HasReward);
        Assert.IsFalse(r.wasCapped);
    }

    [Test]
    public void Calculate_ZeroElapsed_GivesNoReward()
    {
        OfflineRewardConfig config = MakeConfig();
        DateTime t = new DateTime(2026, 5, 5, 5, 5, 5, DateTimeKind.Utc);

        OfflineRewardResult r = OfflineRewardCalculator.Calculate(t, t, config);

        Assert.IsFalse(r.HasReward);
        Assert.AreEqual(0.0, r.elapsedSeconds);
    }

    [Test]
    public void Calculate_FractionalKills_AreFloored()
    {
        OfflineRewardConfig config = MakeConfig();
        config.killsPerMinute = 30f; // 초당 0.5킬
        config.expPerKill = 3;
        config.goldPerKill = 1;

        DateTime logout = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime now = logout.AddSeconds(5); // 5초 * 0.5 = 2.5킬

        OfflineRewardResult r = OfflineRewardCalculator.Calculate(logout, now, config);

        // 2.5킬 * 3 = 7.5 -> 내림 7
        Assert.AreEqual(7, r.gainedExp);
        // 2.5킬 * 1 = 2.5 -> 내림 2
        Assert.AreEqual(2, r.gainedGold);
    }

    [Test]
    public void Calculate_ZeroKillRate_GivesNoReward()
    {
        OfflineRewardConfig config = MakeConfig();
        config.killsPerMinute = 0f;

        DateTime logout = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        OfflineRewardResult r = OfflineRewardCalculator.Calculate(logout, logout.AddHours(5), config);

        Assert.IsFalse(r.HasReward);
        Assert.IsTrue(r.elapsedSeconds > 0.0);
    }
}
