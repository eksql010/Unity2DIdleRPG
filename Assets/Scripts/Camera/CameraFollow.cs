using UnityEngine;

/// <summary>
/// 카메라가 타겟(플레이어)을 따라가되, X축 범위를 스테이지 경계로 제한한다.
/// MVP 범위이므로 Cinemachine 없이 간단한 수동 추적만 구현한다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("따라갈 대상(보통 플레이어).")]
    [SerializeField] private Transform target;

    [Tooltip("카메라 X 최소 위치 (스테이지 왼쪽 경계).")]
    [SerializeField] private float minX = -10f;
    [Tooltip("카메라 X 최대 위치 (스테이지 오른쪽 경계).")]
    [SerializeField] private float maxX = 10f;

    [Tooltip("카메라 Y 고정 위치.")]
    [SerializeField] private float fixedY = 0f;

    [Tooltip("추적 부드러움. 0 이면 즉시 따라간다.")]
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 _velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float desiredX = Mathf.Clamp(target.position.x, minX, maxX);
        Vector3 desired = new Vector3(desiredX, fixedY, transform.position.z);

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime)
            : desired;
    }
}
