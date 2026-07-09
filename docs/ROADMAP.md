# LOP 로드맵 — 한 일 / 할 일 / 파킹

이 문서는 **"지금 어디까지 왔고, 다음이 뭔지"의 단일 원천**이다. 슬라이스/워크스트림 단위 상태만 여기 둔다.

> 왜 이 파일이 필요했나: 상태가 메모리 status 파일(~11개) + 아키텍처 문서의 "상태/backlog" 절 + 58개 spec + 74개 plan에 **4겹으로 흩어져** 있어서, "다음에 뭐 하지?"를 한 곳에서 볼 수 없었다. (실제로 메모리에 "파킹"으로 적힌 항목이 이미 머지돼 있는 stale도 발생했다.)

## 경계 규칙 — 뭐가 어디 사는가 (필독)

| 종류 | 사는 곳 | 예 |
|---|---|---|
| **상태** (한 일/다음/파킹) | **이 파일** | "commit gate = 다음", "kinematic 이행 = 완료" |
| **왜 / gotcha / 결정 / 제약** (코드로 안 드러나는 durable 지식) | **메모리** (`~/.claude/.../memory/`) | "스폰 flush 통과→Depenetrate", "Mirror transport=메인스레드라 ping 정확도 이득 미미" |
| **구조 / 설계 계약 / 컨벤션** (선언적, 시간 비의존) | **아키텍처 문서** (`docs/*.md`) | connection-architecture, netcode-redesign(모델), topology |
| **태스크 상세** (단계·근거·체크리스트) | **spec / plan** (`docs/superpowers/`) | 각 슬라이스의 설계·구현 계획 |

**따라서:**
- 메모리 status 파일은 status 역할을 벗고 **durable 지식만** 남긴다 → 손댈 때 기회 있을 때 얇게 정리(지금 일괄 이관 X — YAGNI).
- 아키텍처 문서의 "상태" 절은 세부 진행을 적지 말고 **이 파일을 가리킨다**.
- 완료 원장의 "한 줄"은 spec/plan 링크만 걸고, 상세는 그 문서에 둔다.

---

## ✅ 한 일 (Done ledger)

최근 활성 워크스트림(넷코드 / 이동 / Stage④) 중심. 오래된 완료 워크스트림은 맨 아래 요약 + 메모리 링크.

### Stage④ + 넷코드 + 이동 (2026-07, 시간순)

| 날짜 | 슬라이스 | spec/plan |
|---|---|---|
| 07-01 | **Motion 권위 → World.Entity** (Slice 4 마무리 문서 포함) | `specs/2026-07-01-stage4-motion-world-authority` |
| 07-02 | **input-as-data** — `InputCommand` + `InputBuffer` World 컴포넌트, 이동을 `LOPWorld.Tick`으로 | `specs/2026-07-02-input-as-data`, `-4e-velocity-apply-to-world`, `-4e-dash-world-direct-velocity-bridge` |
| 07-04 | **Stage④ slice 2 — SnapshotHistory** 기록 + DebugHud | `specs/2026-07-04-stage4-snapshot-history` |
| 07-04 | **Stage④ slice 3 — 하드 롤백 reconcile** 복원+재생 | `specs/2026-07-04-stage4-rollback-reconcile` |
| 07-04 | **Stage④ — 원격 엔티티 kinematic** + 가드 | `specs/2026-07-04-stage4-remote-kinematic` |
| 07-05 | **Stage④ — 어빌리티/상태이상 예측 replay** (풀 상태 스냅샷 + 풀 틱 재조정) | `specs/2026-07-05-stage4-ability-replay` |
| 07-05 | **velocity 단일 권위 + 기여 모델 slice 1** (MovementSystem = velocity 유일 writer) | `specs/2026-07-05-velocity-motor-contribution-slice1` |
| 07-05 | **넉백 slice 2** — 스냅 수신 + Reconciler 스냅 복원 (Additive 기여 첫 실사용) | `specs/2026-07-05-velocity-knockback-slice2` |
| 07-06 | **넉백 MasterData 승격** — AbilityDataProvider 매핑 | `specs/2026-07-06-knockback-masterdata-promotion` |
| 07-06 | **멀티틱 입력 기아 수정** (걷기 정상화) | `specs/2026-07-06-multi-tick-input-starvation` |
| 07-06 | **렌더 보정 offset-decay 이관** (`RenderCorrectionSmoother`) | `specs/2026-07-06-render-correction-smoothing` |
| 07-07 | **원격 엔티티 표준 스냅샷 보간** (receive-anchored 재생시계 + Hermite + 적응형 쿠션) | `specs/2026-07-07-remote-entity-interpolation` |
| 07-09 | **공유 키네마틱 캐릭터 컨트롤러 이행** (slice 1~3, 4레포 main) — velocity·위치 권위 = `World.Entity`, 예측=권위로 지면 recon 소멸 | `specs/2026-07-09-shared-kinematic-character-controller-design`, `plans/2026-07-09-kinematic-*` |
| 07-09 | **Depenetrate 공유 헬퍼 추출** (`KinematicDepenetration`, 클·서 중복 제거) | — |

### 그 밖의 완료 워크스트림 (요약 — 상세는 메모리)

- **Slice 4 (Runner→World 추출)** 리네임 + 4a~4c + I/O 어댑터 — `[[world-core-runner-world-naming]]`
- **어빌리티/상태이상 World Core** (레거시 Action/Status → Ability/StatusEffect, behavior 조합 B0, B1 attack=DamageEffect) — `[[ability-statuseffect-world-core]]`
- **World Core 뷰 이행** (Health/Mana/Level/Stats/Ownership 단일 진실원본, 클·서 패리티) — `[[world-core-view-migration-status]]`
- **넷코드 Phase 0~4** (clock sync + server input buffer + timing feedback) — `[[netcode-migration-status]]`
- **NetworkTime 추상화** (`GameEngine.NetworkTime` facade, 클·서) — `[[netcode-migration-status]]`
- **UI Toolkit 마이그레이션 M1~M5a** — `[[uitoolkit-migration-status]]`
- **MasterData Luban 전환** (α/β/γ) — `[[masterdata-slice-2b-2c-roadmap]]`, `[[masterdata-key-convention]]`

---

## ▶ 다음 (Next — 순서 있음)

### Stage④ 남은 트랙 (netcode-redesign.md §5 프론티어)

1. **확정 게이트 (commit gate) + 통합 fan-out** ← **현재 브레인스토밍 중**
   - 이벤트에 **틱 도장** + Quantum식 **해시(이벤트 데이터 + 틱) dedup 게이트**를 씌워, 재생(replay) 틱의 연출이 중복 fan-out되지 않게 한다. 지금 `Reconciler`가 손으로 하는 cue 회피(`AbilitySystem.TryActivate` 직접 호출)를 **구조로 대체**.
   - 통합 fan-out: egress 경로(클 EventBus / 서 wire)를 게이트된 **단일 드레인**으로.
   - 관련 임시코드 청소: `fix/reconciler-tick-guard`의 `[임시]` 틱 가드(`TryGetValue` 스킵)를 걷어내고 틱 정렬 제대로.
   - 산업 근거(웹 검증 2026-07-09): Quantum "이벤트 데이터+ID+틱 해시로 중복 제거", `synced` 이벤트는 서버 확정까지 뷰 전달 지연 / non-synced는 즉시(틀리면 취소).

2. **B — 예측/확정 이벤트 machinery** (commit gate 위에 얹음)
   - 내 캐릭이 만든 **예측 이벤트를 확정 전 즉시** 연출(데미지 숫자 등) → 서버 확정 도착 시 **맞으면 확정 / 안 나타나면 취소**. Quantum non-synced 이벤트 모델의 취소 방향.
   - **선결 조건 (이게 B를 지금 안 하는 이유):** 오늘 클라엔 **예측 이벤트 생산자가 없다** — 데미지=서버권위라 클라 `AbilityEffectExecutor`에 `DamageEffectHandler` 미등록. B의 취소/확정 배관을 지금 깔면 **파이프에 물이 없어** 끝-끝 검증 불가. → 먼저 **클라 측 예측 전투 생성**(client `DamageEffectHandler` 등록 + 내 히트 예측)이 있어야 B가 돌아간다.
   - **A(commit gate)가 B의 뼈대 ~80%** (틱 도장 + 해시 dedup 게이트)를 이미 깐다. B가 추가하는 건 **취소 방향 + 예측 생산자**뿐.
   - 설계 결정 박제: `[[event-model-wire-decision]]` — durable=snapshot / transient-cosmetic=event, 예측 즉시표시+보정.

3. **완전 결정론 RNG** — 시드 기반 RNG로 크리/회피/스프레드가 클 예측·서버에서 동일 재생. **B의 예측 전투에 RNG가 들어오기 전 선결.** (산업 표준: "RNG를 입력처럼 취급 — 시드를 상태에 넣거나 입력에 실어 보냄".)

4. **`IInputSource` 표준 provider (4d)** — 입력 캡처를 `Poll(tick)→data` provider(Quantum `PollInput` / Unity Netcode `ICommandData`)로. capture+예측+송신 묶인 현 `PlayerInputManager.ProcessInput`을 표준 모양으로 분리. spec 보류 표시: `specs/2026-06-30-slice4-input-source-port-design`.

> 위 1→2→3 순서는 의존이 강제한다: B는 A 필요, RNG는 B의 예측전투 전제. 4d는 독립(언제든).

### 넷코드 잔여 (Stage④ 밖)

- **Phase 5 — 점프 임펄스 vy** ⏸ 보류(게임 디자인 콜). `[[netcode-migration-status]]`

---

## ⏸ 파킹 (Parked — 미룬 것 + 재개 조건)

| 항목 | 왜 미뤘나 | 재개 조건 |
|---|---|---|
| **Recon 엔티티-로드 러버밴딩** | 엔티티 많을 때 빈 곳 점프에도 recon 러버밴딩 관찰. 진단틀(A 서버틱밀림 vs B 클라FPS) 프로토타입 후 롤백 | 각 잡고 재개. `[[recon-entity-load-parked]]` |
| **네이티브 clock sync (방향 B)** | Mirror transport=메인스레드라 ping/pong 정확도 이득 미미; 순수측정=전용소켓+스레드 큰 작업 | **Mirror 제거가 실제 안건이 될 때.** `[[netcode-migration-status]]` §9.8 |
| **M5b — LOP.UI 인프라 GameFramework 승격** | 단일 클라라 YAGNI | 서버도 같은 UI 인프라가 필요해질 때. `[[uitoolkit-migration-status]]` |
| **넷코드 status 메모리류 `GameFramework.Netcode` 수렴** | 흩어진 클래스 일괄 이동은 YAGNI | 각 클래스 손댈 때 기회 있을 때. `[[netcode-namespace-consolidation]]` |
| **MasterData `file:` → git URL + tag 전환** | 안정화 후 결정 | 패키지 3종 함께 전환 시점. topology Open Decisions |
| **게임 씬 스코프 분리** (GamePlay 씬) | 설계 박제만, 즉시 구현 X | 착수 시 `specs/2026-06-06-game-scene-scope-design` 기준 plan |

---

## 상태

이 파일은 **2026-07-09 생성**, Stage④ 프론티어 + 최근 넷코드/이동 워크스트림을 시드했다. 나머지 워크스트림 상태는 각 메모리에 남아 있으며, **손댈 때 기회 있을 때** 이리로 점진 이관한다(일괄 이관 안 함).
