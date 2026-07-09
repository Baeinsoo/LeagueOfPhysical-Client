# LOP Netcode 재설계 — Overwatch 스타일 Server-Authoritative Fast-Paced

> 목적: 클라이언트가 서버 권위 fast-paced 방식으로 동작하면서 물리 시뮬레이션 기반 움직임에서도 공중 점프/낙하 등에서 reconciliation gap이 보이지 않도록 개선한다.

---

## 1. 배경

LOP는 PhysX 기반의 물리 시뮬레이션 게임이며, 클라이언트가 서버보다 먼저 인풋을 처리하고 서버 스냅샷으로 보정(reconcile)하는 fast-paced 형태로 구현되어 있다.

지상 이동은 큰 문제 없이 보이지만, **공중 낙하 중 점프**와 같이 수직 속도가 큰 상태에서의 인풋은 클라와 서버의 처리 시점 차이로 인해 visible gap이 발생한다.

---

## 2. 현재 구조

### 2.1 데이터 흐름

- 클라 `LOPGameEngine.UpdateEngine()`: `ProcessInput → UpdateEntity → SimulatePhysics`
- 인풋 발생 시: 클라는 즉시 `ApplyInput()` + Physics 시뮬, 동시에 서버로 전송
- 서버: 받은 인풋을 `EntityInputComponent.inputBuffer`에 적재, 다음 서버 틱에서 1개씩 꺼내 처리
- 서버 스냅 송신 → 클라 `SnapReconciler`가 시퀀스 기반으로 보정

### 2.2 주요 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Game/LOPGameEngine.cs` | 게임 엔진 메인 루프 (`UpdateEngine`, `SimulatePhysics`) |
| `Assets/Scripts/Game/LOPTickUpdater.cs` | 틱 시간 계산 (Mirror.NetworkTime에 smoothing) |
| `Assets/Scripts/Game/PlayerInputManager.cs` | 클라 인풋 캡처/전송/예측 적용 |
| `Assets/Scripts/Game/LOPMovementManager.cs` | 이동/점프 입력 처리 |
| `Assets/Scripts/Entity/SnapReconciler.cs` | 본인 캐릭터 reconciliation |
| `Assets/Scripts/Entity/ServerStateReconciler.cs` | 다른 플레이어 보간 |
| (서버) `Assets/Scripts/Component/InputBufferComponent.cs` (구 `EntityInputComponent` — 2026-07-02 입력-as-데이터 슬라이스에서 표준 용어로 rename) | 인풋 버퍼 + 처리 큐 |
| (서버) `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs` | 인풋 수신 핸들러 |

### 2.3 핵심 메커니즘 — 현재 reconciliation

`SnapReconciler.cs`는 **delta replay** 방식:
1. 매 틱 `(end - begin)` 변화량을 `localEntitySnaps`에 저장
2. 서버 스냅 도착 시 그 스냅이 처리한 인풋 시퀀스를 기준으로 anchor
3. anchor 이후의 로컬 delta들을 서버 상태 위에 누적
4. `Vector3.SmoothDamp`로 부드럽게 보정 (threshold 0.06, max 30× 시 텔레포트)

이는 일반적 fast-paced의 **input replay** 방식과 다름. 물리 재시뮬을 안 함.

---

## 3. 발견된 문제

### 3.1 (구조적, 큼) 인풋 처리 시점 차이 → 적용 직전 상태 차이

- 클라: `local_tick=100`에 인풋 처리 (y=10, vy=-5)
- 서버: 같은 인풋을 `server_tick=103`에 처리 (그 사이 중력 누적 → y=9.14, vy=-6.5)
- "seq 50 처리 결과"가 서로 다름 → 시퀀스 기반 reconciliation도 이 갭은 보정 못 함
- **갭 크기 ≈ RTT × velocity_at_input_time**
  - 지상 (vy=0): 거의 0
  - 공중 낙하 (vy=-15, RTT=150ms): ≈ 2m
  - 빠른 낙하 (vy=-30): ≈ 4.5m

### 3.2 (구조적, 큼) 점프 임펄스의 y-velocity 리셋

`LOPMovementManager.cs:39-41`:
```csharp
physicsComponent.entityRigidbody.linearVelocity -= new Vector3(0, ..., 0);
physicsComponent.entityRigidbody.AddForce(Vector3.up * JumpPower, ForceMode.Impulse);
```
처리 시점이 다르면 리셋 대상 y-vel이 달라 결과가 크게 어긋남. 위 3.1 효과를 증폭.

### 3.3 (구현 버그, 중간) `Mathf.SmoothDampAngle` 사용 방식

`LOPMovementManager.cs:30-33`:
```csharp
float myFloat = 0;
var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
```
- `ref` 변수가 매 호출마다 0으로 초기화 → SmoothDamp 의도와 다르게 동작
- 내부적으로 `Time.deltaTime` 사용 → fixed-tick 시스템과 어긋남, 클라/서버에서 다른 dt → 회전 결과 차이 → 위치까지 미세 발산

### 3.4 (작음) 패킷 손실/지터

서버 인풋 버퍼가 비면 그 틱은 인풋 없이 진행 → 클라와 다름. 현재 `INPUT_DELAY_TICKS = 0`이라 jitter buffer 없음.

### 3.5 (참고) PhysX 부동소수점 비결정성

외부 충돌 없으면 미미. 누적되면 수 mm 단위. 우선 무시 가능.

---

## 4. 해결 방향 — Overwatch 모델 채택

### 4.1 핵심 아이디어

**클라이언트의 시계를 서버보다 `(one-way latency + jitter buffer)` 만큼 앞쪽으로 세팅하고, 같은 속도(틱 레이트)로 달리게 한다.**

- 클라 tick rate ≠ 빨라지는 게 아님. **시작점이 앞쪽**일 뿐.
- 클라가 `client_tick = T`에 인풋 발생 → 패킷 도달 시 서버도 `server_tick = T` 부근
- 서버는 인풋을 자기 같은 tick 번호에 처리 → **같은 시간선 같은 입력 → 같은 결과**
- Reconciliation gap이 이론상 거의 0

### 4.2 부가 메커니즘

- **Server-side input buffer (jitter buffer)**: 인풋이 지터로 흔들려도 평탄화
- **Time dilation (선택)**: 서버가 클라에게 시계 조정 신호 (현재 lead time이 너무 작거나 크면)
- **인풋 누락 대응**: 마지막 인풋 반복 또는 no-input으로 진행

### 4.3 왜 Overwatch 모델인가?

| 게임 | 클라 ahead? | 비고 |
|---|---|---|
| Overwatch | ✅ | Tim Ford GDC 2017로 잘 문서화됨 |
| Valorant | ✅ | Overwatch와 유사 |
| Source (CS) | ❌ | Lag compensation은 히트만 |
| Unreal | ❌ | ServerMove에 결과 state 첨부 (client-trust 위주) |
| Rocket League | ❌ | 비결정 물리(Bullet/UE3) — 예측 + 서버 권위 *상태* 복제 + 보정 시 재시뮬. **결정론 락스텝 아님**(Cone, GDC 2018) |

LOP는 PhysX 기반(비결정적)이고 strict server-authoritative를 원하므로 → Overwatch 모델이 가장 직접적인 참고

---

## 5. 단계별 적용 계획

> 각 단계는 독립적으로 측정 가능. 한 단계 끝낼 때마다 공중 점프 시나리오에서 갭 크기를 측정해서 다음 단계로 넘어갈지 결정.

### Phase 0 — 측정 도구 마련 (선행 작업) ✅ 완료(2026-06-20)

- [x] 디버그 HUD에 표시: 클라 tick, **서버 tick(추정) + lead**, RTT, reconciliation distance(last/avg/max). `ReconciliationStats` 홀더 + `DebugHud` 확장. spec/plan: `docs/superpowers/specs|plans/2026-06-20-netcode-phase0-debug-hud*`.
- [ ] 공중 점프 시나리오 재현 케이스 (반복 가능한 테스트 환경) — HUD가 측정 도구. 수동 재현.
- [ ] 변경 전 baseline 측정: 평균/최대 reconciliation distance — Phase 2 전 기록.

> 로컬 2-에디터에선 RTT가 와이어 지연이 아니라 프레임/throttling 지연(수십 ms) — 실측 baseline은 Latency Simulation으로 현실적 RTT 주입 필요(§9.6).

### Phase 1 — 명백한 버그 수정 (효과: 작지만 즉시 적용 가치 있음) ✅ 완료(2026-06-20)

- [x] `LOPMovementManager` 회전 SmoothDampAngle 버그 수정 → **결정론적 snap**(`entity.rotation = new Vector3(0, angle, 0)`) 채택. 클·서 동시.
  - 채택 근거: 회전은 velocity와 무관한 cosmetic facing + 현 `smoothTime=0.01`이 사실상 즉시 → snap이 체감 동일·무상태·dt 비의존(클·서 동일). SmoothDamp는 velocity 상태가 스냅샷/롤백에 끌려 들어가 시뮬엔 부적합(카메라/뷰 idiom). **부드러운 facing ease는 시뮬 아닌 뷰(Stage④ view-pull)로 분리** — `MoveTowardsAngle`(고정 rate, 무상태)은 게임플레이상 turn 시간이 필요해질 때 대안.
  - spec/plan: `docs/superpowers/specs|plans/2026-06-20-netcode-phase1-rotation-snap*`.

### Phase 2 — Clock Sync (Overwatch 모델의 절반) ✅ 완료(2026-06-20)

목적: 클라가 서버 시간선보다 앞쪽에서 시뮬레이션하도록 변경

- [ ] `LOPTickUpdater.cs` 수정: 내 캐릭 틱을 `predictedTime` 기반으로 (방향 A 확정 — §9.4)
  ```csharp
  // 의사 코드 (방향 A — Mirror predictedTime 활용)
  // predictedTime ≈ 서버 현재시간 + 편도지연 (RTT/2 이미 포함 + 서버 피드백 폐루프로 자동 유지)
  double aheadMargin = 0.030;  // 오버워치식 +지터/+1프레임만 추가 (편도지연 이중계산 금지). 시작 30ms 튜닝
  double targetClientTime = Mirror.NetworkTime.predictedTime + aheadMargin;
  // ⚠️ 이 타깃을 "값 smoothing" 아니라 rate time-dilation으로 수렴.
  //   (predictedTime은 hard offset correction=점프 가능 → dilation 레이어가 흡수; 값 SmoothDamp 금지=역행 위험)
  // 원격 엔티티 클럭은 NetworkTime.time/스냅샷 보간 유지 (별 타임라인 — ServerStateReconciler/SnapInterpolator)
  ```
- [ ] Lead time 변동 시 갑작스러운 점프 방지 (lead 자체도 smoothing)
- [ ] RTT 급변(예: ping spike) 시 안전장치 (max delta, freeze 등)

### Phase 3 — Server Input Buffer 정렬 ✅ 완료(2026-06-21, 3a+3b+3c)

목적: 서버가 "인풋의 클라 tick == 자기 tick"에 인풋을 처리하도록

- [ ] `EntityInputComponent.cs` 수정
  - 현재 `GetInput(serverTick)`: `tick <= targetTick` 인 가장 오래된 인풋
  - 변경 후: `tick == serverTick` 인 인풋 (또는 `tick <= serverTick`이지만 가장 가까운 것)
  - `INPUT_DELAY_TICKS`를 jitter buffer로 명확히 정의 (예: 2틱)
- [x] 인풋이 해당 tick에 도착 안 했을 때 정책 → **옵션 B(no-input) 채택** (옵션 A=마지막 인풋 반복은 미채택 — 아래 "고손실 강건성 드롭" 참고)
- [x] 인풋이 너무 늦게 도착 시 정책 → **옵션 A(drop/prune)** + drop·seq-gap count를 클라에 피드백(Phase 4 `InputTimingToC`)

> **고손실 강건성(옵션 A = miss 시 마지막 인풋 반복) — 드롭(2026-06-21).** 한때 reconciliation 갭이 "심함"으로 보고돼 후속으로 검토했으나, 발산 근본원인은 입력 정책이 아니라 *redundancy 스트림 끊김*(Phase 3c에서 해결)이었음이 밝혀짐. 이후 측정(공중 점프 × RTT 50/150/300ms × 20% loss)에서 prune≈0·갭 수용 가능·rubberbanding 육안 소멸 확인 → 현 miss=no-input 유지, 옵션 A 업그레이드는 **로드맵에서 내림**. 실환경 prune율이 유의미해지면 재개.

#### Phase 3c — 연속 command-frame 스트림 (sliding-window redundancy의 전제) ✅ 완료(2026-06-21)

**핵심 전제 박제:** sliding-window redundancy(3b)는 *"패킷이 유실돼도 다음 패킷이 곧바로 온다"* 를 암묵 전제한다. 이는 **틱을 건너뛰지 않고 연속으로 흐르는 패킷 스트림**이 있어야만 성립한다 — 표준 fast-paced(Overwatch)의 command-frame 모델이 *매 틱 입력 프레임을 전송*하는 이유. 무입력 틱도 스트림에 포함된다.

**발견된 버그(3b의 결함):** 클라가 *입력 있을 때만* 전송 → 무입력 구간(특히 점프 후 공중 정지)에 스트림이 **끊김**. 유실된 입력의 복구 사본을 실어 나를 "다음 패킷"이 수십 틱 뒤에야 와서, 서버 jitter buffer(`INPUT_DELAY_TICKS=2`)를 넘겨 **PRUNE(폐기)** 됨. 결과: 유실된 점프가 서버에 영영 적용 안 됨 → 클·서 발산("떨어지는 점프가 완전히 다르게 보임"). 20% loss + 공중 점프에서 재현. *latency가 아니라 loss가 원인*이라 latency=0에서도 재현.

**수정(클라 단독):** `PlayerInputManager.ProcessInput`에서 **무입력 틱에도 redundancy 윈도우를 재전송**해 연속 스트림 유지. **새 seq를 만들지 않고 기존 윈도우만 재송출** — 서버 처리·`SnapReconciler`(real-input seq 매칭에 의존)·seq cadence 전부 무변경. 서버는 이미 `RecentInputs` 루프 + `AddInput` dedup으로 redundancy를 처리하고 있었으므로 **서버 변경 0**. 유실 입력이 1틱 내 재도착 → buffer 안에서 복구.

**검증:** loss 20% + 공중 점프 반복. 이전 3건 PRUNE → **0건**, 모든 점프 `received → processed` 1:1, 점프 발산 체감 소멸. (계측: 클라 `[JUMP] client sent` ↔ 서버 `[JUMP] received/processed/PRUNED` 로그로 1:1 대조 — 검증 후 제거.)

> Anemic 대칭성 확인: 무입력은 `LOPMovementManager.ProcessInput(...,0,0,false)`에서 `direction.sqrMagnitude > 0` 가드로 **완전한 no-op**(velocity·rotation 미변경, jump 없음) → no-op 처리/스킵이 클·서 동일. 그래서 윈도우 재전송 방식이 안전.

### Phase 4 — 입력 타이밍 피드백 + 동적 lead ✅ 완료(2026-06-21)

목적: 클라 lead(특히 *임의로 둔* `AheadMargin` 30ms jitter 쿠션)가 환경에 맞춰 자동으로 적정값을 유지하도록. **latency 추적은 이미 `predictedTime`이 담당(Phase 2)** — Phase 4는 그 위의 **jitter 마진만** 동적 조정. spec/plan: `docs/superpowers/specs|plans/2026-06-21-netcode-phase4-input-timing-feedback*`.

- [x] 서버가 클라 입력 도착 분포 측정 (per-client) — `EntityInputComponent`가 GameFramework `InputTimingTracker` 소유, 도착 마진 `d`(=serverTick−inputTick) / prune / seq-gap 누적.
- [x] 서버 → 클라 피드백 메시지 — 신규 `InputTimingToC`(LOP-Shared proto, MessageId 14, unreliable), ~0.5초마다 per-client 요약(avg d / max d / prune / seq-gap / sample) 전송.
- [x] 클라가 그 피드백으로 lead 조정 — GameFramework `LeadController`(오버워치식 dead-zone + 계단식 밴드 + 비대칭) 정책이 `AheadMargin`을 갱신, 기존 `ClockDilator`가 rate dilation으로 수렴. `LeadState`(토글 보유)로 고정/동적 A/B.
- **구조 결정:** 측정=서버 / 정책=클라(A, DOTS `ServerCommandAge`식) / 정책 모양=오버워치 / 실행=클라 `ClockDilator`. 서버 `INPUT_DELAY_TICKS=2`는 상수 유지(동적인 건 클라 lead뿐). 순수 로직(Tracker/Controller)은 EditMode 테스트 위해 GameFramework에 배치.
- **측정 근거:** Stage 1 관측 — latency 0/100/300ms+20% loss 모두 prune 0, d max ≤ 0, 체감 정상 → *고정 30ms도 테스트 범위에선 충분*. 동적 조정은 더 거친 실환경 jitter 보험 + 평온 시 마진 트림. 정책 임계값(dead-zone [−1,+1], step 10/2ms, 0~100ms)은 그 데이터 기반.

### Phase 5 — 점프 임펄스 처리 개선 (보조) ⏸ 보류 — 게임 디자인 콜(2026-06-21)

목적: y-velocity 리셋이 시점 차이에 덜 민감하도록

- [ ] 점프 처리 방식 검토
  - 현재: `vy = 0` 리셋 후 `+JumpPower`
  - 대안 A: 그냥 `+JumpPower` (낙하 중 점프는 약하게 느껴짐 — 게임 디자인 문제)
  - 대안 B: `vy = max(vy, 0) + JumpPower` (낙하 상쇄 + 점프력 보존)
  - 대안 C: 점프 입력에 클라가 본 vy 값 첨부, 서버가 그 vy 기준으로 처리
- 게임플레이 의도와 절충 필요. Phase 2/3 효과 확인 후 결정

### Phase 6 — 종합 검증 ✅ 검증됨(2026-06-21, 사전 관측 — 절대 수치+육안)

- [ ] Baseline 대비 갭 크기 비교 (평균, 95th percentile, max)
- [ ] 다양한 RTT (50ms / 150ms / 300ms) 시나리오 측정
- [ ] 다양한 시나리오 (지상 이동, 공중 낙하 중 점프, 연속 점프, 가만히 있을 때) 측정
- [ ] 시각적으로 rubberbanding 확인 (육안 + 녹화)

### 진행 상태 (2026-06-21 — netcode 일단락)

**netcode-redesign 핵심 = 완료 + 검증.** Overwatch 모델의 핵심 절반(clock sync Phase 2 + server input buffer Phase 3)이 공중 점프 갭을 닫았다. 검증은 Phase 3c·4 진행 중 사전 관측으로 갈음 — 공중 점프 × RTT 50/150/300ms × 20% loss에서 prune≈0, reconciliation 갭 수용 가능, rubberbanding 육안 소멸. (단 Phase 0 절대 baseline 수치는 기록된 적 없어 "before/after delta"는 없고 *절대 수치 + 육안*으로 판정.)

내려간/보류 항목:
- **Phase 3 고손실 강건성(옵션 A = miss 시 마지막 인풋 반복) — 드롭.** 발산 근본원인은 입력 정책이 아니라 redundancy 스트림 끊김(Phase 3c에서 해결)이었고, 측정에서 현 miss=no-input이 수용 가능함이 확인됨. 실환경 prune율이 유의미해지면 재개.
- **Phase 5 (점프 임펄스 vy) — 보류.** clock sync가 처리 시점 차이를 이미 줄여 netcode 측 동기가 약해짐 → 남은 건 순수 게임 디자인 콜(낙하 중 점프 세기). 게임플레이상 필요해지면 재개.

**남은 큰 트랙 = Stage④** (클라 예측·롤백·Snapshot/Restore·통합 fan-out·`LOPGameSimulation` 공유 시뮬 추출 — `world-core-connection-architecture.md` Slice 4와 결합). 별도 brainstorm→spec→plan.

---

## 6. 진행 시 주의사항

### 6.1 한 번에 한 변경만

각 Phase 끝에 baseline 측정 → 다음 Phase. 두 변경을 묶지 말 것.

### 6.2 클라/서버 동기화된 변경

`LOPMovementManager`, `EntityInputComponent` 등은 클라/서버에 같은 코드가 있을 수 있음. 양쪽 동시 수정.

### 6.3 기존 reconciliation은 유지

Clock sync가 완벽해도 FP 오차 / 패킷 손실로 인한 미세 갭은 여전히 존재. `SnapReconciler`의 SmoothDamp 보정은 그대로 두고, 단지 *갭이 크게 줄어들 것*을 기대.

### 6.4 Cheating 우려

이 모델은 여전히 server-authoritative이므로 cheating 대비는 별도. 단, `PlayerInputToS.EntityTransform` 같은 클라 보고 값을 *그대로 사용*하면 안 됨 (현재 서버가 안 쓰고 있는데, 쓰는 방향으로 갈 일 있으면 검증 필요).

### 6.5 Snapshot/Restore 책임 위치 (Stage ④)

예측·롤백에서 *전체 시뮬 상태를 되돌리는* `Snapshot/Restore` 능력은 **클라 외각(`LOPGameEngine`)의 책임**이고, **시뮬 코어(`LOPGameSimulation`)에는 두지 않는다**. 근거:

- **시뮬 = "인풋 → 이벤트 생성" 그 자체** — 책임을 최소로. 결정론·계산만.
- **롤백 정책은 클라 특화** — server-authoritative LOP에서 *전체 World Snapshot/Restore*는 클라만 사용 (서버는 *전체* 롤백 안 함, lag compensation은 *부분 historical state*로 별도).
- **서버 코드에 안 쓰는 능력을 코어에 두지 않음** — YAGNI.

### 구조

- 시뮬은 *상태 access* 만 외부에 노출 (`EntityRegistry`, `WorldEventBuffer`, 물리 상태 등)
- 클라 LOP-Client 측 클래스(`SnapshotHistory`, `ReplayController` 같은 신규 클래스)가 매 틱 상태를 보관하고 서버 snap 도착 시 시뮬에 *덮어쓰기*
- GameFramework 추상(`IGameSimulation`/`GameSimulationBase`)에 `Snapshot()`/`Restore(snap)` 메서드는 **없음**

### Stage ④에서 결정될 디테일

- `SnapshotHistory`의 보관 형태 (slot/ring buffer, 압축 여부)
- 시뮬 상태의 *deep clone* 비용 vs *증분 변경 추적* 비용 trade-off
- 물리 상태(`Rigidbody.position/velocity`)를 어떻게 캡처/복원할지 (PhysX는 직접 set 가능)
- 예측 vs 확정 fan-out commit gate와의 연동

→ Stage ④ 시작 시 `docs/superpowers/specs/`에 별도 spec.

---

## 7. 참고 자료

### 1차 자료 (Overwatch 모델)
- [GDC 2017: Overwatch Gameplay Architecture and Netcode — Tim Ford (YouTube)](https://www.youtube.com/watch?v=W3aieHjyNvw)
- [Edgegap: Overwatch Netcode Architecture (강연 요약)](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback)

### 실무자 토론
- [GameDev.net: Command Frames and Tick Synchronization](https://www.gamedev.net/forums/topic/696756-command-frames-and-tick-synchronization/)
- [GameDev.net: Overwatch Client Input Buffer + Dynamic FixedTimeStep](https://gamedev.net/forums/topic/701605-overwatch-client-input-buffer-dynamic-fixedtimestep/)

### 입문/배경
- [Gabriel Gambetta: Client-Server Game Architecture 시리즈](https://www.gabrielgambetta.com/client-server-game-architecture.html) (기본형 — clock sync는 없음)
- [Glenn Fiedler: Networked Physics 카테고리](https://gafferongames.com/categories/networked-physics/)

### 비교용
- [Valve Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Quake 3 Networking Primer (Bryan McNeely)](https://www.ra.is/unlagged/network.html)

---

## 8. 미해결 / 추가 검토 사항

- Mirror의 `NetworkTime` 정확도가 lead time 계산에 충분한가? (jitter 흡수 후 ±몇 ms 안정성?)
- Tick interval이 변경되면 영향받는 시스템 (애니메이션, 이펙트, AI 등) 검토
- 본인 외 엔티티(`ServerStateReconciler`)는 별도 — clock sync와 무관, interpolation delay 그대로 유지
- Phase 2/3 도입 후 기존 `SnapReconciler`의 `entityTransformSnaps` 기반 delayed rendering (`SnapReconciler.cs:177-202`)과의 상호작용 확인
  - ⚠️ **임시 가드 박제 (2026-06-28):** 지연 렌더링이 스냅을 *틱 카운터*(`Runner.Time.tick`)로 저장하는데, `LateUpdate`는 *경과시간 역산*(`elapsedTime / tickInterval`)으로 틱을 찾는다. 둘이 1틱 어긋나거나 중간 틱이 비면 `BoundedDictionary` 인덱서가 `KeyNotFoundException`을 던져 런타임 예외가 폭주했다(클럭 dilation 도입 후 노출된 잠복 버그). **임시 조치 = `SnapReconciler`/`ServerStateReconciler` 두 `LateUpdate`에서 `TryGetValue` 가드 → 없으면 그 프레임 보간 스킵**(직전 위치 유지). 이건 *증상 차단*일 뿐, 두 시간선이 어긋나는 *근본 원인*은 안 고쳤다. **Stage④에서 스냅샷 타임라인을 재설계(delta replay → Snapshot/Restore + input replay)할 때 가드를 걷어내고 틱 정렬을 제대로 잡을 것.**
- LOPGameEngine ↔ LOPGameSimulation 컴포지션 (Slice 4) 도입 후, 현 `SnapReconciler`의 *delta replay* 방식을 *Snapshot/Restore + input replay* 방식으로 자연 수렴 가능 — Stage ④에서 평가

---

## 9. 시간 동기화 — 업계 표준 대비 & 구조/네이밍 (리서치 2026-06-20)

Unity 생태계(Mirror/NGO/Netcode-for-Entities/FishNet/Photon Fusion·Quantum), Unreal, 그리고 1차 원리(NTP·Gambetta·Gaffer·Overwatch GDC) 깊은 조사 결과. 현 `LOPTickUpdater`(값을 `NetworkTime.time`으로 critically-damped smooth, 클라 lead 없음) 방식의 표준 정합성 평가와 정련 방향.

### 9.1 업계 표준 모델 (전 시스템 수렴)

조사한 **모든** 성숙 netcode가 동일 패턴:

| 시스템 | 클라 lead | 보정 방식 | 서버 피드백 |
|---|---|---|---|
| Unity NGO | RTT/2 + buffer | time dilation (`AdjustmentRatio`≈1%), 200ms 초과 시 snap | 동기 |
| Unity NetCode(DOTS) | RTT/tick + `TargetCommandSlack`(2) | DeltaTime 스케일링 | `ServerCommandAge` 피드백(폐루프) |
| FishNet | HalfRTT | `_adjustedTickDelta` dilation | 주기적 timing 패킷 |
| Photon Fusion/Quantum | RTT/2 ticks | 틱 micro-correction + rollback/resim | 서버가 per-client lead 지시 |
| Overwatch(원형) | RTT/2 + 1 command frame | 계단식 time dilation(16ms→~15.2ms) | input-buffer 점유 피드백 |
| **LOP 현재** | **없음(서버 시간선 위)** | **시간 *값* smoothing** | **없음** |

**표준 3단계:**
1. **오프셋 추정** — NTP식 4-timestamp(`θ=((t1−t0)+(t2−t3))/2`) 또는 단순 `RTT/2`. 단 raw RTT의 EWMA가 아니라 **min-RTT 샘플 / trimmed-mean**(혼잡 시 EWMA가 오프셋을 systematically 왜곡 — 오차는 `RTT/2`로 bound, 짧은 RTT 샘플일수록 정확).
2. **lead 타깃** — `clientClock = serverTime + RTT/2 + jitterBuffer`. jitter ≈ `K·σ_OWL`(K≈3) 또는 2~3틱(≈30~50ms). 이게 입력이 서버에 *적시 도착*하게 만드는 핵심.
3. **smooth 수렴 = time dilation** — 시간 *값*을 건드리지 않고 **rate를 ±1~5% 조정**(절대 뒤로 안 감). 값에 저역통과/critically-damped를 걸면 타깃이 뒤일 때 클럭이 역행해 결정론·보간을 깸 → rate에 적용해야 함(=1차 PLL과 동형).

### 9.2 두(세) 타임라인 모델

fast-paced 클라는 **시간 기준점을 동시에 3개** 유지(naive 구현이 가장 많이 놓치는 지점):

- **server time** — 기준.
- **predicted time** = `serverTime + RTT/2 + jitter` — **앞선** 클럭, *내 캐릭* 입력/예측용.
- **interpolation time** = `serverTime − RTT/2 − jitterMargin` — **뒤처진** 클럭, *원격 엔티티* 렌더용(스냅샷 보간 버퍼 ≈ 3× 패킷 간격, Fiedler).

내 캐릭 입력은 서버가 필요로 하기 *전에* 도착해야 하므로 앞서고, 원격은 *이미 받은* 두 스냅샷 사이를 보간하므로 뒤처진다 — 두 클럭을 하나로 합치면 안 됨.

### 9.3 LOP 평가 — 갭과 이미 정합인 부분

**구조적 결함(우선순위):**
1. **클라 lead 없음** (Critical) — 입력이 서버에 ~RTT/2 늦게 도착 → 서버가 버퍼 지연을 전역 부과하거나 입력 유실. reconciliation 갭이 체계적으로 ~RTT/2에 비례(Phase 0 Recon HUD가 측정하는 바로 그것).
2. **값 smoothing(rate dilation 아님)** (High) — 큰 음수 오차 시 클럭 역행 위험.
3. **RTT 추정이 Mirror EWMA** (Medium) — min-RTT 샘플이 더 정확.
4. **서버 피드백 없음** (High, Phase 4) — open-loop lead만으론 경로 변화/지터에 self-correct 불가.
5. **단일 입력 전송** (Medium) — 패킷당 최근 N틱 입력 **sliding-window 중복 전송**이면 단일 손실=1틱 손실 방지(싸고 효과 큼).

**이미 표준 정합:**
- **두 reconciler 분리** — `SnapReconciler`(내 캐릭, delta replay) vs `ServerStateReconciler`(원격 보간) + `SnapReconciler.LateUpdate` 지연 렌더링 = 9.2의 predicted/interpolation 타임라인 분리와 부분 정합(단 predicted에 lead 없음).
- 시작 시 서버 `gameInfo.Tick`으로 시드 = 권위 기준 시드(옳음).
- `processibleTick` accumulator = 표준 fixed-timestep(Fiedler "Fix Your Timestep").

### 9.4 ✅ Mirror `NetworkTime` 의미 검증 완료 (2026-06-20) — Phase 2는 `predictedTime` 활용(방향 A 확정)

우리 프로젝트 Mirror 소스(`Assets/Mirror/Core/NetworkTime.cs`) 직접 확인 결과:

- **`NetworkTime.time`(클라) = `NetworkClient.localTimeline` = 스냅샷 보간 클럭, 서버보다 `bufferTime`만큼 *뒤*.** Mirror 자체 주석이 명시: *"NetworkTime.time is server time, behind by bufferTime."* → 현 `LOPTickUpdater`가 `elapsedTime`을 `.time`으로 smooth하므로 **LOP 시뮬 클럭은 서버보다 bufferTime 뒤에서 달린다**(서버 시간선 "위"도 아닌 *뒤*). 입력 적시성 최악.
- **⭐ Mirror가 이미 표준 client-ahead 예측 클럭 `predictedTime`을 제공(LOP 미사용).** `predictedTime = localTime + predictionErrorUnadjusted`. `predictionErrorUnadjusted`는 **서버 피드백 폐루프** EWMA(20×100ms): 서버가 `OnServerPing`에서 `unadjustedError = 서버시간 − 클라 ping 송신시간`을 계산해 pong으로 회신, 클라가 누적. 수식상 **`predictedTime ≈ 서버 현재시간 + 편도지연(RTT/2)`** — 입력을 predictedTime으로 스탬프하면 서버가 그 tick에 도달할 때 도착("on time"). 표준이 처방하는 client-ahead 클럭 + 서버 피드백(=Phase 4의 핵심)이 *이미 자동 유지*되고 있음. (검증: LatencySimulation 50ms → 서버 `OnServerPing` predictionError가 0으로 수렴.)
- **두 타임라인이 Mirror 안에 그대로**: `time`(뒤, bufferTime) = 원격 보간용 / `predictedTime`(앞, 편도지연) = 내 캐릭 예측용. §9.2와 1:1. LOP는 현재 *둘 다 `.time` 하나*로 틱 구동 → 내 캐릭이 앞서지 못함.

**확정 결정 — 방향 A: Mirror `predictedTime` 재사용 (자체 lead/피드백 재발명 안 함).** Phase 2의 내 캐릭 시뮬 틱을 `predictedTime` 기반으로:

```csharp
// predictedTime ≈ 서버 현재시간 + 편도지연 (RTT/2 이미 포함, 서버 피드백 폐루프로 자동 유지)
// 오버워치식 ahead = 편도지연을 또 더하는 게 아니라 jitter/+1프레임 마진만 추가 (latency 이중계산 금지)
double aheadMargin = 0.030; // +지터버퍼/+1프레임, 시작 30ms 튜닝
double targetClientTime = Mirror.NetworkTime.predictedTime + aheadMargin;
// 이 타깃으로 "값 smoothing"이 아니라 rate time-dilation으로 수렴
//   (predictedTime은 hard offset correction=점프 가능 → 우리 dilation 레이어가 흡수; 값 SmoothDamp 금지=역행)
// 원격 엔티티는 NetworkTime.time/스냅샷 보간 유지 (별 타임라인)
```

**A의 caveat(Phase 2 설계 시 처리):** ① `predictedTime`은 hard offset correction(점프)이라 틱 클럭에 직결 금지 → dilation/smoothing 한 겹 필수. ② jitter 마진 미포함(정확히 서버+편도지연 타깃) → 위 `aheadMargin` 추가. ③ `predictionErrorUnadjusted` EWMA 창 2초 — 반응성 필요시 평가. **B(자체 RTT/2+피드백 구현)는 기각** — Phase 4 폐루프를 Mirror가 이미 제공하므로 재발명 불필요.

### 9.5 구조/네이밍 — TickUpdater 분리 (durable 가이드)

- **TickUpdater를 별도 클래스로 빼고 GameFramework generic 베이스 + 사이드별 override(클=Mirror smoothing, 서=자기 클럭)로 둔 것 = 표준 정합, 유지.** 모든 엔진이 전용 time/tick 서브시스템을 가지며, 시간 동기는 본질적으로 사이드별이라 connection-architecture가 `ITickUpdater`를 I/O 어댑터로 분류한 것이 정확.
- **단, 현 단일 `TickUpdater`가 3책임을 혼재**: (1) 클럭 동기/추정(시간 출처+lead+dilation, 현 `OnElapsedTimeUpdate` override), (2) 틱 루프 구동(accumulator), (3) 시간 값 보유(tick/interval/elapsed). 업계는 분리: NGO=`NetworkTimeSystem`(동기)+`NetworkTickSystem`(구동)+`NetworkTime`(값 struct); FishNet=`TimeManager`(통합·멤버 분리). LOP는 이미 `GameEngine.Time`(값 facade)가 값 책임을 분리 → 동기·구동이 한 클래스에 엉킨 게 잔여 문제.
- **권고(언제):** 지금 rename/분리는 churn이라 안 함. **Phase 2에서 lead/dilation을 붙이는 시점**에 `OnElapsedTimeUpdate`가 god-method화하므로, 그때 **"클럭/시간소스 추정"(사이드별)을 별도 seam으로 추출**하고 틱 구동은 generic 유지. 네이밍도 그때 책임 기반으로 — `TickUpdater`는 *구동*엔 맞지만 *동기/추정*까지 담기엔 좁음. 통합 시 `TimeManager`, 분리 시 `Clock`/`TimeSync`(추정) vs `TickDriver`(구동) 류.
- **라이브러리 교체(NGO/FishNet)는 불필요** — 동작하는 Mirror 커스텀 스택을 표준 쪽으로 *진화*시키는 게 맞음(아래 Phase 2~4가 그 경로).

### 9.6 Phase 2~4 정련 (이 리서치가 기존 계획을 검증 + 보정)

리서치는 §5의 Phase 2(clock sync)/3(input buffer)/4(time dilation 피드백) 방향이 **정확히 표준**임을 확인. 단 다음을 반영:

- **Phase 2 (방향 A 확정, §9.4)** — 내 캐릭 틱을 **`NetworkTime.predictedTime`(=서버시간+편도지연, 서버 피드백 폐루프 내장) + `aheadMargin`(지터/+1프레임)** 기반으로. RTT/2·서버 피드백을 자체 구현하지 않음(predictedTime이 제공). 틱 클럭은 **값 SmoothDamp 아닌 rate time-dilation**으로 타깃 수렴(predictedTime 점프 흡수). 원격은 `.time`/스냅샷 유지.
- **Phase 3** — 서버 input 버퍼를 "클라 tick == 서버 tick"에 정렬(=표준 command-frame).
- **Phase 4** — predictedTime의 서버 피드백 폐루프가 *클럭 lead* 자체는 자동 보정하므로 별도 신설 불필요. 다만 **서버 input-buffer 점유 기반 미세 조정**(starvation/overflow)이 추가로 필요하면 그때 도입. Overwatch 계단식 표(>32틱=¼ 보정, 15~32=⅛, 1~7틱 early=dead-zone, late=가속)가 참조 구현.
- **추가(저비용·고효과)** — sliding-window 입력 중복 전송(§9.3-5), 원격 엔티티 적응형 jitter buffer.

### 9.7 출처 (대표)

- Unity NGO NetworkTime/Ticks, Netcode-for-Entities Time Synchronization(DeltaTime 스케일링), FishNet TimeManager API — 각 공식 문서.
- Overwatch: [Edgegap deep dive](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback), [Daposto Overwatch model](https://daposto.medium.com/game-networking-9-bonus-overwatch-model-4faba078cf05), [GDC Vault(Tim Ford)](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and).
- 1차 원리: [NTP FAQ algo](https://www.ntp.org/ntpfaq/NTP-s-algo/), [Gambetta](https://www.gabrielgambetta.com/client-server-game-architecture.html), [Gaffer Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/), [SnapNet snapshot-interpolation](https://snapnet.dev/blog/netcode-architectures-part-3-snapshot-interpolation/).
- Unreal: [Josh Sutphin — syncing UE network clock](https://joshsutphin.com/blog/accurately-syncing-unreals-network-clock.html)(NTP식 핸드셰이크), [Vorixo non-destructive synced clock](https://vorixo.github.io/devtricks/non-destructive-synced-net-clock/).

### 9.8 네이티브 clock sync (방향 B) — 측정 한계 & 보류 결정 (2026-06-21)

`INetworkTime` 추상화(NetworkTime 슬라이스) 위에 **Mirror `predictedTime`을 자체 ping/pong으로 대체**하는 네이티브 구현(방향 B)을 검토 → **보류**. 동기는 "더 순수·정확한 latency 측정 + Mirror-time 독립 + 튜닝 자유"였으나, *정확도* 동기는 Mirror transport 위에서 달성 불가임이 드러남:

- **Mirror transport를 쓰는 한 ping/pong 타임스탬프는 메인스레드에 묶인다.** Mirror가 데이터를 메인스레드 update에서 우리 코드로 마샬링하고, 기본 transport(kcp2k)는 소켓 recv 자체가 `LateUpdate`(메인스레드)다. → 우리가 어디서 타임스탬프를 찍든 **메인스레드 시각**이라 별도 스레드를 돌려도 순수 와이어 지연을 못 얻는다(= "Mirror transport + 별도 스레드"는 양립 불가, 효과 없음). 측정 정확도가 **Mirror 자신의 predictedTime과 동급**.
- **순수 와이어 측정 ⟺ Mirror transport를 떠나야 한다.** 전용 UDP 소켓 + 전용 스레드에서 소켓 send/recv 직전에 고해상도 Stopwatch 타임스탬프 = **별도 networking 인프라**(포트/NAT/Mirror 연결과 매칭 handshake/스푸핑 방지) = 큰 작업.
- **min-RTT 통계 정제는 메인스레드 오염을 *부분* 회복**: 프레임 지터는 *항상 지연을 더하기만* 하므로 창 내 **최소 RTT 샘플 ≈ 순수 와이어 지연**(NTP 방식). 로컬 프레임 히칭 spike는 최소값에서 자동 배제되고, 진짜 네트워크 지연 변화는 추적된다(맞음). 단 Mirror EWMA 대비 이득은 미미.
- **→ Mirror transport 위 네이티브 sync의 실질 가치 = 정확도 아니라 *구조적 Mirror-time 독립 + 튜닝 훅 소유*.** 정확도가 주동기였고 전용 소켓은 부담이라 **보류**(§9.4 방향 A = Mirror predictedTime 재사용 결론 재확인).
- **재개 조건 = Mirror 제거가 실제 안건이 될 때.** 그땐 (b) 구조독립+튜닝 목적 네이티브(Mirror transport + min-RTT) 또는 (c) 전용 소켓 순수 측정. **측정 레이어를 인터페이스(`ILatencyProbe` 류)로 추상화**하면 (b)→(c)를 무중단 교체 가능.
