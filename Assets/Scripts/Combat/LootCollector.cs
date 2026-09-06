using UnityEngine;

/// <summary>
/// 자동전투 FSM 의 Loot 단계를 실제 보상 지급으로 잇는 계층. (기획서 4.1 "Loot: 처치 후 골드/아이템 자동 획득")
///
/// <see cref="AutoBattleFsm.MonsterKilled"/> 이벤트를 구독해, 죽은 몬스터의
/// <see cref="MonsterData.goldReward"/> / <see cref="MonsterData.expReward"/> 를 <see cref="PlayerWallet"/> 에 넣는다.
/// FSM 은 "무엇을 죽였는지"만 알리고 지갑을 모르게 두어(의존성 분리), 보상 규칙은 여기서만 다룬다.
/// 이렇게 하면 FSM 자체는 3단계-2 테스트를 그대로 통과한다.
/// </summary>
[DisallowMultipleComponent]
public class LootCollector : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("처치 이벤트를 내보내는 자동전투 FSM. 보통 같은 Player 오브젝트.")]
    [SerializeField] private AutoBattleFsm fsm;

    [Tooltip("보상을 쌓을 지갑. 보통 같은 Player 오브젝트.")]
    [SerializeField] private PlayerWallet wallet;

    private bool _subscribed;

    /// <summary>이 콜렉터가 지금까지 회수한 처치 수(HUD/디버그용).</summary>
    public int LootedCount { get; private set; }

    /// <summary>테스트/런타임에서 의존성을 주입한다. 인스펙터 참조 대신 사용할 수 있다.</summary>
    public void Configure(AutoBattleFsm autoBattleFsm, PlayerWallet playerWallet)
    {
        Unsubscribe();
        fsm = autoBattleFsm;
        wallet = playerWallet;
        if (isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || fsm == null)
        {
            return;
        }
        fsm.MonsterKilled += OnMonsterKilled;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || fsm == null)
        {
            _subscribed = false;
            return;
        }
        fsm.MonsterKilled -= OnMonsterKilled;
        _subscribed = false;
    }

    private void OnMonsterKilled(Monster monster)
    {
        if (monster == null)
        {
            return;
        }

        LootedCount++;

        MonsterData data = monster.Data;
        if (data == null || wallet == null)
        {
            return;
        }

        wallet.AddGold(data.goldReward);
        wallet.AddExp(data.expReward);
    }
}
