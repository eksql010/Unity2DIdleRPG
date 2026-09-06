using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 4단계-3a: 자동전투 FSM 의 Attack 이 데미지 파이프라인(기획서 5.3)을 거치는지 검증.
/// 크리티컬 난수를 페이크로 고정해 결정적으로 확인한다. 이동은 가짜 모터, 타겟은 실제 스포너+몬스터.
/// </summary>
public class AutoBattleDamageTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>고정된 결과만 돌려주는 페이크 크리티컬 판정기.</summary>
    private class FixedRoller : ICritChanceRoller
    {
        private readonly bool _result;
        public FixedRoller(bool result) { _result = result; }
        public bool Roll(float probability) => _result;
    }

    private class FakeMotor : IPlayerMotor
    {
        public float LastHorizontal;
        public int JumpCount;
        public int DropCount;
        public void MoveHorizontal(float direction) => LastHorizontal = direction;
        public void Jump() => JumpCount++;
        public void DropDown() => DropCount++;
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    private MonsterSpawner NewSpawner(int maxAlive, MonsterData data)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);
        SetPrivate(spawner, "scatterX", 0f);
        spawner.Configure(data, maxAlive, 3f);
        return spawner;
    }

    private AutoBattleFsm NewFsm(FakeMotor motor, MonsterSpawner spawner)
    {
        var go = new GameObject("Player");
        _spawned.Add(go);
        go.transform.position = Vector2.zero;
        var fsm = go.AddComponent<AutoBattleFsm>();
        fsm.Configure(motor, spawner);
        fsm.Tune(newAttackRange: 1.2f, newAttackInterval: 0.5f, newAttackDamage: 7f, newVerticalThreshold: 0.75f);
        return fsm;
    }

    /// <summary>사거리 안(0.5) 에 몬스터 1마리를 두고 Attack 상태로 진입시킨 뒤 그 몬스터를 돌려준다.</summary>
    private Monster EnterAttack(AutoBattleFsm fsm, MonsterSpawner s)
    {
        s.FillToCapacity();
        Monster m = s.AliveMonsters[0];
        m.transform.position = new Vector2(0.5f, 0f);
        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);
        return m;
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

    [Test]
    public void 몬스터는_스폰_시_Data_기반_StatContainer_를_갖는다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("orc", hp: 40f, attackPower: 6f, defense: 4f, expReward: 0, goldReward: 0));
        s.FillToCapacity();

        Monster m = s.AliveMonsters[0];
        Assert.IsNotNull(m.Stats);
        Assert.AreEqual(4f, m.Stats.Defense.Value, 0.001f);
        Assert.AreEqual(6f, m.Stats.AttackPower.Value, 0.001f);
        Assert.AreEqual(40f, m.Stats.MaxHP.Value, 0.001f);
    }

    [Test]
    public void 파이프라인을_주입하면_공격력에서_방어력을_뺀_데미지로_깎는다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("t", hp: 100f, attackPower: 3f, defense: 5f, expReward: 0, goldReward: 0));
        var fsm = NewFsm(new FakeMotor(), s);
        fsm.ConfigureCombat(new StatContainer(baseAttackPower: 20f), new DamageCalculator(new FixedRoller(false)));

        Monster m = EnterAttack(fsm, s);

        fsm.Tick(0.5f); // 첫 타격: 20 - 5 = 15
        Assert.AreEqual(85f, m.CurrentHp, 0.001f);

        fsm.Tick(0.5f); // 두 번째 타격
        Assert.AreEqual(70f, m.CurrentHp, 0.001f);
    }

    [Test]
    public void 크리티컬이_뜨면_1_5배_데미지가_들어간다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("t", hp: 100f, attackPower: 3f, defense: 0f, expReward: 0, goldReward: 0));
        var fsm = NewFsm(new FakeMotor(), s);
        fsm.ConfigureCombat(
            new StatContainer(baseAttackPower: 20f, baseCritRate: 0.5f),
            new DamageCalculator(new FixedRoller(true)));

        Monster m = EnterAttack(fsm, s);

        fsm.Tick(0.5f); // 20 × 1.5 = 30
        Assert.AreEqual(70f, m.CurrentHp, 0.001f);
    }

    [Test]
    public void MonsterDamaged_이벤트가_타격마다_계산결과와_함께_발생한다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("t", hp: 100f, attackPower: 3f, defense: 4f, expReward: 0, goldReward: 0));
        var fsm = NewFsm(new FakeMotor(), s);
        fsm.ConfigureCombat(
            new StatContainer(baseAttackPower: 20f, baseCritRate: 1f),
            new DamageCalculator(new FixedRoller(true)));

        var hits = new List<DamageResult>();
        Monster hitMonster = null;
        fsm.MonsterDamaged += (mon, res) => { hitMonster = mon; hits.Add(res); };

        Monster m = EnterAttack(fsm, s);
        fsm.Tick(0.5f);

        Assert.AreEqual(1, hits.Count);
        Assert.AreSame(m, hitMonster);
        Assert.IsTrue(hits[0].IsCrit);
        Assert.AreEqual(24f, hits[0].Damage, 0.001f); // (20 - 4) × 1.5
    }

    [Test]
    public void 파이프라인_미배선이면_고정_attackDamage_로_폴백한다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("t", hp: 100f, attackPower: 3f, defense: 50f, expReward: 0, goldReward: 0));
        var fsm = NewFsm(new FakeMotor(), s); // ConfigureCombat 호출 안 함

        var hits = new List<DamageResult>();
        fsm.MonsterDamaged += (_, res) => hits.Add(res);

        Monster m = EnterAttack(fsm, s);
        fsm.Tick(0.5f);

        Assert.AreEqual(93f, m.CurrentHp, 0.001f); // 방어력 무시하고 고정 7
        Assert.AreEqual(7f, hits[0].Damage, 0.001f);
        Assert.IsFalse(hits[0].IsCrit);
    }

    [Test]
    public void 파이프라인_데미지로_처치해도_KillCount_와_MonsterKilled_는_동작한다()
    {
        MonsterSpawner s = NewSpawner(1, new MonsterData("t", hp: 25f, attackPower: 3f, defense: 5f, expReward: 0, goldReward: 0));
        var fsm = NewFsm(new FakeMotor(), s);
        fsm.ConfigureCombat(new StatContainer(baseAttackPower: 20f), new DamageCalculator(new FixedRoller(false)));

        int killedEvents = 0;
        fsm.MonsterKilled += _ => killedEvents++;

        Monster m = EnterAttack(fsm, s);
        fsm.Tick(0.5f); // 15 → hp 10
        Assert.AreEqual(10f, m.CurrentHp, 0.001f);
        Assert.AreEqual(0, fsm.KillCount);

        fsm.Tick(0.5f); // 15 → 사망
        Assert.AreEqual(1, fsm.KillCount);
        Assert.AreEqual(1, killedEvents);
        Assert.AreEqual(AutoBattleFsm.State.Idle, fsm.CurrentState);
    }
}
