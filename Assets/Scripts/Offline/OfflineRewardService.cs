using System;
using System.Globalization;

/// <summary>
/// 현재 UTC 시각을 제공하는 시계 추상화.
/// 테스트에서 시간을 주입하기 위해 인터페이스로 분리한다. (기획서 6.1 — 시각 기반 계산)
/// </summary>
public interface IOfflineClock
{
    DateTime UtcNow { get; }
}

/// <summary>시스템 시계(DateTime.UtcNow) 구현. 런타임 기본값.</summary>
public class SystemOfflineClock : IOfflineClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// 마지막 접속(종료) 시각을 저장/복원하는 저장소 추상화.
/// 시각은 항상 UTC 로 저장한다. (기획서 6.1 "앱 종료 시각을 저장")
/// </summary>
public interface IOfflineTimeStore
{
    /// <summary>저장된 마지막 접속 시각(UTC). 저장된 적이 없거나 값이 손상됐으면 null.</summary>
    DateTime? LoadLastSeenUtc();

    /// <summary>마지막 접속 시각을 UTC 로 저장한다. (UTC 가 아니면 UTC 로 변환)</summary>
    void SaveLastSeenUtc(DateTime time);

    /// <summary>저장된 시각을 지운다(첫 실행 상태로 되돌림).</summary>
    void Clear();
}

/// <summary>
/// PlayerPrefs 기반 저장소. ISO 8601 왕복 형식("o") 문자열로 UTC 시각을 보관한다.
/// </summary>
public class PlayerPrefsOfflineTimeStore : IOfflineTimeStore
{
    /// <summary>PlayerPrefs 기본 키.</summary>
    public const string DefaultKey = "offline.lastSeenUtc";

    private readonly string key;

    public PlayerPrefsOfflineTimeStore(string key = DefaultKey)
    {
        this.key = string.IsNullOrEmpty(key) ? DefaultKey : key;
    }

    public DateTime? LoadLastSeenUtc()
    {
        string raw = UnityEngine.PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return null;

        // "o" 포맷은 항상 'Z'(UTC) 로 저장되므로 RoundtripKind 로 파싱하면 Kind=Utc 가 된다.
        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        // 손상된 값은 저장된 적 없는 것으로 취급한다.
        return null;
    }

    public void SaveLastSeenUtc(DateTime time)
    {
        DateTime asUtc = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();
        UnityEngine.PlayerPrefs.SetString(key, asUtc.ToString("o", CultureInfo.InvariantCulture));
        UnityEngine.PlayerPrefs.Save();
    }

    public void Clear()
    {
        UnityEngine.PlayerPrefs.DeleteKey(key);
        UnityEngine.PlayerPrefs.Save();
    }
}

/// <summary>
/// 오프라인 방치 보상의 "서비스 계층". (기획서 6장)
/// 순수 계산기(OfflineRewardCalculator) + 저장소(마지막 접속 시각) + 시계 를 조합해
///  - 앱 시작/종료·일시정지 시 현재 UTC 시각을 저장하고 (MarkSeen)
///  - 재접속 시 저장된 시각과 현재 시각으로 보상을 계산한 뒤 기준 시각을 갱신한다 (ClaimOnReconnect)
/// MonoBehaviour 가 아니므로 EditMode 테스트로 검증 가능하다.
/// Unity 수명주기 훅(OnApplicationPause/Quit) 및 보상 팝업 UI 연결은 2단계-3에서 한다.
/// </summary>
public class OfflineRewardService
{
    private readonly OfflineRewardCalculator calculator;
    private readonly IOfflineTimeStore store;
    private readonly IOfflineClock clock;

    public OfflineRewardService(
        OfflineRewardCalculator calculator,
        IOfflineTimeStore store,
        IOfflineClock clock = null)
    {
        this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? new SystemOfflineClock();
    }

    /// <summary>오프라인 보상을 계산할 기준(마지막 접속 시각)이 저장돼 있는가.</summary>
    public bool HasLastSeen => store.LoadLastSeenUtc().HasValue;

    /// <summary>
    /// 현재 UTC 시각을 "마지막 접속 시각"으로 저장한다.
    /// 앱 종료·일시정지 시, 그리고 첫 실행 기준점을 잡을 때 호출한다.
    /// </summary>
    public void MarkSeen()
    {
        store.SaveLastSeenUtc(clock.UtcNow);
    }

    /// <summary>
    /// 재접속 시 오프라인 보상을 계산한다.
    /// 저장된 시각이 없으면(첫 실행) 계산하지 않고 현재 시각만 기록한 뒤 null 을 돌려준다.
    /// 정상 계산되면 기준 시각을 현재로 갱신해 같은 구간을 두 번 보상하지 않는다.
    /// 시간 되돌리기(rejected)면 기준 시각을 뒤로 밀지 않는다(단조 증가 유지).
    /// </summary>
    public OfflineRewardResult ClaimOnReconnect()
    {
        DateTime now = clock.UtcNow;
        DateTime? lastSeen = store.LoadLastSeenUtc();

        if (!lastSeen.HasValue)
        {
            store.SaveLastSeenUtc(now);
            return null;
        }

        OfflineRewardResult result = calculator.Calculate(lastSeen.Value, now);

        if (!result.rejected)
            store.SaveLastSeenUtc(now);

        return result;
    }
}
