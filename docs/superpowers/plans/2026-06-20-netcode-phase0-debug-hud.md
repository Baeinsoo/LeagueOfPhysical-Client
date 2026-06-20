# Netcode Phase 0 — 측정 HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 `DebugHud`에 추정 서버 tick, lead(클라−서버 tick), reconciliation distance(last/avg/max)를 추가해 netcode 튜닝 baseline 측정 도구를 만든다.

**Architecture:** 신규 `ReconciliationStats`(DI Singleton 홀더)에 `SnapReconciler`가 보정 거리를 write하고 `DebugHudViewModel`이 read. 서버 tick·lead·RTT는 VM이 `NetworkTime`/`GameEngine`에서 직접 계산. 기존 DebugHud의 pull 패턴(`schedule.Execute(Refresh)`)·UI Toolkit MVVM 구조를 그대로 확장. 클라 전용.

**Tech Stack:** C# (Unity), UI Toolkit (UXML/USS), VContainer DI, R3 미사용(샘플링 값이라 pull), UnityMCP 컴파일 검증. 자동 테스트 없음(클라 단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Related spec:** `docs/superpowers/specs/2026-06-20-netcode-phase0-debug-hud-design.md`

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Client — `mcpforunity://instances`에서 `LeagueOfPhysical-Client` 인스턴스의 `id`(`Name@hash`) 해석 후 사용(현재 `LeagueOfPhysical-Client@de70658b9450cbb4`, hash 변동 가능). 클라 전용 작업 — 서버 인스턴스 건드리지 말 것.

> **브랜치:** 클라는 이미 `feature/netcode-phase0-debug-hud`(spec 커밋됨). 새 브랜치 생성 불필요 — 그 브랜치에서 작업. 세션 시작 시점 무관 dirty(`UIRoot.prefab`/`PackageManagerSettings.asset`, 미추적 plan들)는 건드리지 말 것.

> **`git commit` 전 `.git/index.lock` 주의:** 이 repo에서 stale lock이 간헐 발생. 커밋 실패 시 `rm -f .git/index.lock` 후 재시도.

---

## File Structure

- **Create:** `Assets/Scripts/Game/ReconciliationStats.cs` — 순수 C# DI Singleton 홀더(Last/Max/Average + Record).
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — `ReconciliationStats` Singleton 등록.
- **Modify:** `Assets/Scripts/Entity/SnapReconciler.cs` — `[Inject] ReconciliationStats` + `Reconcile()`에서 distance Record.
- **Modify:** `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` — ServerTickEstimate/Lead/ReconLast/ReconAverage/ReconMax getter.
- **Modify:** `Assets/Scripts/UI/DebugHud/DebugHudView.cs` — 라벨 3개 바인딩 + Refresh pull.
- **Modify:** `Assets/UI/DebugHud/DebugHud.uxml` — 라벨 3개 추가.

Task 1(홀더+DI+write)을 먼저 완성해 컴파일 통과시키면 데이터 소스가 살아나고, Task 2(VM)·Task 3(View/uxml)이 그 위에 표시를 얹는다. 각 Task 끝에 컴파일.

---

## Task 1: ReconciliationStats 홀더 + DI 등록 + 리컨실러 write

**Files:**
- Create: `Assets/Scripts/Game/ReconciliationStats.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Entity/SnapReconciler.cs`

- [ ] **Step 1: `ReconciliationStats.cs` 생성**

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// netcode 측정용 reconciliation 통계 홀더(클라). SnapReconciler가 매 보정 시 distance를
    /// Record하고, DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다.
    /// </summary>
    public class ReconciliationStats
    {
        private const int WindowSize = 60;
        private readonly Queue<float> _window = new Queue<float>(WindowSize);
        private float _sum;

        public float Last { get; private set; }
        public float Max { get; private set; }
        public float Average { get; private set; }

        public void Record(float distance)
        {
            Last = distance;
            if (distance > Max)
            {
                Max = distance;
            }

            _window.Enqueue(distance);
            _sum += distance;
            if (_window.Count > WindowSize)
            {
                _sum -= _window.Dequeue();
            }
            Average = _sum / _window.Count;
        }
    }
}
```

- [ ] **Step 2: `GameLifetimeScope.cs`에 Singleton 등록** — `DebugHudViewModel` Transient 등록(현 line 64 부근) 근처에 추가:
```csharp
            builder.Register<ReconciliationStats>(Lifetime.Singleton);
```
(이미 `using VContainer;` 있음. `DebugHudViewModel`/`DebugHudView` 등록 라인 바로 위/아래 아무 곳.)

- [ ] **Step 3: `SnapReconciler.cs`에 주입 필드 추가** — 기존 `[Inject] private IGameEngine gameEngine;`(line 12-13) 아래에:
```csharp
        [Inject]
        private ReconciliationStats reconciliationStats;
```

- [ ] **Step 4: `SnapReconciler.Reconcile()`에서 Record** — 현 `float distance = (position - targetPosition).magnitude;`(line 127) 바로 다음 줄에 추가:
```csharp
            float distance = (position - targetPosition).magnitude;
            reconciliationStats.Record(distance);
```
(그 외 Reconcile 로직 무변경. `serverEntitySnaps.Count == 0` early-return 시엔 여기 도달 안 함 = 보정 없는 틱 미집계 — 의도된 동작.)

- [ ] **Step 5: 클라 컴파일 0에러**
- 클라 instance id 해석: `mcpforunity://instances` 읽어 `LeagueOfPhysical-Client`의 `id`.
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")`
- `read_console(action="get", types=["error"], unity_instance="<클라 id>")` → 0 errors.

- [ ] **Step 6: 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/ReconciliationStats.cs Assets/Scripts/Game/ReconciliationStats.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Entity/SnapReconciler.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(netcode): ReconciliationStats holder + SnapReconciler records reconciliation distance

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`.meta`는 Unity가 생성 — Step 5 compile 후 존재. lock 에러 시 `rm -f .git/index.lock` 후 재시도. 무관 dirty 파일 stage 금지.)

---

## Task 2: DebugHudViewModel getter 추가

**Files:**
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`

- [ ] **Step 1: getter + 주입 필드 추가** — 현 파일 전체를 아래로 교체(기존 IsRunning/Tick/ElapsedTime/RttMs 유지 + 신규 추가):

```csharp
using GameFramework;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD ViewModel. tick·경과시간·RTT·서버tick추정·lead·reconciliation은 변경을
    /// 통지하는 이벤트 소스가 없는 샘플링 값이라 R3(push) 대신 평범한 getter로 노출하고,
    /// View가 매 프레임 pull한다. reconciliation 값은 ReconciliationStats(SnapReconciler가 write)에서 읽는다.
    /// </summary>
    public class DebugHudViewModel
    {
        [Inject]
        private ReconciliationStats reconciliationStats;

        public bool IsRunning => GameEngine.current != null;

        public long Tick => GameEngine.Time.tick;

        public double ElapsedTime => GameEngine.Time.elapsedTime;

        public double RttMs => Mirror.NetworkTime.rtt * 1000;

        public long ServerTickEstimate => (long)(Mirror.NetworkTime.time / GameEngine.Time.tickInterval);

        public long Lead => GameEngine.Time.tick - ServerTickEstimate;

        public float ReconLast => reconciliationStats.Last;

        public float ReconAverage => reconciliationStats.Average;

        public float ReconMax => reconciliationStats.Max;
    }
}
```
(`ReconciliationStats`는 `LOP` 네임스페이스, 이 파일은 `LOP.UI`라 `LOP.`가 보임 — 추가 using 불필요. View가 `IsRunning`일 때만 Refresh하므로 `tickInterval > 0` 보장.)

- [ ] **Step 2: 클라 컴파일 0에러** — Task 1 Step 5와 동일(클라 instance 핀, refresh_unity + read_console → 0 errors).

- [ ] **Step 3: 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(netcode): DebugHudViewModel exposes server tick estimate, lead, reconciliation stats

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: DebugHudView 라벨 바인딩 + uxml

**Files:**
- Modify: `Assets/UI/DebugHud/DebugHud.uxml`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudView.cs`

- [ ] **Step 1: uxml에 라벨 3개 추가** — `Assets/UI/DebugHud/DebugHud.uxml`의 `debug-panel` 안, 기존 `rtt-text` 다음에 추가. 전체를 아래로 교체:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="DebugHud.uss" />
    <ui:VisualElement name="debug-hud-root" class="debug-hud-root" picking-mode="Ignore">
        <ui:VisualElement name="debug-panel" class="debug-panel" picking-mode="Ignore">
            <ui:Label name="tick-text" class="debug-text" text="Tick: 0" />
            <ui:Label name="server-tick-text" class="debug-text" text="Server: 0" />
            <ui:Label name="lead-text" class="debug-text" text="Lead: 0" />
            <ui:Label name="elapsed-text" class="debug-text" text="elapsed: 0.00" />
            <ui:Label name="rtt-text" class="debug-text" text="RTT: 0" />
            <ui:Label name="recon-text" class="debug-text" text="Recon: 0.00 / avg 0.00 / max 0.00" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```
(uss 무변경 — 기존 `.debug-text` 재사용.)

- [ ] **Step 2: `DebugHudView.cs` 라벨 필드 + 바인딩 + Refresh** — 전체를 아래로 교체:

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD View. 화면 고정(Window 밴드, 표시 전용 / picking 통과).
    /// ViewModel에서 매 프레임 값을 pull해 라벨을 갱신한다(이벤트 없는 샘플링 값이라 R3 미사용).
    /// </summary>
    public class DebugHudView : UIView
    {
        private readonly DebugHudViewModel _viewModel;

        private Label _tickText;
        private Label _serverTickText;
        private Label _leadText;
        private Label _elapsedText;
        private Label _rttText;
        private Label _reconText;

        private IVisualElementScheduledItem _tick;

        public DebugHudView(DebugHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _tickText = Root.Q<Label>("tick-text");
            _serverTickText = Root.Q<Label>("server-tick-text");
            _leadText = Root.Q<Label>("lead-text");
            _elapsedText = Root.Q<Label>("elapsed-text");
            _rttText = Root.Q<Label>("rtt-text");
            _reconText = Root.Q<Label>("recon-text");

            _tick = Root.schedule.Execute(Refresh).Every(0);
        }

        private void Refresh(TimerState _)
        {
            if (!_viewModel.IsRunning)
            {
                return;
            }

            _tickText.text = $"Tick: {_viewModel.Tick}";
            _serverTickText.text = $"Server: {_viewModel.ServerTickEstimate}";
            _leadText.text = $"Lead: {_viewModel.Lead}";
            _elapsedText.text = $"elapsed: {_viewModel.ElapsedTime:F2}";
            _rttText.text = $"RTT: {_viewModel.RttMs:F0}";
            _reconText.text = $"Recon: {_viewModel.ReconLast:F2} / avg {_viewModel.ReconAverage:F2} / max {_viewModel.ReconMax:F2}";
        }

        public override void Dispose()
        {
            _tick?.Pause();
            base.Dispose();
        }
    }
}
```

- [ ] **Step 3: 클라 컴파일 0에러** — Task 1 Step 5와 동일.

- [ ] **Step 4: 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/UI/DebugHud/DebugHud.uxml Assets/Scripts/UI/DebugHud/DebugHudView.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(netcode): DebugHud shows server tick, lead, reconciliation distance (Phase 0 HUD)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 런타임 수동 검증 (사용자)

클라 플레이:
1. **HUD 6항목 표시** — Tick / Server / Lead / elapsed / RTT / Recon.
2. **이동·공중 점프** — Recon last가 튀고, Max가 최악값 유지, avg가 최근 흐름 반영.
3. **Lead ≈ 0** — 현재(clock sync 전) 0 부근. 큰 음수/양수 아님.
4. **Server tick ≈ 클라 tick** — 같은 epoch 확인.
5. **콘솔 에러 0** — 0-나눗셈/NRE 없음.

baseline: 공중 점프 시나리오 반복 후 Recon max/avg 기록(이후 Phase 2 비교용).

---

## 완료 기준

- [ ] 클라 `feature/netcode-phase0-debug-hud`에 커밋 3개(홀더+DI+write / VM / View+uxml).
- [ ] 클라 컴파일 0에러. GameFramework/서버 무변경.
- [ ] 무관 dirty(`UIRoot.prefab` 등) 미커밋 보존.
- [ ] 수동 플레이: HUD 6항목, Recon 변동, Lead≈0, 에러 0.

이후: 사용자 검증 후 클라 `feature/netcode-phase0-debug-hud` → main `--no-ff` 머지(spec 커밋도 같은 브랜치라 함께). netcode Phase 0 완료 — 이후 Phase 2(clock sync)부터 이 HUD로 before/after 측정.

## 구현 중 조정 (런타임 검증 폴리시 — Task 3 이후)

- 검증 중 `recon-text` 한 라벨이 패널(150px)에서 잘림 → **3 라벨로 분리**(`recon-last-text`/`recon-avg-text`/`recon-max-text`), 패널 폭 **150 → 200px**, 라벨 문구·단위 명시(`Client tick`/`Server tick`/`Lead: N tick`/`Elapsed: N s`/`RTT: N ms`/`Recon last|avg|max: N m`). View/uxml/uss만 조정, VM/홀더 무변경. 별도 커밋으로 동일 브랜치에 포함.
