using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3단계-3: 자동전투 FSM 의 Loot 단계가 실제로 골드/경험치를 지급하는지 검증. (기획서 4.1)
/// - <see cref="PlayerWallet"/> 는 값 누적 + 변경 이벤트만.
/// - <see cref="LootCollector"/> 는 <see cref="AutoBattleFsm.MonsterKilled"/> 를 받아 지갑에 보상 지급.
/// - <see cref="WalletHud"/> 는 지갑 변경 시 라벨 텍스트 갱신.
/// </summary>
public class LootRewardTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    private MonsterSpawner NewSpawner(int maxAlive, float hp, int expReward, int goldReward)
    {
        var go = new GameObject("Spawner");
        _spawned.Add(go);
        var spawner = go.AddComponent<MonsterSpawner>();
        SetPrivate(spawner, "spawnOnStart", false);
        SetPrivate(spawner, "prewarm", 0);
        SetPrivate(spawner, "scatterX", 0f);
        // respawnDelay 를 크게 잡아 테스트 도중 리스폰이 끼어들지 않게 한다.
        spawner.Configure(new MonsterData("slime", hp, 3f, 1f, expReward, goldReward), maxAlive, 100f);
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

    private LootCollector NewCollector(AutoBattleFsm fsm, PlayerWallet wallet)
    {
        var go = new GameObject("LootCollector");
        _spawned.Add(go);
        var collector = go.AddComponent<LootCollector>();
        collector.Configure(fsm, wallet);
        return collector;
    }

    private PlayerWallet NewWallet()
    {
        var go = new GameObject("Wallet");
        _spawned.Add(go);
        return go.AddComponent<PlayerWallet>();
    }

    /// <summary>몬스터 전부를 사거리 안에 두고 FSM 을 여러 번 돌려 모두 처치한다.</summary>
    private static void RunUntilAllDead(AutoBattleFsm fsm, MonsterSpawner spawner, int maxTicks = 200)
    {
        foreach (Monster m in spawner.AliveMonsters)
        {
            m.transform.position = new Vector2(0.5f, 0f);
        }
        for (int i = 0; i < maxTicks && spawner.AliveCount > 0; i++)
        {
            foreach (Monster m in spawner.AliveMonsters)
            {
                m.transform.position = new Vector2(0.5f, 0f);
            }
            fsm.Tick(0.3f);
        }
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
    // PlayerWallet
    // ------------------------------------------------------------------

    [Test]
    public void Wallet_는_골드_경험치를_누적하고_Changed_를_발생시킨다()
    {
        PlayerWallet w = NewWallet();
        int changes = 0;
        w.Changed += () => changes++;

        w.AddGold(30);
        w.AddExp(45);
        w.AddGold(20);

        Assert.AreEqual(50, w.Gold);
        Assert.AreEqual(45, w.Exp);
        Assert.AreEqual(3, changes);
    }

    [Test]
    public void Wallet_은_0이하_입력을_무시한다()
    {
        PlayerWallet w = NewWallet();
        int changes = 0;
        w.Changed += () => changes++;

        w.AddGold(0);
        w.AddGold(-10);
        w.AddExp(-5);

        Assert.AreEqual(0, w.Gold);
        Assert.AreEqual(0, w.Exp);
        Assert.AreEqual(0, changes);
    }

    [Test]
    public void Wallet_ResetWallet_은_0으로_되돌린다()
    {
        PlayerWallet w = NewWallet();
        w.AddGold(100);
        w.AddExp(100);

        w.ResetWallet();

        Assert.AreEqual(0, w.Gold);
        Assert.AreEqual(0, w.Exp);
    }

    // ------------------------------------------------------------------
    // LootCollector
    // ------------------------------------------------------------------

    [Test]
    public void 처치하면_죽은_몬스터의_보상만큼_지갑에_들어간다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1, hp: 13f, expReward: 10, goldReward: 5);
        s.FillToCapacity();
        AutoBattleFsm fsm = NewFsm(s);
        PlayerWallet w = NewWallet();
        LootCollector c = NewCollector(fsm, w);

        RunUntilAllDead(fsm, s);

        Assert.AreEqual(1, fsm.KillCount);
        Assert.AreEqual(1, c.LootedCount);
        Assert.AreEqual(5, w.Gold);
        Assert.AreEqual(10, w.Exp);
    }

    [Test]
    public void 여러_마리를_처치하면_보상이_계속_누적된다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 3, hp: 13f, expReward: 10, goldReward: 5);
        s.FillToCapacity();
        AutoBattleFsm fsm = NewFsm(s);
        PlayerWallet w = NewWallet();
        LootCollector c = NewCollector(fsm, w);

        RunUntilAllDead(fsm, s);

        Assert.AreEqual(3, c.LootedCount);
        Assert.AreEqual(15, w.Gold);
        Assert.AreEqual(30, w.Exp);
    }

    [Test]
    public void 콜렉터가_비활성화되면_보상을_받지_않는다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 2, hp: 13f, expReward: 10, goldReward: 5);
        s.FillToCapacity();
        AutoBattleFsm fsm = NewFsm(s);
        PlayerWallet w = NewWallet();
        LootCollector c = NewCollector(fsm, w);

        c.gameObject.SetActive(false); // OnDisable → 이벤트 구독 해제

        RunUntilAllDead(fsm, s);

        Assert.Greater(fsm.KillCount, 0, "FSM 은 여전히 처치한다");
        Assert.AreEqual(0, c.LootedCount);
        Assert.AreEqual(0, w.Gold);
        Assert.AreEqual(0, w.Exp);
    }

    [Test]
    public void 지갑이_없어도_크래시없이_처치수만_센다()
    {
        MonsterSpawner s = NewSpawner(maxAlive: 1, hp: 13f, expReward: 10, goldReward: 5);
        s.FillToCapacity();
        AutoBattleFsm fsm = NewFsm(s);
        LootCollector c = NewCollector(fsm, null);

        Assert.DoesNotThrow(() => RunUntilAllDead(fsm, s));
        Assert.AreEqual(1, c.LootedCount);
    }

    // ------------------------------------------------------------------
    // WalletHud
    // ------------------------------------------------------------------

    [Test]
    public void Hud_는_지갑_변경시_라벨_텍스트를_갱신한다()
    {
        PlayerWallet w = NewWallet();

        var canvasGo = new GameObject("Canvas", typeof(Canvas));
        _spawned.Add(canvasGo);
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(canvasGo.transform, false);
        Text label = labelGo.AddComponent<Text>();

        var hudGo = new GameObject("Hud");
        _spawned.Add(hudGo);
        WalletHud hud = hudGo.AddComponent<WalletHud>();
        hud.Configure(w, label);

        Assert.IsTrue(label.text.Contains("0"), "초기 표시");

        w.AddGold(1234);
        w.AddExp(50);

        StringAssert.Contains("1,234", label.text);
        StringAssert.Contains("50", label.text);
    }
}
