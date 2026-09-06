using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// INBOX 2026-09-07 05:26 항목 4: 몬스터 머리 위 체력바. 좌측 고정으로 오른쪽만 깎이는 방향.
/// - <see cref="MonsterHealthBar"/> : 게이지 하나. 좌측 피벗 채움 막대의 scale.x 만 줄인다.
/// - <see cref="MonsterHealthBarPool"/> : Instantiate/Destroy 반복 없이 재사용.
/// - <see cref="MonsterHealthBarSpawner"/> : <see cref="AutoBattleFsm.MonsterDamaged"/> 를 받아 바를 붙인다.
/// </summary>
public class MonsterHealthBarTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private class FixedRoller : ICritChanceRoller
    {
        private readonly bool _result;
        public FixedRoller(bool result) { _result = result; }
        public bool Roll(float probability) => _result;
    }

    private class FakeMotor : IPlayerMotor
    {
        public void MoveHorizontal(float direction) { }
        public void Jump() { }
        public void DropDown() { }
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    private MonsterHealthBar NewBar()
    {
        var go = new GameObject("MonsterHealthBar");
        _spawned.Add(go);
        return go.AddComponent<MonsterHealthBar>();
    }

    private MonsterHealthBarSpawner NewSpawner(AutoBattleFsm fsm, int prewarm = 0)
    {
        var go = new GameObject("MonsterHealthBarSpawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterHealthBarSpawner>();
        SetPrivate(spawner, "prewarm", prewarm);
        SetPrivate(spawner, "_pool", null);
        spawner.Configure(fsm);
        return spawner;
    }

    private MonsterSpawner NewMonsterSpawner(MonsterData data)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);
        SetPrivate(spawner, "scatterX", 0f);
        spawner.Configure(data, 1, 100f);
        return spawner;
    }

    private MonsterSpawner NewMonsterSpawnerMulti(MonsterData data, int maxAlive)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);
        SetPrivate(spawner, "scatterX", 0f);
        spawner.Configure(data, maxAlive, 100f);
        return spawner;
    }

    private AutoBattleFsm NewFsm(MonsterSpawner spawner)
    {
        var go = new GameObject("Player");
        _spawned.Add(go);
        go.transform.position = Vector2.zero;
        var fsm = go.AddComponent<AutoBattleFsm>();
        fsm.Configure(new FakeMotor(), spawner);
        fsm.Tune(newAttackRange: 1.2f, newAttackInterval: 0.5f, newAttackDamage: 7f, newVerticalThreshold: 0.75f);
        return fsm;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Object.Destroy(go);
        }
        _spawned.Clear();

        foreach (var bar in Object.FindObjectsByType<MonsterHealthBar>(FindObjectsSortMode.None))
        {
            if (bar != null) Object.Destroy(bar.gameObject);
        }
    }

    // ------------------------------------------------------------------
    // MonsterHealthBar
    // ------------------------------------------------------------------

    [Test]
    public void Bind_시_채움이_가득이고_몬스터_위에_위치한다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 50f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = new Vector3(2f, 1f, 0f);

        MonsterHealthBar bar = NewBar();
        bar.Bind(m);

        Assert.AreEqual(1f, bar.Fraction, 0.001f);
        Assert.IsTrue(bar.IsShowing);
        Assert.Greater(bar.transform.position.y, m.transform.position.y, "몬스터보다 위");
        Assert.AreEqual(m.transform.position.x, bar.transform.position.x, 0.001f);
    }

    [Test]
    public void SetFraction_은_왼쪽_끝을_고정하고_scale_x_만_줄인다()
    {
        MonsterHealthBar bar = NewBar();
        bar.SetFraction(1f);
        float fullScaleX = bar.FillLocalScaleX;
        float leftEdgeX = bar.FillLocalPositionX;

        bar.SetFraction(0.3f);

        // 왼쪽 끝(localPosition.x)은 그대로여야 한다 — 좌측 고정.
        Assert.AreEqual(leftEdgeX, bar.FillLocalPositionX, 0.0001f, "왼쪽 끝이 움직이면 안 된다");
        // 채움 폭만 비율대로 줄어든다.
        Assert.AreEqual(fullScaleX * 0.3f, bar.FillLocalScaleX, 0.0001f);
        Assert.Less(bar.FillLocalScaleX, fullScaleX);
    }

    [Test]
    public void SetFraction_은_0에서_1로_클램프된다()
    {
        MonsterHealthBar bar = NewBar();
        bar.SetFraction(-2f);
        Assert.AreEqual(0f, bar.Fraction, 0.001f);
        Assert.AreEqual(0f, bar.FillLocalScaleX, 0.001f);
        bar.SetFraction(5f);
        Assert.AreEqual(1f, bar.Fraction, 0.001f);
    }

    [Test]
    public void Tick_은_몬스터_체력비율을_갱신하고_따라다닌다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 0f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = Vector3.zero;

        MonsterHealthBar bar = NewBar();
        bar.Bind(m);

        m.TakeDamage(40f); // 60/100
        m.transform.position = new Vector3(3f, 0f, 0f);
        Assert.IsTrue(bar.Tick());

        Assert.AreEqual(0.6f, bar.Fraction, 0.001f);
        Assert.AreEqual(3f, bar.transform.position.x, 0.001f, "몬스터를 따라 이동");
    }

    [Test]
    public void 몬스터가_죽으면_Tick_이_false_를_돌려준다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 10f, 3f, 0f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        MonsterHealthBar bar = NewBar();
        bar.Bind(m);
        Assert.IsTrue(bar.Tick());

        m.TakeDamage(999f);
        Assert.IsFalse(bar.Tick(), "죽은 몬스터는 더 이상 유효하지 않다");
    }

    [Test]
    public void Hide_는_오브젝트를_비활성화하고_대상을_비운다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 10f, 3f, 0f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        MonsterHealthBar bar = NewBar();
        bar.Bind(m);
        Assert.IsTrue(bar.IsShowing);

        bar.Hide();
        Assert.IsFalse(bar.IsShowing);
        Assert.IsNull(bar.Monster);
    }

    // ------------------------------------------------------------------
    // MonsterHealthBarPool
    // ------------------------------------------------------------------

    [Test]
    public void 풀은_반납된_인스턴스를_재사용하고_새로_만들지_않는다()
    {
        var pool = new MonsterHealthBarPool(() => NewBar());
        MonsterHealthBar a = pool.Rent();
        pool.Return(a);
        MonsterHealthBar b = pool.Rent();

        Assert.AreSame(a, b);
        Assert.AreEqual(1, pool.TotalCreated);
        Assert.AreEqual(0, pool.IdleCount);
    }

    [Test]
    public void 풀_중복반납은_무시된다()
    {
        var pool = new MonsterHealthBarPool(() => NewBar());
        MonsterHealthBar a = pool.Rent();
        pool.Return(a);
        pool.Return(a);
        Assert.AreEqual(1, pool.IdleCount);
    }

    [Test]
    public void 풀_null_팩토리는_거부된다()
    {
        Assert.Throws<System.ArgumentNullException>(() => new MonsterHealthBarPool(null));
    }

    // ------------------------------------------------------------------
    // MonsterHealthBarSpawner
    // ------------------------------------------------------------------

    [Test]
    public void MonsterDamaged_이벤트가_오면_맞은_몬스터에_체력바를_붙인다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = new Vector3(3f, 0f, 0f);

        AutoBattleFsm fsm = NewFsm(ms);
        MonsterHealthBarSpawner spawner = NewSpawner(fsm);

        RaiseMonsterDamaged(fsm, m, new DamageResult(17f, false, 17f));

        Assert.AreEqual(1, spawner.ActiveCount);
        MonsterHealthBar bar = FirstActive(spawner);
        Assert.AreSame(m, bar.Monster);
        Assert.Greater(bar.transform.position.y, m.transform.position.y);
    }

    [Test]
    public void 같은_몬스터를_여러_번_때려도_체력바는_하나다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        AutoBattleFsm fsm = NewFsm(ms);
        MonsterHealthBarSpawner spawner = NewSpawner(fsm);

        RaiseMonsterDamaged(fsm, m, new DamageResult(5f, false, 5f));
        RaiseMonsterDamaged(fsm, m, new DamageResult(5f, false, 5f));
        RaiseMonsterDamaged(fsm, m, new DamageResult(5f, false, 5f));

        Assert.AreEqual(1, spawner.ActiveCount);
        Assert.AreEqual(1, spawner.PoolSize);
    }

    [Test]
    public void 몬스터가_죽으면_체력바가_풀로_반납되고_인스턴스는_재사용된다()
    {
        var ms = NewMonsterSpawnerMulti(new MonsterData("t", 100f, 3f, 2f, 0, 0), 2);
        ms.FillToCapacity();
        Monster first = ms.AliveMonsters[0];
        Monster second = ms.AliveMonsters[1];

        AutoBattleFsm fsm = NewFsm(ms);
        MonsterHealthBarSpawner spawner = NewSpawner(fsm);

        RaiseMonsterDamaged(fsm, first, new DamageResult(5f, false, 5f));
        Assert.AreEqual(1, spawner.ActiveCount);
        Assert.AreEqual(1, spawner.PoolSize);

        first.TakeDamage(999f); // 사망
        spawner.Tick();
        Assert.AreEqual(0, spawner.ActiveCount, "죽은 몬스터의 바는 반납된다");

        RaiseMonsterDamaged(fsm, second, new DamageResult(5f, false, 5f));
        Assert.AreEqual(1, spawner.ActiveCount);
        Assert.AreEqual(1, spawner.PoolSize, "바 인스턴스가 재사용된다(새로 만들지 않음)");
    }

    [Test]
    public void 비활성화된_스포너는_체력바를_붙이지_않는다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        AutoBattleFsm fsm = NewFsm(ms);
        MonsterHealthBarSpawner spawner = NewSpawner(fsm);
        spawner.gameObject.SetActive(false);

        RaiseMonsterDamaged(fsm, m, new DamageResult(9f, false, 9f));

        Assert.AreEqual(0, spawner.ActiveCount);
    }

    [Test]
    public void 실제_FSM_타격이_체력바로_이어지고_바가_줄어든다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 4f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = new Vector3(0.5f, 0f, 0f);

        AutoBattleFsm fsm = NewFsm(ms);
        fsm.ConfigureCombat(new StatContainer(baseAttackPower: 20f), new DamageCalculator(new FixedRoller(false)));
        MonsterHealthBarSpawner spawner = NewSpawner(fsm);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);
        fsm.Tick(0.5f); // 첫 타격 (20 - 4 = 16 데미지)

        Assert.AreEqual(1, spawner.ActiveCount);
        MonsterHealthBar bar = FirstActive(spawner);
        float leftEdgeBefore = bar.FillLocalPositionX;
        float fillFull = bar.FillLocalScaleX;
        spawner.Tick(); // 체력 비율 갱신

        Assert.AreEqual(0.84f, bar.Fraction, 0.001f, "100 → 84");
        Assert.AreEqual(fillFull * 0.84f, bar.FillLocalScaleX, 0.001f, "채움 폭만 비율대로 줄어든다");
        Assert.AreEqual(leftEdgeBefore, bar.FillLocalPositionX, 0.0001f, "왼쪽 끝은 고정");
    }

    // ------------------------------------------------------------------

    private static void RaiseMonsterDamaged(AutoBattleFsm fsm, Monster monster, DamageResult result)
    {
        var field = typeof(AutoBattleFsm).GetField("MonsterDamaged",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "MonsterDamaged 이벤트 백킹 필드를 찾지 못했습니다.");
        var handler = (System.Delegate)field.GetValue(fsm);
        if (handler != null)
        {
            handler.DynamicInvoke(monster, result);
        }
    }

    private static MonsterHealthBar FirstActive(MonsterHealthBarSpawner spawner)
    {
        var field = typeof(MonsterHealthBarSpawner).GetField("_active",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (List<MonsterHealthBar>)field.GetValue(spawner);
        Assert.Greater(list.Count, 0, "붙어 있는 체력바가 없습니다.");
        return list[0];
    }
}
