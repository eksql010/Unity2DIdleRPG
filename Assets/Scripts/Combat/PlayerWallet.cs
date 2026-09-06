using System;
using UnityEngine;

/// <summary>
/// 플레이어가 모은 골드/경험치 지갑. (기획서 1 핵심 루프 "경험치/골드 획득", 4.1 Loot)
///
/// 자동전투 FSM 의 Loot 단계에서 <see cref="LootCollector"/> 가 이 지갑에 보상을 넣고,
/// <see cref="WalletHud"/> 가 <see cref="Changed"/> 를 구독해 화면에 표시한다.
/// MVP 3단계 범위에서는 "숫자를 쌓는 것"까지만 한다(소비처·레벨업은 이후 단계).
/// 저장/복원은 하지 않는다 — 오프라인 보상(6장)과 달리 세션 내 누적만 본다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWallet : MonoBehaviour
{
    [Header("현재 보유량 (런타임 표시용)")]
    [SerializeField] private long gold;
    [SerializeField] private long exp;

    /// <summary>보유 골드.</summary>
    public long Gold => gold;

    /// <summary>보유 경험치.</summary>
    public long Exp => exp;

    /// <summary>골드나 경험치가 바뀔 때마다 발생. HUD 갱신용.</summary>
    public event Action Changed;

    /// <summary>골드를 더한다. 0 이하는 무시한다.</summary>
    public void AddGold(long amount)
    {
        if (amount <= 0)
        {
            return;
        }
        gold += amount;
        Changed?.Invoke();
    }

    /// <summary>경험치를 더한다. 0 이하는 무시한다.</summary>
    public void AddExp(long amount)
    {
        if (amount <= 0)
        {
            return;
        }
        exp += amount;
        Changed?.Invoke();
    }

    /// <summary>지갑을 0 으로 되돌린다(테스트/새 게임용).</summary>
    public void ResetWallet()
    {
        gold = 0;
        exp = 0;
        Changed?.Invoke();
    }
}
