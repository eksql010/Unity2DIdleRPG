using System;
using UnityEngine;

/// <summary>
/// 데미지 계산 파이프라인 (기획서 5.3).
///   최종 데미지 = (공격력 - 방어력 보정치) × 크리티컬 배율 × 기타 % 증가 배율
/// 4단계에서 공격력 입력이 레이어드 스탯으로 대체되더라도 이 진입점 시그니처는 유지한다.
/// 크리티컬 판정을 외부에서 주입할 수 있게 해서 순수 함수로 테스트 가능하게 둔다.
/// </summary>
public static class DamageCalculator
{
    /// <summary>크리티컬 발동 시 데미지 배율(기본 1.5배).</summary>
    public const float DefaultCriticalMultiplier = 1.5f;

    /// <summary>
    /// 크리티컬 판정을 직접 주입하는 버전(테스트/결정론 계산용).
    /// </summary>
    /// <param name="attackPower">공격자 공격력.</param>
    /// <param name="defense">피격자 방어력.</param>
    /// <param name="critRoll">0~1 난수. critRate 보다 작으면 크리티컬.</param>
    /// <param name="critRate">크리티컬 확률(0~1).</param>
    /// <param name="critMultiplier">크리티컬 배율.</param>
    /// <param name="extraMultiplier">기타 % 증가 배율(1 = 증가 없음).</param>
    public static DamageResult CalculateWithRoll(
        float attackPower,
        float defense,
        float critRoll,
        float critRate,
        float critMultiplier = DefaultCriticalMultiplier,
        float extraMultiplier = 1f)
    {
        float baseDamage = Mathf.Max(1f, attackPower - defense);

        bool isCrit = critRoll < critRate;
        float multiplier = (isCrit ? critMultiplier : 1f) * Mathf.Max(0f, extraMultiplier);

        int amount = Mathf.Max(1, Mathf.FloorToInt(baseDamage * multiplier));

        return new DamageResult { Amount = amount, IsCritical = isCrit };
    }

    /// <summary>
    /// 실제 전투용. 크리티컬 판정을 UnityEngine.Random 으로 굴린다.
    /// </summary>
    public static DamageResult Calculate(
        float attackPower,
        float defense,
        float critRate,
        float critMultiplier = DefaultCriticalMultiplier,
        float extraMultiplier = 1f)
    {
        return CalculateWithRoll(attackPower, defense, UnityEngine.Random.value, critRate, critMultiplier, extraMultiplier);
    }
}
