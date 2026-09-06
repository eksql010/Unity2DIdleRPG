# STATUS — 어디까지 했고 다음은 무엇인가

<!-- 한 바퀴마다 루프가 갱신한다. 기억이 대화가 아니라 이 파일로 이어진다.
     맨 위 '다음 할 일'을 최신으로 유지하고, 아래 기록에 바퀴별로 추가한다. -->

## 다음 할 일

**1단계 완료. 2단계 전부 완료(2-1·2-2·2-3).** 이제 3단계 자동전투 FSM 으로 진행.

3단계: 자동전투 상태머신 (DESIGN.md 4장). Idle→Move→Attack→Loot→Idle 순환.
2.4 의 IPlayerMotor(MoveHorizontal/Jump/DropDown)를 그대로 호출. 슬라이스 제안:
- [ ] 3단계-1: 몬스터 스포너 + MonsterData/몬스터 오브젝트(HP만) + Object Pool.
  스포너가 살아있는 몬스터 리스트를 관리(FindObjectsOfType 매프레임 금지, DESIGN 4.2/4.4).
- [ ] 3단계-2: AutoBattleFsm — Idle(가장 가까운 살아있는 몬스터 탐색)→Move(IPlayerMotor
  로 접근, 위=점프/아래 원웨이=드롭다운 휴리스틱, DESIGN 4.3)→Attack(사거리 진입 시).
- [ ] 3단계-3: Loot(처치 후 골드/exp 획득) + 플레이모드에서 실제로 순환 도는지 스크린샷.
그 다음: 4단계 스탯/데미지 파이프라인 (5장) — 3단계 Attack 에서 사용.

### 2단계 슬라이스 (전부 완료)
- [x] 2단계-1: OfflineRewardCalculator — 순수 계산 클래스(경과시간 → exp/gold),
  최대 캡(8h), 시간 되돌리기 방어. EditMode 테스트로 검증.  → 커밋 eb7f4f4
- [x] 2단계-2: 종료시각 저장/복원 + 재접속 시 계산 호출. OfflineRewardService
  (계산기 + IOfflineTimeStore + IOfflineClock 조합), PlayerPrefsOfflineTimeStore
  (ISO8601 "o" 문자열, UTC 왕복). EditMode 테스트로 검증.  → 커밋 45ab163
- [x] 2단계-3: OfflineRewardController(MonoBehaviour) + Game 씬 Overlay Canvas UI
  (상태 텍스트 + Login/Logout 버튼 + 보상 팝업). Start=재접속 보상 계산·표시,
  Logout=MarkSeen, Login=ClaimOnReconnect, OnApplicationPause/Quit 도 저장.
  FormatRewardMessage/HasReward 는 순수 static → EditMode 검증. 플레이모드 스크린샷 확인.
  → 커밋 345deba

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

### 2026-09-07 바퀴 #8
- 한 일: 2단계-3 슬라이스 — `Assets/Scripts/Offline/OfflineRewardController.cs` 신규(MonoBehaviour).
  Awake 에서 OfflineRewardCalculator(인스펙터 튜닝값 killsPerMinute/expPerKill/goldPerKill/
  maxRewardHours) + PlayerPrefsOfflineTimeStore 로 OfflineRewardService 구성.
  Start = 재접속: ClaimOnReconnect → 지급할 보상이 있으면 팝업 표시. Logout 버튼 = MarkSeen +
  팝업 숨김, Login 버튼 = ClaimOnReconnect + 팝업 표시. OnApplicationPause(true)/OnApplicationQuit
  에서도 MarkSeen. `HasReward`(null/rejected/획득 0 → false) 와 `FormatRewardMessage`(경과 시간 +
  획득 경험치/골드, 캡 도달 시 안내 덧붙임) 를 public static 순수 함수로 분리.
  `Game.unity` 에 execute_code(codedom, C# 6 — 루트에서 `using` 불가, System.Func 람다로 헬퍼
  구성)로 Screen Space Overlay Canvas + InputSystemUIInputModule EventSystem + 상단 상태 텍스트 +
  하단 Login/Logout 버튼 + 보상 팝업(제목 "돌아오신 것을 환영합니다" / 본문 / "닫기" 버튼) 배선.
  폰트는 LegacyRuntime.ttf(빌트인). SerializedObject 로 컨트롤러 6개 참조 주입 후 씬 저장.
- 확인한 것: EditMode 테스트 `Assets/Tests/EditMode/OfflineRewardControllerTests.cs` 5종 신설
  (null→보상없음 / rejected→보상없음 / 획득 0→보상없음 & HasReward false / 정상→경과·획득량 포함 /
  캡→"최대 인정 시간에 도달" 포함). EditMode 25/25, PlayMode 14/14 통과. 콘솔 CS 에러 0
  (RelayService 경고는 무관).
  플레이모드 검증(에디터 포커스 상태라 이번엔 프레임이 진행됨, frameCount 수천):
  · PlayerPrefs 에 3시간 12분 전 UTC 시각을 심고 진입 → Start 에서 팝업 자동 표시,
    본문 "오프라인 보상 (경과 3시간 12분 18초) / 획득 경험치 115389 / 획득 골드 57694",
    상태 "온라인 — 마지막 접속 시각 기록됨". 스크린샷 `Assets/Screenshots/offline_2-3_popup.png`
    직접 확인 — 팝업/버튼/텍스트 정상 렌더, 한글 표시됨.
  · 닫기 버튼 → 팝업 비활성. Logout 버튼 → 상태 "오프라인 — 로그아웃함". 직후 Login →
    경과 ~0 → 본문 "쌓인 오프라인 보상이 없습니다."(획득 0 처리 확인).
- 커밋: 345deba (컨트롤러 + EditMode 테스트 + Game.unity), STATUS 갱신은 별도 커밋.
  주의: ProjectSettings/EditorSettings.asset 의 m_EnterPlayModeOptions 1→0 변화는 이번 작업과
  무관(플레이모드 진입 부수효과)이라 커밋에 포함하지 않음.
- 다음 할 일: 3단계-1 (몬스터 스포너 + 몬스터 오브젝트 + Object Pool).

### 2026-09-07 바퀴 #7
- 한 일: 2단계-2 슬라이스 — `Assets/Scripts/Offline/OfflineRewardService.cs` 신규.
  · `IOfflineClock` / `SystemOfflineClock` — 현재 UTC 시각 주입용 추상화.
  · `IOfflineTimeStore` / `PlayerPrefsOfflineTimeStore` — 마지막 접속 시각 저장/복원.
    ISO 8601 왕복 형식("o", 항상 'Z')으로 PlayerPrefs 에 문자열 저장, 읽을 때
    RoundtripKind 파싱 → Kind=Utc. 손상된 값은 null(저장된 적 없음)로 취급. 키 기본값
    "offline.lastSeenUtc", 생성자로 교체 가능.
  · `OfflineRewardService` — 계산기 + 저장소 + 시계 조합(모두 생성자 주입, 시계 생략 시
    SystemOfflineClock). `MarkSeen()` = 현재 UTC 저장(종료/일시정지용).
    `ClaimOnReconnect()` = 저장된 시각 없으면 계산 없이 현재만 기록 후 null 반환;
    있으면 Calculate(lastSeen, now) 후 rejected 아니면 기준 시각을 now 로 갱신
    (같은 구간 두 번 보상 방지, 시간 되돌리기 시엔 기준을 과거로 밀지 않아 단조 증가 유지).
    `HasLastSeen` 프로퍼티. 생성자는 null 계산기/저장소를 ArgumentNullException 으로 거부.
  MonoBehaviour 아님 → Unity 수명주기 훅(OnApplicationPause/Quit)과 팝업 UI 는 2단계-3.
- 확인한 것: EditMode 테스트 `Assets/Tests/EditMode/OfflineRewardServiceTests.cs` 11종
  신설 — 서비스 7종(첫 실행 기록만 / 경과 비례 보상 / 수령 후 기준 갱신→재수령 rejected /
  시간 되돌리기 rejected & 기준 안 밀림 / 캡 서비스 경로 적용 / MarkSeen 현재시각 저장 /
  null 인자 거부), 저장소 4종(Local→UTC 왕복 & Kind=Utc / 미저장 시 null / 손상값 null /
  Clear 후 null). FakeClock·InMemoryTimeStore 페이크 주입, 저장소 테스트는 전용 키
  "test.offline.lastSeenUtc" + SetUp/TearDown 에서 삭제. EditMode 20/20, PlayMode 14/14 통과.
  콘솔 CS 에러 0. 화면 없는 서비스 로직이라 스크린샷 불필요(스크린샷은 2단계-3).
- 커밋: 45ab163 (서비스 + 저장소 + EditMode 테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 2단계-3 (보상 팝업 UI + Login/Logout 토글 버튼, 플레이모드 스크린샷).

### 2026-09-07 바퀴 #6
- 한 일: 2단계-1 슬라이스 — `Assets/Scripts/Offline/OfflineRewardCalculator.cs` 신규.
  MonoBehaviour 아님, DateTime(lastSeen, now) 를 인자로 받는 순수 계산 클래스.
  `Calculate` → `OfflineRewardResult{ elapsedSeconds, rawElapsedSeconds, gainedExp,
  gainedGold, capped, rejected }`. 공식(기획서 6.1): (killsPerMinute/60) × 경과초 ×
  expPerKill/goldPerKill, 정수 내림. 캡(기획서 6.2): 기본 8h(28800s) 초과 시 캡 값으로
  절단 + capped=true. 시간 되돌리기 방어: now<=lastSeen 이면 rejected=true, 보상 0.
  생성자에서 음수 설정값은 0 으로, 캡 인자 0 이하는 기본 8h 로 클램프.
- 확인한 것: EditMode 테스트 `Assets/Tests/EditMode/OfflineRewardCalculatorTests.cs`
  9종 신설(비례 지급 / 2배 시간→2배 보상 / 캡 절단 / 캡 경계 직전 / 과거 시각 rejected /
  경과 0 / 정수 내림 / 음수 설정값 방어 / 캡 인자 0→기본값). EditMode asmdef 신규
  (`Game.Tests.EditMode`, Editor 전용). EditMode 9/9 + 기존 PlayMode 14/14 통과.
  콘솔 CS 에러 0 (RelayService 경고는 무관). 화면 없는 순수 로직이라 스크린샷 불필요.
- 커밋: eb7f4f4 (계산 클래스 + EditMode 테스트 + asmdef), STATUS 갱신은 별도 커밋.
- 다음 할 일: 2단계-2 (종료시각 저장/복원 + 재접속 시 계산 호출, UTC 기준).

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
