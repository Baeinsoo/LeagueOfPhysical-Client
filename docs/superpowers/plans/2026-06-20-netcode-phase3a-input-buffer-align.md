# Netcode Phase 3a — 서버 Input Buffer command-frame 정렬 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 `EntityInputComponent.GetInput`을 command-frame 정렬(입력의 클라 tick == 서버 처리 tick)로 바꾸고 2틱 jitter buffer + miss=no-input + 지각 prune(drop)을 적용한다.

**Architecture:** 서버 전용 1파일(`EntityInputComponent`). `GetInput(serverTick)`이 `tick <= serverTick 중 최소`(현재)에서 `tick == serverTick − INPUT_DELAY_TICKS 정확 일치`로 바뀌고, `INPUT_DELAY_TICKS = 0 → 2`. 처리 시점 지난(`< targetTick`) 입력은 prune. miss는 null 반환(호출부 `LOPGameEngine.ProcessInput`이 이미 `if(input==null) continue`로 no-input 처리). 클라/wire/핸들러 무변경.

**Tech Stack:** C# (Unity), UnityMCP 컴파일 검증. 자동 테스트 없음(서버 단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Related spec:** `docs/superpowers/specs/2026-06-20-netcode-phase3-input-buffer-design.md` (Slice 3a)

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(변동 시 `mcpforunity://instances`로 재해석). 사용자 지시 netcode 서버 작업 — 서버 인스턴스 조작 인가. 클라 인스턴스 건드리지 말 것.

> **픽스처:** 변경 파일(`EntityInputComponent.cs`)은 픽스처 아님. `LOPGame.cs`/`ConfigureRoomComponent.cs`(dirty 로컬 픽스처)는 **미접촉** → stash 댄스 불필요, 커밋 시 제외만. `.git/index.lock` 에러 시 `rm -f .git/index.lock` 후 재시도.

---

## File Structure

- **Modify:** `Assets/Scripts/Component/EntityInputComponent.cs` — `INPUT_DELAY_TICKS = 2`, `GetInput` 재작성(정확 정렬 + prune + miss=null). `AddInput`/필드/시퀀스 가드는 유지.

단일 파일·단일 책임 변경. 호출부(`LOPGameEngine.ProcessInput`)는 `GetInput`의 null 반환을 이미 no-input으로 처리하므로 무변경.

---

## Task 1: 서버 피처 브랜치

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: dirty = `Assets/Scripts/Game/LOPGame.cs` + `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`(픽스처, 미접촉). 다른 dirty 있으면 확인.

- [ ] **Step 2: 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/netcode-phase3-input-buffer
```
(픽스처 dirty는 따라옴 — 이번 변경과 무관, stash 안 함.)

---

## Task 2: EntityInputComponent — 정렬 + jitter buffer + prune

**Files:** Modify `Assets/Scripts/Component/EntityInputComponent.cs`

> 현재 파일(서버):
> ```csharp
> private static int INPUT_DELAY_TICKS = 0;
> private SortedDictionary<long, PlayerInputToS> inputBuffer = new SortedDictionary<long, PlayerInputToS>();
> private long lastProcessedSequence = -1;
> public long expectedNextSequence { get; private set; }
>
> public PlayerInputToS GetInput(long tick) {
>     if (inputBuffer.Count == 0) return null;
>     long targetTick = tick - INPUT_DELAY_TICKS;
>     var availableInputs = inputBuffer.Where(kvp => kvp.Key <= targetTick).ToList();
>     if (availableInputs.Count == 0) return null;
>     PlayerInputToS input = availableInputs.First().Value;
>     inputBuffer.Remove(input.Tick);
>     lastProcessedSequence = input.PlayerInput.SequenceNumber;
>     return input;
> }
> // AddInput(...) 이하 유지
> ```

- [ ] **Step 1: `INPUT_DELAY_TICKS` 2로 변경** — 라인:
```csharp
        private static int INPUT_DELAY_TICKS = 0;
```
→
```csharp
        // jitter buffer: 입력이 처리 시점(serverTick)보다 2틱 일찍 도착하도록 여유. (Phase 2 lead + 이 버퍼로 적시 도착)
        private static int INPUT_DELAY_TICKS = 2;
```

- [ ] **Step 2: `GetInput` 본문 교체** — 기존 `GetInput` 메서드 전체를:
```csharp
        public PlayerInputToS GetInput(long tick)
        {
            if (inputBuffer.Count == 0)
            {
                return null;
            }

            // 지연을 적용한 처리 틱 계산
            long targetTick = tick - INPUT_DELAY_TICKS;

            var availableInputs = inputBuffer.Where(kvp => kvp.Key <= targetTick).ToList();
            if (availableInputs.Count == 0)
            {
                return null;
            }

            PlayerInputToS input = availableInputs.First().Value;
            inputBuffer.Remove(input.Tick);

            lastProcessedSequence = input.PlayerInput.SequenceNumber;

            return input;
        }
```
→ 다음으로 교체:
```csharp
        public PlayerInputToS GetInput(long tick)
        {
            // command-frame 정렬: 입력의 클라 tick == 서버 처리 tick(= serverTick − jitter buffer)
            long targetTick = tick - INPUT_DELAY_TICKS;

            // 처리 시점이 지난 입력(지각/처리불가)은 버린다 — 버퍼에 남아 나중에 잘못 처리되는 것 방지.
            var staleTicks = inputBuffer.Keys.Where(k => k < targetTick).ToList();
            foreach (var staleTick in staleTicks)
            {
                inputBuffer.Remove(staleTick);
            }

            // 정확히 targetTick의 입력만 처리. 없으면 miss → null(호출부가 no-input으로 진행, 클라 recon이 보정).
            if (inputBuffer.TryGetValue(targetTick, out var input))
            {
                inputBuffer.Remove(targetTick);
                lastProcessedSequence = input.PlayerInput.SequenceNumber;
                return input;
            }

            return null;
        }
```

- [ ] **Step 3: `using System.Linq;` 확인** — 파일 상단에 이미 있음(기존 `.Where`/`.First` 사용). 없으면 추가. `AddInput` 및 나머지(필드, `lastProcessedSequence`, `expectedNextSequence`)는 변경하지 않는다.

- [ ] **Step 4: 서버 컴파일 0에러**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.

- [ ] **Step 5: 커밋 (이 파일만 — 픽스처 제외)**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Component/EntityInputComponent.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(netcode): command-frame align server input buffer (tick==serverTick-2) + 2-tick jitter buffer + prune stale (Phase 3a)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 6:** `git -C "..." show --stat HEAD | head -6` — `EntityInputComponent.cs` 1파일만. `LOPGame.cs`/`ConfigureRoomComponent.cs` 포함되면 중단.

---

## Task 3: 런타임 수동 검증 (사용자)

서버 플레이 (가능하면 LatencySimulation으로 RTT 주입 — 현실적 조건):

1. **이동/액션 정상** — 캐릭터가 입력대로 이동, 액션 발동. (정렬이 정확 tick으로 바뀌어도 Phase 2 lead 덕에 입력이 `serverTick−2`에 도착해 있어야 함.)
2. **과한 끊김 없음** — miss(입력 없는 tick)는 no-input으로 진행하나, lead+버퍼로 드물어야 함. 가끔의 miss는 recon이 보정.
3. **recon 동등/개선** — Phase 2 대비 reconciliation distance가 비슷하거나 줄어듦(정렬 정확화 효과).
4. **지각 입력 잔여 없음** — 옛 입력이 뒤늦게 처리돼 캐릭이 과거대로 튀는 현상 없음.
5. **콘솔 에러 0**.

동작 보존/개선이 성공. (입력이 아직 reliable·단일 전송이라 유실 자체는 거의 없음 — 정렬·jitter buffer 효과 위주 확인. 유실 복구는 3b.)

---

## 완료 기준

- [ ] 서버 `feature/netcode-phase3-input-buffer`에 커밋 1개(`EntityInputComponent.cs` 1파일).
- [ ] 서버 컴파일 0에러. 클라/GameFramework 무변경.
- [ ] 서버 픽스처 2파일 working tree 보존(미커밋), 기존 stash 무손상.
- [ ] 수동 플레이: 이동/액션/정렬 동작 보존, 지각 잔여·과한 끊김 없음.

이후: 사용자 검증 후 서버 `feature/netcode-phase3-input-buffer` → main `--no-ff` 머지(머지가 LOPGame 안 건드림 — stash 댄스 불필요). 그 다음 **3b**(입력 unreliable 전환 + sliding-window redundancy — LOP-Shared proto + 클·서) 별도 plan.
