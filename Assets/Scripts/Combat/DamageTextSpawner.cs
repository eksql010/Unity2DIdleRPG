using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동전투 FSM 의 타격을 화면 위 "떠오르는 데미지 숫자"로 잇는 계층. (4단계-3b, 기획서 4.4)
///
/// <see cref="AutoBattleFsm.MonsterDamaged"/> 이벤트를 구독해, 맞은 몬스터 머리 위에
/// <see cref="DamageText"/> 하나를 <see cref="DamageTextPool"/> 에서 꺼내 띄운다.
/// 숫자는 위로 떠오르며 페이드아웃한 뒤 풀로 반납된다(Instantiate/Destroy 반복 없음).
/// FSM 은 데미지 텍스트를 모르게 두어(의존성 분리) 4단계-3a 테스트를 그대로 통과한다.
/// <see cref="LootCollector"/> 가 <see cref="AutoBattleFsm.MonsterKilled"/> 를 잇는 것과 같은 구조.
/// </summary>
[DisallowMultipleComponent]
public class DamageTextSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("타격 이벤트를 내보내는 자동전투 FSM. 보통 같은 Player 오브젝트.")]
    [SerializeField] private AutoBattleFsm fsm;

    [Tooltip("숫자를 그릴 폰트. 비우면 빌트인 LegacyRuntime.ttf 를 쓴다.")]
    [SerializeField] private Font font;

    [Header("튜닝")]
    [Tooltip("맞은 몬스터 위치에서 숫자를 얼마나 위에 띄울지(월드 단위).")]
    [SerializeField] private float verticalOffset = 0.9f;

    [Tooltip("풀에 미리 만들어 둘 데미지 텍스트 수.")]
    [SerializeField] private int prewarm = 8;

    private DamageTextPool _pool;
    private readonly List<DamageText> _active = new List<DamageText>();
    private bool _subscribed;

    /// <summary>지금 화면에 떠 있는 데미지 숫자 수.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>지금까지 만들어진 풀 인스턴스 총수(재사용 여부 확인용).</summary>
    public int PoolSize => _pool != null ? _pool.TotalCreated : 0;

    /// <summary>지금까지 띄운 데미지 숫자 누적 수(디버그용).</summary>
    public int TotalShown { get; private set; }

    private void Awake()
    {
        EnsurePool();
    }

    private void EnsurePool()
    {
        if (_pool == null)
        {
            _pool = new DamageTextPool(CreateDamageText, Mathf.Max(0, prewarm));
        }
    }

    /// <summary>테스트/런타임에서 의존성을 주입한다. 인스펙터 참조 대신 사용할 수 있다.</summary>
    public void Configure(AutoBattleFsm autoBattleFsm, Font textFont)
    {
        Unsubscribe();
        fsm = autoBattleFsm;
        if (textFont != null)
        {
            font = textFont;
        }
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
        Tick(Time.deltaTime);
    }

    /// <summary>떠 있는 숫자들을 한 스텝 진행하고, 수명이 끝난 것은 풀로 반납한다.</summary>
    public void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            DamageText t = _active[i];
            if (t == null || !t.Tick(deltaTime))
            {
                _active.RemoveAt(i);
                _pool.Return(t);
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
        if (monster == null || result == null)
        {
            return;
        }

        EnsurePool();
        DamageText text = _pool.Rent();
        Vector3 pos = monster.transform.position + Vector3.up * verticalOffset;
        text.Play(pos, result.Damage, result.IsCrit);
        _active.Add(text);
        TotalShown++;
    }

    private DamageText CreateDamageText()
    {
        var go = new GameObject("DamageText");
        go.transform.SetParent(transform, false);

        var text = go.AddComponent<DamageText>();
        text.SetFont(font != null ? font : BuiltinFont());
        go.SetActive(false);
        return text;
    }

    private static Font _builtinFont;

    private static Font BuiltinFont()
    {
        if (_builtinFont == null)
        {
            _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return _builtinFont;
    }
}
