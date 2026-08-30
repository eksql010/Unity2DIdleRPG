using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타겟 선정 로직 (기획서 4.2): 살아있는 대상 중 가장 가까운 것.
/// 위치 리스트만 받는 순수 함수로 분리해 테스트 가능하게 둔다.
/// </summary>
public static class TargetSelector
{
    /// <summary>
    /// <paramref name="from"/> 에서 가장 가까운 위치의 인덱스를 반환한다. 없으면 -1.
    /// </summary>
    public static int GetNearestIndex(Vector2 from, IReadOnlyList<Vector2> positions)
    {
        int best = -1;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < positions.Count; i++)
        {
            float sqr = ((Vector2)positions[i] - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = i;
            }
        }

        return best;
    }
}
