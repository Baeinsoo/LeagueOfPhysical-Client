# 서버 player↔entity 매핑 표준화 — LOPEntityManager 맵 제거 (세션 forward 참조 + Ownership back)

**Date:** 2026-06-20
**Branch (docs):** `feature/owner-map-dedup` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [Owner 이행](2026-06-19-world-core-owner-migration-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

서버 `LOPEntityManager`의 player↔entity 양방향 딕셔너리(`userEntityMap` userId→entityId, `entityUserMap` entityId→userId)를 제거하고, **업계 표준 패턴**으로 전환한다: **세션이 조종 엔티티 참조를 보유**(forward, Unreal `PlayerController.Pawn` / Unity Netcode `CommandTarget`)하고 **엔티티가 소유자 id를 보유**(back, `GameFramework.World.Ownership.OwnerId` — 이미 존재, Pawn.Controller / GhostOwner 대응). 중앙 딕셔너리와 두-맵 일관성 유지 부담을 제거.

동작 보존: 입력 라우팅·유저별 스냅샷·엔티티→세션 역추적·사망 디스폰 모두 이전과 동일.

> **업계 근거(웹 확인):** Unreal(PlayerController possess Pawn — `GetPawn()`/`GetController()`), Unity Netcode for Entities(연결 엔티티의 `CommandTarget`이 플레이어 엔티티 가리킴 + `GhostOwner`), Mirror(`NetworkConnection.identity`). 공통: 연결/세션 forward 참조 + 엔티티 back 참조, **중앙 맵 아님**.

## 현재 구조 (조사 확인)

- `LOPEntityManager`(`[DIMonoBehaviour]`, `sessionManager`+`entityRegistry` 주입됨): `userEntityMap`/`entityUserMap`(둘 다 `Dictionary<string,string>`). populate = `CreateEntity`(L86–91, `CharacterCreationData` && userId 비어있지 않을 때), clear = `DestroyMarkedEntities`(L128–132, `entityRegistry.Remove`[L118] 후).
- 모든 접근은 두 메서드로 funnel: `GetEntityByUserId<T>(userId)`(userEntityMap), `GetUserIdByEntityId(entityId)`(entityUserMap). 맵 필드 직접 read 없음.
- **호출 5곳**: `LOPGameEngine:87`(GetUserIdByEntityId, ProcessInput — entityId만 보유), `LOPGameEngine:145`(EndUpdate, 세션 루프 — **세션 보유**), `Game.Input.MessageHandler:38`·`Game.Entity.MessageHandler:39`·`Game.Info.MessageHandler:51`(GetEntityByUserId — 모두 `GetSessionBy*` 직후라 **세션 보유**).
- `ISessionManager.GetSessionByUserId`/`<T>` **이미 존재**(GameFramework, `SessionManager.sessionsByUserId` 백킹). userId→session O(1).
- `ISession`(GameFramework): `sessionId`/`userId`(get-only)/`Send`/… `LOPSession`(서버 concrete, 유일 impl)은 `networkConnection { get; set; }` 가변 프로퍼티 보유(선례).
- `Ownership`(GameFramework): `OwnerId`(get, ctor) = creationData.userId, **플레이어 한정**(CharacterCreator `isPlayer` gate) — 맵 populate gate와 동일 집합.
- **세션은 disconnect에도 생존**(`LOPRoom.OnPlayerDisconnect`는 `networkConnection=null`만, `RemoveSession` 안 함; userId 키, 재접 유지). → forward 참조 클리어는 **despawn 시점**이 맞음.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | 패턴 | **세션 forward 참조 + Ownership back** (중앙 맵 제거) | Unreal/Netcode/Mirror 표준. 두-맵 일관성 부담 제거 |
| ② | forward 위치 | `LOPSession.controlledEntityId`(서버 concrete) — **GameFramework `ISession` 무변경** | generic 인터페이스에 game-specific 필드 안 박음. LOP가 `GetSessionByUserId<LOPSession>`로 접근 |
| ③ | back | `Ownership.OwnerId`(기존) | 이미 entity→owner 단일 소스 |
| ④ | 접근자 facade | `GetEntityByUserId`/`GetUserIdByEntityId` **시그니처 유지, 본문만 재구현** | 호출 5곳 변경 0. Unreal GetPawn/GetController 류 thin accessor |
| ⑤ | set/clear | set = `CreateEntity`(플레이어), clear = `DestroyMarkedEntities`(despawn, registry.Remove 전 Ownership 캡처) | 세션이 disconnect 생존 → despawn에 클리어 |
| ⑥ | 범위 | **서버 전용, 단일 슬라이스.** GameFramework/클라/`LOPGame` 무변경 | 변경이 LOPSession+LOPEntityManager 2파일에 갇힘 → 픽스처 stash 불필요 |

## Scope (In) — 서버 전용, 2파일

### `Assets/Scripts/Room/LOPSession.cs`
`networkConnection { get; set; }` 옆에 추가:
```csharp
public string controlledEntityId { get; set; }
```
(서버 concrete `LOPSession`에만. `ISession` 인터페이스 무변경.)

### `Assets/Scripts/Entity/LOPEntityManager.cs`
1. **맵 필드 제거**: `userEntityMap`, `entityUserMap`(L27–28).
2. **`CreateEntity` populate 교체**(L86–91): 맵 쓰기 두 줄 → 세션 forward 참조 세팅:
   ```csharp
   if (creationData is CharacterCreationData characterCreationData
       && string.IsNullOrEmpty(characterCreationData.userId) == false)
   {
       if (sessionManager.TryGetSessionByUserId<LOPSession>(characterCreationData.userId, out var session))
       {
           session.controlledEntityId = entity.entityId;
       }
   }
   ```
   (세션은 connect 시 생성되어 spawn 전에 존재 — TryGet은 방어.)
3. **`DestroyMarkedEntities` clear 교체**(L128–132): 맵 제거 → despawn 엔티티의 owner 세션 forward 참조 클리어. **`entityRegistry.Remove`(L118) 전에** Ownership에서 ownerId 캡처:
   ```csharp
   // (per-entity 루프에서 registry.Remove 전)
   string ownerId = entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
   // ... 기존 detach/cleanup/registry.Remove/Destroy/entityMap.Remove ...
   // (맵 clear 자리 대체)
   if (ownerId != null
       && sessionManager.TryGetSessionByUserId<LOPSession>(ownerId, out var ownerSession))
   {
       ownerSession.controlledEntityId = null;
   }
   ```
4. **`GetEntityByUserId<T>(userId)` 재구현**:
   ```csharp
   public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
   {
       LOPSession session = sessionManager.GetSessionByUserId<LOPSession>(userId);
       return GetEntity<TEntity>(session.controlledEntityId);
   }
   ```
5. **`GetUserIdByEntityId(entityId)` 재구현**:
   ```csharp
   public string GetUserIdByEntityId(string entityId)
   {
       return entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
   }
   ```

> 호출 5곳·`LOPGame`·`ISession`·클라 무변경. `using` 필요 시 `LOPSession`은 같은 어셈블리(`LOP`)라 추가 불필요(또는 풀네임 `GameFramework.World.Ownership`).

## 데이터 흐름 (전환 후)

```
스폰(플레이어): CreateEntity → sessionManager.GetSessionByUserId<LOPSession>(userId).controlledEntityId = entityId
                            + (CharacterCreator) worldEntity.Add(Ownership(userId))
forward(owner→entity): GetEntityByUserId(userId) = GetSessionByUserId<LOPSession>(userId).controlledEntityId → GetEntity
back(entity→owner):    GetUserIdByEntityId(entityId) = entityRegistry.Get(entityId)?.Get<Ownership>()?.OwnerId
despawn: DestroyMarkedEntities → ownerId(Ownership) 캡처 → registry.Remove → ownerSession.controlledEntityId = null
```

두 맵 제거. 세션이 forward 단일 소스, Ownership이 back 단일 소스.

## 검증

- **컴파일**: 서버 0에러(UnityMCP `refresh_unity`+`read_console`, 서버 인스턴스 핀). GameFramework/클라 무변경.
- **런타임(수동, 서버 플레이)**:
  1. **입력 라우팅**: 이동/액션 정상(Game.Input → GetEntityByUserId → EntityInputComponent), 클라가 InputSequenceToC 수신(LOPGameEngine:87 GetUserIdByEntityId 역방향).
  2. **유저별 스냅샷**: HUD HP/MP/Level/Stat/StatPoints 정상(EndUpdate GetEntityByUserId via 세션).
  3. **스탯 할당**: +버튼 정상(Game.Entity GetEntityByUserId).
  4. **늦은접속/GameInfo**: 정상(Game.Info GetEntityByUserId).
  5. **사망 디스폰**: 몬스터 사망 → 디스폰 정상. 플레이어 엔티티 despawn 시 세션 controlledEntityId 클리어(스테일 참조 없음).
  6. 콘솔 에러 0(특히 `KeyNotFoundException`/NRE 없음).
- 자동화 테스트 신규 없음(단일 Assembly-CSharp; LOPEntityManager는 LOP 글루).

## GUID / .meta 정책

삭제 파일 없음(맵은 필드 제거, 파일 유지). 신규 파일 없음. `.meta` 영향 0.

## 픽스처 정책

변경 2파일(`LOPSession.cs`/`LOPEntityManager.cs`)은 픽스처 아님. `LOPGame.cs`/`ConfigureRoomComponent.cs`(로컬 픽스처)는 **미접촉 → stash 댄스 불필요**. 커밋 시 그 둘 제외(working tree dirty 유지). 커밋 금지·stash 보존 원칙 유지.

## Out of scope (defer)

- **`controlledEntityId`를 GameFramework `ISession`으로 승격** — 현재 LOPSession concrete로 충분(generic 인터페이스 오염 회피). 다른 세션 impl이 생기거나 공유 시뮬에서 필요해지면 재논의.
- **접근자 facade 제거 + 호출부가 `session.controlledEntityId` 직접 사용** — 향후 idiom 강화 시(현재는 facade 유지로 churn 0).
- **`SessionManager.sessionsByUserId`도 Ownership 기반으로** — 세션 자체 인덱스는 별개 관심사(연결 관리), 유지.
- 클라 player↔entity — 클라는 맵 없음(stub), 무관.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/owner-map-dedup`, **코드는 서버 repo** 피처 브랜치. 서버 로컬 픽스처 커밋 금지·stash 보존(이번엔 미접촉). 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 업계 패턴 웹 확인 + 세션 forward(LOPSession.controlledEntityId)+Ownership back 합의 + 맵 제거 범위
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan
- [ ] 구현(서버 repo) → 검증 → 머지

> 이 슬라이스로 player↔entity 매핑이 업계 표준(세션 forward + Ownership back)으로 정리됨. 남은 World Core 후보 = Stage④급(공유 시뮬/예측/물리/통합 fan-out).
