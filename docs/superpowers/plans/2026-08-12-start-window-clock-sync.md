# 시작 구간 클럭 동기화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치 입장 직후 서버 시간 추정이 자리를 잡을 때까지 기다렸다가 시뮬레이션 시계의 출발선을 그어, 입장 직후 10초 넘게 지속되는 러버밴딩을 없앤다.

**Architecture:** `LOPRoom`에 private 대기 메서드 하나를 추가한다. `drift`(= Mirror `PredictedTime` − `Time.unscaledTimeAsDouble`)를 매 프레임 표본으로 모아, 0.5초 창의 진폭이 5ms 미만이면 추정이 멈춘 것으로 보고 시드한다. 대기는 접속 직후 시작해 `gameInfo` 왕복과 겹쳐 돌리고 시드 직전에 합류한다. 7초 타임아웃 시 현재 값으로 진행한다.

**Tech Stack:** Unity 6000.3.16f1 · C# · Mirror(벤더, `Assets/Mirror`) · UniTask · VContainer

## Global Constraints

- 설계 원본: `docs/superpowers/specs/2026-08-12-start-window-clock-sync-design.md`
- **실시간 기준은 반드시 `Time.unscaledTimeAsDouble`** — Mirror의 `NetworkTime.localTime`이 같은 값이라야 `drift`에서 실시간 항이 상쇄된다. `Time.timeAsDouble`은 `timeScale`에 끌려 상쇄가 깨진다.
- 상수: 창 `0.5초` · 최소 표본 `5` · 진폭 임계 `0.005`(5ms) · 타임아웃 `7초`
- **유닛 테스트를 작성하지 않는다.** 대상이 `Assets/`의 앱 코드이고 이 프로젝트의 두 Unity 앱에는 asmdef가 없어 테스트 어셈블리를 붙일 수 없다(스펙의 의식적 결정). 각 태스크의 검증 사이클은 **UnityMCP 컴파일 확인 + 라이브 측정**이다.
- **UnityMCP 호출에는 항상 `unity_instance`를 명시**한다. 클라 인스턴스 id는 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 항목의 `id`(예: `LeagueOfPhysical-Client@de70658b9450cbb4`, 해시는 바뀔 수 있다).
- 커밋은 `worktree-feature+start-window-clock-sync` 브랜치에서만. `main` 직접 커밋 금지.
- **Unity 에디터는 `main` 체크아웃을 본다.** 워크트리의 `Assets/` 변경은 그 자리에서 컴파일 검증이 불가하므로, 컴파일·라이브 검증은 **`main`에 머지한 뒤** 수행한다(각 태스크 절차에 포함).
- 머지 시 `git merge --no-ff --no-autostash` 를 쓴다. `main` 체크아웃에는 커밋하면 안 되는 측정용 픽스처가 있고, 이 저장소의 stash 스택은 다른 워크트리와 공유된다.

---

### Task 1: 시계 안정 대기 구현 + 배선

**Files:**
- Modify: `Assets/Scripts/Room/LOPRoom.cs` — 신규 private 메서드 추가 + `Awake` 진입 흐름 수정

**Interfaces:**
- Consumes: `runner.networkTime.PredictedTime` (`GameFramework.Netcode.INetworkTime`), `Mirror.NetworkClient.ready`, `destroyCancellationToken` (MonoBehaviour)
- Produces: `private async Task WaitForClockSettleAsync()` — 반환값 없음. 안정 / 타임아웃 / 연결끊김 중 하나로 종료하며 **예외를 던지지 않는다**(취소는 내부에서 흡수). 로그 태그 `[ClockSettle]`

- [ ] **Step 1: 대기 메서드를 추가한다**

`Assets/Scripts/Room/LOPRoom.cs`에서 `public async Task StartGameAsync()` 선언 **바로 앞**에 아래 메서드를 삽입한다.

```csharp
        // 서버 시간 추정이 자리를 잡을 때까지 기다린다.
        //
        // 왜 필요한가: Mirror는 접속 즉시 첫 핑을 보내는데 그 순간이 인증·씬·스폰으로 가장 바쁘다.
        // 실측상 첫 표본만 왕복 1052ms(이후 180~200ms)로 부풀고, Mirror의 평균은 첫 표본을 그대로
        // 채택한 뒤 한 번에 9.5%씩만 교정한다. 그 상태에서 출발선을 그으면 0.7초(35틱) 어긋난 채
        // 시작하고 시계는 초당 5%씩만 따라잡아 10초 넘게 어긋나 있다 — 입장 직후 러버밴딩의 원인.
        //
        // 판정: drift(예측시간 − 실시간)가 창 안에서 거의 안 변하면 추정이 멈춘 것 = 안정.
        // 진폭 5ms는 Mirror의 평균 계수(PredictionErrorWindowSize=20 → 표본당 9.5%)에서 유도했다 —
        // 0.5초(퐁 5개) 동안의 변화량이 남은 오차의 약 39%이므로, 진폭 5ms면 남은 오차는 13ms
        // 미만(1틱 미만)이다. Mirror가 그 상수를 바꾸면 임계값을 다시 유도해야 한다.
        private async Task WaitForClockSettleAsync()
        {
            const double WindowSeconds = 0.5;         // 퐁이 0.1초 간격이라 표본 5개가 들어간다
            const int MinSamples = 5;                 // 프레임이 느릴 때 진폭이 우연히 작게 나오는 것 방지
            const double AmplitudeThreshold = 0.005;  // 5ms
            const double TimeoutSeconds = 7;

            var times = new Queue<double>();
            var drifts = new Queue<double>();
            double start = Time.unscaledTimeAsDouble;
            double amplitude = 0;
            double drift = 0;
            bool settled = false;
            bool disconnected = false;

            try
            {
                while (true)
                {
                    // 연결이 끊기면 7초를 헛되이 기다리지 않는다. 끊김 자체의 처리는 기존 흐름 소관.
                    if (NetworkClient.ready == false)
                    {
                        disconnected = true;
                        break;
                    }

                    double now = Time.unscaledTimeAsDouble;
                    double elapsed = now - start;

                    // 실시간 기준은 반드시 unscaledTime — Mirror의 localTime과 같아야 실시간 항이
                    // 상쇄돼 drift가 곧 "서버와의 시차"가 된다.
                    drift = runner.networkTime.PredictedTime - now;
                    times.Enqueue(now);
                    drifts.Enqueue(drift);

                    // 창 밖으로 나간 표본을 버린다. 방금 넣은 표본은 나이가 0이라 큐가 비지 않는다.
                    while (now - times.Peek() > WindowSeconds)
                    {
                        times.Dequeue();
                        drifts.Dequeue();
                    }

                    if (elapsed >= WindowSeconds && drifts.Count >= MinSamples)
                    {
                        double min = double.MaxValue;
                        double max = double.MinValue;
                        foreach (double d in drifts)
                        {
                            if (d < min) min = d;
                            if (d > max) max = d;
                        }
                        amplitude = max - min;

                        if (amplitude < AmplitudeThreshold)
                        {
                            settled = true;
                            break;
                        }
                    }

                    if (elapsed >= TimeoutSeconds)
                    {
                        break;
                    }

                    await UniTask.Yield(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;   // 오브젝트가 사라지는 중 — 조용히 끝낸다
            }

            double total = Time.unscaledTimeAsDouble - start;
            if (disconnected)
            {
                Debug.Log($"[ClockSettle] 연결 끊김 elapsed={total:F2}s");
            }
            else if (settled)
            {
                Debug.Log(
                    $"[ClockSettle] settled elapsed={total:F2}s amplitude={amplitude:F4}" +
                    $" drift={drift:F3} window={drifts.Count}");
            }
            else
            {
                Debug.LogWarning(
                    $"[ClockSettle] TIMEOUT elapsed={total:F2}s amplitude={amplitude:F4}" +
                    $" drift={drift:F3} window={drifts.Count} — 최선값으로 시작");
            }
        }
```

- [ ] **Step 2: 진입 흐름에 배선한다**

같은 파일의 `Awake`에서 아래 4줄을

```csharp
                await InitializeAsync();
                await ConnectRoomServerAsync();
                await JoinRoomServerAsync();
                await StartGameAsync();
```

이렇게 바꾼다.

```csharp
                await InitializeAsync();
                await ConnectRoomServerAsync();

                // 접속하자마자 시계 안정 대기를 시작해 gameInfo 왕복과 겹쳐 돌린다. 순차로 두면
                // 겹치지 않아 오히려 늦다 — 시작만 앞당기고 합류는 시드 직전에 한다.
                var clockSettle = WaitForClockSettleAsync();
                await JoinRoomServerAsync();
                await clockSettle;

                await StartGameAsync();
```

- [ ] **Step 3: 필요한 using이 있는지 확인한다**

파일 상단 using 목록에 아래가 모두 있어야 한다. **`System`(→ `OperationCanceledException`), `System.Collections.Generic`(→ `Queue<T>`), `Cysharp.Threading.Tasks`(→ `UniTask`), `Mirror`(→ `NetworkClient`), `UnityEngine`(→ `Time`, `Debug`)** 는 이미 있다. 없는 것이 있으면 추가한다. `System.Linq`는 **추가하지 않는다**(Step 1 코드가 min/max를 직접 순회한다).

Run:
```bash
sed -n '1,12p' Assets/Scripts/Room/LOPRoom.cs
```
Expected: `using System;`, `using System.Collections.Generic;`, `using Cysharp.Threading.Tasks;`, `using Mirror;`, `using UnityEngine;` 가 모두 보인다.

- [ ] **Step 4: main에 머지해 에디터가 컴파일하게 한다**

Run:
```bash
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(netcode): 서버 시간 추정이 안정될 때까지 기다렸다 시계 시드

접속 직후엔 로딩 부하로 첫 핑이 부풀어(실측 1052ms vs 이후 180~200ms) 추정이 0.7초
어긋나는데, 그 값으로 출발선을 그으면 시계가 초당 5%씩만 교정돼 10초 넘게 어긋난 채
달린다. drift(예측시간 - 실시간)가 0.5초 창에서 진폭 5ms 미만이 될 때까지 기다린 뒤
시드한다. 7초 타임아웃 시 최선값으로 진행한다.

대기는 접속 직후 시작해 gameInfo 왕복과 겹쳐 돌리고, 이미 떠 있는 로딩 화면 뒤에 숨는다."

M="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
rm -f "$M/.git/index.lock"
git -C "$M" merge --no-ff --no-autostash worktree-feature+start-window-clock-sync \
  -m "Merge feature/start-window-clock-sync: 시계 안정 대기 후 시드"
```
Expected: 머지 성공, `Assets/Scripts/Room/LOPRoom.cs` 1 file changed.

- [ ] **Step 5: 컴파일 오류가 없는지 확인한다**

UnityMCP `refresh_unity`(`compile="request"`, `mode="force"`, `scope="scripts"`, `wait_for_ready=true`, `unity_instance=<클라 id>`) 후
`read_console`(`action="get"`, `types=["error"]`, `count=20`, `unity_instance=<클라 id>`).

Expected: `Retrieved 0 log entries.` (오류 0건)

오류가 있으면 워크트리에서 고치고 Step 4~5를 반복한다.

---

### Task 2: 라이브 검증

**Files:** 없음 (측정만)

**Interfaces:**
- Consumes: Task 1의 `[ClockSettle]` 로그, 기존 `[ClockSeed]`·`[ClockTrace]`·`[ReconSpike]` 진단 로그, HUD `Dump` 버튼
- Produces: 통과/실패 판정. 실패 시 Task 1로 되돌아간다.

- [ ] **Step 1: 사용자에게 측정을 요청한다**

측정 절차를 그대로 전달한다:

1. 유니티 에디터에서 플레이 (메인 + MPPM 클론 2인)
2. **Reset 버튼을 누르지 않는다** — 누르면 누적 카운터가 초기화돼 시작 구간이 측정에서 빠진다
3. **입장하자마자 바로 걷기 시작**(가만히 있으면 증상이 안 드러난다)
4. **~8초에 HUD `Dump`** 한 번 — 이때 `entities=2`여야 적 넉백 오염이 없음이 증명된다(첫 스폰이 t=10초)

- [ ] **Step 2: 클론 로그에서 결과를 읽는다**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
f="Library/VP/mppm61234b75/Logs/Editor.log"
grep "ClockSettle" "$f" | tail -2
grep "ClockSeed" "$f" | tail -1
grep "ClockTrace" "$f" | tail -20
```
Expected: `[ClockSettle] settled elapsed=...` 가 보이고, `[ClockSeed]`의 `pongs=`가 3보다 훨씬 크다.

> 클론 워크스페이스 이름(`mppm61234b75`)이 다르면 `ls Library/VP/` 로 확인한다.

- [ ] **Step 3: 통과 기준을 대조한다**

| 볼 것 | 통과 기준 | 어디서 |
|---|---|---|
| `[ClockSettle]` | `settled`, `elapsed` < 7.00s | 클론 로그 |
| `reconMax` | **0.06 미만** (현재 0.48~0.64) | Dump |
| `entities` | `2` (오염 없음 증명) | Dump |
| `corrections` | 크게 감소 (현재 20~28) | Dump |
| `[ClockTrace]`의 `error` | 최대치 100ms 미만 (현재 420ms) | 클론 로그 |
| 체감 | 입장 직후 "드르륵" 소멸 | 사용자 보고 |

`stalls`가 0이 아닌 회차는 버리고 다시 측정한다.

- [ ] **Step 4: 판정하고 기록한다**

전부 통과하면 Task 3으로 진행한다. 하나라도 실패하면 **추측하지 말고** 실패한 지표의 로그를 그대로 읽어 원인을 좁힌 뒤 Task 1로 돌아간다. 이 트랙은 추론으로 세 번 단언하고 세 번 틀린 이력이 있다.

측정된 수치(안정까지 걸린 시간 포함)를 다음 태스크의 ROADMAP 기록에 쓸 수 있게 메모해 둔다.

---

### Task 3: 진단용 임시 코드 정리

**Files:**
- Modify: `Assets/Mirror/Core/NetworkTime.cs` — 벤더 코드, **원상복구**
- Modify: `Assets/Scripts/Room/LOPRoom.cs` — `[ClockSeed]` 로그와 `BeginClockTrace()` 호출 제거
- Modify: `Assets/Scripts/Netcode/LOPTickUpdater.cs` — `[ClockTrace]` 블록 제거

**Interfaces:**
- Consumes: Task 2의 통과 판정
- Produces: 없음. `[ClockSettle]`만 영구 계측으로 남는다.

- [ ] **Step 1: Mirror를 원상복구한다**

> ⚠️ Step 1~4를 **모두 마친 뒤에** 머지·컴파일한다(Step 6). `LOPRoom`의 `[ClockSeed]` 로그가
> Mirror의 `LopPongCount`를 참조하므로, Mirror만 먼저 되돌리고 컴파일하면 깨진다.

`Assets/Mirror/Core/NetworkTime.cs`에서 두 곳을 되돌린다.

(1) `ResetStatics()` 안의 추가 줄을 삭제한다.

```csharp
            _rtt = new ExponentialMovingAverage(PingWindowSize);
            LopResetPongTrace();   // [LOP 진단용 임시] 접속마다 샘플 번호를 1부터 다시 센다   ← 이 줄 삭제
```

(2) `OnClientPong` 끝의 호출과 그 아래 진단 블록 전체를 삭제한다. 삭제 대상은 `LopPongSampleTrace(...)` 호출 한 줄과, 그에 이어지는 `// [LOP 진단용 임시] 접속 후 처음 ...` 주석부터 `LopPongSampleTrace` 메서드 닫는 중괄호까지다(`LopTraceSamples`, `lopPongCount`, `LopPongCount`, `LopResetPongTrace`, `LopPongSampleTrace` 전부 포함).

`OnClientPong`은 아래 상태로 끝나야 한다.

```csharp
            // feed unadjusted prediction error into our exponential moving average
            // store adjusted prediction error for debug / GUI purposes
            _predictionErrorUnadjusted.Add(message.predictionErrorUnadjusted);
            predictionErrorAdjusted = message.predictionErrorAdjusted;
            // Debug.Log($"[Client] predictionError avg={(_predictionErrorUnadjusted.Value*1000):F1} ms");
        }
```

- [ ] **Step 2: Mirror에 우리 흔적이 남지 않았는지 확인한다**

Run:
```bash
grep -n "Lop\|진단용" Assets/Mirror/Core/NetworkTime.cs
```
Expected: 출력 없음 (exit 1)

- [ ] **Step 3: LOPRoom의 `[ClockSeed]` 진단을 제거한다**

`StartGameAsync`를 아래 상태로 되돌린다(대기 메서드와 배선은 Task 1의 것을 그대로 둔다).

```csharp
        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다 — 지금 시계에서 유도한다.
            double target = ((LOPTickUpdater)runner.tickUpdater).TargetTime;
            runner.Run((long)(target / gameInfo.Interval), gameInfo.Interval, target);
        }
```

- [ ] **Step 4: LOPTickUpdater의 `[ClockTrace]` 진단을 제거한다**

`Assets/Scripts/Netcode/LOPTickUpdater.cs`를 아래 상태로 되돌린다.

```csharp
        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = clockDilator.Advance(elapsedTime, TargetTime, Time.deltaTime);
        }
    }
}
```

`TraceDuration`, `traceStartRealtime`, `lastTracedSecond`, `BeginClockTrace()`, `TraceClock(...)` 를 전부 삭제한다.

- [ ] **Step 5: 진단 흔적이 남지 않았는지 확인한다**

Run:
```bash
grep -rn "ClockSeed\|ClockTrace\|BeginClockTrace\|PongSample\|LopPongCount" Assets/Scripts Assets/Mirror
```
Expected: 출력 없음 (exit 1)

- [ ] **Step 6: 커밋하고 머지 후 컴파일을 확인한다**

Run:
```bash
git add Assets/Mirror/Core/NetworkTime.cs Assets/Scripts/Room/LOPRoom.cs Assets/Scripts/Netcode/LOPTickUpdater.cs
git commit -m "chore(netcode): 시작 구간 진단용 임시 계측 제거

원인이 확정되고 수정이 검증됐으므로 진단 코드를 되돌린다. Mirror(벤더)는 원상복구하고,
ClockSeed/ClockTrace는 제거한다. 영구 계측으로는 ClockSettle만 남는다."

M="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
rm -f "$M/.git/index.lock"
git -C "$M" merge --no-ff --no-autostash worktree-feature+start-window-clock-sync \
  -m "Merge feature/start-window-clock-sync: 진단용 임시 계측 제거"
```

그다음 UnityMCP `refresh_unity` + `read_console`(`types=["error"]`, `unity_instance=<클라 id>`).

Expected: 오류 0건.

- [ ] **Step 7: 정리 후에도 증상이 없는지 한 번 더 확인한다**

사용자에게 Task 2의 절차(입장 직후 걷기 → 8초 Dump)를 한 번만 더 요청한다.

Expected: `[ClockSettle] settled` 가 찍히고 `reconMax` < 0.06.

> 진단 코드를 빼면서 배선을 건드렸을 수 있으므로 마지막에 한 번 더 본다.

---

### Task 4: ROADMAP 기록

**Files:**
- Modify: `docs/ROADMAP.md` — "🟠 시작 구간 클럭 정렬" 절

**Interfaces:**
- Consumes: Task 2·3의 측정 수치
- Produces: 없음 (문서)

- [ ] **Step 1: 트랙을 완료로 바꾼다**

`docs/ROADMAP.md`의 `## 🟠 시작 구간 클럭 정렬 — 걷기 시작 시 64cm 러버밴딩 (2026-08-11 규명, 미수정)` 제목을 아래로 바꾸고,

```markdown
## ✅ 시작 구간 클럭 정렬 — 해결 (2026-08-11 규명 → 2026-08-12 수정)
```

같은 절 끝의 `### 다음 — 왜 시작에 43틱이나 앞서는가` 절을 아래 내용으로 교체한다. `<...>` 자리에는 Task 2에서 측정한 실제 값을 넣는다.

```markdown
### 원인 — 퐁 첫 표본 하나가 오염된 채 정답으로 굳는다

Mirror의 `OnClientPong`에 원본 표본 계측을 넣어 확정했다.

| | rawRtt | rawOffset |
|---|---|---|
| **n=1** | **1052ms** | **−191.692** (0.86초 오차) |
| n=2 | 192ms | −192.540 |
| n=3 | 179ms | −192.563 |

**첫 표본 하나만 오염돼 있다.** Mirror는 접속 즉시 첫 핑을 보내는데(`OnTransportConnected` →
`SendPing()`) 그 순간이 인증·씬·스폰으로 가장 바쁘고, Mirror 문서도 RTT가 처리 지연을 포함한
값임을 명시한다. 그리고 `ExponentialMovingAverage`는 **첫 표본을 평균 없이 그대로 채택**한 뒤
표본당 9.5%씩만 교정한다.

그 상태에서 우리는 **퐁 3개째에 출발선을 확정**했다(`[ClockSeed] pongs=3`) — 시드 오차
**0.70초 = 35틱**. 이후 `ClockDilator`가 초당 5%만 교정하므로 복구에 13초가 걸렸다.

**Mirror의 결함이 아니다.** `predictedTime`은 계속 읽으면 수렴하는 값이고 실제로 4~5초면 오차
10ms 이하다. 우리가 그것을 *한 번 읽어 출발선을 긋는 용도*로 쓴 것이고, 고정 틱 시뮬레이션은
우리가 얹은 것이므로 "언제 읽을지"도 우리 몫이다. 그 층이 비어 있었다.

### 수정 — 추정이 자리를 잡은 뒤에 시드 (2026-08-12)

`LOPRoom.WaitForClockSettleAsync()` — `drift`(예측시간 − 실시간)가 0.5초 창에서 진폭 5ms 미만이
될 때까지 기다렸다 시드한다. 임계 5ms는 Mirror의 평균 계수에서 유도했다(잔여 오차 1틱 미만).
대기는 접속 직후 시작해 `gameInfo` 왕복과 겹쳐 돌리고, 이미 떠 있는 로딩 화면 뒤에 숨는다.
7초 타임아웃 시 최선값으로 진행한다(폴백이 종전 동작보다 항상 같거나 낫다).

**측정 결과:** 안정까지 `<elapsed>`초 · `reconMax` `0.48~0.64` → `<reconMax>` ·
`corrections` `20~28` → `<corrections>` · 입장 직후 "드르륵" 소멸.

**영구 계측:** `[ClockSettle] settled elapsed=.. amplitude=.. drift=.. window=..`.
다른 환경(느린 폰, 실제 네트워크)에서 대기가 길어지면 추측이 아니라 이 로그로 판단한다.

spec `docs/superpowers/specs/2026-08-12-start-window-clock-sync-design.md`,
plan `docs/superpowers/plans/2026-08-12-start-window-clock-sync.md`.
```

- [ ] **Step 2: 커밋하고 머지한다**

Run:
```bash
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 시작 구간 클럭 정렬 해결 기록

원인(퐁 첫 표본 오염 + 첫 표본 그대로 채택 + 퐁 3개째 시드)과 수정(추정 안정 후 시드),
측정 결과를 남긴다."

M="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
rm -f "$M/.git/index.lock"
git -C "$M" merge --no-ff --no-autostash worktree-feature+start-window-clock-sync \
  -m "Merge feature/start-window-clock-sync: 시작 구간 클럭 정렬 해결"
```

Expected: 머지 성공.

- [ ] **Step 3: 브랜치가 전부 머지됐는지 확인한다**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" rev-list --count main..worktree-feature+start-window-clock-sync
```
Expected: `0`

---

## 완료 기준

- `[ClockSettle] settled` 가 타임아웃 미만으로 찍힌다
- 입장 직후 걷기에서 `reconMax` < 0.06 (`entities=2` 구간)
- 진단용 임시 코드가 전부 제거되고 `Assets/Mirror`가 원상복구됐다
- ROADMAP에 원인·수정·측정치가 기록됐다
- 브랜치가 `main`에 머지됐다
