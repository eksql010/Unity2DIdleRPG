using UnityEngine;

/// <summary>
/// 자동전투 상태머신 (기획서 4).
///   Idle → Move → Attack → Loot → Idle 순환.
/// 이동/점프/드롭다운은 <see cref="PlayerMovement"/> 의 공유 API 를 그대로 호출한다
/// (수동 입력 핸들러와 동일한 함수). 자동전투가 켜져 있으면 수동 입력 핸들러는 비활성화된다.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class AutoBattleController : MonoBehaviour
{
    public enum BattleState
    {
        Idle,
        Move,
        Attack,
        Loot,
    }

    [Header("참조")]
    [SerializeField] private MonsterSpawner spawner;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private DamageTextSpawner damageTextSpawner;
    [Tooltip("자동전투가 켜지면 이 수동 입력 핸들러를 비활성화한다.")]
    [SerializeField] private PlayerInputHandler manualInput;

    [Header("전투 튜닝")]
    [Tooltip("이 수평 거리 안이면 공격 가능.")]
    [SerializeField] private float attackRange = 1.4f;
    [Tooltip("공격 간격(초).")]
    [SerializeField] private float attackInterval = 0.55f;
    [Tooltip("목표와의 수직 차이가 이 값을 넘으면 점프/드롭다운으로 접근.")]
    [SerializeField] private float verticalReachThreshold = 0.7f;
    [Tooltip("처치 후 전리품 획득 연출 시간(초).")]
    [SerializeField] private float lootDuration = 0.25f;

    [SerializeField] private bool autoBattleEnabled = true;

    private PlayerMovement _movement;
    private BattleState _state = BattleState.Idle;
    private Monster _target;
    private float _attackTimer;
    private float _lootTimer;

    public BattleState State => _state;
    public Monster CurrentTarget => _target;
    public bool AutoBattleEnabled => autoBattleEnabled;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        if (stats == null) stats = GetComponent<CharacterStats>();
        if (manualInput == null) manualInput = GetComponent<PlayerInputHandler>();
        if (spawner == null) spawner = FindFirstObjectByType<MonsterSpawner>();
        if (wallet == null) wallet = FindFirstObjectByType<PlayerWallet>();
        if (damageTextSpawner == null) damageTextSpawner = FindFirstObjectByType<DamageTextSpawner>();
    }

    private void OnEnable()
    {
        ApplyAutoBattleToInput();
    }

    /// <summary>자동전투 on/off 토글. 수동 조작 테스트에 사용.</summary>
    public void SetAutoBattle(bool enabled)
    {
        autoBattleEnabled = enabled;
        ApplyAutoBattleToInput();
        if (!autoBattleEnabled)
        {
            _movement.MoveHorizontal(0f);
            _state = BattleState.Idle;
            _target = null;
        }
    }

    private void ApplyAutoBattleToInput()
    {
        if (manualInput != null)
        {
            // 자동전투가 켜져 있으면 수동 입력은 끈다(둘 다 MoveHorizontal 을 호출하므로 충돌 방지)
            manualInput.enabled = !autoBattleEnabled;
        }
    }

    private void Update()
    {
        if (!autoBattleEnabled)
        {
            return;
        }

        switch (_state)
        {
            case BattleState.Idle:
                TickIdle();
                break;
            case BattleState.Move:
                TickMove();
                break;
            case BattleState.Attack:
                TickAttack();
                break;
            case BattleState.Loot:
                TickLoot();
                break;
        }
    }

    // Idle: 살아있는 몬스터 탐색
    private void TickIdle()
    {
        _movement.MoveHorizontal(0f);

        _target = spawner != null ? spawner.GetNearestAlive(transform.position) : null;
        if (_target != null)
        {
            _state = BattleState.Move;
        }
    }

    // Move: 가장 가까운 몬스터로 접근(필요 시 점프/드롭다운)
    private void TickMove()
    {
        if (!IsTargetValid())
        {
            GoIdle();
            return;
        }

        Vector2 me = transform.position;
        Vector2 to = _target.transform.position;
        float dx = to.x - me.x;
        float dy = to.y - me.y;

        if (Mathf.Abs(dx) <= attackRange && Mathf.Abs(dy) <= verticalReachThreshold)
        {
            _movement.MoveHorizontal(0f);
            _attackTimer = 0f;
            _state = BattleState.Attack;
            return;
        }

        bool targetLevel = Mathf.Abs(dy) <= verticalReachThreshold;

        // 수평 접근. 목표가 같은 높이면 사거리 근처에서 멈춰 떨림을 막고,
        // 목표가 위/아래에 있으면 목표 X 바로 아래에 서도록 끝까지 붙는다(발판 진입용).
        float horizontalDeadzone = targetLevel ? attackRange * 0.6f : 0.15f;
        _movement.MoveHorizontal(Mathf.Abs(dx) > horizontalDeadzone ? Mathf.Sign(dx) : 0f);

        // 수직 접근 휴리스틱 (기획서 4.3: 위면 점프, 아래(원웨이)면 드롭다운).
        // 목표 X 에 거의 붙었을 때만 수직 이동을 시도한다.
        if (_movement.IsGrounded && Mathf.Abs(dx) <= attackRange)
        {
            if (dy > verticalReachThreshold)
            {
                _movement.Jump();
            }
            else if (dy < -verticalReachThreshold)
            {
                _movement.DropDown();
            }
        }
    }

    // Attack: 사거리 안에서 일정 주기로 데미지 파이프라인 호출
    private void TickAttack()
    {
        if (!IsTargetValid())
        {
            GoIdle();
            return;
        }

        _movement.MoveHorizontal(0f);

        Vector2 me = transform.position;
        Vector2 to = _target.transform.position;
        if (Mathf.Abs(to.x - me.x) > attackRange + 0.4f ||
            Mathf.Abs(to.y - me.y) > verticalReachThreshold + 0.4f)
        {
            _state = BattleState.Move;
            return;
        }

        // 목표를 바라보게
        _movement.MoveHorizontal(0f);

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            _attackTimer = attackInterval;
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        DamageResult damage = DamageCalculator.Calculate(
            stats != null ? stats.AttackPower : 10f,
            _target.Defense,
            stats != null ? stats.CritRate : 0f);

        _target.TakeDamage(damage.Amount);

        if (damageTextSpawner != null)
        {
            damageTextSpawner.Spawn(_target.transform.position + Vector3.up * 0.7f, damage.Amount, damage.IsCritical);
        }

        if (!_target.IsAlive)
        {
            _lootTimer = lootDuration;
            _state = BattleState.Loot;
        }
    }

    // Loot: 처치 보상 획득 후 Idle 로
    private void TickLoot()
    {
        _movement.MoveHorizontal(0f);

        _lootTimer -= Time.deltaTime;
        if (_lootTimer <= 0f)
        {
            if (_target != null && wallet != null)
            {
                wallet.AddKillReward(_target.ExpReward, _target.GoldReward);
            }
            GoIdle();
        }
    }

    private bool IsTargetValid()
    {
        return _target != null && _target.IsAlive && _target.isActiveAndEnabled;
    }

    private void GoIdle()
    {
        _target = null;
        _state = BattleState.Idle;
        _movement.MoveHorizontal(0f);
    }
}
