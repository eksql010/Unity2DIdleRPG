using System;
using UnityEngine;

/// <summary>
/// 오프라인 방치 보상 계산에 필요한 설정값.
/// MVP 단계이므로 스탯 파이프라인(4단계)과 분리된 단순 값 묶음으로 둔다.
/// </summary>
[Serializable]
public class OfflineRewardConfig
{
    [Tooltip("자동전투 시 분당 처치 수. 초당 처치율 = 이 값 / 60.")]
    [Min(0f)]
    public float killsPerMinute = 30f;

    [Tooltip("몬스터 1마리 처치당 경험치.")]
    [Min(0)]
    public int expPerKill = 12;

    [Tooltip("몬스터 1마리 처치당 골드.")]
    [Min(0)]
    public int goldPerKill = 7;

    [Tooltip("오프라인 보상으로 인정하는 최대 시간(시간 단위). 이 시간을 넘으면 캡으로 고정된다.")]
    [Min(0f)]
    public float maxOfflineHours = 8f;
}
