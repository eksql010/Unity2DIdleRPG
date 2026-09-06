using System;
using System.Collections.Generic;

/// <summary>
/// 데미지 숫자(<see cref="DamageText"/>) 오브젝트 풀. Instantiate/Destroy 를 반복하지 않고
/// 비활성 인스턴스를 재사용한다. (기획서 4.4 "데미지 텍스트는 Object Pooling 으로 관리")
///
/// <see cref="MonsterPool"/> 과 같은 설계 — 생성 팩토리를 주입받는 순수 클래스라
/// 프리팹 없이도(테스트에서 코드로 만든 인스턴스로) 검증할 수 있다.
/// </summary>
public class DamageTextPool
{
    private readonly Func<DamageText> _factory;
    private readonly Stack<DamageText> _idle = new Stack<DamageText>();

    /// <summary>지금까지 팩토리로 실제 생성한 총 인스턴스 수(풀 크기).</summary>
    public int TotalCreated { get; private set; }

    /// <summary>현재 대기(비활성) 중인 인스턴스 수.</summary>
    public int IdleCount => _idle.Count;

    /// <param name="factory">새 <see cref="DamageText"/> 인스턴스를 만드는 함수. null 이면 예외.</param>
    /// <param name="prewarm">미리 만들어 둘 인스턴스 수(선택).</param>
    public DamageTextPool(Func<DamageText> factory, int prewarm = 0)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        for (int i = 0; i < prewarm; i++)
        {
            DamageText t = Create();
            t.Hide();
            _idle.Push(t);
        }
    }

    private DamageText Create()
    {
        DamageText t = _factory();
        if (t == null)
        {
            throw new InvalidOperationException("DamageTextPool 팩토리가 null 을 반환했다.");
        }
        TotalCreated++;
        return t;
    }

    /// <summary>대기 인스턴스가 있으면 재사용하고, 없으면 새로 만든다. 아직 Play 는 호출하지 않는다.</summary>
    public DamageText Rent()
    {
        return _idle.Count > 0 ? _idle.Pop() : Create();
    }

    /// <summary>사용이 끝난 인스턴스를 풀로 되돌린다. 중복 반납은 무시한다.</summary>
    public void Return(DamageText text)
    {
        if (text == null || _idle.Contains(text))
        {
            return;
        }
        text.Hide();
        _idle.Push(text);
    }
}
