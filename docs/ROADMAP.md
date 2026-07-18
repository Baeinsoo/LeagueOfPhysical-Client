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
| 07-14 | **공격 어빌리티 이동 정책** (6레포) — 어빌리티가 Startup/Active/Recovery **페이즈별 이동배율(0~1)** + `BlockJump`를 데이터로 선언(`TbAbility`), 공유 `MovementSystem.Tick`이 수평속도에 곱(플레이어=모터결과·AI=잔류속도 공통), 회전은 자유. 업계표준=격투 프레임데이터 + GAS 이속 modifier. Tasks 1-6 TDD(122 EditMode), 플레이 검증. 캔슬(벌처 킁)은 v2 | `specs/2026-07-14-attack-movement-policy-design`, `plans/2026-07-14-attack-movement-policy` |
| 07-15 | **키네마틱 지면 캐칭 수정** — `KinematicMover`가 수평+중력을 한 sweep으로 합쳐, 발이 바닥 flush일 때 그 sweep이 바닥을 dist≈0로 맞아 `moveDist=0` → **수평 이동까지 취소**(발-바닥 접촉 종이한장 차이로 간헐 발현). 표준대로 **수평/수직 스텝 분리 + step offset**(수평 sweep을 0.1 띄워 발밑 바닥 회피). TDD(`GroundPlaneQuery` 재현) 122/122. **기본형** — 경사 따라가기·명시 step-up·ground snap은 경사/계단 콘텐츠 시 후속 | — |

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

전반 구조/구현을 업계 표준 대비 5영역 병렬 감사. 코드는 대체로 건강 — 유의미한 것만. 소스 레벨 확인됨. **전체 findings 상세(file:line·심각도·노력): `docs/superpowers/audit-2026-07-13-structure.md`.**

- ✅ **#1 데미지 Amount 데이터 구동** (07-13) — `LOPCombatSystem.Attack`에 baseDamage 배선 → `DamageEffect.Amount` 소비(무동작; attack Amount=10=옛 하드코딩). 이제 Excel로 데미지 조정 가능.
- ✅ **#2 넉백 공유화 + `AttackSector` 추출** (07-13) — 넉백을 `IOverlapQuery`+`World.Transform`로 이관(마지막 World Core 우회 제거), 부채꼴 판정 공유 헬퍼화(Damage/Knockback 복사본 2벌 제거). 18 EditMode.
- ✅ **#4 스냅샷 채널 reliable→unreliable (서브셋 청킹)** (07-13, `e3d4496` — ROADMAP 상태만 stale이었음, 코드는 완료) — 통짜 flip 실패(Mirror unreliable 조각내기 불가, `EntitySnapsToC` >1184B 드롭) → **서브셋 청킹**(Quake/Source): 서버 `LOPRunner.EndUpdate`가 엔티티를 바이트 예산(`MaxEntityBytesPerMessage=1000`, `snap.CalculateSize()` 합)으로 나눠 **여러 `EntitySnapsToC`(같은 tick) `reliable:false`** 송신 — 각 청크 독립이라 손실 시 그 엔티티만 한 틱 놓침. 클라: 도착 기록 **틱당 1회 dedupe**(`GameEntityMessageHandler.lastRecordedArrivalTick` — 다중 메시지로 interval≈0→쿠션 폭증 방지), 소비자는 이미 tick-stale 무시(`RemoteEntityInterpolator`). 델타 압축(C)은 엔티티 많아질 때 후속. UserEntitySnap은 reliable 유지. 근거: `[[snapshot-mtu-chunking]]`.
- ✅ **#5 `generate_protos.sh` MessageId 보존** (07-13) — 부모 스크립트의 `rm MessageIds.cs` 제거. `generate_message_ids.sh`의 기존-ID-보존 로직이 이제 작동(파일이 있어야 읽어 보존). 검증: 서브스크립트 재실행 시 13개 ID 전부 불변. 메모리 gotcha 해소 → `[[proto-message-id-regen-gotcha]]`.
  - **UserEntitySnapToC → unreliable은 안 함(결정):** 한 엔티티(내 HP/MP/레벨) 소용량이라 reliable head-of-line-blocking 비용이 작음. 번호표(tick)+가드 들일 값어치 낮음 → **reliable 유지.** (스냅=unreliable 정석은 고빈도·대용량 스트림[위치]에서 값을 함.)
- ✅ **적(AI) 넉백 적용** (07-13) — 넉백 resolve가 `MovementSystem.Tick`의 입력 게이트 안에서만 돌아 AI(버퍼 없음)가 스킵되던 버그. 재사용 헬퍼 `MotionContributionSystem.ApplyToVelocity`(현 수평 velocity를 base로 외력 folding, y 보존·프루닝) 신설 + 서버 `MoveCharacters`에서 입력 비조종 캐릭에 호출(KinematicMove 통합 전). **공유 `MovementSystem.Tick`·클라 원격 경로 무변경** — 원격은 스냅 팔로워라 게이트 밖으로 빼면 스냅샷 권위 충돌 → 서버 host에서 AI만 folding. Shared 111 EditMode green + **인게임 육안 확인됨(몬스터 넉백 적용).** ⚠️ **임시 배치**(서버 분기) — 이상적 공통 루프 이전은 파킹(아래 "외력 처리 공통 루프 이전" 부채).
- ✅ **문서 stale 정합** (07-13, `docs/audit-stale-reconcile`) — `entity-system-design.md` 전면 재작성(코드 위치·타입명·enum값·컴포넌트/시스템 인벤토리 실제화) · `netcode-redesign.md §2.2`(input-as-데이터 축 + `InputBuffer` 실명; audit의 "예측 없음" 주장은 반박 — PlayerInputManager는 예측 트리거 유지) · connection-arch "괴리 #2" 해소 반영(`DeathCascadeSystem`, `LOPGame.HandleDeath` 삭제, death wire=`EntityDespawnToC` — `DamageDealtEvent.IsDead` 없음).
- ✅ **#3-WC `ctx.EntityManager` 탈출구 제거** (07-13) — 재검증 결과 완전한 죽은 pass-through(핸들러 전부 `EntityRegistry`+`IOverlapQuery`로 이전 완료). `AbilityEffectContext` 필드+ctor·`DriveActiveEntity` 파라미터·호출부 3곳·`Reconcile` 미사용 파라미터·테스트 5곳 정리. 동작 무변경.
- ✅ **#6 통합 World Tick (완료, 07-13)** — Reconciler 재생이 `LOPWorld.Mutation` 시퀀스를 수기 복제하던 desync 실패 클래스. **표준 정합**(클라 시뮬=예측 엔티티만 / 남·NPC=보간)으로 `Simulated` 마커 도입 → `world.Tick`이 그것만 순회 → 라이브==재생. spec `2026-07-13-unified-world-tick-client-sim-scope-design.md`, 3-슬라이스 분해(A/B/C). **✅ Sub-slice A 완료·머지**(07-13: `Simulated` 마커 + `Mutation` 순회 + driveeffects·외력 흡수, 넉백 부채 정산). **✅ Sub-slice B 완료·머지**(07-13: 클라 scope 축소[내 캐릭만] + `IMotionBridge` 포트 + 키네마틱 `world.Tick` 흡수 → **5페이즈 단일 진입점**). **✅ 후속 리팩터: 모션 브릿지 공유화**(07-13, 4 repo) — 처음엔 per-side `LOPMotionBridge` 2개(→클라 `IEntityManager` DI gotcha)였는데, 사용자 지적으로 **공유 concrete 1개(`MotionBridge`) + 공유 `PhysicsBody` 핸들 컴포넌트**로 통합(포트 유지, `UnityCollisionQuery`와 동형). 중복 + DI gotcha 동시 해소. **✅ Sub-slice C 완료·머지**(07-13: `Reconciler` 재생을 `world.Tick` 하나로 — 수기 5시스템 시퀀스 삭제). **→ 라이브·재생 둘 다 `world.Tick` = 두 벌 시퀀스 소멸 → `#6` 종결. `IWorld.Tick`이 단일 결정론 진입점 실현(클라=예측 엔티티만 시뮬 표준 정합 포함).**
- ✅ **죽은 코드 정리** (07-13) — #6-NC 레거시 `Status` 매틱 제거(구체 서브클래스 0, World Core StatusEffect가 대체) · #5-DM `MessageHandler<T>` 제거(4레포 사용처 0, 실 라우팅=`MessageFactory`+`EventBus`). 클·서 클린 컴파일.
- ✅ **#4-NC 링버퍼 3벌 → 공유 `SequenceBuffer<T>`** (07-16, feature 브랜치 `sequence-buffer-extract`) — `SnapshotHistory`/`InputHistory`/`PredictedAbilityStateHistory`가 각자 복제하던 `tick%capacity` 슬롯팅 + 병렬 tick 배열 stale 판별을 `GameFramework.Netcode.SequenceBuffer<T>` 하나로 흡수. 이름은 Fiedler "sequence buffer"(넷코드 표준, `RingBuffer`≠FIFO큐 구분). 순수 별칭이던 `InputHistory`/`PredictedAbilityStateHistory`는 삭제 → 호출처가 `SequenceBuffer<InputCommand>`/`<PredictedAbilityState>` 직접 사용, `SnapshotHistory`는 `Latest`/`Count`/tick-내장 `Record` 편의 있어 얇은 어댑터로 유지. GameFramework EditMode +10(269 green), 컴파일 클린.
- ✅ **#8 EventBus → MessagePipe (DI/R3 통일 + leak 구조적 해소)** (07-16, feature 브랜치 `eventbus-messagepipe-migration`, 클·서·GameFramework) — 전역 static 커스텀 버스(`GameFramework.EventBus`+앱 `EventBus.Default`/`EventTopic`)를 **Cysharp MessagePipe**(타입·keyed pub/sub + DI 스코프 브로커)로 이전. 웹 리서치 근거(패턴=표준/전역-static 형태=비표준/MessagePipe=R3 생태계 표준 답, `[[connection-arch]]`+spec). **①구독 IDisposable(AddTo) + Root 싱글턴 브로커**로 룸 재입장 leak 구조적 해소(②스코프 브로커는 교차스코프 복잡도 대비 redundant라 드롭). 5슬라이스: 0(도입) → 1(WebResponse/라이프사이클/ItemTouch) → 2(네트워크 수신=리플렉션 없는 `NetworkMessageDispatcher` 타입 라우팅) → 3(엔티티 keyed[키=entityId] + 죽은코드 트림[rotation/velocity 발행·컨트롤러 no-op]) → 4(버스 삭제). 정적/엔티티 컴포넌트=`GlobalMessagePipe`, DI 서비스/VM=`IPublisher`/`ISubscriber` 주입. IL2CPP 대비 `RegisterMessageBroker<T>` 명시 등록. 종합 플레이 검증 통과, EditMode 269 green, 커스텀 버스 전 레포 삭제. spec/plan `docs/superpowers/{specs,plans}/2026-07-16-eventbus-messagepipe-migration*`.
- ✅ **#7 WorldEventBatch 단일 envelope** (07-17, feature 브랜치 `world-event-batch-envelope`, Shared·Server·Client) — 개념별 top-level 패킷(`DamageEventToC`/`AbilityActivatedToC`)을 단일 폴리모픽 `WorldEventBatchToC`(oneof `WorldEventToC`)로 통합. 서버=버퍼를 배치 1개로 조립해 세션당 1회 송신, 클라=단일 `GameWorldEventMessageHandler`(oneof 순회 + `WorldEventWire.FromWire`, ability self-skip 보존), 변형 2개는 `@auto_generate` 제거로 nested-only 은퇴(MessageId/IMessage/creator 빠짐, 본문·필드번호 유지=바이너리 호환). 새 WorldEvent 타입이 MessageId/dispatcher/핸들러를 새로 안 요구. 공유 순수 매퍼 `WorldEventWire`(Generated 어셈블리) EditMode 6. 클라 EditMode 275/275, 최종 whole-branch 리뷰(3레포 교차) Critical/Important 0, 서버 컴파일 + 플레이 회귀 검증 통과. spec/plan `2026-07-17-world-event-batch-envelope*`.
- ✅ **전투 히트 해소 Part 1 — 닷지 on-hit 게이트** (07-17, feature 브랜치 `feature/combat-hit-resolution`, Shared·Server·Client) — 버그(닷지해도 넉백 당함)를 표준 구조로 수정. **데미지=히트 정의자**(per-attack 닷지 판정 + 명중 대상을 발동당 `AttackHitContext`에 기록), **넉백=on-hit 라이더**(명중 대상만 밀기, 자체 타게팅/닷지 제거). 닷지=per-attack seed(effectIndex 제거→모든 효과 동일 답), 크리=per-effect seed. 넉백 `Range/Angle` 제거(히트 형상=데미지). 업계표준=WoW attack table/GAS/LoL on-hit. EditMode(AttackHitContext 3+LOPCombatSystem 7+Knockback 3), 최종 whole-branch 리뷰 Critical/Important 0, 인게임 검증(닷지→안 밀림/명중→데미지+넉백). spec/plan `2026-07-17-combat-hit-resolution-*`. `[[combat-hit-resolution]]`
- ✅ **전투 히트 해소 Part 2 — 크리/회피 상수 MasterData 승격** (07-17, feature 브랜치 `feature/combat-config-masterdata`, infra·MasterData-Server·Shared·Server) — 하드코딩 회피/크리 확률·배수(0.05/0.95/0.05/0.50/1.25/1.75)를 새 전역 테이블 **`TbCombatConfig`**(단일 행 id=1, **서버 group `s`** — `LOPCombatSystem`이 서버 전용 등록이라 클라 불필요)로 승격. Luban 저작(python openpyxl `#CombatConfig.xlsx`+`__tables__` 행)→gen.sh 재생성→서버 `CombatConfigProvider`(AbilityData 패턴)→`CombatConfig` struct(Shared) 주입. 기본값=구 하드코딩이라 **동작 무변화**(밸런스만 Excel 조정 가능). `TableFiles` 배열 갱신 필수. EditMode 9, 최종 whole-branch 리뷰(4레포) Critical/Important 0(필드 swap 0), 서버 컴파일+플레이 무변화 검증. spec `2026-07-17-combat-hit-resolution-design`(Part 2 §), plan `2026-07-17-combat-hit-resolution-part2`. `[[combat-hit-resolution]]`
- ✅ **Tier-2/3 정리 (Item 2·3, 07-18)** — 넉백 Luban `Range/Angle` 잔재 제거(`__beans__.xlsx` KnockbackEffect bean + `#Ability.xlsx` 데이터 → gen.sh 재생성 → 클·서 생성 `KnockbackEffect.cs`가 `Strength/DurationTicks/DecayPerTick`만; 제거 필드는 C#·양 매퍼 미참조 죽은 바이트라 **동작 무변화**) · `CombatConfigProvider.Get()` fail-loud 가드(`Get(1)`의 애매한 `KeyNotFoundException` → `GetOrDefault(1)`+null 가드로 원인 짚는 `InvalidOperationException`). 클·서 editor 컴파일·MasterData 로드 클린 검증. 4레포 브랜치 `feature/combat-tier23-cleanup`.
  - **Item 1 (`ctx.Target` 항상 자기자신)은 그대로 유지** (사용자 결정, 07-18) — 제거 안 함. 향후 실 타게팅/on-hit 디버프는 넉백처럼 `HitContext` 라이더로 지을 축으로 남김.

---

## ⏸ 파킹 (Parked — 미룬 것 + 재개 조건)

| 항목 | 왜 미뤘나 | 재개 조건 |
|---|---|---|
| ~~**외력(넉백) 처리를 공통 엔티티 루프로 이전** (부채)~~ ✅ **정산(07-13)** — 통합 World Tick Sub-slice A에서 외력 resolve를 공유 `MovementSystem.Tick`(=`world.Tick` 이동 페이즈)로 흡수, 서버 `MoveCharacters` 임시분기 제거. 입력 없는 Simulated(서버 AI)도 resolve. `Simulated` 마커가 클라 원격 문제를 자연 해소(원격은 마킹 안 돼 클라가 안 틱). `[[velocity-motor-contribution-slice]]` | — |
| **Recon 엔티티-로드 러버밴딩** | 엔티티 많을 때 빈 곳 점프에도 recon 러버밴딩 관찰. 진단틀(A 서버틱밀림 vs B 클라FPS) 프로토타입 후 롤백 | 각 잡고 재개. `[[recon-entity-load-parked]]` |
| ~~**캐릭터끼리 충돌 wedge** (몹 뭉침)~~ ✅ **결론(07-16) — "단단한 벽" 확정** | 소프트 분리(BOTW식)를 시도했다 폐기: 클라 예측(현재 틱) vs 원격 보간(과거 틱) **타임라인 불일치** 때문에 클라가 분리를 밀면 덜덜(recon 폭발) / 안 밀면 관통 → config로 "안 겹침+안 덜덜" 동시 불가(predict-all이나 통과만 가능, 범위 밖). **클·서 동일 벽 모델**(sweep에 Character 포함 + 디펜 full)로 확정 → 겹침·덜덜·recon 다 해소, 8마리 군집 정상. wedge는 실전 비문제(비스듬 접근=곡면 슬라이드). 동작상 원래와 사실상 동일 + 전용 레이어·명시 디펜·측정지식 추가. spec `2026-07-16-character-soft-separation-design`, `[[kinematic-controller-migration]]` | — |
| **서버 뷰 NRE** (`LOPEntity.get_position`/`LOPEntityView.LateUpdate`) | 뷰 `LateUpdate`가 `worldTransform` 링크 전/해제 후 `position`을 읽는 수명 타이밍. 도메인 리로드(재시작) 시 발현. 이동 버그와 무관(07-15 확인) | 서버 뷰 수명 손댈 때 |
| **EventSystem 2개** (additive 씬 중복) | GamePlay + Room/베이스 씬이 각각 하나씩 → "only one active EventSystem" 경고. 키보드 폴링(디바이스 직접)엔 영향 없으나 UI 이벤트 위생 이슈 | 씬 구성 정리 시(하나만 남기기) |
| **입력 포커스** (에디터 Play Mode Input Behavior) | **게임 버그 아님** — Game 뷰 포커스 잃으면 Input System이 키를 0으로 봄(`kbNull=False`인데 전 키 false). brake-to-desired 모터가 입력 0→즉시 정지시켜 "낀 것처럼" 드러남(옛 관성 모터는 덮여 안 보였음). 빌드 무관, 2에디터 테스트 artifact | 테스트 편의 시 InputSettings에 `All Device Input Always Goes To Game View` 설정, 또는 Game 뷰 포커스 유지 |
| **네이티브 clock sync (방향 B)** | Mirror transport=메인스레드라 ping/pong 정확도 이득 미미; 순수측정=전용소켓+스레드 큰 작업 | **Mirror 제거가 실제 안건이 될 때.** `[[netcode-migration-status]]` §9.8 |
| **M5b — LOP.UI 인프라 GameFramework 승격** | 단일 클라라 YAGNI | 서버도 같은 UI 인프라가 필요해질 때. `[[uitoolkit-migration-status]]` |
| **넷코드 status 메모리류 `GameFramework.Netcode` 수렴** | 흩어진 클래스 일괄 이동은 YAGNI | 각 클래스 손댈 때 기회 있을 때. `[[netcode-namespace-consolidation]]` |
| **MasterData `file:` → git URL + tag 전환** | 안정화 후 결정 | 패키지 3종 함께 전환 시점. topology Open Decisions |
| ~~**게임 씬 스코프 분리** (GamePlay 씬)~~ ✅ **종결(07-17)** — 이미 구현돼 있었고(Root→Room→Game 스코프 + `EnqueueParent(roomScope)` + additive 로드: `LOPGameFactory`/`GameLifetimeScope`/`RoomLifetimeScope`), **문서 정합 완료**: spec 상단에 "구현 완료 + 실제와의 차이" 배너 추가(씬명 `GamePlay`→`LOPGame`, gameInfo=`runner.Run` 파라미터[Enqueue 아님], SceneManager 채택, 수명제어=`IGameFactory` 캡슐화, Runner/World 리네임) + CLAUDE.md 자동로드 `@` 줄 제거(구현됨). | — |

---

## 상태

이 파일은 **2026-07-09 생성**, Stage④ 프론티어 + 최근 넷코드/이동 워크스트림을 시드했다. 나머지 워크스트림 상태는 각 메모리에 남아 있으며, **손댈 때 기회 있을 때** 이리로 점진 이관한다(일괄 이관 안 함).
