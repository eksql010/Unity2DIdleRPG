using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프라인 보상 테스트용 화면 UI.
/// 기획서 6.3 요구사항: Login/Logout 토글 버튼 + 재접속 시 "획득한 보상" 팝업(텍스트 표시).
/// 실제 게임 UI 라기보다 방치 보상 로직을 손으로 확인하기 위한 디버그 패널이다.
/// </summary>
public class OfflineRewardUI : MonoBehaviour
{
    [SerializeField] private OfflineRewardManager manager;

    [Header("상단 상태")]
    [SerializeField] private Text statusText;

    [Header("조작 버튼")]
    [SerializeField] private Button loginToggleButton;
    [SerializeField] private Text loginToggleLabel;
    [SerializeField] private Button addOneHourButton;
    [SerializeField] private Button addNineHoursButton;
    [SerializeField] private Button resetButton;

    [Header("보상 팝업")]
    [SerializeField] private GameObject rewardPopup;
    [SerializeField] private Text rewardText;
    [SerializeField] private Button popupCloseButton;

    private void Awake()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<OfflineRewardManager>();
        }
    }

    private void Start()
    {
        loginToggleButton.onClick.AddListener(OnToggleLogin);
        addOneHourButton.onClick.AddListener(delegate { OnAddOfflineTime(1.0); });
        addNineHoursButton.onClick.AddListener(delegate { OnAddOfflineTime(9.0); });
        resetButton.onClick.AddListener(OnReset);
        popupCloseButton.onClick.AddListener(delegate { rewardPopup.SetActive(false); });

        rewardPopup.SetActive(false);
        RefreshUI();
    }

    private void OnToggleLogin()
    {
        if (manager.IsLoggedIn)
        {
            manager.Logout();
        }
        else
        {
            OfflineRewardResult result = manager.Login();
            if (result != null && result.elapsedSeconds >= 1.0)
            {
                ShowPopup(result);
            }
        }
        RefreshUI();
    }

    private void OnAddOfflineTime(double hours)
    {
        manager.AddDebugOfflineTime(hours);
        RefreshUI();
    }

    private void OnReset()
    {
        manager.ResetAll();
        rewardPopup.SetActive(false);
        RefreshUI();
    }

    private void ShowPopup(OfflineRewardResult result)
    {
        TimeSpan span = TimeSpan.FromSeconds(result.elapsedSeconds);
        string cappedNote = result.wasCapped
            ? string.Format("  (최대 {0:0.#}시간 캡 적용)", manager.Config.maxOfflineHours)
            : string.Empty;

        rewardText.text =
            "경과 시간: " + FormatSpan(span) + cappedNote + "\n" +
            "획득 경험치: +" + result.gainedExp + "\n" +
            "획득 골드: +" + result.gainedGold;

        rewardPopup.SetActive(true);
    }

    private void RefreshUI()
    {
        bool loggedIn = manager.IsLoggedIn;

        loginToggleLabel.text = loggedIn ? "로그아웃" : "로그인";
        statusText.text =
            "상태: " + (loggedIn ? "로그인 중" : "로그아웃") + "\n" +
            "누적 경험치: " + manager.TotalExp + "\n" +
            "누적 골드: " + manager.TotalGold;

        // 오프라인 시간 추가는 로그아웃 상태에서만 의미가 있다
        addOneHourButton.interactable = !loggedIn;
        addNineHoursButton.interactable = !loggedIn;
    }

    private static string FormatSpan(TimeSpan span)
    {
        if (span.TotalHours >= 1.0)
        {
            return string.Format("{0}시간 {1}분 {2}초", (int)span.TotalHours, span.Minutes, span.Seconds);
        }
        if (span.TotalMinutes >= 1.0)
        {
            return string.Format("{0}분 {1}초", span.Minutes, span.Seconds);
        }
        return string.Format("{0}초", span.Seconds);
    }
}
