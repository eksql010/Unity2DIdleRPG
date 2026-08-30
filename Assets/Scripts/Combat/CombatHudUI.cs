using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자동전투 상태/획득 보상을 보여주는 간단한 HUD + 자동전투 on/off 토글.
/// (수동 조작 테스트를 위해 자동전투를 끌 수 있게 한다.)
/// </summary>
public class CombatHudUI : MonoBehaviour
{
    [SerializeField] private AutoBattleController autoBattle;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private Text infoText;
    [SerializeField] private Button autoToggleButton;
    [SerializeField] private Text autoToggleLabel;

    private void Awake()
    {
        if (autoBattle == null) autoBattle = FindFirstObjectByType<AutoBattleController>();
        if (wallet == null) wallet = FindFirstObjectByType<PlayerWallet>();
    }

    private void Start()
    {
        if (autoToggleButton != null)
        {
            autoToggleButton.onClick.AddListener(OnToggleAuto);
        }
        if (wallet != null)
        {
            wallet.Changed += Refresh;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (wallet != null)
        {
            wallet.Changed -= Refresh;
        }
    }

    private void OnToggleAuto()
    {
        autoBattle.SetAutoBattle(!autoBattle.AutoBattleEnabled);
        Refresh();
    }

    private void Update()
    {
        // 상태(state)는 매 프레임 바뀔 수 있어 가볍게 갱신
        if (infoText != null && autoBattle != null)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (infoText != null)
        {
            string state = autoBattle != null ? autoBattle.State.ToString() : "-";
            int kills = wallet != null ? wallet.Kills : 0;
            int exp = wallet != null ? wallet.Exp : 0;
            int gold = wallet != null ? wallet.Gold : 0;
            infoText.text =
                "자동전투: " + (autoBattle != null && autoBattle.AutoBattleEnabled ? "ON" : "OFF") + "\n" +
                "상태: " + state + "\n" +
                "처치: " + kills + "   경험치: " + exp + "   골드: " + gold;
        }

        if (autoToggleLabel != null && autoBattle != null)
        {
            autoToggleLabel.text = autoBattle.AutoBattleEnabled ? "자동전투 끄기" : "자동전투 켜기";
        }
    }
}
