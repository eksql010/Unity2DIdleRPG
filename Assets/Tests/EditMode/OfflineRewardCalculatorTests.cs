using System;
using NUnit.Framework;

/// <summary>
/// OfflineRewardCalculator 순수 계산 검증. (기획서 6장 오프라인 방치 보상)
/// 씬/플레이모드 불필요 — DateTime 을 직접 주입한다.
/// </summary>
public class OfflineRewardCalculatorTests
{
    // 분당 60킬(=초당 1킬), 킬당 exp 10 / gold 5, 캡 8시간.
    private static OfflineRewardCalculator MakeCalc(double maxSeconds = OfflineRewardCalculator.DefaultMaxRewardSeconds)
    {
        return new OfflineRewardCalculator(
            killsPerMinute: 60.0,
            expPerKill: 10.0,
            goldPerKill: 5.0,
            maxRewardSeconds: maxSeconds);
    }

    private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void 경과시간에_비례해_보상이_지급된다()
    {
        var calc = MakeCalc();

        // 100초 경과 → 초당 1킬 → 100킬 → exp 1000, gold 500
        var r = calc.Calculate(Base, Base.AddSeconds(100));

        Assert.IsFalse(r.rejected);
        Assert.IsFalse(r.capped);
        Assert.AreEqual(100.0, r.elapsedSeconds, 0.001);
        Assert.AreEqual(100.0, r.rawElapsedSeconds, 0.001);
        Assert.AreEqual(1000, r.gainedExp);
        Assert.AreEqual(500, r.gainedGold);
    }

    [Test]
    public void 두배의_시간이면_두배의_보상이_지급된다()
    {
        var calc = MakeCalc();

        var r1 = calc.Calculate(Base, Base.AddSeconds(30));
        var r2 = calc.Calculate(Base, Base.AddSeconds(60));

        Assert.AreEqual(r1.gainedExp * 2, r2.gainedExp);
        Assert.AreEqual(r1.gainedGold * 2, r2.gainedGold);
    }

    [Test]
    public void 최대_캡을_초과하면_캡_시간으로_잘린다()
    {
        var calc = MakeCalc(); // 8시간 = 28800초 캡

        // 20시간 경과 → 8시간(28800초)만 인정 → 28800킬 → exp 288000
        var r = calc.Calculate(Base, Base.AddHours(20));

        Assert.IsTrue(r.capped);
        Assert.AreEqual(OfflineRewardCalculator.DefaultMaxRewardSeconds, r.elapsedSeconds, 0.001);
        Assert.AreEqual(20 * 3600.0, r.rawElapsedSeconds, 0.001);
        Assert.AreEqual(288000, r.gainedExp);
        Assert.AreEqual(144000, r.gainedGold);
    }

    [Test]
    public void 캡_경계_직전에는_캡이_걸리지_않는다()
    {
        var calc = MakeCalc();

        var r = calc.Calculate(Base, Base.AddSeconds(OfflineRewardCalculator.DefaultMaxRewardSeconds));

        Assert.IsFalse(r.capped);
        Assert.AreEqual(OfflineRewardCalculator.DefaultMaxRewardSeconds, r.elapsedSeconds, 0.001);
    }

    [Test]
    public void 현재_시각이_과거면_보상_계산을_건너뛴다()
    {
        var calc = MakeCalc();

        // 시간 되돌리기(기기 시계 조작) → now < lastSeen
        var r = calc.Calculate(Base, Base.AddSeconds(-3600));

        Assert.IsTrue(r.rejected);
        Assert.AreEqual(0, r.gainedExp);
        Assert.AreEqual(0, r.gainedGold);
        Assert.AreEqual(0.0, r.elapsedSeconds, 0.001);
    }

    [Test]
    public void 경과_시간이_0이면_보상이_없다()
    {
        var calc = MakeCalc();

        var r = calc.Calculate(Base, Base);

        Assert.IsTrue(r.rejected);
        Assert.AreEqual(0, r.gainedExp);
        Assert.AreEqual(0, r.gainedGold);
    }

    [Test]
    public void 보상은_정수로_내림_처리된다()
    {
        // 분당 1킬(초당 1/60킬), 킬당 exp 1 → 100초 후 1.666..킬 → exp 1
        var calc = new OfflineRewardCalculator(
            killsPerMinute: 1.0, expPerKill: 1.0, goldPerKill: 1.0);

        var r = calc.Calculate(Base, Base.AddSeconds(100));

        Assert.AreEqual(1, r.gainedExp);
        Assert.AreEqual(1, r.gainedGold);
    }

    [Test]
    public void 음수_설정값은_0으로_막혀_보상이_음수가_되지_않는다()
    {
        var calc = new OfflineRewardCalculator(
            killsPerMinute: -100.0, expPerKill: -5.0, goldPerKill: -1.0);

        var r = calc.Calculate(Base, Base.AddHours(1));

        Assert.AreEqual(0, r.gainedExp);
        Assert.AreEqual(0, r.gainedGold);
    }

    [Test]
    public void 캡_인자가_0이하면_기본_8시간이_적용된다()
    {
        var calc = new OfflineRewardCalculator(
            killsPerMinute: 60.0, expPerKill: 1.0, goldPerKill: 1.0, maxRewardSeconds: 0.0);

        var r = calc.Calculate(Base, Base.AddHours(20));

        Assert.IsTrue(r.capped);
        Assert.AreEqual(OfflineRewardCalculator.DefaultMaxRewardSeconds, r.elapsedSeconds, 0.001);
    }
}
