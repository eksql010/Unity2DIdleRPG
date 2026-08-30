using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayerMovement 의 공유 API(MoveHorizontal / Jump / DropDown / IsGrounded) 동작 검증.
/// 자동전투 FSM 이 이 API 들을 그대로 호출하므로, FSM 구현 전에 계약을 고정해 둔다.
/// </summary>
public class PlayerMovementTests
{
    private GameObject _player;
    private GameObject _ground;
    private PlayerMovement _move;
    private Rigidbody2D _rb;

    private const float MoveSpeed = 5f;
    private const float JumpForce = 14f;

    /// <summary>private [SerializeField] 필드를 리플렉션으로 주입한다(테스트 전용).</summary>
    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    private static int GroundMask =>
        (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("OneWayPlatform"));

    private static int OneWayMask => 1 << LayerMask.NameToLayer("OneWayPlatform");

    [SetUp]
    public void SetUp()
    {
        Assert.AreNotEqual(-1, LayerMask.NameToLayer("Ground"),
            "'Ground' 레이어가 필요합니다.");
        Assert.AreNotEqual(-1, LayerMask.NameToLayer("OneWayPlatform"),
            "'OneWayPlatform' 레이어가 필요합니다.");

        _ground = new GameObject("TestGround");
        _ground.layer = LayerMask.NameToLayer("Ground");
        _ground.transform.position = new Vector3(0f, -1f, 0f);
        BoxCollider2D groundCol = _ground.AddComponent<BoxCollider2D>();
        groundCol.size = new Vector2(80f, 1f);

        _player = new GameObject("TestPlayer");
        _player.transform.position = new Vector3(0f, 1f, 0f);

        _rb = _player.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 3f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D playerCol = _player.AddComponent<CapsuleCollider2D>();
        playerCol.size = new Vector2(0.8f, 1.6f);

        _player.AddComponent<SpriteRenderer>();

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(_player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, -0.85f, 0f);

        _move = _player.AddComponent<PlayerMovement>();
        SetPrivate(_move, "groundCheck", groundCheck.transform);
        SetPrivate(_move, "groundCheckRadius", 0.2f);
        SetPrivate(_move, "groundLayer", (LayerMask)GroundMask);
        SetPrivate(_move, "oneWayLayer", (LayerMask)OneWayMask);
        SetPrivate(_move, "moveSpeed", MoveSpeed);
        SetPrivate(_move, "jumpForce", JumpForce);
        SetPrivate(_move, "dropDownDuration", 0.3f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_player != null) Object.Destroy(_player);
        if (_ground != null) Object.Destroy(_ground);
    }

    private IEnumerator SettleOnGround()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator IsGrounded_True_WhenRestingOnGround()
    {
        yield return SettleOnGround();
        Assert.IsTrue(_move.IsGrounded, "바닥 위에 있는데 접지로 인식하지 못했습니다.");
    }

    [UnityTest]
    public IEnumerator MoveHorizontal_SetsVelocityToMoveSpeed()
    {
        yield return SettleOnGround();

        _move.MoveHorizontal(1f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(MoveSpeed, _rb.linearVelocity.x, 0.5f, "오른쪽 이동 속도가 맞지 않습니다.");
        Assert.AreEqual(1, _move.FacingDirection);

        _move.MoveHorizontal(-1f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(-MoveSpeed, _rb.linearVelocity.x, 0.5f, "왼쪽 이동 속도가 맞지 않습니다.");
        Assert.AreEqual(-1, _move.FacingDirection);
    }

    [UnityTest]
    public IEnumerator MoveHorizontal_Zero_StopsImmediately()
    {
        yield return SettleOnGround();

        _move.MoveHorizontal(1f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        _move.MoveHorizontal(0f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0f, _rb.linearVelocity.x, 0.01f, "정지 명령 후에도 수평 속도가 남아 있습니다.");
    }

    [UnityTest]
    public IEnumerator Jump_LiftsPlayer_AndBlocksAirReJump()
    {
        yield return SettleOnGround();
        Assert.IsTrue(_move.IsGrounded);

        _move.Jump();
        yield return new WaitForFixedUpdate();
        Assert.Greater(_rb.linearVelocity.y, 0.5f, "점프 후 위로 상승하지 않았습니다.");

        // 공중에서 재점프 시도 → Impulse 가 추가되지 않아야 한다
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(_move.IsGrounded, "아직 공중이어야 합니다.");
        float vyBefore = _rb.linearVelocity.y;
        _move.Jump();
        yield return new WaitForFixedUpdate();
        Assert.Less(_rb.linearVelocity.y, vyBefore,
            "공중 재점프가 막히지 않고 위쪽 속도가 증가했습니다.");
    }

    [UnityTest]
    public IEnumerator DropDown_FallsThroughOneWayPlatform()
    {
        // 바닥 위쪽에 원웨이 플랫폼 배치
        GameObject platform = new GameObject("OneWay");
        platform.layer = LayerMask.NameToLayer("OneWayPlatform");
        platform.transform.position = new Vector3(0f, 1f, 0f);
        BoxCollider2D pcol = platform.AddComponent<BoxCollider2D>();
        pcol.size = new Vector2(10f, 0.4f);
        pcol.usedByEffector = true;
        PlatformEffector2D eff = platform.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;

        _player.transform.position = new Vector3(0f, 3f, 0f);

        // 플랫폼 위에 안착할 때까지 대기
        for (int i = 0; i < 120; i++)
        {
            yield return new WaitForFixedUpdate();
            if (_move.IsGrounded && Mathf.Abs(_rb.linearVelocity.y) < 0.05f)
            {
                break;
            }
        }
        Assert.Greater(_player.transform.position.y, 1f, "플랫폼 위에 서 있어야 합니다.");

        _move.DropDown();

        for (int i = 0; i < 120; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.Less(_player.transform.position.y, 1f,
            "드롭다운 후 원웨이 플랫폼 아래로 내려가야 합니다.");

        Object.Destroy(platform);
    }
}
