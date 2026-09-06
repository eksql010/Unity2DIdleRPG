using System;

/// <summary>
/// 크리티컬 발동 판정기. (기획서 5.3 "크리티컬 발동 여부는 CritRate 확률에 따라 매 공격마다 판정")
/// 데미지 계산에서 난수를 분리해 주입하기 위한 추상화 —
/// 런타임은 <see cref="UnityCritChanceRoller"/>(난수), EditMode 테스트는 고정된 페이크를 넣는다.
/// </summary>
public interface ICritChanceRoller
{
    /// <summary>확률(0~1)로 크리티컬 발동 여부를 판정한다.</summary>
    bool Roll(float probability);
}

/// <summary>UnityEngine.Random 기반 크리티컬 판정기(런타임 기본값).</summary>
public class UnityCritChanceRoller : ICritChanceRoller
{
    public bool Roll(float probability)
    {
        if (probability <= 0f) return false;
        if (probability >= 1f) return true;
        return UnityEngine.Random.value < probability;
    }
}

/// <summary>데미지 계산 1회의 결과.</summary>
public class DamageResult
{
    /// <summary>최종 데미지(방어력 · 크리티컬 · 기타 배율 모두 반영).</summary>
    public float Damage { get; }

    /// <summary>이번 공격이 크리티컬이었는가.</summary>
    public bool IsCrit { get; }

    /// <summary>크리티컬 · 기타 배율을 적용하기 전, 방어력까지만 반영한 데미지.</summary>
    public float BaseDamage { get; }

    public DamageResult(float damage, bool isCrit, float baseDamage)
    {
        Damage = damage;
        IsCrit = isCrit;
        BaseDamage = baseDamage;
    }
}

/// <summary>
/// 데미지 계산 파이프라인. (기획서 5.3)
///
///   최종 데미지 = (공격력 - 방어력 보정치) × 크리티컬 배율(발동 시 1.5배) × 기타 % 증가 배율
///
/// 입력은 4단계-1 의 <see cref="StatContainer"/>(공격자 · 피격자)에서 최종 스탯(<see cref="Stat.Value"/>)을 읽는다.
/// 크리티컬 발동 판정은 <see cref="ICritChanceRoller"/> 로 분리해 주입하므로 EditMode 로 결정적 검증이 가능하다.
///
/// MonoBehaviour 가 아닌 순수 클래스.
/// </summary>
public class DamageCalculator
{
    /// <summary>크리티컬 발동 시 곱해지는 배율. (기획서 5.3)</summary>
    public const float CritMultiplier = 1.5f;

    private readonly ICritChanceRoller _critRoller;
    private readonly float _minimumDamage;

    /// <param name="critRoller">크리티컬 발동 판정기. null 이면 <see cref="UnityCritChanceRoller"/>(난수).</param>
    /// <param name="minimumDamage">
    /// 방어력이 공격력 이상이어도 최소한 들어가는 데미지(크리티컬 · 기타 배율 적용 전).
    /// 방어 몬스터에게 데미지가 0 이 되어 전투가 교착되는 것을 막는다. 음수는 0 으로 클램프.
    /// </param>
    public DamageCalculator(ICritChanceRoller critRoller = null, float minimumDamage = 1f)
    {
        _critRoller = critRoller ?? new UnityCritChanceRoller();
        _minimumDamage = minimumDamage < 0f ? 0f : minimumDamage;
    }

    /// <summary>
    /// 한 번의 공격 데미지를 계산한다.
    /// </summary>
    /// <param name="attacker">공격자 스탯(공격력 · 크리티컬 확률을 읽음). null 이면 예외.</param>
    /// <param name="target">피격자 스탯(방어력을 읽음). null 이면 방어력 0 으로 본다.</param>
    /// <param name="extraMultiplier">기타 % 증가 배율(1 = 증가 없음). 음수는 0 으로 클램프.</param>
    public DamageResult Calculate(StatContainer attacker, StatContainer target, float extraMultiplier = 1f)
    {
        if (attacker == null)
        {
            throw new ArgumentNullException(nameof(attacker));
        }

        float attackPower = attacker.AttackPower.Value;
        float defense = target != null ? target.Defense.Value : 0f;
        float critRate = attacker.CritRate.Value;

        // (공격력 - 방어력 보정치). MVP 에서 "보정치" = 방어력 값 그대로(뺄셈).
        float afterDefense = attackPower - defense;
        if (afterDefense < _minimumDamage)
        {
            afterDefense = _minimumDamage;
        }

        // 크리티컬 확률이 0 이면 판정기를 호출조차 하지 않는다(불필요한 난수 소비 방지).
        bool isCrit = critRate > 0f && _critRoller.Roll(critRate);
        float critMul = isCrit ? CritMultiplier : 1f;

        float extra = extraMultiplier < 0f ? 0f : extraMultiplier;

        float damage = afterDefense * critMul * extra;
        return new DamageResult(damage, isCrit, afterDefense);
    }
}
