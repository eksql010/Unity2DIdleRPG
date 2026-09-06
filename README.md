# Unity2DIdleRPG

## 자율 개발 루프

한 바퀴마다 **새 헤드리스 세션**을 열어 `loop/PROMPT.md` 를 주고 일을 시킨다.
대화는 이어 붙이지 않는다. 기억은 `docs/` 안의 파일로만 이어진다.

### 구성 파일

| 파일 | 역할 |
|---|---|
| `loop/loop.ps1` | 루프 본체. 무한 반복, 한 바퀴마다 새 세션. |
| `loop/env.ps1` | 설정 (모델 / 한 바퀴 최대 턴 수 / 바퀴 사이 대기 / 최대 바퀴 수 / 권한 모드 / PATH). |
| `loop/PROMPT.md` | 세션에 주는 지시서 (6절). ①②③은 사람이 채운다. |
| `loop/loopctl.ps1` | 제어 스크립트 (install / uninstall / start / stop / status). |
| `loop/STOP` | 이 파일이 있으면 현재 바퀴를 마치고 멈춘다 (git 미추적). |
| `docs/DESIGN.md` | 무엇을 만드는가 (기획서, 거의 안 고침). |
| `docs/STATUS.md` | 어디까지 했고 다음은 뭔가 (한 바퀴마다 갱신). |
| `docs/feedback/INBOX.md` | 사람이 던지는 지시 (루프가 가장 먼저 처리). |
| `logs/loop_YYYY-MM-DD.log` | 날짜별 실행 로그 (git 미추적). |

### 자동 실행 등록 (작업 스케줄러)

```powershell
.\loop\loopctl.ps1 install
```

- 로그인하면 자동 시작
- 비정상 종료 시 1분 뒤 재시작
- 정상 종료(STOP)면 재시작하지 않음
- PATH 는 `loop/env.ps1` 의 `$LOOP_PATH` 에 명시 (자동 실행은 터미널 환경을 물려받지 않음)

`install` 은 등록만 한다. 아직 돌지 않는다.

### 켜기 / 끄기 / 상태 보기

```powershell
.\loop\loopctl.ps1 start
```

```powershell
.\loop\loopctl.ps1 stop
```

```powershell
.\loop\loopctl.ps1 status
```

- `stop` 은 `loop/STOP` 파일을 만든다. **현재 바퀴는 끝까지 돈 뒤** 멈춘다 (작업 유실 방지).
- 당장 죽여야 하면: `.\loop\loopctl.ps1 stop -Force`
- `start` 는 `loop/STOP` 을 지우고 태스크를 실행한다.

### 등록 해제

```powershell
.\loop\loopctl.ps1 uninstall
```

### 두 바퀴만 수동으로 돌려보기 (등록 없이)

한 바퀴 최대 턴 수를 정하기 전에, 로그를 보고 감을 잡는 용도.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\loop\loop.ps1
```

`loop/env.ps1` 에서 `$LOOP_MAX_ROUNDS = 2` 로 두면 두 바퀴 후 정상 종료한다.
로그: `logs/loop_YYYY-MM-DD.log`, 턴 수: `research-logs/loop.csv` 의 `turn_count`.

### 주의

- `loop/loop.ps1` 을 터미널에서 직접 실행하면 무한 루프라 그 창에 묶인다.
  창을 닫으면 같이 죽는다. 오래 돌릴 거면 `loopctl.ps1 install` + `start` 를 써라.
- `$LOOP_PERMISSION_MODE` 기본값은 `bypassPermissions` 다. 헤드리스 세션은
  권한 프롬프트에 사람이 답할 수 없어서 그렇다. 이 저장소 안에서만 동작한다는 전제다.
