using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// PlayerInputHandler 가 새 Input System 의 Player 맵(Move / Jump)을
/// IPlayerMotor 의 공유 API(MoveHorizontal / Jump / DropDown)로 올바르게 번역하는지 검증한다. (기획서 2.3 / 2.4)
/// 이동 물리 자체는 PlayerMovementTests 가 담당하므로, 여기서는 가짜 모터로 "무엇을 호출했는지"만 본다.
/// </summary>
public class PlayerInputHandlerTests : InputTestFixture
{
    /// <summary>호출된 API 만 기록하는 가짜 IPlayerMotor.</summary>
    private class FakeMotor : MonoBehaviour, IPlayerMotor
    {
        public float LastHorizontal;
        public int JumpCount;
        public int DropDownCount;

        public void MoveHorizontal(float direction) => LastHorizontal = direction;
        public void Jump() => JumpCount++;
        public void DropDown() => DropDownCount++;
    }

    private Keyboard _keyboard;
    private InputActionAsset _actions;
    private GameObject _go;
    private FakeMotor _motor;

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, $"필드 '{fieldName}' 를 찾지 못했습니다.");
        f.SetValue(target, value);
    }

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        _keyboard = InputSystem.AddDevice<Keyboard>();

        // 실제 InputSystem_Actions 와 같은 형태의 최소 Player 맵을 코드로 구성한다.
        _actions = ScriptableObject.CreateInstance<InputActionAsset>();
        InputActionMap map = _actions.AddActionMap("Player");

        InputAction move = map.AddAction("Move", InputActionType.Value);
        move.expectedControlType = "Vector2";
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        InputAction jump = map.AddAction("Jump", InputActionType.Button);
        jump.AddBinding("<Keyboard>/space");

        // Awake/OnEnable 전에 SerializeField 를 주입하기 위해 비활성 상태로 만든 뒤 컴포넌트를 붙인다.
        _go = new GameObject("PlayerInput");
        _go.SetActive(false);
        _motor = _go.AddComponent<FakeMotor>();
        PlayerInputHandler input = _go.AddComponent<PlayerInputHandler>();
        SetPrivate(input, "inputActions", _actions);
        SetPrivate(input, "dropDownThreshold", -0.5f);
        _go.SetActive(true);
    }

    [TearDown]
    public override void TearDown()
    {
        if (_go != null) Object.Destroy(_go);
        if (_actions != null) Object.Destroy(_actions);
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator Move_RightKey_DrivesMoveHorizontalPositive()
    {
        Press(_keyboard.dKey);
        yield return null; // PlayerInputHandler.Update 한 프레임 실행

        Assert.Greater(_motor.LastHorizontal, 0.5f, "오른쪽 키 입력이 MoveHorizontal 양수로 전달되지 않았습니다.");

        Release(_keyboard.dKey);
        yield return null;
        Assert.AreEqual(0f, _motor.LastHorizontal, 0.01f, "키를 뗐는데도 이동 입력이 남아 있습니다.");
    }

    [UnityTest]
    public IEnumerator Jump_SpaceOnly_CallsJumpNotDropDown()
    {
        Press(_keyboard.spaceKey);
        yield return null;

        Assert.AreEqual(1, _motor.JumpCount, "Space 입력이 Jump 로 이어지지 않았습니다.");
        Assert.AreEqual(0, _motor.DropDownCount, "아래 입력 없이 드롭다운이 호출됐습니다.");
    }

    [UnityTest]
    public IEnumerator Jump_WithDownHeld_CallsDropDownNotJump()
    {
        Press(_keyboard.sKey);
        yield return null; // 아래 방향이 Move.y = -1 로 반영될 시간

        Press(_keyboard.spaceKey);
        yield return null;

        Assert.AreEqual(1, _motor.DropDownCount, "아래+점프 입력이 드롭다운으로 이어지지 않았습니다.");
        Assert.AreEqual(0, _motor.JumpCount, "드롭다운 상황에서 일반 점프가 호출됐습니다.");
    }
}
