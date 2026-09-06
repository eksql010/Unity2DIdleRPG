using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 3단계-1: 몬스터 오브젝트(HP만) + Object Pool + 스포너의 살아있는 리스트/타겟 선정 검증.
/// (기획서 4.2 스포너가 리스트 관리, 4.4 풀링)
/// </summary>
public class MonsterSpawnerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    private Monster NewMonster(string name = "M")
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go.AddComponent<Monster>();
    }

    private MonsterSpawner NewSpawner(int maxAlive, float respawnDelay, int prewarm = 0)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", prewarm);
        SetPrivate(spawner, "scatterX", 0f);
        spawner.Configure(new MonsterData("slime", 20f, 3f, 1f, 10, 5), maxAlive, respawnDelay);
        return spawner;
    }

    /// <summary>스폰 지점(자식 Transform)을 붙인 스포너. 지점마다 슬롯 1개.</summary>
    private MonsterSpawner NewSpawnerWithPoints(Vector2[] positions, float respawnDelay)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);

        var points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = new GameObject("SpawnPoint" + i);
            p.transform.SetParent(go.transform);
            p.transform.position = positions[i];
            points[i] = p.transform;
        }
        SetPrivate(spawner, "spawnPoints", points);
        // Configure 가 슬롯을 다시 만들므로 spawnPoints 주입 뒤에 호출한다.
        spawner.Configure(new MonsterData("slime", 20f, 3f, 1f, 10, 5), positions.Length, respawnDelay);
        return spawner;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        _spawned.Clear();
    }

    // ---------------- Monster (HP만) ----------------

    [Test]
    public void Spawn_체력을_가득_채우고_살아있는_상태로_만든다()
    {
        Monster m = NewMonster();
        m.Spawn(new MonsterData("slime", 30f, 0f, 0f, 1, 1), new Vector2(2f, 3f));

        Assert.IsTrue(m.IsAlive);
        Assert.AreEqual(30f, m.MaxHp, 0.001f);
        Assert.AreEqual(30f, m.CurrentHp, 0.001f);
        Assert.AreEqual(new Vector3(2f, 3f, 0f), m.transform.position);
        Assert.IsTrue(m.gameObject.activeSelf);
    }

    [Test]
    public void TakeDamage_체력을_깎되_0_밑으로는_안_내려가고_죽으면_Died가_한_번만_발생한다()
    {
        Monster m = NewMonster();
        m.Spawn(new MonsterData("slime", 10f, 0f, 0f, 1, 1), Vector2.zero);

        int diedCount = 0;
        m.Died += _ => diedCount++;

        Assert.IsFalse(m.TakeDamage(4f));
        Assert.AreEqual(6f, m.CurrentHp, 0.001f);
        Assert.IsTrue(m.IsAlive);

        Assert.IsTrue(m.TakeDamage(999f), "치명타는 true 를 돌려줘야 한다");
        Assert.AreEqual(0f, m.CurrentHp, 0.001f);
        Assert.IsFalse(m.IsAlive);
        Assert.AreEqual(1, diedCount);

        // 죽은 뒤 추가 타격은 무시되고 Died 도 다시 발생하지 않는다.
        Assert.IsFalse(m.TakeDamage(5f));
        Assert.AreEqual(1, diedCount);
    }

    [Test]
    public void TakeDamage_0_이하_피해는_무시한다()
    {
        Monster m = NewMonster();
        m.Spawn(new MonsterData("slime", 10f, 0f, 0f, 1, 1), Vector2.zero);

        Assert.IsFalse(m.TakeDamage(0f));
        Assert.IsFalse(m.TakeDamage(-3f));
        Assert.AreEqual(10f, m.CurrentHp, 0.001f);
    }

    [Test]
    public void Spawn_은_죽었던_몬스터를_다시_살려_재사용할_수_있다()
    {
        Monster m = NewMonster();
        m.Spawn(new MonsterData("slime", 5f, 0f, 0f, 1, 1), Vector2.zero);
        m.TakeDamage(999f);
        Assert.IsFalse(m.IsAlive);

        m.Spawn(new MonsterData("slime", 8f, 0f, 0f, 1, 1), new Vector2(1f, 0f));
        Assert.IsTrue(m.IsAlive);
        Assert.AreEqual(8f, m.CurrentHp, 0.001f);
    }

    [Test]
    public void Spawn_은_음수_hp_등_오입력을_보정한다()
    {
        Monster m = NewMonster();
        m.Spawn(new MonsterData("", -5f, -1f, -1f, -2, -3), Vector2.zero);

        Assert.Greater(m.MaxHp, 0f);
        Assert.AreEqual(0, m.Data.expReward);
        Assert.AreEqual(0, m.Data.goldReward);
    }

    // ---------------- MonsterPool ----------------

    [Test]
    public void Pool_은_반납한_인스턴스를_재사용하고_새로_만들지_않는다()
    {
        Monster shared = NewMonster("pooled");
        int created = 0;
        var pool = new MonsterPool(() => { created++; return shared; });

        Monster a = pool.Rent();
        Assert.AreEqual(1, created);
        pool.Return(a);
        Assert.AreEqual(1, pool.IdleCount);

        Monster b = pool.Rent();
        Assert.AreSame(a, b);
        Assert.AreEqual(1, created, "반납분이 있으면 새로 만들지 않는다");
    }

    [Test]
    public void Pool_prewarm_은_인스턴스를_미리_만들어_비활성으로_대기시킨다()
    {
        var made = new List<GameObject>();
        var pool = new MonsterPool(() =>
        {
            var go = new GameObject("pw");
            _spawned.Add(go);
            made.Add(go);
            return go.AddComponent<Monster>();
        }, prewarm: 3);

        Assert.AreEqual(3, pool.TotalCreated);
        Assert.AreEqual(3, pool.IdleCount);
        foreach (var go in made)
        {
            Assert.IsFalse(go.activeSelf, "prewarm 인스턴스는 비활성이어야 한다");
        }
    }

    [Test]
    public void Pool_중복_반납은_무시한다()
    {
        Monster shared = NewMonster("dup");
        var pool = new MonsterPool(() => shared);
        Monster a = pool.Rent();

        pool.Return(a);
        pool.Return(a);

        Assert.AreEqual(1, pool.IdleCount);
    }

    [Test]
    public void Pool_생성자는_null_팩토리를_거부한다()
    {
        Assert.Throws<ArgumentNullException>(() => new MonsterPool(null));
    }

    // ---------------- MonsterSpawner ----------------

    [Test]
    public void FillToCapacity_는_최대_마리수만큼_스폰한다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 4, respawnDelay: 3f);
        s.FillToCapacity();

        Assert.AreEqual(4, s.AliveCount);
        Assert.AreEqual(4, s.AliveMonsters.Count);
        foreach (Monster m in s.AliveMonsters)
        {
            Assert.IsTrue(m.IsAlive);
        }
    }

    [Test]
    public void SpawnOne_은_최대치를_넘으면_null_을_돌려준다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 2, respawnDelay: 3f);
        Assert.IsNotNull(s.SpawnOne());
        Assert.IsNotNull(s.SpawnOne());
        Assert.IsNull(s.SpawnOne());
        Assert.AreEqual(2, s.AliveCount);
    }

    [Test]
    public void 몬스터가_죽으면_살아있는_리스트에서_빠지고_풀로_반납된다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 3, respawnDelay: 3f);
        s.FillToCapacity();
        Assert.AreEqual(3, s.PoolSize);

        Monster victim = s.AliveMonsters[0];
        victim.TakeDamage(9999f);

        Assert.AreEqual(2, s.AliveCount);
        CollectionAssert.DoesNotContain(s.AliveMonsters, victim);
        Assert.IsFalse(victim.gameObject.activeSelf, "반납된 몬스터는 비활성이어야 한다");
    }

    [Test]
    public void 리스폰은_지연_후_풀_인스턴스를_재사용해_다시_채운다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 3, respawnDelay: 2f);
        s.FillToCapacity();
        Assert.AreEqual(3, s.PoolSize);

        s.AliveMonsters[0].TakeDamage(9999f);
        Assert.AreEqual(2, s.AliveCount);

        s.TickRespawn(1f);            // 아직 지연 안 지남
        Assert.AreEqual(2, s.AliveCount);

        s.TickRespawn(1.5f);          // 누적 2.5s > 2s → 보충
        Assert.AreEqual(3, s.AliveCount);
        Assert.AreEqual(3, s.PoolSize, "새로 만들지 않고 반납분을 재사용해야 한다");
    }

    [Test]
    public void GetNearestAliveMonster_는_가장_가까운_살아있는_몬스터를_고른다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 3, respawnDelay: 3f);
        s.FillToCapacity();

        s.AliveMonsters[0].transform.position = new Vector2(10f, 0f);
        s.AliveMonsters[1].transform.position = new Vector2(2f, 0f);
        s.AliveMonsters[2].transform.position = new Vector2(-6f, 0f);

        Monster nearest = s.GetNearestAliveMonster(new Vector2(0f, 0f));
        Assert.AreSame(s.AliveMonsters[1], nearest);
    }

    [Test]
    public void GetNearestAliveMonster_는_죽은_몬스터를_건너뛰고_아무도_없으면_null()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 2, respawnDelay: 3f);
        s.FillToCapacity();

        s.AliveMonsters[0].transform.position = new Vector2(1f, 0f);
        s.AliveMonsters[1].transform.position = new Vector2(5f, 0f);

        Monster far = s.AliveMonsters[1];
        s.AliveMonsters[0].TakeDamage(9999f); // 가까운 놈이 죽음 → 리스트에서 빠짐

        Assert.AreSame(far, s.GetNearestAliveMonster(Vector2.zero));

        far.TakeDamage(9999f);
        Assert.IsNull(s.GetNearestAliveMonster(Vector2.zero));
    }

    // ---------------- 스폰 지점별 슬롯 + 동일 위치 리스폰 (INBOX 2026-09-07 항목 2) ----------------

    [Test]
    public void 스폰_지점마다_몬스터가_한_마리씩_채워진다()
    {
        var points = new[] { new Vector2(-8f, -1.35f), new Vector2(-6f, 0.65f), new Vector2(6.5f, 2.3f) };
        MonsterSpawner s = NewSpawnerWithPoints(points, respawnDelay: 5f);

        s.FillToCapacity();

        Assert.AreEqual(3, s.MaxAlive, "슬롯 수 = 스폰 지점 수");
        Assert.AreEqual(3, s.AliveCount);
        foreach (Vector2 p in points)
        {
            bool matched = false;
            foreach (Monster m in s.AliveMonsters)
            {
                if (((Vector2)m.transform.position - p).sqrMagnitude < 0.001f) matched = true;
            }
            Assert.IsTrue(matched, $"{p} 지점에 몬스터가 없다");
        }
    }

    [Test]
    public void 몬스터가_죽으면_지연_후_같은_자리에서_리스폰된다()
    {
        var points = new[] { new Vector2(-8f, -1.35f), new Vector2(6.5f, 2.3f) };
        MonsterSpawner s = NewSpawnerWithPoints(points, respawnDelay: 5f);
        s.FillToCapacity();

        Monster victim = s.AliveMonsters[0];
        Vector3 deathPos = victim.transform.position;
        victim.TakeDamage(9999f);

        Assert.AreEqual(1, s.AliveCount);
        Assert.AreEqual(1, s.PendingRespawnCount, "죽은 슬롯은 대기 상태여야 한다");

        s.TickRespawn(3f);
        Assert.AreEqual(1, s.AliveCount, "지연이 안 지났으면 리스폰 안 함");
        Assert.AreEqual(1, s.PendingRespawnCount);

        s.TickRespawn(2.5f); // 누적 5.5s > 5s
        Assert.AreEqual(2, s.AliveCount);
        Assert.AreEqual(0, s.PendingRespawnCount);

        bool respawnedAtSamePos = false;
        foreach (Monster m in s.AliveMonsters)
        {
            if ((m.transform.position - deathPos).sqrMagnitude < 0.001f) respawnedAtSamePos = true;
        }
        Assert.IsTrue(respawnedAtSamePos, "죽은 그 자리에서 다시 나와야 한다");
        Assert.AreEqual(2, s.PoolSize, "풀 인스턴스를 재사용해야 한다");
    }

    [Test]
    public void 리스폰_대기_중에는_그_지점을_다시_채우지_않는다()
    {
        var points = new[] { new Vector2(-8f, -1.35f), new Vector2(6.5f, 2.3f) };
        MonsterSpawner s = NewSpawnerWithPoints(points, respawnDelay: 5f);
        s.FillToCapacity();

        s.AliveMonsters[0].TakeDamage(9999f);
        Assert.AreEqual(1, s.AliveCount);

        s.FillToCapacity();      // 대기 중 슬롯은 건너뛴다
        Assert.AreEqual(1, s.AliveCount, "대기 중에는 중첩 스폰하지 않는다");
        Assert.IsNull(s.SpawnOne(), "빈 슬롯이 대기 중이면 SpawnOne 도 null");
    }

    [Test]
    public void 지점별_리스폰_타이머는_서로_독립적이다()
    {
        var points = new[] { new Vector2(-8f, 0f), new Vector2(0f, 0f), new Vector2(8f, 0f) };
        MonsterSpawner s = NewSpawnerWithPoints(points, respawnDelay: 4f);
        s.FillToCapacity();

        s.AliveMonsters[0].TakeDamage(9999f);
        s.TickRespawn(2f);                       // 첫 슬롯 대기 2s 경과
        s.AliveMonsters[0].TakeDamage(9999f);    // 남은 것 중 하나 더 죽임(이제 대기 2개)
        Assert.AreEqual(1, s.AliveCount);
        Assert.AreEqual(2, s.PendingRespawnCount);

        s.TickRespawn(2.5f); // 먼저 죽은 슬롯(누적 4.5s)만 부활, 나중 슬롯(2.5s)은 아직
        Assert.AreEqual(2, s.AliveCount);
        Assert.AreEqual(1, s.PendingRespawnCount);

        s.TickRespawn(2f); // 나중 슬롯도 부활
        Assert.AreEqual(3, s.AliveCount);
        Assert.AreEqual(0, s.PendingRespawnCount);
    }
}
