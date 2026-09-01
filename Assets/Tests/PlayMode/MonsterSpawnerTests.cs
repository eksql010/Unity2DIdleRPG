using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 스폰 지점(슬롯) 단위 스폰/리스폰 검증:
///   - 모든 스폰 지점에 몬스터 1마리씩
///   - 처치 시 같은 지점에서 respawnDelay 뒤 리스폰
///   - 대기 중에는 그 지점이 비어 있고, 중첩 스폰이 없음
/// </summary>
public class MonsterSpawnerTests
{
    private GameObject _root;
    private MonsterSpawner _spawner;
    private readonly Vector3[] _points =
    {
        new Vector3(-3f, 0f, 0f),
        new Vector3(0f, 2f, 0f),
        new Vector3(3f, -1f, 0f),
    };

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{field}' 없음");
        f.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("SpawnerTestRoot");

        var monsterPrefab = new GameObject("MonsterPrefab");
        monsterPrefab.transform.SetParent(_root.transform);
        monsterPrefab.SetActive(false);
        monsterPrefab.AddComponent<SpriteRenderer>();
        var monster = monsterPrefab.AddComponent<Monster>();
        SetPrivate(monster, "data", new MonsterData { maxHp = 10f, defense = 0f, expReward = 1, goldReward = 1 });

        var spawnerGo = new GameObject("MonsterSpawner");
        spawnerGo.transform.SetParent(_root.transform);
        var transforms = new Transform[_points.Length];
        for (int i = 0; i < _points.Length; i++)
        {
            var pt = new GameObject("SpawnPoint_" + i);
            pt.transform.SetParent(spawnerGo.transform);
            pt.transform.position = _points[i];
            transforms[i] = pt.transform;
        }

        _spawner = spawnerGo.AddComponent<MonsterSpawner>();
        SetPrivate(_spawner, "monsterPrefab", monster);
        SetPrivate(_spawner, "spawnPoints", transforms);
        SetPrivate(_spawner, "respawnDelay", 1f);
        SetPrivate(_spawner, "corpseLingerTime", 0.1f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
        }
    }

    private static List<Monster> ActiveMonstersAt(Vector3 point)
    {
        var result = new List<Monster>();
        foreach (Monster m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m.gameObject.activeInHierarchy && m.IsAlive &&
                (m.transform.position - point).sqrMagnitude < 0.01f)
            {
                result.Add(m);
            }
        }
        return result;
    }

    [UnityTest]
    public IEnumerator EverySpawnPoint_HasExactlyOneMonster()
    {
        yield return null;
        yield return null;

        Assert.AreEqual(_points.Length, _spawner.AliveMonsters.Count);
        foreach (Vector3 p in _points)
        {
            Assert.AreEqual(1, ActiveMonstersAt(p).Count, $"{p} 에 몬스터가 정확히 1마리여야 합니다.");
        }
    }

    [UnityTest]
    public IEnumerator KilledMonster_RespawnsAtSamePoint_AfterDelay_NoOverlap()
    {
        yield return null;
        yield return null;

        Vector3 target = _points[1];
        Monster victim = ActiveMonstersAt(target)[0];
        victim.TakeDamage(9999);

        // 대기 중에는 그 지점이 비어 있어야 한다
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(0, ActiveMonstersAt(target).Count, "리스폰 대기 중에는 지점이 비어 있어야 합니다.");
        Assert.AreEqual(_points.Length - 1, _spawner.AliveMonsters.Count);

        // respawnDelay(1s) 경과 후 같은 지점에 1마리만 다시 스폰
        yield return new WaitForSeconds(1.0f);
        var after = ActiveMonstersAt(target);
        Assert.AreEqual(1, after.Count, "같은 지점에 정확히 1마리 리스폰되어야 합니다(중첩 금지).");
        Assert.AreEqual(_points.Length, _spawner.AliveMonsters.Count);

        // 여러 번 죽여도 계속 1마리 유지
        after[0].TakeDamage(9999);
        yield return new WaitForSeconds(1.4f);
        Assert.AreEqual(1, ActiveMonstersAt(target).Count);
    }
}
