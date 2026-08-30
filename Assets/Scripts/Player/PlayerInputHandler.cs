using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 수동 조작용 입력 처리기.
/// 새 Input System 의 액션 값을 읽어 <see cref="PlayerMovement"/> 의 public API 를 호출하기만 한다.
/// 자동전투 FSM 도 같은 API(MoveHorizontal / Jump / DropDown)를 호출하는 또 다른 "호출자"이며,
/// 이 클래스와 FSM 은 서로 독립적이다. (입력 로직 ↔ 이동 로직 분리)
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerInputHandler : MonoBehaviour
{
    [Tooltip("Player 액션 맵(Move / Jump)이 정의된 Input Action Asset. 보통 InputSystem_Actions.")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("아래 방향으로 인정할 Move.y 임계값. 이 값보다 작으면서 점프를 누르면 드롭다운.")]
    [SerializeField] private float dropDownThreshold = -0.5f;

    private PlayerMovement _movement;
    private InputAction _moveAction;
    private InputAction _jumpAction;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();

        if (inputActions == null)
        {
            Debug.LogError("[PlayerInputHandler] inputActions 가 비어 있습니다. Inspector 에서 할당하세요.");
            enabled = false;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        _moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        _jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _jumpAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _jumpAction?.Disable();
    }

    private void Update()
    {
        Vector2 move = _moveAction.ReadValue<Vector2>();

        // 지속 입력 : 매 프레임 수평 이동 명령 전달
        _movement.MoveHorizontal(move.x);

        // 점프 입력이 이번 프레임에 눌렸는가
        if (_jumpAction.WasPressedThisFrame())
        {
            if (move.y < dropDownThreshold)
            {
                // 아래 방향 + 점프 동시 입력 → 원웨이 플랫폼 드롭다운
                _movement.DropDown();
            }
            else
            {
                _movement.Jump();
            }
        }
    }
}
