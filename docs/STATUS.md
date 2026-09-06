# STATUS — 어디까지 했고 다음은 무엇인가

<!-- 한 바퀴마다 루프가 갱신한다. 기억이 대화가 아니라 이 파일로 이어진다.
     맨 위 '다음 할 일'을 최신으로 유지하고, 아래 기록에 바퀴별로 추가한다. -->

## 다음 할 일

1단계(플레이어 컨트롤러 + 타일맵)를 슬라이스로 쪼갬. 위에서부터 하나씩:

- [x] 1단계-1: PlayerMovement 이동 API (좌우/점프/드롭다운) + PlayMode 테스트  → 커밋 05520f4
- [ ] 1단계-2: PlayerInputHandler (새 Input System → PlayerMovement API 호출). InputSystem_Actions 의 Player 맵 Move/Jump 사용
- [ ] 1단계-3: 플레이 가능한 Game 씬 — 바닥 + 플레이어(스프라이트/Rigidbody2D/Collider/groundCheck) 배치, 플레이모드로 실행해 스크린샷 확인
- [ ] 1단계-4: 타일맵 레벨 — 기본 지형 + 공중 일반 발판 2~3개 + 원웨이 플랫폼(PlatformEffector2D). `[바닥]-[원웨이]-[공중발판]-[원웨이]-[바닥]` 구성
- [ ] 1단계-5: 카메라 추적(간단한 X축 제한 또는 Confiner)

그 다음: 2단계 오프라인 방치 보상 → 3단계 자동전투 FSM → 4단계 스탯/데미지 파이프라인 (DESIGN.md 8장 순서)

## 알려진 문제 / 막힌 것

- (없음)

---

## 기록 (최신이 위)

<!-- 형식:
### YYYY-MM-DD 바퀴 #N
- 한 일:
- 확인한 것 (스크린샷/실행 결과):
- 커밋:
- 다음 할 일:
-->

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
