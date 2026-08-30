using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 자동전투 FSM 통합 검증 (기획서 4, 완료 기준 체크리스트).
/// 몬스터를 스폰하면 플레이어가 스스로 접근·공격·처치하고 보상을 획득하는지 본다.
/// </summary>
public class AutoBattleFlowTests
{
    private GameObject _root;

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{field}' 없음");
        f.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("AutoBattleTestRoot");

        // 바닥
        var ground = new GameObject("Ground");
        ground.transform.SetParent(_root.transform);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(0f, -1f, 0f);
        var gcol = ground.AddComponent<BoxCollider2D>();
        gcol.size = new Vector2(60f, 1f);

        // 플레이어
        var player = new GameObject("Player");
        player.transform.SetParent(_root.transform);
        player.transform.position = new Vector3(0f, 0.5f, 0f);
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        var pcol = player.AddComponent<CapsuleCollider2D>();
        pcol.size = new Vector2(0.8f, 1.6f);
        player.AddComponent<SpriteRenderer>();

        var groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, -0.85f, 0f);

        var movement = player.AddComponent<PlayerMovement>();
        SetPrivate(movement, "groundCheck", groundCheck.transform);
        SetPrivate(movement, "groundCheckRadius", 0.25f);
        SetPrivate(movement, "groundLayer", (LayerMask)(1 << LayerMask.NameToLayer("Ground")));
        SetPrivate(movement, "oneWayLayer", (LayerMask)0);
        SetPrivate(movement, "moveSpeed", 6f);
        SetPrivate(movement, "jumpForce", 12f);

        var stats = player.AddComponent<PlayerCombatStats>();
        SetPrivate(stats, "attackPower", 50f);
        SetPrivate(stats, "defense", 5f);
        SetPrivate(stats, "critRate", 0f);

        var wallet = player.AddComponent<PlayerWallet>();

        // 몬스터 프리팹(비활성 씬 오브젝트)
        var monsterPrefab = new GameObject("MonsterPrefab");
        monsterPrefab.transform.SetParent(_root.transform);
        monsterPrefab.SetActive(false);
        monsterPrefab.AddComponent<SpriteRenderer>();
        var monster = monsterPrefab.AddComponent<Monster>();
        var data = new MonsterData { maxHp = 30f, defense = 5f, attackPower = 3f, expReward = 9, goldReward = 4 };
        SetPrivate(monster, "data", data);

        // 스포너
        var spawnerGo = new GameObject("MonsterSpawner");
        spawnerGo.transform.SetParent(_root.transform);
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetParent(spawnerGo.transform);
        spawnPoint.transform.position = new Vector3(6f, 0f, 0f);
        var spawner = spawnerGo.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "monsterPrefab", monster);
        SetPrivate(spawner, "spawnPoints", new Transform[] { spawnPoint.transform });
        SetPrivate(spawner, "maxAlive", 1);
        SetPrivate(spawner, "respawnDelay", 1f);
        SetPrivate(spawner, "prewarm", 1);

        // FSM
        var fsm = player.AddComponent<AutoBattleController>();
        SetPrivate(fsm, "spawner", spawner);
        SetPrivate(fsm, "stats", stats);
        SetPrivate(fsm, "wallet", wallet);
        SetPrivate(fsm, "attackInterval", 0.15f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
        }
    }

    [UnityTest]
    public IEnumerator Player_AutoHunts_And_Kills_Monster()
    {
        var player = GameObject.Find("Player");
        var wallet = player.GetComponent<PlayerWallet>();
        var fsm = player.GetComponent<AutoBattleController>();

        // 스포너 Start() 가 몬스터를 스폰할 시간
        yield return null;
        yield return null;

        var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
        Assert.Greater(spawner.AliveMonsters.Count, 0, "몬스터가 스폰되지 않았습니다.");

        float startX = player.transform.position.x;

        // 최대 8초 안에 1마리 처치
        float timeout = 8f;
        while (timeout > 0f && wallet.Kills == 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.AreEqual(1, wallet.Kills, "플레이어가 몬스터를 처치하지 못했습니다.");
        Assert.AreEqual(9, wallet.Exp);
        Assert.AreEqual(4, wallet.Gold);
        Assert.Greater(player.transform.position.x, startX + 1f, "플레이어가 몬스터 쪽으로 이동하지 않았습니다.");
    }

    [UnityTest]
    public IEnumerator SetAutoBattle_False_StopsHunting()
    {
        var player = GameObject.Find("Player");
        var fsm = player.GetComponent<AutoBattleController>();

        yield return null;
        yield return null;

        fsm.SetAutoBattle(false);
        float x1 = player.transform.position.x;

        for (int i = 0; i < 120; i++)
        {
            yield return null;
        }

        Assert.AreEqual(AutoBattleController.BattleState.Idle, fsm.State);
        Assert.Less(Mathf.Abs(player.transform.position.x - x1), 0.5f, "자동전투 OFF 인데 플레이어가 이동했습니다.");
    }
}
