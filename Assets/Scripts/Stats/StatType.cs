/// <summary>
/// 관리 대상 스탯 종류 (기획서 5.2 최소 세트).
/// </summary>
public enum StatType
{
    AttackPower,
    Defense,
    MaxHP,
    CritRate,
    MoveSpeed,
}

/// <summary>
/// 스탯 수정자의 적용 방식 (기획서 5.1).
///   Flat            : 고정값 더하기
///   PercentAdd      : 퍼센트 가산 (여러 개가 먼저 합산된 뒤 한 번에 곱해짐)
///   PercentMultiply : 퍼센트 곱연산 (각각 순차적으로 곱해짐)
/// </summary>
public enum StatModifierType
{
    Flat = 100,
    PercentAdd = 200,
    PercentMultiply = 300,
}
