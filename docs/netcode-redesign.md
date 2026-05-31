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
| (서버) `Assets/Scripts/Component/EntityInputComponent.cs` | 인풋 버퍼 + 처리 큐 |
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
| Rocket League | ❌ | 결정적 물리 + 풀 rollback |

LOP는 PhysX 기반(비결정적)이고 strict server-authoritative를 원하므로 → Overwatch 모델이 가장 직접적인 참고

---

## 5. 단계별 적용 계획

> 각 단계는 독립적으로 측정 가능. 한 단계 끝낼 때마다 공중 점프 시나리오에서 갭 크기를 측정해서 다음 단계로 넘어갈지 결정.

### Phase 0 — 측정 도구 마련 (선행 작업)

- [ ] 디버그 HUD에 표시: 클라 tick, 서버 tick(추정), RTT, 마지막 reconciliation distance
- [ ] 공중 점프 시나리오 재현 케이스 (반복 가능한 테스트 환경)
- [ ] 변경 전 baseline 측정: 평균/최대 reconciliation distance

### Phase 1 — 명백한 버그 수정 (효과: 작지만 즉시 적용 가치 있음)

- [ ] `LOPMovementManager.cs:30-33` SmoothDampAngle 수정
  - `ref` 변수를 컴포넌트 필드로 (예: `CharacterComponent.rotationVelocity`)
  - `Time.deltaTime` 대신 명시적 `(float)GameEngine.Time.tickInterval` 전달
  - `Mathf.SmoothDampAngle(currentRot, targetRot, ref field, smoothTime, Mathf.Infinity, tickInterval)` 형태 사용
  - 또는 자체 보간 (현재 0.01f smoothTime은 사실상 즉시 회전이므로 단순 `Mathf.LerpAngle` 도 검토)

### Phase 2 — Clock Sync (Overwatch 모델의 절반)

목적: 클라가 서버 시간선보다 앞쪽에서 시뮬레이션하도록 변경

- [ ] `LOPTickUpdater.cs` 수정: target time에 lead 오프셋 추가
  ```csharp
  // 의사 코드
  double oneWayLatency = Mirror.NetworkTime.rtt * 0.5;
  double jitterBuffer = 0.030;  // 30ms 시작값, 추후 튜닝
  double clientLeadTime = oneWayLatency + jitterBuffer;
  double targetClientTime = Mirror.NetworkTime.time + clientLeadTime;
  // 기존 smoothing은 그대로
  ```
- [ ] Lead time 변동 시 갑작스러운 점프 방지 (lead 자체도 smoothing)
- [ ] RTT 급변(예: ping spike) 시 안전장치 (max delta, freeze 등)

### Phase 3 — Server Input Buffer 정렬

목적: 서버가 "인풋의 클라 tick == 자기 tick"에 인풋을 처리하도록

- [ ] `EntityInputComponent.cs` 수정
  - 현재 `GetInput(serverTick)`: `tick <= targetTick` 인 가장 오래된 인풋
  - 변경 후: `tick == serverTick` 인 인풋 (또는 `tick <= serverTick`이지만 가장 가까운 것)
  - `INPUT_DELAY_TICKS`를 jitter buffer로 명확히 정의 (예: 2틱)
- [ ] 인풋이 해당 tick에 도착 안 했을 때 정책 결정
  - 옵션 A: 마지막 인풋 반복 (input prediction)
  - 옵션 B: no-input으로 진행 (가만히 있음)
  - 추천: A로 시작
- [ ] 인풋이 너무 늦게 도착 (이미 처리 시점이 지남) 시 정책
  - 옵션 A: 무시 (drop)
  - 옵션 B: 가장 최근 tick에 강제 처리
  - 추천: A로 시작 + drop count를 클라에 알림 (Phase 4용)

### Phase 4 — Time Dilation 피드백 루프 (선택)

목적: 클라 lead가 항상 적절한 값을 유지하도록

- [ ] 서버가 클라 인풋 도착 분포 모니터링 (몇 틱 일찍/늦게 오는지)
- [ ] 서버 → 클라 시계 조정 메시지
- [ ] 클라가 잠시 ±1% 정도 빠르거나 느리게 진행
- 우선 Phase 2/3만으로 충분한지 확인 후 필요시 진행

### Phase 5 — 점프 임펄스 처리 개선 (보조)

목적: y-velocity 리셋이 시점 차이에 덜 민감하도록

- [ ] 점프 처리 방식 검토
  - 현재: `vy = 0` 리셋 후 `+JumpPower`
  - 대안 A: 그냥 `+JumpPower` (낙하 중 점프는 약하게 느껴짐 — 게임 디자인 문제)
  - 대안 B: `vy = max(vy, 0) + JumpPower` (낙하 상쇄 + 점프력 보존)
  - 대안 C: 점프 입력에 클라가 본 vy 값 첨부, 서버가 그 vy 기준으로 처리
- 게임플레이 의도와 절충 필요. Phase 2/3 효과 확인 후 결정

### Phase 6 — 종합 검증

- [ ] Baseline 대비 갭 크기 비교 (평균, 95th percentile, max)
- [ ] 다양한 RTT (50ms / 150ms / 300ms) 시나리오 측정
- [ ] 다양한 시나리오 (지상 이동, 공중 낙하 중 점프, 연속 점프, 가만히 있을 때) 측정
- [ ] 시각적으로 rubberbanding 확인 (육안 + 녹화)

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
- LOPGameEngine ↔ LOPGameSimulation 컴포지션 (Slice 4) 도입 후, 현 `SnapReconciler`의 *delta replay* 방식을 *Snapshot/Restore + input replay* 방식으로 자연 수렴 가능 — Stage ④에서 평가
