using System;
using UnityEngine;

/// <summary>
/// 자동전투로 획득한 경험치/골드/처치 수를 담는 런타임 지갑.
/// (오프라인 보상 누적치와의 통합은 이후 단계에서 정리.)
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    public int Exp { get; private set; }
    public int Gold { get; private set; }
    public int Kills { get; private set; }

    /// <summary>값이 바뀔 때마다 호출된다.</summary>
    public event Action Changed;

    /// <summary>몬스터 1마리 처치 보상을 더한다.</summary>
    public void AddKillReward(int exp, int gold)
    {
        Exp += Mathf.Max(0, exp);
        Gold += Mathf.Max(0, gold);
        Kills += 1;
        Changed?.Invoke();
    }
}
