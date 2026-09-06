using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// CameraFollow 의 X 추적 + 좌우 경계 제한 + Y 고정 동작 검증. (기획서 3.4)
/// </summary>
public class CameraFollowTests
{
    private GameObject _camObj;
    private GameObject _targetObj;
    private CameraFollow _follow;

    private const float MinX = -10f;
    private const float MaxX = 10f;
    private const float FixedY = 0f;

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        _targetObj = new GameObject("TestTarget");
        _targetObj.transform.position = new Vector3(0f, -3f, 0f);

        _camObj = new GameObject("TestCamera");
        _camObj.transform.position = new Vector3(0f, FixedY, -10f);
        _follow = _camObj.AddComponent<CameraFollow>();
        SetPrivate(_follow, "target", _targetObj.transform);
        SetPrivate(_follow, "minX", MinX);
        SetPrivate(_follow, "maxX", MaxX);
        SetPrivate(_follow, "fixedY", FixedY);
        SetPrivate(_follow, "smoothTime", 0f); // 스냅 모드로 검증
    }

    [TearDown]
    public void TearDown()
    {
        if (_camObj != null) Object.Destroy(_camObj);
        if (_targetObj != null) Object.Destroy(_targetObj);
    }

    [UnityTest]
    public IEnumerator FollowsTargetX_WithinBounds()
    {
        _targetObj.transform.position = new Vector3(4.2f, -3f, 0f);
        yield return null;
        Assert.AreEqual(4.2f, _camObj.transform.position.x, 0.001f, "경계 안에서는 대상 X 를 따라가야 합니다.");
    }

    [UnityTest]
    public IEnumerator ClampsAtMaxX_WhenTargetBeyondRightEdge()
    {
        _targetObj.transform.position = new Vector3(999f, -3f, 0f);
        yield return null;
        Assert.AreEqual(MaxX, _camObj.transform.position.x, 0.001f, "오른쪽 경계를 넘어가면 maxX 로 제한되어야 합니다.");
    }

    [UnityTest]
    public IEnumerator ClampsAtMinX_WhenTargetBeyondLeftEdge()
    {
        _targetObj.transform.position = new Vector3(-999f, -3f, 0f);
        yield return null;
        Assert.AreEqual(MinX, _camObj.transform.position.x, 0.001f, "왼쪽 경계를 넘어가면 minX 로 제한되어야 합니다.");
    }

    [UnityTest]
    public IEnumerator KeepsFixedY_AndZ_RegardlessOfTarget()
    {
        _targetObj.transform.position = new Vector3(3f, 50f, 0f);
        yield return null;
        Assert.AreEqual(FixedY, _camObj.transform.position.y, 0.001f, "Y 는 고정값을 유지해야 합니다.");
        Assert.AreEqual(-10f, _camObj.transform.position.z, 0.001f, "Z 는 원래 값을 유지해야 합니다.");
    }

    [UnityTest]
    public IEnumerator NullTarget_DoesNotMoveOrThrow()
    {
        SetPrivate(_follow, "target", null);
        _camObj.transform.position = new Vector3(2f, FixedY, -10f);
        yield return null;
        Assert.AreEqual(2f, _camObj.transform.position.x, 0.001f, "대상이 없으면 카메라가 움직이지 않아야 합니다.");
    }

    [UnityTest]
    public IEnumerator SetBounds_SwapsInvertedArguments()
    {
        _follow.SetBounds(20f, -5f); // 뒤집어 전달
        _targetObj.transform.position = new Vector3(999f, -3f, 0f);
        yield return null;
        Assert.AreEqual(20f, _camObj.transform.position.x, 0.001f, "SetBounds 는 인자 순서가 뒤집혀도 정상 동작해야 합니다.");
    }
}
