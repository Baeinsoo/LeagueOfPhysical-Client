# 서버 player↔entity 맵 dedup — 중복 역방향 맵 제거 (Ownership back, 스코프 보존)

**Date:** 2026-06-20
**Branch (docs):** `feature/owner-map-dedup` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [Owner 이행](2026-06-19-world-core-owner-migration-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Game 씬 스코프](2026-06-06-game-scene-scope-design.md)

## Goal

서버 `LOPEntityManager`의 player↔entity 양방향 딕셔너리 중 **중복 역방향 맵(`entityUserMap` entityId→userId)을 제거**하고, entity→owner 조회를 이미 존재하는 `GameFramework.World.Ownership.OwnerId`에서 파생한다. **정방향 맵(`userEntityMap` userId→entityId)은 게임 스코프 인덱스로 유지**한다.

동작 보존: 입력 라우팅·유저별 스냅샷·엔티티→유저 역추적·사망 디스폰 모두 이전과 동일.

## 스코프 제약이 패턴을 결정한다 (핵심 — 이번 재설계의 이유)

처음엔 "업계 표준(Unreal `PlayerController.Pawn` / Unity Netcode `CommandTarget` / Mirror `NetworkConnection.identity`)"을 따라 **세션이 조종 엔티티를 forward 참조**(`LOPSession.controlledEntityId`)하고 맵을 둘 다 제거하는 안(이하 **옵션 B**)을 설계했다. 그러나 LOP의 **스코프 경계**가 그 패턴을 부적합하게 만든다:

- **세션 = Room 스코프** — `SessionManager`/`LOPSession`은 `RoomLifetimeScope`에 살고 **게임 teardown(GamePlay 씬 언로드)에도 생존**한다(재접속·리매치 위해). disconnect에도 `userId` 키로 유지(`LOPRoom.OnPlayerDisconnect`는 `networkConnection=null`만).
- **엔티티 = Game 스코프** — `LOPEntityManager`/`EntityRegistry`/엔티티는 `GameLifetimeScope`에 살고 **게임마다 파괴**된다.

세션(넓은 수명)이 엔티티(좁은 수명)를 forward 참조하면 **수명 역전**이 된다 — 게임이 끝나 엔티티가 사라져도 세션의 `controlledEntityId`는 남아 **stale 참조**가 된다. despawn 시 클리어해도, 게임 통째 teardown(개별 despawn 없이 씬 언로드)에서는 클리어 훅이 안 돌아 다음 게임에 stale이 새어 든다. 즉 옵션 B는 **teardown 규율**을 추가로 요구하는데 LOP엔 그 훅이 없다.

업계 샘플이 session-holds-entity를 쓰는 건 사실이지만, 그들은 **연결과 엔티티의 수명을 한 스코프에서 teardown 규율과 함께** 관리한다(Unreal: PlayerController가 Pawn을 possess/unpossess, 연결 종료 시 정리). LOP는 연결(Room)과 엔티티(Game)를 **의도적으로 다른 스코프로 분리**(game-scene-scope spec)했으므로, 그 경계를 넘는 forward 참조는 분리의 이점을 깨뜨린다.

→ **결정: 스코프를 존중한다(옵션 A).** 정방향 인덱스(`userEntityMap`)를 **Game 스코프인 `LOPEntityManager`에 그대로 두고**(게임과 함께 죽음 → stale 없음), 군더더기인 역방향 맵만 제거한다. 역방향 조회는 **Game 스코프인 `Ownership`**(엔티티가 보유 → 엔티티와 함께 죽음)에서 파생한다. 두 진실원본 모두 Game 스코프라 수명이 일치하고 정리 부담이 없다.

## 현재 구조 (조사 확인)

- `LOPEntityManager`(`[DIMonoBehaviour]`, `sessionManager`+`entityRegistry` 주입됨): `userEntityMap`(userId→entityId)·`entityUserMap`(entityId→userId), 둘 다 `Dictionary<string,string>`. populate = `CreateEntity`(`CharacterCreationData` && userId 비어있지 않을 때), clear = `DestroyMarkedEntities`(`entityRegistry.Remove` 후).
- 모든 접근은 두 메서드로 funnel: `GetEntityByUserId<T>(userId)`(userEntityMap), `GetUserIdByEntityId(entityId)`(entityUserMap). 맵 필드 직접 read 없음.
- **`entityUserMap`은 군더더기** — entity→owner는 `Ownership.OwnerId`(= creationData.userId, 플레이어 한정 — populate gate와 동일 집합)에 이미 단일 소스로 존재. 별도 역방향 딕셔너리는 그 중복.
- **호출처**: `GetUserIdByEntityId` = `LOPGameEngine.ProcessInput`(entityId만 보유, InputSequenceToC 역송신). `GetEntityByUserId` = `LOPGameEngine.EndUpdate`·`Game.Input/Entity/Info.MessageHandler`(모두 userId 보유, 세션 컨텍스트).
- `Ownership`(GameFramework): `OwnerId`(get, ctor). 플레이어 엔티티만 보유(CharacterCreator `isPlayer` gate).

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 패턴 | **정방향 맵 유지 + 역방향 맵 제거**(Ownership 파생) — 옵션 B(세션 forward 참조) 기각 | 스코프 보존: 세션(Room)이 엔티티(Game)를 알면 수명 역전·stale. 정방향 인덱스와 Ownership 모두 Game 스코프 → 일치 |
| ② | 정방향 | `userEntityMap`(userId→entityId) `LOPEntityManager`에 유지 | Game 스코프, 게임과 함께 죽음. O(1) userId 조회. 별도 forward 소스 불필요 |
| ③ | 역방향 | `Ownership.OwnerId`(기존) | entity→owner 단일 소스. 중복 `entityUserMap` 제거 |
| ④ | 접근자 facade | `GetEntityByUserId`/`GetUserIdByEntityId` 시그니처 유지 | 호출처 변경 0. `GetEntityByUserId`는 본문도 불변(userEntityMap), `GetUserIdByEntityId`만 Ownership 파생으로 재구현 |
| ⑤ | clear | `DestroyMarkedEntities`에서 `registry.Remove` **전에** Ownership으로 ownerId 캡처 → `userEntityMap.Remove(ownerId)` | Ownership은 registry.Remove 후 사라지므로 사전 캡처 |
| ⑥ | 범위 | **서버 전용, 1파일.** GameFramework/클라/`LOPSession`/`LOPGame`/`LOPGameEngine` 무변경 | 변경이 `LOPEntityManager`에 갇힘. `LOPSession`은 main 그대로 |

## Scope (In) — 서버 전용

### `Assets/Scripts/Entity/LOPEntityManager.cs` (유일 변경)

1. **`entityUserMap` 필드 제거** (`userEntityMap`은 유지).
2. **`CreateEntity`**: `entityUserMap[entity.entityId] = userId;` 줄 제거 (`userEntityMap[userId] = entity.entityId;`는 유지).
3. **`DestroyMarkedEntities`**: `GetEntity<LOPEntity>(entityId)` 다음에 `registry.Remove` 전 캡처 추가 —
   ```csharp
   string ownerId = GetUserIdByEntityId(entityId);   // capture before registry.Remove (reads Ownership)
   ```
   맵 clear 블록을 교체 —
   ```csharp
   if (ownerId != null)
   {
       userEntityMap.Remove(ownerId);
   }
   ```
4. **`GetUserIdByEntityId` 재구현** (Ownership 파생):
   ```csharp
   public string GetUserIdByEntityId(string entityId)
   {
       return entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
   }
   ```
5. **`GetEntityByUserId` 무변경**: `string entityId = userEntityMap[userId]; return GetEntity<TEntity>(entityId);`

> **`GetUserIdByEntityId` 계약 변경 — 가드 추가 안 함 (의도적 fail-loud)**: 본문이 `entityUserMap[entityId]`(miss 시 `KeyNotFoundException`) → `entityRegistry.Get(...)?.Get<Ownership>()?.OwnerId`(miss 시 null)로 바뀐다. 유일 호출처 `LOPGameEngine.ProcessInput`은 "입력 엔티티 = 항상 플레이어 = 항상 Ownership 보유"라는 **불변식**이 성립하는 자리이며, 그 불변식이 깨지면 **시끄럽게 터지도록** 일부러 예외 처리를 두지 않는다(원 설계 의도). null이 내려가면 `GetSessionByUserId(null)`/`session.Send`에서 여전히 loud하게 실패 → fail-loud 보존. 따라서 `ProcessInput`에 null 가드(`continue`)를 **추가하지 않는다** — 가드는 불변식 위반을 조용히 삼켜 의도와 정반대.

> `LOPSession`·호출처(`GetEntityByUserId` 4곳 + `GetUserIdByEntityId` 1곳)·`LOPGame`·`LOPGameEngine`·`ISession`·클라·GameFramework 무변경.

## 데이터 흐름 (전환 후)

```
스폰(플레이어): CreateEntity → userEntityMap[userId] = entityId   (Game 스코프 인덱스)
                            + (CharacterCreator) worldEntity.Add(Ownership(userId))  (Game 스코프)
forward(owner→entity): GetEntityByUserId(userId) = userEntityMap[userId] → GetEntity   (불변)
back(entity→owner):    GetUserIdByEntityId(entityId) = entityRegistry.Get(entityId)?.Get<Ownership>()?.OwnerId
despawn: DestroyMarkedEntities → ownerId(Ownership) 캡처 → registry.Remove → userEntityMap.Remove(ownerId)
teardown(게임 종료): GameLifetimeScope dispose → LOPEntityManager(userEntityMap 포함)·EntityRegistry·엔티티 통째 소멸 → stale 0
```

`entityUserMap` 제거. `userEntityMap`(정방향)·`Ownership`(역방향) 둘 다 Game 스코프 — 수명 일치, 세션(Room)은 엔티티를 모름.

## 검증

- **컴파일**: 서버 0에러(UnityMCP `refresh_unity`+`read_console`, 서버 인스턴스 핀). GameFramework/클라 무변경.
- **런타임(수동, 서버 플레이)**:
  1. **입력 라우팅**: 이동/액션 정상(GetEntityByUserId via userEntityMap), 클라가 InputSequenceToC 수신(GetUserIdByEntityId via Ownership).
  2. **유저별 스냅샷**: HUD HP/MP/Level/Stat/StatPoints 정상(EndUpdate).
  3. **스탯 할당**: +버튼 정상.
  4. **늦은접속/GameInfo**: 정상.
  5. **사망 디스폰**: 몬스터 사망 → 디스폰 정상. 플레이어 엔티티 despawn 시 userEntityMap 클리어.
  6. 콘솔 에러 0(`KeyNotFoundException`/NRE 없음).
- 자동화 테스트 신규 없음(단일 Assembly-CSharp; LOPEntityManager는 LOP 글루).

## GUID / .meta 정책

삭제 파일 없음(맵은 필드 제거, 파일 유지). 신규 파일 없음. `.meta` 영향 0.

## 픽스처 정책

변경 파일(`LOPEntityManager.cs`)은 픽스처 아님. `LOPGame.cs`/`ConfigureRoomComponent.cs`(로컬 픽스처)는 **미접촉 → stash 댄스 불필요**. 커밋 시 그 둘 제외(working tree dirty 유지). 커밋 금지·stash 보존 원칙 유지.

## Out of scope (defer)

- **세션 forward 참조(옵션 B)** — 스코프 역전으로 기각. 연결과 엔티티를 한 스코프에서 teardown 규율과 함께 관리하게 되면(예: 공유 시뮬에서 연결-엔티티 결합) 재논의.
- **정방향도 제거하고 호출부가 다른 인덱스 직접 사용** — 현재 facade + userEntityMap이 가장 단순. YAGNI.
- 클라 player↔entity — 클라는 맵 없음(stub), 무관.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/owner-map-dedup`, **코드는 서버 repo** 피처 브랜치. 서버 로컬 픽스처 커밋 금지·stash 보존(이번엔 미접촉). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 업계 패턴 웹 확인(옵션 B) → **스코프 역전 발견 → 옵션 A(스코프 보존) 채택**
- [x] 이 spec 작성(옵션 A로 개정)
- [x] 구현(서버 repo) — `LOPEntityManager` dedup (1파일). `GetUserIdByEntityId`는 fail-loud 유지(가드 추가 안 함)
- [ ] 사용자 런타임 검증
- [ ] 서버 → main 머지

> 옵션 B(세션 forward)는 LOP의 Room/Game 스코프 분리와 충돌(수명 역전·stale)해 기각. 정방향 인덱스를 Game 스코프에 유지하고 중복 역방향 맵만 Ownership으로 대체하는 스코프 보존안으로 정리. 남은 World Core 후보 = Stage④급.
