using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 원웨이 플랫폼 다층 구조에서
///   (1) 바닥에서부터 점프만으로 모든 층을 순서대로 올라갈 수 있는지
///   (2) 각 층에서 드롭다운으로 바로 아래층까지 내려올 수 있는지
/// 를 자동 검증한다.
///
/// 플랫폼 간 수직 간격은 현재 점프 파라미터(jumpForce 13, gravityScale 3, mass 1)의
/// 최대 도달 높이(약 2.87 units)를 기준으로 여유를 둔 2.0 units 이다.
/// </summary>
public class PlatformClimbTests
{
    private const float JumpForce = 13f;
    private const float GravityScale = 3f;
    private const float TierStep = 2.0f;      // 층 간 표면 높이 차 (max 도달 2.87 의 약 70%)
    private const int TierCount = 3;          // 바닥 위 원웨이 플랫폼 층 수
    private const float GroundSurfaceY = 0f;

    private GameObject _root;
    private PlayerMovement _movement;
    private Rigidbody2D _rb;
    private readonly List<float> _tierSurfaceY = new List<float>();

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{field}' 없음");
        f.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        Assert.AreNotEqual(-1, groundLayer);
        Assert.AreNotEqual(-1, oneWayLayer);

        _root = new GameObject("PlatformClimbRoot");

        // 바닥
        var ground = new GameObject("Ground");
        ground.transform.SetParent(_root.transform);
        ground.layer = groundLayer;
        ground.transform.position = new Vector3(0f, GroundSurfaceY - 0.5f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);

        // 원웨이 플랫폼 층 (모두 x=0 에 수직 정렬)
        _tierSurfaceY.Clear();
        for (int i = 1; i <= TierCount; i++)
        {
            float surfaceY = GroundSurfaceY + TierStep * i;
            _tierSurfaceY.Add(surfaceY);

            var plat = new GameObject($"OneWay_Tier{i}");
            plat.transform.SetParent(_root.transform);
            plat.layer = oneWayLayer;
            plat.transform.position = new Vector3(0f, surfaceY - 0.15f, 0f);
            plat.transform.localScale = new Vector3(6f, 0.3f, 1f);
            var col = plat.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.usedByEffector = true;
            var eff = plat.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 170f;
        }

        // 플레이어
        var player = new GameObject("Player");
        player.transform.SetParent(_root.transform);
        player.transform.position = new Vector3(0f, GroundSurfaceY + 1f, 0f);
        _rb = player.AddComponent<Rigidbody2D>();
        _rb.gravityScale = GravityScale;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var pcol = player.AddComponent<CapsuleCollider2D>();
        pcol.size = new Vector2(0.8f, 1.6f);
        player.AddComponent<SpriteRenderer>();

        var groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, -0.85f, 0f);

        _movement = player.AddComponent<PlayerMovement>();
        SetPrivate(_movement, "groundCheck", groundCheck.transform);
        SetPrivate(_movement, "groundCheckRadius", 0.22f);
        SetPrivate(_movement, "groundLayer", (LayerMask)((1 << groundLayer) | (1 << oneWayLayer)));
        SetPrivate(_movement, "oneWayLayer", (LayerMask)(1 << oneWayLayer));
        SetPrivate(_movement, "moveSpeed", 5f);
        SetPrivate(_movement, "jumpForce", JumpForce);
        SetPrivate(_movement, "dropDownDuration", 0.35f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
        }
    }

    private IEnumerator SettleUntilGrounded(float timeout = 3f)
    {
        float t = timeout;
        while (t > 0f)
        {
            yield return new WaitForFixedUpdate();
            if (_movement.IsGrounded && Mathf.Abs(_rb.linearVelocity.y) < 0.05f)
            {
                yield break;
            }
            t -= Time.fixedDeltaTime;
        }
    }

    /// <summary>바닥 표면 기준 현재 서 있는 층 인덱스(-1 = 바닥).</summary>
    private int CurrentTier()
    {
        float feetY = _rb.position.y - 0.8f;
        int tier = -1;
        for (int i = 0; i < _tierSurfaceY.Count; i++)
        {
            if (feetY > _tierSurfaceY[i] - 0.35f)
            {
                tier = i;
            }
        }
        return tier;
    }

    [UnityTest]
    public IEnumerator Player_CanJumpUp_EveryTier_InOrder()
    {
        yield return SettleUntilGrounded();
        Assert.AreEqual(-1, CurrentTier(), "바닥에서 시작해야 합니다.");

        for (int target = 0; target < TierCount; target++)
        {
            // 한 층 오를 때까지 점프 반복(최대 6회)
            int jumps = 0;
            while (CurrentTier() < target && jumps < 6)
            {
                _movement.Jump();
                jumps++;
                yield return SettleUntilGrounded();
            }

            Assert.AreEqual(target, CurrentTier(),
                $"{target}층(표면 y={_tierSurfaceY[target]:0.00})에 점프로 도달하지 못했습니다. " +
                $"현재 발 높이 ≈ {_rb.position.y - 0.8f:0.00}");
        }
    }

    [UnityTest]
    public IEnumerator Player_CanDropDown_EveryTier_InOrder()
    {
        // 최상층까지 순간이동 후, 드롭다운으로 한 층씩 내려오는지 확인
        _rb.position = new Vector2(0f, _tierSurfaceY[TierCount - 1] + 0.8f);
        yield return SettleUntilGrounded();
        Assert.AreEqual(TierCount - 1, CurrentTier(), "최상층에 서 있어야 합니다.");

        for (int expected = TierCount - 2; expected >= -1; expected--)
        {
            int before = CurrentTier();
            _movement.DropDown();

            float t = 3f;
            while (t > 0f && CurrentTier() >= before)
            {
                yield return new WaitForFixedUpdate();
                t -= Time.fixedDeltaTime;
            }
            yield return SettleUntilGrounded();

            Assert.AreEqual(expected, CurrentTier(),
                $"드롭다운으로 {expected}층까지 내려오지 못했습니다. 현재 발 높이 ≈ {_rb.position.y - 0.8f:0.00}");
        }
    }
}
