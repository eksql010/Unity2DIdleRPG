using System;

/// <summary>
/// 오프라인 방치 보상을 "수학적으로" 계산하는 순수 함수.
/// 실시간 시뮬레이션이 아니라, 마지막 종료 시각과 현재 시각의 차이로 한 번에 산출한다.
/// 유니티 의존이 없어 EditMode 테스트로 빠르게 검증할 수 있다.
/// </summary>
public static class OfflineRewardCalculator
{
    /// <summary>
    /// 마지막 로그아웃(UTC) ~ 현재(UTC) 사이의 경과 시간으로 보상을 계산한다.
    /// </summary>
    /// <param name="lastLogoutUtc">마지막으로 저장된 종료 시각(UTC).</param>
    /// <param name="nowUtc">현재 시각(UTC).</param>
    /// <param name="config">보상 계산 설정.</param>
    public static OfflineRewardResult Calculate(DateTime lastLogoutUtc, DateTime nowUtc, OfflineRewardConfig config)
    {
        var result = new OfflineRewardResult();

        double elapsed = (nowUtc - lastLogoutUtc).TotalSeconds;

        // 시간 되돌리기 악용 방지: 현재 시각이 저장된 시각보다 이전이면 보상 없음
        if (elapsed <= 0.0)
        {
            return result;
        }

        double maxSeconds = Math.Max(0.0, config.maxOfflineHours) * 3600.0;
        double cappedSeconds = Math.Min(elapsed, maxSeconds);

        double killsPerSecond = Math.Max(0f, config.killsPerMinute) / 60.0;
        double totalKills = killsPerSecond * cappedSeconds;

        result.elapsedSeconds = elapsed;
        result.cappedSeconds = cappedSeconds;
        result.wasCapped = elapsed > maxSeconds;
        result.gainedExp = (int)Math.Floor(totalKills * Math.Max(0, config.expPerKill));
        result.gainedGold = (int)Math.Floor(totalKills * Math.Max(0, config.goldPerKill));
        return result;
    }
}
