# Netcode Phase 4 — 입력 타이밍 피드백 + 동적 lead

**Date:** 2026-06-21
**Branch (제안):** `feature/netcode-phase4-input-timing-feedback`
**Related:** [netcode-redesign.md](../../netcode-redesign.md) (§5 Phase 4, §9.4/§9.6) · [world-core-connection-architecture.md](../../world-core-connection-architecture.md) · [lop-repo-topology.md](../../lop-repo-topology.md)

## Goal

서버가 각 클라의 입력 도착 타이밍(서버 입력 버퍼 건강도)을 **per-client로 측정**해 그 클라에게 피드백하고, **클라가 그 피드백으로 자신의 lead(`AheadMargin`)를 동적으로 조정**한다. 현재 고정 30ms lead를, 실제 네트워크 조건에 맞춰 "miss 0을 유지하는 최소 lead"로 자기보정한다.

오버워치 모델(서버 입력 버퍼 점유 → time dilation 피드백)의 마지막 조각. 클럭 offset은 이미 Mirror `predictedTime`이 폐루프로 잡고 있으므로, 이 작업은 그 위에 **버퍼 점유(마진) 튜닝 레이어**만 추가한다.

## 배경 / 동기

Phase 2(clock sync)에서 클라가 `predictedTime + AheadMargin`(고정 30ms) 타깃으로 서버보다 앞서 달리게 만들었다. Phase 3(input buffer 정렬 + redundancy + 연속 스트림)으로 입력이 서버 `INPUT_DELAY_TICKS=2` jitter buffer에 제때 도착하게 했다.

남은 문제: **`AheadMargin`이 고정값**이라 네트워크 조건(RTT·지터)이 달라지면 과하거나(불필요한 입력 렉) 모자란다(입력 지각 → prune → 발산). 적정 lead는 조건마다 다르고 클라마다 다르다. 이를 **서버 피드백 기반 폐루프로 자기보정**하는 것이 Phase 4다.

`netcode-redesign.md` §9.6은 이 방향이 표준임을 확인했고, Phase 4의 실질 잔여 범위가 "predictedTime이 못 잡는 버퍼 점유 미세조정"임을 명시했다. 이 spec이 그 구현 설계다.

### 측정-우선 동기 (왜 2단계인가)

고정 30ms가 실제로 과한지/모자란지는 측정 없이 알 수 없다. 그래서 **한 spec, 2단계**:
- **Stage 1**: 측정 파이프라인 + 클라 HUD 표시 (행동 변화 0). 실데이터 관측.
- **Stage 2**: 그 데이터로 정책 임계값을 정하고 동적 lead 적용.

측정용 피드백 파이프라인(서버 측정 → per-client 전송 → 클라 표시)이 곧 Phase 4 인프라이므로, 던져버릴 서버 HUD 대신 진짜 클라측 파이프라인을 짓는다.

## 핵심 신호 — 도착 마진 `d`

우리 모델에서 **소비 시점은 고정**이다 (항상 `targetTick = serverTick − INPUT_DELAY_TICKS`에 소비). 따라서 정보를 담은 신호는 소비 시점이 아니라 **도착 마진 `d`**다:

```
d = (도착 시 serverTick) − (입력의 inputTick)
버퍼 대기 = INPUT_DELAY_TICKS − d
```

- `d = −5` → 입력이 자기 tick보다 5틱 일찍 도착 = 버퍼 7틱 대기 = **여유 과다**(불필요한 렉).
- `d = +2` → 소비 직전 도착 = **여유 0**(아슬).
- `d > +2` → 소비 후 도착 = **폐기(PRUNE)**.

목표: **최악 `d`(max d)를 +2 아래 안전하게 유지하되, 평균 `d`가 과하게 음수가 되지 않게** = miss 0을 유지하는 최소 lead.

이는 오버워치의 "입력 버퍼 점유" 신호와 동등하다 (버퍼 대기 = `INPUT_DELAY − d`).

## 아키텍처 결정 (브레인스토밍 합의)

| 축 | 결정 | 비고 |
|---|---|---|
| 측정 위치 | **서버** (버퍼가 서버에 있음) | per-client = per-entity (`EntityInputComponent`) |
| 정책 위치 | **클라** (방향 A, DOTS식) | 서버=측정만(dumb), 클라=정책+실행 |
| 정책 모양 | **오버워치식** (dead-zone + 계단식 밴드 + time dilation) | 모양은 오버워치, 위치는 클라 |
| 실행 | 클라 `ClockDilator` (기존 재사용) | rate dilation, 하드 점프 없음 |
| 서버 offset | `INPUT_DELAY_TICKS=2` **상수 유지** | 동적인 건 클라 lead뿐 |
| 단계화 | 한 spec, 2 stage (관측 → 동적) | Stage 1 후 멈춰 데이터 관측 |

### 방향 A vs 오버워치 B (정책 위치)

오버워치는 서버가 버퍼 점유를 보고 dilation을 *판단해 지시*(B)한다. 우리는 서버가 *측정값만* 보내고 클라가 매핑(A, DOTS `ServerCommandAge`식). 차이는 "점유 → 보정량 매핑"이 어디서 도느냐 한 곳뿐이다 (측정=서버, 실행=클라는 양쪽 동일). Stage 1에서 어차피 raw 측정값을 클라 HUD에 띄우므로, 클라가 그 값으로 매핑까지 하는 게 자연스럽고 기존 클라 `ClockDilator`를 재사용한다. **정책의 모양은 오버워치, 위치는 클라.**

## 데이터 흐름

```
[서버] EntityInputComponent (per-entity)
   │  AddInput → d 샘플 / GetInput → prune·seq-gap
   ▼
[서버] InputTimingTracker (per-entity 누적·요약, 롤링 윈도우)
   │  요약 4값: avg d / max d / prune Δ / seq-gap Δ
   ▼
[서버] 주기 sender (~0.5초) ──InputTimingToC (unreliable)──> 해당 클라 세션만
   ▼
[클라] InputTimingToC 핸들러 → InputTimingStats 홀더 (DI singleton)
   ├─ (Stage 1) DebugHudViewModel → HUD 4라벨 (관측만)
   └─ (Stage 2) LeadController → LOPTickUpdater.AheadMargin
                                  (ClockDilator가 predictedTime+margin으로 수렴)
```

## 측정 (서버)

`EntityInputComponent`(이미 엔티티=클라별)가 측정 지점을 모두 갖는다:

| 값 | 측정 위치 | 계산 |
|---|---|---|
| **d** (도착 마진) | `AddInput` | `GameEngine.Time.tick − tick` |
| **prune 수** | `GetInput` stale 제거 | `d > INPUT_DELAY_TICKS`로 버려진 입력 개수 |
| **seq-gap 수** | `GetInput` 소비 시점 | 소비 seq가 직전 소비 seq+1을 건너뛰면 그 간격 = 유실 입력 |

신규 **`InputTimingTracker`**(순수 C#, per-entity, `EntityInputComponent`가 소유)가 샘플을 롤링 윈도우에 모아 주기적으로 요약: **avg d / max d(최악=가장 빠듯) / prune Δ / seq-gap Δ**.

> **seq-gap이 idle과 안 섞이는 이유:** real input만 연속 seq를 받는다(Phase 3c에서도 idle은 seq 미생성). 소비 seq가 건너뛰면 *진짜 유실*이 확정된다. idle 틱의 null(miss)과 깨끗하게 구분된다.

> **주기 sender:** 서버 게임 루프에 타이머 훅(~0.5초마다)을 두어, 조종 엔티티별로 `InputTimingTracker` 요약을 그 세션에 `InputTimingToC`로 전송한다. `WireBroadcaster` 패턴에 준한다. 매 입력 전송이 아니라 저빈도 상태 리포트다.

## 신규 메시지 — `InputTimingToC`

- **방향/대상:** 서버 → *해당 클라 세션만* (per-client). 내 입력 타이밍이라 entity id 불필요.
- **내용 (4값):** `avg_d`, `max_d`, `prune_count`, `seq_gap_count`.
- **주기:** ~0.5초 (상수). 제어 루프가 느려 충분 (오버워치 연속 감시보다 거친 입도 — 필요시 조정 가능한 튜닝 변수).
- **전송:** **unreliable** — 주기 상태 리포트라 latest-wins, 하나 잃어도 다음 게 옴. HOL 불필요.
- **위치:** LOP-Shared proto 신규 메시지 + 재생성. **MessageId는 끝에 추가라 기존 불변.**
- **네이밍:** `InputTimingToC` — DOTS `ServerCommandAge`(서버가 본 클라 입력 버퍼 타이밍) 개념 대응 + 기존 `InputSequenceToC`/`EntitySnapsToC`의 `...ToC` 패턴과 짝.

> ⚠️ **Phase 3b 교훈:** proto 주석에 `@auto_generate` 리터럴을 쓰지 않는다(codegen이 주석도 매칭해 MessageId 시프트 → wire 깨짐).

## 클라 — Stage 1 (수신 + HUD)

- 신규 **`InputTimingStats`** 홀더 (DI singleton, `ReconciliationStats` 패턴) — 메시지 최신 4값 보관.
- 신규 `InputTimingToC` 핸들러 → 홀더 write (클라 메시지 핸들러 세트 등록).
- `DebugHudViewModel` → 홀더 read → HUD에 **4라벨**: `avg d / max d / prune / seq-gap`.
- lead는 고정 30ms 유지. **관측만**, 행동 변화 0.

> 홀더(클라)와 누적기(서버)는 이름을 분리한다: 서버 `InputTimingTracker`(샘플 누적·요약 *생성*), 클라 `InputTimingStats`(요약 *보관*). 역할이 달라 동명 회피.

## 클라 — Stage 2 (동적 lead, 오버워치 정책)

- 신규 **`LeadController`** (순수 C#, `ClockDilator`처럼 EditMode 테스트 가능) — 피드백 4값 → `AheadMargin` 조정량 계산.
- 제어 대상 = **`max d`(최악)** + prune/seq-gap 하드 신호.

**시작 정책** (Stage 1 데이터로 임계값 확정 — 아래는 형태):

```
// 0.5초마다 InputTimingToC 수신 시
if (prune > 0 || seqGap > 0)              // 실패 발생 = 비상
    margin += bigStep;                     //   쿠션 빠르게 추가
else if (maxD > tightBand)                 // 예: +1 초과 = prune 경계 근접
    margin += smallStep * (maxD - tightBand);  // 계단식(초과량 비례)
else if (maxD < looseBand)                 // 예: -1 미만 = 쿠션 과다
    margin -= smallStep;                   // 천천히 깎음(비대칭)
// dead-zone: looseBand ≤ maxD ≤ tightBand → 변화 없음
margin = clamp(margin, minMargin, maxMargin);  // 폭주 방지
```

- **오버워치 모양:** dead-zone + 계단식 밴드 + 비대칭(실패 시 빠르게 늘림 / 여유 과다 시 천천히 줄임).
- **실행:** `LeadController`가 `AheadMargin`만 갱신 → 기존 `LOPTickUpdater` + `ClockDilator`가 `predictedTime + AheadMargin`으로 부드럽게 rate dilation 수렴(이미 있는 메커니즘 재사용). 0.5초 단위 갱신이라 자연히 점진적.
- **토글:** `LeadController` 적용 on/off 스위치(상수 또는 HUD) — 고정 30ms vs 동적 *라이브 A/B* 비교용.

> `LeadController`는 일단 클라(LOP)에 둔다 — 정책이 우리 피드백 모양에 종속하고 서버가 안 쓰므로 GameFramework 승격 보류(YAGNI). `ClockDilator`(이미 GameFramework, 제네릭 rate dilation)와 역할 분리.

## 배선

```
서버 InputTimingTracker ──InputTimingToC──> 클라 핸들러 ──> InputTimingStats 홀더
                                                            ├─> DebugHud (Stage 1)
                                                            └─> LeadController ──> LOPTickUpdater.AheadMargin (Stage 2)
```

## 영향 파일 (개략)

**LOP-Shared**
- `InputTimingToC` 메시지 proto 추가 + 재생성(.cs / IMessage / MessageIds).

**Server**
- `EntityInputComponent.cs` — 측정 훅(AddInput=d, GetInput=prune/seq-gap) → tracker 기록.
- 신규 `InputTimingTracker.cs` (순수 C#, per-entity 누적·요약) + EditMode 테스트.
- 신규 주기 sender (서버 루프 타이머 훅, 0.5초마다 조종 엔티티별 `InputTimingToC` 전송).

**Client**
- 신규 `InputTimingStats.cs` (DI singleton 홀더).
- 신규 `InputTimingToC` 핸들러 (핸들러 세트 등록).
- `DebugHudViewModel.cs` + HUD UXML — 4라벨.
- 신규 `LeadController.cs` (순수 C#, 정책) + EditMode 테스트. **(Stage 2)**
- `LOPTickUpdater.cs` — `AheadMargin` settable화 + LeadController 구동 + 토글. **(Stage 2)**
- `GameLifetimeScope` DI 등록(홀더, LeadController).

**GameFramework**: 변경 없음 (`ClockDilator` 재사용).

## 테스트 전략

- **서버 `InputTimingTracker`** (순수 C#): d/prune/seq-gap 누적 + 요약 산출 — EditMode 단위 테스트.
- **클라 `LeadController`** (순수 C#): dead-zone / 계단식 비례 / clamp / 실패-override — EditMode TDD.
- **통합:** LatencySimulation(지연 + loss + 지터) 주입 → HUD 관찰. Stage 2 켜면 avg/max d가 목표 밴드로 수렴 + prune→0 + `AheadMargin` 적응. 토글로 고정 vs 동적 라이브 비교.

## 산업 표준 매핑

- **Overwatch** (Tim Ford, GDC 2017): 서버가 클라 입력 버퍼 점유를 감시 → time dilation으로 클라 클럭 ±% 조정, dead-zone + 계단식 보정. 이 spec의 **정책 모양** 원천.
- **Unity NetCode for Entities (DOTS)**: `ServerCommandAge`(서버→클라 per-client 피드백) + 클라 `NetworkTimeSystem`이 prediction tick 조정, `TargetCommandSlack`(=우리 `INPUT_DELAY_TICKS`) 상수. 이 spec의 **정책 위치(A)** 원천 — 서버=측정, 클라=정책.
- **Unity NGO / FishNet**: 동일 패턴(client-ahead + 서버 피드백 + time dilation).
- 우리 차이: 클럭 offset은 Mirror `predictedTime`이 이미 폐루프 → Phase 4는 **버퍼 점유 마진 튜닝만** 담당. 피드백 입도는 주기 요약(오버워치 연속 대비 거침).

## Out of Scope

- **풀 예측/롤백** (Stage④) — 별개 축. 이 작업은 lead/클럭만.
- **서버 `INPUT_DELAY_TICKS` 동적화** — 상수 유지. 동적인 건 클라 lead뿐.
- **redundancy window 변경** — Phase 3c 그대로.
- **lag compensation / 히트 되감기** — 별개 축.
- **대규모 멀티 플레이어 부하 튜닝** — per-entity 구조는 이미 지원하나, 다수 클라 동시 튜닝 검증은 별도.

## Open Decisions (Stage 1 데이터 후 / 구현 plan에서 해소)

- [ ] **정책 임계값** — `tightBand` / `looseBand` / `bigStep` / `smallStep` / `minMargin` / `maxMargin` 구체값. **Stage 1 관측 데이터로 확정** (감으로 오버워치 표 베끼지 않음).
- [ ] **제어 변수** — `max d` 단독 vs `avg d` 병용 vs 백분위. max d는 한 패킷에 노이즈 가능 — Stage 1에서 분포 보고 결정.
- [ ] **주기 0.5초 적정성** — Stage 1에서 피드백 반응성 확인 후 조정.
- [ ] **토글 위치** — 상수 vs HUD 버튼.
- [ ] **`max d` 윈도우 길이** — 롤링 샘플 수.

## 진행

- [x] 브레인스토밍 합의 (측정=서버/정책=클라(A)/모양=오버워치, 2단계, 신호=d, 메시지=InputTimingToC)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성

이 spec은 설계 박제다. 구현은 plan 작성 후 단계(Stage 1 → 관측 → Stage 2)로 진행한다.
