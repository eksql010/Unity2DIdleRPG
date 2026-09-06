using UnityEngine;

/// <summary>
/// 플레이어를 따라가는 2D 카메라. 대상의 X 를 따라가되 스테이지 좌우 경계에서 X 를 제한한다.
/// Y 는 스테이지가 세로로 짧으므로 고정값을 유지한다. (기획서 3.4 — "간단한 X축 제한")
/// Cinemachine 등 외부 패키지 없이 동작하도록 최소 구현.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    [Tooltip("따라갈 대상. 보통 Player Transform.")]
    [SerializeField] private Transform target;

    [Header("X축 제한")]
    [Tooltip("카메라 중심 X 의 최소값 (스테이지 왼쪽 경계).")]
    [SerializeField] private float minX = -11f;
    [Tooltip("카메라 중심 X 의 최대값 (스테이지 오른쪽 경계).")]
    [SerializeField] private float maxX = 11f;

    [Header("Y축")]
    [Tooltip("카메라가 유지할 고정 Y. 스테이지가 세로로 짧아 Y 추적은 하지 않는다.")]
    [SerializeField] private float fixedY = 0f;

    [Header("따라가기")]
    [Tooltip("목표 X 로 수렴하는 데 걸리는 대략적인 시간(초). 0 이면 즉시 스냅.")]
    [SerializeField] private float smoothTime = 0.15f;

    private float _velocityX;

    /// <summary>런타임에 대상을 교체한다 (씬 로드 후 스포너 등에서 사용).</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>스테이지 경계에 맞춰 X 제한 범위를 설정한다.</summary>
    public void SetBounds(float newMinX, float newMaxX)
    {
        minX = Mathf.Min(newMinX, newMaxX);
        maxX = Mathf.Max(newMinX, newMaxX);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // minX > maxX (스테이지가 화면보다 좁은 경우) 이면 중앙에 고정
        float lo = Mathf.Min(minX, maxX);
        float hi = Mathf.Max(minX, maxX);
        float desiredX = Mathf.Clamp(target.position.x, lo, hi);

        float newX;
        if (smoothTime > 0f)
        {
            newX = Mathf.SmoothDamp(transform.position.x, desiredX, ref _velocityX, smoothTime);
        }
        else
        {
            newX = desiredX;
        }

        transform.position = new Vector3(newX, fixedY, transform.position.z);
    }
}
