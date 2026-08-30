/// <summary>
/// 스탯 하나에 적용되는 수정자 (기획서 5.1).
/// <paramref name="source"/> 는 이 수정자를 부여한 주체(장비, 버프 등)로,
/// 나중에 그 주체가 준 수정자만 한꺼번에 제거할 때 사용한다.
/// </summary>
public class StatModifier
{
    public readonly float Value;
    public readonly StatModifierType Type;

    /// <summary>같은 타입 내 적용 순서(작을수록 먼저). 기본은 타입 값을 사용.</summary>
    public readonly int Order;

    public readonly object Source;

    public StatModifier(float value, StatModifierType type, int order, object source)
    {
        Value = value;
        Type = type;
        Order = order;
        Source = source;
    }

    public StatModifier(float value, StatModifierType type)
        : this(value, type, (int)type, null)
    {
    }

    public StatModifier(float value, StatModifierType type, object source)
        : this(value, type, (int)type, source)
    {
    }
}
