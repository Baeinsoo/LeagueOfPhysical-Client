# World Core 뷰 이행 Slice A — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `CharacterNameplate`가 HP 초기값을 레거시 `HealthComponent`(MonoBehaviour) 대신 순수 C# `World.Health`에서 pull하도록 바꿔, "뷰가 `World.Entity`를 통해 코어를 직접 읽는" Model A 소비 패턴을 박제한다.

**Architecture:** Model A — 코어(`GameFramework.World`)는 순수 C# 데이터, 뷰는 `EntityRegistry.Get(id)`로 `World.Entity`를 해석해 컴포넌트를 직접 읽는다. 동작 변화 없음(두 소스의 초기값이 동일) — 의존 방향을 레거시→코어로 돌린 *템플릿*이 산출물이다.

**Tech Stack:** Unity (C#, Assembly-CSharp 단일), VContainer(`[Inject]`), GameFramework.World, UnityMCP(컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-16-world-core-view-slice-a-design.md`

**Branch:** `feature/world-core-view-slice-a` (현재 체크아웃됨 — 새 브랜치 생성 불필요)

---

## File Structure

수정 1파일(생성/삭제 없음 → `.meta` 변경 없음):

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs` | `EntityRegistry` 주입, `Start`의 HP 초기값 읽기를 `HealthComponent` → `World.Health`로, doc 주석 정정 |

**World 타입 네이밍 컨벤션:** 이 파일은 `using GameFramework.World;`를 추가하지 않고 World 타입을 **풀 네임스페이스로 한정**한다(`GameFramework.World.EntityRegistry` 등). `UnityEngine.Component`와의 모호성 회피 — `world-core-connection-architecture.md` 컨벤션.

---

## Verification Procedure (Task 1의 "compile 검증" 단계에서 수행)

> 이 프로젝트는 **클라이언트**다. 서버/클라 에디터가 동시에 붙어 있으니 **모든 UnityMCP 호출에 `unity_instance`를 클라로 명시**(CLAUDE.md). UnityMCP 툴은 deferred — 먼저 로드: `ToolSearch("select:refresh_unity,read_console")`.

1. 클라 인스턴스 id 해석: 리소스 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`의 full `id`(`Name@hash`). (작성 시점 예 `LeagueOfPhysical-Client@de70658b9450cbb4` — hash는 바뀔 수 있으니 매번 조회.)
2. `refresh_unity(unity_instance="<클라 id>", mode="force", scope="scripts", compile="request", wait_for_ready=true)`.
3. `read_console(unity_instance="<클라 id>", action="get", types=["error"], count="40", format="plain")`.
   - **기대:** `CharacterNameplate` 관련 컴파일 에러 **0건**.

---

## Task 1: `CharacterNameplate`의 HP 초기값을 `World.Health`에서 pull

**Files:**
- Modify: `Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs`

- [ ] **Step 1: `using VContainer;` 추가** (`[Inject]` 어트리뷰트에 필요)

Replace:
```csharp
using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using UnityEngine.UIElements;
```
With:
```csharp
using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
```

- [ ] **Step 2: `EntityRegistry` 주입 필드 추가** (클래스 본문 맨 위)

Replace:
```csharp
    public class CharacterNameplate : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }
```
With:
```csharp
    public class CharacterNameplate : MonoBehaviour, ICleanup
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPEntity entity { get; private set; }
```

- [ ] **Step 3: `Start`의 HP 초기값 읽기를 레거시 `HealthComponent` → `World.Health`로 교체**

Replace:
```csharp
            HealthComponent health = entity.GetEntityComponent<HealthComponent>();
            _maxHp = health != null ? health.maxHP : 1;
            _currentHp = health != null ? health.currentHP : _maxHp;
```
With:
```csharp
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            _maxHp = health != null ? health.Max : 1;
            _currentHp = health != null ? health.Current : _maxHp;
```

(근거: `EntityRegistry.Get(id)` → `Entity` 또는 null; `Entity.Get<T>()` → `T` 또는 null; `Health.Max`/`Health.Current`는 int 프로퍼티. 미등록/미보유 시 기존과 동일한 폴백(`_maxHp = 1`).)

- [ ] **Step 4: doc 주석 정정** (정확성 — 레거시 표기 제거)

Replace:
```csharp
    /// maxHP/초기값은 스폰 시 초기화된 HealthComponent에서 읽는다.
```
With:
```csharp
    /// maxHP/초기값은 World.Entity의 World.Health(순수 C# 코어)에서 읽는다(Model A — 뷰가 코어를 pull).
```

- [ ] **Step 5: compile 검증** — 위 "Verification Procedure" 수행. 기대: 새 에러 0건.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs
git commit -m "$(cat <<'EOF'
refactor(world): CharacterNameplate reads HP from World.Health (Model A pull)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 런타임 플레이 검증 (수동 — 사용자)

자동화 테스트 없음(단일 Assembly-CSharp). 동작은 이전과 동일해야 하며(회귀 없음 = 성공), 사용자가 플레이로 확인한다.

- [ ] **Step 1: 클라 Play 진입**, 룸 연결 → 게임 씬 진행.
- [ ] **Step 2: 확인**
  - 캐릭터 머리 위 **네임플레이트 HP바가 초기값(가득 또는 현재 HP 비율)** 으로 정상 표시 — `World.Health.Max/Current`에서 읽힘
  - **피격 시 HP바 감소** 정상 (`EntityDamage` 갱신 경로 — 변경 없음)
  - 스폰/디스폰 정상, 콘솔 에러 0
- [ ] **Step 3: 회귀 확인** — 변경 전과 시각적으로 동일하면 통과(값 출처만 레거시→코어로 바뀜).

> 통과하면 Slice A 완료. 후속(별도 spec): Slice B(Motion/물리 DIP), Slice C(reconciler/netcode), 스포너 view-resolver 완전체 + 레거시 컴포넌트 제거.

---

## 진행

- [ ] Task 1 — Nameplate HP 읽기를 World.Health로
- [ ] Task 2 — 런타임 플레이 검증(수동)
