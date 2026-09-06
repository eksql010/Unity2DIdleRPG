using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 새 Input System 의 Player 액션맵(Move / Jump)을 읽어 <see cref="IPlayerMotor"/> 의 공유 API 를 호출하는 "입력 호출자".
/// 이동을 실제로 수행하는 것은 <see cref="PlayerMovement"/> 이고, 이 클래스는 수동 조작 입력을 그 API 로 번역만 한다.
/// 자동전투 FSM 은 이 클래스를 거치지 않고 동일한 <see cref="IPlayerMotor"/> API 를 직접 호출한다. (기획서 2.4)
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [Tooltip("InputSystem_Actions 애셋. 'Player' 맵의 Move(Vector2) / Jump(Button) 액션을 사용한다.")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Jump 입력 시점에 Move.y 가 이 값보다 작으면(아래를 누르고 있으면) 드롭다운으로 처리한다. (기획서 2.3)")]
    [SerializeField] private float dropDownThreshold = -0.5f;

    private IPlayerMotor _motor;
    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _jumpAction;

    private void Awake()
    {
        _motor = GetComponent<IPlayerMotor>();
        if (_motor == null)
        {
            Debug.LogError("[PlayerInputHandler] 같은 GameObject 에서 IPlayerMotor 를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        if (inputActions == null)
        {
            Debug.LogError("[PlayerInputHandler] inputActions 가 비어 있습니다. InputSystem_Actions 애셋을 할당하세요.", this);
            enabled = false;
            return;
        }

        _playerMap = inputActions.FindActionMap("Player", throwIfNotFound: false);
        _moveAction = _playerMap?.FindAction("Move", throwIfNotFound: false);
        _jumpAction = _playerMap?.FindAction("Jump", throwIfNotFound: false);
        if (_moveAction == null || _jumpAction == null)
        {
            Debug.LogError("[PlayerInputHandler] 'Player' 맵의 Move / Jump 액션을 찾지 못했습니다.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_jumpAction == null)
        {
            return;
        }
        _playerMap.Enable();
        _jumpAction.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
        }
        _playerMap?.Disable();

        // 입력이 끊기는 순간 이동도 멈춘다 (마지막 방향이 남아 계속 걷는 것 방지).
        _motor?.MoveHorizontal(0f);
    }

    private void Update()
    {
        _motor.MoveHorizontal(_moveAction.ReadValue<Vector2>().x);
    }

    /// <summary>
    /// Jump 입력 처리. 아래 방향을 함께 누르고 있으면 원웨이 플랫폼 드롭다운, 아니면 일반 점프. (기획서 2.3)
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext _)
    {
        if (_moveAction.ReadValue<Vector2>().y < dropDownThreshold)
        {
            _motor.DropDown();
        }
        else
        {
            _motor.Jump();
        }
    }
}
