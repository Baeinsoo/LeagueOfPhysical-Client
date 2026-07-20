# 전반 구조/구현 감사 — 2026-07-13

업계 표준(DOTS/Quantum/GAS/Source/Overwatch/MVVM/CommonUI/VContainer/CQRS) 대비 5영역 병렬 감사(World Core / 넷코드 / 어빌리티·전투 / UI / DI·메시징). **코드는 전반적으로 건강** — anemic 모델·System/커널 규율·DI 스코프·UI MVVM·sim/host 분리 모두 표준 충실. 아래는 유의미한 divergence만(소스 레벨 확인).

> 상태 원장은 `ROADMAP.md`의 "구조 정리 백로그". 이 문서는 그 **세부 백킹**(file:line·근거). 상태: ✅완료 / ⏳남음 / 📄문서 / ⚪비이슈(정당한 예외).

---

## ✅ 이 세션 완료 (#1·#2·#4·#5, 2026-07-13)

| # | 발견 | 위치 | 커밋 |
|---|---|---|---|
| **#1** | `DamageEffect.Amount` 죽은 데이터 — 데미지 하드코딩(10+STR×3), MasterData Amount 무시 | `LOPCombatSystem.cs:24,46,55` · `AbilityDataProvider.cs:53` | LOP-Shared `0a8e0d2` |
| **#2** | 넉백 핸들러가 World Core/`IOverlapQuery` 우회(Unity Physics+ctx.EntityManager) + 부채꼴 수학 중복 | `Server/KnockbackEffectHandler.cs:21-58` | Shared `f8474e5`+Server `836e83e` (`AttackSector` 추출) |
| **#4** | per-tick 위치 스냅이 reliable 채널 | `Server/LOPRunner.cs:315` | 서브셋 청킹 Server `459b550`+Client `d1f22bb` (`[[snapshot-mtu-chunking]]`) |
| **#5** | `generate_protos.sh`가 `MessageIds.cs` 파괴 → ID 재번호 wire desync | `Shared/Scripts/generate_protos.sh:7` | Shared `52ecb3c` |

부수: `AttackSector` 공유 헬퍼(Damage/Knockback 부채꼴 중복 제거) · **적 넉백 갭 발견**(아래) · game-scene-scope 이미 구현됨 확인(파킹 해제).

---

## ⏳ 남은 것 — Tier 2 (별도 슬라이스, 유의미)

### ✅ #6 (넷코드 HIGH, M) Reconciler 재생이 `LOPWorld.Mutation` 시스템 시퀀스를 수기 복제 — 완료 (07-13)
> **해소:** "통합 World Tick" 슬라이스(A/B/C + 모션 브릿지 공유화)로 `IWorld.Tick`을 단일 결정론 진입점화. 표준 정합(`Simulated` 마커로 클라 시뮬=예측 엔티티만) → `Reconciler` 재생이 `world.Tick` 하나 = 라이브==재생, 수기 시퀀스 소멸. spec `2026-07-13-unified-world-tick-client-sim-scope-design.md`. (아래는 착수 시점 서술.)

- **위치:** `Client/Reconciler.cs:140-148` (replay가 `movementSystem.Tick`→`abilitySystem.Tick`→`statusEffectSystem.Tick`→`abilityEffectExecutor.DriveActiveEntity`→`kinematicMoveSystem.Tick` 개별 호출) vs `Shared/LOPWorld.cs:22-36`(`Mutation`) + `LOPRunner.cs:102-105`.
- **위배:** `IWorld.Tick`이 *단일* 결정론 진입점이어야(connection-arch "코어 능력"). 지금 라이브 경로와 재생 경로가 **두 벌 수기 시퀀스** → 컴파일러 아닌 기억으로 lockstep. 메모리 `[[hard-rollback-input-tick-alignment]]`의 desync 실패 클래스.
- **수정:** per-entity-filtered `IWorld.Tick` 또는 공유 "TickEntity" 헬퍼를 양쪽이 호출.
- **문서 인지:** 부분(connection-arch "알려진 괴리"가 `DriveAbilityEffects`/물리 페이즈 흡수는 언급하나 Reconciler 중복은 미언급).

### #7 (DI HIGH, M) `WorldEventBatch` 단일 envelope 미구현 — 개념별 패킷이 와이어에
- **위치:** `Server/WorldEventSink.cs:29,50`(`new DamageEventToC`/`new AbilityActivatedToC` + `session.Send` 개별) · 클라 미러 `Client/WorldEventSink.cs`. `WorldEventBatch` 전체 소스 0건. proto `DamageEventToC.proto`/`AbilityActivatedToC.proto`.
- **위배:** connection-arch "와이어 추상" — 단일 폴리모픽 `WorldEventBatch`가 여러 `WorldEvent` 운반, "개념별 패킷 신설 안 함" 명시. 지금은 정반대(새 이벤트마다 패킷+MessageId 증식).
- **수정:** `WorldEventBatch` 도입, 레거시 `ToC`는 수신 어댑터에서 `WorldEvent`로 변환 격리.

### ✅ #8 static `EventBus.Default` 글로벌 버스 — 완료 (07-16, Cysharp MessagePipe 이전)
- **해소:** 전역 static 커스텀 버스를 **MessagePipe**(타입·keyed pub/sub + DI 스코프 브로커)로 이전 후 삭제(클·서·GameFramework). 문자열 토픽·리플렉션 디스패치·전역 static 제거. **①구독 IDisposable(AddTo) + Root 싱글턴 브로커**로 룸 재입장 leak 구조적 해소(②스코프 브로커=redundant 드롭). 네트워크 수신은 `NetworkMessageDispatcher`(리플렉션 없는 타입 라우팅, IL2CPP 안전), 엔티티별은 keyed(키=entityId). 정적/엔티티 컴포넌트=`GlobalMessagePipe`, DI 서비스/VM=주입. 5슬라이스, 종합 플레이 검증 통과, EditMode 269 green. spec/plan `2026-07-16-eventbus-messagepipe-migration*`. 리서치: 패턴=표준 / 전역-static 형태=비표준 / MessagePipe=R3 생태계 표준 답.

### ✅ #3-WC (Med, M) `ctx.EntityManager` 레거시 탈출구 제거 — 완료 (07-13)
- **위치(였음):** `Shared/Ability/AbilityEffectContext.cs`(`IEntityManager EntityManager`, 구 `GameFramework/Entity/IEntityManager` = UnityEngine 의존). 소비 `AbilityEffectExecutor.cs`.
- **재검증 결과:** 필드는 **완전한 죽은 pass-through** — 어떤 핸들러도 읽지 않음(Damage/Knockback/StatusEffectApply 전부 이미 `EntityRegistry`+`IOverlapQuery`로 이전 완료). executor가 파라미터로 받아 ctx에 복사만 하고 소비처 0.
- **수정:** 필드+ctor 파라미터 삭제, `DriveActiveEntity`의 `IEntityManager` 파라미터+2개 전파 복사 삭제, 호출부(Server/Client `LOPRunner`, Client `Reconciler` — 미사용 `Reconcile` 파라미터까지) + 테스트 5곳 정리. 순수 시그니처 변경(동작 무변경, Shared 111 EditMode green).

---

## ⏳ 남은 것 — Tier 3 (정리/일관성, 저심각)

- ✅ **적(AI) 넉백 미적용 — 완료 (07-13)** — `MotionContributionSystem.Resolve`/`Prune`이 `MovementSystem.Tick`(InputBuffer 게이트) 안에서만 실행 → AI(버퍼 없음) 스킵, 넉백 기여가 folding 안 됨. **수정:** 재사용 헬퍼 `MotionContributionSystem.ApplyToVelocity(entity, tick)`(현재 수평 velocity를 base로 외력 folding, y 보존, 프루닝) 신설 + 서버 `MoveCharacters`에서 입력 비조종(AI) 캐릭에 호출(KinematicMove 통합 전). **공유 `MovementSystem.Tick`은 안 건드림** — 클라 원격은 스냅 팔로워라 게이트 밖으로 빼면 스냅샷 권위와 충돌(그래서 서버 host 쪽에서 AI만 folding). 플레이어는 여전히 `MovementSystem.Tick`이 입력 base로 같은 `Resolve`를 태움. EditMode +4. **인게임 육안 확인됨(몬스터 넉백 적용).**
  - ⚠️ **아키텍처 부채(임시 배치):** 이상적 자리는 서버 분기가 아니라 **공통 루프 `LOPWorld.Tick`(Mutation)에서 플레이어·AI 일괄**. 지금 못 하는 이유 = 클라 공통 루프가 원격(스냅 팔로워) 엔티티까지 훑어서 게이트 밖으로 빼면 스냅샷 권위 충돌 → "클라 sim 대상 = 내 캐릭만" 정리(Stage④ 4e 흡수)가 선행. 로직은 `ApplyToVelocity` 공통 함수로 이미 추출 → 이전 비용 최소. ROADMAP 파킹 "외력 처리 공통 루프 이전" + connection-arch 4e 노트 참고.
- **#5-AC (Low→High-if-unnoticed, M) `ctx.Target` 항상 자기자신** — 실 타게팅 없음. `AbilityActivator.cs:37`/`Reconciler.cs:137`/`EnemyBrain.cs:47` 전부 target==caster. `StatusEffectApplyEffectHandler`가 `ctx.Target`에 적용 → 미래 비-자기 status(디버프/힐)가 조용히 시전자에 적용될 함정. GAS `TargetData` 대응 없음.
- **#4-AC (Low-Med, S) 크리/회피 상수 하드코딩** — `LOPCombatSystem.cs:99`(dodge clamp 0.05~0.95), `:107`(crit 0.05~0.50), `:71`(crit mult 1.25~1.75). MasterData(`TbCombatConfig` 등)로 승격 여지.
- ✅ **#4-NC 링버퍼 3벌 중복 — 완료 (07-16)** — 동일 `tick%capacity` 슬롯팅 + 병렬 tick 배열 stale 판별을 `GameFramework.Netcode.SequenceBuffer<T>` 하나로 추출(Fiedler "sequence buffer" 표준명 — `RingBuffer`=FIFO큐라 부정확). 순수 별칭 `InputHistory`/`PredictedAbilityStateHistory` 삭제(호출처가 `SequenceBuffer<InputCommand>`/`<PredictedAbilityState>` 직접), `SnapshotHistory`는 `Latest`/`Count`/tick-내장 `Record` 편의로 얇은 어댑터 유지. GameFramework EditMode +10(269 green). feature 브랜치 `sequence-buffer-extract`(GameFramework/LOP-Shared/Client 3레포).
- ✅ **#6-NC 죽은 레거시 `Status` 매틱 제거 — 완료 (07-13)** — 구체 `Status` 서브클래스 0 재확인 → `Component/Status.cs` 삭제 + `LOPEntity.UpdateStatuses` 제거(`UpdateEntity`는 `MonoEntity` abstract 계약이라 빈 override 잔류). 클·서 클린 컴파일. Client `cleanup/dead-status-matic`.
- ✅ **#5-DM `MessageHandler<T>` 죽은 코드 제거 — 완료 (07-13)** — 4레포 전수 사용처 0 재확인 → `Shared/Network/Message/MessageHandler.cs` 삭제. 실 라우팅은 `MessageFactory`+`LOPRoom.cs:80`+`EventBus`. Shared `cleanup/dead-message-handler`.
- **#6-DM (Med-Low, S) `LoginService` MonoSingleton+`[DIMonoBehaviour]` 혼종** — `Client/Login/LoginService.cs:13`. static accessor + `[Inject]` 동시 → 수명 모호.
- **#1-UI (Med, M) `MatchMakingViewModel`이 코디네이터 대신 직접 네비게이션** — `Client/UI/MatchMaking/MatchMakingViewModel.cs:42-65`(`_windowManager.Open`/`Close` + child View API 직접). guidelines "큰 흐름=코디네이터" 위배. 옳은 예: `PlayerHudCoordinator.cs`/`LOPGamePresenter.cs`.
- **#3-UI (Low, S) `PercentBar` 위젯 미추출** — `Client/UI/CharacterHud/CharacterHudView.cs:58-63`(`SetBar`)와 `WorldSpace/CharacterNameplate.cs:101-110`(`UpdateHpBar`) 동일 `Length.Percent` 채우기 중복. 문서가 `HealthBar:VisualElement`를 정본 예로 지목.
- **#3-WC (Low/Med, M) `PredictedAbilityState.RestoreTo` 직접 필드 변경** — `Shared/PredictedAbilityState.cs:50-89`(abilities/stats/status/mana 직접). Health/Mana/Level는 `*System.ApplyAuthoritativeState` 경유인데 이것만 System 우회. **문서 인지**(plan `2026-07-05-stage4-ability-replay:17` 의도적 tradeoff).
- **#8-DM (Low, M, gray) `LOPEntity.RaisePropertyChanged`가 setter에서 push** — `Client/LOPEntity.cs:56-59`(Transform/Velocity setter→`EventBus.Publish`). 연속 상태는 pull이 원칙. 단 `LOPEntity`는 outer MonoEntity(코어 아님)라 inner/outer 경계 위배 아님(코어엔 `EventBus.Publish` 0건 확인). 스타일 divergence.

---

## 📄 문서 stale 정합 (저비용·고레버리지 — 자동 로드라 능동적 오해)

> ✅ **아래 3건 정합 완료 (2026-07-13, `docs/audit-stale-reconcile` 브랜치).** 정합 전 재검증에서 아래 원래 findings 중 **§2.2 항목은 부분 반박**됨(정정 반영).

- ✅ **`entity-system-design.md` 전면 stale** — 재작성. 코드 위치(GameFramework.World + LOP-Shared, 이 repo 아님) 헤더 추가; `IEntityComponent`→`abstract class Component`; `EntityStatType`→실제 6종(Strength/Dexterity/Intelligence/Vitality/MoveSpeed/JumpPower); `EntityStatModifier`+`ModifierSource`→`StatModifier`+`ModifierType`(Flat/PercentAdd/PercentMult); 실제 컴포넌트 인벤토리(Health/Mana/Level/Stats/Ownership/Transform/Velocity + LOP StatusEffects/Abilities/InputBuffer/…)와 System(Health/Mana/Level/Stats + LOP Combat/Movement/Kinematic/Ability/StatusEffect); `Combat`/`Dialogue`/`Interactable`/`EntityFactory` 미존재 명시.
- ✅ **`netcode-redesign.md` §2.2 stale** — 정정. ⚠️ **원 finding 부분 반박:** "capture+buffer+send만(예측 없음)"은 틀림 — `PlayerInputManager.ProcessInput`은 여전히 로컬 예측을 **트리거**한다(이동=`SetCurrent`→공유 `MovementSystem`, 어빌리티=`abilityActivator.TryActivate`). 따라서 §4d `IInputSource` provider는 *아직 미해소*(Stage④). 실제 오류는 서버 버퍼 컴포넌트 실명(`InputBuffer`, `InputBufferComponent`/`EntityInputComponent` 아님). §2 배너에 input-as-데이터 축 추가 + 구체 참조 교정.
- ✅ **`world-core-connection-architecture.md` "알려진 괴리 #2"(despawn cascade)** — 해소 반영. `DeathCascadeSystem.Resolve`(resolve 단계, `LOPRunner.ProcessDeaths`가 egress보다 먼저), `LOPGame.HandleDeath`는 **삭제됨**(클래스 자체 없음). death-wire를 `DamageDealtEvent.IsDead`(존재하지 않는 필드)→실제 `EntityDespawnToC`(+HP 스냅샷)로 교정. 4단계 표 + 백로그 #2 둘 다 갱신.
- **`#3-NC (Low)` `IWorld` DI 인터페이스 seam** — `Server/GameLifetimeScope.cs:30`(`Register<IWorld, LOPWorld>`)이 형제 `*System`(concrete 등록)과 불일치. connection-arch "시뮬=Register<Concrete>, I/O=Register<IFoo>" 컨벤션 회색지대(문서 미해소).
- **`#2-NC`·`#7-NC` (Low) 넷코드 네임스페이스 분산** — `ClockDilator`/`InputTimingTracker`/`LeadController`/`INetworkTime`은 `GameFramework`, 나머지는 `GameFramework.Netcode`. 메모리 `[[netcode-namespace-consolidation]]` 인지(YAGNI).

---

## ⚪ 비이슈 (확인·refute — 안심용)

- World Core `EntityRegistry`/`Entity`/`Component` = 표준 ECS/CBD 정합. 결정론 seam(`LOPCombatSystem`/`KinematicMover`/`MovementSystem`=공유 concrete, I/O만 인터페이스) 정확. static은 순수 커널만.
- DI: Root→Room→Game 스코프 계층 정확(parent/child 가시성, `LOPGameFactory` 양쪽 `EnqueueParent`), World Core는 Game 스코프만, 크로스스코프 중복 0. 메시지 핸들러 `RegisterEntryPoint` 생명주기. 클라 R3 구독 100% `CompositeDisposable`.
- UI: `VisualElement` 상속 View 0, View에 비즈니스 로직 0, dialog service(`OpenModalAsync`+`IResultView`) 정합, world-space UI 분리. DebugHud 폴링=문서 허용 예외(라이브 상태 없음).
- 어빌리티 페이즈머신(Startup/Active/Recovery, `AbilityEffectExecutor.DriveActiveEntity` 정확 틱 enter)은 멀티틱 catch-up에도 스킵 안 됨(refute됨). `MotionEffect`가 executor 우회(static query)는 문서상 의도적 tradeoff(velocity 단일 writer).
- 청킹 스냅샷 초기상태 안전(`EntitySpawnToC` reliable 유지).

---

## 세션 진행 순서 참고 (2026-07-13)

완료: #1 → #2(+AttackSector) → #4(통짜 flip 실패→서브셋 청킹 재구현) → #5. 남은 것 착수 추천 순: (a) 문서 stale 정합(저비용·auto-load 오해 제거), (b) #3-WC `ctx.EntityManager` 제거(#2로 열림) + 적 넉백, (c) Tier-2 큰 것(#6 Reconciler / #7 WorldEventBatch / #8 EventBus)은 각각 brainstorm→plan.
