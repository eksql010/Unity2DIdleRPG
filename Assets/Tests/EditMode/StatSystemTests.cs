using System;
using NUnit.Framework;

/// <summary>
/// 레이어드 스탯 + Dirty Flag + 데미지 공식 입력값(공격력/방어력)의 순수 계산 검증.
/// (기획서 5.1 / 5.2 / 5.4) 씬/플레이모드 불필요.
/// </summary>
public class StatSystemTests
{
    private const float Eps = 0.0001f;

    // --- 기본값 ---

    [Test]
    public void 수정자가_없으면_최종값은_기본값이다()
    {
        var stat = new Stat(10f);
        Assert.AreEqual(10f, stat.Value, Eps);
    }

    // --- Flat ---

    [Test]
    public void Flat_수정자는_기본값에_그대로_더해진다()
    {
        var stat = new Stat(10f);
        stat.AddModifier(StatModifier.Flat(5f));
        stat.AddModifier(StatModifier.Flat(3f));

        Assert.AreEqual(18f, stat.Value, Eps);
    }

    // --- PercentAdd ---

    [Test]
    public void PercentAdd_수정자들은_먼저_합산된_뒤_한_번만_곱해진다()
    {
        var stat = new Stat(100f);
        stat.AddModifier(StatModifier.PercentAdd(0.2f)); // +20%
        stat.AddModifier(StatModifier.PercentAdd(0.3f)); // +30%

        // 100 * (1 + 0.5) = 150
        Assert.AreEqual(150f, stat.Value, Eps);
    }

    // --- PercentMultiply ---

    [Test]
    public void PercentMultiply_수정자들은_각각_따로_곱해진다()
    {
        var stat = new Stat(100f);
        stat.AddModifier(StatModifier.PercentMultiply(0.2f)); // ×1.2
        stat.AddModifier(StatModifier.PercentMultiply(0.3f)); // ×1.3

        // 100 * 1.2 * 1.3 = 156
        Assert.AreEqual(156f, stat.Value, Eps);
    }

    // --- 적용 순서: Flat → PercentAdd → PercentMultiply ---

    [Test]
    public void 세_종류가_섞이면_Flat_다음_PercentAdd_다음_PercentMultiply_순서로_적용된다()
    {
        var stat = new Stat(100f);
        stat.AddModifier(StatModifier.PercentMultiply(0.1f)); // 넣는 순서를 일부러 뒤섞음
        stat.AddModifier(StatModifier.Flat(50f));
        stat.AddModifier(StatModifier.PercentAdd(0.5f));

        // (100 + 50) * (1 + 0.5) * (1 + 0.1) = 150 * 1.5 * 1.1 = 247.5
        Assert.AreEqual(247.5f, stat.Value, Eps);
    }

    // --- Dirty Flag ---

    [Test]
    public void 변경이_없으면_여러_번_읽어도_재계산하지_않는다()
    {
        var stat = new Stat(10f);
        stat.AddModifier(StatModifier.Flat(5f));

        float _ = stat.Value; // 첫 계산
        _ = stat.Value;
        _ = stat.Value;

        Assert.AreEqual(1, stat.RecalculationCount);
    }

    [Test]
    public void 기본값을_바꾸면_다음_읽기에서_한_번_재계산한다()
    {
        var stat = new Stat(10f);
        float _ = stat.Value;                 // 재계산 1
        stat.BaseValue = 20f;
        Assert.AreEqual(20f, stat.Value, Eps); // 재계산 2
        _ = stat.Value;                        // 캐시

        Assert.AreEqual(2, stat.RecalculationCount);
    }

    [Test]
    public void 같은_기본값을_다시_대입하면_Dirty가_되지_않는다()
    {
        var stat = new Stat(10f);
        float _ = stat.Value;
        stat.BaseValue = 10f; // 동일 값
        _ = stat.Value;

        Assert.AreEqual(1, stat.RecalculationCount);
    }

    [Test]
    public void 수정자를_추가하면_다음_읽기에서_재계산한다()
    {
        var stat = new Stat(10f);
        float _ = stat.Value; // 재계산 1

        stat.AddModifier(StatModifier.Flat(5f));
        Assert.AreEqual(15f, stat.Value, Eps); // 재계산 2
        Assert.AreEqual(2, stat.RecalculationCount);
    }

    // --- 수정자 제거 ---

    [Test]
    public void 특정_수정자_인스턴스를_제거하면_최종값에서_빠진다()
    {
        var stat = new Stat(10f);
        var mod = StatModifier.Flat(5f);
        stat.AddModifier(mod);
        Assert.AreEqual(15f, stat.Value, Eps);

        Assert.IsTrue(stat.RemoveModifier(mod));
        Assert.AreEqual(10f, stat.Value, Eps);
    }

    [Test]
    public void 붙어_있지_않은_수정자_제거는_false를_돌려주고_Dirty로_만들지_않는다()
    {
        var stat = new Stat(10f);
        float _ = stat.Value;

        Assert.IsFalse(stat.RemoveModifier(StatModifier.Flat(1f)));
        Assert.IsFalse(stat.RemoveModifier(null));
        _ = stat.Value;

        Assert.AreEqual(1, stat.RecalculationCount);
    }

    [Test]
    public void 출처로_수정자를_한꺼번에_제거할_수_있다()
    {
        var weapon = new object();
        var buff = new object();

        var stat = new Stat(100f);
        stat.AddModifier(StatModifier.Flat(10f, weapon));
        stat.AddModifier(StatModifier.PercentAdd(0.5f, weapon));
        stat.AddModifier(StatModifier.Flat(20f, buff));

        // (100 + 10 + 20) * 1.5 = 195
        Assert.AreEqual(195f, stat.Value, Eps);

        Assert.IsTrue(stat.RemoveAllModifiersFromSource(weapon));

        // 100 + 20 = 120 (weapon 의 Flat/PercentAdd 둘 다 제거됨)
        Assert.AreEqual(120f, stat.Value, Eps);
        Assert.AreEqual(1, stat.Modifiers.Count);
    }

    [Test]
    public void 없는_출처로_제거를_시도하면_false다()
    {
        var stat = new Stat(10f);
        stat.AddModifier(StatModifier.Flat(5f, new object()));

        Assert.IsFalse(stat.RemoveAllModifiersFromSource(new object()));
        Assert.IsFalse(stat.RemoveAllModifiersFromSource(null));
    }

    [Test]
    public void ClearModifiers_는_모든_수정자를_제거한다()
    {
        var stat = new Stat(10f);
        stat.AddModifier(StatModifier.Flat(5f));
        stat.AddModifier(StatModifier.PercentMultiply(1f));
        Assert.AreEqual(30f, stat.Value, Eps);

        stat.ClearModifiers();
        Assert.AreEqual(10f, stat.Value, Eps);
        Assert.AreEqual(0, stat.Modifiers.Count);
    }

    // --- 방어적 처리 ---

    [Test]
    public void null_수정자_추가는_거부된다()
    {
        var stat = new Stat(10f);
        Assert.Throws<ArgumentNullException>(() => stat.AddModifier(null));
    }

    [Test]
    public void 알_수_없는_수정자_종류는_생성자에서_거부된다()
    {
        Assert.Throws<ArgumentException>(
            () => new StatModifier(1f, (StatModifierType)999));
    }

    [Test]
    public void Modifiers_목록은_읽기_전용_뷰다()
    {
        var stat = new Stat(10f);
        stat.AddModifier(StatModifier.Flat(5f));

        Assert.IsInstanceOf<System.Collections.Generic.IReadOnlyList<StatModifier>>(stat.Modifiers);
        Assert.AreEqual(1, stat.Modifiers.Count);
    }

    // --- StatContainer ---

    [Test]
    public void StatContainer_는_기본값으로_5개_스탯을_초기화한다()
    {
        var c = new StatContainer(
            baseAttackPower: 20f, baseDefense: 5f, baseMaxHP: 100f,
            baseCritRate: 0.1f, baseMoveSpeed: 5f);

        Assert.AreEqual(20f, c.AttackPower.Value, Eps);
        Assert.AreEqual(5f, c.Defense.Value, Eps);
        Assert.AreEqual(100f, c.MaxHP.Value, Eps);
        Assert.AreEqual(0.1f, c.CritRate.Value, Eps);
        Assert.AreEqual(5f, c.MoveSpeed.Value, Eps);
    }

    [Test]
    public void StatContainer_Get_은_종류에_맞는_스탯을_돌려준다()
    {
        var c = new StatContainer(baseAttackPower: 20f);
        Assert.AreSame(c.AttackPower, c.Get(StatType.AttackPower));
        Assert.AreSame(c.MoveSpeed, c.Get(StatType.MoveSpeed));

        c.Get(StatType.AttackPower).AddModifier(StatModifier.PercentAdd(0.5f));
        Assert.AreEqual(30f, c.AttackPower.Value, Eps);
    }

    [Test]
    public void StatContainer_ForMonster_는_MonsterData_를_스탯으로_옮긴다()
    {
        var data = new MonsterData("orc", hp: 50f, attackPower: 12f, defense: 4f, expReward: 30, goldReward: 15);
        var c = StatContainer.ForMonster(data);

        Assert.AreEqual(12f, c.AttackPower.Value, Eps);
        Assert.AreEqual(4f, c.Defense.Value, Eps);
        Assert.AreEqual(50f, c.MaxHP.Value, Eps);
        Assert.AreEqual(0f, c.CritRate.Value, Eps);
    }

    [Test]
    public void StatContainer_ForMonster_는_오입력을_Sanitize_한다()
    {
        var data = new MonsterData("bad", hp: -10f, attackPower: -3f, defense: -1f, expReward: -5, goldReward: -2);
        var c = StatContainer.ForMonster(data);

        Assert.AreEqual(1f, c.MaxHP.Value, Eps);      // hp<=0 → 1
        Assert.AreEqual(0f, c.AttackPower.Value, Eps); // 음수 → 0
        Assert.AreEqual(0f, c.Defense.Value, Eps);
    }

    [Test]
    public void StatContainer_ForMonster_는_null_도_받는다()
    {
        Assert.DoesNotThrow(() => StatContainer.ForMonster(null));
    }

    // --- 실전 시나리오: 스탯이 바뀌면 다음 프레임에만 반영 ---

    [Test]
    public void 이동속도_버프를_붙였다_떼면_원래대로_돌아온다()
    {
        var c = new StatContainer(baseMoveSpeed: 5f);
        var haste = new object();

        c.MoveSpeed.AddModifier(StatModifier.PercentMultiply(0.5f, haste)); // 질주 +50%
        Assert.AreEqual(7.5f, c.MoveSpeed.Value, Eps);

        c.MoveSpeed.RemoveAllModifiersFromSource(haste);
        Assert.AreEqual(5f, c.MoveSpeed.Value, Eps);
    }
}
