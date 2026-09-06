using System;
using NUnit.Framework;

/// <summary>
/// 데미지 계산 파이프라인 검증. (기획서 5.3)
/// 크리티컬 판정을 페이크로 주입해 결정적으로 검증한다. 씬/플레이모드 불필요.
/// </summary>
public class DamageCalculatorTests
{
    private const float Eps = 0.0001f;

    /// <summary>고정된 결과만 돌려주고, 넘겨받은 확률을 기록하는 페이크 판정기.</summary>
    private class FixedRoller : ICritChanceRoller
    {
        private readonly bool _result;
        public float LastProbability { get; private set; } = float.NaN;
        public int RollCount { get; private set; }

        public FixedRoller(bool result) { _result = result; }

        public bool Roll(float probability)
        {
            LastProbability = probability;
            RollCount++;
            return _result;
        }
    }

    private static StatContainer Attacker(float attackPower, float critRate = 0f)
        => new StatContainer(baseAttackPower: attackPower, baseCritRate: critRate);

    private static StatContainer Target(float defense)
        => new StatContainer(baseDefense: defense);

    // --- 기본 공식 ---

    [Test]
    public void 크리티컬이_없으면_데미지는_공격력에서_방어력을_뺀_값이다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        DamageResult r = calc.Calculate(Attacker(20f), Target(5f));

        Assert.AreEqual(15f, r.Damage, Eps);
        Assert.AreEqual(15f, r.BaseDamage, Eps);
        Assert.IsFalse(r.IsCrit);
    }

    [Test]
    public void 크리티컬이_발동하면_1_5배가_곱해진다()
    {
        var calc = new DamageCalculator(new FixedRoller(true));

        DamageResult r = calc.Calculate(Attacker(20f, critRate: 0.5f), Target(0f));

        Assert.AreEqual(30f, r.Damage, Eps);
        Assert.AreEqual(20f, r.BaseDamage, Eps);
        Assert.IsTrue(r.IsCrit);
    }

    [Test]
    public void 기타_증가_배율이_곱해진다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        DamageResult r = calc.Calculate(Attacker(10f), Target(0f), extraMultiplier: 2f);

        Assert.AreEqual(20f, r.Damage, Eps);
    }

    [Test]
    public void 크리티컬과_기타_배율은_함께_곱해진다()
    {
        var calc = new DamageCalculator(new FixedRoller(true));

        // (10 - 0) × 1.5(크리) × 1.5(기타) = 22.5
        DamageResult r = calc.Calculate(Attacker(10f, critRate: 1f), Target(0f), extraMultiplier: 1.5f);

        Assert.AreEqual(22.5f, r.Damage, Eps);
    }

    // --- 최소 데미지 ---

    [Test]
    public void 방어력이_공격력보다_높아도_최소_데미지는_보장된다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        DamageResult r = calc.Calculate(Attacker(5f), Target(100f));

        Assert.AreEqual(1f, r.Damage, Eps);       // 기본 최소 데미지 1
        Assert.AreEqual(1f, r.BaseDamage, Eps);
    }

    [Test]
    public void 최소_데미지는_생성자로_바꿀_수_있다()
    {
        var calc = new DamageCalculator(new FixedRoller(false), minimumDamage: 3f);

        DamageResult r = calc.Calculate(Attacker(1f), Target(10f));

        Assert.AreEqual(3f, r.Damage, Eps);
    }

    [Test]
    public void 음수_최소_데미지는_0으로_클램프된다()
    {
        var calc = new DamageCalculator(new FixedRoller(false), minimumDamage: -5f);

        DamageResult r = calc.Calculate(Attacker(2f), Target(10f));

        Assert.AreEqual(0f, r.Damage, Eps);
    }

    // --- 크리티컬 확률 전달 ---

    [Test]
    public void 크리티컬_확률은_공격자_CritRate에서_읽어_판정기에_전달된다()
    {
        var roller = new FixedRoller(false);
        var calc = new DamageCalculator(roller);

        calc.Calculate(Attacker(10f, critRate: 0.25f), Target(0f));

        Assert.AreEqual(0.25f, roller.LastProbability, Eps);
        Assert.AreEqual(1, roller.RollCount);
    }

    [Test]
    public void CritRate가_0이면_판정기를_호출하지_않고_크리티컬이_아니다()
    {
        var roller = new FixedRoller(true); // 호출되면 크리티컬이 될 것
        var calc = new DamageCalculator(roller);

        DamageResult r = calc.Calculate(Attacker(10f, critRate: 0f), Target(0f));

        Assert.IsFalse(r.IsCrit);
        Assert.AreEqual(0, roller.RollCount);
    }

    [Test]
    public void 스탯_수정자가_반영된_크리티컬_확률로_판정한다()
    {
        var roller = new FixedRoller(false);
        var calc = new DamageCalculator(roller);
        var attacker = Attacker(10f, critRate: 0.1f);
        attacker.CritRate.AddModifier(StatModifier.Flat(0.2f)); // 0.1 → 0.3

        calc.Calculate(attacker, Target(0f));

        Assert.AreEqual(0.3f, roller.LastProbability, Eps);
    }

    // --- 방어적 처리 ---

    [Test]
    public void 피격자가_null이면_방어력을_0으로_본다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        DamageResult r = calc.Calculate(Attacker(12f), null);

        Assert.AreEqual(12f, r.Damage, Eps);
    }

    [Test]
    public void 공격자가_null이면_ArgumentNullException()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        Assert.Throws<ArgumentNullException>(() => calc.Calculate(null, Target(0f)));
    }

    [Test]
    public void 음수_기타_배율은_0으로_클램프된다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        DamageResult r = calc.Calculate(Attacker(10f), Target(0f), extraMultiplier: -1f);

        Assert.AreEqual(0f, r.Damage, Eps);
    }

    // --- 스탯 파이프라인 연동 ---

    [Test]
    public void 공격력과_방어력은_수정자가_적용된_최종값으로_계산한다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));

        var attacker = Attacker(20f);
        attacker.AttackPower.AddModifier(StatModifier.Flat(10f)); // 20 → 30

        var target = Target(5f);
        target.Defense.AddModifier(StatModifier.Flat(5f));        // 5 → 10

        DamageResult r = calc.Calculate(attacker, target);

        Assert.AreEqual(20f, r.Damage, Eps); // 30 - 10
    }

    [Test]
    public void 몬스터_스탯으로도_계산할_수_있다()
    {
        var calc = new DamageCalculator(new FixedRoller(false));
        var monster = StatContainer.ForMonster(
            new MonsterData("orc", hp: 50f, attackPower: 12f, defense: 4f, expReward: 0, goldReward: 0));

        // 플레이어(공격력 20)가 몬스터(방어력 4)를 때림 → 16
        DamageResult r = calc.Calculate(Attacker(20f), monster);
        Assert.AreEqual(16f, r.Damage, Eps);

        // 몬스터(공격력 12)가 플레이어(방어력 3)를 때림 → 9
        DamageResult r2 = calc.Calculate(monster, Target(3f));
        Assert.AreEqual(9f, r2.Damage, Eps);
    }

    // --- 기본(난수) 판정기 경계 ---

    [Test]
    public void 기본_생성자의_난수_판정기는_확률_1이면_항상_크리티컬_0이면_항상_비크리티컬()
    {
        var calc = new DamageCalculator(); // UnityCritChanceRoller

        DamageResult always = calc.Calculate(Attacker(10f, critRate: 1f), Target(0f));
        Assert.IsTrue(always.IsCrit);
        Assert.AreEqual(15f, always.Damage, Eps);

        DamageResult never = calc.Calculate(Attacker(10f, critRate: 0f), Target(0f));
        Assert.IsFalse(never.IsCrit);
        Assert.AreEqual(10f, never.Damage, Eps);
    }

    [Test]
    public void BaseDamage는_배율_적용_전_값이고_Damage는_적용_후_값이다()
    {
        var calc = new DamageCalculator(new FixedRoller(true));

        // (20 - 4) = 16, × 1.5(크리) × 2(기타) = 48
        DamageResult r = calc.Calculate(Attacker(20f, critRate: 1f), Target(4f), extraMultiplier: 2f);

        Assert.AreEqual(16f, r.BaseDamage, Eps);
        Assert.AreEqual(48f, r.Damage, Eps);
        Assert.IsTrue(r.IsCrit);
    }
}
