# World Core 뷰 이행 — Slice A: 뷰가 World.Entity에서 pull (Model A 소비 템플릿)

**Date:** 2026-06-16
**Branch:** `feature/world-core-view-slice-a`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [MVC 디커플 spec](2026-06-14-entity-view-mvc-decouple-design.md)

## Goal

월드 프레젠테이션을 **Model A**(순수 C# 코어 + 바깥 어댑터/뷰, id 링크)로 옮기는 inside-out 이행의 **첫 칸**. 가장 작은 수직 슬라이스로 **"뷰가 `World.Entity` 참조를 보유하고 이산 데이터를 순수 C# World 컴포넌트에서 pull"** 하는 패턴을 박제한다. 구체 대상은 `CharacterNameplate`가 HP 초기값을 레거시 `HealthComponent`(MonoBehaviour) 대신 **`World.Health`** 에서 읽는 것.

동작 변화 없음(두 곳의 값이 동일) — 이 슬라이스의 가치는 *의존 방향을 레거시→코어로 돌린 템플릿*이고, 이후 컴포넌트/뷰가 이를 복제한다.

## 잠긴 결정 (durable — 이번 대화에서 확정, 문서 빈칸 보완)

- **Model A 확정**: 코어 엔티티 컴포넌트는 **순수 C# 데이터**, 엔진 객체(Rigidbody/Mesh/GameObject)는 **바깥 어댑터/뷰**가 보유하고 entityId로 링크. 하이브리드 컴포넌트(엔티티 컴포넌트가 Unity 컴포넌트/엔진 참조 보유, 모델 B)는 **기각**.
  - 근거: 문서 방향(엔진비의존 코어·클서 공유 시뮬·예측 지향) + OSS 교차검증 — Quantum=엄격 순수(FPVector3), Svelto(implementor)/DOTS(managed component)/Entitas(ViewComponent)는 엔진 참조를 *경계 브릿지로만* 허용. (deep-research 1차 소스 확인)
- **물리/Motion의 DIP**(`IPhysicsSimulator` 주입 → `Tick`에서 호출 → Rigidbody↔Motion write-back)는 *방향만* 합의. 코어가 페이즈를 트리거하고 **실제 sync는 어댑터**가 수행(코어는 Rigidbody 비참조). 결정론은 주지 않음(PhysX 비결정 → Snapshot/Restore는 클라 외각, Stage ④). → **Slice B 설계 대상, 이번 범위 아님.**
- **신규 추상 인터페이스 도입 안 함(YAGNI)**: 현재 월드 프레젠테이션은 추상 인터페이스가 거의 없는 구체 구현이며(연속 pull=직접 읽기라 추상 불필요, 이산 fan-out은 구체 `WorldEventBridge`+`EventBus`), 이번 슬라이스에서 새 인터페이스를 만들지 않는다. (`IEventSink` 등 I/O 어댑터 추상은 문서상 Slice 4 영역, 미구현.)

## Scope (In)

`Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs`:
- `[Inject] private GameFramework.World.EntityRegistry entityRegistry;` 추가 (World 타입은 풀 네임스페이스 한정 — 프로젝트 컨벤션).
- `Start`에서 `entityRegistry.Get(entity.entityId)`로 `World.Entity`를 1회 해석·캐시(뷰가 코어 참조 보유 = Model A).
- 초기 `maxHP`/`currentHP`를 `world.Get<GameFramework.World.Health>().Max/Current`에서 읽음 — **레거시 `entity.GetEntityComponent<HealthComponent>()` 읽기 제거.**
- 실시간 갱신(`EntityDamage` 구독 → `remainingHP`)은 그대로(이미 World 발 fan-out).
- null 가드: 미등록 시(`world == null` 또는 `Health` 부재) 기존 폴백(`_maxHp = 1`)과 동등하게 안전 처리.

## Out of scope (defer — 이유 명시)

- **`DamageFloaterEmitter`**: 당장 pull할 이산 World 데이터가 없음(순수 `EntityDamage` 이벤트 기반, 위치는 연속상태=Slice B). World.Entity 참조 도입은 *쓸 데가 생길 때*(Slice B Motion) — 지금 넣으면 YAGNI.
- **스포너를 `EntityRegistry` 수명 구독으로(view-resolver 완전체)**: 뷰 GameObject 호스트가 아직 레거시 `LOPEntity`에 종속 + 생성 타이밍(`registry.Add`가 `LOPEntityManager.entityMap` 세팅 전에 발생)이라 시기상조. GameObject 호스트가 코어 수명으로 넘어갈 때 별도 슬라이스. **이번엔 기존 `EntityCreated` 스폰 경로 유지.**
- **레거시 `HealthComponent` 제거**: 아직 snapshot 핸들러(`Game.Entity.MessageHandler`의 `UserEntitySnapToC`가 write)와 `CharacterHudViewModel`(read)이 사용. `World.Health`가 snapshot 경로까지 진실원본이 되면 별도 정리.
- `LOPEntityView`/`LOPEntityController`(연속상태 결합), Motion/position·velocity/물리(Slice B), reconciler/netcode(Slice C), `World.Appearance` 등 그 외 컴포넌트.

## 조사 사실 (구현 근거)

- **World 등록은 캐릭터만**: `CharacterCreator`만 `EntityRegistry.Add`(Health 보유 World.Entity). `ItemCreator`는 World 미등록 → `registry.Get(entity.entityId)`는 캐릭터(=Nameplate 대상)에 대해 유효.
- **`World.Health`는 이미 존재·등록**: `CharacterCreator`가 `new Health(maxHP){ Current = currentHP }`로 등록. `Health.Max`/`Health.Current` 노출.
- **타이밍 안전**: Nameplate는 `EntityViewSpawner`가 `EntityCreated`(= `CreateEntity` 반환 후, 따라서 `registry.Add` 이후)에 생성 → `Start` 시점에 World.Entity 등록 완료.
- **`HealthComponent` 소비자 4곳**: `CharacterCreator`(생성), `CharacterHudViewModel`(read), `CharacterNameplate`(read → 이번에 World로), `Game.Entity.MessageHandler`(snapshot write). → 제거 불가, 이번엔 Nameplate read만 이전.

## 검증

- **컴파일**: 클라 0에러 (UnityMCP `refresh_unity` + `read_console`, 클라 인스턴스 핀).
- **런타임 플레이(수동)**: 네임플레이트 HP바 초기값 정상 표시 + 피격 시 감소 정상(World.Health 초기값 + EntityDamage 갱신), 스폰/디스폰 정상, 콘솔 에러 0. (동작은 이전과 동일해야 함 — 회귀 없음 확인이 곧 성공.)
- 자동화 테스트 신규 없음(단일 Assembly-CSharp, 기존 슬라이스 기조). 핵심은 컴파일 + 플레이.

## 진행

- [x] 방향(inside-out)·모델(A)·범위 합의 + OSS 교차검증(deep-research)
- [ ] 이 spec 사용자 리뷰
- [ ] `writing-plans`로 구현 plan
- [ ] 구현 → 검증 → 머지

> 후속: Slice B(Motion/물리 DIP — `IPhysicsSimulator`, Rigidbody↔Motion write-back, `noEngineReferences`용 수학 타입), Slice C(reconciler/netcode), 그리고 스포너 view-resolver 완전체 + 레거시 컴포넌트 제거. 각자 별도 spec.
