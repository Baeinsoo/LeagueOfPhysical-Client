# Server player↔entity Map Dedup Implementation Plan (옵션 A — 스코프 보존)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPEntityManager`의 중복 역방향 맵(`entityUserMap`)을 제거하고 entity→owner 조회를 `Ownership.OwnerId`로 파생 — 정방향 맵(`userEntityMap`)은 Game 스코프 인덱스로 유지(접근자 시그니처 유지, 호출부 무변경).

**Architecture:** 서버 전용 1파일. **스코프 보존이 핵심** — 세션(Room 스코프)이 엔티티(Game 스코프)를 forward 참조하면 수명 역전·stale이 되므로(옵션 B 기각), 정방향 인덱스 `userEntityMap`을 Game 스코프 `LOPEntityManager`에 그대로 두고 군더더기 역방향 맵만 Ownership(Game 스코프)으로 대체한다. `GetEntityByUserId`는 본문도 불변, `GetUserIdByEntityId`만 Ownership 파생으로 재구현. `GetUserIdByEntityId`가 throw→null로 바뀌지만 호출처 `ProcessInput`은 "입력=항상 플레이어=항상 Ownership" 불변식 자리라 **가드를 추가하지 않는다**(원 설계 의도 = fail-loud; null이 내려가도 downstream에서 시끄럽게 터짐). 자동 테스트 없음(단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), VContainer DI, UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-20-owner-map-dedup-design.md`

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (서버 작업 인가).

> **픽스처:** 변경 파일(`LOPEntityManager.cs`)은 픽스처 아님. `LOPGame.cs`/`ConfigureRoomComponent.cs`(dirty 로컬 픽스처)는 **미접촉** → stash 댄스 불필요, 커밋 시 제외만.

> **옵션 B 폐기 노트:** 초기 plan은 `LOPSession.controlledEntityId`(세션 forward 참조)로 두 맵 모두 제거하는 옵션 B였으나, 세션(Room 스코프)이 엔티티(Game 스코프)를 알면 게임 teardown 시 stale 참조가 남는 스코프 역전이 발견되어 기각. 이 plan은 옵션 A(스코프 보존)로 개정됨. `LOPSession`은 main 그대로.

> **`ProcessInput` 가드 안 함 노트:** `GetUserIdByEntityId`가 throw→null로 바뀌지만, 그 자리는 "입력 엔티티=항상 플레이어=항상 Ownership 보유" 불변식이 성립하는 곳이라 **null 가드를 추가하지 않는다**(원 설계가 일부러 예외 처리를 안 둔 fail-loud 의도). 불변식이 깨지면 downstream(`GetSessionByUserId`/`Send`)에서 시끄럽게 터지게 둔다. `LOPGameEngine`은 main 그대로.

---

## File Structure (server repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`)

- **Modify:** `Assets/Scripts/Entity/LOPEntityManager.cs` — `entityUserMap` 제거 + `CreateEntity` populate 1줄 제거 + `DestroyMarkedEntities` ownerId 캡처/clear 교체 + `GetUserIdByEntityId` Ownership 파생 재구현. (`userEntityMap`·`GetEntityByUserId` 유지.) **이 1파일만 변경.**

---

## Task 1: 서버 피처 브랜치

- [ ] **Step 1:** `git -C "..." status --short` — dirty = `LOPGame.cs` + `ConfigureRoomComponent.cs`(픽스처, 미접촉). 다른 dirty 있으면 확인.
- [ ] **Step 2:** `git -C "..." checkout -b feature/owner-map-dedup` (픽스처 dirty는 따라옴 — stash 안 함).

---

## Task 2: LOPEntityManager — 역방향 맵 제거 + 접근자 재구현

**Files:** Modify `Assets/Scripts/Entity/LOPEntityManager.cs`

> 상호의존 편집 — 한 태스크로 묶어 끝에 컴파일.

- [ ] **Step 1: `entityUserMap` 필드 제거** (`userEntityMap`은 유지):
```csharp
private Dictionary<string, string> entityUserMap = new Dictionary<string, string>();
```
삭제.

- [ ] **Step 2: CreateEntity populate 1줄 제거** — `entityUserMap[entity.entityId] = characterCreationData.userId;` 삭제. `userEntityMap[characterCreationData.userId] = entity.entityId;`는 유지.

- [ ] **Step 3: DestroyMarkedEntities — ownerId 캡처(registry.Remove 전) + clear 교체.** `LOPEntity lopEntity = GetEntity<LOPEntity>(entityId);` 다음에:
```csharp
                string ownerId = GetUserIdByEntityId(entityId);   // capture before registry.Remove (reads Ownership)
```
맵 clear 블록(기존 `entityUserMap.TryGetValue` 분기)을 교체:
```csharp
                if (ownerId != null)
                {
                    userEntityMap.Remove(ownerId);
                }
```
(`GetUserIdByEntityId`는 Step 5에서 Ownership 파생 → registry.Remove 전엔 유효, 후엔 null이므로 반드시 사전 캡처.)

- [ ] **Step 4: GetEntityByUserId 무변경 확인** — `string entityId = userEntityMap[userId]; return GetEntity<TEntity>(entityId);` 그대로.

- [ ] **Step 5: GetUserIdByEntityId 재구현** (Ownership 파생):
```csharp
        public string GetUserIdByEntityId(string entityId)
        {
            return entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
        }
```

> **ProcessInput 가드 추가 안 함:** `GetUserIdByEntityId`가 throw→null로 바뀌지만 호출처는 "입력=항상 플레이어=항상 Ownership" 불변식 자리라 null 가드를 두지 않는다(fail-loud 의도). `LOPGameEngine`은 main 그대로.

---

## Task 3: 컴파일 + 잔존 참조 확인

- [ ] **Step 1: 컴파일** — `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` + `read_console(types=["error"])` → 0 errors.
- [ ] **Step 2: 역방향 맵 잔존 0** — `git -C "..." grep -n "entityUserMap" -- "Assets/Scripts" || echo "NO MATCHES"` → `NO MATCHES`. `userEntityMap`은 유지(매치 있어야 정상).

---

## Task 4: 커밋 (1파일 — 픽스처 제외)

- [ ] **Step 1:** `git -C "..." status --short` — `LOPEntityManager.cs` 수정 + (무관 픽스처) `LOPGame.cs`/`ConfigureRoomComponent.cs`. **픽스처 stage 안 함.**
- [ ] **Step 2:** `git add Assets/Scripts/Entity/LOPEntityManager.cs` + 커밋.
- [ ] **Step 3:** `git show --stat HEAD | head -8` — `LOPEntityManager.cs` 1파일만. 픽스처/`LOPGameEngine` 포함되면 중단.

---

## Task 5: 런타임 수동 검증 (사용자)

서버 플레이: 1. 입력 라우팅(이동/액션 + InputSequenceToC 수신). 2. 유저별 스냅샷(HUD HP/MP/Level/Stat/StatPoints). 3. 스탯 할당 +버튼. 4. 늦은접속/GameInfo. 5. 사망 디스폰 + 플레이어 despawn 시 userEntityMap 클리어. 6. 콘솔 에러 0(`KeyNotFoundException`/NRE 없음). 동작 보존(회귀 0)이 성공.

---

## 완료 기준

- [ ] 서버 `feature/owner-map-dedup`에 커밋(`LOPEntityManager.cs` 1파일, 픽스처/`LOPGameEngine` 미포함).
- [ ] 서버 컴파일 0에러. `entityUserMap` 완전 제거(grep 0), `userEntityMap` 유지.
- [ ] 픽스처 2파일(`LOPGame`/`ConfigureRoomComponent`) working tree 보존(미커밋), `5-23` stash 무손상.
- [ ] 수동 플레이: 입력/스냅샷/할당/늦은접속/디스폰 동작 보존.

이후: 사용자 검증 후 서버 → main 머지(머지가 LOPGame 커밋 안 건드림 — stash 댄스 불필요). 중복 역방향 맵 제거 + 스코프 보존 완료. 남은 건 Stage④급.
