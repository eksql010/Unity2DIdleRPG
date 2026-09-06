using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동전투 FSM 의 타격을 "몬스터 머리 위 체력바"로 잇는 계층. (INBOX 2026-09-07 05:26 항목 4)
///
/// <see cref="AutoBattleFsm.MonsterDamaged"/> 이벤트를 구독해, 처음 맞은 몬스터에
/// <see cref="MonsterHealthBar"/> 를 <see cref="MonsterHealthBarPool"/> 에서 꺼내 붙인다.
/// 체력바는 매 프레임 그 몬스터를 따라다니며 남은 체력 비율을 갱신하고, 몬스터가 죽거나
/// 사라지면 풀로 반납된다(Instantiate/Destroy 반복 없음 — 기획서 4.4).
///
/// FSM 은 체력바를 모르게 두어(의존성 분리) 기존 테스트를 그대로 통과시킨다.
/// <see cref="DamageTextSpawner"/> / <see cref="LootCollector"/> 와 완전히 같은 구조.
/// </summary>
[DisallowMultipleComponent]
public class MonsterHealthBarSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("타격 이벤트를 내보내는 자동전투 FSM. 보통 같은 Player 오브젝트.")]
    [SerializeField] private AutoBattleFsm fsm;

    [Header("튜닝")]
    [Tooltip("풀에 미리 만들어 둘 체력바 수. 보통 동시에 살아있는 몬스터 수와 같게 둔다.")]
    [SerializeField] private int prewarm = 8;

    private MonsterHealthBarPool _pool;
    private readonly List<MonsterHealthBar> _active = new List<MonsterHealthBar>();
    private bool _subscribed;

    /// <summary>지금 화면에 떠 있는 체력바 수.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>지금까지 만들어진 풀 인스턴스 총수(재사용 여부 확인용).</summary>
    public int PoolSize => _pool != null ? _pool.TotalCreated : 0;

    private void Awake()
    {
        EnsurePool();
    }

    private void EnsurePool()
    {
        if (_pool == null)
        {
            _pool = new MonsterHealthBarPool(CreateBar, Mathf.Max(0, prewarm));
        }
    }

    /// <summary>테스트/런타임에서 의존성을 주입한다. 인스펙터 참조 대신 사용할 수 있다.</summary>
    public void Configure(AutoBattleFsm autoBattleFsm)
    {
        Unsubscribe();
        fsm = autoBattleFsm;
        EnsurePool();
        if (isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    private void OnEnable()
    {
        EnsurePool();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        Tick();
    }

    /// <summary>떠 있는 체력바들을 한 스텝 진행하고, 대상이 사라진 것은 풀로 반납한다.</summary>
    public void Tick()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            MonsterHealthBar bar = _active[i];
            if (bar == null || !bar.Tick())
            {
                _active.RemoveAt(i);
                _pool.Return(bar);
            }
        }
    }

    private void Subscribe()
    {
        if (_subscribed || fsm == null)
        {
            return;
        }
        fsm.MonsterDamaged += OnMonsterDamaged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (fsm != null)
        {
            fsm.MonsterDamaged -= OnMonsterDamaged;
        }
        _subscribed = false;
    }

    private void OnMonsterDamaged(Monster monster, DamageResult result)
    {
        if (monster == null || !monster.IsAlive)
        {
            return;
        }

        // 이미 이 몬스터에 체력바가 붙어 있으면 새로 만들지 않는다(풀 재사용된 인스턴스 포함).
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i] != null && _active[i].Monster == monster)
            {
                return;
            }
        }

        EnsurePool();
        MonsterHealthBar bar = _pool.Rent();
        bar.Bind(monster);
        _active.Add(bar);
    }

    private MonsterHealthBar CreateBar()
    {
        // 부모를 두지 않는다 — Player 처럼 비균일 스케일인 오브젝트에 붙이면 게이지가 늘어난다.
        var go = new GameObject("MonsterHealthBar");
        var bar = go.AddComponent<MonsterHealthBar>();
        go.SetActive(false);
        return bar;
    }
}
