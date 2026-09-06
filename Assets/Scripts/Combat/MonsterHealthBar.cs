using UnityEngine;

/// <summary>
/// 몬스터 머리 위에 붙는 체력 게이지 하나. (INBOX 2026-09-07 05:26 항목 4)
///
/// 월드 공간 스프라이트 2장(배경 + 채움)으로 그린다 — 별도 Canvas 가 필요 없다.
/// 채움 막대의 <b>피벗을 좌측(0, 0.5)</b>에 두고, 왼쪽 끝을 배경 왼쪽 끝에 고정한 채
/// <see cref="Transform.localScale"/> 의 x 만 줄인다. 그래서 체력이 깎일 때 게이지가
/// 항상 "왼쪽 고정, 오른쪽이 줄어드는" 방향으로 감소한다.
/// (INBOX 항목 4 — 중앙 피벗이면 양쪽에서 줄어들어 보이는 문제를 좌측 피벗으로 해소)
///
/// <see cref="DamageText"/> 와 마찬가지로 Object Pooling 으로 재사용되므로 스스로 Destroy 하지
/// 않는다. 대상 몬스터가 죽거나 비활성화되면 <see cref="Tick"/> 이 false 를 돌려주고,
/// 실제 숨김/반납은 <see cref="MonsterHealthBarSpawner"/> 가 담당한다. (기획서 4.4)
/// </summary>
[DisallowMultipleComponent]
public class MonsterHealthBar : MonoBehaviour
{
    [Tooltip("게이지 전체 너비(월드 단위).")]
    [SerializeField] private float barWidth = 1.1f;

    [Tooltip("게이지 높이(월드 단위).")]
    [SerializeField] private float barHeight = 0.22f;

    [Tooltip("몬스터 위치에서 게이지를 얼마나 위에 띄울지(월드 단위).")]
    [SerializeField] private float verticalOffset = 1.05f;

    [Tooltip("게이지 배경(빈 체력) 색.")]
    [SerializeField] private Color backColor = new Color(0.11f, 0.11f, 0.11f, 0.85f);

    [Tooltip("남은 체력 색.")]
    [SerializeField] private Color fillColor = new Color(0.25f, 0.85f, 0.28f, 1f);

    private Transform _fill;
    private Monster _monster;
    private float _fraction = 1f;
    private bool _built;

    /// <summary>지금 체력을 표시 중인 대상 몬스터. 없으면 null.</summary>
    public Monster Monster => _monster;

    /// <summary>현재 표시 중인 체력 비율(0~1).</summary>
    public float Fraction => _fraction;

    /// <summary>지금 화면에 보이는가.</summary>
    public bool IsShowing => gameObject.activeSelf;

    /// <summary>채움 막대의 로컬 스케일 x (테스트 확인용 — 좌측 고정 스케일 검증).</summary>
    public float FillLocalScaleX => _fill != null ? _fill.localScale.x : 0f;

    /// <summary>채움 막대의 로컬 x 위치 (테스트 확인용 — 왼쪽 끝이 고정인지 검증).</summary>
    public float FillLocalPositionX => _fill != null ? _fill.localPosition.x : 0f;

    private static Sprite _centerSprite;
    private static Sprite _leftSprite;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;

        SpriteRenderer back = CreateQuad("Back", CenterSprite(), backColor, 40);
        back.transform.SetParent(transform, false);
        back.transform.localPosition = Vector3.zero;
        back.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        SpriteRenderer fill = CreateQuad("Fill", LeftSprite(), fillColor, 41);
        _fill = fill.transform;
        _fill.SetParent(transform, false);
        // 왼쪽 끝을 배경 왼쪽 끝(-barWidth/2)에 맞춘다. 좌측 피벗이라 scale.x 를 줄이면 오른쪽만 깎인다.
        _fill.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
        _fill.localScale = new Vector3(barWidth, barHeight, 1f);

        ApplyFraction();
    }

    private static SpriteRenderer CreateQuad(string quadName, Sprite sprite, Color color, int sortingOrder)
    {
        var go = new GameObject(quadName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder; // 몬스터(5)보다 앞, 데미지 숫자(50)보다 뒤
        return sr;
    }

    /// <summary>풀에서 꺼내 이 몬스터의 체력을 표시하기 시작한다. 채움은 가득 찬 상태로 시작.</summary>
    public void Bind(Monster monster)
    {
        EnsureBuilt();
        _monster = monster;
        _fraction = 1f;
        ApplyFraction();
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        gameObject.SetActive(true);
        FollowMonster();
    }

    /// <summary>표시 체력 비율을 직접 지정한다(0~1 로 클램프). 왼쪽 고정, scale.x 만 조정.</summary>
    public void SetFraction(float fraction)
    {
        _fraction = Mathf.Clamp01(fraction);
        ApplyFraction();
    }

    private void ApplyFraction()
    {
        if (_fill == null)
        {
            return;
        }
        Vector3 s = _fill.localScale;
        s.x = barWidth * _fraction; // 왼쪽 끝은 고정(localPosition 불변), 오른쪽만 줄어든다
        _fill.localScale = s;
    }

    /// <summary>
    /// 한 스텝. 대상 몬스터를 따라다니며 체력 비율을 갱신한다.
    /// </summary>
    /// <returns>대상이 아직 유효하면 true, 죽었거나 사라졌으면 false(스포너가 반납).</returns>
    public bool Tick()
    {
        if (_monster == null || !_monster.IsAlive || !_monster.gameObject.activeSelf)
        {
            return false;
        }

        float max = _monster.MaxHp > 0f ? _monster.MaxHp : 1f;
        SetFraction(_monster.CurrentHp / max);
        FollowMonster();
        return true;
    }

    private void FollowMonster()
    {
        if (_monster == null)
        {
            return;
        }
        transform.position = _monster.transform.position + Vector3.up * verticalOffset;
    }

    /// <summary>스포너가 풀로 반납하기 직전에 호출. 씬에서 감춘다.</summary>
    public void Hide()
    {
        _monster = null;
        gameObject.SetActive(false);
    }

    private static Sprite CenterSprite()
    {
        if (_centerSprite == null)
        {
            _centerSprite = BuildSprite(new Vector2(0.5f, 0.5f));
        }
        return _centerSprite;
    }

    private static Sprite LeftSprite()
    {
        if (_leftSprite == null)
        {
            _leftSprite = BuildSprite(new Vector2(0f, 0.5f)); // 좌측 피벗
        }
        return _leftSprite;
    }

    private static Sprite BuildSprite(Vector2 pivot)
    {
        Texture2D tex = Texture2D.whiteTexture;
        Sprite sp = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            pivot,
            tex.width);
        sp.name = "HealthBarQuad";
        return sp;
    }
}
