using System;
using System.Collections.Generic;

/// <summary>
/// "기본값 + 여러 수정자"로 구성되는 스탯 하나. (기획서 5.1 레이어드 스탯 구조)
///
/// Dirty Flag 패턴: 기본값이나 수정자 목록이 실제로 바뀔 때만 최종값을 재계산한다.
/// <see cref="Value"/> 를 매 프레임 읽어도, 변경이 없으면 캐시된 값을 즉시 돌려준다.
/// (기획서 5.1 "매 프레임 재계산 금지")
///
/// MonoBehaviour 가 아닌 순수 클래스 → EditMode 테스트로 검증 가능.
/// </summary>
public class Stat
{
    private float _baseValue;
    private readonly List<StatModifier> _modifiers = new List<StatModifier>();

    private bool _isDirty = true;
    private float _cachedValue;

    /// <summary>
    /// 최종값이 실제로 다시 계산된 횟수. 테스트/디버그 전용 — Dirty Flag 가 동작하는지
    /// (읽기만 반복할 때 재계산이 안 일어나는지) 확인하는 데 쓴다.
    /// </summary>
    public int RecalculationCount { get; private set; }

    public Stat(float baseValue = 0f)
    {
        _baseValue = baseValue;
    }

    /// <summary>
    /// 기본값. 실제로 다른 값을 넣을 때만 Dirty 로 표시한다(같은 값 대입은 무시).
    /// </summary>
    public float BaseValue
    {
        get => _baseValue;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_baseValue == value)
            {
                return;
            }

            _baseValue = value;
            _isDirty = true;
        }
    }

    /// <summary>현재 붙어 있는 수정자들(읽기 전용).</summary>
    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    /// <summary>
    /// 최종 스탯 값. Dirty 상태일 때만 재계산하고, 아니면 캐시를 돌려준다.
    /// </summary>
    public float Value
    {
        get
        {
            if (_isDirty)
            {
                _cachedValue = CalculateFinalValue();
                RecalculationCount++;
                _isDirty = false;
            }

            return _cachedValue;
        }
    }

    /// <summary>수정자 추가. null 은 거부.</summary>
    public void AddModifier(StatModifier modifier)
    {
        if (modifier == null)
        {
            throw new ArgumentNullException(nameof(modifier));
        }

        _modifiers.Add(modifier);
        _isDirty = true;
    }

    /// <summary>특정 수정자 인스턴스를 제거. 제거되면 true.</summary>
    public bool RemoveModifier(StatModifier modifier)
    {
        if (modifier != null && _modifiers.Remove(modifier))
        {
            _isDirty = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 주어진 출처가 붙인 수정자를 전부 제거(장비 해제 등). 하나라도 제거되면 true.
    /// </summary>
    public bool RemoveAllModifiersFromSource(object source)
    {
        int removed = _modifiers.RemoveAll(m => Equals(m.Source, source));
        if (removed > 0)
        {
            _isDirty = true;
            return true;
        }

        return false;
    }

    /// <summary>모든 수정자 제거.</summary>
    public void ClearModifiers()
    {
        if (_modifiers.Count == 0)
        {
            return;
        }

        _modifiers.Clear();
        _isDirty = true;
    }

    /// <summary>
    /// 최종값 계산: 기본값 → Flat 합산 → PercentAdd 합산 후 한 번 곱 → PercentMultiply 순차 곱.
    /// (기획서 5.1 적용 순서)
    /// </summary>
    private float CalculateFinalValue()
    {
        float value = _baseValue;
        float percentAddSum = 0f;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier m = _modifiers[i];
            switch (m.Type)
            {
                case StatModifierType.Flat:
                    value += m.Value;
                    break;
                case StatModifierType.PercentAdd:
                    percentAddSum += m.Value;
                    break;
            }
        }

        value *= 1f + percentAddSum;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier m = _modifiers[i];
            if (m.Type == StatModifierType.PercentMultiply)
            {
                value *= 1f + m.Value;
            }
        }

        return value;
    }
}
