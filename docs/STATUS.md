# STATUS — 어디까지 했고 다음은 무엇인가

<!-- 한 바퀴마다 루프가 갱신한다. 기억이 대화가 아니라 이 파일로 이어진다.
     맨 위 '다음 할 일'을 최신으로 유지하고, 아래 기록에 바퀴별로 추가한다. -->

## 다음 할 일

**1단계(플레이어 컨트롤러 + 타일맵) 완료.** 이제 2단계로 진행.

2단계: 오프라인 방치 보상 (DESIGN.md 6장). 코드량 적고 독립적. 슬라이스 후보:
- [ ] 2단계-1: OfflineRewardCalculator — 순수 계산 클래스(경과시간 → exp/gold),
  최대 캡(8h), 시간 되돌리기 방어. EditMode 테스트로 검증(씬 불필요).
- [ ] 2단계-2: 종료시각 저장/복원 (PlayerPrefs 또는 JSON), 재접속 시 계산 호출.
- [ ] 2단계-3: 보상 팝업 UI + Login/Logout 토글 버튼(DESIGN 6.3), 플레이모드 스크린샷.

그 다음: 3단계 자동전투 FSM → 4단계 스탯/데미지 파이프라인 (DESIGN.md 8장 순서)

### 1단계 슬라이스 (전부 완료)
- [x] 1단계-1: PlayerMovement 이동 API (좌우/점프/드롭다운) + PlayMode 테스트  → 커밋 05520f4
- [x] 1단계-2: PlayerInputHandler (새 Input System → IPlayerMotor API 호출)  → 커밋 9ae7298
- [x] 1단계-3: 플레이 가능한 Game 씬 — 바닥 + 플레이어 배치  → 커밋 52a1516
- [x] 1단계-4: 레벨 레이아웃 — 바닥 + 원웨이 발판 A/B + 일반 공중발판 1개  → 커밋 e89f2ab
- [x] 1단계-5: 카메라 추적 — 플레이어 X 따라가되 스테이지 좌우 경계에서 X 제한  → 커밋 d71d86c

## 알려진 문제 / 막힌 것

- 레벨은 지면↔발판 1층 구조뿐(상/하 이동은 점프 1회 + 드롭다운 1회로 커버). 3단계
  자동전투 FSM 에서 다층 경로가 필요해지면 상단 발판을 추가할 것. (이번 바퀴에 상단
  발판을 시도했으나, 캐릭터 높이 1.6 + 발판 간격 ~2u 라 아래 발판이 위 발판으로의
  점프를 막아 제거함. 추가하려면 수평 오프셋을 두거나 위 발판도 원웨이로 만들 것.)

---

## 기록 (최신이 위)

### 2026-09-07 바퀴 #5
- 한 일: 1단계-5 슬라이스 — 카메라 추적. `Assets/Scripts/Camera/CameraFollow.cs`
  신규(Game asmdef). LateUpdate 에서 target.x 를 minX/maxX 로 Clamp → SmoothDamp(smoothTime
  0.15) 로 수렴, Y 는 fixedY(0) 고정(스테이지 세로로 짧음, 기획서 3.4 "간단한 X축 제한").
  Cinemachine 미사용. SetTarget/SetBounds public 메서드 노출(씬 로드 후 스포너용).
  minX>maxX(스테이지가 화면보다 좁음) 이면 중앙 고정하도록 방어.
  Game.unity Main Camera 에 부착: target=Player, X 제한 ±11.1
  (바닥 폭 40 → x -20..20, ortho size 5, 16:9 반너비 ≈ 8.89 → 20-8.89 ≈ 11.1).
- 확인한 것: PlayMode 테스트 `Assets/Tests/PlayMode/CameraFollowTests.cs` 6종 신규
  (경계 안 추적 / 오른쪽·왼쪽 clamp / Y·Z 고정 / null target 무동작 / SetBounds 인자뒤집힘).
  기존 8종 포함 PlayMode 14/14 통과. 콘솔 CS 에러 0.
  스크린샷 2장 직접 확인(에디터 비포커스라 LateUpdate 를 리플렉션으로 직접 호출해 검증):
  · `Assets/Screenshots/level_1-5_camera_center.png` — player.x=0 → cam.x=0, 플레이어가
    화면 중앙, 3발판이 한 줄로, 지면이 하단을 채움.
  · `Assets/Screenshots/level_1-5_camera_right_edge.png` — player.x=25 → cam.x=11.10 에서
    멈춤. 오른쪽 끝에도 검은 여백 없이 지면이 화면 폭을 채움(경계 clamp 정상).
  · 계산 로그: x=0→cam 0, x=25→cam 11.10, x=-25→cam -11.10, x=7→cam 7.
- 커밋: d71d86c (CameraFollow + 테스트 + Game.unity), STATUS 갱신은 별도 커밋.
- 다음 할 일: 2단계-1 (OfflineRewardCalculator 순수 계산 클래스 + EditMode 테스트).


<!-- 형식:
### YYYY-MM-DD 바퀴 #N
- 한 일:
- 확인한 것 (스크린샷/실행 결과):
- 커밋:
- 다음 할 일:
-->

### 2026-09-07 바퀴 #4
- 한 일: 1단계-4 슬라이스 — Game.unity 에 레벨 레이아웃 추가. `Level` 부모 아래
  OneWayPlatform_A(-6,-1.2) / AirPlatform(0,-1.2) / OneWayPlatform_B(6,-1.2), 셋 다
  worldSize (4, 0.3). A·B 는 OneWayPlatform 레이어 + PlatformEffector2D(useOneWay) +
  BoxCollider2D.usedByEffector, AirPlatform 은 Ground 레이어 일반 발판(양면 충돌).
  플레이스홀더 스프라이트 white.png, 원웨이=시안 / 일반=회색. 발판 간격 2u(점프로 건너뜀).
  Player 씬 인스턴스 jumpForce 8→13 (지면 top -3.25 에서 발판 top -1.05 로 오르려면 필요.
  apex ≈ 13²/(2·9.81·3) ≈ 2.87u. PlayMode 테스트는 자체 주입값이라 무관).
  execute_code(C#) 로 결정적 생성 후 씬 저장.
- 확인한 것: 플레이모드 + Physics2D.simulationMode=Script 로 수동 검증
  (주의: Physics2D.Simulate 는 MonoBehaviour.FixedUpdate 를 호출하지 않으므로
  PlayerMovement 로직 대신 rb 속도를 직접 넣어 지오메트리만 검증).
  · 지면 x=-6 에서 점프(v=13) → peak y 0.31, 원웨이 발판 A 위 안착(중심 y -0.24 ≈ 기대 -0.25).
    아래→위 원웨이 통과 후 착지 확인.
  · 발판 A 위에서 드롭다운(콜라이더 무시 + 아래 속도) → y -0.24 → -2.44, 지면으로 낙하.
  · 지면에서 x=-10 → 5.25 까지 이동, 발판 아래로 끼임 없이 통행(y -2.44 유지).
  스크린샷 `Assets/Screenshots/level_1-4.png` 직접 확인 — 지면 위 플레이어, 그 위로
  시안-회색-시안 3발판이 한 줄로 배치됨. 콘솔 CS 에러 0 (RelayService 경고는 무관).
  EditMode 0/0, PlayMode 8/8 통과.
- 커밋: e89f2ab (Game.unity), STATUS 갱신은 별도 커밋.
- 다음 할 일: 1단계-5 (카메라 추적 — 플레이어 X 를 따라가되 스테이지 경계에서 X축 제한).

### 2026-09-07 바퀴 #3
- 한 일: 1단계-3 슬라이스 — 플레이 가능한 Game 씬 최초 구성.
  `Assets/Scenes/Game.unity` 신규(2D URP, 빌드 인덱스 0). Ground(SpriteRenderer+BoxCollider2D,
  Ground 레이어, 폭 40u) + Player(SpriteRenderer + Rigidbody2D[회전잠금/Continuous/gravityScale 3]
  + BoxCollider2D + 자식 GroundCheck) 배치. Player 에 PlayerMovement / PlayerInputHandler 부착하고
  직렬화 필드 주입: groundCheck=자식 Transform, groundLayer=Ground|OneWayPlatform,
  oneWayLayer=OneWayPlatform, inputActions=Assets/InputSystem_Actions.inputactions.
  Global Light 2D + 정사영 카메라(size 5). 플레이스홀더 스프라이트 `Assets/Art/white.png`(4x4, PPU 4).
  씬 구성은 execute_code(C#)로 결정적으로 생성. QA 스크린샷은 `Assets/Screenshots/`(gitignore).
- 확인한 것: 플레이모드 진입 후 Physics2D 스크립트 시뮬레이션으로 프레임을 진행시켜
  플레이어가 낙하 → 바닥에 안착(playerBottom -3.235 ≈ groundTop -3.25, vel 0). 스크린샷으로
  플레이어가 바닥 위에 서 있고 지면이 화면 하단을 채우는 것 확인. 콘솔 CS 에러 0
  (RelayService/NoSubscription 경고는 Unity AI 관련, 이번 작업 무관). PlayMode 8/8 통과.
  주의: 에디터 비포커스 시 플레이모드 게임 루프가 거의 진행되지 않음 → 물리 검증은
  Physics2D.simulationMode=Script + Simulate() 루프로 수동 진행해야 함.
- 커밋: 52a1516 (씬+스프라이트+빌드세팅+gitignore), STATUS 갱신은 별도 커밋.
- 다음 할 일: 1단계-4 (타일맵 레벨 — 기본 지형 + 공중 일반 발판 2~3개 + 원웨이 플랫폼).

### 2026-09-07 바퀴 #2
- 한 일: 1단계-2 슬라이스 — 수동 입력을 이동 API 로 번역하는 계층.
  `Assets/Scripts/Player/IPlayerMotor.cs`(MoveHorizontal/Jump/DropDown 계약),
  `PlayerMovement` 가 이 인터페이스를 구현하도록 수정,
  `Assets/Scripts/Player/PlayerInputHandler.cs` 신규 — InputSystem_Actions 의 Player 맵에서
  Move(Vector2)/Jump(Button) 을 읽어 IPlayerMotor 호출. Jump 시점에 Move.y < -0.5(아래 누름)면
  DropDown, 아니면 Jump 로 분기(기획서 2.3). inputActions 는 SerializeField 로 애셋 주입.
  자동전투 FSM 은 이 핸들러를 거치지 않고 같은 IPlayerMotor 를 직접 호출하는 구조(기획서 2.4).
- 확인한 것: PlayMode 테스트 `Assets/Tests/PlayMode/PlayerInputHandlerTests.cs` 3종 신규
  (우이동→MoveHorizontal 양수 & 키 뗌→0, Space→Jump 1회·DropDown 0, 아래+Space→DropDown 1회·Jump 0),
  InputTestFixture 로 가상 키보드 구동. 기존 5종 포함 PlayMode 8/8 통과. 콘솔 CS 에러 0
  (NoSubscription 등 Unity AI 경고는 이번 작업과 무관). 화면 있는 씬은 1단계-3에서.
  test asmdef 에 `Unity.InputSystem.TestFramework` 참조 추가.
- 커밋: 9ae7298 (인터페이스+핸들러+테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 1단계-3 (플레이 가능한 Game 씬 — 바닥+플레이어 배치, 플레이모드 스크린샷 확인).

### 2026-09-07 바퀴 #1
- 한 일: 빈 프로젝트(loop 브랜치)에서 시작. 기획서 2장 이동 로직의 첫 슬라이스 구현 —
  `Assets/Scripts/Game.asmdef`, `Assets/Scripts/Player/PlayerMovement.cs`.
  MoveHorizontal / Jump / DropDown / IsGrounded / FacingDirection / SetMoveSpeed 를 public API 로 노출해
  수동 입력·자동전투 FSM 이 공유하도록 설계(2.4). 접지는 Physics2D.OverlapCircle, 점프는 AddForce(Impulse),
  드롭다운은 Physics2D.IgnoreCollision 을 dropDownDuration(0.3초) 동안 적용. Ground(slot 8)/OneWayPlatform(slot 9) 레이어 추가.
- 확인한 것: PlayMode 테스트 `Assets/Tests/PlayMode/PlayerMovementTests.cs` 5/5 통과
  (IsGrounded, 좌우 이동 속도/방향, 정지, 점프+공중 재점프 차단, 원웨이 플랫폼 드롭다운).
  콘솔 에러 0 (Unity AI 구독 관련 경고 2건은 이번 작업과 무관).
  화면 있는 씬은 아직 없음 → 1단계-3에서 스크린샷 확인 예정.
- 커밋: 05520f4 (기능+테스트), STATUS 갱신은 별도 커밋
- 다음 할 일: 위 "다음 할 일"의 1단계-2 (PlayerInputHandler)
