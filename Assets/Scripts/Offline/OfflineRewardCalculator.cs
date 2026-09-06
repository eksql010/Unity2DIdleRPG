using System;

/// <summary>
/// 오프라인(방치) 보상 계산 결과. (기획서 7장 OfflineRewardResult)
/// </summary>
public class OfflineRewardResult
{
    /// <summary>보상 계산에 실제로 사용된 경과 시간(초). 캡이 걸리면 캡 값으로 잘린다.</summary>
    public double elapsedSeconds;

    /// <summary>캡을 적용하기 전, 실제로 흐른 경과 시간(초).</summary>
    public double rawElapsedSeconds;

    /// <summary>획득 경험치.</summary>
    public int gainedExp;

    /// <summary>획득 골드.</summary>
    public int gainedGold;

    /// <summary>경과 시간이 최대 캡을 초과해 잘렸는가.</summary>
    public bool capped;

    /// <summary>시간 되돌리기 등으로 보상 계산을 건너뛰었는가(모든 보상 0).</summary>
    public bool rejected;
}

/// <summary>
/// 오프라인 방치 보상을 "수학적으로" 계산하는 순수 클래스. (기획서 6장)
/// 실시간 시뮬레이션이 아니라, 재접속 시점의 경과 시간만으로 보상을 산출한다.
/// MonoBehaviour 가 아니며 DateTime 을 인자로 받으므로 EditMode 테스트로 검증 가능하다.
/// </summary>
public class OfflineRewardCalculator
{
    /// <summary>분당 처치 수. 현재 스테이지에서 자동전투 시 1분간 잡는 몬스터 수. (기획서 6.1)</summary>
    public double killsPerMinute;

    /// <summary>몬스터 1마리 처치당 경험치.</summary>
    public double expPerKill;

    /// <summary>몬스터 1마리 처치당 골드.</summary>
    public double goldPerKill;

    /// <summary>보상으로 인정하는 최대 경과 시간(초). 기본 8시간. (기획서 6.2)</summary>
    public double maxRewardSeconds;

    /// <summary>기본 캡: 8시간(초).</summary>
    public const double DefaultMaxRewardSeconds = 8 * 60 * 60;

    /// <param name="killsPerMinute">분당 처치 수.</param>
    /// <param name="expPerKill">처치당 경험치.</param>
    /// <param name="goldPerKill">처치당 골드.</param>
    /// <param name="maxRewardSeconds">최대 보상 인정 시간(초). 0 이하면 기본 8시간.</param>
    public OfflineRewardCalculator(
        double killsPerMinute,
        double expPerKill,
        double goldPerKill,
        double maxRewardSeconds = DefaultMaxRewardSeconds)
    {
        // 음수 설정값은 0 으로 막는다(잘못된 인스펙터 입력 방어).
        this.killsPerMinute = Math.Max(0.0, killsPerMinute);
        this.expPerKill = Math.Max(0.0, expPerKill);
        this.goldPerKill = Math.Max(0.0, goldPerKill);
        this.maxRewardSeconds = maxRewardSeconds > 0.0 ? maxRewardSeconds : DefaultMaxRewardSeconds;
    }

    /// <summary>
    /// 마지막 접속 시각과 현재 시각으로 오프라인 보상을 계산한다.
    /// 두 시각은 같은 기준(권장: UTC)이어야 한다.
    /// </summary>
    /// <param name="lastSeen">앱을 마지막으로 종료(또는 저장)한 시각.</param>
    /// <param name="now">재접속한 현재 시각.</param>
    public OfflineRewardResult Calculate(DateTime lastSeen, DateTime now)
    {
        var result = new OfflineRewardResult();

        double elapsed = (now - lastSeen).TotalSeconds;

        // 시간 되돌리기 악용 방지: 현재 시각이 마지막 시각보다 이전(또는 동일)이면 건너뛴다. (기획서 6.2)
        if (elapsed <= 0.0)
        {
            result.rejected = true;
            return result;
        }

        result.rawElapsedSeconds = elapsed;

        double effective = elapsed;
        if (effective > maxRewardSeconds)
        {
            effective = maxRewardSeconds;
            result.capped = true;
        }
        result.elapsedSeconds = effective;

        // 초당 처치율 × 경과 시간(초) = 총 처치 수. (기획서 6.1)
        double kills = (killsPerMinute / 60.0) * effective;

        // 보상은 정수로 내림 처리.
        result.gainedExp = (int)Math.Floor(kills * expPerKill);
        result.gainedGold = (int)Math.Floor(kills * goldPerKill);

        return result;
    }
}
