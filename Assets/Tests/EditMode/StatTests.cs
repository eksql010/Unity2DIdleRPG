using NUnit.Framework;

/// <summary>
/// 레이어드 스탯 + Dirty Flag 검증 (기획서 5.1).
/// </summary>
public class StatTests
{
    [Test]
    public void Value_NoModifiers_EqualsBase()
    {
        var stat = new Stat(100f);
        Assert.AreEqual(100f, stat.Value, 0.001f);
    }

    [Test]
    public void Flat_Adds_Directly()
    {
        var stat = new Stat(100f);
        stat.AddModifier(new StatModifier(15f, StatModifierType.Flat));
        Assert.AreEqual(115f, stat.Value, 0.001f);
    }

    [Test]
    public void PercentAdd_ModifiersAreSummedThenAppliedOnce()
    {
        var stat = new Stat(100f);
        stat.AddModifier(new StatModifier(0.10f, StatModifierType.PercentAdd));
        stat.AddModifier(new StatModifier(0.20f, StatModifierType.PercentAdd));
        // 100 * (1 + 0.30) = 130
        Assert.AreEqual(130f, stat.Value, 0.001f);
    }

    [Test]
    public void PercentMultiply_ModifiersAreAppliedSequentially()
    {
        var stat = new Stat(100f);
        stat.AddModifier(new StatModifier(0.10f, StatModifierType.PercentMultiply));
        stat.AddModifier(new StatModifier(0.20f, StatModifierType.PercentMultiply));
        // 100 * 1.10 * 1.20 = 132
        Assert.AreEqual(132f, stat.Value, 0.001f);
    }

    [Test]
    public void Order_FlatThenPercentAddThenPercentMultiply()
    {
        var stat = new Stat(100f);
        stat.AddModifier(new StatModifier(0.5f, StatModifierType.PercentMultiply));
        stat.AddModifier(new StatModifier(50f, StatModifierType.Flat));
        stat.AddModifier(new StatModifier(0.2f, StatModifierType.PercentAdd));
        // (100 + 50) * (1 + 0.2) * (1 + 0.5) = 150 * 1.2 * 1.5 = 270
        Assert.AreEqual(270f, stat.Value, 0.001f);
    }

    [Test]
    public void RemoveModifier_RevertsValue()
    {
        var stat = new Stat(100f);
        var mod = new StatModifier(0.5f, StatModifierType.PercentAdd);
        stat.AddModifier(mod);
        Assert.AreEqual(150f, stat.Value, 0.001f);

        Assert.IsTrue(stat.RemoveModifier(mod));
        Assert.AreEqual(100f, stat.Value, 0.001f);
    }

    [Test]
    public void RemoveAllFromSource_RemovesOnlyThatSource()
    {
        var stat = new Stat(100f);
        object buff = new object();
        object gear = new object();
        stat.AddModifier(new StatModifier(10f, StatModifierType.Flat, buff));
        stat.AddModifier(new StatModifier(20f, StatModifierType.Flat, gear));

        int removed = stat.RemoveAllFromSource(buff);
        Assert.AreEqual(1, removed);
        Assert.AreEqual(120f, stat.Value, 0.001f);
    }

    [Test]
    public void DirtyFlag_RecalculatesOnlyAfterChange()
    {
        int changedCount = 0;
        var stat = new Stat(10f);
        stat.Changed += () => changedCount++;

        float a = stat.Value; // 최초 1회 계산
        float b = stat.Value; // 캐시
        Assert.AreEqual(a, b);
        Assert.AreEqual(0, changedCount, "값 조회만으로는 Changed 가 발생하면 안 된다.");

        stat.AddModifier(new StatModifier(5f, StatModifierType.Flat));
        Assert.AreEqual(1, changedCount);
        Assert.AreEqual(15f, stat.Value, 0.001f);

        stat.BaseValue = 10f; // 동일 값 -> 변화 없음
        Assert.AreEqual(1, changedCount, "같은 값으로 설정하면 Dirty 처리하지 않는다.");

        stat.BaseValue = 20f;
        Assert.AreEqual(2, changedCount);
        Assert.AreEqual(25f, stat.Value, 0.001f);
    }
}
