using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스탯 파이프라인 테스트용 UI. 버프(수정자)를 켜고 끄면서
/// 최종 스탯값과 자동전투 데미지가 즉시 바뀌는 것을 확인한다.
/// </summary>
public class StatDebugUI : MonoBehaviour
{
    [SerializeField] private CharacterStats stats;
    [SerializeField] private Text statValueText;
    [SerializeField] private Button attackBuffButton;
    [SerializeField] private Text attackBuffLabel;
    [SerializeField] private Button moveSpeedBuffButton;
    [SerializeField] private Text moveSpeedBuffLabel;

    // 버프 주체 식별자(제거 시 이 주체가 준 수정자만 지운다)
    private readonly object _attackBuffSource = new object();
    private readonly object _moveSpeedBuffSource = new object();

    private bool _attackBuffOn;
    private bool _moveSpeedBuffOn;

    private void Awake()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<CharacterStats>();
        }
    }

    private void Start()
    {
        if (attackBuffButton != null)
        {
            attackBuffButton.onClick.AddListener(ToggleAttackBuff);
        }
        if (moveSpeedBuffButton != null)
        {
            moveSpeedBuffButton.onClick.AddListener(ToggleMoveSpeedBuff);
        }
        Refresh();
    }

    private void ToggleAttackBuff()
    {
        _attackBuffOn = !_attackBuffOn;
        if (_attackBuffOn)
        {
            // 공격력 +100% (PercentAdd)
            stats.AddModifier(StatType.AttackPower,
                new StatModifier(1.0f, StatModifierType.PercentAdd, _attackBuffSource));
        }
        else
        {
            stats.RemoveAllModifiersFromSource(_attackBuffSource);
        }
        Refresh();
    }

    private void ToggleMoveSpeedBuff()
    {
        _moveSpeedBuffOn = !_moveSpeedBuffOn;
        if (_moveSpeedBuffOn)
        {
            // 이동속도 +50% (PercentAdd) -> PlayerMovement 에 연동됨
            stats.AddModifier(StatType.MoveSpeed,
                new StatModifier(0.5f, StatModifierType.PercentAdd, _moveSpeedBuffSource));
        }
        else
        {
            stats.RemoveAllModifiersFromSource(_moveSpeedBuffSource);
        }
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (statValueText != null && stats != null)
        {
            statValueText.text =
                "[스탯 최종값]\n" +
                "공격력: " + stats.AttackPower.ToString("0.#") + "\n" +
                "방어력: " + stats.Defense.ToString("0.#") + "\n" +
                "이동속도: " + stats.MoveSpeed.ToString("0.##") + "\n" +
                "크리티컬: " + (stats.CritRate * 100f).ToString("0") + "%";
        }

        if (attackBuffLabel != null)
        {
            attackBuffLabel.text = _attackBuffOn ? "공격력 버프 해제" : "공격력 +100%";
        }
        if (moveSpeedBuffLabel != null)
        {
            moveSpeedBuffLabel.text = _moveSpeedBuffOn ? "이동속도 버프 해제" : "이동속도 +50%";
        }
    }
}
