using UnityEngine;

/// <summary>
/// 플레이어의 전투 스탯 (기획서 5.2 최소 세트 중 전투 관련).
/// 3단계에서는 단순 필드지만, 4단계에서 레이어드 스탯(기본값 + 수정자 + Dirty Flag)으로
/// 내부 구현이 대체될 예정이다. 외부에는 이 읽기 전용 프로퍼티만 노출한다.
/// </summary>
public class PlayerCombatStats : MonoBehaviour
{
    [SerializeField] private float attackPower = 16f;
    [SerializeField] private float defense = 5f;
    [SerializeField] private float maxHP = 100f;
    [Range(0f, 1f)]
    [SerializeField] private float critRate = 0.25f;

    public float AttackPower => attackPower;
    public float Defense => defense;
    public float MaxHP => maxHP;
    public float CritRate => critRate;
}
