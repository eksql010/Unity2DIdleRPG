using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerWallet"/> 의 보유 골드/경험치를 화면에 표시하는 HUD. (기획서 1 핵심 루프)
/// 지갑의 <see cref="PlayerWallet.Changed"/> 이벤트에만 반응해 갱신한다(매 프레임 폴링하지 않음).
/// 3단계-3 플레이모드 스크린샷에서 자동전투가 실제로 보상을 쌓는지 눈으로 확인하는 용도.
/// </summary>
[DisallowMultipleComponent]
public class WalletHud : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private Text label;

    private void OnEnable()
    {
        if (wallet != null)
        {
            wallet.Changed += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null)
        {
            wallet.Changed -= Refresh;
        }
    }

    /// <summary>테스트/런타임에서 의존성을 주입한다.</summary>
    public void Configure(PlayerWallet playerWallet, Text targetLabel)
    {
        if (wallet != null)
        {
            wallet.Changed -= Refresh;
        }
        wallet = playerWallet;
        label = targetLabel;
        if (isActiveAndEnabled && wallet != null)
        {
            wallet.Changed += Refresh;
        }
        Refresh();
    }

    /// <summary>현재 지갑 값을 라벨에 반영한다.</summary>
    public void Refresh()
    {
        if (label == null)
        {
            return;
        }
        long g = wallet != null ? wallet.Gold : 0;
        long e = wallet != null ? wallet.Exp : 0;
        label.text = $"골드 {g:N0}    경험치 {e:N0}";
    }
}
