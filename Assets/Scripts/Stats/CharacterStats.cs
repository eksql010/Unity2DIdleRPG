using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터(플레이어/몬스터 공용)의 스탯 묶음 (기획서 5.1 / 5.2 / 5.4).
/// 기본값은 Inspector 에서 설정하고, 런타임에는 <see cref="AddModifier"/> 로 수정자를 붙인다.
/// MoveSpeed 는 <see cref="PlayerMovement"/> 와 연동된다(기획서 5.2).
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("기본값 (기획서 7. StatContainer)")]
    [SerializeField] private float baseAttackPower = 16f;
    [SerializeField] private float baseDefense = 5f;
    [SerializeField] private float baseMaxHP = 100f;
    [Range(0f, 1f)]
    [SerializeField] private float baseCritRate = 0.25f;
    [SerializeField] private float baseMoveSpeed = 5f;

    [Tooltip("MoveSpeed 스탯을 이 PlayerMovement 에 반영한다(선택).")]
    [SerializeField] private PlayerMovement movement;

    private readonly Dictionary<StatType, Stat> _stats = new Dictionary<StatType, Stat>();
    private bool _initialized;

    /// <summary>아무 스탯이라도 최종값이 바뀌었을 수 있을 때 호출된다.</summary>
    public event Action Changed;

    public float AttackPower => GetValue(StatType.AttackPower);
    public float Defense => GetValue(StatType.Defense);
    public float MaxHP => GetValue(StatType.MaxHP);
    public float CritRate => Mathf.Clamp01(GetValue(StatType.CritRate));
    public float MoveSpeed => GetValue(StatType.MoveSpeed);

    private void Start()
    {
        // 첫 접근 시 스탯을 구성하고 초기 MoveSpeed 를 PlayerMovement 에 반영한다.
        // (Awake 가 아니라 여기서 구성하므로, 테스트가 AddComponent 후 기본값 필드를 주입할 수 있다.)
        EnsureInitialized();
        ApplyMoveSpeed();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        _stats[StatType.AttackPower] = new Stat(baseAttackPower);
        _stats[StatType.Defense] = new Stat(baseDefense);
        _stats[StatType.MaxHP] = new Stat(baseMaxHP);
        _stats[StatType.CritRate] = new Stat(baseCritRate);
        _stats[StatType.MoveSpeed] = new Stat(baseMoveSpeed);

        foreach (KeyValuePair<StatType, Stat> pair in _stats)
        {
            StatType type = pair.Key;
            pair.Value.Changed += () => OnStatChanged(type);
        }
    }

    /// <summary>해당 스탯 객체를 반환한다(수정자 직접 조작용).</summary>
    public Stat GetStat(StatType type)
    {
        EnsureInitialized();
        return _stats[type];
    }

    /// <summary>해당 스탯의 최종값(Dirty 일 때만 재계산됨).</summary>
    public float GetValue(StatType type)
    {
        EnsureInitialized();
        return _stats[type].Value;
    }

    public void AddModifier(StatType type, StatModifier modifier)
    {
        GetStat(type).AddModifier(modifier);
    }

    public bool RemoveModifier(StatType type, StatModifier modifier)
    {
        return GetStat(type).RemoveModifier(modifier);
    }

    /// <summary>특정 주체(버프/장비 등)가 부여한 모든 스탯 수정자를 제거한다.</summary>
    public void RemoveAllModifiersFromSource(object source)
    {
        EnsureInitialized();
        foreach (Stat stat in _stats.Values)
        {
            stat.RemoveAllFromSource(source);
        }
    }

    private void OnStatChanged(StatType type)
    {
        if (type == StatType.MoveSpeed)
        {
            ApplyMoveSpeed();
        }
        Changed?.Invoke();
    }

    private void ApplyMoveSpeed()
    {
        if (movement != null)
        {
            movement.SetMoveSpeed(GetValue(StatType.MoveSpeed));
        }
    }
}
