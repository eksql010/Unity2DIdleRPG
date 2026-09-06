using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 오브젝트 풀. Instantiate/Destroy 를 반복하지 않고 비활성 인스턴스를 재사용한다. (기획서 4.4)
///
/// MonoBehaviour 가 아니라 생성 팩토리(<see cref="Func{Monster}"/>)를 주입받는 순수 클래스라,
/// 프리팹 없이도(테스트에서 코드로 만든 Monster 로) 동작을 검증할 수 있다.
/// </summary>
public class MonsterPool
{
    private readonly Func<Monster> _factory;
    private readonly Stack<Monster> _idle = new Stack<Monster>();

    /// <summary>지금까지 팩토리로 실제 생성한 총 인스턴스 수(풀 크기).</summary>
    public int TotalCreated { get; private set; }

    /// <summary>현재 대기(비활성) 중인 인스턴스 수.</summary>
    public int IdleCount => _idle.Count;

    /// <param name="factory">새 Monster 인스턴스를 만드는 함수. null 이면 예외.</param>
    /// <param name="prewarm">미리 만들어 둘 인스턴스 수(선택).</param>
    public MonsterPool(Func<Monster> factory, int prewarm = 0)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        for (int i = 0; i < prewarm; i++)
        {
            Monster m = Create();
            m.Despawn();
            _idle.Push(m);
        }
    }

    private Monster Create()
    {
        Monster m = _factory();
        if (m == null)
        {
            throw new InvalidOperationException("MonsterPool 팩토리가 null 을 반환했다.");
        }
        TotalCreated++;
        return m;
    }

    /// <summary>대기 인스턴스가 있으면 재사용하고, 없으면 새로 만든다. 아직 Spawn 은 호출하지 않는다.</summary>
    public Monster Rent()
    {
        return _idle.Count > 0 ? _idle.Pop() : Create();
    }

    /// <summary>사용이 끝난 인스턴스를 풀로 되돌린다. 중복 반납은 무시한다.</summary>
    public void Return(Monster monster)
    {
        if (monster == null || _idle.Contains(monster))
        {
            return;
        }
        monster.Despawn();
        _idle.Push(monster);
    }
}
