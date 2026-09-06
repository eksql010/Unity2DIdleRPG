<#
loop/loopctl.ps1 — 자율 개발 루프 제어

  .\loop\loopctl.ps1 install     작업 스케줄러에 등록 (로그인 시 시작 / 비정상 종료 시 재시작)
  .\loop\loopctl.ps1 uninstall   등록 해제
  .\loop\loopctl.ps1 start       지금 켜기 (STOP 파일 제거 후 태스크 실행)
  .\loop\loopctl.ps1 stop        끄기 (STOP 파일 생성 → 현재 바퀴까지 마치고 멈춤)
  .\loop\loopctl.ps1 status      상태 보기 (태스크 상태 + STOP 유무 + 최근 로그)

'stop' 은 STOP 파일만 만든다. 현재 바퀴는 끝까지 돈 뒤 멈춘다 (작업 유실 방지).
당장 죽여야 하면: .\loop\loopctl.ps1 stop -Force
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet("install", "uninstall", "start", "stop", "status")]
    [string]$Command,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "env.ps1")

$loopScript = Join-Path $PSScriptRoot "loop.ps1"
$psExe      = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"

function Cmd-Install {
    $action = New-ScheduledTaskAction `
        -Execute $psExe `
        -Argument "-NoProfile -ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -File `"$loopScript`"" `
        -WorkingDirectory $LOOP_REPO

    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

    # 비정상 종료(exit != 0) 시 1분 뒤 재시작, 사실상 무제한.
    # loop.ps1 은 STOP 으로 멈추면 exit 0 → 재시작 안 함 ("정상 종료면 그대로 둔다").
    # ExecutionTimeLimit 0 = 시간 제한 없음 (무한 루프이므로 필수).
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -MultipleInstances IgnoreNew

    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

    Register-ScheduledTask -TaskName $LOOP_TASK_NAME `
        -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
        -Description "자율 개발 루프 (loop/loop.ps1). 제어: loop/loopctl.ps1" `
        -Force | Out-Null

    Write-Host "등록 완료: '$LOOP_TASK_NAME'"
    Write-Host "  - 로그인하면 자동 시작"
    Write-Host "  - 비정상 종료 시 1분 뒤 재시작"
    Write-Host "  - 지금은 아직 안 돌고 있음. 켜려면:  .\loop\loopctl.ps1 start"
}

function Cmd-Uninstall {
    if (Get-ScheduledTask -TaskName $LOOP_TASK_NAME -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $LOOP_TASK_NAME -Confirm:$false
        Write-Host "등록 해제 완료: '$LOOP_TASK_NAME'"
    } else {
        Write-Host "등록된 태스크 없음."
    }
}

function Cmd-Start {
    if (-not (Get-ScheduledTask -TaskName $LOOP_TASK_NAME -ErrorAction SilentlyContinue)) {
        Write-Host "먼저 등록하라:  .\loop\loopctl.ps1 install"; return
    }
    if (Test-Path $LOOP_STOP) { Remove-Item $LOOP_STOP -Force; Write-Host "STOP 파일 제거." }
    Start-ScheduledTask -TaskName $LOOP_TASK_NAME
    Write-Host "루프 시작됨. 상태:  .\loop\loopctl.ps1 status"
}

function Cmd-Stop {
    New-Item -ItemType File -Force -Path $LOOP_STOP | Out-Null
    Write-Host "STOP 파일 생성. 현재 바퀴를 마치면 멈춘다."
    if ($Force) {
        if (Get-ScheduledTask -TaskName $LOOP_TASK_NAME -ErrorAction SilentlyContinue) {
            Stop-ScheduledTask -TaskName $LOOP_TASK_NAME
        }
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -match '-File' -and $_.CommandLine -match 'loop[\\/]loop\.ps1' } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
        Write-Host "실행 중이던 loop.ps1 프로세스 강제 종료."
    }
}

function Cmd-Status {
    $task = Get-ScheduledTask -TaskName $LOOP_TASK_NAME -ErrorAction SilentlyContinue
    if ($task) {
        $info = Get-ScheduledTaskInfo -TaskName $LOOP_TASK_NAME
        Write-Host "태스크        : $LOOP_TASK_NAME"
        Write-Host "  State       : $($task.State)"
        Write-Host "  LastRunTime : $($info.LastRunTime)"
        Write-Host "  LastResult  : $($info.LastTaskResult)"
        Write-Host "  NextRunTime : $($info.NextRunTime)"
    } else {
        Write-Host "태스크        : 미등록"
    }
    Write-Host "STOP 파일     : $(if (Test-Path $LOOP_STOP) { '있음 (멈추는 중/멈춤)' } else { '없음' })"

    $running = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -match '-File' -and $_.CommandLine -match 'loop[\\/]loop\.ps1' }
    Write-Host "loop.ps1 PID  : $(if ($running) { ($running.ProcessId -join ', ') } else { '없음' })"

    $latest = Get-ChildItem $LOOP_LOGDIR -Filter "loop_*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Host "`n최근 로그 ($($latest.Name)) 마지막 20줄:"
        Write-Host ("─" * 60)
        Get-Content $latest.FullName -Tail 20
    } else {
        Write-Host "`n로그 없음 (아직 한 바퀴도 안 돎)."
    }
}

switch ($Command) {
    "install"   { Cmd-Install }
    "uninstall" { Cmd-Uninstall }
    "start"     { Cmd-Start }
    "stop"      { Cmd-Stop }
    "status"    { Cmd-Status }
}
