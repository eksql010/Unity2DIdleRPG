using System;

/// <summary>
/// 몬스터 1종의 정적 데이터. (기획서 7장 MonsterData, 5.4 몬스터 스탯 최소 세트)
/// MonoBehaviour 가 아니라 순수 직렬화 클래스이므로 스포너 인스펙터에서 값을 튜닝할 수 있고
/// EditMode 테스트에서도 자유롭게 만들 수 있다.
/// 4단계(스탯/데미지 파이프라인)에서 StatContainer 로 확장될 수 있으나, MVP 3단계에서는
/// HP/공격력/방어력/보상만 담는다.
/// </summary>
[Serializable]
public class MonsterData
{
    /// <summary>몬스터 식별자(디버그/로그용).</summary>
    public string monsterId = "slime";

    /// <summary>최대 체력. 스폰 시 현재 체력의 초기값이 된다.</summary>
    public float hp = 30f;

    /// <summary>공격력. (3단계에서는 미사용, 4단계 데미지 계산에서 사용)</summary>
    public float attackPower = 5f;

    /// <summary>방어력. (3단계에서는 미사용, 4단계 데미지 계산에서 사용)</summary>
    public float defense = 1f;

    /// <summary>처치 시 지급 경험치.</summary>
    public int expReward = 10;

    /// <summary>처치 시 지급 골드.</summary>
    public int goldReward = 5;

    public MonsterData() { }

    /// <summary>테스트/런타임에서 값을 한 번에 지정하기 위한 편의 생성자.</summary>
    public MonsterData(string monsterId, float hp, float attackPower, float defense, int expReward, int goldReward)
    {
        this.monsterId = monsterId;
        this.hp = hp;
        this.attackPower = attackPower;
        this.defense = defense;
        this.expReward = expReward;
        this.goldReward = goldReward;
    }

    /// <summary>인스펙터 오입력(음수 등) 방어용 복사본. 원본은 건드리지 않는다.</summary>
    public MonsterData Sanitized()
    {
        return new MonsterData(
            string.IsNullOrEmpty(monsterId) ? "monster" : monsterId,
            hp > 0f ? hp : 1f,
            attackPower < 0f ? 0f : attackPower,
            defense < 0f ? 0f : defense,
            expReward < 0 ? 0 : expReward,
            goldReward < 0 ? 0 : goldReward);
    }
}
