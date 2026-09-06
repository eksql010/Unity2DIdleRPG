# loop/loop.ps1
# 자율 개발 루프 본체.
#
# 핵심: 한 바퀴마다 헤드리스 세션을 "새로" 연다. 대화를 이어 붙이지 않는다.
#       세션이 아는 것은 loop/PROMPT.md 와 거기서 지시한 문서들뿐이다.
#       기억은 대화가 아니라 파일(docs/STATUS.md 등)로만 이어진다.
#
# 이 스크립트를 터미널에서 직접 실행하지 말 것 — 무한 루프라서 그 창에 묶인다.
# loop/loopctl.ps1 로 작업 스케줄러에 등록해서 돌려라. (README.md 참고)

$ErrorActionPreference = "Stop"

# UTF-8 고정. PS 5.1 은 기본 파이프/콘솔 인코딩이 ASCII/ANSI 라
# 한글이 물음표로 깨진다 (claude 출력 로그, 인자 전달 모두).
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding           = [System.Text.UTF8Encoding]::new($false)
$env:PYTHONIOENCODING     = "utf-8"

# ── 설정 로드 ──────────────────────────────────────────────────
. (Join-Path $PSScriptRoot "env.ps1")

$env:Path = $LOOP_PATH          # 자동 실행 환경에 PATH 주입
Set-Location $LOOP_REPO
New-Item -ItemType Directory -Force -Path $LOOP_LOGDIR | Out-Null

function Write-Log {
    param([string]$Message)
    $ts   = Get-Date -Format "yyyy-MM-ddTHH:mm:ssK"
    $file = Join-Path $LOOP_LOGDIR ("loop_{0}.log" -f (Get-Date -Format "yyyy-MM-dd"))
    "[$ts] $Message" | Tee-Object -FilePath $file -Append
}

# ── 사전 점검 ─────────────────────────────────────────────────
$claude = (Get-Command claude -ErrorAction SilentlyContinue)
if (-not $claude) {
    Write-Log "FATAL: claude 를 PATH 에서 못 찾음. loop/env.ps1 의 `$LOOP_PATH 확인."
    exit 1
}
if (-not (Test-Path $LOOP_PROMPT)) {
    Write-Log "FATAL: loop/PROMPT.md 없음."
    exit 1
}

Write-Log "루프 시작. model=$LOOP_MODEL max-turns=$LOOP_MAX_TURNS sleep=${LOOP_SLEEP_SEC}s max-rounds=$LOOP_MAX_ROUNDS perm=$LOOP_PERMISSION_MODE"

# ── 메인 루프 ─────────────────────────────────────────────────
$round = 0
$stopReason = "unknown"

while ($true) {

    # STOP 파일이 있으면 이번 바퀴를 "시작하지 않고" 멈춘다.
    if (Test-Path $LOOP_STOP) {
        $stopReason = "STOP 파일 감지"
        break
    }

    if ($LOOP_MAX_ROUNDS -gt 0 -and $round -ge $LOOP_MAX_ROUNDS) {
        $stopReason = "최대 바퀴 수($LOOP_MAX_ROUNDS) 도달"
        break
    }

    $round++
    $roundLog = Join-Path $LOOP_LOGDIR ("loop_{0}.log" -f (Get-Date -Format "yyyy-MM-dd"))
    Write-Log "───── 바퀴 #$round 시작 ─────"

    # 부트스트랩 프롬프트는 ASCII 로만. 실제 지시는 loop/PROMPT.md 에 있고,
    # claude 가 자기 파일 도구로 (UTF-8 정확히) 읽는다.
    # 프롬프트를 stdin 으로 파이프하면 PS 5.1 인코딩에서 한글이 깨지므로 인자로만 준다.
    $bootstrap = "Read the file loop/PROMPT.md in this repository and do exactly what it says for one round, then stop."

    # 이번 바퀴 전용 세션 ID를 직접 발급 (새 세션인지 나중에 로그로 확실히 검증하기 위함)
    $roundSessionId = [guid]::NewGuid().ToString()
    Write-Log "바퀴 #$round 세션 ID: $roundSessionId"

    # 백틱 줄바꿈 대신 배열(스플래팅)로 인자를 넘긴다 — PS 5.1에서 백틱 뒤 공백 하나로도
    # 줄바꿈이 조용히 깨지는 문제를 구조적으로 피하기 위함.
    $claudeArgs = @(
        "-p", $bootstrap,
        "--model", $LOOP_MODEL,
        "--max-turns", $LOOP_MAX_TURNS,
        "--permission-mode", $LOOP_PERMISSION_MODE,
        "--add-dir", $LOOP_REPO,
        "--session-id", $roundSessionId
    )

    try {
        # 새 헤드리스 세션. --continue / --resume 를 쓰지 않으므로 매번 백지에서 시작한다.
        & claude @claudeArgs 2>&1 | Tee-Object -FilePath $roundLog -Append
        $code = $LASTEXITCODE
        Write-Log "바퀴 #$round 종료 (exit=$code)"
    }
    catch {
        Write-Log "바퀴 #$round 예외: $_"
    }

    # 바퀴가 끝난 뒤 STOP 이 생겼으면 여기서 멈춘다 (현재 바퀴는 이미 마침).
    if (Test-Path $LOOP_STOP) {
        $stopReason = "STOP 파일 감지 (바퀴 종료 후)"
        break
    }

    Write-Log "다음 바퀴까지 ${LOOP_SLEEP_SEC}s 대기"
    Start-Sleep -Seconds $LOOP_SLEEP_SEC
}

Write-Log "루프 정상 종료: $stopReason (총 $round 바퀴)"
exit 0