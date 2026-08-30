using System;
using UnityEngine;

/// <summary>
/// 자동전투의 대상이 되는 몬스터. 오브젝트 풀로 재사용된다.
/// MVP 범위에서는 제자리에 서 있는 표적이며 플레이어에게 반격하지 않는다.
/// </summary>
public class Monster : MonoBehaviour
{
    [SerializeField] private MonsterData data = new MonsterData();
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("체력 비율만큼 X 스케일이 줄어드는 HP 바(선택).")]
    [SerializeField] private Transform hpBarFill;

    private float _currentHp;
    private Vector3 _hpBarBaseScale = Vector3.one;

    /// <summary>이 몬스터가 죽었을 때 호출된다(자기 자신을 인자로).</summary>
    public event Action<Monster> Died;

    public bool IsAlive { get; private set; }
    public MonsterData Data => data;
    public float AttackPower => data.attackPower;
    public float Defense => data.defense;
    public int ExpReward => data.expReward;
    public int GoldReward => data.goldReward;
    public float HealthNormalized => data.maxHp > 0f ? Mathf.Clamp01(_currentHp / data.maxHp) : 0f;

    private void Awake()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (hpBarFill != null)
        {
            _hpBarBaseScale = hpBarFill.localScale;
        }
    }

    /// <summary>풀에서 꺼낸 뒤 지정 위치에 살아있는 상태로 초기화한다.</summary>
    public void Spawn(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        _currentHp = data.maxHp;
        IsAlive = true;
        UpdateVisual();
    }

    /// <summary>
    /// 데미지를 적용한다. HP 가 0 이하가 되면 죽고 <see cref="Died"/> 를 호출한다.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (!IsAlive)
        {
            return;
        }

        _currentHp -= Mathf.Max(0, amount);
        if (_currentHp <= 0f)
        {
            _currentHp = 0f;
            IsAlive = false;
            UpdateVisual();
            Died?.Invoke(this);
            return;
        }

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        float t = HealthNormalized;
        if (hpBarFill != null)
        {
            Vector3 s = _hpBarBaseScale;
            s.x = _hpBarBaseScale.x * t;
            hpBarFill.localScale = s;
        }
        if (bodyRenderer != null)
        {
            // 체력이 높으면 보라, 낮을수록 붉게
            bodyRenderer.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.6f, 0.35f, 0.72f), t);
        }
    }
}
