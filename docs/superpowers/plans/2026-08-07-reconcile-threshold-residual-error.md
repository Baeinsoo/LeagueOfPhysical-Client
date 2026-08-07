# 문턱 아래 잔류 오차 제거 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 재조정 문턱을 6cm → 1cm로 낮춰, 문턱 아래에서 영원히 방치되던 오차를 없앤다.

**Architecture:** 시뮬은 즉시 권위 값으로 보정하고, 부드러움은 이미 있는 렌더 스무더가 계속 담당한다(언리얼 캡슐/메시 분리). 새 스무딩 메커니즘은 만들지 않는다. 본체는 상수 하나이며, 그 변경의 비용을 재기 위한 카운터를 **먼저** 넣어 기준선을 확보한다.

**Tech Stack:** Unity 6 · C# · VContainer · UI Toolkit · Mirror

## Global Constraints

- **spec:** `docs/superpowers/specs/2026-08-07-reconcile-threshold-residual-error-design.md`. 이 계획은 그 spec을 구현한다.
- **작업 위치:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` — **main 체크아웃**, 브랜치 `feature/reconcile-threshold`(이미 생성돼 있고 spec 커밋이 올라가 있다).
  - ⚠️ **워크트리를 만들지 말 것.** 연결된 Unity 클라 에디터가 이 main 체크아웃을 본다. 워크트리로 가면 컴파일도 플레이 검증도 불가능해진다. 이 슬라이스는 플레이 검증이 유일한 검증 수단이라 치명적이다.
- **클라 저장소만 건드린다.** GameFramework·서버·Shared 변경 없음.
- **`main`에 직접 커밋 금지.**
- **커밋하면 안 되는 로컬 변경이 있다.** 작업 시작 시점에 이미 dirty한 것들이며 전부 사용자의 로컬 설정이다:
  `.gitignore` · `Assets/Art`(서브모듈 포인터) · `Assets/Scenes/Room.unity`(LatencySimulation `latency: 150` 테스트 설정) · `ProjectSettings/QualitySettings.asset`.
  **절대 `git add .`를 쓰지 말고 파일을 하나씩 지정해 스테이징할 것.**
- **UnityMCP: 모든 호출에 `unity_instance`를 명시한다.** 서버·클라 에디터가 동시에 붙어 있다. 인스턴스 id는 MCP 리소스 `mcpforunity://instances`에서 `name`이 `LeagueOfPhysical-Client`인 항목의 전체 `id`(`Name@hash`)를 쓴다. 해시는 바뀔 수 있으니 하드코딩 금지.
  - 리소스 도구를 못 불러오면 UnityMCP 로그 파일에서 해시를 얻고, 사이드 특정적인 호출(클라 전용 에셋 검색 등)로 교차 확인한다.
  - `read_console`은 좁은 `types` 필터에서 줄이 안 보이는 경우가 관측됐다. 비어 보이면 `types=["all"]`로 다시 읽을 것.
  - 스크립트 변경 후 컴파일 중에는 **MCP 연결이 일시적으로 끊길 수 있다**(이 프로젝트에서 반복 관측됨). 인스턴스 목록을 다시 읽어 재연결을 기다린 뒤 진행한다.
- **주석 컨벤션:** 코드로 자명한 것에는 주석을 달지 않는다. 비자명한 *의도(왜)* 만 일상어 한국어로 짧게. 전문용어는 그 자리에서 풀어 쓴다.
- **명명:** 새 식별자는 업계 표준 어휘를 따른다. 이 슬라이스에서 쓰는 용어는 **correction**(서버 권위로 클라 상태를 되돌리는 사건)이다 — 언리얼 CMC의 *client adjustment / correction*과 대응한다.
- **`.meta` 파일:** 이 슬라이스는 새 파일을 만들지 않으므로 새 `.meta`가 생기면 안 된다.

---

## 파일 구조

| 파일 | 책임 | Task |
|---|---|---|
| **수정** `Assets/Scripts/Netcode/ReconciliationStats.cs` | 보정 횟수 카운터 추가 | 1 |
| **수정** `Assets/Scripts/Netcode/Reconciler.cs` | 보정 발생 시 카운트 / 문턱 상수 | 1, 2 |
| **수정** `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` | 카운터 노출 + `DumpStats`에 포함 | 1 |
| **수정** `Assets/Scripts/UI/DebugHud/DebugHudView.cs` | Recon max 라벨에 함께 표시 | 1 |
| **수정** `docs/ROADMAP.md` | 결과 기록 | 3 |

> UXML은 건드리지 않는다. 새 라벨을 만들지 않고 기존 `recon-max-text` 한 줄에 붙인다 —
> UXML 이름과 `Q<Label>` 조회가 어긋나면 컴파일 에러가 아니라 **런타임 null**이라, 이 프로젝트에서
> 가장 위험한 실패 모드다. 요소를 안 늘리면 그 위험이 0이다.

---

### Task 1: 보정 횟수 카운터 (기준선 확보)

문턱을 바꾸기 **전에** 넣는다. 지금(6cm) 보정이 얼마나 일어나는지를 알아야 바꾼 뒤와 비교할 수 있다.

**Files:**
- Modify: `Assets/Scripts/Netcode/ReconciliationStats.cs`
- Modify: `Assets/Scripts/Netcode/Reconciler.cs`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudView.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `ReconciliationStats.CorrectionCount { get; }` · `ReconciliationStats.RecordCorrection()` ·
  `DebugHudViewModel.CorrectionCount { get; }`

- [ ] **Step 1: `ReconciliationStats`에 카운터 추가**

`Assets/Scripts/Netcode/ReconciliationStats.cs` — `Max` 프로퍼티 아래에 추가:

```csharp
        /// <summary>서버 권위로 되돌린(롤백+재생) 횟수. 문턱을 낮출 때 늘어나는 비용을 재려고 센다.</summary>
        public int CorrectionCount { get; private set; }
```

`Record` 메서드 아래에 추가:

```csharp
        public void RecordCorrection()
        {
            CorrectionCount++;
        }
```

`Reset()` 안에 한 줄 추가(기존 초기화들 옆):

```csharp
            CorrectionCount = 0;
```

- [ ] **Step 2: 보정이 실제로 일어난 자리에서 카운트**

`Assets/Scripts/Netcode/Reconciler.cs`에서 **하드 복원 직전**에 호출한다. 게이트를 통과해 실제로
되돌리는 시점이라 "보정이 일어났다"의 정의와 정확히 맞는다.

`// 하드 복원: ...` 주석으로 시작하는 블록 **바로 위**에 추가:

```csharp
            // 게이트를 통과했다 = 실제로 되돌린다. 여기서 세야 "스킵"과 "보정"이 정확히 갈린다.
            reconciliationStats.RecordCorrection();
```

> 주의: `MaxReplayTicks` 초과로 재생을 생략하는 텔레포트 경로도 이 카운트에 포함된다.
> 그 경로도 "서버 권위로 되돌린" 사건이므로 의도된 것이다.

- [ ] **Step 3: ViewModel에 노출**

`Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`에서 `ReconMax` 프로퍼티 아래에 추가:

```csharp
        public int CorrectionCount => reconciliationStats.CorrectionCount;
```

같은 파일 `DumpStats()`의 로그 문자열에서 `reconLast={ReconLast:F3}` 뒤에 이어 붙인다:

```csharp
                      $" corrections={CorrectionCount}" +
```

- [ ] **Step 4: HUD 표시**

`Assets/Scripts/UI/DebugHud/DebugHudView.cs`의 `Refresh`에서 Recon max 줄을 교체:

```csharp
            _reconMaxText.text = $"Recon max: {_viewModel.ReconMax:F2} m (corr {_viewModel.CorrectionCount})";
```

- [ ] **Step 5: 컴파일 확인**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true,
                             unity_instance="LeagueOfPhysical-Client@<hash>")
mcp__UnityMCP__read_console(action="get", types=["error"], count=10, format="plain",
                            unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: **에러 0.** 타임아웃이 나면 인스턴스 목록을 다시 읽어 재연결을 기다린 뒤, 가벼운
`refresh_unity(mode="if_dirty", compile="none", wait_for_ready=true)`로 `resulting_state: "idle"`을 확인하고
콘솔을 다시 읽는다.

- [ ] **Step 6: 기준선 측정 (문턱 6cm 상태)**

서버·클라를 띄워 게임에 입장한 뒤:

1. 빈 곳으로 이동
2. HUD `Reset stats`
3. **60초 동안 계속 걷기**(방향 전환 섞어서 — 걷기가 이 문제의 자극이다. 점프는 안 해도 된다)
4. `Dump`

기록: `[HudDump]` 줄에서 `corrections=`, `reconMax=`, `fps=`.
그리고 `[ReconSpike]` 로그에서 **같은 `delta`가 반복되는지**.

> Dump 전에 `Snap gap avg`가 20ms 근처인지 확인할 것. `0.0`이면 연결이 끊긴 것이라 그 측정은 무효다.

이 수치가 Task 3의 비교 대상이다. **반드시 기록하고 넘어간다.**

**측정 결과 (2026-08-07, 문턱 6cm):**

```
[HudDump] elapsed=124.3 fps=60 frameMs=16.7 entities=13
          reconMax=0.400 reconAvg=0.000 reconLast=0.000 corrections=5
          snapLag=5 snapGapAvg=20.0 snapGapMax=333.3 cushion=72.3
          rtt=207 lead=8 dAvg=-3.6 dMax=-2 prune=0 seqGap=0
```

`[ReconSpike]`: **`delta=(0.040, 0.000, -0.002)`가 12건 연속 동일** — 4cm 잔류가 그대로 재현됐다.
첫 건에서 `predVel=(-3.00, 0.15)` vs `srvVel=(-1.00, 0.05)`, 차이가 정확히 `2.00 m/s`
(= `maxAcceleration × dt`)로 한 틱 제동이 확인됨.

조건: 환경 `local`, `latency: 150` / `unreliableLoss: 2`, 60초 걷기, Snap gap avg 20.0(유효).

- [ ] **Step 7: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git status --short
git add Assets/Scripts/Netcode/ReconciliationStats.cs Assets/Scripts/Netcode/Reconciler.cs \
        Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/UI/DebugHud/DebugHudView.cs
git commit -m "feat(diagnostics): 보정 횟수 카운터

문턱을 낮추면 롤백·재생이 얼마나 늘어나는지 짐작하지 말고 재려고 넣는다.
문턱을 바꾸기 전에 기준선을 잡아야 비교가 성립한다."
```

`git status`로 로컬 변경 4개(.gitignore·Art·Room.unity·QualitySettings)가 **스테이징되지 않았는지** 확인한다.

---

### Task 2: 문턱 교정 (6cm → 1cm)

이 슬라이스의 본체.

**Files:**
- Modify: `Assets/Scripts/Netcode/Reconciler.cs:13`

**Interfaces:**
- Consumes: Task 1의 `CorrectionCount`(측정용)
- Produces: 없음

- [ ] **Step 1: 상수 교체**

`Assets/Scripts/Netcode/Reconciler.cs`의 문턱 선언을 교체:

```csharp
        // 이 이하 오차는 롤백 스킵. 문턱은 "스냅이냐 점진이냐"가 아니라 "고치느냐 마느냐"를 가르므로
        // 그 아래는 영원히 안 고쳐진다 — 잡음을 거를 만큼만 작게 잡는다.
        // 6cm였을 때, 입력 한 틱 누락이 만든 4cm 오차가 정지 중에도 45틱 넘게 그대로 남는 것이 관측됐다.
        // 클·서가 같은 코드를 돌아 정상 구간 오차는 정확히 0이므로 거를 잡음 자체가 거의 없다.
        private const float Threshold = 0.01f;
```

- [ ] **Step 2: 컴파일 확인**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true,
                             unity_instance="LeagueOfPhysical-Client@<hash>")
mcp__UnityMCP__read_console(action="get", types=["error"], count=10, format="plain",
                            unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Netcode/Reconciler.cs
git commit -m "fix(netcode): 재조정 문턱 6cm → 1cm

문턱 아래 오차를 줄이는 경로가 없어서, 입력 한 틱 누락이 만든 4cm가 정지
중에도 네트워크 회복 후에도 영구히 남았다(45틱 연속 동일 delta 관측).

표준에는 방치 구간이 없다 — 문턱은 스냅이냐 점진이냐를 가르는 선이다.
우리 구조에선 '점진'을 렌더 스무더가 이미 맡고 있으므로 시뮬은 즉시
보정하면 된다(언리얼 캡슐/메시 분리와 같은 모양)."
```

---

### Task 3: 검증 + 기록

**Files:**
- Modify: `docs/ROADMAP.md`

**Interfaces:**
- Consumes: Task 1·2 전부
- Produces: 판정 결과

- [ ] **Step 1: 고친 뒤 측정 (문턱 1cm 상태)**

Task 1 Step 6과 **완전히 같은 절차**로 반복한다 — 같은 자리, 같은 60초 걷기, 같은 기록 항목.

> 조건을 하나만 바꿔야 비교가 성립한다. 걷는 경로·시간·서버 상태를 최대한 같게 유지할 것.
> Dump 전 `Snap gap avg`≈20ms 확인은 이번에도 필수다.

- [ ] **Step 2: 판정**

| 항목 | 기준선(6cm) | 수정 후(1cm) | 판정 |
|---|---|---|---|
| 같은 `delta` 반복 | 45틱 연속 관측됨 | **1~2회 찍고 사라져야 함** | ← **단일 판정 기준** |
| `corrections` | (Task 1 Step 6 기록) | 늘어남은 정상. **상시로 치솟으면 문제** | 비용 |
| `fps` | (기록) | 유의미한 하락 없어야 함 | 비용 |
| 체감 | — | 조작감 변화 없음, 화면 톡톡 튐 없음 | 부작용 |

**성공 = "같은 delta가 계속 찍히는 현상이 사라졌다."** 나머지는 부작용 점검이다.

판정이 실패하거나 애매하면 **멈추고 보고한다.** 상수를 임의로 더 조정하지 말 것 —
값을 바꾸는 건 사용자 결정이다.

- [ ] **Step 3: 부작용 점검**

플레이하며 확인:

- 걷기·정지·방향전환에서 **조작감이 달라졌는가**(끌리는 느낌, 미세한 튐)
- 화면이 **톡톡 튀는가**(1~2.5cm 보정은 렌더가 스냅하므로 여기서 드러난다면 `minCorrection` 후속 대상)
- 연출(데미지 숫자·이펙트)이 **중복되거나 누락되는가**(롤백이 잦아지며 재생 억제 경로를 더 자주 지난다)

관측된 것을 전부 기록한다. 문제가 보이면 고치지 말고 **보고**한다 — 별도 판단이 필요하다.

- [ ] **Step 4: ROADMAP 기록**

`docs/ROADMAP.md`의 "Recon 러버밴딩 원인 규명" 절 **아래에** 대응 슬라이스 결과를 잇는다.
포함할 것:

- 기준선 vs 수정 후 수치 표(2절)
- 판정 한 문장
- 부작용 점검 결과(3절) — 없으면 "없음"이라고 명시
- 남은 후속: 입력 미스 자체를 줄이는 슬라이스(옵션 A / `LeadController.minMargin`), 필요 시 렌더 `minCorrection`

- [ ] **Step 5: 커밋 + 머지**

```bash
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 문턱 교정 결과 기록"
git checkout main
git merge --no-ff feature/reconcile-threshold -m "Merge feature/reconcile-threshold: 문턱 아래 잔류 오차 제거"
git log --oneline -1
```

---

## 완료 조건

- [ ] 클라 컴파일 에러 0
- [ ] 기준선(6cm)과 수정 후(1cm) 측정이 **같은 절차로** 각각 기록됐다
- [ ] `[ReconSpike]`에서 같은 delta가 반복되는 현상이 **사라졌다**
- [ ] `corrections`·`fps`로 비용이 확인됐고 상시 치솟지 않는다
- [ ] 부작용 점검 결과가 기록됐다
- [ ] ROADMAP에 결과가 남았고 브랜치가 main에 머지됐다
