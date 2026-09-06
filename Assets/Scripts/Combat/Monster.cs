using System;
using UnityEngine;

/// <summary>
/// 씬 위의 몬스터 오브젝트. MVP 3단계-1 범위에서는 "체력만" 가진다.
/// (공격/이동 AI 는 이후 슬라이스, 데미지 계산 파이프라인은 4단계)
///
/// Object Pooling 으로 재사용되므로 스스로 Destroy 하지 않는다. 죽으면 <see cref="Died"/> 를
/// 발생시키고, 실제 비활성화/반납은 스포너(<see cref="MonsterSpawner"/>)가 담당한다. (기획서 4.4)
/// </summary>
[DisallowMultipleComponent]
public class Monster : MonoBehaviour
{
    [Header("현재 상태(런타임 표시용)")]
    [Tooltip("현재 체력. Spawn 시 데이터의 hp 로 초기화된다.")]
    [SerializeField] private float currentHp;

    /// <summary>이 몬스터를 스폰할 때 사용된 데이터. Loot 단계에서 보상 계산에 쓰인다.</summary>
    public MonsterData Data { get; private set; }

    /// <summary>
    /// 이 몬스터의 레이어드 스탯(공격력/방어력/HP 등). <see cref="Spawn"/> 시 <see cref="Data"/> 로부터
    /// 구성된다. 데미지 계산 파이프라인이 방어력을 읽는다. (기획서 5.4 — 플레이어와 같은 시스템 재사용)
    /// </summary>
    public StatContainer Stats { get; private set; }

    /// <summary>현재 체력.</summary>
    public float CurrentHp => currentHp;

    /// <summary>최대 체력(= 스폰에 사용된 데이터의 hp).</summary>
    public float MaxHp { get; private set; }

    /// <summary>살아있는가. 체력이 0 이하가 되면 false.</summary>
    public bool IsAlive { get; private set; }

    /// <summary>죽는 순간 1회 발생. 인자는 죽은 몬스터 자신(스포너가 반납 처리).</summary>
    public event Action<Monster> Died;

    /// <summary>
    /// 풀에서 꺼내 재사용할 때 호출. 체력을 가득 채우고 살아있는 상태로 되돌린다.
    /// </summary>
    public void Spawn(MonsterData data, Vector2 position)
    {
        Data = (data ?? new MonsterData()).Sanitized();
        Stats = StatContainer.ForMonster(Data);
        MaxHp = Data.hp;
        currentHp = MaxHp;
        IsAlive = true;

        transform.position = position;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 피해를 입힌다. 3단계-1 에서는 방어력/크리티컬 보정 없이 들어온 값을 그대로 깎는다.
    /// (레이어드 스탯 기반 데미지 공식은 4단계에서 이 앞단에 붙는다 — 기획서 5.3)
    /// </summary>
    /// <returns>이 타격으로 몬스터가 죽었으면 true.</returns>
    public bool TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return false;
        }

        currentHp -= amount;
        if (currentHp > 0f)
        {
            return false;
        }

        currentHp = 0f;
        IsAlive = false;
        Died?.Invoke(this);
        return true;
    }

    /// <summary>스포너가 풀로 반납하기 직전에 호출. 씬에서 감춘다.</summary>
    public void Despawn()
    {
        IsAlive = false;
        Died = null;
        gameObject.SetActive(false);
    }
}
