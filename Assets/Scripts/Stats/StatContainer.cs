using System;

/// <summary>
/// MVP 에서 관리하는 스탯의 종류. (기획서 5.2 관리 대상 스탯 최소 세트)
/// </summary>
public enum StatType
{
    /// <summary>공격력.</summary>
    AttackPower,

    /// <summary>방어력.</summary>
    Defense,

    /// <summary>최대 체력.</summary>
    MaxHP,

    /// <summary>크리티컬 확률(0~1 비율).</summary>
    CritRate,

    /// <summary>이동속도(기획서 2.1 moveSpeed 와 연동).</summary>
    MoveSpeed,
}

/// <summary>
/// 한 캐릭터(플레이어/몬스터 공용)의 스탯 묶음. (기획서 5.1 / 7장 StatContainer)
/// 5개 스탯을 각각 <see cref="Stat"/>(기본값 + 수정자 + Dirty Flag)으로 들고 있다.
/// 플레이어와 몬스터가 완전히 같은 시스템을 재사용한다(기획서 5.4).
///
/// MonoBehaviour 가 아닌 순수 클래스 → EditMode 테스트로 검증 가능.
/// </summary>
public class StatContainer
{
    public Stat AttackPower { get; }
    public Stat Defense { get; }
    public Stat MaxHP { get; }
    public Stat CritRate { get; }
    public Stat MoveSpeed { get; }

    /// <param name="baseAttackPower">기본 공격력.</param>
    /// <param name="baseDefense">기본 방어력.</param>
    /// <param name="baseMaxHP">기본 최대 체력.</param>
    /// <param name="baseCritRate">기본 크리티컬 확률(0~1).</param>
    /// <param name="baseMoveSpeed">기본 이동속도.</param>
    public StatContainer(
        float baseAttackPower = 0f,
        float baseDefense = 0f,
        float baseMaxHP = 0f,
        float baseCritRate = 0f,
        float baseMoveSpeed = 0f)
    {
        AttackPower = new Stat(baseAttackPower);
        Defense = new Stat(baseDefense);
        MaxHP = new Stat(baseMaxHP);
        CritRate = new Stat(baseCritRate);
        MoveSpeed = new Stat(baseMoveSpeed);
    }

    /// <summary>종류로 스탯을 얻는다(수정자를 일괄 적용할 때 편리).</summary>
    public Stat Get(StatType type)
    {
        switch (type)
        {
            case StatType.AttackPower: return AttackPower;
            case StatType.Defense: return Defense;
            case StatType.MaxHP: return MaxHP;
            case StatType.CritRate: return CritRate;
            case StatType.MoveSpeed: return MoveSpeed;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "알 수 없는 스탯 종류");
        }
    }

    /// <summary>
    /// <see cref="MonsterData"/> 로부터 몬스터용 스탯 묶음을 만든다(기획서 5.4 재사용).
    /// 크리티컬/이동속도는 MVP 몬스터에서 쓰지 않으므로 0.
    /// </summary>
    public static StatContainer ForMonster(MonsterData data)
    {
        MonsterData d = (data ?? new MonsterData()).Sanitized();
        return new StatContainer(
            baseAttackPower: d.attackPower,
            baseDefense: d.defense,
            baseMaxHP: d.hp,
            baseCritRate: 0f,
            baseMoveSpeed: 0f);
    }
}
