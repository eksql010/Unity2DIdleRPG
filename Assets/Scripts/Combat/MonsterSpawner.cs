using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 몬스터를 리스트로 관리하는 스포너 (기획서 4.2).
/// FSM 은 매 프레임 FindObjectsOfType 하지 않고 이 스포너에 최근접 대상을 질의한다.
/// 몬스터는 오브젝트 풀로 재사용된다 (기획서 4.4).
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private Monster monsterPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("동시에 살아있는 최대 몬스터 수.")]
    [SerializeField] private int maxAlive = 5;
    [Tooltip("처치 후 재생성까지 지연(초).")]
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private int prewarm = 5;

    private ComponentPool<Monster> _pool;
    private readonly List<Monster> _alive = new List<Monster>();
    private readonly List<Vector2> _positionBuffer = new List<Vector2>();

    /// <summary>현재 살아있는 몬스터들.</summary>
    public IReadOnlyList<Monster> AliveMonsters => _alive;

    private void Start()
    {
        EnsurePool();
        for (int i = 0; i < maxAlive; i++)
        {
            SpawnOne();
        }
    }

    private void EnsurePool()
    {
        if (_pool == null && monsterPrefab != null)
        {
            _pool = new ComponentPool<Monster>(monsterPrefab, transform, prewarm);
        }
    }

    private void SpawnOne()
    {
        EnsurePool();
        if (_pool == null || monsterPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        Monster monster = _pool.Get();
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        monster.Spawn(point.position);
        monster.Died += OnMonsterDied;
        _alive.Add(monster);
    }

    private void OnMonsterDied(Monster monster)
    {
        monster.Died -= OnMonsterDied;
        _alive.Remove(monster);
        StartCoroutine(RespawnRoutine(monster));
    }

    private IEnumerator RespawnRoutine(Monster monster)
    {
        yield return new WaitForSeconds(respawnDelay);
        _pool.Release(monster);
        while (_alive.Count < maxAlive)
        {
            SpawnOne();
        }
    }

    /// <summary>
    /// <paramref name="from"/> 에서 가장 가까운 살아있는 몬스터를 반환한다. 없으면 null.
    /// </summary>
    public Monster GetNearestAlive(Vector2 from)
    {
        _positionBuffer.Clear();
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null || !_alive[i].IsAlive)
            {
                _alive.RemoveAt(i);
            }
        }

        for (int i = 0; i < _alive.Count; i++)
        {
            _positionBuffer.Add(_alive[i].transform.position);
        }

        int index = TargetSelector.GetNearestIndex(from, _positionBuffer);
        return index >= 0 ? _alive[index] : null;
    }
}
