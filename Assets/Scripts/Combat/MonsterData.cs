using System;
using UnityEngine;

/// <summary>
/// 몬스터 1종의 스탯/보상 데이터 (기획서 7. MonsterData 참고안).
/// 플레이어와 동일한 스탯 개념(공격력/방어력/HP)을 최소한으로 가진다 (기획서 5.4).
/// </summary>
[Serializable]
public class MonsterData
{
    [Tooltip("몬스터 식별자.")]
    public string monsterId = "slime";

    [Tooltip("최대 체력.")]
    [Min(1f)]
    public float maxHp = 50f;

    [Tooltip("공격력.")]
    [Min(0f)]
    public float attackPower = 5f;

    [Tooltip("방어력. 플레이어가 데미지를 계산할 때 차감된다.")]
    [Min(0f)]
    public float defense = 2f;

    [Tooltip("크리티컬 확률(0~1). MVP에서는 몬스터가 반격하지 않으므로 참고용.")]
    [Range(0f, 1f)]
    public float critRate = 0f;

    [Tooltip("처치 시 획득 경험치.")]
    [Min(0)]
    public int expReward = 8;

    [Tooltip("처치 시 획득 골드.")]
    [Min(0)]
    public int goldReward = 4;
}
