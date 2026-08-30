using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹 기반 오브젝트 풀 (기획서 4.4: Instantiate/Destroy 반복 금지).
/// 컴포넌트 T 를 가진 프리팹을 재사용한다.
/// </summary>
public class ComponentPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Queue<T> _inactive = new Queue<T>();
    private readonly List<T> _all = new List<T>();

    public ComponentPool(T prefab, Transform parent, int prewarm = 0)
    {
        _prefab = prefab;
        _parent = parent;
        for (int i = 0; i < prewarm; i++)
        {
            T instance = CreateNew();
            instance.gameObject.SetActive(false);
            _inactive.Enqueue(instance);
        }
    }

    /// <summary>지금까지 생성된 전체 인스턴스(활성 + 비활성).</summary>
    public IReadOnlyList<T> All => _all;

    private T CreateNew()
    {
        T instance = Object.Instantiate(_prefab, _parent);
        _all.Add(instance);
        return instance;
    }

    /// <summary>사용 가능한 인스턴스를 꺼내 활성화한다.</summary>
    public T Get()
    {
        T instance = _inactive.Count > 0 ? _inactive.Dequeue() : CreateNew();
        instance.gameObject.SetActive(true);
        return instance;
    }

    /// <summary>인스턴스를 비활성화하고 풀로 되돌린다.</summary>
    public void Release(T instance)
    {
        if (instance == null)
        {
            return;
        }
        instance.gameObject.SetActive(false);
        _inactive.Enqueue(instance);
    }
}
