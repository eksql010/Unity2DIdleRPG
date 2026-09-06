using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프라인 방치 보상의 "씬 연결 계층". (기획서 6장, 특히 6.3 UI)
/// <see cref="OfflineRewardService"/>(계산기 + PlayerPrefs 저장소 + 시스템 시계)를 구성하고
///  - 게임 시작 = 재접속: 저장된 종료 시각 기준으로 보상을 계산해 팝업으로 표시
///  - Logout 버튼: 현재 시각을 종료 시각으로 저장 (<see cref="OfflineRewardService.MarkSeen"/>)
///  - Login 버튼: 저장된 종료 시각 기준으로 보상을 재계산해 팝업으로 표시
///  - 앱 일시정지/종료 시에도 종료 시각을 저장
/// MonoBehaviour 라 Unity 수명주기 훅과 UI 갱신만 맡고, 계산·저장 규칙은 서비스에 위임한다.
/// </summary>
public class OfflineRewardController : MonoBehaviour
{
    [Header("보상 계산 튜닝값 (기획서 6.1)")]
    [Tooltip("자동전투 시 분당 처치 수.")]
    [SerializeField] private double killsPerMinute = 60.0;
    [Tooltip("몬스터 1마리 처치당 경험치.")]
    [SerializeField] private double expPerKill = 10.0;
    [Tooltip("몬스터 1마리 처치당 골드.")]
    [SerializeField] private double goldPerKill = 5.0;
    [Tooltip("보상으로 인정하는 최대 오프라인 시간(시간 단위). 기획서 6.2 캡. 0 이하면 기본 8시간.")]
    [SerializeField] private double maxRewardHours = 8.0;

    [Header("UI 참조")]
    [Tooltip("보상 팝업 루트. 표시/숨김을 토글한다.")]
    [SerializeField] private GameObject popupPanel;
    [Tooltip("팝업 본문 텍스트 (경과 시간 + 획득 경험치/골드).")]
    [SerializeField] private Text rewardText;
    [Tooltip("화면 상단 상태 표시 (로그인/로그아웃 여부).")]
    [SerializeField] private Text statusText;
    [Tooltip("Login 버튼 — 저장된 종료 시각 기준으로 보상 재계산.")]
    [SerializeField] private Button loginButton;
    [Tooltip("Logout 버튼 — 현재 시각을 종료 시각으로 저장.")]
    [SerializeField] private Button logoutButton;
    [Tooltip("팝업 닫기 버튼.")]
    [SerializeField] private Button closeButton;

    private OfflineRewardService _service;

    private void Awake()
    {
        double capSeconds = maxRewardHours > 0.0
            ? maxRewardHours * 3600.0
            : OfflineRewardCalculator.DefaultMaxRewardSeconds;

        var calculator = new OfflineRewardCalculator(killsPerMinute, expPerKill, goldPerKill, capSeconds);
        _service = new OfflineRewardService(calculator, new PlayerPrefsOfflineTimeStore());

        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);
        if (closeButton != null) closeButton.onClick.AddListener(HidePopup);
    }

    private void Start()
    {
        HidePopup();

        // 게임 실행 = 재접속. 저장된 종료 시각이 있으면 경과 시간만큼 보상을 계산해 보여준다.
        OfflineRewardResult result = _service.ClaimOnReconnect();
        if (HasReward(result))
        {
            ShowPopup(result);
        }

        UpdateStatus(_service.HasLastSeen ? "온라인 — 마지막 접속 시각 기록됨" : "온라인 — 첫 실행");
    }

    /// <summary>Login 버튼: 저장된 종료 시각 기준으로 오프라인 보상을 재계산해 팝업으로 표시한다.</summary>
    public void OnLoginClicked()
    {
        OfflineRewardResult result = _service.ClaimOnReconnect();
        ShowPopup(result);
        UpdateStatus("온라인 — 로그인함");
    }

    /// <summary>Logout 버튼: 현재 시각을 종료 시각으로 저장한다. 이후 Login 하면 그 사이 경과분만큼 보상.</summary>
    public void OnLogoutClicked()
    {
        _service.MarkSeen();
        HidePopup();
        UpdateStatus("오프라인 — 로그아웃함 (종료 시각 저장됨)");
    }

    // 모바일에서 홈으로 나가는 등 일시정지 시점을 종료 시각으로 본다. (기획서 6.1)
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) _service?.MarkSeen();
    }

    private void OnApplicationQuit()
    {
        _service?.MarkSeen();
    }

    private void ShowPopup(OfflineRewardResult result)
    {
        if (rewardText != null) rewardText.text = FormatRewardMessage(result);
        if (popupPanel != null) popupPanel.SetActive(true);
    }

    private void HidePopup()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    /// <summary>
    /// 실제로 지급할 보상이 있는가. null / rejected(첫 실행·시간 되돌리기) /
    /// 경과가 너무 짧아 경험치·골드가 모두 0 인 경우는 "보상 없음"으로 본다.
    /// </summary>
    public static bool HasReward(OfflineRewardResult result)
    {
        return result != null && !result.rejected
            && (result.gainedExp > 0 || result.gainedGold > 0);
    }

    /// <summary>
    /// 보상 결과를 팝업에 표시할 한글 문자열로 변환한다. (순수 함수 — EditMode 테스트 대상)
    /// 지급할 보상이 없으면 안내 문구, 있으면 경과 시간과 획득량을 보이고
    /// 캡이 걸렸으면 최대 인정 시간에 도달했음을 덧붙인다. (기획서 6.2 / 6.3)
    /// </summary>
    public static string FormatRewardMessage(OfflineRewardResult result)
    {
        if (!HasReward(result))
        {
            return "쌓인 오프라인 보상이 없습니다.";
        }

        TimeSpan span = TimeSpan.FromSeconds(result.elapsedSeconds);
        string elapsed = $"{(int)span.TotalHours}시간 {span.Minutes}분 {span.Seconds}초";

        string body =
            $"오프라인 보상 (경과 {elapsed})\n" +
            $"획득 경험치  {result.gainedExp}\n" +
            $"획득 골드  {result.gainedGold}";

        if (result.capped)
        {
            body += "\n(최대 인정 시간에 도달해 일부만 지급)";
        }

        return body;
    }
}
