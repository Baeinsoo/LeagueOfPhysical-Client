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
| 07-10 | **확정 게이트 — 재생 억제 (방식 1)** — `WorldEventBuffer.Suppress()`(GameFramework) + `Reconciler` cue 손-회피 제거(라이브/재생 발동 경로 통일). 재생이 만든 연출을 억제 스코프가 버림 | `specs/2026-07-09-commit-gate-replay-suppression-design`, `plans/2026-07-09-commit-gate-replay-suppression` |
| 07-10 | **A1 — `DeterministicRandom`** (SplitMix64 결정론 난수 struct, GameFramework, 엔진 비의존) — 클라 예측 전투(A)의 첫 조각. 씨앗 유도·서버 배선·IRandom 교체는 A2 | `specs/2026-07-10-deterministic-random-primitive-design`, `plans/2026-07-10-deterministic-random-primitive` |
| 07-12 | **A2.1 — 서버 전투 키 RNG + 매치시드** (4저장소) — `LOPCombatSystem`이 키 `hash(matchSeed,tick,attacker,target,effectIndex)`로 크리/회피, `Hashing` 헬퍼, `AbilityEffectContext.EffectIndex`, `GameInfo.match_seed` 서버→클라 동기(클라 보관, A2.3 흡수). combat만 | `specs/2026-07-12-a2-1-server-combat-keyed-rng-design`, `plans/2026-07-12-a2-1-server-combat-keyed-rng` |
| 07-12 | **A2.2a — 전투 해소 LOP-Shared 공유화** — `LOPCombatSystem`을 서버→LOP-Shared 공유 concrete(`World.Entity`+씨앗 param), `ICombatSystem` 제거, `DamageEffectHandler` 배선. 이동으로 전투 해소 EditMode 테스트 가능. (+별도 밸런스: 데미지 3배) | `specs/2026-07-12-a2-2a-combat-resolution-shared-design`, `plans/2026-07-12-a2-2a-combat-resolution-shared` |

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

**셋 다 A(클라 예측 재구성)에 수렴한다 — commit gate 이후 깨끗한 독립 Stage④ 곁다리는 없다.** 현재 진행 중인 항목 없음.

1. **클라 측 예측 전투 생성 (A — 키스톤)** — 내 히트/데미지/크리를 클라가 로컬에서 굴려 서버 답 기다리지 않고 **즉시 연출** → 서버 확정과 reconcile. **B·RNG·C(표준 IInputSource)가 모두 이걸 기다리는 실제 열쇠.**
   - **A1 완료(07-10):** `DeterministicRandom`(SplitMix64). **A2.1 완료(07-12):** 서버 combat 키 RNG + 매치시드 동기. **A2.2a 완료(07-12):** 전투 해소를 LOP-Shared 공유 concrete로(클·서 같은 코드). **남은 A2 =** **A2.2b** 히트 판정 공유화 · **A2.4** 클라 `DamageEffectHandler` 등록(예측 히트 생성, 저장된 매치시드로 같은 키 RNG) · **A2.5(=B)** 서버 확정 대조·예측/확정 reconcile(확인/취소=예측 실패 보정).

   #### A2.2b 접근 확정 (2026-07-12, 브레인스토밍 — spec/구현은 다음 세션) — **접근 1: 오버랩 포트 + 공유 드라이버**
   - 현재 서버 `DamageEffectHandler.OnActiveEnter` = ①`Physics.OverlapSphere`→Collider[] ②collider→`LOPEntity`→entityId ③부채꼴 필터(`IsInAttackSector`)→공유 `LOPCombatSystem.Attack`. ①②와 matchSeed가 사이드 특화라 공유 blocker.
   - **`IOverlapQuery`**(GameFramework 인터페이스, `ICollisionQuery` 짝): `entityId[] OverlapSphere(System.Numerics.Vector3 center, float radius)`. 구체는 **사이드별**(`LOPOverlapQuery` 클·서)이 `Physics.OverlapSphere` + collider→`LOPEntity`.entityId 매핑(LOPEntity가 사이드 타입이라 구체는 각 레포).
   - **부채꼴 필터 + Attack 루프는 LOP-Shared 공유** — numerics(`World.Transform.Position`=Numerics.Vector3, `Rotation`=Numerics.Quaternion; forward=`Vector3.Transform(UnitZ, Rotation)`). Unity transform 대신 **World.Transform**(진실원본) 사용 = 엔진 프리 + 더 정확.
   - **matchSeed는 `IMatchSeed`**(양쪽 `MatchSeed`가 구현하는 공유 인터페이스)로 공유 핸들러에 주입 → 핸들러가 사이드 무지.
   - **`DamageEffectHandler`를 LOP-Shared로**(공유 concrete), 서버가 `IOverlapQuery`(서버 구체)·`IMatchSeed`(서버 MatchSeed)·`LOPCombatSystem`·`EntityRegistry` 주입해 등록. 클라 등록은 A2.4.
   - 근거: A2의 목표가 "클·서 같은 코드로 예측" → 판정까지 공유해야 A2.4가 판정 재구현 없이 깔끔. (접근 2=순수 로직만 공유는 A2.4에서 OverlapSphere+매핑 중복이라 기각.)
   - 열린 디테일(spec에서): `IMatchSeed`·`IOverlapQuery` 위치(GameFramework vs LOP-Shared), `World.Transform.Rotation`이 실제 facing과 정합인지(키네마틱 이행 후), 부채꼴 판정에 Unity vs World 위치 차이 영향.
   - 필요: 클 `DamageEffectHandler` 등록(내 캐릭 예측 히트 생성) + 서버 확정 대조.
   - **결정론 RNG는 별도 트랙이 아니라 이 안에 포함** — 시드 스트림이 아니라 **counter-based/키 기반**(`값 = hash(매치시드, tick, entityId, 굴림종류)`). 이유: 예측-보정은 클·서가 같은 글로벌 스트림을 안 돌려 호출 횟수가 달라짐 → 스트림 방식은 데스싱크. 키 기반은 굴림마다 독립 재현이라 횟수 무관. `IRandom`(GameFramework)에 counter-based 구현 drop-in(문서가 이미 예고). 상세·근거: `[[deterministic-rng-counter-based]]`.
   - Stage④ "클라 측 Generation(예측 액션/공격)" 트랙과 동일.

2. **B — 예측/확정 이벤트 machinery = 방식 3 (해시 dedup)** (A 필요 + 완료된 commit gate 위에 얹음)
   - 방식 1(재생 억제)을 **방식 3(이벤트 (내용+틱) 해시 dedup, Quantum식)**으로 확장: 재생이 이벤트를 다시 만들되 **이미 낸 것과 같으면 버리고, 예측이 틀려 달라졌으면 내보낸다.** 내 예측 연출을 확정 전 즉시 표시 → 서버 확정 도착 시 맞으면 유지 / 틀리면 취소.
   - **필요 부품:** ① `WorldEvent`에 **틱 도장**(방식 1은 안 넣음) ② A의 예측 이벤트 생산자 ③ **취소 방향** ④ 상황 X 흡수 — 서버 사본 self-skip(`GameAbilityMessageHandler`)을 해시 dedup으로 통합.
   - **완료된 commit gate(방식 1)가 깐 토대:** `WorldEventBuffer.Suppress()` 단일 egress 제어점 + "라이브만 연출 / 재생은 스킵" baseline. B는 그 위에 dedup + 취소만 얹는다.
   - 설계 결정 박제: `[[event-model-wire-decision]]`.

3. **`IInputSource` 표준 provider (4d)** (A 필요 — **독립 아님**)
   - 입력 캡처를 `Poll(tick)→data` provider(Quantum `PollInput` / Unity Netcode `ICommandData`)로. capture+예측+송신 묶인 현 `PlayerInputManager.ProcessInput`을 표준 모양으로 분리.
   - **왜 A에 묶이나:** 표준 provider는 입력을 *읽기 전용 데이터*로만 내주고 **예측 적용·송신을 입력 경로 밖**(world Collection→Mutation / 호스트)으로 빼야 성립 = 클라 예측 재구성(A). 그래서 A 없이는 표준 모양이 안 나온다.
   - **독립인 wrap-only(`void ProcessInput()`)는 거부됨(2026-07-01)** — 비표준 verb라 나중 표준화 시 인터페이스가 깨짐(rename churn, CLAUDE.md 명명 규칙 위반). 보류 spec: `specs/2026-06-30-slice4-input-source-port-design`.

> 순서: 셋 다 A에 의존 → **A(1)가 먼저**, B(2)·C(3)는 A 후. **결정론 RNG는 독립 트랙 아님 → A에 흡수(counter-based).**
> (별개 정리) `fix/reconciler-tick-guard`의 `[임시]` 틱 가드 제거 — Stage④ 스냅샷 타임라인 재설계 때.

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
