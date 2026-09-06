# loop/env.ps1
# 자율 개발 루프 설정. loop.ps1 이 실행 시작 시 이 파일을 dot-source 한다.
# 값만 바꾸면 되고, 로직은 loop.ps1 에 있다.

# ── 튜닝 노브 4개 ────────────────────────────────────────────────
# 모델: 한 바퀴(헤드리스 세션)가 쓸 모델
$LOOP_MODEL = "claude-sonnet-5"

# 한 바퀴 최대 턴 수: 세션이 이 턴 수를 넘으면 강제 종료된다.
#   감으로 넣지 말 것. 두 바퀴 이상 돌린 로그(research-logs/loop.csv 의 turn_count)를
#   보고 정하라. 너무 작으면 작업이 잘리고, 너무 크면 폭주한다.
$LOOP_MAX_TURNS = 40

# 바퀴 사이 대기(초): 한 바퀴가 끝나고 다음 바퀴를 열기 전 쉬는 시간.
$LOOP_SLEEP_SEC = 30

# 최대 바퀴 수: 이 횟수만큼 돌면 정상 종료한다. 0 = 무제한.
$LOOP_MAX_ROUNDS = 0

# ── 권한 모드 ───────────────────────────────────────────────────
# 헤드리스 세션은 사람이 권한 프롬프트에 답할 수 없다.
#   acceptEdits       : 파일 편집만 자동 승인. bash(git 커밋 등)는 실패 → 루프가 아무것도 커밋 못 함.
#   bypassPermissions : 전부 자동 승인. 자율 루프를 실제로 굴리려면 이게 필요하다. (기본값)
# 위험을 이해하고 쓸 것. 루프는 이 저장소 안에서만 동작한다는 전제다.
$LOOP_PERMISSION_MODE = "bypassPermissions"

# ── PATH (자동 실행 대비) ──────────────────────────────────────
# 작업 스케줄러로 뜬 프로세스는 평소 터미널 PATH 를 물려받지 않는다.
# 여기서 명시하지 않으면 claude / git / node 를 못 찾고 조용히 죽는다.
$LOOP_PATH = @(
    "C:\Users\dleks\.local\bin"                                  # claude.exe
    "C:\Program Files\nodejs"                                    # node
    "C:\Program Files\Git\bin"                                   # git.exe, bash.exe
    "C:\Program Files\Git\usr\bin"                               # coreutils (date 등)
    "C:\Users\dleks\AppData\Local\Microsoft\WinGet\Packages\jqlang.jq_Microsoft.Winget.Source_8wekyb3d8bbwe"  # jq.exe (Stop 훅용)
    "C:\WINDOWS\system32"
    "C:\WINDOWS"
    "C:\WINDOWS\System32\WindowsPowerShell\v1.0"
) -join ";"

# ── 경로 상수 (건드릴 일 없음) ─────────────────────────────────
$LOOP_REPO   = Split-Path -Parent $PSScriptRoot   # 저장소 루트
$LOOP_PROMPT = Join-Path $PSScriptRoot "PROMPT.md"
$LOOP_STOP   = Join-Path $PSScriptRoot "STOP"
$LOOP_LOGDIR = Join-Path $LOOP_REPO   "logs"
$LOOP_TASK_NAME = "ClaudeDevLoop"
