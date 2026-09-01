using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 몬스터를 스폰 지점(슬롯) 단위로 관리하는 스포너 (기획서 4.2 / 4.4).
/// - 스폰 지점 하나당 항상 몬스터 1마리를 유지한다.
/// - 처치되면 <b>그 스폰 지점에서</b> <see cref="respawnDelay"/> 초 뒤에 다시 스폰한다.
/// - 리스폰 대기 중에는 해당 지점을 비워 둔다(중첩 스폰 없음).
/// FSM 은 매 프레임 FindObjectsOfType 하지 않고 이 스포너에 최근접 대상을 질의한다.
/// 몬스터 인스턴스는 오브젝트 풀로 재사용된다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private Monster monsterPrefab;
    [Tooltip("몬스터가 스폰될 지점들. 각 지점당 항상 1마리를 유지한다(공중 플랫폼 포함).")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("처치 후 같은 지점에서 다시 스폰되기까지의 대기 시간(초).")]
    [Min(0f)]
    [SerializeField] private float respawnDelay = 5f;
    [Tooltip("죽은 몬스터가 풀로 반환되기까지의 짧은 연출 시간(초).")]
    [Min(0f)]
    [SerializeField] private float corpseLingerTime = 0.6f;

    private ComponentPool<Monster> _pool;

    // 슬롯 = 스폰 지점. 인덱스는 spawnPoints 와 동일.
    private Monster[] _slotMonster;
    private float[] _slotRespawnAt;   // 리스폰 예정 시각(Time.time). 음수면 대기 아님.

    private readonly List<Monster> _alive = new List<Monster>();
    private readonly List<Vector2> _positionBuffer = new List<Vector2>();

    /// <summary>현재 살아있는 몬스터들.</summary>
    public IReadOnlyList<Monster> AliveMonsters => _alive;

    /// <summary>스폰 지점 개수(= 유지 목표 몬스터 수).</summary>
    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

    /// <summary>리스폰 대기 시간(초).</summary>
    public float RespawnDelay => respawnDelay;

    private void Start()
    {
        EnsureInitialized();
        for (int i = 0; i < _slotMonster.Length; i++)
        {
            SpawnAtSlot(i);
        }
    }

    private void EnsureInitialized()
    {
        if (_slotMonster == null)
        {
            int count = SpawnPointCount;
            _slotMonster = new Monster[count];
            _slotRespawnAt = new float[count];
            for (int i = 0; i < count; i++)
            {
                _slotRespawnAt[i] = -1f;
            }
        }

        if (_pool == null && monsterPrefab != null)
        {
            _pool = new ComponentPool<Monster>(monsterPrefab, transform, Mathf.Max(1, SpawnPointCount));
        }
    }

    private void Update()
    {
        if (_slotMonster == null)
        {
            return;
        }

        for (int i = 0; i < _slotMonster.Length; i++)
        {
            if (_slotMonster[i] == null && _slotRespawnAt[i] >= 0f && Time.time >= _slotRespawnAt[i])
            {
                _slotRespawnAt[i] = -1f;
                SpawnAtSlot(i);
            }
        }
    }

    private void SpawnAtSlot(int slot)
    {
        EnsureInitialized();
        if (_pool == null || monsterPrefab == null ||
            spawnPoints == null || slot < 0 || slot >= spawnPoints.Length || spawnPoints[slot] == null)
        {
            return;
        }

        Monster monster = _pool.Get();
        monster.Spawn(spawnPoints[slot].position);
        monster.Died -= OnMonsterDied;
        monster.Died += OnMonsterDied;

        _slotMonster[slot] = monster;
        if (!_alive.Contains(monster))
        {
            _alive.Add(monster);
        }
    }

    private void OnMonsterDied(Monster monster)
    {
        monster.Died -= OnMonsterDied;
        _alive.Remove(monster);

        int slot = System.Array.IndexOf(_slotMonster, monster);
        if (slot >= 0)
        {
            _slotMonster[slot] = null;                         // 지점을 즉시 비운다
            _slotRespawnAt[slot] = Time.time + Mathf.Max(0f, respawnDelay);
        }

        StartCoroutine(ReleaseCorpse(monster));
    }

    private IEnumerator ReleaseCorpse(Monster monster)
    {
        yield return new WaitForSeconds(corpseLingerTime);
        _pool.Release(monster);
    }

    /// <summary>
    /// <paramref name="from"/> 에서 가장 가까운 살아있는 몬스터를 반환한다. 없으면 null.
    /// </summary>
    public Monster GetNearestAlive(Vector2 from)
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null || !_alive[i].IsAlive)
            {
                _alive.RemoveAt(i);
            }
        }

        _positionBuffer.Clear();
        for (int i = 0; i < _alive.Count; i++)
        {
            _positionBuffer.Add(_alive[i].transform.position);
        }

        int index = TargetSelector.GetNearestIndex(from, _positionBuffer);
        return index >= 0 ? _alive[index] : null;
    }
}
