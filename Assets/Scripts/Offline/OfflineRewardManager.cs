using System;
using UnityEngine;

/// <summary>
/// 오프라인 보상의 상태 저장/복원과 로그인·로그아웃 흐름을 담당한다.
/// - 로그아웃 시 현재 UTC 시각을 PlayerPrefs 에 저장한다.
/// - 로그인(또는 앱 재시작) 시 저장된 시각과 현재 시각 차이로 보상을 계산한다.
/// 앱을 완전히 종료(OnApplicationQuit)해도 로그아웃과 동일하게 시각이 저장된다.
/// </summary>
public class OfflineRewardManager : MonoBehaviour
{
    private const string KeyLastLogoutTicks = "offline_last_logout_ticks";
    private const string KeyIsLoggedIn = "offline_is_logged_in";
    private const string KeyTotalExp = "offline_total_exp";
    private const string KeyTotalGold = "offline_total_gold";

    [SerializeField] private OfflineRewardConfig config = new OfflineRewardConfig();

    /// <summary>보상 계산 설정(읽기 전용 접근).</summary>
    public OfflineRewardConfig Config => config;

    /// <summary>현재 로그인 상태인지.</summary>
    public bool IsLoggedIn { get; private set; }

    /// <summary>지금까지 오프라인 보상으로 누적된 경험치.</summary>
    public int TotalExp { get; private set; }

    /// <summary>지금까지 오프라인 보상으로 누적된 골드.</summary>
    public int TotalGold { get; private set; }

    private void Awake()
    {
        IsLoggedIn = PlayerPrefs.GetInt(KeyIsLoggedIn, 0) == 1;
        TotalExp = PlayerPrefs.GetInt(KeyTotalExp, 0);
        TotalGold = PlayerPrefs.GetInt(KeyTotalGold, 0);
    }

    /// <summary>로그아웃: 현재 UTC 시각을 종료 시각으로 저장한다.</summary>
    public void Logout()
    {
        PlayerPrefs.SetString(KeyLastLogoutTicks, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.SetInt(KeyIsLoggedIn, 0);
        PlayerPrefs.Save();
        IsLoggedIn = false;
    }

    /// <summary>
    /// 로그인: 저장된 종료 시각 ~ 현재 시각으로 보상을 계산해 누적한다.
    /// 이전 종료 기록이 전혀 없으면 null 을 반환한다.
    /// </summary>
    public OfflineRewardResult Login()
    {
        OfflineRewardResult result = null;

        string saved = PlayerPrefs.GetString(KeyLastLogoutTicks, string.Empty);
        long ticks;
        if (!string.IsNullOrEmpty(saved) && long.TryParse(saved, out ticks))
        {
            DateTime lastLogoutUtc = new DateTime(ticks, DateTimeKind.Utc);
            result = OfflineRewardCalculator.Calculate(lastLogoutUtc, DateTime.UtcNow, config);

            if (result.HasReward)
            {
                TotalExp += result.gainedExp;
                TotalGold += result.gainedGold;
                PlayerPrefs.SetInt(KeyTotalExp, TotalExp);
                PlayerPrefs.SetInt(KeyTotalGold, TotalGold);
            }
        }

        PlayerPrefs.SetInt(KeyIsLoggedIn, 1);
        // 로그인 순간을 새 기준 시각으로 저장(다음 로그아웃 전에 앱이 꺼져도 여기서부터 계산)
        PlayerPrefs.SetString(KeyLastLogoutTicks, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
        IsLoggedIn = true;

        return result;
    }

    /// <summary>
    /// [테스트용] 저장된 종료 시각을 과거로 당겨 오프라인 경과 시간을 인위적으로 늘린다.
    /// 로그아웃 상태에서 호출해야 의미가 있다.
    /// </summary>
    public void AddDebugOfflineTime(double hours)
    {
        string saved = PlayerPrefs.GetString(KeyLastLogoutTicks, string.Empty);
        long ticks;
        if (!string.IsNullOrEmpty(saved) && long.TryParse(saved, out ticks))
        {
            DateTime backdated = new DateTime(ticks, DateTimeKind.Utc).AddHours(-hours);
            PlayerPrefs.SetString(KeyLastLogoutTicks, backdated.Ticks.ToString());
            PlayerPrefs.Save();
        }
    }

    /// <summary>[테스트용] 저장된 모든 오프라인 보상 상태를 초기화한다.</summary>
    public void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyLastLogoutTicks);
        PlayerPrefs.DeleteKey(KeyIsLoggedIn);
        PlayerPrefs.DeleteKey(KeyTotalExp);
        PlayerPrefs.DeleteKey(KeyTotalGold);
        PlayerPrefs.Save();
        IsLoggedIn = false;
        TotalExp = 0;
        TotalGold = 0;
    }

    private void OnApplicationQuit()
    {
        // 앱을 완전히 종료하는 것도 로그아웃으로 처리(종료 시각 저장)
        if (IsLoggedIn)
        {
            Logout();
        }
    }
}
