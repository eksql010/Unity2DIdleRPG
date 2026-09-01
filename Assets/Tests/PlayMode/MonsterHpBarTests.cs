using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 몬스터 HP 바 채움이 <b>좌측 고정, 우측만 안쪽으로</b> 줄어드는지 검증한다.
/// (기존에는 중앙 피벗 스프라이트를 스케일만 줄여 양쪽에서 동시에 줄어들었음.)
/// </summary>
public class MonsterHpBarTests
{
    private GameObject _root;
    private Monster _monster;
    private Transform _fill;
    private SpriteRenderer _fillRenderer;

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{field}' 없음");
        f.SetValue(target, value);
    }

    private static Sprite MakeUnitSprite()
    {
        var tex = new Texture2D(32, 32);
        return Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
    }

    private float LeftEdgeX()
    {
        float w = _fillRenderer.sprite.bounds.size.x * _fill.localScale.x;
        return _fill.localPosition.x - w * 0.5f;
    }

    private float RightEdgeX()
    {
        float w = _fillRenderer.sprite.bounds.size.x * _fill.localScale.x;
        return _fill.localPosition.x + w * 0.5f;
    }

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("HpBarTestRoot");

        var monsterGo = new GameObject("Monster");
        monsterGo.transform.SetParent(_root.transform);
        monsterGo.AddComponent<SpriteRenderer>();

        var fillGo = new GameObject("HpBarFill");
        fillGo.transform.SetParent(monsterGo.transform, false);
        fillGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        fillGo.transform.localScale = new Vector3(1f, 0.12f, 1f);
        _fillRenderer = fillGo.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite = MakeUnitSprite();
        _fill = fillGo.transform;

        _monster = monsterGo.AddComponent<Monster>();
        SetPrivate(_monster, "data", new MonsterData { maxHp = 100f, defense = 0f });
        SetPrivate(_monster, "hpBarFill", _fill);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
        }
    }

    [UnityTest]
    public IEnumerator Fill_ShrinksFromRight_WithLeftEdgeFixed()
    {
        _monster.Spawn(Vector3.zero);
        yield return null;

        float leftAtFull = LeftEdgeX();
        float rightAtFull = RightEdgeX();

        _monster.TakeDamage(40); // 60%
        float leftAt60 = LeftEdgeX();
        float rightAt60 = RightEdgeX();

        _monster.TakeDamage(40); // 20%
        float leftAt20 = LeftEdgeX();
        float rightAt20 = RightEdgeX();

        // 좌측 모서리는 항상 같은 위치
        Assert.AreEqual(leftAtFull, leftAt60, 0.0005f, "체력 60%에서 좌측 모서리가 이동했습니다.");
        Assert.AreEqual(leftAtFull, leftAt20, 0.0005f, "체력 20%에서 좌측 모서리가 이동했습니다.");

        // 우측 모서리는 체력이 줄수록 안쪽(왼쪽)으로
        Assert.Less(rightAt60, rightAtFull - 0.01f, "체력이 줄었는데 우측 모서리가 안 줄었습니다.");
        Assert.Less(rightAt20, rightAt60 - 0.01f, "체력이 더 줄었는데 우측 모서리가 안 줄었습니다.");

        // 채움 폭은 체력 비율에 비례 (100% -> 20% 이면 폭도 0.2배)
        float widthFull = rightAtFull - leftAtFull;
        float width20 = rightAt20 - leftAt20;
        Assert.AreEqual(0.2f, width20 / widthFull, 0.02f);
    }

    [UnityTest]
    public IEnumerator Fill_RestoresOnRespawn()
    {
        _monster.Spawn(Vector3.zero);
        yield return null;
        float rightAtFull = RightEdgeX();

        _monster.TakeDamage(80); // 20%
        Assert.Less(RightEdgeX(), rightAtFull - 0.01f);

        _monster.Spawn(Vector3.zero); // 풀 재사용 시뮬레이션
        Assert.AreEqual(rightAtFull, RightEdgeX(), 0.0005f, "리스폰 후 HP 바가 가득 차야 합니다.");
    }
}
