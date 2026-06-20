# Server player↔entity Map Dedup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPEntityManager`의 두 player↔entity 딕셔너리를 제거하고, 세션 forward 참조(`LOPSession.controlledEntityId`) + 엔티티 back 참조(`Ownership.OwnerId`)로 전환 — 접근자 시그니처는 유지(호출부 무변경).

**Architecture:** 서버 전용 2파일. forward = `LOPSession.controlledEntityId`(서버 concrete, GameFramework `ISession` 무변경), back = `Ownership.OwnerId`(기존). `GetEntityByUserId`/`GetUserIdByEntityId`는 본문만 새 참조 위로 재구현(Unreal GetPawn/GetController 류 thin accessor) → 호출 5곳 변경 0. set = `CreateEntity`(플레이어), clear = `DestroyMarkedEntities`(despawn). 자동 테스트 없음(단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), VContainer DI, UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-20-owner-map-dedup-design.md`

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (서버 작업 인가).

> **픽스처:** 변경 2파일(`LOPSession.cs`/`LOPEntityManager.cs`)은 픽스처 아님. `LOPGame.cs`/`ConfigureRoomComponent.cs`(dirty 로컬 픽스처)는 **미접촉** → stash 댄스 불필요, 커밋 시 제외만(Stats Slice 3 선례).

---

## File Structure (server repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`)

- **Modify:** `Assets/Scripts/Room/LOPSession.cs` — `controlledEntityId` 프로퍼티 추가.
- **Modify:** `Assets/Scripts/Entity/LOPEntityManager.cs` — 두 맵 제거 + `CreateEntity` set + `DestroyMarkedEntities` clear + 두 접근자 재구현.

---

## Task 1: 서버 피처 브랜치

- [ ] **Step 1:**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: dirty = `LOPGame.cs` + `ConfigureRoomComponent.cs`(픽스처, 미접촉). 다른 dirty 있으면 중단.
- [ ] **Step 2:**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/owner-map-dedup
```
(픽스처 dirty는 따라옴 — 이번 변경과 무관, stash 안 함.)

---

## Task 2: LOPSession — controlledEntityId 추가

**Files:** Modify `Assets/Scripts/Room/LOPSession.cs`

- [ ] **Step 1:** `public NetworkConnection networkConnection { get; set; }`(15줄) 다음에 추가:
```csharp

        public string controlledEntityId { get; set; }
```
(서버 concrete `LOPSession`에만. `ISession` 인터페이스 무변경.)

---

## Task 3: LOPEntityManager — 맵 제거 + set/clear + 접근자 재구현

**Files:** Modify `Assets/Scripts/Entity/LOPEntityManager.cs`

> 이 4개 편집은 상호의존(맵 필드 제거 시 4 사용처 동시 수정해야 컴파일). 한 태스크로 묶어 끝에 컴파일.

- [ ] **Step 1: 맵 필드 제거 (27–28줄)**
```csharp
        private Dictionary<string, string> userEntityMap = new Dictionary<string, string>();
        private Dictionary<string, string> entityUserMap = new Dictionary<string, string>();
```
삭제. (26줄 `entityMap`은 유지.)

- [ ] **Step 2: CreateEntity populate 교체 (86–91줄)**
```csharp
            if (creationData is CharacterCreationData characterCreationData
                && string.IsNullOrEmpty(characterCreationData.userId) == false)
            {
                userEntityMap[characterCreationData.userId] = entity.entityId;
                entityUserMap[entity.entityId] = characterCreationData.userId;
            }
```
→
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

- [ ] **Step 3: DestroyMarkedEntities — ownerId 캡처(registry.Remove 전) + clear 교체**

`LOPEntity lopEntity = GetEntity<LOPEntity>(entityId);`(105줄) 다음에 ownerId 캡처 추가(registry.Remove[118줄] 전에 Ownership 읽어야 함):
```csharp
                LOPEntity lopEntity = GetEntity<LOPEntity>(entityId);
                string ownerId = GetUserIdByEntityId(entityId);
```
그리고 맵 clear(128–132줄)를 교체:
```csharp
                if (entityUserMap.TryGetValue(entityId, out var userId))
                {
                    userEntityMap.Remove(userId);
                    entityUserMap.Remove(entityId);
                }
```
→
```csharp
                if (ownerId != null
                    && sessionManager.TryGetSessionByUserId<LOPSession>(ownerId, out var ownerSession))
                {
                    ownerSession.controlledEntityId = null;
                }
```
(`GetUserIdByEntityId`는 Step 5에서 Ownership 기반으로 재구현됨 → 105줄 시점[registry.Remove 전]엔 Ownership 살아있어 유효. registry.Remove[118] 후엔 null이 되므로 반드시 105줄에서 캡처.)

- [ ] **Step 4: GetEntityByUserId 재구현 (160–165줄)**
```csharp
        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
        {
            string entityId = userEntityMap[userId];

            return GetEntity<TEntity>(entityId);
        }
```
→
```csharp
        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
        {
            LOPSession session = sessionManager.GetSessionByUserId<LOPSession>(userId);

            return GetEntity<TEntity>(session.controlledEntityId);
        }
```

- [ ] **Step 5: GetUserIdByEntityId 재구현 (155–158줄)**
```csharp
        public string GetUserIdByEntityId(string entityId)
        {
            return entityUserMap[entityId];
        }
```
→
```csharp
        public string GetUserIdByEntityId(string entityId)
        {
            return entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
        }
```

---

## Task 4: 컴파일 + 잔존 참조 확인

- [ ] **Step 1: 컴파일**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.
- [ ] **Step 2: 맵 잔존 0 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "userEntityMap\|entityUserMap" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치 있으면 처리 — 중단.

---

## Task 5: 커밋 (2파일 — 픽스처 제외)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: `LOPSession.cs` + `LOPEntityManager.cs` 수정 + (무관 픽스처) `LOPGame.cs`/`ConfigureRoomComponent.cs`. **픽스처 2파일 stage 안 함.**
- [ ] **Step 2: stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Room/LOPSession.cs Assets/Scripts/Entity/LOPEntityManager.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "refactor(server): replace LOPEntityManager player<->entity maps with session forward ref + Ownership back

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 3:** `git -C "..." show --stat HEAD | head -8` — 2파일만. `LOPGame.cs`/`ConfigureRoomComponent.cs` 포함되면 중단.

---

## Task 6: 런타임 수동 검증 (사용자)

서버 플레이:
1. **입력 라우팅**: 이동/액션 정상(GetEntityByUserId via 세션), 클라 InputSequenceToC 수신(GetUserIdByEntityId via Ownership).
2. **유저별 스냅샷**: HUD HP/MP/Level/Stat/StatPoints 정상(EndUpdate).
3. **스탯 할당**: +버튼 정상.
4. **늦은접속/GameInfo**: 신규/재접 유저 엔티티 정상 표시.
5. **사망 디스폰**: 몬스터 사망 → 디스폰 정상. 플레이어 엔티티 despawn 시 세션 controlledEntityId 클리어(다음 접근에 스테일 없음).
6. 콘솔 에러 0(`KeyNotFoundException`/NRE 없음).

동작 보존(회귀 0)이 성공.

---

## 완료 기준

- [ ] 서버 `feature/owner-map-dedup`에 커밋 1개(2파일, 픽스처 미포함).
- [ ] 서버 컴파일 0에러. `userEntityMap`/`entityUserMap` 완전 제거(grep 0).
- [ ] 픽스처 2파일 working tree 보존(미커밋), `5-23` stash 무손상.
- [ ] 수동 플레이: 입력/스냅샷/할당/늦은접속/디스폰 동작 보존.

이후: 사용자 검증 후 서버 → main 머지(stash 댄스 — 머지가 LOPGame 커밋 안 건드리므로 실제론 불필요하나 픽스처 dirty 보존 위해 동일 절차). player↔entity 매핑 업계 표준화 완료. 남은 건 Stage④급.
