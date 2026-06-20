# Netcode Phase 0 — 측정 HUD (서버 tick 추정 + lead + reconciliation distance)

**Date:** 2026-06-20
**Branch (docs):** `feature/netcode-phase0-debug-hud` (클라 repo — 설계 허브)
**Branch (code):** 클라 repo 피처 브랜치 (구현 시 생성)
**Related:** [Netcode 재설계](../../netcode-redesign.md) (Phase 0) · [Netcode Phase 1 spec](2026-06-20-netcode-phase1-rotation-snap-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [아키텍처 가이드라인](../../architecture-guidelines.md) (UI)

## Goal

`netcode-redesign.md` Phase 0의 **측정 HUD**를 구현한다. 이후 모든 netcode 튜닝(Phase 2 clock sync 등)의 **baseline 측정 도구**로, 현재는 눈으로만 보이는 reconciliation 갭을 **숫자로** 만든다. 기존 클라 `DebugHud`(이미 tick/elapsed/RTT 표시)에 **추정 서버 tick**, **lead(클라−서버 tick)**, **reconciliation distance(last/avg/max)** 를 추가한다.

핵심 메트릭은 **reconciliation distance** — 서버 스냅샷 보정이 클라 위치를 끌어당기는 거리, 즉 화면에서 보이는 "튐"의 크기 그 자체다. 서버 tick·lead는 그 갭을 해석하는 보조 컨텍스트(클라가 서버 시간선보다 얼마나 앞서는가 = Phase 2가 조정할 양).

## 배경 / 조사 (구현 사실)

- **기존 클라 DebugHud**: UI Toolkit MVVM, **pull 방식**(이벤트 소스 없는 샘플링 값이라 R3 대신 `schedule.Execute(Refresh).Every(0)` 폴링). `DebugHudViewModel`이 `GameEngine.Time.tick`/`elapsedTime`/`NetworkTime.rtt`를 getter로 노출, `DebugHudView`가 매 프레임 pull. `DebugHudViewModel`/`DebugHudView` 모두 `GameLifetimeScope`에 Transient 등록, `PlayerHudCoordinator`가 `windowManager.Open<DebugHudView>()`로 오픈. uxml: `Assets/UI/DebugHud/DebugHud.uxml`(+uss), 루트 `picking-mode="Ignore"`(표시 전용).
- **reconciliation distance 소스**: `SnapReconciler.Reconcile()`의 지역 변수 `float distance = (position - targetPosition).magnitude`(현 line 127, SmoothDamp 보정 *전*의 갭). 현재 노출 안 됨.
- **SnapReconciler는 per-entity**: `CharacterCreator.cs:62`에서 로컬 플레이어 엔티티에 `AddComponent<SnapReconciler>()`. 이미 `[Inject] IGameEngine`을 받는 컨테이너 주입 MonoBehaviour라 추가 `[Inject]`도 동일 배선. → HUD VM(`GameEngine` statics만 읽음)이 리컨실러에 직접 못 닿으므로 **둘을 잇는 DI 홀더** 필요.
- **클라 tick ↔ 서버 tick epoch**: `LOPRoom.cs:145`에서 `game.Run(gameInfo.Tick + 1, gameInfo.Interval, gameInfo.ElapsedTime)`로 클라 tick을 **서버 tick으로 시드**, `LOPTickUpdater`가 `elapsedTime`을 `NetworkTime.time`으로 smooth하며 tick은 `elapsedTime/interval`(`TickUpdaterBase.processibleTick`)로 전진. → 클라 tick과 서버 tick은 **같은 epoch**(서버 게임 시간 = NetworkTime). 따라서 `클라 tick − NetworkTime.time/interval = lead`가 의미 있는 값. **현재 lead ≈ 0**(클라가 서버 시간선 위에서 달림, Phase 2 미적용) — 이걸 숫자로 확인하는 게 baseline의 핵심 발견.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 핵심 메트릭 | **reconciliation distance**(last/avg/max) | 보정이 끌어당긴 거리 = 보이는 튐의 크기. baseline 비교의 본 지표 |
| ② | 서버 tick | **추정 현재 서버 tick** = `(long)(NetworkTime.time / tickInterval)` | 문서가 "서버 tick(추정)"으로 명시. 마지막 수신 snap tick은 ~편도지연만큼 과거라 lead가 지연과 섞임. 추정치는 `클라 tick − 추정` = 순수 lead |
| ③ | lead 표시 | `클라 tick − 추정 서버 tick`도 함께 | Phase 2(clock sync)가 조정하는 바로 그 양. 현재 ≈0 관측 |
| ④ | 텔레메트리 배선 | **DI Singleton 홀더 `ReconciliationStats`** — 리컨실러 write, HUD VM read | 서버 tick·lead·RTT는 VM이 NetworkTime/GameEngine에서 직접 계산(홀더 불필요). 홀더는 per-entity 리컨실러의 distance만 전달 |
| ⑤ | avg/max | 홀더가 **롤링 평균(최근 N) + 세션 Max** | 문서 "평균/최대" 충족. 롤링 평균은 시나리오 중 최근값 반영(idle 희석 회피), Max는 최악 갭 포착. reset UI 불필요 |
| ⑥ | reset | 없음 — 게임 재시작이 리셋(홀더가 게임 스코프 Singleton) | YAGNI. picking-ignore 표시 전용 HUD에 인터랙티브 버튼 안 넣음 |
| ⑦ | 범위 | **클라 전용, 기존 HUD 확장** | reconciliation은 클라 개념. 서버 무관 |
| ⑧ | 바인딩 | **pull 유지**(R3 아님) | 기존 DebugHud 패턴. 샘플링 값(이벤트 소스 없음) → 폴링이 정석(아키텍처 가이드라인) |

## Scope (In) — 클라 전용

### 1. 신규 `Assets/Scripts/Game/ReconciliationStats.cs`

순수 C# DI Singleton. 리컨실러가 write, HUD VM이 read.

```csharp
namespace LOP
{
    /// <summary>
    /// netcode 측정용 reconciliation 통계 홀더(클라). SnapReconciler가 매 보정 시 distance를
    /// Record하고, DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다.
    /// </summary>
    public class ReconciliationStats
    {
        private const int WindowSize = 60;          // 롤링 평균 표본 수(≈리컨실 60틱)
        private readonly Queue<float> _window = new Queue<float>(WindowSize);
        private float _sum;

        public float Last { get; private set; }
        public float Max { get; private set; }
        public float Average { get; private set; }

        public void Record(float distance)
        {
            Last = distance;
            if (distance > Max) Max = distance;

            _window.Enqueue(distance);
            _sum += distance;
            if (_window.Count > WindowSize) _sum -= _window.Dequeue();
            Average = _sum / _window.Count;
        }
    }
}
```

### 2. `Assets/Scripts/Entity/SnapReconciler.cs` (수정)

- `[Inject] private ReconciliationStats reconciliationStats;` 추가.
- `Reconcile()`에서 `float distance = (position - targetPosition).magnitude;`(현 line 127) 직후 `reconciliationStats.Record(distance);` 추가. (그 외 로직 무변경. `serverEntitySnaps.Count == 0` early-return 시엔 Record 안 함 = 보정 없는 틱은 미집계.)

### 3. `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` (수정)

getter 추가(기존 pull 패턴):

```csharp
[Inject] private ReconciliationStats reconciliationStats;   // 클래스 필드

public long ServerTickEstimate => (long)(Mirror.NetworkTime.time / GameEngine.Time.tickInterval);
public long Lead => GameEngine.Time.tick - ServerTickEstimate;
public float ReconLast => reconciliationStats.Last;
public float ReconAverage => reconciliationStats.Average;
public float ReconMax => reconciliationStats.Max;
```

> VM은 현재 무인자 클래스라 `[Inject]` 필드 주입을 쓴다(VContainer가 Transient 생성 시 채움). `tickInterval == 0` 분모 보호: `IsRunning`(= `GameEngine.current != null`)일 때만 View가 Refresh하므로 게임 구동 후엔 interval>0. 방어적으로 0이면 ServerTickEstimate=0 취급(아래 View가 IsRunning 가드).

### 4. `Assets/Scripts/UI/DebugHud/DebugHudView.cs` (수정)

라벨 3개 추가 + Refresh에서 pull:

```csharp
private Label _serverTickText;
private Label _leadText;
private Label _reconText;
// OnOpen: Root.Q<Label>("server-tick-text") 등 바인딩
// Refresh:
_serverTickText.text = $"Server: {_viewModel.ServerTickEstimate}";
_leadText.text = $"Lead: {_viewModel.Lead}";
_reconText.text = $"Recon: {_viewModel.ReconLast:F2} / avg {_viewModel.ReconAverage:F2} / max {_viewModel.ReconMax:F2}";
```

### 5. `Assets/UI/DebugHud/DebugHud.uxml` (수정)

`debug-panel` 안에 라벨 3개 추가(`server-tick-text`, `lead-text`, `recon-text`), `class="debug-text"`. uss는 기존 `.debug-text` 재사용(신규 스타일 불필요 — 필요 시 recon만 강조 클래스 추가는 선택).

### 6. `Assets/Scripts/Game/GameLifetimeScope.cs` (수정)

```csharp
builder.Register<ReconciliationStats>(Lifetime.Singleton);
```
(기존 `DebugHudViewModel`/`DebugHudView` Transient 등록 옆.)

## 데이터 흐름

```
SnapReconciler.Reconcile() (매 End 틱, 서버 snap 있을 때):
   distance = |position − targetPosition|         (보정 전 갭)
   reconciliationStats.Record(distance)            (Last/Max/Average 갱신)
                    │
DebugHudView.Refresh (매 렌더 프레임 pull):
   Tick   = GameEngine.Time.tick
   Server = NetworkTime.time / tickInterval        (추정)
   Lead   = Tick − Server                          (현재 ≈0)
   Recon  = stats.Last / Average / Max
   RTT    = NetworkTime.rtt * 1000
```

## 사용 (baseline 측정 절차 — 문서 Phase 0)

1. 플레이 진입 → HUD 표시 확인.
2. **공중 점프 시나리오** 수동 반복(낙하 중 점프 등 — 갭이 큰 케이스).
3. **Recon max / avg** 읽음 = baseline. (Lead ≈0 확인 — 아직 clock sync 없음.)
4. 이후 Phase 2+ 적용 후 같은 시나리오 재측정 → baseline과 비교.

## Out of scope (defer)

- 공중 점프 시나리오 **자동화** — 수동 재현(HUD가 측정 도구). 반복 자동화는 별도.
- **reset 버튼/키** — 게임 재시작이 리셋(게임 스코프 홀더). 인터랙티브 위젯 안 넣음(picking-ignore HUD 유지).
- **정교한 lead/jitter 추정·clock sync** — Phase 2. 여기선 `NetworkTime` 단순 추정.
- **input drop count 등 다른 netcode 메트릭** — Phase 3에서 생기면 `ReconciliationStats`를 `NetcodeDebugStats`로 일반화. 지금 YAGNI.
- **서버 측** — 무관(클라 전용).

## 검증

- **컴파일**: 클라 0에러 (UnityMCP `refresh_unity`+`read_console`, 클라 인스턴스 핀). GameFramework/서버 무변경.
- **런타임(수동, 플레이)**:
  - HUD에 6항목(Tick/Server/Lead/RTT/Recon/elapsed) 표시.
  - 이동·공중 점프 시 **Recon last가 튀고 Max가 최악값 유지, Average가 최근 흐름 반영**.
  - **Lead ≈ 0**(현재, clock sync 전) — 음수/큰 양수 아님이 정상.
  - Server tick이 클라 tick과 근접(같은 epoch 확인).
  - 콘솔 에러 0(특히 0-나눗셈/NRE 없음).
- 자동화 테스트 신규 없음(클라 단일 Assembly-CSharp, LOP 글루 — 컴파일+수동). `ReconciliationStats`는 순수 C#이나 클라 asmdef 제약으로 EditMode 불가.

## GUID / .meta 정책

신규: `ReconciliationStats.cs`(+Unity 생성 `.meta` 동반 커밋). uxml 라벨 추가는 텍스트 변경(.meta 무관). 수정 파일 파일·클래스명 유지. 삭제 없음.

## 문서/브랜치 정책

선례대로 **spec·plan·코드 모두 클라 repo**(클라 전용 작업) 피처 브랜치 `feature/netcode-phase0-debug-hud`. 서버 무변경(픽스처 무관). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 목적(측정 도구)·핵심 메트릭(reconciliation distance)·서버 tick=추정+lead·텔레메트리 홀더 합의 (브레인스토밍)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
- [ ] 구현(클라) → 검증 → 머지

> netcode-redesign Phase 0. 이후 Phase 2(clock sync)부터 이 HUD로 before/after 측정. 부드러운 회전/예측·롤백 등은 Stage④.
