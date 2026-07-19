# 클라 Assets/Scripts 폴더 재편 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 `Assets/Scripts` 폴더를 개념 단위로 그루핑하고 업계 표준 이름으로 정정한다 — 순수 파일/폴더 이동, 코드 편집 0줄.

**Architecture:** 모든 파일이 평면 `namespace LOP`라 폴더 이동이 타입 해석/참조에 영향을 주지 않는다(코드 무편집). 씬/프리팹 GUID 참조는 `.cs`와 짝 `.meta`를 함께 옮기면 보존된다. 검증은 마지막에 Unity 컴파일 클린 + `.meta` 무결성 + 인게임 스모크로.

**Tech Stack:** git(`git mv`), Unity 6 Editor(단일 Assembly-CSharp, asmdef 없음), UnityMCP(컴파일 확인).

## Global Constraints

- **코드 내용 편집 0줄** — 이 계획의 어떤 태스크도 `.cs` 파일 *내용*을 바꾸지 않는다. 순수 이동/개명만.
- **`.cs`와 `.cs.meta`는 항상 쌍으로 이동.** 폴더 개명 시 폴더 `.meta`(부모에 위치, 예 `Assets/Scripts/Data.meta`)도 함께 개명. 빈 폴더 삭제 시 폴더 `.meta`도 `git rm`.
- **작업 브랜치**: `refactor/scripts-folder-reorg` (이미 생성됨, spec 커밋 존재). main 직접 커밋 금지.
- **모든 git 명령의 기준 디렉터리**: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`. 태스크 내 `cd Assets/Scripts`는 그 안에서 상대 이동을 쉽게 하기 위함.
- **UnityMCP 대상 고정**: 클라 인스턴스에만. `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 `id`를 찾아 모든 UnityMCP 호출에 `unity_instance="<그 id>"`로 넘긴다(서버 인스턴스 오염 금지).
- 무관한 작업트리 변경(`Assets/Art`, `UIRoot.prefab`, `ProjectSettings/*`)은 건드리지 않는다 — 각 커밋은 이 계획이 만든 파일만 스테이징.
- **이동 중 Unity 자동 refresh 방지**: `.cs`를 옮겼는데 짝 `.meta`가 아직 안 옮겨진 찰나에 Unity 에디터가 포커스를 받아 refresh하면 새 GUID로 meta를 재생성해 참조가 깨질 수 있다. Task 1~7의 파일 이동은 **Unity 에디터를 백그라운드(비포커스)에 둔 채** 실행한다(포커스 안 주면 auto-refresh 안 함). 이동이 다 끝난 Task 8에서 한 번에 refresh.

---

### Task 1: 빈 폴더 삭제

파일이 0개인 껍데기 폴더 3개를 제거한다(폴더 `.meta`만 tracked).

**Files:**
- Delete: `Assets/Scripts/Extensions/` (+ `Assets/Scripts/Extensions.meta`)
- Delete: `Assets/Scripts/Popup/` (+ `Assets/Scripts/Popup.meta`)
- Delete: `Assets/Scripts/Game/UI/` (+ `Assets/Scripts/Game/UI.meta`)

- [ ] **Step 1: 폴더가 진짜 비었는지 재확인**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && for d in Assets/Scripts/Extensions Assets/Scripts/Popup Assets/Scripts/Game/UI; do echo "$d: $(find "$d" -type f 2>/dev/null | wc -l) files"; done
```
Expected: 각 `0 files`.

- [ ] **Step 2: 폴더 meta 제거 + 빈 디렉터리 삭제**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git rm Extensions.meta Popup.meta Game/UI.meta && rmdir Extensions Popup Game/UI 2>/dev/null || true
```
Expected: `rm 'Assets/Scripts/Extensions.meta'` 등 3줄.

- [ ] **Step 3: 스테이징 내용 확인**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git status --short | grep -E "Extensions|Popup|Game/UI"
```
Expected: `D  Assets/Scripts/Extensions.meta`, `D  Assets/Scripts/Popup.meta`, `D  Assets/Scripts/Game/UI.meta` 3줄만.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): 빈 폴더 삭제 (Extensions/Popup/Game.UI)"
```

---

### Task 2: Entity 통합

엔티티 관련 코드를 `Entity/`로 모으고 빈 껍데기 `EntityCreator/`·`Component/`를 제거한다.

**Files:**
- Move: `EntityCreator/CharacterCreator.cs` → `Entity/CharacterCreator.cs`
- Move: `EntityCreator/ItemCreator.cs` → `Entity/ItemCreator.cs`
- Move: `Component/PhysicsFollower.cs` → `Entity/PhysicsFollower.cs`
- Move: `Game/EntityBinder.cs` → `Entity/EntityBinder.cs`
- Delete: `EntityCreator/` (+ `.meta`), `Component/` (+ `.meta`)

**Interfaces:**
- Consumes: (없음 — 독립)
- Produces: `Entity/`가 엔티티 앵커/뷰/스폰/생성 코드의 단일 집이 됨. 이후 태스크는 이 위치 가정.

- [ ] **Step 1: `.cs`+`.meta` 쌍 이동**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && for f in EntityCreator/CharacterCreator EntityCreator/ItemCreator Component/PhysicsFollower Game/EntityBinder; do b=$(basename "$f"); git mv "$f.cs" "Entity/$b.cs" && git mv "$f.cs.meta" "Entity/$b.cs.meta"; done
```
Expected: 에러 없이 완료(출력 없음).

- [ ] **Step 2: 빈 폴더 meta 제거 + 디렉터리 삭제**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git rm EntityCreator.meta Component.meta && rmdir EntityCreator Component 2>/dev/null || true
```
Expected: `rm 'Assets/Scripts/EntityCreator.meta'`, `rm 'Assets/Scripts/Component.meta'`.

- [ ] **Step 3: 짝 이동 검증 ( `.cs`만 옮기고 `.meta` 누락된 게 없는지 )**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git status --short | grep -E "CharacterCreator|ItemCreator|PhysicsFollower|EntityBinder"
```
Expected: 각 파일마다 `R` (rename) 2줄씩(`.cs` + `.cs.meta`), 총 8줄. `.cs`만 있고 `.meta` 없는 항목이 없어야 함.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): 엔티티 코드를 Entity/로 통합 (EntityCreator/Component/EntityBinder)"
```

---

### Task 3: Netcode/ 신설 — 흩어진 넷코드 집결

시간동기·보정·스냅샷을 새 `Netcode/`로 모은다. `Model/`에서 스냅샷을 먼저 빼내야 Task 5(Model→Domain)가 깨끗해진다 → 반드시 Task 5보다 먼저.

**Files:**
- Create dir: `Assets/Scripts/Netcode/`
- Move (from `Game/`): LOPTickUpdater, MirrorNetworkTime, LeadState, ReconciliationStats, InputTimingStats, MatchSeed
- Move (from `Entity/`): Reconciler, LocalEntityInterpolator, RemoteEntityInterpolator, RemoteInterpolationClock
- Move (from `World/`): WorldEventSink → 그 후 빈 `World/` (+ `.meta`) 삭제
- Move (from `Model/`): EntitySnap, LocalEntitySnap

**Interfaces:**
- Consumes: Task 2 결과(보간기/Reconciler가 아직 `Entity/`에 있음).
- Produces: `Netcode/`가 클라 넷코드 코드의 집. `Model/`은 스냅샷 2개가 빠져 11파일.

- [ ] **Step 1: Netcode 폴더 생성**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && mkdir -p Netcode
```
Expected: 출력 없음.

- [ ] **Step 2: `.cs`+`.meta` 쌍 이동 (13파일)**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && for f in Game/LOPTickUpdater Game/MirrorNetworkTime Game/LeadState Game/ReconciliationStats Game/InputTimingStats Game/MatchSeed Entity/Reconciler Entity/LocalEntityInterpolator Entity/RemoteEntityInterpolator Entity/RemoteInterpolationClock World/WorldEventSink Model/EntitySnap Model/LocalEntitySnap; do b=$(basename "$f"); git mv "$f.cs" "Netcode/$b.cs" && git mv "$f.cs.meta" "Netcode/$b.cs.meta"; done
```
Expected: 에러 없이 완료.

- [ ] **Step 3: 빈 World/ 제거**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git rm World.meta && rmdir World 2>/dev/null || true
```
Expected: `rm 'Assets/Scripts/World.meta'`.

- [ ] **Step 4: 이동 개수·짝 검증**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && echo "Netcode .cs: $(git status --short | grep -c 'Netcode/.*\.cs$')" && echo "Netcode .meta: $(git status --short | grep -c 'Netcode/.*\.cs\.meta$')"
```
Expected: `Netcode .cs: 13`, `Netcode .meta: 13` (짝이 맞음).

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): Netcode/ 신설 — 시간동기·보정·스냅샷 집결"
```

---

### Task 4: Data/ → Stores/ 개명

런타임 상태 캐시(DataStore들)를 `Stores/`로. `Model/`(도메인)과의 이름 혼동 해소.

**Files:**
- Rename dir: `Assets/Scripts/Data/` → `Assets/Scripts/Stores/`
- Rename folder meta: `Assets/Scripts/Data.meta` → `Assets/Scripts/Stores.meta`

**Interfaces:**
- Consumes: (없음 — 독립)
- Produces: DataStore 8파일이 `Stores/`에.

- [ ] **Step 1: 폴더 + 폴더 meta 개명**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git mv Data Stores && git mv Data.meta Stores.meta
```
Expected: 에러 없음.

- [ ] **Step 2: 검증**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && ls Assets/Scripts/Stores/*.cs | wc -l && test -f Assets/Scripts/Stores.meta && echo "folder meta OK" && test ! -e Assets/Scripts/Data && echo "old dir gone"
```
Expected: `8`, `folder meta OK`, `old dir gone`.

- [ ] **Step 3: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): Data/ -> Stores/ (런타임 상태 캐시, Model과 혼동 해소)"
```

---

### Task 5: Model/ → Domain/ 개명

앱 내부 도메인 표현을 `Domain/`으로 역할 명시. (Task 3에서 스냅샷이 이미 빠졌으므로 순수 도메인 데이터/enum 11파일만 남음.)

**Files:**
- Rename dir: `Assets/Scripts/Model/` → `Assets/Scripts/Domain/`
- Rename folder meta: `Assets/Scripts/Model.meta` → `Assets/Scripts/Domain.meta`

**Interfaces:**
- Consumes: Task 3 완료(EntitySnap/LocalEntitySnap가 이미 `Netcode/`로 빠짐).
- Produces: 도메인 데이터/enum 11파일이 `Domain/`에.

- [ ] **Step 1: 스냅샷이 이미 빠졌는지 확인(Task 3 선행 검증)**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && ls Assets/Scripts/Model/*.cs | wc -l && ! ls Assets/Scripts/Model/EntitySnap.cs 2>/dev/null && echo "snapshots already moved"
```
Expected: `11`, `snapshots already moved`.

- [ ] **Step 2: 폴더 + 폴더 meta 개명**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git mv Model Domain && git mv Model.meta Domain.meta
```
Expected: 에러 없음.

- [ ] **Step 3: 검증**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && ls Assets/Scripts/Domain/*.cs | wc -l && test -f Assets/Scripts/Domain.meta && echo "folder meta OK" && test ! -e Assets/Scripts/Model && echo "old dir gone"
```
Expected: `11`, `folder meta OK`, `old dir gone`.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): Model/ -> Domain/ (anemic 도메인 표현 역할 명시)"
```

---

### Task 6: WebAPI/Model/ → WebAPI/Dto/ 개명

백엔드 응답 원본은 전송 객체(DTO)라 이름을 정정.

**Files:**
- Rename dir: `Assets/Scripts/WebAPI/Model/` → `Assets/Scripts/WebAPI/Dto/`
- Rename folder meta: `Assets/Scripts/WebAPI/Model.meta` → `Assets/Scripts/WebAPI/Dto.meta`

**Interfaces:**
- Consumes: (없음 — 독립)
- Produces: WebAPI 응답 DTO들이 `WebAPI/Dto/`에.

- [ ] **Step 1: 폴더 + 폴더 meta 개명**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git mv WebAPI/Model WebAPI/Dto && git mv WebAPI/Model.meta WebAPI/Dto.meta
```
Expected: 에러 없음.

- [ ] **Step 2: 검증**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && ls Assets/Scripts/WebAPI/Dto/*.cs | wc -l && test -f Assets/Scripts/WebAPI/Dto.meta && echo "folder meta OK" && test ! -e Assets/Scripts/WebAPI/Model && echo "old dir gone"
```
Expected: 파일 수(>0), `folder meta OK`, `old dir gone`.

- [ ] **Step 3: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): WebAPI/Model/ -> WebAPI/Dto/ (전송 객체 정정)"
```

---

### Task 7: Messaging/ → Network/ 병합

`NetworkMessageDispatcher`는 네트워킹이라 `Network/`로. 빈 `Messaging/` 제거.

**Files:**
- Move: `Messaging/NetworkMessageDispatcher.cs` → `Network/NetworkMessageDispatcher.cs`
- Delete: `Messaging/` (+ `.meta`)

**Interfaces:**
- Consumes: (없음 — 독립)
- Produces: 네트워크 코드가 `Network/`에 응집.

- [ ] **Step 1: `.cs`+`.meta` 쌍 이동**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git mv Messaging/NetworkMessageDispatcher.cs Network/NetworkMessageDispatcher.cs && git mv Messaging/NetworkMessageDispatcher.cs.meta Network/NetworkMessageDispatcher.cs.meta
```
Expected: 에러 없음.

- [ ] **Step 2: 빈 Messaging/ 제거**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts" && git rm Messaging.meta && rmdir Messaging 2>/dev/null || true
```
Expected: `rm 'Assets/Scripts/Messaging.meta'`.

- [ ] **Step 3: 검증**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && test -f Assets/Scripts/Network/NetworkMessageDispatcher.cs && test -f Assets/Scripts/Network/NetworkMessageDispatcher.cs.meta && echo "moved OK" && test ! -e Assets/Scripts/Messaging && echo "old dir gone"
```
Expected: `moved OK`, `old dir gone`.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git commit -m "refactor(scripts): Messaging/ -> Network/ 병합"
```

---

### Task 8: Unity 컴파일 검증 + 신규 폴더 meta 커밋 + ROADMAP

Unity가 재import하며 새 `Netcode/` 폴더 meta를 생성하고 컴파일한다. 에러 0을 확인하고, Unity가 만든 meta를 커밋한 뒤 ROADMAP에 기록한다.

**Files:**
- Commit: `Assets/Scripts/Netcode.meta` (Unity 생성)
- Modify: `docs/ROADMAP.md`

**Interfaces:**
- Consumes: Task 1~7 완료(모든 이동/개명 끝).
- Produces: 컴파일 클린 검증 + 로드맵 기록.

- [ ] **Step 1: Unity 재import + 컴파일**

UnityMCP `refresh_unity` 호출(클라 인스턴스, `unity_instance` 지정). 이어 `editor_state`의 `isCompiling`이 false가 될 때까지 대기.

- [ ] **Step 2: 컴파일 에러 0 확인**

UnityMCP `read_console`(클라 인스턴스, `unity_instance` 지정)로 error/exception 필터. 
Expected: 컴파일 에러 0. "meta file without asset" / "asset without meta" 경고도 0.

만약 에러가 나면: 십중팔구 `.meta` 누락/orphan. `git status`로 `.cs`↔`.meta` 짝을 재확인하고 누락된 쪽을 마저 `git mv`.

- [ ] **Step 3: Unity가 생성한 Netcode 폴더 meta 스테이징**

Run:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Netcode.meta && git status --short Assets/Scripts/Netcode.meta
```
Expected: `A  Assets/Scripts/Netcode.meta` (Unity가 생성). 없으면 Step 1 refresh가 아직 안 돈 것 — 재확인.

- [ ] **Step 4: ROADMAP Done 원장에 기록**

`docs/ROADMAP.md`의 "그 밖의 완료 워크스트림" 위(활성 트랙 근처 적절한 위치)에 한 줄 추가:

```markdown
- ✅ **클라 Assets/Scripts 폴더 재편** (2026-07-19) — 개념 그루핑 + 업계 표준 네이밍: 엔티티 3폴더→`Entity/` 통합, 넷코드 집결 `Netcode/` 신설, `Data/`→`Stores/`·`Model/`→`Domain/`·`WebAPI/Model/`→`WebAPI/Dto/` 개명, `Messaging/`→`Network/` 병합, 빈 폴더 삭제. 평면 namespace라 코드 편집 0(순수 이동). 컴파일 클린 + 인게임 스모크. spec/plan `2026-07-19-scripts-folder-reorg*`. 서버 대칭은 별도 세션. `[[entity-unity-layer-rearchitecture]]`.
```

- [ ] **Step 5: 인게임 스모크 검증 (사용자)**

사용자에게 요청: 로그인 → 매칭 → 게임 진입 → 이동/충돌/아이템/넉백/데미지/원격보간/롤백 한 바퀴. MonoBehaviour(PhysicsFollower/보간기/LOPActor/EntityBinder)의 씬·프리팹 참조가 살아있는지(에러/미싱 스크립트 없음) 확인.

- [ ] **Step 6: Commit (meta + ROADMAP)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Netcode.meta docs/ROADMAP.md && git commit -m "refactor(scripts): Netcode 폴더 meta + ROADMAP 기록"
```

---

## 완료 후

- `superpowers:finishing-a-development-branch`로 main `--no-ff` 머지 → push → 브랜치 삭제.
- 서버 repo 대칭 정리는 **별도 세션**(구조가 클라와 완전 동일하지 않아 별도 감사 필요).
