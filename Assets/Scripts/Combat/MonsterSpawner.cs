using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 몬스터를 관리하는 스포너. (기획서 4.2 / 4.4)
///
/// - 살아있는 몬스터를 <see cref="AliveMonsters"/> 리스트로 직접 들고 있어, 자동전투 FSM 이
///   매 프레임 FindObjectsOfType 으로 씬을 뒤지지 않아도 된다.
/// - 몬스터 인스턴스는 <see cref="MonsterPool"/> 로 재사용한다(Instantiate/Destroy 반복 금지).
/// - 몬스터가 죽으면 리스트에서 빼고 풀로 반납한 뒤, 일정 지연 후 최대 마리수까지 다시 채운다.
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
    [Tooltip("동시에 살아있을 수 있는 최대 몬스터 수.")]
    [SerializeField] private int maxAlive = 4;

    [Tooltip("몬스터가 죽은 뒤 다음 몬스터가 나오기까지의 지연(초).")]
    [SerializeField] private float respawnDelay = 3f;

    [Tooltip("풀에 미리 만들어 둘 인스턴스 수. 보통 maxAlive 와 같게 둔다.")]
    [SerializeField] private int prewarm = 4;

    [Tooltip("게임 시작 시 곧바로 최대 마리수까지 채울지 여부.")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("스폰 위치")]
    [Tooltip("몬스터를 배치할 지점들. 순서대로 돌아가며 사용한다. 비우면 스포너 위치 주변에 흩뿌린다.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("스폰 지점이 없을 때 스포너 위치 기준 좌우로 흩뿌릴 범위(±X).")]
    [SerializeField] private float scatterX = 6f;

    private readonly List<Monster> _alive = new List<Monster>();
    private MonsterPool _pool;
    private MonsterData _data;
    private float _respawnTimer;
    private int _spawnIndex;
    private bool _initialized;

    // 코드로 만드는 플레이스홀더 몬스터가 공유하는 1×1 흰색 스프라이트.
    private static Sprite _placeholderSprite;

    /// <summary>현재 살아있는 몬스터들(읽기 전용). 자동전투 FSM 의 타겟 후보.</summary>
    public IReadOnlyList<Monster> AliveMonsters => _alive;

    /// <summary>현재 살아있는 몬스터 수.</summary>
    public int AliveCount => _alive.Count;

    /// <summary>동시에 살아있을 수 있는 최대 수.</summary>
    public int MaxAlive => maxAlive;

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
        _respawnTimer = 0f;
        _initialized = true;
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
        EnsureInitialized();
    }

    /// <summary>최대 마리수까지 즉시 채운다.</summary>
    public void FillToCapacity()
    {
        EnsureInitialized();
        int guard = 0;
        while (_alive.Count < maxAlive && guard++ < 1000)
        {
            if (SpawnOne() == null)
            {
                break;
            }
        }
    }

    /// <summary>몬스터 1마리를 스폰한다. 이미 최대치면 null 을 반환한다.</summary>
    public Monster SpawnOne()
    {
        EnsureInitialized();
        if (_alive.Count >= maxAlive)
        {
            return null;
        }

        Monster m = _pool.Rent();
        m.Died += HandleMonsterDied;
        m.Spawn(_data, NextSpawnPosition());
        _alive.Add(m);
        return m;
    }

    /// <summary>
    /// 시작 지점에서 가장 가까운 살아있는 몬스터를 돌려준다. 없으면 null.
    /// 리스트가 maxAlive 로 유한하므로 매 프레임 호출해도 부담 없다. (기획서 4.2 / 4.3)
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
    /// 리스폰 타이머를 <paramref name="deltaTime"/> 만큼 흘리고, 때가 되면 1마리 보충한다.
    /// 매 프레임 Update 가 호출하며, 테스트에서 직접 시간 경과를 주입할 수도 있다.
    /// </summary>
    public void TickRespawn(float deltaTime)
    {
        if (_alive.Count >= maxAlive)
        {
            return;
        }
        _respawnTimer -= deltaTime;
        if (_respawnTimer <= 0f)
        {
            SpawnOne();
            _respawnTimer = respawnDelay;
        }
    }

    private void HandleMonsterDied(Monster monster)
    {
        _alive.Remove(monster);
        _pool.Return(monster); // Despawn() 이 호출되며 Died 구독도 정리된다.
        // 방금 죽었으니 최소 respawnDelay 만큼은 비워 둔다.
        _respawnTimer = respawnDelay;
    }

    private Vector2 NextSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[_spawnIndex % spawnPoints.Length];
            _spawnIndex++;
            if (point != null)
            {
                return point.position;
            }
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
