# 죽음 cascade를 egress에서 resolve 단계로 이전 (cleanup backlog #2 — 위치 교정)

**Date:** 2026-06-22
**Branch:** `feature/death-cascade-to-generation`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (선택적 deferral / 4단계 배리어 / backlog #2)

## Goal

죽음의 결과 처리(despawn + 경험치 구슬 생성)를 **egress(클라 송출) 경로에서 빼서 resolve 단계(송출 전)로 옮긴다.** `WorldEventReactor` → `EventBus` → `LOPGame.HandleDeath`로 도는 indirection을 제거하고, 죽음 cascade를 전용 서버 시스템이 resolve 단계에서 직접 처리하게 한다.

`world-core-connection-architecture.md`의 cleanup backlog **#2 (despawn이 fan-out에)** 를 *위치 교정* 수준으로 해소한다. (풀 이벤트화 — `DespawnEvent` emit + Application 적용 — 는 deferred 큐 인프라가 필요해 **Stage④로 미룸**.)

## 배경 — 무엇이 잘못됐나

현재 서버 한 틱:

```
ProcessInput → UpdateEntity(전투: HP mutate + DeathEvent 버퍼에 append)
  → UpdateAI → SimulatePhysics
  → ProcessEvent: Emit(연출 송출) → React(죽음 cascade) → Clear   ← ★문제
  → EndUpdate(스냅샷 송출 + DestroyMarkedEntities)
```

`ProcessEvent`는 **클라로 내보내는(egress) 단계**인데, 그 안의 `reactor.React`가 `DeathEvent`를 `EventBus`로 다시 쏘고 → `LOPGame.HandleDeath`가 despawn + 경험치 구슬을 **생성**한다. "내보낸 뒤 새 사실을 만든다"는 안티패턴 (egress가 상태 변경).

또한 `버퍼 → EventBus publish → 구독` 왕복은 순수 indirection이다. `DeathEvent`는 클라로 안 나간다(클라는 `DamageDealtEvent.IsDead`로 죽음을 안다). EventBus 왕복은 같은 서버 안에서 한 바퀴 더 도는 낭비.

> 이 슬라이스는 *위치/순서 교정*만 한다. 죽음 판정·despawn·exp는 이미 직접 mutate(서버 in-place)라, 풀 deferred 큐(`ModifyHealthQueue`급)·`DespawnEvent` 이벤트화는 **콘텐츠(버프·연계·광역)가 생길 때** — 지금은 YAGNI. 산업 정렬: OW는 결과 side-effect를 *시뮬(UpdateFixed) 안*에서 처리하고 그 *뒤*에 송출, Source는 `SV_Physics`/`Think`(죽음·think)를 `SV_SendClientMessages`(송출) *전*에 둔다 — death 결과는 송출 전 resolve 단계가 표준.

## 설계 결정 (브레인스토밍 합의)

| 축 | 결정 |
|---|---|
| cascade 코드 위치 | **(b) 전용 resolve-단계 시스템** — `WorldEventReactor`를 `DeathCascadeSystem`으로 repurpose(rename) |
| 트리거 | `LOPGameEngine`에 **`ProcessDeaths()` 스텝** 신설 — `SimulatePhysics` 뒤·`ProcessEvent` 앞. 버퍼의 `DeathEvent`를 읽어 시스템 호출 |
| EventBus/reactor 왕복 | **제거** — `LOPGame.HandleDeath` 및 `DeathEvent` 구독 삭제, `ProcessEvent`의 `reactor.React` 삭제 |
| 풀 이벤트화(`DespawnEvent`+Application) | **Stage④로 미룸** (deferred 큐 인프라 필요) |
| 클라 | **무변경** — 클라엔 죽음 cascade 없음. despawn은 이미 스냅샷 파생(엔티티 사라짐) |
| `HandleItemTouch` | **무변경** (별 경로 — 충돌 트리거). `DespawnEntity`는 ItemTouch용으로 LOPGame에 유지 |

## 변경 사항 (서버만 — LeagueOfPhysical-Server)

### 1. `DeathCascadeSystem` 신규 (git mv `WorldEventReactor.cs` → `DeathCascadeSystem.cs`)

`Assets/Scripts/World/DeathCascadeSystem.cs`. 순수 C# 서버 시스템(생성자 주입).

- **deps:** `LOPEntityManager entityManager`, `ISessionManager sessionManager`, `IEntityCreationDataFactory entityCreationDataFactory`
- **메서드:** `Resolve(IReadOnlyList<GameFramework.World.WorldEvent> events)` — 이벤트를 순회, `DeathEvent`마다:
  1. `entityManager.GetEntity<LOPEntity>(death.victimId)` → null이면 경고+skip
  2. `Vector3 position = victim.position` (despawn 전 캡처)
  3. `entityManager.DeleteEntityById(death.victimId)` (despawn = 파괴 마크, 실제 파괴는 EndUpdate)
  4. exp 마블 스폰 — `ItemCreationData`(`GenerateEntityId`, visualId=`"Assets/Art/Items/ExpMarble/ExpMarble.prefab"`, itemCode=`"exp_marble"`, position) → `CreateEntity<LOPEntity, ItemCreationData>` → `EntitySpawnToC{ EntityCreationData = entityCreationDataFactory.Create(entity) }` → 모든 세션에 `Send`

> 로직은 기존 `LOPGame.HandleDeath` + `SpawnExpMarble` + `DespawnEntity(victim)`를 그대로 옮긴 것. 동작 동일.

### 2. `LOPGameEngine.cs`

- `[Inject] private WorldEventReactor reactor;` → `[Inject] private DeathCascadeSystem deathCascade;`
- `UpdateEngine()`의 `SimulatePhysics();` 와 `ProcessEvent();` 사이에 `ProcessDeaths();` 추가
- 신규 메서드:
  ```csharp
  private void ProcessDeaths()
  {
      var snapshot = worldEventBuffer.Snapshot;
      if (snapshot.Count == 0) return;
      deathCascade.Resolve(snapshot);
  }
  ```
- `ProcessEvent()`에서 `reactor.React(snapshot);` **삭제** → `Emit` + `Clear`만

### 3. `GameLifetimeScope.cs`

- `builder.Register<WorldEventReactor>(Lifetime.Singleton);` → `builder.Register<DeathCascadeSystem>(Lifetime.Singleton);`
- `builder.RegisterComponent(entityManager).As<IEntityManager>();` → `...As<IEntityManager>().AsSelf();` (concrete `LOPEntityManager` 주입 가능하게)

### 4. `LOPGame.cs`

- `InitializeAsync`의 `EventBus.Default.Subscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath);` **삭제**
- `DeinitializeAsync`의 대응 `Unsubscribe` **삭제**
- `HandleDeath` 메서드 **삭제**
- `SpawnExpMarble` 메서드 **삭제** (DeathCascadeSystem으로 이전)
- `DespawnEntity`, `HandleItemTouch` + `ItemTouch` 구독 **유지**

### 5. (선택) `WorldEventSink.cs` (서버)

- `Emit`의 `case DeathEvent de: Debug.Log(...)` **삭제** — 죽음은 이제 `ProcessDeaths`가 소비, egress의 Debug.Log는 잉여. (`DamageDealtEvent` case는 유지.)

## 일부러 안 바꾸는 것

- **클라 전체** — 죽음 cascade 없음. despawn은 이미 스냅샷에서 엔티티가 사라지는 걸로 처리. `DamageDealtEvent.IsDead`(사망 연출)도 그대로.
- **`HandleItemTouch`** (아이템 줍기 → 경험치) — 충돌 트리거 별 경로. 이번 범위 밖.
- **풀 이벤트화** — `DespawnEvent`/`ExpSpawnEvent` + Application 적용은 Stage④.

## 검증 (플레이)

리팩터(위치 이동)라 새 단위 테스트 없음. 서버·클라 플레이로 **이전과 동일** 확인:

1. 엔티티가 죽으면 **사라지고**(despawn) 그 자리에 **경험치 구슬**이 생긴다
2. 죽을 때 **사망 연출**이 난다(`DamageDealtEvent.IsDead` 경로 — 무변경)
3. 경험치 구슬을 **주우면 경험치**가 오른다(`HandleItemTouch` — 무변경)
4. 데미지 숫자·HP바 정상

성공 기준 = 동작 동일(순서/구조만 바뀜). 서버 컴파일 0 에러.

## Out of Scope

- 풀 deferred 이벤트화(`DespawnEvent` emit → Application 적용) — Stage④, deferred 큐 인프라와 함께.
- `HandleItemTouch`(아이템 cascade) 동일 패턴 이전 — 별 슬라이스(필요 시).
- 클라 변경.

## 산업 표준 매핑

- **죽음 결과는 송출(egress) 전 resolve 단계에서** — Overwatch는 결과 side-effect(`ModifyHealthQueue` 등)를 시뮬(`UpdateFixed`) 안에서 처리하고 그 *뒤* 델타 송출. Source는 `SV_Physics`/`CBaseEntity::Think`(죽음·게임룰)를 `SV_SendClientMessages`(송출) *전*에 둔다.
- **EventBus 왕복 제거** — resolve 단계에서 버퍼를 *직접 읽어* 처리(같은 단계 producer→consumer)는 OW/Source 정상 패턴. 문제였던 건 egress 뒤에서 한 바퀴 더 도는 것.

## Progress

- [x] 브레인스토밍 합의 ((b) 전용 시스템, `ProcessDeaths`, EventBus 제거, 풀 이벤트화는 Stage④)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
