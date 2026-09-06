# STATUS — 어디까지 했고 다음은 무엇인가

<!-- 한 바퀴마다 루프가 갱신한다. 기억이 대화가 아니라 이 파일로 이어진다.
     맨 위 '다음 할 일'을 최신으로 유지하고, 아래 기록에 바퀴별로 추가한다. -->

## 다음 할 일

**1단계 완료. 2단계 전부 완료. 3단계 전부 완료. 4단계-1 완료. 4단계-2 완료.**
다음은 **4단계-3**: `AutoBattleFsm.attackDamage`(고정값) 와 `Monster.TakeDamage` 앞단을
`DamageCalculator` 로 교체 + 플레이어/몬스터에 `StatContainer` 부착 + 데미지 텍스트(플레이모드 스크린샷).
· FSM 에 `DamageCalculator`(런타임은 `new DamageCalculator()` = 난수 판정기) + 플레이어 `StatContainer`
  주입. Attack 상태에서 `calc.Calculate(playerStats, target.Stats)` → `target.TakeDamage(result.Damage)`.
· `Monster` 에 `StatContainer`(스폰 시 `StatContainer.ForMonster(Data)`) 노출 — 현재 `Monster` 는 HP만
  들고 있으므로 방어력을 계산에 넣으려면 스탯 컨테이너를 붙여야 한다.
· 데미지 텍스트: 크리티컬 여부(`DamageResult.IsCrit`)에 따라 색/크기 구분, Object Pooling(DESIGN 4.4).
  플레이모드 스크린샷으로 몬스터 위에 데미지 숫자가 뜨는지 직접 확인.
· 4단계 완료 시 DESIGN 9장 체크리스트 3번("처치 시 스탯 파이프라인 기반 데미지 계산") 충족.

### 4단계 슬라이스
- [x] 4단계-2: `Assets/Scripts/Combat/DamageCalculator.cs` 신규 1파일 — 데미지 공식(DESIGN 5.3).
  · `ICritChanceRoller.Roll(prob)` — 크리티컬 판정 추상화. `UnityCritChanceRoller`(런타임, `Random.value`,
    prob≤0 항상 false / prob≥1 항상 true). 테스트는 `FixedRoller` 페이크 주입 → 결정적.
  · `DamageResult` — `Damage`(최종) / `IsCrit` / `BaseDamage`(방어력만 반영, 배율 전).
  · `DamageCalculator(critRoller=null, minimumDamage=1)` — `Calculate(attacker, target, extraMultiplier=1)`.
    `afterDefense = max(attacker.AttackPower.Value - target.Defense.Value, minimumDamage)`,
    `isCrit = critRate>0 && roller.Roll(critRate)` (CritRate 0 이면 판정기 호출 안 함),
    `Damage = afterDefense × (isCrit ? 1.5 : 1) × max(extraMultiplier, 0)`.
    attacker null → ArgumentNullException, target null → 방어력 0, minimumDamage 음수 → 0 클램프.
  · EditMode `Assets/Tests/EditMode/DamageCalculatorTests.cs` 17종.
  → 커밋 67fffa8 (STATUS 갱신은 별도 커밋)
- [x] 4단계-1: `Assets/Scripts/Stats/` 신규 3파일 — 레이어드 스탯 + Dirty Flag.
  · `StatModifier` — 불변 값 객체. `StatModifierType{Flat=100, PercentAdd=200, PercentMultiply=300}`,
    `Value`/`Type`/`Source`(출처, 일괄 제거용). `StatModifier.Flat/PercentAdd/PercentMultiply` 팩토리.
    알 수 없는 종류는 생성자에서 ArgumentException.
  · `Stat` — `BaseValue`(같은 값 재대입은 무시) + `List<StatModifier>` + `_isDirty`/`_cachedValue`.
    `Value` getter 는 Dirty 일 때만 `CalculateFinalValue()` 하고 `RecalculationCount++`(테스트용).
    적용 순서: 기본값 → Flat 합산 → PercentAdd 전부 합산 후 한 번 곱 → PercentMultiply 순차 곱.
    `AddModifier`(null 거부)/`RemoveModifier`/`RemoveAllModifiersFromSource`/`ClearModifiers`.
  · `StatContainer` — `StatType{AttackPower,Defense,MaxHP,CritRate,MoveSpeed}` (DESIGN 5.2 최소 세트).
    5개 `Stat` 프로퍼티 + `Get(StatType)` + `ForMonster(MonsterData)`(5.4 재사용, Sanitize 경유).
  · EditMode `Assets/Tests/EditMode/StatSystemTests.cs` 23종.
  → 커밋 72f98b6 (STATUS 갱신은 별도 커밋)

3단계: 자동전투 상태머신 (DESIGN.md 4장). Idle→Move→Attack→Loot→Idle 순환.
2.4 의 IPlayerMotor(MoveHorizontal/Jump/DropDown)를 그대로 호출. 슬라이스:
- [x] 3단계-1: 몬스터 스포너 + MonsterData + Monster(HP만) + MonsterPool.  → 커밋 9c60a36
- [x] 3단계-2a: AutoBattleFsm — Idle(GetNearestAliveMonster 로 타겟)→Move(IPlayerMotor 로 접근,
  위=Jump/아래=DropDown 휴리스틱 DESIGN 4.3)→Attack(사거리 진입 시 attackInterval 주기 TakeDamage)
  →Loot(통과 상태, 처치 수만 집계)→Idle. Tick(dt) 주입식, PlayMode 10종.  → 커밋 30f22a5
- [x] 3단계-2b: Game.unity 의 Player 에 AutoBattleFsm 부착 + MonsterSpawner GameObject + 스폰 지점
  4개(x -9/-5/5/9, y -2.85) 배치. AutoBattleFsm 실행순서 100(PlayerInputHandler 뒤) — 자동전투가
  기본 조작이므로 AI 가 매 프레임 마지막에 이동을 덮어씀. 둘 다 같은 PlayerMovement 공유 확인
  (GetComponent<IPlayerMotor>() == Player 의 PlayerMovement, sharedMotor=True).  → 커밋 8aa556b
- [x] 3단계-3: Loot 에서 골드/exp 실제 지급. `PlayerWallet`(골드/경험치 누적 + Changed 이벤트),
  `LootCollector`(AutoBattleFsm.MonsterKilled 구독 → 죽은 몬스터의 goldReward/expReward 를 지갑에),
  `WalletHud`(지갑 Changed → 화면 우상단 Text 갱신). FSM 은 지갑을 모르게 두어 3단계-2 테스트
  그대로 통과. PlayMode 8종 신설. 플레이모드에서 HUD 가 골드+5/경험치+10 씩 실시간 누적 확인.
  → 커밋 2eab21a(스크립트+테스트) / 65237ad(Game.unity 배선)
그 다음: 4단계 스탯/데미지 파이프라인 (5장) — 3단계 Attack 에서 사용.

### 3단계 슬라이스
- [x] 3단계-1: `Assets/Scripts/Combat/` 신규 4파일.
  · `MonsterData` — 몬스터 1종 정적 데이터(monsterId/hp/attackPower/defense/expReward/goldReward),
    `[Serializable]`, `Sanitized()` 로 음수 등 오입력 보정. (공격력/방어력은 4단계 전까지 미사용)
  · `Monster` (MonoBehaviour) — HP만. `Spawn(data,pos)` 풀에서 꺼내 재사용, `TakeDamage(amount)`
    (3단계는 방어력/크리티컬 보정 없이 그대로 차감 → 4단계에서 앞단에 데미지 공식 붙임),
    사망 시 `Died` 이벤트 1회, `Despawn()` 로 비활성. 스스로 Destroy 안 함.
  · `MonsterPool` — `Func<Monster>` 팩토리 주입 순수 클래스(프리팹 없이 테스트 가능).
    prewarm/Rent/Return, 중복 반납 무시, TotalCreated/IdleCount 노출.
  · `MonsterSpawner` (MonoBehaviour) — `AliveMonsters`(IReadOnlyList) 직접 관리(FindObjectsOfType
    안 씀, DESIGN 4.2). 풀 재사용, 사망 시 리스트 제거 + 반납 + `respawnDelay` 후 보충
    (`TickRespawn(dt)` 는 Update 가 호출, 테스트에서 직접 주입 가능). `GetNearestAliveMonster(from)`
    = 가장 가까운 살아있는 몬스터(DESIGN 4.3 타겟 선정). 프리팹 없으면 빨간 사각형 플레이스홀더
    (SpriteRenderer + isTrigger BoxCollider2D) 를 코드로 생성. `Configure()` 로 런타임 튜닝.
  · PlayMode 테스트 `Assets/Tests/PlayMode/MonsterSpawnerTests.cs` 15종
    (Monster 5: Spawn 초기화 / 체력차감·0클램프·Died 1회 / 0이하 무시 / 사망후 재사용 / 오입력 보정.
     Pool 4: 재사용 시 재생성 안 함 / prewarm 비활성 대기 / 중복반납 무시 / null 팩토리 거부.
     Spawner 6: FillToCapacity=maxAlive / SpawnOne 초과 시 null / 사망→리스트 제거·반납 /
     지연 후 풀 재사용 리스폰(PoolSize 불변) / GetNearest 최근접 / 죽은놈 스킵·전멸 시 null).
  → 커밋 9c60a36 (STATUS 갱신은 별도 커밋)

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

- 3단계-3 관찰: OfflineRewardController.Start 가 저장된 마지막 접속 시각 기준으로 보상 팝업을
  자동으로 띄워, 플레이모드 진입 직후 스크린샷이 팝업에 가려진다. 전투 화면을 보려면 팝업을
  닫아야 한다(RewardPopup.SetActive(false)). 지갑 HUD 는 팝업과 무관하게 우상단에 항상 보임.
- 3단계-3: 지갑은 세션 내 누적만 한다(저장/복원 없음). 오프라인 보상(6장)의 exp/gold 는 여전히
  팝업 표시만 하고 PlayerWallet 에 반영되지 않는다 — 둘을 잇는 것은 MVP 범위 밖(필요 시 별도 슬라이스).
- (해결) 3단계-2b 에서 Game.unity 에 MonsterSpawner + 스폰 지점 + Player 에 AutoBattleFsm 배선 완료.
- 3단계-2b 스폰 지점은 전부 지면 높이(y -2.85)라 FSM 이 Jump()/DropDown() 을 호출하지 않는다
  (dy ≈ -0.5 < verticalThreshold 0.75). 다층 이동 검증은 상단 발판 추가(아래 항목) 후 별도 슬라이스로.
- 3단계-2b 관찰: FSM 이 좌측 클러스터(x -9/-5)에 오래 머문다 — 리스폰이 그쪽에서 먼저 차서
  항상 최근접이 됨. 순회 자체는 정상(플레이어 X 가 -8~8 전 구간 왕복하는 것 확인).
- 레벨은 지면↔발판 1층 구조뿐(상/하 이동은 점프 1회 + 드롭다운 1회로 커버). 3단계
  자동전투 FSM 에서 다층 경로가 필요해지면 상단 발판을 추가할 것. (이번 바퀴에 상단
  발판을 시도했으나, 캐릭터 높이 1.6 + 발판 간격 ~2u 라 아래 발판이 위 발판으로의
  점프를 막아 제거함. 추가하려면 수평 오프셋을 두거나 위 발판도 원웨이로 만들 것.)

---

## 기록 (최신이 위)

### 2026-09-07 바퀴 #14
- 한 일: 4단계-2 슬라이스 — 데미지 계산 파이프라인(DESIGN 5.3). `Assets/Scripts/Combat/DamageCalculator.cs`
  신규 1파일(`ICritChanceRoller` + `UnityCritChanceRoller` + `DamageResult` + `DamageCalculator`).
  MonoBehaviour 아닌 순수 클래스. 공식: `(공격력 - 방어력) × 크리티컬(1.5) × 기타 배율`, 입력은
  4단계-1 `StatContainer` 의 최종값(`Stat.Value`, 수정자 반영). 크리티컬 판정을 `ICritChanceRoller`
  로 분리 주입 → 런타임은 난수, 테스트는 페이크로 결정적. 방어력≥공격력이어도 `minimumDamage`(기본 1)
  로 최소 데미지 보장(교착 방지). CritRate 0 이면 판정기 미호출. 자세한 내용은 위 "4단계 슬라이스" 참고.
  FSM/Monster 배선·데미지 텍스트는 이 슬라이스 밖(4단계-3).
- 확인한 것: EditMode 65/65(기존 48 + DamageCalculatorTests 17), PlayMode 47/47(무수정 통과).
  콘솔 CS/게임플레이 에러 0 (TestResults.xml 저장 로그 / connection.state_change 는 무관).
  신규 17종: 기본 공식 4종(방어력 뺄셈 / 크리 1.5배 / 기타 배율 / 크리+기타 동시 곱),
  최소 데미지 3종(방어 우세 시 1 보장 / 생성자 변경 / 음수 → 0), 크리 확률 전달 3종
  (CritRate 를 판정기에 전달·RollCount / CritRate 0 이면 미호출 / 수정자 반영된 확률),
  방어적 3종(피격자 null → 방어력 0 / 공격자 null → 예외 / 음수 기타배율 → 0),
  스탯 연동 2종(공격력·방어력 수정자 최종값 / ForMonster 로 양방향 계산),
  난수 판정기 경계 2종(확률 1·0 / BaseDamage vs Damage).
  화면 없는 순수 로직이라 스크린샷 불필요(스크린샷은 4단계-3).
- 커밋: 67fffa8 (DamageCalculator + EditMode 테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 4단계-3 (FSM/Monster 에 DamageCalculator + StatContainer 배선 + 데미지 텍스트,
  플레이모드 스크린샷). 완료 시 DESIGN 9장 체크리스트 3번 충족.

### 2026-09-07 바퀴 #13
- 한 일: 4단계-1 슬라이스 — 레이어드 스탯 + Dirty Flag. `Assets/Scripts/Stats/` 신규 3파일
  (`StatModifier` / `Stat` / `StatContainer`). 순수 클래스라 씬/플레이모드 불필요.
  Dirty Flag: `BaseValue` 변경·수정자 추가/제거 시에만 `_isDirty=true`, `Value` getter 가
  Dirty 일 때만 재계산(기획서 5.1 "매 프레임 재계산 금지"). 같은 값 재대입/없는 수정자 제거는
  Dirty 로 만들지 않음. 적용 순서 Flat → PercentAdd(합산 후 1회 곱) → PercentMultiply(순차 곱).
  출처(Source)로 수정자 일괄 제거 지원(버프/장비 해제). `StatContainer.ForMonster(MonsterData)`
  로 플레이어/몬스터가 같은 시스템 재사용(5.4). 자세한 내용은 위 "4단계 슬라이스" 참고.
  DamageCalculator·FSM 배선은 이 슬라이스 밖(4단계-2/3).
- 확인한 것: EditMode 48/48(기존 25 + StatSystemTests 23), PlayMode 47/47(무수정 통과).
  콘솔 CS/게임플레이 에러 0 (TestResults.xml 저장 로그 / connection.state_change 는 무관).
  신규 23종: 기본값/Flat/PercentAdd 합산/PercentMultiply 순차/3종 혼합 순서,
  Dirty Flag 5종(반복 읽기 재계산 안 함 / BaseValue 변경 시 1회 / 동일 값 무시 / 수정자 추가 시 /
  없는 수정자 제거는 Dirty 아님), 수정자 제거 5종(인스턴스/출처 일괄/ClearModifiers/없는 출처 false),
  방어 3종(null 수정자 거부 / 미정의 종류 거부 / Modifiers 읽기전용), StatContainer 6종
  (5개 초기화 / Get / ForMonster 이관·Sanitize·null / 이동속도 버프 부착·제거 왕복).
  화면 없는 순수 로직이라 스크린샷 불필요(스크린샷은 4단계-3).
- 커밋: 72f98b6 (Stats 3파일 + EditMode 테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 4단계-2 (DamageCalculator — 데미지 공식 5.3, 크리티컬 판정 주입식, EditMode).

### 2026-09-07 바퀴 #12
- 한 일: 3단계-3 슬라이스 — Loot 단계 보상 지급. 신규 3파일.
  · `Assets/Scripts/Combat/PlayerWallet.cs` (MonoBehaviour) — long Gold/Exp 누적,
    AddGold/AddExp(0 이하 무시), ResetWallet, Changed 이벤트. 저장/복원 없음(세션 내 누적만).
  · `Assets/Scripts/Combat/LootCollector.cs` (MonoBehaviour) — OnEnable/OnDisable 에서
    `AutoBattleFsm.MonsterKilled` 구독/해제. 처치 시 죽은 몬스터의 Data.goldReward/expReward 를
    지갑에 넣고 LootedCount++. `Configure(fsm, wallet)` 주입식. FSM 은 지갑을 모른다(의존성 분리)
    → AutoBattleFsm/3단계-2 테스트 무수정 통과.
  · `Assets/Scripts/UI/WalletHud.cs` (MonoBehaviour) — 지갑 Changed 구독, "골드 N    경험치 N"
    포맷으로 Text 갱신(매 프레임 폴링 안 함). `Configure(wallet, label)` 주입식.
  Game.unity 배선: Player 에 위 3컴포넌트 부착 + 상호 참조 주입, UI Canvas 상단 우측에
  WalletText(LegacyRuntime.ttf, 노란색, UpperRight) 추가. execute_code(codedom) + SerializedObject.
- 확인한 것: EditMode 25/25, PlayMode 47/47(기존 39 + LootRewardTests 8종). 콘솔 CS/게임플레이
  에러 0 (RelayService/connection.state_change 는 무관).
  신규 테스트: Wallet 3(누적+Changed / 0이하 무시 / ResetWallet), LootCollector 4(처치→보상 지급 /
  다마리 누적 / 비활성화 시 미수령 / 지갑 null 이어도 크래시 없이 LootedCount), WalletHud 1(변경 시
  라벨 텍스트 갱신). 실제 MonsterSpawner+Monster+FakeMotor 로 FSM 을 돌려 통합 검증.
  플레이모드(에디터 포커스, frame ~8천): FSM 이 Idle→Move→Attack→Loot 순환하며 처치, KillCount 와
  LootedCount 가 동일하게 증가(9→15→16), wallet gold/exp = kills×(5/10) 정확히 일치,
  HUD 우상단이 "골드 80    경험치 160" 으로 실시간 갱신됨. PoolSize 는 3단계-2b 확인대로 고정.
  스크린샷 2장 직접 확인:
  · `Assets/Screenshots/loot_3-3.png` — 진입 직후(오프라인 보상 팝업 떠 있음), 우상단 HUD "골드 50 경험치 100".
  · `Assets/Screenshots/loot_3-3_combat.png` — 팝업 닫은 전투 화면. 노란 플레이어가 빨간 몬스터
    2마리 사이, 시안 원웨이/회색 공중발판/녹색 지면/하단 Login·Logout, 우상단 HUD "골드 80 경험치 160".
- 커밋: 2eab21a (PlayerWallet+LootCollector+WalletHud + LootRewardTests),
  65237ad (Game.unity 배선 — 커밋 메시지에 "3단계-2b" 로 오기했으나 실제로는 3단계-3).
  STATUS 갱신은 별도 커밋. EditorSettings.asset 의 EnterPlayModeOptions 변화는 플레이모드 부수효과라 제외.
- 다음 할 일: 4단계-1 (StatContainer + Modifier 순수 클래스 + Dirty Flag, EditMode 테스트).

### 2026-09-07 바퀴 #11
- 한 일: 3단계-2b 슬라이스 — 로직만 있던 스포너/FSM 을 Game.unity 에 처음 배선.
  · `MonsterSpawner` GameObject 를 (0,-2.85) 에 생성, 자식 스폰 지점 4개(x -9/-5/5/9, y -2.85).
    SerializedObject 로 spawnPoints 배열 주입. monsterData 는 필드 기본값(hp 30) 그대로,
    maxAlive 4 / respawnDelay 3 / prewarm 4 / spawnOnStart true.
  · Player 에 `AutoBattleFsm` 부착, playerMovement=Player 의 PlayerMovement, spawner=위 스포너 주입.
  · `MonoImporter.SetExecutionOrder(AutoBattleFsm, 100)` — PlayerInputHandler(0) 보다 뒤에 돌게 해서
    자동전투가 매 프레임 마지막에 MoveHorizontal 을 덮어쓰도록(기획서 1: "이번 범위에서는 자동전투가
    기본"). 실행순서는 AutoBattleFsm.cs.meta 에 저장됨. 둘 다 같은 IPlayerMotor(PlayerMovement)
    공유 확인(sharedMotor=True).
  execute_code(codedom) 로 결정적 배선 후 씬 저장.
- 확인한 것: EditMode 25/25, PlayMode 39/39 통과. 콘솔 CS/게임플레이 에러 0
  (RelayService TaskCanceled 경고·connection.state_change 는 무관).
  플레이모드(에디터 포커스 상태라 프레임 진행됨, frameCount ~1만):
  · FSM 이 Idle→Move→Attack→Loot→Idle 순환하며 몬스터를 처치. KillCount 가 10→16→21 로 계속 증가,
    PoolSize 는 4 고정(Instantiate/Destroy 반복 없음 — 기획서 4.4 풀링 확인).
  · 스폰 지점 4곳에 몬스터 스폰, 사망 후 respawnDelay 지나 풀에서 재사용 리스폰.
  · 플레이어 X 가 -8~8 전 구간 왕복(카메라도 따라 이동), 몬스터 사거리 진입 시 정지 후 공격.
  · 스크린샷 `Assets/Screenshots/combat_3-2b_a.png` / `combat_3-2b_b.png` 직접 확인 —
    노란 플레이어가 빨간 몬스터에 붙어 공격, 시안 원웨이/회색 공중발판/녹색 지면/하단 Login·Logout
    UI 정상 렌더. 카메라가 플레이어를 따라 좌측으로 이동한 것도 두 장 비교로 확인.
- 커밋: 8aa556b (Game.unity + AutoBattleFsm.cs.meta 실행순서), STATUS 갱신은 별도 커밋.
- 다음 할 일: 3단계-3 (Loot 에서 골드/exp 실제 지급 + 플레이모드 순환 스크린샷).

### 2026-09-07 바퀴 #10
- 한 일: 3단계-2a 슬라이스 — `Assets/Scripts/Combat/AutoBattleFsm.cs` 신규(MonoBehaviour).
  상태 enum Idle/Move/Attack/Loot. `Tick(float dt)` 를 Update 가 호출하고 테스트는 직접 주입.
  · Idle: `spawner.GetNearestAliveMonster(transform.position)` 로 타겟 → 있으면 Move.
  · Move: 타겟과 수평 거리 > horizontalStopDistance 면 그쪽으로 `MoveHorizontal(±1)`, 사거리
    안이면 정지 후 Attack. 타겟이 verticalThreshold 이상 위면 `Jump()`, 이상 아래면 `DropDown()`
    — 조건 없이 호출만 하고 성립 판정은 PlayerMovement 에 위임(수동 입력과 동일 방식, DESIGN 2.4).
  · Attack: 사거리*1.25 밖이면 Move 로 복귀, 아니면 정지 후 attackInterval 주기로
    `Monster.TakeDamage(attackDamage)`(고정값 — 4단계에서 스탯 파이프라인으로 대체).
  · Loot: 프레임에 머무르지 않는 통과 상태. 진입 시 KillCount++ 와 `MonsterKilled` 이벤트
    (3단계-3 보상 지급이 소비), 그 Tick 안에서 곧바로 Idle 로 돌아가 다음 타겟 탐색.
  타겟이 죽거나(우리 공격이든 외부든) 풀 반납되면 Tick 서두에서 감지해 Loot 경유 복귀.
  `Configure(IPlayerMotor, MonsterSpawner)` / `Tune(range, interval, damage, vThreshold)` 주입식.
- 확인한 것: PlayMode 테스트 `Assets/Tests/PlayMode/AutoBattleFsmTests.cs` 10종 신설 —
  가짜 IPlayerMotor(이동 기록) + 실제 MonsterSpawner/Monster. (Idle 타겟팅·무몬스터 정지 /
  Move 수평방향·위Jump·아래DropDown·사거리진입 Attack전이 / Attack 주기 데미지·처치 시
  KillCount·이벤트·Idle복귀·사거리이탈 재추격 / 타겟 소멸 시 크래시 없이 복귀 / 의존성 없으면 무동작).
  EditMode 25/25, PlayMode 39/39(기존 29 + 신규 10) 통과. 콘솔 CS 에러 0
  (TestResults.xml 저장 로그 / connection.state_change 는 무관).
  화면 배선은 이 슬라이스 밖 → 스크린샷은 3단계-2b 로 분리(3단계-1 이 스크린샷을 미룬 것과 동일).
- 커밋: 30f22a5 (AutoBattleFsm + PlayMode 테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 3단계-2b (Game.unity 에 AutoBattleFsm + MonsterSpawner 배치, 플레이모드 스크린샷).

### 2026-09-07 바퀴 #9
- 한 일: 3단계-1 슬라이스 — `Assets/Scripts/Combat/` 신규(MonsterData / Monster / MonsterPool /
  MonsterSpawner). 몬스터는 HP만 가지며 스스로 Destroy 하지 않고 풀로 재사용된다. 스포너는
  살아있는 몬스터를 `AliveMonsters` 리스트로 직접 들고 있어 FindObjectsOfType 매프레임 탐색이
  필요 없다(DESIGN 4.2/4.4). 사망 시 리스트 제거→풀 반납→`respawnDelay` 후 보충. 타겟 선정
  `GetNearestAliveMonster(from)` 제공(3단계-2 Move 상태용). 프리팹 없으면 빨간 사각형
  플레이스홀더를 코드로 생성. 자세한 내용은 위 "3단계 슬라이스" 참고.
- 확인한 것: PlayMode 테스트 `MonsterSpawnerTests.cs` 15종 신설 — EditMode 25/25,
  PlayMode 29/29(기존 14 + 신규 15) 통과. 콘솔 CS 에러 0 (RelayService 경고는 무관).
  화면 배선(씬에 스포너 배치)은 이번 슬라이스 범위 밖 → 스크린샷은 3단계-2 로 미룸.
  단 PlayMode 테스트가 실제 SpriteRenderer + BoxCollider2D 를 붙인 플레이스홀더 생성 경로를
  타므로 오브젝트 구성 자체는 검증됨.
- 커밋: 9c60a36 (Combat 4파일 + PlayMode 테스트), STATUS 갱신은 별도 커밋.
- 다음 할 일: 3단계-2 (AutoBattleFsm — Idle→Move→Attack, IPlayerMotor 활용) +
  Game.unity 에 MonsterSpawner 배치 후 플레이모드 스크린샷.

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
