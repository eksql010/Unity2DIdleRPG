using System;
using UnityEngine;

/// <summary>
/// 데미지 숫자를 잠깐 위로 떠오르며 사라지게 표시하는 월드 텍스트. 오브젝트 풀로 재사용된다.
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class DamageText : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private float riseSpeed = 1.6f;
    [SerializeField] private Color normalColor = new Color(1f, 0.95f, 0.6f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.5f, 0.2f);

    private TextMesh _text;
    private float _elapsed;
    private bool _running;

    /// <summary>수명이 다했을 때 호출된다(풀 반환용).</summary>
    public event Action<DamageText> Finished;

    private void Awake()
    {
        _text = GetComponent<TextMesh>();
    }

    /// <summary>지정 위치에서 데미지 숫자를 띄운다.</summary>
    public void Show(Vector3 worldPosition, int amount, bool isCritical)
    {
        transform.position = worldPosition;
        _text.text = isCritical ? amount + "!" : amount.ToString();
        _text.color = isCritical ? criticalColor : normalColor;
        _text.characterSize = isCritical ? 0.16f : 0.12f;
        _elapsed = 0f;
        _running = true;
    }

    private void Update()
    {
        if (!_running)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        float k = Mathf.Clamp01(_elapsed / lifetime);
        Color c = _text.color;
        c.a = 1f - k;
        _text.color = c;

        if (_elapsed >= lifetime)
        {
            _running = false;
            Finished?.Invoke(this);
        }
    }
}
