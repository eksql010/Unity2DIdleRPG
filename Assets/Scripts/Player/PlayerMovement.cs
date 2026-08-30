using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어의 물리 이동 로직.
/// 좌우 이동 / 점프 / 원웨이 플랫폼 드롭다운을 담당하며,
/// 이 세 기능은 외부(수동 입력 핸들러, 자동전투 FSM)가 공유해서 호출하는 public API 로 노출한다.
/// 즉 이 클래스는 "이동을 실제로 수행하는 쪽"이고, 누가 명령하는지는 알지 못한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("좌우 이동 속도 (units/sec). 가속 없이 즉시 이 속도로 전환된다.")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("점프")]
    [Tooltip("점프 시 Rigidbody2D 에 가하는 위쪽 Impulse 크기.")]
    [SerializeField] private float jumpForce = 8f;

    [Header("접지 판정")]
    [Tooltip("발밑 접지 판정 기준점. 보통 콜라이더 하단에 두는 자식 Transform.")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("접지 판정 원의 반지름.")]
    [SerializeField] private float groundCheckRadius = 0.15f;
    [Tooltip("바닥으로 인정할 레이어 (Ground + OneWayPlatform).")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("원웨이 플랫폼 레이어 (드롭다운 대상 판별용).")]
    [SerializeField] private LayerMask oneWayLayer;

    [Header("드롭다운")]
    [Tooltip("원웨이 플랫폼을 통과하는 동안 충돌을 무시하는 시간(초).")]
    [SerializeField] private float dropDownDuration = 0.3f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private SpriteRenderer _sprite;

    private float _moveInput;      // -1, 0, 1 로 정규화된 수평 입력
    private bool _isGrounded;
    private bool _jumpQueued;      // 이번 물리 프레임에 점프 요청됨
    private bool _dropQueued;      // 이번 물리 프레임에 드롭다운 요청됨

    /// <summary>현재 접지 상태. 자동전투 FSM 이 이동/점프 판단에 사용한다.</summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>바라보는 방향(+1 오른쪽 / -1 왼쪽).</summary>
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
    }

    // ---------------------------------------------------------------------
    // public API : 수동 입력 핸들러와 자동전투 FSM 이 공유해서 호출한다.
    // ---------------------------------------------------------------------

    /// <summary>
    /// 수평 이동 명령. 매 프레임 호출되는 지속 입력이며, 부호만 사용한다.
    /// 0 이면 즉시 정지한다.
    /// </summary>
    /// <param name="direction">이동 방향. 양수=오른쪽, 음수=왼쪽, 0=정지.</param>
    public void MoveHorizontal(float direction)
    {
        _moveInput = Mathf.Approximately(direction, 0f) ? 0f : Mathf.Sign(direction);
        if (_moveInput != 0f)
        {
            FacingDirection = (int)_moveInput;
        }
    }

    /// <summary>점프 요청. 실제 점프는 다음 FixedUpdate 에서 접지 상태일 때만 실행된다.</summary>
    public void Jump()
    {
        _jumpQueued = true;
    }

    /// <summary>
    /// 아래로 점프(드롭다운) 요청. 원웨이 플랫폼 위에 서 있을 때만 동작한다.
    /// 자동전투 FSM 이 아래층 몬스터에게 이동할 때도 호출한다.
    /// </summary>
    public void DropDown()
    {
        _dropQueued = true;
    }

    // ---------------------------------------------------------------------
    // 물리 처리
    // ---------------------------------------------------------------------

    private void FixedUpdate()
    {
        // 1. 접지 판정
        _isGrounded = groundCheck != null &&
                      Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 2. 수평 속도 즉시 전환 (가속/감속 없음). 공중에서도 동일하게 조작 가능.
        _rb.linearVelocity = new Vector2(_moveInput * moveSpeed, _rb.linearVelocity.y);

        // 3. 점프 : 접지 상태에서만, 공중 재점프 불가
        if (_jumpQueued)
        {
            _jumpQueued = false;
            if (_isGrounded)
            {
                // 기존 수직 속도를 지우고 일정한 점프 높이를 보장
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
                _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            }
        }

        // 4. 드롭다운 : 원웨이 플랫폼 위에 서 있을 때만
        if (_dropQueued)
        {
            _dropQueued = false;
            if (_isGrounded)
            {
                TryDropDown();
            }
        }
    }

    private void Update()
    {
        // 스프라이트 좌우 반전 (기본 스프라이트는 오른쪽을 바라본다고 가정)
        if (_sprite != null && _moveInput != 0f)
        {
            _sprite.flipX = _moveInput < 0f;
        }
    }

    /// <summary>
    /// 현재 밟고 있는 원웨이 플랫폼의 콜라이더를 잠시 무시해서 아래로 통과시킨다.
    /// </summary>
    private void TryDropDown()
    {
        // 발밑에서 원웨이 플랫폼 콜라이더 탐색
        Collider2D platform = Physics2D.OverlapCircle(
            groundCheck.position, groundCheckRadius * 1.5f, oneWayLayer);
        if (platform == null)
        {
            return;
        }

        StartCoroutine(IgnorePlatformRoutine(platform));
    }

    private IEnumerator IgnorePlatformRoutine(Collider2D platform)
    {
        Physics2D.IgnoreCollision(_col, platform, true);
        // 아래로 확실히 떨어지도록 살짝 아래 방향 속도를 준다
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -1f);

        yield return new WaitForSeconds(dropDownDuration);

        if (platform != null && _col != null)
        {
            Physics2D.IgnoreCollision(_col, platform, false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
