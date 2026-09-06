using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 몬스터를 관리하는 스포너. (기획서 4.2 / 4.4)
///
/// - 살아있는 몬스터를 <see cref="AliveMonsters"/> 리스트로 직접 들고 있어, 자동전투 FSM 이
///   매 프레임 FindObjectsOfType 으로 씬을 뒤지지 않아도 된다.
/// - 몬스터 인스턴스는 <see cref="MonsterPool"/> 로 재사용한다(Instantiate/Destroy 반복 금지).
/// - 스폰 지점(<see cref="spawnPoints"/>)마다 "슬롯"을 하나씩 두고, 슬롯당 최대 1마리만 유지한다.
///   몬스터가 죽으면 그 슬롯이 <see cref="respawnDelay"/> 만큼 비어 있다가, 지연이 끝나면
///   같은 자리에서 다시 스폰된다(중첩 스폰 없음). — INBOX 2026-09-07 05:26 항목 2
/// - 스폰 지점을 비워 두면 스포너 위치 주변에 <see cref="maxAlive"/> 마리를 흩뿌린다(구버전 호환).
///
/// 타겟 선정(가장 가까운 살아있는 몬스터)도 여기서 제공한다 — 3단계-2 의 Move 상태가 사용한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("몬스터 데이터")]
    [Tooltip("이 스포너가 뿌리는 몬스터 1종의 스탯/보상. 음수 등 오입력은 런타임에 보정된다.")]
    [SerializeField] private MonsterData monsterData = new MonsterData();

    [Tooltip("스폰할 몬스터 프리팹. 비워 두면 코드로 만든 플레이스홀더(빨간 사각형)를 쓴다.")]
    [SerializeField] private Monster monsterPrefab;

    [Header("스폰 규칙")]
    [Tooltip("스폰 지점이 없을 때 동시에 살아있을 최대 몬스터 수. 스폰 지점이 있으면 지점 수가 곧 최대 수다.")]
    [SerializeField] private int maxAlive = 4;

    [Tooltip("몬스터가 죽은 뒤 같은 자리에서 다시 나오기까지의 지연(초). Inspector 에서 조정.")]
    [SerializeField] private float respawnDelay = 5f;

    [Tooltip("풀에 미리 만들어 둘 인스턴스 수. 보통 스폰 지점 수와 같게 둔다.")]
    [SerializeField] private int prewarm = 8;

    [Tooltip("게임 시작 시 곧바로 모든 스폰 지점을 채울지 여부.")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("스폰 위치")]
    [Tooltip("몬스터를 배치할 지점들. 지점마다 슬롯 1개(= 최대 1마리). 비우면 스포너 위치 주변에 흩뿌린다.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("스폰 지점이 없을 때 스포너 위치 기준 좌우로 흩뿌릴 범위(±X).")]
    [SerializeField] private float scatterX = 6f;

    private readonly List<Monster> _alive = new List<Monster>();
    private readonly List<Slot> _slots = new List<Slot>();
    private MonsterPool _pool;
    private MonsterData _data;
    private bool _initialized;

    // 코드로 만드는 플레이스홀더 몬스터가 공유하는 1×1 흰색 스프라이트.
    private static Sprite _placeholderSprite;

    /// <summary>스폰 지점 하나에 대응하는 슬롯. 한 슬롯은 살아있는 몬스터 1마리 또는 리스폰 대기 상태다.</summary>
    private sealed class Slot
    {
        public Transform Point;        // 고정 스폰 지점(없으면 흩뿌리기 슬롯)
        public Monster Monster;        // null = 비어 있음
        public float Cooldown;         // > 0 = 리스폰 대기 중
    }

    /// <summary>현재 살아있는 몬스터들(읽기 전용). 자동전투 FSM 의 타겟 후보.</summary>
    public IReadOnlyList<Monster> AliveMonsters => _alive;

    /// <summary>현재 살아있는 몬스터 수.</summary>
    public int AliveCount => _alive.Count;

    /// <summary>슬롯(스폰 지점) 수 = 동시에 살아있을 수 있는 최대 수.</summary>
    public int MaxAlive => _slots.Count;

    /// <summary>리스폰 지연을 기다리는 중인(비어 있는) 슬롯 수.</summary>
    public int PendingRespawnCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Monster == null && _slots[i].Cooldown > 0f)
                {
                    n++;
                }
            }
            return n;
        }
    }

    /// <summary>지금까지 만들어진 풀 인스턴스 총수(재사용 여부 확인용).</summary>
    public int PoolSize => _pool != null ? _pool.TotalCreated : 0;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            FillToCapacity();
        }
    }

    private void Update()
    {
        TickRespawn(Time.deltaTime);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }
        _data = (monsterData ?? new MonsterData()).Sanitized();
        _pool = new MonsterPool(CreateMonster, Mathf.Max(0, prewarm));
        BuildSlots();
        _initialized = true;
    }

    /// <summary>스폰 지점(또는 흩뿌리기 슬롯)으로부터 슬롯 목록을 다시 만든다.</summary>
    private void BuildSlots()
    {
        _slots.Clear();
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform p = spawnPoints[i];
                if (p == null)
                {
                    continue;
                }
                _slots.Add(new Slot { Point = p });
            }
        }
        // 스폰 지점이 하나도 없으면 구버전처럼 maxAlive 마리를 흩뿌린다.
        if (_slots.Count == 0)
        {
            int n = Mathf.Max(0, maxAlive);
            for (int i = 0; i < n; i++)
            {
                _slots.Add(new Slot());
            }
        }
    }

    /// <summary>
    /// 테스트/런타임 튜닝용. Start 전에 호출하면 인스펙터 값을 덮어쓴다.
    /// </summary>
    public void Configure(MonsterData data, int newMaxAlive, float newRespawnDelay)
    {
        monsterData = data ?? monsterData;
        maxAlive = Mathf.Max(0, newMaxAlive);
        respawnDelay = Mathf.Max(0f, newRespawnDelay);
        _initialized = false;
        _alive.Clear();
        EnsureInitialized();
    }

    /// <summary>비어 있는(대기 중이 아닌) 모든 슬롯을 즉시 채운다.</summary>
    public void FillToCapacity()
    {
        EnsureInitialized();
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot.Monster == null && slot.Cooldown <= 0f)
            {
                SpawnInSlot(slot);
            }
        }
    }

    /// <summary>비어 있는 슬롯 하나에 몬스터를 스폰한다. 빈 슬롯이 없으면 null 을 반환한다.</summary>
    public Monster SpawnOne()
    {
        EnsureInitialized();
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot.Monster == null && slot.Cooldown <= 0f)
            {
                return SpawnInSlot(slot);
            }
        }
        return null;
    }

    private Monster SpawnInSlot(Slot slot)
    {
        Monster m = _pool.Rent();
        m.Died += HandleMonsterDied;
        m.Spawn(_data, ResolvePosition(slot));
        slot.Monster = m;
        slot.Cooldown = 0f;
        _alive.Add(m);
        return m;
    }

    /// <summary>
    /// 시작 지점에서 가장 가까운 살아있는 몬스터를 돌려준다. 없으면 null.
    /// 리스트가 슬롯 수로 유한하므로 매 프레임 호출해도 부담 없다. (기획서 4.2 / 4.3)
    /// </summary>
    public Monster GetNearestAliveMonster(Vector2 from)
    {
        Monster best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _alive.Count; i++)
        {
            Monster m = _alive[i];
            if (m == null || !m.IsAlive)
            {
                continue;
            }
            float sqr = ((Vector2)m.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = m;
            }
        }
        return best;
    }

    /// <summary>
    /// 비어 있는 슬롯들의 리스폰 타이머를 <paramref name="deltaTime"/> 만큼 흘리고,
    /// 지연이 끝난 슬롯을 같은 자리에서 다시 채운다.
    /// 매 프레임 Update 가 호출하며, 테스트에서 직접 시간 경과를 주입할 수도 있다.
    /// </summary>
    public void TickRespawn(float deltaTime)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot.Monster != null)
            {
                continue;
            }
            if (slot.Cooldown > 0f)
            {
                slot.Cooldown -= deltaTime;
                if (slot.Cooldown > 0f)
                {
                    continue;
                }
            }
            SpawnInSlot(slot);
        }
    }

    private void HandleMonsterDied(Monster monster)
    {
        _alive.Remove(monster);
        _pool.Return(monster); // Despawn() 이 호출되며 Died 구독도 정리된다.
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Monster == monster)
            {
                _slots[i].Monster = null;
                _slots[i].Cooldown = respawnDelay; // 이 슬롯은 지연 동안 비어 있는다(중첩 방지).
                break;
            }
        }
    }

    private Vector2 ResolvePosition(Slot slot)
    {
        if (slot.Point != null)
        {
            return slot.Point.position;
        }
        float offsetX = scatterX > 0f ? Random.Range(-scatterX, scatterX) : 0f;
        return (Vector2)transform.position + new Vector2(offsetX, 0f);
    }

    private Monster CreateMonster()
    {
        Monster monster;
        if (monsterPrefab != null)
        {
            monster = Instantiate(monsterPrefab, transform);
        }
        else
        {
            monster = BuildPlaceholder();
        }
        monster.gameObject.SetActive(false);
        return monster;
    }

    /// <summary>프리팹이 없을 때 쓰는 최소 몬스터 — 빨간 사각형 + BoxCollider2D.</summary>
    private Monster BuildPlaceholder()
    {
        var go = new GameObject("Monster(Placeholder)");
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSprite();
        sr.color = new Color(0.85f, 0.2f, 0.2f, 1f);
        sr.sortingOrder = 5;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = true;

        return go.AddComponent<Monster>();
    }

    private static Sprite PlaceholderSprite()
    {
        if (_placeholderSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            _placeholderSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                tex.width);
            _placeholderSprite.name = "MonsterPlaceholder";
        }
        return _placeholderSprite;
    }
}
