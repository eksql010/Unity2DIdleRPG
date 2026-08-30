using System;
using System.Collections.Generic;

/// <summary>
/// 레이어드 스탯 (기획서 5.1): 기본값 + 여러 수정자로 최종값을 만든다.
/// Dirty Flag 패턴 — 수정자가 실제로 바뀔 때만 최종값을 다시 계산하고, 그 외에는 캐시를 돌려준다.
/// 유니티 의존이 없어 EditMode 로 테스트한다.
/// </summary>
public class Stat
{
    private float _baseValue;
    private bool _isDirty = true;
    private float _cachedValue;

    private readonly List<StatModifier> _modifiers = new List<StatModifier>();

    /// <summary>최종값이 바뀌었을 수 있을 때 호출된다(수정자 추가/제거/기본값 변경).</summary>
    public event Action Changed;

    public Stat(float baseValue)
    {
        _baseValue = baseValue;
    }

    /// <summary>기본값. 변경 시 Dirty 처리.</summary>
    public float BaseValue
    {
        get => _baseValue;
        set
        {
            if (Math.Abs(_baseValue - value) > float.Epsilon)
            {
                _baseValue = value;
                MarkDirty();
            }
        }
    }

    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    /// <summary>최종값. Dirty 일 때만 재계산한다.</summary>
    public float Value
    {
        get
        {
            if (_isDirty)
            {
                _cachedValue = CalculateFinalValue();
                _isDirty = false;
            }
            return _cachedValue;
        }
    }

    public void AddModifier(StatModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }
        _modifiers.Add(modifier);
        MarkDirty();
    }

    public bool RemoveModifier(StatModifier modifier)
    {
        if (_modifiers.Remove(modifier))
        {
            MarkDirty();
            return true;
        }
        return false;
    }

    /// <summary>특정 주체가 부여한 수정자를 모두 제거한다. 제거된 개수 반환.</summary>
    public int RemoveAllFromSource(object source)
    {
        int removed = 0;
        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_modifiers[i].Source, source))
            {
                _modifiers.RemoveAt(i);
                removed++;
            }
        }
        if (removed > 0)
        {
            MarkDirty();
        }
        return removed;
    }

    public void ClearModifiers()
    {
        if (_modifiers.Count > 0)
        {
            _modifiers.Clear();
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
        Changed?.Invoke();
    }

    private float CalculateFinalValue()
    {
        float finalValue = _baseValue;
        float percentAddSum = 0f;

        // Flat -> PercentAdd(합산 후 1회) -> PercentMultiply(순차 곱) 순서로 적용
        _modifiers.Sort(CompareModifierOrder);

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier mod = _modifiers[i];
            switch (mod.Type)
            {
                case StatModifierType.Flat:
                    finalValue += mod.Value;
                    break;

                case StatModifierType.PercentAdd:
                    percentAddSum += mod.Value;
                    // 다음 수정자가 PercentAdd 가 아니면 지금까지 합산분을 반영
                    if (i + 1 >= _modifiers.Count || _modifiers[i + 1].Type != StatModifierType.PercentAdd)
                    {
                        finalValue *= 1f + percentAddSum;
                        percentAddSum = 0f;
                    }
                    break;

                case StatModifierType.PercentMultiply:
                    finalValue *= 1f + mod.Value;
                    break;
            }
        }

        return (float)Math.Round(finalValue, 4);
    }

    private static int CompareModifierOrder(StatModifier a, StatModifier b)
    {
        if (a.Order != b.Order)
        {
            return a.Order < b.Order ? -1 : 1;
        }
        return 0;
    }
}
