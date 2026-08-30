using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TargetSelector 최근접 선택 검증 (기획서 4.2).
/// </summary>
public class TargetSelectorTests
{
    [Test]
    public void GetNearestIndex_PicksClosest()
    {
        var positions = new List<Vector2>
        {
            new Vector2(10f, 0f),
            new Vector2(3f, 1f),
            new Vector2(-5f, 2f),
        };

        int index = TargetSelector.GetNearestIndex(new Vector2(2f, 0f), positions);
        Assert.AreEqual(1, index);
    }

    [Test]
    public void GetNearestIndex_EmptyList_ReturnsMinusOne()
    {
        int index = TargetSelector.GetNearestIndex(Vector2.zero, new List<Vector2>());
        Assert.AreEqual(-1, index);
    }

    [Test]
    public void GetNearestIndex_ConsidersBothAxes()
    {
        var positions = new List<Vector2>
        {
            new Vector2(0f, 9f),
            new Vector2(4f, 0f),
        };

        int index = TargetSelector.GetNearestIndex(Vector2.zero, positions);
        Assert.AreEqual(1, index);
    }
}
