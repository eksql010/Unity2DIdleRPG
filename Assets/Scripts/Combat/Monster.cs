using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동전투의 대상이 되는 몬스터. 오브젝트 풀로 재사용된다.
/// MVP 범위에서는 제자리에 서 있는 표적이며 플레이어에게 반격하지 않는다.
/// 스탯(공격력/방어력/최대 HP)은 플레이어와 동일한 <see cref="Stat"/> 파이프라인을 재사용한다(기획서 5.4).
/// </summary>
public class Monster : MonoBehaviour
{
    [SerializeField] private MonsterData data = new MonsterData();
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("체력 비율만큼 좌측 고정으로 줄어드는 HP 바 채움(선택).")]
    [SerializeField] private Transform hpBarFill;

    private readonly Stat _attackStat = new Stat(0f);
    private readonly Stat _defenseStat = new Stat(0f);
    private readonly Stat _maxHpStat = new Stat(1f);

    private float _currentHp;

    // HP 바 채움의 기준값 (풀 재사용 간에도 유지)
    private bool _hpBarCached;
    private Vector3 _hpBarBaseScale = Vector3.one;
    private Vector3 _hpBarBaseLocalPos;
    private float _hpBarFillWidth = 1f;   // 스케일 1 기준 채움 스프라이트의 로컬 폭

    /// <summary>이 몬스터가 죽었을 때 호출된다(자기 자신을 인자로).</summary>
    public event Action<Monster> Died;

    public bool IsAlive { get; private set; }
    public MonsterData Data => data;
    public float AttackPower => _attackStat.Value;
    public float Defense => _defenseStat.Value;
    public float MaxHp => _maxHpStat.Value;
    public int ExpReward => data.expReward;
    public int GoldReward => data.goldReward;
    public float HealthNormalized => MaxHp > 0f ? Mathf.Clamp01(_currentHp / MaxHp) : 0f;

    private void Awake()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    /// <summary>풀에서 꺼낸 뒤 지정 위치에 살아있는 상태로 초기화한다.</summary>
    public void Spawn(Vector3 worldPosition)
    {
        Spawn(worldPosition, null);
    }

    /// <summary>
    /// 스폰하면서 추가 수정자(엘리트 버프 등)를 적용한다.
    /// 딕셔너리 키는 대상 스탯, 값은 그 스탯에 붙일 수정자들.
    /// </summary>
    public void Spawn(Vector3 worldPosition, IReadOnlyDictionary<StatType, IEnumerable<StatModifier>> spawnModifiers)
    {
        transform.position = worldPosition;

        _attackStat.ClearModifiers();
        _defenseStat.ClearModifiers();
        _maxHpStat.ClearModifiers();
        _attackStat.BaseValue = data.attackPower;
        _defenseStat.BaseValue = data.defense;
        _maxHpStat.BaseValue = data.maxHp;

        if (spawnModifiers != null)
        {
            ApplySpawnModifiers(StatType.AttackPower, _attackStat, spawnModifiers);
            ApplySpawnModifiers(StatType.Defense, _defenseStat, spawnModifiers);
            ApplySpawnModifiers(StatType.MaxHP, _maxHpStat, spawnModifiers);
        }

        _currentHp = MaxHp;
        IsAlive = true;
        UpdateVisual();
    }

    private static void ApplySpawnModifiers(
        StatType type, Stat stat, IReadOnlyDictionary<StatType, IEnumerable<StatModifier>> source)
    {
        if (!source.TryGetValue(type, out IEnumerable<StatModifier> mods) || mods == null)
        {
            return;
        }
        foreach (StatModifier mod in mods)
        {
            stat.AddModifier(mod);
        }
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

    private void CacheHpBarBase()
    {
        if (_hpBarCached || hpBarFill == null)
        {
            return;
        }
        _hpBarCached = true;
        _hpBarBaseScale = hpBarFill.localScale;
        _hpBarBaseLocalPos = hpBarFill.localPosition;

        SpriteRenderer fillRenderer = hpBarFill.GetComponent<SpriteRenderer>();
        if (fillRenderer != null && fillRenderer.sprite != null)
        {
            _hpBarFillWidth = fillRenderer.sprite.bounds.size.x;
        }
    }

    private void UpdateVisual()
    {
        float t = HealthNormalized;

        if (hpBarFill != null)
        {
            CacheHpBarBase();

            // 좌측 끝은 고정하고 우측만 안쪽으로 줄어들게 한다.
            // 스프라이트 피벗이 중앙이므로, 스케일을 줄인 만큼 오른쪽으로 위치를 보정해
            // 좌측 모서리 X 를 항상 만체력 기준값에 붙여 둔다.
            float fullHalfWidth = _hpBarBaseScale.x * _hpBarFillWidth * 0.5f;
            float leftEdgeX = _hpBarBaseLocalPos.x - fullHalfWidth;

            Vector3 scale = _hpBarBaseScale;
            scale.x = _hpBarBaseScale.x * t;
            hpBarFill.localScale = scale;

            Vector3 pos = _hpBarBaseLocalPos;
            pos.x = leftEdgeX + scale.x * _hpBarFillWidth * 0.5f;
            hpBarFill.localPosition = pos;
        }

        if (bodyRenderer != null)
        {
            // 체력이 높으면 보라, 낮을수록 붉게
            bodyRenderer.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.6f, 0.35f, 0.72f), t);
        }
    }
}
