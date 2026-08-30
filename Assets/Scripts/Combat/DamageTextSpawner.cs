using UnityEngine;

/// <summary>
/// 데미지 텍스트를 오브젝트 풀로 관리하며 요청 위치에 띄운다 (기획서 4.4).
/// </summary>
public class DamageTextSpawner : MonoBehaviour
{
    [SerializeField] private DamageText damageTextPrefab;
    [SerializeField] private int prewarm = 8;

    private ComponentPool<DamageText> _pool;

    /// <summary>지정 위치에 데미지 숫자를 띄운다.</summary>
    public void Spawn(Vector3 worldPosition, int amount, bool isCritical)
    {
        if (damageTextPrefab == null)
        {
            return;
        }
        if (_pool == null)
        {
            _pool = new ComponentPool<DamageText>(damageTextPrefab, transform, prewarm);
        }

        DamageText text = _pool.Get();
        text.Finished -= OnFinished;
        text.Finished += OnFinished;
        text.Show(worldPosition, amount, isCritical);
    }

    private void OnFinished(DamageText text)
    {
        text.Finished -= OnFinished;
        _pool.Release(text);
    }
}
