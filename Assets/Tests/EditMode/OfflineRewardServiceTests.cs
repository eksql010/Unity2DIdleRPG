using System;
using NUnit.Framework;

/// <summary>
/// OfflineRewardService(서비스 계층) + 저장소 검증. (기획서 6장 오프라인 방치 보상, 2단계-2)
/// 시계·저장소를 페이크로 주입하므로 씬/플레이모드가 필요 없다.
/// </summary>
public class OfflineRewardServiceTests
{
    // 분당 60킬(=초당 1킬), 킬당 exp 10 / gold 5, 캡 8시간.
    private static OfflineRewardCalculator MakeCalc()
    {
        return new OfflineRewardCalculator(
            killsPerMinute: 60.0, expPerKill: 10.0, goldPerKill: 5.0);
    }

    private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class FakeClock : IOfflineClock
    {
        public DateTime UtcNow { get; set; }
    }

    private sealed class InMemoryTimeStore : IOfflineTimeStore
    {
        private DateTime? value;
        public int saveCount;

        public InMemoryTimeStore(DateTime? initial = null) { value = initial; }

        public DateTime? LoadLastSeenUtc() => value;

        public void SaveLastSeenUtc(DateTime time)
        {
            value = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();
            saveCount++;
        }

        public void Clear() => value = null;
    }

    [Test]
    public void 첫_실행이면_보상없이_현재시각만_저장된다()
    {
        var store = new InMemoryTimeStore();
        var clock = new FakeClock { UtcNow = Base };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        var r = svc.ClaimOnReconnect();

        Assert.IsNull(r);
        Assert.AreEqual(Base, store.LoadLastSeenUtc());
        Assert.IsTrue(svc.HasLastSeen);
    }

    [Test]
    public void 재접속하면_경과시간만큼_보상이_계산된다()
    {
        var store = new InMemoryTimeStore(Base);
        var clock = new FakeClock { UtcNow = Base.AddSeconds(100) };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        var r = svc.ClaimOnReconnect();

        Assert.IsNotNull(r);
        Assert.IsFalse(r.rejected);
        Assert.AreEqual(1000, r.gainedExp);
        Assert.AreEqual(500, r.gainedGold);
    }

    [Test]
    public void 보상_수령_후_기준시각이_현재로_갱신되어_같은구간을_두번_주지_않는다()
    {
        var store = new InMemoryTimeStore(Base);
        var clock = new FakeClock { UtcNow = Base.AddSeconds(100) };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        var first = svc.ClaimOnReconnect();
        Assert.AreEqual(1000, first.gainedExp);
        Assert.AreEqual(clock.UtcNow, store.LoadLastSeenUtc());

        // 시각이 그대로면 경과 0 → rejected.
        var second = svc.ClaimOnReconnect();
        Assert.IsTrue(second.rejected);
        Assert.AreEqual(0, second.gainedExp);
    }

    [Test]
    public void 시간되돌리기면_rejected이고_기준시각은_뒤로_밀리지_않는다()
    {
        var store = new InMemoryTimeStore(Base);
        var clock = new FakeClock { UtcNow = Base.AddHours(-1) };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        var r = svc.ClaimOnReconnect();

        Assert.IsTrue(r.rejected);
        Assert.AreEqual(Base, store.LoadLastSeenUtc(), "기준 시각이 과거로 밀리면 안 된다");
    }

    [Test]
    public void 최대_캡이_서비스_경로에서도_적용된다()
    {
        var store = new InMemoryTimeStore(Base);
        var clock = new FakeClock { UtcNow = Base.AddHours(20) };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        var r = svc.ClaimOnReconnect();

        Assert.IsTrue(r.capped);
        Assert.AreEqual(OfflineRewardCalculator.DefaultMaxRewardSeconds, r.elapsedSeconds, 0.001);
    }

    [Test]
    public void MarkSeen은_현재_UTC시각을_저장한다()
    {
        var store = new InMemoryTimeStore();
        var clock = new FakeClock { UtcNow = Base.AddMinutes(42) };
        var svc = new OfflineRewardService(MakeCalc(), store, clock);

        svc.MarkSeen();

        Assert.AreEqual(Base.AddMinutes(42), store.LoadLastSeenUtc());
    }

    [Test]
    public void 생성자는_null_인자를_거부한다()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OfflineRewardService(null, new InMemoryTimeStore()));
        Assert.Throws<ArgumentNullException>(
            () => new OfflineRewardService(MakeCalc(), null));
    }

    // ---- PlayerPrefsOfflineTimeStore (실제 저장소 구현) ----

    private const string TestKey = "test.offline.lastSeenUtc";

    [SetUp]
    public void ClearTestKey() => UnityEngine.PlayerPrefs.DeleteKey(TestKey);

    [TearDown]
    public void CleanupTestKey() => UnityEngine.PlayerPrefs.DeleteKey(TestKey);

    [Test]
    public void 저장소는_저장한_시각을_UTC로_왕복시킨다()
    {
        var store = new PlayerPrefsOfflineTimeStore(TestKey);

        // Local 시각을 넣어도 UTC 로 변환되어 같은 순간으로 복원된다.
        DateTime local = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Local);
        store.SaveLastSeenUtc(local);

        DateTime? loaded = store.LoadLastSeenUtc();

        Assert.IsTrue(loaded.HasValue);
        Assert.AreEqual(DateTimeKind.Utc, loaded.Value.Kind);
        Assert.AreEqual(local.ToUniversalTime(), loaded.Value);
    }

    [Test]
    public void 저장소는_저장된적_없으면_null을_돌려준다()
    {
        var store = new PlayerPrefsOfflineTimeStore(TestKey);
        Assert.IsNull(store.LoadLastSeenUtc());
    }

    [Test]
    public void 저장소는_손상된_값을_null로_취급한다()
    {
        UnityEngine.PlayerPrefs.SetString(TestKey, "not-a-date");
        var store = new PlayerPrefsOfflineTimeStore(TestKey);

        Assert.IsNull(store.LoadLastSeenUtc());
    }

    [Test]
    public void 저장소_Clear_후에는_null이다()
    {
        var store = new PlayerPrefsOfflineTimeStore(TestKey);
        store.SaveLastSeenUtc(Base);
        Assert.IsNotNull(store.LoadLastSeenUtc());

        store.Clear();
        Assert.IsNull(store.LoadLastSeenUtc());
    }
}
