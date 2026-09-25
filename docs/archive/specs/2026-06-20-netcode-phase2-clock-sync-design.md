# Netcode Phase 2 — Clock Sync (client-ahead via predictedTime + rate dilation)

**Date:** 2026-06-20
**Branch (docs):** `feature/netcode-phase2-clock-sync` (클라 repo — 설계 허브)
**Branch (code):** GameFramework(2a) + 클라 repo(2a·2b) 피처 브랜치 (구현 시 생성)
**Related:** [Netcode 재설계](../../netcode-redesign.md) (§5 Phase2 / §9.4-9.6) · [Phase 0 측정 HUD](2026-06-20-netcode-phase0-debug-hud-design.md) · [Phase 1 회전 snap](2026-06-20-netcode-phase1-rotation-snap-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

클라이언트 시뮬 클럭을 서버 시간선보다 **앞쪽(client-ahead)**에서 달리게 해, 내 캐릭 입력이 서버에 *적시 도착*하도록 한다(= reconciliation 갭의 구조적 원인 제거). Overwatch 모델의 절반(clock sync). **방향 A 확정**(§9.4): Mirror가 이미 제공하는 `NetworkTime.predictedTime`(= 서버시간 + 편도지연, 서버 피드백 폐루프 내장)을 재사용하고, 그 위에 오버워치식 `aheadMargin`(지터/+1프레임)만 더한다. 클럭 수렴은 *값 smoothing*이 아니라 **rate time-dilation**으로 한다.

blast radius가 크므로(틱 클럭이 입력 스탬프·물리 스텝·reconciliation·AI의 기준) **2슬라이스로 분해** — 2a(구조+메커니즘, 동작 보존) → 2b(lead 도입, 동작 변경) — 해서 회귀 원인을 분리한다.

## 배경 / 조사 (구현 사실)

- **클라 틱 흐름**: `TickUpdaterBase`(GameFramework)의 코루틴 루프가 `OnElapsedTimeUpdate`로 `elapsedTime`을 매 프레임 갱신 → `tick = elapsedTime/interval`(`processibleTick` accumulator) → `onTick` → `UpdateEngine`. **`OnElapsedTimeUpdate`가 유일한 클럭 진입점**.
- 현 클라 `LOPTickUpdater.OnElapsedTimeUpdate`: `elapsedTime`을 `NetworkTime.time`으로 critically-damped(SmoothDamp류, smoothTime 0.05) 수렴, `|Δ|>0.5s`(MAX_DELTA)면 snap.
- **입력 스탬프가 이미 `GameEngine.Time.tick` 사용**(`PlayerInputManager:46`) → 클럭이 앞서면 입력이 자동으로 lead. 핵심 효과를 최소 변경으로 얻음.
- **서버 `LOPTickUpdater`는 `elapsedTime = NetworkTime.time`**(서버에선 = 자기 `Time.unscaledTimeAsDouble`) = 자기 권위 클럭 → **Phase 2는 클라 전용**, 서버 무변경.
- **`NetworkTime.time`(클라) = `NetworkClient.localTimeline` = 서버보다 bufferTime *뒤*** (Mirror 주석 명시). 현 LOP sim은 서버보다 뒤에서 달림.
- **`NetworkTime.predictedTime` = `localTime + predictionErrorUnadjusted` ≈ 서버시간 + 편도지연**, 서버가 `OnServerPing`에서 offset 오차를 계산해 pong으로 회신, 클라 EWMA(20×100ms) = **서버 피드백 폐루프**(= Phase 4 핵심을 이미 제공). `predictedTime`은 hard offset correction(점프 가능)이라 dilation 한 겹으로 흡수 필요. LOP 현재 미사용.
- **두 타임라인 이미 분리**: 내 캐릭(`SnapReconciler` + `elapsedTime` 클럭) vs 원격(`ServerStateReconciler`/`SnapInterpolator`, `NetworkTime.time`·`.rtt` 기반). 원격은 우리 클럭 변경에 영향 없음.

## 잠긴 결정 (브레인스토밍 + 리서치 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 클럭 source | **`predictedTime + aheadMargin`** (방향 A) | predictedTime이 서버시간+편도지연+서버피드백을 이미 제공 — 재발명 안 함. margin만 추가(편도지연 이중계산 금지) |
| ② | 수렴 방식 | **rate time-dilation**(속도 조정, 역행 금지) | 값 smoothing은 타깃이 뒤면 클럭 역행 → 결정론/보간 깸. 표준(NGO/NetCode/FishNet/Overwatch) |
| ③ | steering 추출 | **GameFramework `ClockDilator`**(순수·EditMode 테스트) | §9.5 클럭 steering seam. source는 클라 override(사이드별), driving/value는 그대로 |
| ④ | rename | **안 함** (`LOPTickUpdater`/`TickUpdaterBase`/`ITickUpdater` 유지) | 양 repo + connection-arch 문서 touch = churn. steering만 분리 |
| ⑤ | 분해 | **2a(구조+메커니즘, 동작보존) → 2b(lead)** | blast radius 큼 → 회귀 원인 분리. 2a는 타깃 동일이라 recon baseline 유지 |
| ⑥ | 범위 | **클라 전용**(2a는 GameFramework도) | clock lead는 클라 개념. 서버는 자기 클럭(무변경) |
| ⑦ | reconciliation | `SnapReconciler` **그대로**(§6.3) | 갭 축소만 기대. delayed-rendering 상호작용은 관찰 |
| ⑧ | 측정 | Phase 0 HUD + **LatencySimulation**(에디터 설정) | 로컬 RTT는 throttle로 부정확 → Mirror transport로 RTT 주입 |

## Slice 2a — ClockDilator 추출 + 메커니즘 전환 (동작 ≈ 보존)

### GameFramework (신규, 순수 C#)

`Runtime/Scripts/Game/ClockDilator.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 클럭(시간 값)을 타깃 시간으로 rate time-dilation으로 수렴시킨다. 값을 직접 대입(smoothing)하지 않고
    /// 진행 *속도*를 ±MaxRate로 조정 → 절대 역행하지 않음(결정론/보간 보호). 큰 오차는 1회 snap.
    /// netcode clock sync용(클라가 predictedTime+margin으로 수렴). 순수 함수라 EditMode 테스트 가능.
    /// </summary>
    public class ClockDilator
    {
        private readonly double maxRate;       // 예: 0.05 (±5%)
        private readonly double errorScale;    // 예: 0.1s — 이 오차에서 MaxRate 포화
        private readonly double snapThreshold; // 예: 0.5s — 초과 시 즉시 타깃으로 snap

        public ClockDilator(double maxRate = 0.05, double errorScale = 0.1, double snapThreshold = 0.5);

        /// <summary>current를 target 쪽으로 realDelta 동안 dilation해 advance한 새 값 반환.</summary>
        public double Advance(double current, double target, double realDelta)
        {
            double error = target - current;
            if (System.Math.Abs(error) > snapThreshold)
            {
                return target; // 급변(경로 변화/ping spike) — 1회 snap
            }
            // 비례 + 포화. error>0(타깃 앞섬)→가속(rate>1), error<0→감속(rate<1). MaxRate<1이라 rate>0(역행 없음).
            double dilation = System.Math.Clamp(error / errorScale, -maxRate, maxRate);
            double rate = 1.0 + dilation;
            return current + realDelta * rate;
        }
    }
}
```
(dead-zone은 `errorScale` 비례식이 작은 오차에서 자연히 미세 보정하므로 별도 불필요 — 단순 유지. 필요 시 추가.)

**EditMode 테스트**(`baegames.GameFramework.*.Tests`, TDD red→green):
- 타깃이 앞섬(error>0) → `Advance` 결과가 current보다 크고 target에 가까워짐(가속).
- 타깃이 뒤(error<0) → 결과가 current보다 작지만 **realDelta>0이면 절대 < current가 아님? → 역행 없음 검증**: rate ≥ 1−maxRate > 0이므로 `current + realDelta*rate > current`. 즉 **느려질 뿐 뒤로 안 감**(타깃이 뒤여도 elapsed는 전진, 단 느리게 → 타깃이 따라잡음). 이 monotonic 성질 검증.
- `|error| > snapThreshold` → 정확히 target 반환.
- dilation이 `[−maxRate, maxRate]`로 clamp(큰 error에서 rate 포화).

> ⚠️ **명확화(중요한 의미론)**: target이 current보다 *뒤*(우리가 타깃보다 앞섬)일 때 dilation은 elapsed를 `0.95×realDelta`로 *느리게* 전진시킨다 — **elapsed 자체는 계속 증가**(시간 역행 없음). 타깃(predictedTime은 실시간으로 진행)이 우리의 느려진 클럭을 *따라잡아* 수렴한다. 반대로 타깃이 앞서면 `1.05×`로 가속해 따라붙는다. 이것이 "값 SmoothDamp(타깃이 뒤면 클럭을 뒤로 끌어 역행 가능)" 대비 핵심 차이.

### 클라 `LOPTickUpdater` (수정)

`OnElapsedTimeUpdate`를 critically-damped 값 smoothing → `ClockDilator` 사용으로 교체. **타깃은 현행 `NetworkTime.time` 유지(lead 없음)**:
```csharp
public class LOPTickUpdater : TickUpdaterBase
{
    private readonly ClockDilator clockDilator = new ClockDilator();

    protected override void OnElapsedTimeUpdate()
    {
        double target = Mirror.NetworkTime.time; // 2a: 현행 타깃 유지(2b에서 predictedTime+margin으로 flip)
        elapsedTime = clockDilator.Advance(elapsedTime, target, UnityEngine.Time.deltaTime);
    }
}
```
(기존 `elapsedTimeVelocity`/SMOOTH_TIME/MAX_DELTA 필드 제거 — snap은 `ClockDilator.snapThreshold`로 흡수.)

### 2a 효과
구조 개선(steering 분리·테스트) + 값-smoothing→rate-dilation 메커니즘 전환. **타깃 동일이라 클럭이 여전히 `.time`(서버−bufferTime)에 수렴 → recon baseline 유지, Lead≈0**.

## Slice 2b — lead 도입 (동작 변경)

### 클라 `LOPTickUpdater` (수정)
target만 flip:
```csharp
private const double AheadMargin = 0.030; // 오버워치식 지터/+1프레임. predictedTime에 편도지연 이미 포함 — margin만.

protected override void OnElapsedTimeUpdate()
{
    double target = Mirror.NetworkTime.predictedTime + AheadMargin;
    elapsedTime = clockDilator.Advance(elapsedTime, target, UnityEngine.Time.deltaTime);
}
```

### Phase 0 HUD Lead 정확화 (`DebugHudViewModel`)
현 `ServerTickEstimate`는 `.time`(서버−bufferTime) 기반이라 Lead가 bufferTime만큼 과대. 서버 now 추정으로 정정:
```csharp
// 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval
public long ServerTickEstimate => (long)((Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5) / GameEngine.Time.tickInterval);
// Lead = Tick − ServerTickEstimate = (AheadMargin + 편도지연)/interval = 진짜 lead
```
(`Lead` getter는 그대로 `Tick − ServerTickEstimate`.)

### 2b 효과
클라 틱이 `predictedTime + margin` 기반 → **Lead 양수화(~(margin+RTT/2)틱)**, 입력이 서버에 적시 도착 → **recon max/avg 감소**. predictedTime 점프는 ClockDilator가 흡수.

### 전환 시 1회 점프
2b 적용 순간 타깃이 `.time`(뒤) → `predictedTime+margin`(앞)으로 ~`bufferTime+latency+margin` 점프. `snapThreshold`(0.5s) 초과면 1회 snap, 이하면 수초간 dilation 수렴 — 둘 다 허용(게임 시작 시 1회).

## 측정 (LatencySimulation)

로컬 2-에디터 RTT는 프레임 throttle로 부정확(§Phase0). **Mirror `LatencySimulation` transport 컴포넌트**(코드 아님 — 에디터 설정)로 클라에 50ms/150ms 등 주입해 측정:
1. 2a 머지 후 baseline: 공중 점프 반복, Recon max/avg + Lead(≈0) 기록.
2. 2b 후 같은 RTT·시나리오: Lead 양수(~margin+RTT/2틱), Recon max/avg가 2a 대비 **감소** 확인.

LatencySimulation 셋업은 측정 절차(검증)일 뿐 production 코드/커밋 아님.

## 검증

- **2a**:
  - GameFramework EditMode `ClockDilator` 통과(수렴/역행없음/snap/clamp).
  - 클라 컴파일 0 (UnityMCP, 클라 인스턴스 핀). GameFramework는 file: 공유 → 서버도 컴파일 영향, 2a는 *추가*(ClockDilator)라 비파괴, 양쪽 확인.
  - 플레이: 이동/점프 부드러움, 텔레포트/떨림 없음, **Recon ≈ baseline(무회귀)**, Lead≈0, 에러 0.
- **2b**:
  - 클라 컴파일 0.
  - LatencySimulation 주입: **Lead 양수(~margin+RTT/2틱)**, **Recon max/avg가 2a baseline 대비 감소**, 고무줄 육안 감소, 에러 0.
  - delayed-rendering(`SnapReconciler.LateUpdate`) 상호작용 정상(자기 캐릭 렌더 튐 없음).
- 자동 테스트: **GameFramework `ClockDilator`만**(순수 C#). LOP 글루(LOPTickUpdater/HUD)는 컴파일+수동(단일 Assembly-CSharp).

## GUID / .meta 정책

신규 `ClockDilator.cs`(+Unity 생성 `.meta` 동반 커밋, GameFramework). 수정 파일(LOPTickUpdater/DebugHudViewModel) 파일·클래스명 유지. 삭제 없음.

## Out of scope (defer)

- **Phase 3** 서버 input buffer 정렬(`tick==serverTick`), **Phase 4** 서버 input-buffer 점유 기반 미세조정(predictedTime이 lead 폐루프는 이미 제공 — 추가 필요 시).
- sliding-window 입력 중복 전송, 원격 적응형 jitter buffer.
- 예측/롤백/Snapshot-Restore/commit gate = Stage④.
- `LOPTickUpdater`/`ITickUpdater` rename, `LOPGameSimulation` 추출(Slice 4).
- 서버 측 — 무변경(자기 클럭).

## 슬라이스 분해 (구현 순서)

- **2a** (GameFramework `ClockDilator` + EditMode 테스트 → 클라 `LOPTickUpdater` 메커니즘 전환, 타깃 `.time` 유지). GameFramework 먼저(file: 공유), 클라 뒤. 게이트: 2b가 ClockDilator + OnElapsedTimeUpdate seam에 의존.
- **2b** (클라 `LOPTickUpdater` target flip + HUD Lead 정확화). 클라 전용.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/netcode-phase2-clock-sync`, **코드는 GameFramework(2a, file: 공유 — 양쪽 컴파일 확인) + 클라 repo**. 서버 working-tree 로컬 픽스처는 이번 무관(서버 코드 무변경). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 방향 A(predictedTime+margin)·rate dilation·ClockDilator 추출·2슬라이스 분해 합의 (리서치+브레인스토밍)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 2a 구현 plan
- [ ] 구현(2a: GameFramework→클라 → 2b: 클라) → 검증 → 머지

> netcode-redesign Phase 2. 이후 Phase 3(서버 input buffer) — 별도. 부드러운 회전/예측·롤백은 각각 Stage④.
