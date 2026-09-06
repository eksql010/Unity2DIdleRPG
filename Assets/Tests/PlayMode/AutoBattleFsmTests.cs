using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 3단계-2: 자동전투 FSM(Idle→Move→Attack→Loot→Idle) 검증. (기획서 4장)
/// 이동은 가짜 <see cref="IPlayerMotor"/> 로 기록만 하고, 타겟은 실제 MonsterSpawner + Monster 로 만든다.
/// </summary>
public class AutoBattleFsmTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>이동 명령을 받아 기록만 하는 가짜 모터.</summary>
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

    private MonsterSpawner NewSpawner(int maxAlive, float hp = 20f)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);
        SetPrivate(spawner, "scatterX", 0f);
        spawner.Configure(new MonsterData("slime", hp, 3f, 1f, 10, 5), maxAlive, 3f);
        return spawner;
    }

    private AutoBattleFsm NewFsm(FakeMotor motor, MonsterSpawner spawner, Vector2 at)
    {
        var go = new GameObject("Player");
        _spawned.Add(go);
        go.transform.position = at;
        var fsm = go.AddComponent<AutoBattleFsm>();
        fsm.Configure(motor, spawner);
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

    [Test]
    public void Idle_는_살아있는_가장_가까운_몬스터를_타겟으로_잡고_Move_로_전이한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 2);
        s.FillToCapacity();
        s.AliveMonsters[0].transform.position = new Vector2(8f, 0f);
        s.AliveMonsters[1].transform.position = new Vector2(3f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f);

        Assert.AreEqual(AutoBattleFsm.State.Move, fsm.CurrentState);
        Assert.AreSame(s.AliveMonsters[1], fsm.Target);
    }

    [Test]
    public void Idle_는_몬스터가_없으면_그대로_멈춰_있는다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1);
        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f);

        Assert.AreEqual(AutoBattleFsm.State.Idle, fsm.CurrentState);
        Assert.IsNull(fsm.Target);
        Assert.AreEqual(0f, motor.LastHorizontal);
    }

    [Test]
    public void Move_는_타겟_방향으로_수평_이동을_명령한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1);
        s.FillToCapacity();
        s.AliveMonsters[0].transform.position = new Vector2(6f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move: 오른쪽으로
        Assert.AreEqual(1f, motor.LastHorizontal);

        s.AliveMonsters[0].transform.position = new Vector2(-6f, 0f);
        fsm.Tick(0.1f);
        Assert.AreEqual(-1f, motor.LastHorizontal);
    }

    [Test]
    public void Move_는_타겟이_위에_있으면_Jump_아래에_있으면_DropDown_을_호출한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1);
        s.FillToCapacity();
        Monster m = s.AliveMonsters[0];

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        m.transform.position = new Vector2(4f, 3f); // 충분히 위
        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move
        Assert.AreEqual(1, motor.JumpCount);
        Assert.AreEqual(0, motor.DropCount);

        m.transform.position = new Vector2(4f, -3f); // 충분히 아래
        fsm.Tick(0.1f);
        Assert.AreEqual(1, motor.DropCount);
    }

    [Test]
    public void Move_는_사거리_안에_들면_멈추고_Attack_으로_전이한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1);
        s.FillToCapacity();
        s.AliveMonsters[0].transform.position = new Vector2(0.8f, 0f); // attackRange 1.2 안

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);
        Assert.AreEqual(0f, motor.LastHorizontal);
    }

    [Test]
    public void Attack_는_주기마다_몬스터_체력을_깎는다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1, hp: 100f);
        s.FillToCapacity();
        Monster m = s.AliveMonsters[0];
        m.transform.position = new Vector2(0.5f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);

        fsm.Tick(0.5f); // 첫 타격 (진입 직후 timer 0)
        Assert.AreEqual(93f, m.CurrentHp, 0.001f);

        fsm.Tick(0.5f); // 두 번째 타격
        Assert.AreEqual(86f, m.CurrentHp, 0.001f);
    }

    [Test]
    public void Attack_로_몬스터를_처치하면_KillCount_와_이벤트가_오르고_Idle_로_돌아간다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1, hp: 13f); // 7 + 7 = 14 → 두 타격에 사망
        s.FillToCapacity();
        Monster m = s.AliveMonsters[0];
        m.transform.position = new Vector2(0.5f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        int killedEvents = 0;
        fsm.MonsterKilled += _ => killedEvents++;

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        fsm.Tick(0.5f); // 타격 1 → hp 6
        Assert.AreEqual(6f, m.CurrentHp, 0.001f);
        Assert.AreEqual(0, fsm.KillCount);

        fsm.Tick(0.5f); // 타격 2 → 사망 → Loot → Idle (같은 스포너라 남은 타겟 없음)
        Assert.AreEqual(1, fsm.KillCount);
        Assert.AreEqual(1, killedEvents);
        Assert.IsNull(fsm.Target);
        Assert.AreEqual(AutoBattleFsm.State.Idle, fsm.CurrentState);
    }

    [Test]
    public void Attack_중_타겟이_사거리_밖으로_벗어나면_다시_Move_로_추격한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1, hp: 100f);
        s.FillToCapacity();
        Monster m = s.AliveMonsters[0];
        m.transform.position = new Vector2(0.5f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f); // Idle → Move
        fsm.Tick(0.1f); // Move → Attack
        Assert.AreEqual(AutoBattleFsm.State.Attack, fsm.CurrentState);

        m.transform.position = new Vector2(5f, 0f); // 멀어짐
        fsm.Tick(0.1f);
        Assert.AreEqual(AutoBattleFsm.State.Move, fsm.CurrentState);
    }

    [Test]
    public void 타겟이_사라지면_크래시_없이_Idle_로_복귀한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 2, hp: 100f);
        s.FillToCapacity();
        s.AliveMonsters[0].transform.position = new Vector2(3f, 0f);
        s.AliveMonsters[1].transform.position = new Vector2(9f, 0f);

        var motor = new FakeMotor();
        AutoBattleFsm fsm = NewFsm(motor, s, Vector2.zero);

        fsm.Tick(0.1f); // Idle → Move (가까운 놈 타겟)
        Monster targeted = fsm.Target;
        Assert.IsNotNull(targeted);

        targeted.TakeDamage(9999f); // 외부 요인으로 타겟 사망 → 스포너가 반납

        fsm.Tick(0.1f); // 죽음 감지 → Loot → Idle → 남은 몬스터를 새 타겟으로
        Assert.AreEqual(1, fsm.KillCount, "우리 공격이 아니어도 처치로 집계");
        Assert.AreSame(s.AliveMonsters[0], fsm.Target); // 남은 한 마리
        Assert.AreEqual(AutoBattleFsm.State.Move, fsm.CurrentState);
    }

    [Test]
    public void 의존성이_없으면_Tick_은_아무것도_하지_않는다()
    {
        var go = new GameObject("Player");
        _spawned.Add(go);
        var fsm = go.AddComponent<AutoBattleFsm>();

        Assert.DoesNotThrow(() => fsm.Tick(0.1f));
        Assert.AreEqual(AutoBattleFsm.State.Idle, fsm.CurrentState);
    }
}
