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
| 07-12 | **A2.2b — 히트 판정 LOP-Shared 공유화** (4저장소) — `IOverlapQuery`(GameFramework 포트, `ICollisionQuery` 짝) + 사이드별 `LOPOverlapQuery`(엔진 broad-phase). 부채꼴 필터·자기제외·Attack 루프를 공유 `DamageEffectHandler`(LOP-Shared)로, `World.Transform`(numerics 진실원본) 기준. 씨앗은 `IMatchSeed`. EditMode 7테스트. 서버 판정 EditMode 테스트 가능화 + 이중타격 dedup 교정. | `specs/2026-07-12-a2-2b-hit-detection-shared-design`, `plans/2026-07-12-a2-2b-hit-detection-shared` |
| 07-13 | **reconciler tick-guard 근본 수정** — 로컬 지연 렌더(`LocalEntityInterpolator`)의 "절대 틱키 dict 조회 + `[임시]` skip 가드"를 `GameFramework.Netcode.SnapshotInterpolation.Solve`(연속 renderTime 브래킷 탐색, 범위 밖 hold → 미스 불가) + EditMode 7테스트로 교체. 시간 기준(Fiedler alpha) 유지. 원격은 07-07에 이미 해소. | `netcode-redesign.md` §8 |

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

**A(클라 예측 전투)의 데미지 트랙은 닫혔다 (2026-07-12 결정).** 이동은 이미 예측(키네마틱+Reconciler), 어빌리티 발동도 예측(self-skip). **데미지는 예측하지 않고 서버권위 재생 유지** — 넷코드/예측 에픽은 자연 일단락. 아래 잔여는 대부분 **예측 콘텐츠 대기(B)** 또는 **독립 정리**다. 현재 진행 중인 항목 없음.

1. **클라 측 예측 전투 생성 (A)** — ⏸ **데미지 예측은 안 짓기로 결정(2026-07-12).** 데미지 숫자는 서버 `DamageEventToC` 재생 유지(남 캐릭터와 동일). 근거: HP는 스냅샷으로 항상 정확 → 틀릴 수 있는 건 떠오르는 숫자(연출)뿐이고, 이동(게임필의 큰 축)은 이미 예측됨 → 데미지 숫자 ~RTT 지연은 수용(빠른 슈터 표준, YAGNI). **재검토 조건:** 근접 타격감이 지연으로 답답해질 때.
   - 완료 잔재(헛되지 않음): A1 `DeterministicRandom`, A2.1 매치시드 클라 동기(**휴면·준비됨**), A2.2a/A2.2b 전투 공유화(**서버 EditMode 테스트 + 이중타격 dedup 버그교정 = 독립 이득**). 상세: 위 Done 원장.
   - 결정론 RNG(counter-based)·클라 combat RNG 소비는 데미지 예측과 함께 **휴면**. `[[deterministic-rng-counter-based]]`.

2. **B — 예측/확정 이벤트 machinery (해시 dedup)** — ⏸ **예측가치 있는 스킬이 올 때** 그 실제 사례 2~3종과 함께 짓는다. 근거: 재사용되는 "대조 원장"(틱도장+해시 dedup)은 예측 이벤트가 여럿일 때 값을 하고, 연출별 "취소"(예: 데미지 플로터 제자리 교체)는 종류마다 새로 만들어야 해 지금 하나로는 상각 안 됨. 방식 1(재생 억제, `WorldEventBuffer.Suppress()`)이 완료된 토대. 설계: `[[event-model-wire-decision]]`.

3. **`IInputSource` 표준 provider (4d)** — ⏸ A(예측 확장)에 묶여 함께 보류. 독립 wrap-only는 거부됨(2026-07-01, `specs/2026-06-30-slice4-input-source-port-design`).

> ~~독립 정리: reconciler-tick-guard `[임시]` 틱 가드 제거~~ ✅ **완료(07-13)** — 위 Done 원장 참조(브래킷 탐색 교체).

### 넷코드 잔여 (Stage④ 밖)

- **Phase 5 — 점프 임펄스 vy** ⏸ 보류(게임 디자인 콜). `[[netcode-migration-status]]`

### 구조 정리 백로그 (2026-07-13 전반 감사)

전반 구조/구현을 업계 표준 대비 5영역 병렬 감사. 코드는 대체로 건강 — 유의미한 것만. 소스 레벨 확인됨.

- ✅ **#1 데미지 Amount 데이터 구동** (07-13) — `LOPCombatSystem.Attack`에 baseDamage 배선 → `DamageEffect.Amount` 소비(무동작; attack Amount=10=옛 하드코딩). 이제 Excel로 데미지 조정 가능.
- ✅ **#2 넉백 공유화 + `AttackSector` 추출** (07-13) — 넉백을 `IOverlapQuery`+`World.Transform`로 이관(마지막 World Core 우회 제거), 부채꼴 판정 공유 헬퍼화(Damage/Knockback 복사본 2벌 제거). 18 EditMode.
- ⏳ **#4 스냅샷 채널 reliable→unreliable (서브셋 청킹)** — 통짜 flip은 실패: Mirror unreliable은 조각내기 불가라 `EntitySnapsToC`(~1200B, 전 엔티티 한 메시지) > **1184B** → 드롭(플레이서 확인). **표준 해법 = 서브셋 청킹**(Quake/Source): 엔티티를 바이트 예산(~1100B)으로 나눠 **여러 EntitySnapsToC(같은 tick) unreliable** 송신 — 각 청크 독립이라 손실 시 그 엔티티만 한 틱 놓침(Fiedler fragment-재조립의 손실 복리 회피). **클라 무변경**(이미 메시지·엔티티별 loop + tick stale 무시). 델타 압축(C)은 엔티티 많아질 때 후속. UserEntitySnap은 reliable 유지(소용량, #5에서 결정). 근거: `[[snapshot-mtu-chunking]]`.
- ✅ **#5 `generate_protos.sh` MessageId 보존** (07-13) — 부모 스크립트의 `rm MessageIds.cs` 제거. `generate_message_ids.sh`의 기존-ID-보존 로직이 이제 작동(파일이 있어야 읽어 보존). 검증: 서브스크립트 재실행 시 13개 ID 전부 불변. 메모리 gotcha 해소 → `[[proto-message-id-regen-gotcha]]`.
  - **UserEntitySnapToC → unreliable은 안 함(결정):** 한 엔티티(내 HP/MP/레벨) 소용량이라 reliable head-of-line-blocking 비용이 작음. 번호표(tick)+가드 들일 값어치 낮음 → **reliable 유지.** (스냅=unreliable 정석은 고빈도·대용량 스트림[위치]에서 값을 함.)
- **신규(플레이 발견): 적(AI) 넉백 미적용** — `MotionContributionSystem.Resolve`가 `MovementSystem`(입력 게이트, InputBuffer 필요) 안에서만 실행 → AI(버퍼 없음)는 스킵, `EnemyBrain`이 velocity 직접 세팅. 넉백 기여는 붙지만 미해소. **AI 이동을 기여 채널에 태우면 해소**(외력을 플레이어·AI 공통화). 별개 게임플레이 수정. (#2 회귀 아님 — 이동 경로 문제.)
- **Tier-2/3 (별도 슬라이스):** #6 Reconciler 재생이 `LOPWorld.Mutation` 시스템 시퀀스 수기 복제(desync 실패 클래스) · #7 `WorldEventBatch` 단일 envelope 미구현(개념별 패킷 `DamageEventToC` 등) · #8 static `EventBus.Default`(DI/R3 밖) · `ctx.EntityManager` 탈출구 제거(#2로 마지막 소비처 정리 — 잔여 확인 후 제거) · 문서 stale(`entity-system-design.md` 전면, netcode-redesign, connection-arch backlog) · 크리/회피 상수 하드코딩 · `ctx.Target` 항상 자기자신(실 타게팅 없음) · 링버퍼 3중복 · 죽은 레거시 `Status` 매틱.

---

## ⏸ 파킹 (Parked — 미룬 것 + 재개 조건)

| 항목 | 왜 미뤘나 | 재개 조건 |
|---|---|---|
| **Recon 엔티티-로드 러버밴딩** | 엔티티 많을 때 빈 곳 점프에도 recon 러버밴딩 관찰. 진단틀(A 서버틱밀림 vs B 클라FPS) 프로토타입 후 롤백 | 각 잡고 재개. `[[recon-entity-load-parked]]` |
| **네이티브 clock sync (방향 B)** | Mirror transport=메인스레드라 ping/pong 정확도 이득 미미; 순수측정=전용소켓+스레드 큰 작업 | **Mirror 제거가 실제 안건이 될 때.** `[[netcode-migration-status]]` §9.8 |
| **M5b — LOP.UI 인프라 GameFramework 승격** | 단일 클라라 YAGNI | 서버도 같은 UI 인프라가 필요해질 때. `[[uitoolkit-migration-status]]` |
| **넷코드 status 메모리류 `GameFramework.Netcode` 수렴** | 흩어진 클래스 일괄 이동은 YAGNI | 각 클래스 손댈 때 기회 있을 때. `[[netcode-namespace-consolidation]]` |
| **MasterData `file:` → git URL + tag 전환** | 안정화 후 결정 | 패키지 3종 함께 전환 시점. topology Open Decisions |
| ~~**게임 씬 스코프 분리** (GamePlay 씬)~~ ⚠️ **감사(07-13): 이미 구현됨** — DI 감사가 Root→Room→Game 스코프 분리 + `EnqueueParent(roomScope)` + additive 로드 양쪽 확인. spec의 "미구현" 서술이 stale. 파킹 해제, 문서 정합만 남음(Tier-3 문서 stale에 포함). | — |

---

## 상태

이 파일은 **2026-07-09 생성**, Stage④ 프론티어 + 최근 넷코드/이동 워크스트림을 시드했다. 나머지 워크스트림 상태는 각 메모리에 남아 있으며, **손댈 때 기회 있을 때** 이리로 점진 이관한다(일괄 이관 안 함).
