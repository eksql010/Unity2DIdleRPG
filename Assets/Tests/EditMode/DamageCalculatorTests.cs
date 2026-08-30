using NUnit.Framework;

/// <summary>
/// DamageCalculator 검증 (기획서 5.3).
/// </summary>
public class DamageCalculatorTests
{
    [Test]
    public void Calculate_NonCrit_SubtractsDefense()
    {
        // critRoll(0.9) >= critRate(0.0) -> 비크리
        DamageResult r = DamageCalculator.CalculateWithRoll(20f, 8f, 0.9f, 0f);
        Assert.AreEqual(12, r.Amount);
        Assert.IsFalse(r.IsCritical);
    }

    [Test]
    public void Calculate_Crit_AppliesMultiplier()
    {
        // critRoll(0.1) < critRate(1.0) -> 크리, 배율 1.5
        DamageResult r = DamageCalculator.CalculateWithRoll(20f, 8f, 0.1f, 1f, 1.5f);
        Assert.AreEqual(18, r.Amount); // (20-8) * 1.5
        Assert.IsTrue(r.IsCritical);
    }

    [Test]
    public void Calculate_DefenseHigherThanAttack_ClampsToMinimumOne()
    {
        DamageResult r = DamageCalculator.CalculateWithRoll(5f, 100f, 0.9f, 0f);
        Assert.AreEqual(1, r.Amount);
    }

    [Test]
    public void Calculate_ExtraMultiplier_Stacks()
    {
        // (30-10)=20, 비크리, extra 2.0 -> 40
        DamageResult r = DamageCalculator.CalculateWithRoll(30f, 10f, 0.9f, 0f, 1.5f, 2.0f);
        Assert.AreEqual(40, r.Amount);
    }

    [Test]
    public void Calculate_FloorsFractionalDamage()
    {
        // (10-3)=7, 크리 1.5 -> 10.5 -> 내림 10
        DamageResult r = DamageCalculator.CalculateWithRoll(10f, 3f, 0.0f, 1f, 1.5f);
        Assert.AreEqual(10, r.Amount);
        Assert.IsTrue(r.IsCritical);
    }
}
