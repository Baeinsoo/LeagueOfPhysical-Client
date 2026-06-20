# Netcode Phase 1 — 회전 결정론적 snap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클·서 `LOPMovementManager.ProcessInput`의 회전 처리를 버그 있는 `Mathf.SmoothDampAngle`에서 **결정론적 snap**으로 바꿔, 클·서 회전 발산을 제거한다.

**Architecture:** 클·서 `LOPMovementManager.cs`는 현재 **바이트 동일**. 이동 블록(`if (direction.sqrMagnitude > 0)`) 안의 회전 4줄을 무상태·dt-비의존 snap 2줄로 교체. 부드러운 facing 연출은 뷰/Stage④로 미룸(이번 범위 밖). 클·서 두 파일을 동일하게 수정해 바이트 동일 유지.

**Tech Stack:** C# (Unity), UnityMCP 컴파일 검증. 자동 테스트 없음(클·서 단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Related spec:** `docs/superpowers/specs/2026-06-20-netcode-phase1-rotation-snap-design.md`

**Resolved Unity instances (매 UnityMCP 호출에 명시 — HTTP stateless):**
- Client: `mcpforunity://instances`에서 `LeagueOfPhysical-Client` 인스턴스의 `id`(`Name@hash`) 해석 후 사용.
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (직전 작업 기준 — 변동 시 `mcpforunity://instances`로 재해석). 이번은 사용자 지시 netcode 작업이라 서버 인스턴스 조작 인가.

> **픽스처:** 변경 파일(`LOPMovementManager.cs`)은 픽스처 아님. 서버 `LOPGame.cs`/`ConfigureRoomComponent.cs`(dirty 로컬 픽스처)는 **미접촉** → stash 댄스 불필요, 커밋 시 제외만.

---

## File Structure

- **Modify:** `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPMovementManager.cs` — 회전 블록 snap 교체.
- **Modify:** `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPMovementManager.cs` — 동일 교체(바이트 동일 유지).

두 파일은 동일 변경이므로 한 번에 작업하고 양쪽 컴파일로 검증한다. 슬라이스(클/서)는 wire 무관이라 서로 독립이나, 동일 코드라 한 plan에서 같이 처리.

---

## Task 1: 클라 피처 브랜치 + 회전 snap 교체

**Files:** Modify `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPMovementManager.cs`

- [ ] **Step 1: 클라 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" checkout -b feature/netcode-phase1-rotation-snap
```
(현재 클라는 `feature/netcode-phase1-rotation-snap` 브랜치에 이미 spec 커밋이 있을 수 있음 — 그 경우 `checkout`만, 새 브랜치 생성 불필요. dirty: 세션 시작 시점 `UIRoot.prefab`/`PackageManagerSettings.asset` 등 무관 변경은 그대로 둠.)

- [ ] **Step 2: 회전 블록 교체** — `LOPMovementManager.cs`의 이동 블록 안 회전 4줄:
```csharp
                // Rotate
                float myFloat = 0;
                var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
                entity.rotation = new Vector3(0, smooth, 0);
```
→ 다음으로 교체:
```csharp
                // Rotate (deterministic snap — facing은 cosmetic, 부드러운 연출은 뷰/Stage④)
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                entity.rotation = new Vector3(0, angle, 0);
```
(이동 블록 `if (direction.sqrMagnitude > 0)` 안 위치 유지 — 입력 없을 때 facing 보존.)

- [ ] **Step 3: 클라 컴파일 0에러**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")`
- `read_console(action="get", types=["error"], unity_instance="<클라 id>")` → 0 errors.

- [ ] **Step 4: 클라 커밋** (픽스처/무관 dirty 제외, 이 파일만 stage)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/LOPMovementManager.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "fix(netcode): deterministic rotation snap in LOPMovementManager (remove buggy SmoothDampAngle)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 5:** `git -C "..." show --stat HEAD | head -6` — `LOPMovementManager.cs` 1파일만. 무관 파일 포함되면 중단.

---

## Task 2: 서버 동일 교체

**Files:** Modify `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPMovementManager.cs`

- [ ] **Step 1: 서버 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/netcode-phase1-rotation-snap
```
Expected dirty: `LOPGame.cs` + `ConfigureRoomComponent.cs`(픽스처, 미접촉 — 따라옴, stash 안 함). 다른 dirty 있으면 확인.

- [ ] **Step 2: 회전 블록 교체** — Task 1 Step 2와 **완전 동일** 교체:
```csharp
                // Rotate
                float myFloat = 0;
                var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
                entity.rotation = new Vector3(0, smooth, 0);
```
→
```csharp
                // Rotate (deterministic snap — facing은 cosmetic, 부드러운 연출은 뷰/Stage④)
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                entity.rotation = new Vector3(0, angle, 0);
```

- [ ] **Step 3: 서버 컴파일 0에러**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.

- [ ] **Step 4: 서버 커밋** (픽스처 2파일 stage 안 함 — 이 파일만)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Game/LOPMovementManager.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "fix(netcode): deterministic rotation snap in LOPMovementManager (remove buggy SmoothDampAngle)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 5:** `git -C "..." show --stat HEAD | head -6` — `LOPMovementManager.cs` 1파일만. `LOPGame.cs`/`ConfigureRoomComponent.cs` 포함되면 중단.

- [ ] **Step 6: 클·서 파일 동일 확인**
```bash
diff "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LOPMovementManager.cs" \
     "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/LOPMovementManager.cs" && echo "IDENTICAL"
```
Expected: `IDENTICAL`(바이트 동일 유지). 차이 나면 중단.

---

## Task 3: 런타임 수동 검증 (사용자)

클·서 플레이:
1. **이동 중 facing** — 캐릭터가 이동 방향을 향함(이동 방향 바꾸면 즉시 그 방향).
2. **회전 떨림/발산 없음** — 클라·서버 시각상 회전 일치, 떨림 없음.
3. **정지 시 facing 유지** — 입력 멈추면 마지막 facing 유지.
4. **콘솔 에러 0**.

동작 보존(≈즉시 회전)이 곧 성공.

---

## 완료 기준

- [ ] 클·서 각 `feature/netcode-phase1-rotation-snap`에 커밋(각 `LOPMovementManager.cs` 1파일).
- [ ] 클·서 컴파일 0에러. 두 파일 바이트 동일.
- [ ] 서버 픽스처 2파일 working tree 보존(미커밋), 기존 stash 무손상.
- [ ] 수동 플레이: facing/떨림/정지 유지 동작 보존.

이후: 사용자 검증 후 클·서 각 repo `feature/netcode-phase1-rotation-snap` → main `--no-ff` 머지(클라는 spec 커밋도 같은 브랜치라 함께 머지). 서버 머지는 픽스처 미접촉이라 stash 댄스 불필요. netcode Phase 1 완료. 후속 = Phase 2 clock sync 등(각자 별도) / 부드러운 facing은 Stage④ 뷰-pull.
