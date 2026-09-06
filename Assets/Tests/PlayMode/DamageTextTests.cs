using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 4단계-3b: 타격 시 몬스터 위에 떠오르는 데미지 숫자. (기획서 4.4 Object Pooling)
/// - <see cref="DamageText"/> : 숫자 하나. 위로 떠오르고 수명 후 사라진다. 크리티컬이면 색/크기 구분.
/// - <see cref="DamageTextPool"/> : Instantiate/Destroy 반복 없이 재사용.
/// - <see cref="DamageTextSpawner"/> : <see cref="AutoBattleFsm.MonsterDamaged"/> 를 받아 숫자를 띄운다.
/// </summary>
public class DamageTextTests
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

    private DamageText NewDamageText()
    {
        var go = new GameObject("DamageText");
        _spawned.Add(go);
        return go.AddComponent<DamageText>();
    }

    private DamageTextSpawner NewSpawner(AutoBattleFsm fsm, int prewarm = 0)
    {
        var go = new GameObject("DamageTextSpawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<DamageTextSpawner>();
        // Awake 가 이미 기본 prewarm 으로 풀을 만들었으니, 테스트용 값으로 다시 짓게 한다.
        SetPrivate(spawner, "prewarm", prewarm);
        SetPrivate(spawner, "_pool", null);
        spawner.Configure(fsm, null);
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
    }

    // ------------------------------------------------------------------
    // DamageText
    // ------------------------------------------------------------------

    [Test]
    public void Play_는_데미지를_정수로_반올림해_표시하고_위로_떠오른다()
    {
        DamageText t = NewDamageText();
        t.Play(new Vector3(2f, 1f, 0f), 14.6f, isCrit: false);

        Assert.AreEqual("15", t.DisplayedText);
        Assert.IsTrue(t.IsPlaying);
        Assert.IsFalse(t.LastWasCrit);

        float yBefore = t.transform.position.y;
        Assert.IsTrue(t.Tick(0.1f));
        Assert.Greater(t.transform.position.y, yBefore, "위로 떠올라야 한다");
    }

    [Test]
    public void 크리티컬이면_숫자에_강조표시와_다른_색을_쓴다()
    {
        DamageText normal = NewDamageText();
        normal.Play(Vector3.zero, 20f, isCrit: false);
        Color normalColor = normal.GetComponent<TextMesh>().color;

        DamageText crit = NewDamageText();
        crit.Play(Vector3.zero, 30f, isCrit: true);

        Assert.AreEqual("30!", crit.DisplayedText);
        Assert.IsTrue(crit.LastWasCrit);
        Assert.AreNotEqual(normalColor, crit.GetComponent<TextMesh>().color);
        Assert.Greater(crit.transform.localScale.x, normal.transform.localScale.x, "크리티컬이 더 크다");
    }

    [Test]
    public void 수명이_끝나면_Tick_이_false_를_돌려주고_알파가_0_에_수렴한다()
    {
        DamageText t = NewDamageText();
        SetPrivate(t, "lifetime", 0.5f);
        t.Play(Vector3.zero, 10f, isCrit: false);

        // 수명 직전까지: 아직 살아 있다.
        Assert.IsTrue(t.Tick(0.3f));
        // 수명 초과: 이번 Tick 에서 종료.
        Assert.IsFalse(t.Tick(0.3f));
        Assert.IsFalse(t.IsPlaying);
        Assert.Less(t.GetComponent<TextMesh>().color.a, 0.2f, "끝에서 거의 투명");
    }

    [Test]
    public void Hide_는_오브젝트를_비활성화한다()
    {
        DamageText t = NewDamageText();
        t.Play(Vector3.zero, 5f, isCrit: false);
        Assert.IsTrue(t.gameObject.activeSelf);

        t.Hide();
        Assert.IsFalse(t.gameObject.activeSelf);
        Assert.IsFalse(t.IsPlaying);
    }

    // ------------------------------------------------------------------
    // DamageTextPool
    // ------------------------------------------------------------------

    [Test]
    public void 풀은_반납된_인스턴스를_재사용하고_새로_만들지_않는다()
    {
        var created = new List<DamageText>();
        var pool = new DamageTextPool(() =>
        {
            DamageText dt = NewDamageText();
            created.Add(dt);
            return dt;
        });

        DamageText a = pool.Rent();
        pool.Return(a);
        DamageText b = pool.Rent();

        Assert.AreSame(a, b);
        Assert.AreEqual(1, pool.TotalCreated);
        Assert.AreEqual(0, pool.IdleCount);
    }

    [Test]
    public void 풀_중복반납은_무시된다()
    {
        var pool = new DamageTextPool(() => NewDamageText());
        DamageText a = pool.Rent();
        pool.Return(a);
        pool.Return(a);
        Assert.AreEqual(1, pool.IdleCount);
    }

    [Test]
    public void 풀_null_팩토리는_거부된다()
    {
        Assert.Throws<System.ArgumentNullException>(() => new DamageTextPool(null));
    }

    // ------------------------------------------------------------------
    // DamageTextSpawner
    // ------------------------------------------------------------------

    [Test]
    public void MonsterDamaged_이벤트가_오면_맞은_몬스터_위에_숫자를_띄운다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = new Vector3(3f, 0f, 0f);

        AutoBattleFsm fsm = NewFsm(ms);
        DamageTextSpawner spawner = NewSpawner(fsm);

        // FSM 이벤트를 직접 발생시킨다(파이프라인 계산은 4단계-3a 에서 검증됨).
        RaiseMonsterDamaged(fsm, m, new DamageResult(17f, false, 17f));

        Assert.AreEqual(1, spawner.ActiveCount);
        Assert.AreEqual(1, spawner.TotalShown);

        DamageText t = FirstActive(spawner);
        Assert.AreEqual("17", t.DisplayedText);
        Assert.Greater(t.transform.position.y, m.transform.position.y, "몬스터보다 위");
        Assert.AreEqual(m.transform.position.x, t.transform.position.x, 0.001f);
    }

    [Test]
    public void 크리티컬_결과는_강조된_숫자로_뜬다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        AutoBattleFsm fsm = NewFsm(ms);
        DamageTextSpawner spawner = NewSpawner(fsm);

        RaiseMonsterDamaged(fsm, m, new DamageResult(24f, true, 16f));

        DamageText t = FirstActive(spawner);
        Assert.IsTrue(t.LastWasCrit);
        Assert.AreEqual("24!", t.DisplayedText);
    }

    [Test]
    public void 여러_타격의_숫자는_수명_후_모두_풀로_반납되고_인스턴스는_재사용된다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 1000f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        AutoBattleFsm fsm = NewFsm(ms);
        DamageTextSpawner spawner = NewSpawner(fsm);

        for (int i = 0; i < 10; i++)
        {
            RaiseMonsterDamaged(fsm, m, new DamageResult(5f, false, 5f));
            // 다음 타격 전에 이번 숫자가 수명을 다하도록 충분히 틱.
            for (int k = 0; k < 20; k++) spawner.Tick(0.1f);
        }

        Assert.AreEqual(0, spawner.ActiveCount, "전부 반납됨");
        Assert.AreEqual(10, spawner.TotalShown);
        Assert.LessOrEqual(spawner.PoolSize, 2, "재사용되어 풀이 커지지 않는다");
    }

    [Test]
    public void 비활성화된_스포너는_숫자를_띄우지_않는다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 100f, 3f, 2f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];

        AutoBattleFsm fsm = NewFsm(ms);
        DamageTextSpawner spawner = NewSpawner(fsm);
        spawner.gameObject.SetActive(false); // OnDisable → 구독 해제

        RaiseMonsterDamaged(fsm, m, new DamageResult(9f, false, 9f));

        Assert.AreEqual(0, spawner.ActiveCount);
        Assert.AreEqual(0, spawner.TotalShown);
    }

    [Test]
    public void 실제_FSM_타격이_데미지_숫자로_이어진다()
    {
        MonsterSpawner ms = NewMonsterSpawner(new MonsterData("t", 1000f, 3f, 4f, 0, 0));
        ms.FillToCapacity();
        Monster m = ms.AliveMonsters[0];
        m.transform.position = new Vector3(0.5f, 0f, 0f);

        AutoBattleFsm fsm = NewFsm(ms);
        fsm.ConfigureCombat(new StatContainer(baseAttackPower: 20f), new DamageCalculator(new FixedRoller(false)));
        DamageTextSpawner spawner = NewSpawner(fsm);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack (사거리 진입, 타이머만 리셋)
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);
        fsm.Tick(0.5f); // 첫 타격

        Assert.AreEqual(1, spawner.ActiveCount);
        DamageText t = FirstActive(spawner);
        Assert.AreEqual("16", t.DisplayedText); // 20 - 4
    }

    // ------------------------------------------------------------------

    private static void RaiseMonsterDamaged(AutoBattleFsm fsm, Monster monster, DamageResult result)
    {
        var field = typeof(AutoBattleFsm).GetField("MonsterDamaged",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "MonsterDamaged 이벤트 백킹 필드를 찾지 못했습니다.");
        var handler = (System.Delegate)field.GetValue(fsm);
        // 구독자가 없으면(예: 스포너 비활성) 아무 일도 일어나지 않는 것이 정상.
        if (handler != null)
        {
            handler.DynamicInvoke(monster, result);
        }
    }

    private static DamageText FirstActive(DamageTextSpawner spawner)
    {
        var field = typeof(DamageTextSpawner).GetField("_active",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (List<DamageText>)field.GetValue(spawner);
        Assert.Greater(list.Count, 0, "떠 있는 데미지 숫자가 없습니다.");
        return list[0];
    }
}
