using System;

/// <summary>
/// 스탯 수정자의 종류. (기획서 5.1 레이어드 스탯 구조)
/// 적용 순서: Flat → PercentAdd(모두 합산 후 한 번에) → PercentMultiply(각각 순차 곱연산).
/// </summary>
public enum StatModifierType
{
    /// <summary>고정값 더하기. 예: +10 공격력.</summary>
    Flat = 100,

    /// <summary>퍼센트 가산. 같은 종류끼리 먼저 합산한 뒤 한 번만 곱한다. 예: +20% 와 +30% → ×1.5.</summary>
    PercentAdd = 200,

    /// <summary>퍼센트 곱연산. 각각 따로 곱한다. 예: +20% 와 +30% → ×1.2 ×1.3 = ×1.56.</summary>
    PercentMultiply = 300,
}

/// <summary>
/// 스탯 하나에 붙는 수정자 한 개. (기획서 5.1)
/// 불변(immutable) 값 객체 — 한 번 만들면 바뀌지 않는다.
/// <see cref="Source"/> 로 "누가 이 수정자를 붙였는지"를 표시해 두면
/// 나중에 그 출처의 수정자만 한꺼번에 제거할 수 있다(장비 해제 등).
/// </summary>
public class StatModifier
{
    /// <summary>수정자 값. Flat 이면 절대값, Percent 계열이면 비율(0.2 = +20%).</summary>
    public float Value { get; }

    /// <summary>수정자 종류.</summary>
    public StatModifierType Type { get; }

    /// <summary>이 수정자를 붙인 주체(선택). null 이면 출처 없음.</summary>
    public object Source { get; }

    public StatModifier(float value, StatModifierType type, object source = null)
    {
        if (!Enum.IsDefined(typeof(StatModifierType), type))
        {
            throw new ArgumentException($"알 수 없는 수정자 종류: {type}", nameof(type));
        }

        Value = value;
        Type = type;
        Source = source;
    }

    /// <summary>편의 생성자 — 고정값 더하기.</summary>
    public static StatModifier Flat(float value, object source = null)
        => new StatModifier(value, StatModifierType.Flat, source);

    /// <summary>편의 생성자 — 퍼센트 가산(0.2 = +20%).</summary>
    public static StatModifier PercentAdd(float ratio, object source = null)
        => new StatModifier(ratio, StatModifierType.PercentAdd, source);

    /// <summary>편의 생성자 — 퍼센트 곱연산(0.2 = ×1.2).</summary>
    public static StatModifier PercentMultiply(float ratio, object source = null)
        => new StatModifier(ratio, StatModifierType.PercentMultiply, source);
}
