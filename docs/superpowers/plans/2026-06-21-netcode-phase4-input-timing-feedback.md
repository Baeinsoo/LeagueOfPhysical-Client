# Netcode Phase 4 — 입력 타이밍 피드백 + 동적 lead 구현 plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버가 per-client 입력 도착 타이밍을 측정해 클라에 피드백하고(Stage 1), 클라가 그 피드백으로 자기 lead(`AheadMargin`)를 오버워치식 정책으로 동적 조정(Stage 2)한다.

**Architecture:** 순수 로직(`InputTimingTracker`, `LeadController`, `InputTimingSummary`)은 EditMode 테스트 가능한 **GameFramework**에 둔다(서버/클라 게임코드는 전부 `Assembly-CSharp`라 asmdef 참조 불가 — `ClockDilator` 선례). 서버 `EntityInputComponent`가 tracker로 측정, 서버 `LOPGameEngine`이 주기 전송, 클라 핸들러가 수신→홀더(Stage 1)→`LeadController`로 `LeadState.AheadMargin` 갱신(Stage 2), `LOPTickUpdater`가 그 margin을 읽는다.

**Tech Stack:** Unity / C# / GameFramework(asmdef 패키지) / Mirror(transport) / protobuf(LOP-Shared wire) / VContainer(DI) / NUnit(EditMode 테스트) / UI Toolkit(HUD).

**Spec:** `docs/superpowers/specs/2026-06-21-netcode-phase4-input-timing-feedback-design.md`

**저장소:** 작업이 4개 git 저장소에 걸친다 — GameFramework, LeagueOfPhysical-Shared, LeagueOfPhysical-Server, LeagueOfPhysical-Client. **각 저장소에 `feature/netcode-phase4-input-timing-feedback` 브랜치를 만들어** 해당 저장소 변경을 커밋한다(클라 저장소엔 spec/plan이 이미 이 브랜치에 있음). 완료 후 각 저장소에서 main에 `--no-ff` 머지.

**경로 약어:**
- `GF/` = `C:\Users\re5na\workspace\LOP\GameFramework`
- `SH/` = `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`
- `SV/` = `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`
- `CL/` = `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`

**UnityMCP 인스턴스:** 컴파일/테스트 시 `unity_instance`를 명시한다 — 클라 `LeagueOfPhysical-Client@<hash>`, 서버 `LeagueOfPhysical-Server@<hash>`. `mcpforunity://instances`로 hash 확인. GameFramework/LOP-Shared는 `file:` 패키지라 양쪽 에디터가 본다(GameFramework 테스트는 어느 한쪽 인스턴스에서 실행).

---

## File Structure

**GameFramework** (`GF/Runtime/Scripts/Game/`, `GF/Tests/Runtime/`)
- `InputTimingSummary.cs` (신규) — 측정 요약 struct.
- `InputTimingTracker.cs` (신규) — per-client 누적기.
- `LeadController.cs` (신규, Stage 2) — lead 조정 정책.
- `InputTimingTrackerTests.cs`, `LeadControllerTests.cs` (신규) — EditMode.

**LOP-Shared** (`SH/Protos/`, `SH/Scripts/`)
- `InputTimingToC.proto` (신규) — wire 메시지. 재생성 산출물(`Runtime.Generated/`)은 codegen이 갱신.

**Server** (`SV/Assets/Scripts/`)
- `Component/EntityInputComponent.cs` (수정) — tracker 측정 훅 + `SummarizeTiming()`.
- `Game/LOPGameEngine.cs` (수정) — 주기 전송.

**Client** (`CL/Assets/Scripts/`, `CL/Assets/UI/`)
- `Game/InputTimingStats.cs` (신규) — 수신 요약 홀더.
- `Game/MessageHandler/Game.InputTiming.MessageHandler.cs` (신규) — 수신 핸들러.
- `Game/LeadState.cs` (신규, Stage 2) — 동적 margin 홀더 + 토글.
- `Game/LOPTickUpdater.cs` (수정, Stage 2) — margin을 LeadState에서 read.
- `Game/GameLifetimeScope.cs` (수정) — DI 등록.
- `UI/DebugHud/DebugHud.uxml`, `UI/DebugHud/DebugHudViewModel.cs`, `UI/DebugHud/DebugHudView.cs` (수정) — HUD 라벨.

---

# STAGE 1 — 측정 파이프라인 + HUD (행동 변화 없음)

## Task 1: GameFramework — InputTimingSummary + InputTimingTracker + 테스트

**Files:**
- Create: `GF/Runtime/Scripts/Game/InputTimingSummary.cs`
- Create: `GF/Runtime/Scripts/Game/InputTimingTracker.cs`
- Test: `GF/Tests/Runtime/InputTimingTrackerTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`GF/Tests/Runtime/InputTimingTrackerTests.cs`:
```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class InputTimingTrackerTests
    {
        [Test]
        public void Summarize_NoSamples_ZeroAndNoActivity()
        {
            var t = new InputTimingTracker();
            var s = t.Summarize();
            Assert.AreEqual(0.0, s.AvgD, 1e-9);
            Assert.AreEqual(0, s.MaxD);
            Assert.AreEqual(0, s.SampleCount);
            Assert.IsFalse(s.HasActivity);
        }

        [Test]
        public void Summarize_Arrivals_AveragesAndKeepsTightestMax()
        {
            var t = new InputTimingTracker();
            t.RecordArrival(-5);
            t.RecordArrival(-3);
            t.RecordArrival(-1);
            var s = t.Summarize();
            Assert.AreEqual(-3.0, s.AvgD, 1e-9);
            Assert.AreEqual(-1, s.MaxD);   // 가장 큰(가장 빠듯한) d
            Assert.AreEqual(3, s.SampleCount);
            Assert.IsTrue(s.HasActivity);
        }

        [Test]
        public void Summarize_CountsPruneAndSeqGap_ActivityEvenWithoutSamples()
        {
            var t = new InputTimingTracker();
            t.RecordPrune();
            t.RecordSeqGap(2);
            var s = t.Summarize();
            Assert.AreEqual(1, s.PruneCount);
            Assert.AreEqual(2, s.SeqGapCount);
            Assert.AreEqual(0, s.SampleCount);
            Assert.IsTrue(s.HasActivity);
        }

        [Test]
        public void Summarize_ResetsAccumulators()
        {
            var t = new InputTimingTracker();
            t.RecordArrival(-2);
            t.RecordPrune();
            t.RecordSeqGap(1);
            t.Summarize();
            var s = t.Summarize();
            Assert.AreEqual(0, s.SampleCount);
            Assert.AreEqual(0, s.PruneCount);
            Assert.AreEqual(0, s.SeqGapCount);
            Assert.IsFalse(s.HasActivity);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

UnityMCP `run_tests`(mode EditMode, 필터 `InputTimingTrackerTests`)로 실행 — 컴파일 에러(타입 없음)로 FAIL 예상.

- [ ] **Step 3: InputTimingSummary 구현**

`GF/Runtime/Scripts/Game/InputTimingSummary.cs`:
```csharp
namespace GameFramework
{
    /// <summary>서버가 측정한 클라 입력 도착 타이밍 한 간격 요약. d = 도착 마진(serverTick − inputTick).</summary>
    public readonly struct InputTimingSummary
    {
        public readonly double AvgD;
        public readonly int MaxD;
        public readonly int PruneCount;
        public readonly int SeqGapCount;
        public readonly int SampleCount;

        public InputTimingSummary(double avgD, int maxD, int pruneCount, int seqGapCount, int sampleCount)
        {
            AvgD = avgD;
            MaxD = maxD;
            PruneCount = pruneCount;
            SeqGapCount = seqGapCount;
            SampleCount = sampleCount;
        }

        /// <summary>이번 간격에 측정할 거리가 있었나(아무 입력/실패 없으면 전송 skip).</summary>
        public bool HasActivity => SampleCount > 0 || PruneCount > 0 || SeqGapCount > 0;
    }
}
```

- [ ] **Step 4: InputTimingTracker 구현**

`GF/Runtime/Scripts/Game/InputTimingTracker.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 서버측 per-client 입력 도착 타이밍 누적기. 한 피드백 간격 동안 도착 마진 d 샘플과
    /// prune/seq-gap 실패를 모아 Summarize()로 요약하고 비운다. 순수 C#이라 EditMode 테스트 가능.
    /// </summary>
    public class InputTimingTracker
    {
        private double _dSum;
        private int _maxD = int.MinValue;
        private int _sampleCount;
        private int _pruneCount;
        private int _seqGapCount;

        /// <summary>입력이 처음 버퍼에 들어올 때의 도착 마진 d = serverTick − inputTick.</summary>
        public void RecordArrival(int d)
        {
            _dSum += d;
            if (d > _maxD)
            {
                _maxD = d;
            }
            _sampleCount++;
        }

        /// <summary>처리 시점이 지나 폐기된 입력(너무 늦음) 1건.</summary>
        public void RecordPrune() => _pruneCount++;

        /// <summary>소비 seq 불연속으로 감지된 유실 입력 gap건.</summary>
        public void RecordSeqGap(int gap)
        {
            if (gap > 0)
            {
                _seqGapCount += gap;
            }
        }

        /// <summary>간격 요약을 산출하고 누적기를 비운다.</summary>
        public InputTimingSummary Summarize()
        {
            double avgD = _sampleCount > 0 ? _dSum / _sampleCount : 0.0;
            int maxD = _sampleCount > 0 ? _maxD : 0;
            var summary = new InputTimingSummary(avgD, maxD, _pruneCount, _seqGapCount, _sampleCount);

            _dSum = 0;
            _maxD = int.MinValue;
            _sampleCount = 0;
            _pruneCount = 0;
            _seqGapCount = 0;

            return summary;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, `InputTimingTrackerTests`) → 4 PASS. `read_console`로 컴파일 에러 없음 확인.

- [ ] **Step 6: 커밋 (GameFramework 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/netcode-phase4-input-timing-feedback
git add Runtime/Scripts/Game/InputTimingSummary.cs Runtime/Scripts/Game/InputTimingSummary.cs.meta \
        Runtime/Scripts/Game/InputTimingTracker.cs Runtime/Scripts/Game/InputTimingTracker.cs.meta \
        Tests/Runtime/InputTimingTrackerTests.cs Tests/Runtime/InputTimingTrackerTests.cs.meta
git commit -m "feat(netcode): InputTimingTracker + InputTimingSummary for Phase 4 measurement

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> `.meta`는 Unity가 생성한 것만 add. 컴파일/refresh 후 `.meta`가 생겼는지 확인하고 add.

---

## Task 2: LOP-Shared — InputTimingToC proto + 재생성

**Files:**
- Create: `SH/Protos/InputTimingToC.proto`
- Regenerate: `SH/Runtime.Generated/Scripts/Protobuf/*`, `MessageIds.cs`, `MessageInitializer.cs`

- [ ] **Step 1: proto 작성**

`SH/Protos/InputTimingToC.proto`:
```proto
syntax = "proto3";

// @auto_generate
message InputTimingToC
{
	double avg_d = 1;
	int32 max_d = 2;
	int32 prune_count = 3;
	int32 seq_gap_count = 4;
	int32 sample_count = 5;
}
```
> `@auto_generate` 주석이 있어야 MessageId가 부여된다. 5필드 — `sample_count`는 클라가 `InputTimingSummary`를 *충실히* 재구성해 `LeadController`에 넘기기 위함(HUD는 앞 4개만 표시). ⚠️ proto 본문/주석에 `@auto_generate` 외 다른 곳에 그 리터럴을 쓰지 말 것(codegen 매칭 → MessageId 시프트).

- [ ] **Step 2: 재생성 실행**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```
Expected: "All proto-related scripts executed successfully." 산출 — `Runtime.Generated/Scripts/Protobuf/InputTimingToC.cs`(+`.IMessage.cs`), `MessageIds.cs`에 `InputTimingToC` 항목 추가(기존 ID 불변, 새 ID 말미), `MessageInitializer.cs` 갱신.

- [ ] **Step 3: MessageId 추가·기존 불변 확인**

`SH/Runtime.Generated/Scripts/MessageIds.cs`를 열어 `InputTimingToC = N;`이 새로 생겼고 **기존 메시지 ID 숫자가 그대로**인지 확인(diff). 기존 ID가 바뀌면 wire가 깨지므로, 바뀌었다면 중단하고 원인(주석 매칭 등) 조사.

- [ ] **Step 4: 양쪽 컴파일 확인**

클라·서버 둘 다 `refresh_unity`(scope scripts, compile request, force) 후 `read_console`(error) — `InputTimingToC` 타입이 양쪽에서 인식되고 에러 없음 확인. (`file:` 패키지라 양쪽이 새 .cs를 본다.)

- [ ] **Step 5: 커밋 (LOP-Shared 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/netcode-phase4-input-timing-feedback
git add Protos/InputTimingToC.proto Protos/InputTimingToC.proto.meta \
        Runtime.Generated/Scripts/Protobuf Runtime.Generated/Scripts/MessageIds.cs \
        Runtime.Generated/Scripts/MessageInitializer.cs Runtime.Generated/Scripts/Protobuf.meta 2>/dev/null
git add -A Runtime.Generated/Scripts
git commit -m "feat(netcode): InputTimingToC wire message (Phase 4 input timing feedback)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 생성된 `.cs`와 `.meta`를 함께 커밋(`git add -A Runtime.Generated/Scripts`로 신규 산출물 포함). `git status`로 의도한 파일만 스테이징됐는지 확인 후 커밋.

---

## Task 3: Server — EntityInputComponent 측정 훅

**Files:**
- Modify: `SV/Assets/Scripts/Component/EntityInputComponent.cs`

- [ ] **Step 1: tracker 필드 + Summarize 노출 추가**

`EntityInputComponent` 클래스 본문 상단(필드 영역)에 추가:
```csharp
        private readonly GameFramework.InputTimingTracker timingTracker = new GameFramework.InputTimingTracker();

        public GameFramework.InputTimingSummary SummarizeTiming() => timingTracker.Summarize();
```

- [ ] **Step 2: AddInput에 도착 d 기록**

`AddInput`의 `if (inputBuffer.ContainsKey(tick) == false)` 블록 **첫 줄**에 추가:
```csharp
                timingTracker.RecordArrival((int)(GameFramework.GameEngine.Time.tick - tick));
```
(이 블록은 입력이 *처음* 버퍼에 들어올 때만 실행되므로 최초 도착 d가 기록된다.)

- [ ] **Step 3: GetInput에 prune·seq-gap 기록**

prune — stale 제거 루프를:
```csharp
            foreach (var staleTick in staleTicks)
            {
                timingTracker.RecordPrune();
                inputBuffer.Remove(staleTick);
            }
```
seq-gap — 소비 분기(`TryGetValue` 성공)를:
```csharp
            if (inputBuffer.TryGetValue(targetTick, out var input))
            {
                inputBuffer.Remove(targetTick);
                long gap = input.PlayerInput.SequenceNumber - lastProcessedSequence - 1;
                if (gap > 0)
                {
                    timingTracker.RecordSeqGap((int)gap);
                }
                lastProcessedSequence = input.PlayerInput.SequenceNumber;
                return input;
            }
```
(gap 계산은 `lastProcessedSequence` 갱신 *전*에 한다. 최초 소비 seq=0, lastProcessed=-1 → gap=0 → 미기록.)

- [ ] **Step 4: 서버 컴파일 확인**

서버 `refresh_unity`(scripts, compile, force) → `read_console`(error) 없음.

- [ ] **Step 5: 커밋 (Server 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/netcode-phase4-input-timing-feedback
git add Assets/Scripts/Component/EntityInputComponent.cs
git commit -m "feat(netcode): measure per-client input arrival timing in EntityInputComponent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Server — 주기 피드백 전송

**Files:**
- Modify: `SV/Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: UpdateEngine에 전송 호출 추가**

서버 `LOPGameEngine.UpdateEngine()`에서 `ProcessEvent();` 다음 줄(`EndUpdate();` 앞)에 추가:
```csharp
            SendInputTimingFeedback();
```

- [ ] **Step 2: SendInputTimingFeedback 메서드 구현**

`ProcessInput()` 메서드 아래에 추가:
```csharp
        // ~0.5초마다 조종 엔티티별 입력 타이밍 요약을 그 세션에 전송(Phase 4). 활동 없으면 skip.
        private const long InputTimingFeedbackIntervalTicks = 15;  // 틱레이트 기준 ~0.5초 — 필요시 조정

        private void SendInputTimingFeedback()
        {
            if (GameEngine.Time.tick % InputTimingFeedbackIntervalTicks != 0)
            {
                return;
            }

            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                var inputComponent = entity.GetEntityComponent<EntityInputComponent>();
                if (inputComponent == null)
                {
                    continue;
                }

                var summary = inputComponent.SummarizeTiming();
                if (summary.HasActivity == false)
                {
                    continue;
                }

                string userId = entityManager.GetUserIdByEntityId(entity.entityId);
                ISession session = sessionManager.GetSessionByUserId(userId);
                if (session == null)
                {
                    continue;
                }

                session.Send(new InputTimingToC
                {
                    AvgD = summary.AvgD,
                    MaxD = summary.MaxD,
                    PruneCount = summary.PruneCount,
                    SeqGapCount = summary.SeqGapCount,
                    SampleCount = summary.SampleCount,
                }, reliable: false);
            }
        }
```
> `entityManager`/`sessionManager`는 이미 주입돼 `ProcessInput`에서 같은 방식으로 쓰인다. `ISession.Send<T>(T, bool reliable)`은 Phase 3b에서 추가됨. 활동 없는 엔티티(AI 등)는 `HasActivity == false`로 skip → tracker도 비워짐(매 간격 reset).

- [ ] **Step 3: 서버 컴파일 확인**

서버 `refresh_unity` → `read_console`(error) 없음.

- [ ] **Step 4: 커밋 (Server 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/LOPGameEngine.cs
git commit -m "feat(netcode): periodically send InputTimingToC feedback per client

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Client — InputTimingStats 홀더 + DI

**Files:**
- Create: `CL/Assets/Scripts/Game/InputTimingStats.cs`
- Modify: `CL/Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: 홀더 작성**

`CL/Assets/Scripts/Game/InputTimingStats.cs`:
```csharp
namespace LOP
{
    /// <summary>
    /// netcode 측정용 입력 타이밍 피드백 홀더(클라). InputTimingToC 핸들러가 최신 요약을 write하고
    /// DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다. (ReconciliationStats 패턴)
    /// </summary>
    public class InputTimingStats
    {
        public double AvgD { get; private set; }
        public int MaxD { get; private set; }
        public int PruneCount { get; private set; }
        public int SeqGapCount { get; private set; }
        public bool HasData { get; private set; }

        public void Update(double avgD, int maxD, int pruneCount, int seqGapCount)
        {
            AvgD = avgD;
            MaxD = maxD;
            PruneCount = pruneCount;
            SeqGapCount = seqGapCount;
            HasData = true;
        }
    }
}
```

- [ ] **Step 2: DI 등록**

`GameLifetimeScope.Configure`의 `builder.Register<ReconciliationStats>(Lifetime.Singleton);` 바로 아래에 추가:
```csharp
            builder.Register<InputTimingStats>(Lifetime.Singleton);
```

- [ ] **Step 3: 컴파일 확인**

클라 `refresh_unity` → `read_console`(error) 없음.

- [ ] **Step 4: 커밋 (Client 저장소, 기존 feature 브랜치)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/InputTimingStats.cs Assets/Scripts/Game/InputTimingStats.cs.meta \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(netcode): client InputTimingStats holder + DI registration

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 클라는 spec/plan이 이미 `feature/netcode-phase4-input-timing-feedback`에 있으므로 브랜치 새로 만들지 말고 그대로 커밋.

---

## Task 6: Client — InputTimingToC 핸들러 + DI

**Files:**
- Create: `CL/Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs`
- Modify: `CL/Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: 핸들러 작성**

`CL/Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs`:
```csharp
using GameFramework;
using VContainer;

namespace LOP
{
    public class GameInputTimingMessageHandler : IGameMessageHandler
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        public void Register()
        {
            EventBus.Default.Subscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        private void OnInputTimingToC(InputTimingToC message)
        {
            inputTimingStats.Update(message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount);
        }
    }
}
```
> `GameInputMessageHandler` 패턴 그대로. Stage 2에서 LeadController 구동을 여기 추가한다(Task 10).

- [ ] **Step 2: DI 등록**

`GameLifetimeScope.Configure`의 `builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);` 아래에 추가:
```csharp
            builder.Register<IGameMessageHandler, GameInputTimingMessageHandler>(Lifetime.Transient);
```

- [ ] **Step 3: 컴파일 확인**

클라 `refresh_unity` → `read_console`(error) 없음.

- [ ] **Step 4: 커밋 (Client)**

```bash
git add "Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs" \
        "Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs.meta" \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(netcode): client InputTimingToC handler -> InputTimingStats

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Client — DebugHud 4라벨

**Files:**
- Modify: `CL/Assets/UI/DebugHud/DebugHud.uxml`
- Modify: `CL/Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`
- Modify: `CL/Assets/Scripts/UI/DebugHud/DebugHudView.cs`

- [ ] **Step 1: UXML에 라벨 추가**

`DebugHud.uxml`의 `recon-max-text` Label 다음 줄(닫는 `</ui:VisualElement>` 앞)에 추가:
```xml
            <ui:Label name="timing-avgd-text" class="debug-text" text="d avg: 0.0" />
            <ui:Label name="timing-maxd-text" class="debug-text" text="d max: 0" />
            <ui:Label name="timing-prune-text" class="debug-text" text="Prune: 0" />
            <ui:Label name="timing-seqgap-text" class="debug-text" text="SeqGap: 0" />
```

- [ ] **Step 2: ViewModel에 inject + 프로퍼티 추가**

`DebugHudViewModel`에 필드 추가(`reconciliationStats` inject 아래):
```csharp
        [Inject]
        private InputTimingStats inputTimingStats;
```
프로퍼티 추가(`ReconMax` 아래):
```csharp
        public double TimingAvgD => inputTimingStats.AvgD;

        public int TimingMaxD => inputTimingStats.MaxD;

        public int TimingPrune => inputTimingStats.PruneCount;

        public int TimingSeqGap => inputTimingStats.SeqGapCount;
```

- [ ] **Step 3: View에 라벨 필드 + Q + Refresh 추가**

`DebugHudView` 라벨 필드 영역(`_reconMaxText` 아래)에 추가:
```csharp
        private Label _timingAvgDText;
        private Label _timingMaxDText;
        private Label _timingPruneText;
        private Label _timingSeqGapText;
```
`OnOpen`의 `_reconMaxText = Root.Q<Label>("recon-max-text");` 아래에 추가:
```csharp
            _timingAvgDText = Root.Q<Label>("timing-avgd-text");
            _timingMaxDText = Root.Q<Label>("timing-maxd-text");
            _timingPruneText = Root.Q<Label>("timing-prune-text");
            _timingSeqGapText = Root.Q<Label>("timing-seqgap-text");
```
`Refresh`의 마지막 recon 라인 아래에 추가:
```csharp
            _timingAvgDText.text = $"d avg: {_viewModel.TimingAvgD:F1}";
            _timingMaxDText.text = $"d max: {_viewModel.TimingMaxD}";
            _timingPruneText.text = $"Prune: {_viewModel.TimingPrune}";
            _timingSeqGapText.text = $"SeqGap: {_viewModel.TimingSeqGap}";
```

- [ ] **Step 4: 컴파일 확인 + 시각 확인**

클라 `refresh_unity` → 에러 없음. 플레이로 HUD에 4라벨 표시 확인.

- [ ] **Step 5: 커밋 (Client)**

```bash
git add Assets/UI/DebugHud/DebugHud.uxml \
        Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs \
        Assets/Scripts/UI/DebugHud/DebugHudView.cs
git commit -m "feat(netcode): show input timing feedback (d avg/max, prune, seq-gap) on DebugHud

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## ⏸ STAGE 1 관측 중단점

**Stage 2로 넘어가기 전, 실데이터를 관측한다:**

- [ ] 클·서 플레이 + LatencySimulation으로 조건 주입(예: latency 100ms, loss 20%, jitter). HUD에서 `d avg / d max / Prune / SeqGap` 관찰.
- [ ] 다양한 조건(저지연 무손실 / 고지연 / 고손실)에서 4값 분포 기록.
- [ ] 이 데이터로 Task 8의 `LeadController` 시작 임계값(`tightBand`/`looseBand`/step/margin 범위)을 *확정*한다 — 아래 기본값은 출발점일 뿐.

> 관측 없이 Stage 2 임계값을 확정하지 말 것(감으로 오버워치 표 베끼기 금지 — spec Open Decisions).

---

# STAGE 2 — 동적 lead

## Task 8: GameFramework — LeadController + 테스트

**Files:**
- Create: `GF/Runtime/Scripts/Game/LeadController.cs`
- Test: `GF/Tests/Runtime/LeadControllerTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`GF/Tests/Runtime/LeadControllerTests.cs`:
```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class LeadControllerTests
    {
        private static InputTimingSummary S(double avgD, int maxD, int prune, int seqGap, int samples)
            => new InputTimingSummary(avgD, maxD, prune, seqGap, samples);

        [Test]
        public void Adjust_Failure_IncreasesByBigStep()
        {
            var c = new LeadController(bigStep: 0.010, smallStep: 0.002);
            Assert.AreEqual(0.040, c.Adjust(0.030, S(-3, -3, prune: 1, seqGap: 0, samples: 5)), 1e-9);
        }

        [Test]
        public void Adjust_Tight_IncreasesGraduated()
        {
            var c = new LeadController(tightBand: 1, smallStep: 0.002);
            // maxD=3 → +0.002*(3-1)=+0.004
            Assert.AreEqual(0.034, c.Adjust(0.030, S(0, 3, 0, 0, 5)), 1e-9);
        }

        [Test]
        public void Adjust_Loose_DecreasesBySmallStep()
        {
            var c = new LeadController(looseBand: -1, smallStep: 0.002);
            Assert.AreEqual(0.028, c.Adjust(0.030, S(-6, -4, 0, 0, 5)), 1e-9);
        }

        [Test]
        public void Adjust_DeadZone_NoChange()
        {
            var c = new LeadController(tightBand: 1, looseBand: -1);
            Assert.AreEqual(0.030, c.Adjust(0.030, S(0, 0, 0, 0, 5)), 1e-9);
        }

        [Test]
        public void Adjust_ClampsToMax()
        {
            var c = new LeadController(bigStep: 0.010, maxMargin: 0.035);
            Assert.AreEqual(0.035, c.Adjust(0.030, S(-3, -3, 1, 0, 5)), 1e-9);
        }

        [Test]
        public void Adjust_NoSamplesNoFailure_NoChange()
        {
            var c = new LeadController();
            Assert.AreEqual(0.030, c.Adjust(0.030, S(0, 0, 0, 0, 0)), 1e-9);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

`run_tests`(EditMode, `LeadControllerTests`) → 컴파일 에러 FAIL.

- [ ] **Step 3: LeadController 구현**

`GF/Runtime/Scripts/Game/LeadController.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 서버 입력 타이밍 피드백(도착 마진 d + 실패)으로 클라 lead(AheadMargin, 초)를 조정하는 정책.
    /// 오버워치식 dead-zone + 계단식 밴드 + 비대칭(실패 시 빠르게 늘림 / 여유 과다 시 천천히 줄임).
    /// 증분이라 피드백 1건당 1회 호출(매 프레임 아님). 순수 함수라 EditMode 테스트 가능.
    /// 임계값은 Stage 1 관측 데이터로 튜닝(아래 기본은 출발점).
    /// </summary>
    public class LeadController
    {
        private readonly int tightBand;
        private readonly int looseBand;
        private readonly double bigStep;
        private readonly double smallStep;
        private readonly double minMargin;
        private readonly double maxMargin;

        public LeadController(int tightBand = 1, int looseBand = -1,
            double bigStep = 0.010, double smallStep = 0.002,
            double minMargin = 0.0, double maxMargin = 0.100)
        {
            this.tightBand = tightBand;
            this.looseBand = looseBand;
            this.bigStep = bigStep;
            this.smallStep = smallStep;
            this.minMargin = minMargin;
            this.maxMargin = maxMargin;
        }

        /// <summary>현재 margin(초)과 이번 간격 요약으로 새 margin(초) 반환.</summary>
        public double Adjust(double currentMargin, InputTimingSummary summary)
        {
            double margin = currentMargin;

            if (summary.PruneCount > 0 || summary.SeqGapCount > 0)
            {
                margin += bigStep;                                  // 실패 = 비상, 빠르게 쿠션 추가
            }
            else if (summary.SampleCount > 0 && summary.MaxD > tightBand)
            {
                margin += smallStep * (summary.MaxD - tightBand);   // 빠듯 = 계단식 증가
            }
            else if (summary.SampleCount > 0 && summary.MaxD < looseBand)
            {
                margin -= smallStep;                                // 여유 = 천천히 감소
            }
            // dead-zone: looseBand ≤ maxD ≤ tightBand → 변화 없음

            return System.Math.Clamp(margin, minMargin, maxMargin);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

`run_tests`(EditMode, `LeadControllerTests`) → 6 PASS.

- [ ] **Step 5: 커밋 (GameFramework)**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/Game/LeadController.cs Runtime/Scripts/Game/LeadController.cs.meta \
        Tests/Runtime/LeadControllerTests.cs Tests/Runtime/LeadControllerTests.cs.meta
git commit -m "feat(netcode): LeadController dynamic-lead policy (Overwatch-shaped) for Phase 4

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Client — LeadState 홀더 + DI

**Files:**
- Create: `CL/Assets/Scripts/Game/LeadState.cs`
- Modify: `CL/Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: LeadState 작성**

`CL/Assets/Scripts/Game/LeadState.cs`:
```csharp
namespace LOP
{
    /// <summary>
    /// 동적 lead 상태 홀더(클라). 입력 타이밍 핸들러가 LeadController로 AheadMargin을 갱신(0.5초 1회),
    /// LOPTickUpdater가 매 프레임 읽는다. Enabled로 고정/동적 A/B 토글. 게임 스코프 Singleton.
    /// </summary>
    public class LeadState
    {
        public const double DefaultMargin = 0.030;

        public double AheadMargin { get; set; } = DefaultMargin;
        public bool Enabled { get; set; } = true;  // 동적 조정 on/off (A/B 비교)
    }
}
```

- [ ] **Step 2: DI 등록**

`GameLifetimeScope.Configure`의 `builder.Register<InputTimingStats>(Lifetime.Singleton);` 아래에 추가:
```csharp
            builder.Register<LeadState>(Lifetime.Singleton);
```

- [ ] **Step 3: 컴파일 확인 + 커밋**

클라 `refresh_unity` → 에러 없음.
```bash
git add Assets/Scripts/Game/LeadState.cs Assets/Scripts/Game/LeadState.cs.meta \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(netcode): client LeadState holder (dynamic AheadMargin + toggle) + DI

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Client — 핸들러가 LeadController로 LeadState 갱신

**Files:**
- Modify: `CL/Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs`

- [ ] **Step 1: 핸들러에 LeadController 구동 추가**

`Game.InputTiming.MessageHandler.cs` 전체를 다음으로 교체:
```csharp
using GameFramework;
using VContainer;

namespace LOP
{
    public class GameInputTimingMessageHandler : IGameMessageHandler
    {
        [Inject]
        private InputTimingStats inputTimingStats;

        [Inject]
        private LeadState leadState;

        private readonly LeadController leadController = new LeadController();

        public void Register()
        {
            EventBus.Default.Subscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<InputTimingToC>(nameof(IMessage), OnInputTimingToC);
        }

        private void OnInputTimingToC(InputTimingToC message)
        {
            inputTimingStats.Update(message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount);

            if (leadState.Enabled)
            {
                var summary = new InputTimingSummary(
                    message.AvgD, message.MaxD, message.PruneCount, message.SeqGapCount, message.SampleCount);
                leadState.AheadMargin = leadController.Adjust(leadState.AheadMargin, summary);
            }
        }
    }
}
```
> 정책은 메시지 수신 시(0.5초 1회)만 돈다 — 증분이라 매 프레임 호출하면 과조정. `sample_count`를 wire에서 받아 `InputTimingSummary`를 충실히 재구성.

- [ ] **Step 2: 컴파일 확인 + 커밋**

클라 `refresh_unity` → 에러 없음.
```bash
git add "Assets/Scripts/Game/MessageHandler/Game.InputTiming.MessageHandler.cs"
git commit -m "feat(netcode): drive LeadController from timing feedback to update LeadState

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Client — LOPTickUpdater가 LeadState margin 사용

**Files:**
- Modify: `CL/Assets/Scripts/Game/LOPTickUpdater.cs`

- [ ] **Step 1: const margin → LeadState read로 교체**

`LOPTickUpdater.cs` 전체를 다음으로 교체:
```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        [Inject]
        private LeadState leadState;

        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            // 동적 lead(LeadState)는 입력 타이밍 피드백으로 갱신됨. 주입 전(초기 프레임)엔 기본값.
            double aheadMargin = leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin;
            double target = GameEngine.NetworkTime.predictedTime + aheadMargin;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
```
> `LOPTickUpdater`는 게임 씬의 MonoBehaviour라 `GameLifetimeScope`의 `InjectSceneObjects`로 `[Inject]`가 채워진다. 주입 전 초기 프레임 대비 null 가드 + 기본값.

- [ ] **Step 2: 컴파일 확인**

클라 `refresh_unity` → 에러 없음.

- [ ] **Step 3: 통합 검증**

클·서 플레이 + LatencySimulation. HUD 관찰:
- `Enabled=true`(기본)일 때 조건 악화 시 prune/seqgap 발생 → `Lead`(기존 라벨)가 올라가고 prune→0 수렴, 호전 시 천천히 감소.
- `LeadState.Enabled`를 false로(코드/인스펙터) 두면 고정 30ms — A/B로 recon·prune 비교.

- [ ] **Step 4: 커밋 (Client)**

```bash
git add Assets/Scripts/Game/LOPTickUpdater.cs
git commit -m "feat(netcode): LOPTickUpdater reads dynamic AheadMargin from LeadState (Phase 4 Stage 2)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: 마무리 — 문서·메모리·머지

**Files:**
- Modify: `CL/docs/netcode-redesign.md` (Phase 4 완료 표기)

- [ ] **Step 1: netcode-redesign.md Phase 4 완료 표기**

`### Phase 4 — Time Dilation 피드백 루프 (선택)` 섹션에 완료 노트 추가(체크박스 채우고, 구현 요약 한 단락: 측정=서버 per-client `InputTimingTracker`, 전송=`InputTimingToC` 0.5초, 정책=클라 `LeadController` 오버워치 모양, 실행=`ClockDilator`, A=클라 정책 위치/DOTS, 임계값=Stage1 데이터 튜닝).

- [ ] **Step 2: 커밋 (Client)**

```bash
git add docs/netcode-redesign.md
git commit -m "docs(netcode): mark Phase 4 input timing feedback implemented

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: 4개 저장소 main에 --no-ff 머지**

각 저장소에서(GameFramework, LOP-Shared, Server, Client):
```bash
git checkout main
git merge --no-ff feature/netcode-phase4-input-timing-feedback -m "Merge feature/netcode-phase4-input-timing-feedback: input timing feedback + dynamic lead"
git branch -d feature/netcode-phase4-input-timing-feedback
```
> 보존 파일(클: Room.unity/UIRoot.prefab/PackageManagerSettings, 서: LOPGame/ConfigureRoomComponent 픽스처)은 커밋·머지 대상 아님 — 스테이징에서 제외 유지. 푸시는 사용자 요청 시.

- [ ] **Step 4: 메모리 갱신**

`netcode-migration-status.md`에 Phase 4 완료 entry 추가(측정/정책/실행 구조 + 4저장소 커밋).

---

## 검증 매트릭스 (구현 후)

- EditMode: `InputTimingTrackerTests`(4) + `LeadControllerTests`(6) 전부 PASS.
- 양쪽 컴파일 클린(클·서, GameFramework, LOP-Shared `file:` 반영).
- Stage 1: HUD에 `d avg/max/prune/seqgap` 라이브 표시, lead 고정 30ms(행동 무변).
- Stage 2: LatencySimulation 악조건에서 `LeadState.AheadMargin` 적응 → prune/seqgap 수렴, 토글로 고정 vs 동적 비교.

## Out of Scope (spec과 동일)

풀 예측/롤백(Stage④), 서버 `INPUT_DELAY_TICKS` 동적화, redundancy window 변경, lag compensation, 대규모 멀티 부하 튜닝.
