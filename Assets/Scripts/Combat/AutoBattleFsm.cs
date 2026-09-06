using System;
using UnityEngine;

/// <summary>
/// 자동전투 상태머신. (기획서 4장)
///
///   Idle → Move → Attack → Loot → Idle  (순환)
///
/// - Idle:   <see cref="MonsterSpawner.GetNearestAliveMonster"/> 로 살아있는 가장 가까운 몬스터를 탐색.
/// - Move:   타겟 방향으로 <see cref="IPlayerMotor"/> 의 공유 이동 API 를 호출해 접근.
///           타겟이 위에 있으면 Jump(), 아래에 있으면 DropDown() (기획서 4.3 단순 휴리스틱).
///           점프/드롭다운의 실제 성립 여부(접지·원웨이 판정)는 <see cref="PlayerMovement"/> 가 판단하므로
///           FSM 은 조건 없이 호출만 한다 — 수동 입력 핸들러와 완전히 같은 방식. (기획서 2.4)
/// - Attack: 사거리 안이면 제자리 정지 후 <see cref="attackInterval"/> 주기로 공격.
///           데미지는 <see cref="DamageCalculator"/>(플레이어 <see cref="StatContainer"/> vs 몬스터 스탯,
///           기획서 5.3 공식)로 계산해 <see cref="Monster.TakeDamage"/> 에 넘긴다.
///           파이프라인이 배선되지 않았으면 <see cref="attackDamage"/> 고정값으로 폴백한다.
/// - Loot:   처치 보상 획득. 3단계-2 범위에서는 처치 수만 세고 즉시 Idle 로 돌아간다.
///           골드/경험치 지급은 3단계-3.
///
/// 이동을 "명령하는 쪽"일 뿐이라 <see cref="PlayerInputHandler"/> 와 형제 관계다(둘 다 IPlayerMotor 호출자).
/// </summary>
[DisallowMultipleComponent]
public class AutoBattleFsm : MonoBehaviour
{
    /// <summary>FSM 상태. (기획서 4.1)</summary>
    public enum State
    {
        Idle,
        Move,
        Attack,
        Loot,
    }

    [Header("참조")]
    [Tooltip("이동을 실제로 수행하는 컴포넌트. 보통 같은 Player 오브젝트의 PlayerMovement.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("타겟 후보(살아있는 몬스터 리스트)를 제공하는 스포너.")]
    [SerializeField] private MonsterSpawner spawner;

    [Header("전투 튜닝")]
    [Tooltip("이 거리 안에 들어오면 이동을 멈추고 Attack 상태로 전환한다.")]
    [SerializeField] private float attackRange = 1.2f;

    [Tooltip("공격 1회 사이의 간격(초).")]
    [SerializeField] private float attackInterval = 0.6f;

    [Tooltip("공격 1회의 데미지. 데미지 파이프라인(아래)이 배선되지 않았을 때만 쓰는 폴백 고정값.")]
    [SerializeField] private float attackDamage = 7f;

    [Header("데미지 파이프라인 (기획서 5장)")]
    [Tooltip("플레이어 기본 공격력. Awake 에서 이 값으로 StatContainer 를 만들어 데미지 공식에 넣는다.")]
    [SerializeField] private float basePlayerAttackPower = 12f;

    [Tooltip("플레이어 기본 크리티컬 확률(0~1). 공격마다 이 확률로 크리티컬(1.5배) 판정. (기획서 5.3)")]
    [SerializeField] private float basePlayerCritRate = 0.2f;

    [Tooltip("플레이어 기본 방어력(몬스터가 플레이어를 때릴 때 대비 — MVP 범위에선 표시용).")]
    [SerializeField] private float basePlayerDefense = 3f;

    [Header("이동 튜닝")]
    [Tooltip("타겟과의 수평 거리가 이 값보다 크면 그쪽으로 걷는다(작으면 정지 — 좌우 떨림 방지).")]
    [SerializeField] private float horizontalStopDistance = 0.1f;

    [Tooltip("타겟이 나보다 이 값 이상 높으면 Jump(), 이 값 이상 낮으면 DropDown() 을 호출한다. (기획서 4.3)")]
    [SerializeField] private float verticalThreshold = 0.75f;

    private IPlayerMotor _motor;
    private Monster _target;
    private float _attackTimer;
    private int _killCount;

    private StatContainer _playerStats;
    private DamageCalculator _damageCalculator;

    /// <summary>현재 FSM 상태(인스펙터/테스트 확인용).</summary>
    public State CurrentState { get; private set; } = State.Idle;

    /// <summary>현재 추적 중인 타겟. 없으면 null.</summary>
    public Monster Target => _target;

    /// <summary>이 FSM 이 처치한 몬스터 누적 수.</summary>
    public int KillCount => _killCount;

    /// <summary>몬스터를 처치한 순간 1회 발생. 인자는 방금 죽은 몬스터(보상 데이터 포함). 3단계-3 Loot 에서 소비.</summary>
    public event Action<Monster> MonsterKilled;

    /// <summary>
    /// 공격이 몬스터에 명중한 순간 발생. 인자는 (맞은 몬스터, 데미지 계산 결과).
    /// 데미지 텍스트(4단계-3b) 가 이 이벤트를 구독해 몬스터 위에 숫자를 띄운다.
    /// </summary>
    public event Action<Monster, DamageResult> MonsterDamaged;

    /// <summary>데미지 공식에 들어가는 플레이어 스탯. 파이프라인 미배선 시 null.</summary>
    public StatContainer PlayerStats => _playerStats;

    private void Awake()
    {
        if (_motor == null)
        {
            _motor = playerMovement;
        }

        // 실제 씬 배선(playerMovement 가 인스펙터에서 주입됨)일 때만 데미지 파이프라인을
        // 자동 구성한다. 테스트는 Configure()/ConfigureCombat() 로 명시 주입하므로 건드리지 않는다.
        if (playerMovement != null)
        {
            EnsureCombatPipeline();
        }
    }

    private void EnsureCombatPipeline()
    {
        if (_playerStats == null)
        {
            _playerStats = new StatContainer(
                baseAttackPower: basePlayerAttackPower,
                baseDefense: basePlayerDefense,
                baseCritRate: basePlayerCritRate);
        }
        if (_damageCalculator == null)
        {
            _damageCalculator = new DamageCalculator();
        }
    }

    /// <summary>
    /// 테스트/런타임에서 의존성과 튜닝값을 주입한다. 인스펙터 참조 대신 사용할 수 있다.
    /// </summary>
    public void Configure(IPlayerMotor motor, MonsterSpawner monsterSpawner)
    {
        _motor = motor;
        spawner = monsterSpawner;
        ResetState();
    }

    /// <summary>
    /// 데미지 파이프라인(기획서 5장)을 명시 주입한다. 호출하지 않으면 <see cref="attackDamage"/> 고정값을 쓴다.
    /// 크리티컬 난수를 결정적으로 만들려면 <paramref name="calculator"/> 에 페이크 판정기를 넣은
    /// <see cref="DamageCalculator"/> 를 넘긴다.
    /// </summary>
    public void ConfigureCombat(StatContainer playerStats, DamageCalculator calculator)
    {
        _playerStats = playerStats;
        _damageCalculator = calculator ?? new DamageCalculator();
    }

    /// <summary>전투 튜닝값을 한 번에 지정한다(선택).</summary>
    public void Tune(float newAttackRange, float newAttackInterval, float newAttackDamage, float newVerticalThreshold)
    {
        attackRange = Mathf.Max(0.01f, newAttackRange);
        attackInterval = Mathf.Max(0f, newAttackInterval);
        attackDamage = Mathf.Max(0f, newAttackDamage);
        verticalThreshold = Mathf.Max(0.01f, newVerticalThreshold);
    }

    /// <summary>상태를 Idle 로 되돌리고 타겟/타이머를 비운다.</summary>
    public void ResetState()
    {
        CurrentState = State.Idle;
        _target = null;
        _attackTimer = 0f;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// FSM 한 스텝. 매 프레임 <see cref="Update"/> 가 호출하며, 테스트에서 직접 시간 경과를 주입할 수도 있다.
    /// 상태 전이는 매 프레임 무거운 연산 없이 처리한다. (기획서 4.4)
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_motor == null || spawner == null)
        {
            return;
        }

        // 타겟이 죽었거나 풀로 반납(파괴 취급)되면 즉시 Loot 로.
        if (CurrentState != State.Idle && (_target == null || !_target.IsAlive))
        {
            EnterLoot();
        }

        switch (CurrentState)
        {
            case State.Idle:
                TickIdle();
                break;
            case State.Move:
                TickMove();
                break;
            case State.Attack:
                TickAttack(deltaTime);
                break;
        }

        // Loot 은 프레임에 머무르지 않는 통과 상태다. 위에서 어느 경로로 진입했든
        // 같은 Tick 안에서 보상 처리 후 곧바로 Idle 로 되돌리고 다음 타겟을 찾는다.
        if (CurrentState == State.Loot)
        {
            TickLoot();
        }
    }

    // ------------------------------------------------------------------
    // 상태별 처리
    // ------------------------------------------------------------------

    private void TickIdle()
    {
        _motor.MoveHorizontal(0f);

        Monster nearest = spawner.GetNearestAliveMonster(transform.position);
        if (nearest != null)
        {
            _target = nearest;
            CurrentState = State.Move;
        }
    }

    private void TickMove()
    {
        Vector2 self = transform.position;
        Vector2 targetPos = _target.transform.position;

        if (Vector2.Distance(self, targetPos) <= attackRange)
        {
            _motor.MoveHorizontal(0f);
            _attackTimer = 0f; // 사거리에 들어온 즉시 첫 타격
            CurrentState = State.Attack;
            return;
        }

        float dx = targetPos.x - self.x;
        _motor.MoveHorizontal(Mathf.Abs(dx) > horizontalStopDistance ? Mathf.Sign(dx) : 0f);

        float dy = targetPos.y - self.y;
        if (dy > verticalThreshold)
        {
            _motor.Jump();
        }
        else if (dy < -verticalThreshold)
        {
            _motor.DropDown();
        }
    }

    private void TickAttack(float deltaTime)
    {
        Vector2 self = transform.position;
        Vector2 targetPos = _target.transform.position;

        // 사거리를 벗어났으면(몬스터가 멀어졌거나 리스폰으로 위치가 바뀜) 다시 추격.
        if (Vector2.Distance(self, targetPos) > attackRange * 1.25f)
        {
            CurrentState = State.Move;
            return;
        }

        _motor.MoveHorizontal(0f);

        _attackTimer -= deltaTime;
        if (_attackTimer <= 0f)
        {
            _attackTimer = attackInterval;

            DamageResult hit = ComputeDamage();
            MonsterDamaged?.Invoke(_target, hit);

            bool killed = _target.TakeDamage(hit.Damage);
            if (killed)
            {
                // TakeDamage 안에서 Died 가 발생해 스포너가 이미 반납했다.
                // 다음 Tick 서두의 죽음 감지 대신 여기서 바로 Loot 로 넘어간다.
                EnterLoot();
            }
        }
    }

    /// <summary>
    /// 이번 타격의 데미지를 계산한다. 레이어드 스탯 + 데미지 공식(기획서 5.3)이 배선돼 있으면
    /// 그것으로, 아니면 <see cref="attackDamage"/> 고정값으로.
    /// </summary>
    private DamageResult ComputeDamage()
    {
        if (_playerStats != null && _damageCalculator != null)
        {
            StatContainer targetStats = _target != null ? _target.Stats : null;
            return _damageCalculator.Calculate(_playerStats, targetStats);
        }
        return new DamageResult(attackDamage, false, attackDamage);
    }

    private void EnterLoot()
    {
        Monster dead = _target;
        _target = null;
        _attackTimer = 0f;
        CurrentState = State.Loot;

        if (dead != null)
        {
            _killCount++;
            MonsterKilled?.Invoke(dead);
        }
    }

    private void TickLoot()
    {
        // 3단계-2: 보상 지급 없이 곧바로 다음 탐색으로. (골드/경험치 획득은 3단계-3)
        CurrentState = State.Idle;
        TickIdle();
    }
}
