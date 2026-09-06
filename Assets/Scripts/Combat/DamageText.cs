using UnityEngine;

/// <summary>
/// 몬스터가 맞은 순간 그 머리 위에 잠깐 떠오르는 데미지 숫자 하나. (4단계-3b)
///
/// 월드 공간 <see cref="TextMesh"/> 로 그린다(별도 Canvas 불필요). Object Pooling 으로
/// 재사용되므로 스스로 Destroy 하지 않는다 — 수명이 끝나면 <see cref="Tick"/> 이 false 를
/// 돌려주고, 실제 숨김/반납은 <see cref="DamageTextSpawner"/> 가 담당한다. (기획서 4.4)
/// </summary>
[DisallowMultipleComponent]
public class DamageText : MonoBehaviour
{
    [Tooltip("숫자가 떠 있는 총 시간(초).")]
    [SerializeField] private float lifetime = 0.7f;

    [Tooltip("초당 위로 떠오르는 속도(월드 단위).")]
    [SerializeField] private float riseSpeed = 1.6f;

    [Tooltip("일반 타격 색.")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("크리티컬 타격 색.")]
    [SerializeField] private Color critColor = new Color(1f, 0.65f, 0.1f, 1f);

    private TextMesh _textMesh;
    private float _elapsed;
    private bool _playing;
    private Vector3 _baseColorScale;

    /// <summary>지금 떠오르는 중인가.</summary>
    public bool IsPlaying => _playing;

    /// <summary>가장 최근에 표시한 문자열(테스트 확인용).</summary>
    public string DisplayedText => _textMesh != null ? _textMesh.text : null;

    /// <summary>가장 최근 타격이 크리티컬이었는가(테스트 확인용).</summary>
    public bool LastWasCrit { get; private set; }

    private void Awake()
    {
        EnsureTextMesh();
    }

    private void EnsureTextMesh()
    {
        if (_textMesh == null)
        {
            _textMesh = GetComponent<TextMesh>();
        }
        if (_textMesh == null)
        {
            _textMesh = gameObject.AddComponent<TextMesh>();
        }
        _textMesh.anchor = TextAnchor.LowerCenter;
        _textMesh.alignment = TextAlignment.Center;
        _textMesh.fontSize = 48;
        _textMesh.characterSize = 0.12f;
        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 50; // 몬스터(5)·플레이어보다 앞
        }
    }

    /// <summary>표시에 쓸 폰트를 지정한다(스포너가 풀 생성 시 호출).</summary>
    public void SetFont(Font font)
    {
        EnsureTextMesh();
        if (font == null)
        {
            return;
        }
        _textMesh.font = font;
        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = font.material;
        }
    }

    /// <summary>
    /// 풀에서 꺼내 이 위치에서 숫자를 띄우기 시작한다.
    /// </summary>
    /// <param name="worldPosition">시작 위치(보통 맞은 몬스터 머리 위).</param>
    /// <param name="amount">표시할 데미지량. 정수로 반올림해 보여준다.</param>
    /// <param name="isCrit">크리티컬이면 색/크기를 구분한다. (기획서 5.3)</param>
    public void Play(Vector3 worldPosition, float amount, bool isCrit)
    {
        EnsureTextMesh();

        int shown = Mathf.Max(0, Mathf.RoundToInt(amount));
        _textMesh.text = isCrit ? shown + "!" : shown.ToString();
        _textMesh.color = isCrit ? critColor : normalColor;
        LastWasCrit = isCrit;

        float scale = isCrit ? 1.5f : 1f;
        transform.localScale = new Vector3(scale, scale, scale);
        transform.position = worldPosition;

        _elapsed = 0f;
        _playing = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 한 스텝 진행한다. 위로 떠오르고, 수명 후반부에 서서히 사라진다.
    /// </summary>
    /// <returns>아직 떠 있으면 true, 수명이 끝났으면 false(스포너가 반납).</returns>
    public bool Tick(float deltaTime)
    {
        if (!_playing)
        {
            return false;
        }

        _elapsed += deltaTime;
        transform.position += Vector3.up * (riseSpeed * deltaTime);

        float t = lifetime > 0f ? Mathf.Clamp01(_elapsed / lifetime) : 1f;
        // 후반 40% 구간에서 알파를 1 → 0 으로.
        float alpha = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);
        if (_textMesh != null)
        {
            Color c = _textMesh.color;
            c.a = alpha;
            _textMesh.color = c;
        }

        if (_elapsed >= lifetime)
        {
            _playing = false;
            return false;
        }
        return true;
    }

    /// <summary>스포너가 풀로 반납하기 직전에 호출. 씬에서 감춘다.</summary>
    public void Hide()
    {
        _playing = false;
        gameObject.SetActive(false);
    }
}
