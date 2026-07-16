# 캐릭터 소프트 분리 (BOTW식) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 정면으로 붙은 캐릭터가 완전히 잠기는(wedge) 버그를 없애고, 키네마틱 컨트롤러 위에서 "몸으로 공간을 차지하되 부드럽게 밀려 비껴나가는"(젤다 BOTW식) 캐릭터 간 충돌로 바꾼다.

**Architecture:** 캐릭터를 전용 "Character" 레이어로 옮겨 이동 sweep에서 자동 제외(→ 벽으로 안 봄, wedge 소멸)하고, 그 위에 공유 `MotionBridge`의 디펜(depenetration) 패스를 2개로 나눠(지형 full / 캐릭터 reciprocal) 소프트 상호 밀어냄을 넣는다. 분리 로직은 공유 concrete 한 벌이고 클·서 차이는 DI 주입 상수(배율)뿐.

**Tech Stack:** Unity (kinematic character controller), C#, VContainer(DI), NUnit(EditMode). 4개 저장소: GameFramework(포트), LeagueOfPhysical-Shared(공유 시뮬), LeagueOfPhysical-Client, LeagueOfPhysical-Server.

**Design spec:** `docs/superpowers/specs/2026-07-16-character-soft-separation-design.md`

## Global Constraints

- **4개 저장소**를 건드린다: `GameFramework`, `LeagueOfPhysical-Shared`, `LeagueOfPhysical-Client`, `LeagueOfPhysical-Server`. 각 repo에서 **`feature/character-soft-separation` 브랜치**에 작업(main 직접 커밋 금지). GameFramework/Shared는 `file:` 패키지라 클·서 양쪽에 반영됨.
- **커밋은 repo별로** 분리(한 repo = 한 커밋). 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **World 타입은 항상 풀 네임스페이스 한정** — LOP 측 파일은 `using GameFramework.World;`를 추가하지 않는다(`GameFramework.World.Entity` 등). `UnityEngine.Component`와 이름 충돌 회피.
- **"Character" 레이어 = 인덱스 8** — 클·서 `ProjectSettings/TagManager.asset` **동일 인덱스**로 추가(첫 빈 유저 레이어). 인덱스가 같아야 `GetMask`/직렬화 일관.
- **`separationScale` = 클라 1.0 / 서버 0.5** (per-side DI 상수).
- **이동 sweep 마스크는 "Default" 그대로 둔다** — `KinematicMoveSystem` 등록(GameLifetimeScope 52-53줄)은 **건드리지 않는다**. 캐릭터가 Default를 떠나면 자동 제외됨.
- **충돌 매트릭스(`DynamicsManager.asset`) 미편집** — 명시 마스크 쿼리라 무관.
- **UnityMCP 타깃팅:** 이 프로젝트는 클라. 모든 UnityMCP 호출에 `unity_instance`를 명시(클라 id는 `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client`로 해석). 서버 검증은 서버 인스턴스 id로.
- 패키지 코드(GameFramework/Shared) 수정 후 컴파일 확인은 `refresh_unity`(scope 넓게) + `read_console`로.

---

## Task 0: 4개 저장소에 피처 브랜치 준비

**Files:** (git 브랜치만)

- [ ] **Step 1: 각 repo에 브랜치 생성** (클라는 이미 생성됨 — 확인만)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git branch --show-current   # feature/character-soft-separation 확인
cd /c/Users/re5na/workspace/LOP/GameFramework && git checkout -b feature/character-soft-separation
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared && git checkout -b feature/character-soft-separation
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git checkout -b feature/character-soft-separation
```

Expected: 네 repo 모두 `feature/character-soft-separation` 브랜치. (GameFramework/Shared가 이미 다른 브랜치면 상황 파악 후 사용자에게 확인.)

---

## Task 1: "Character" 레이어 도입 + 캡슐 배치 + 전투 마스크 재조준

**목표 상태(리뷰 게이트):** 캐릭터가 서로 **통과**(아직 분리 없음)하고 **wedge 소멸**, 공격은 **여전히 명중**, 스폰 flush·지형 통과 회귀 없음. (이 시점 = 옵션 A 상태. Task 2가 소프트 분리를 얹음.)

**Files:**
- Modify: `LeagueOfPhysical-Client/ProjectSettings/TagManager.asset` (index 8 = "Character")
- Modify: `LeagueOfPhysical-Server/ProjectSettings/TagManager.asset` (index 8 = "Character")
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Component/PhysicsComponent.cs` (레이어 지정 1줄)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Component/PhysicsComponent.cs` (레이어 지정 1줄)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs:15` (전투 마스크)

**Interfaces:**
- Produces: `"Character"` 레이어(둘 다 index 8). 캐릭터 런타임 캡슐(`physicsGameObject`)이 이 레이어에 놓임.

- [ ] **Step 1: 클라 Unity에서 "Character" 레이어 추가 (index 8)**

Unity 에디터(클라 인스턴스): **Edit ▸ Project Settings ▸ Tags and Layers**에서 **User Layer 8**에 `Character` 입력.
(또는 UnityMCP `manage_editor`로 레이어 추가 — `unity_instance`=클라.)

검증: `LeagueOfPhysical-Client/ProjectSettings/TagManager.asset`의 `layers:` 목록 9번째 항목(index 8)이 `- Character`로 바뀜.

- [ ] **Step 2: 서버 Unity에서 "Character" 레이어 추가 (index 8, 클라와 동일)**

서버 Unity 에디터에서 동일하게 **User Layer 8** = `Character`.

검증: `LeagueOfPhysical-Server/ProjectSettings/TagManager.asset` index 8 = `Character`. **클라와 인덱스 동일** 확인.

- [ ] **Step 3: 클라 PhysicsComponent에서 캡슐을 Character 레이어로**

`LeagueOfPhysical-Client/Assets/Scripts/Component/PhysicsComponent.cs` — `physicsGameObject = physics.CreateChild("PhysicsGameObject");` 다음 줄에 추가:

```csharp
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");
```

- [ ] **Step 4: 서버 PhysicsComponent에서 캡슐을 Character 레이어로**

`LeagueOfPhysical-Server/Assets/Scripts/Component/PhysicsComponent.cs` — 동일 편집(같은 `CreateChild` 줄 다음):

```csharp
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");
```

- [ ] **Step 5: 서버 전투 마스크를 Character로 재조준**

`LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs:15`:

```csharp
            LayerMask layerMask = LayerMask.GetMask("Character");
```

(변경 전: `LayerMask.GetMask("Default")`. 캐릭터가 Default를 떠났으니 이걸 안 바꾸면 공격이 아무도 못 맞힌다.)

- [ ] **Step 6: 양쪽 컴파일 확인**

UnityMCP `refresh_unity` + `read_console`(클라·서버 인스턴스 각각, `unity_instance` 명시). Expected: 컴파일 에러 0.

- [ ] **Step 7: In-play 검증**

클라+서버 Play. 두 캐릭터를 정면으로 마주 걸어붙임.
- Expected: **wedge 없이 서로 통과**(겹쳐 지나감 — 아직 분리 없음이 정상).
- 공격 발동 → **여전히 명중**(데미지 뜸). 전투 마스크 회귀 확인.
- 스폰 시 지면 밑으로 안 빠짐(스폰 flush 정상), 지형 벽 통과 안 함.

- [ ] **Step 8: repo별 커밋** (클라 / 서버 두 커밋)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add ProjectSettings/TagManager.asset Assets/Scripts/Component/PhysicsComponent.cs
git commit -m "feat: 캐릭터 캡슐을 전용 Character 레이어로 — sweep 자동 제외로 wedge 소멸

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add ProjectSettings/TagManager.asset Assets/Scripts/Component/PhysicsComponent.cs Assets/Scripts/Game/LOPOverlapQuery.cs
git commit -m "feat: 캐릭터 Character 레이어 배치 + 전투 OverlapSphere 마스크 재조준(Default→Character)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 소프트 상호 분리 (디펜 2패스 + per-side 배율)

**목표 상태(리뷰 게이트):** 캐릭터가 서로 **통과하지 않고**(공간 차지) **부드럽게 밀려 비껴남**(wedge 없음, 진동 없음). 몹 클러스터가 서로 겹치지 않고 퍼짐. 전투·스폰 flush 회귀 없음.

**Files:**
- Modify: `GameFramework/Runtime/Scripts/World/IMotionBridge.cs` (`Separate` 시그니처 추가)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionBridge.cs` (생성자 + 2패스)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs:65-73` (`Separate` 호출)
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs` (SpyBridge에 Separate + 새 테스트)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:54` (배율 1.0 주입)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs:54` (배율 0.5 주입)

**Interfaces:**
- Consumes: `KinematicDepenetration.ComputePushOut(CapsuleCollider own, int layerMask)` → `UnityEngine.Vector3`(합산 push, 겹침 없으면 zero) — GameFramework, 무수정.
- Consumes: `PhysicsBody` 컴포넌트의 `Collider`(CapsuleCollider), `GameFramework.World.Transform.Position`(System.Numerics.Vector3).
- Produces: `IMotionBridge.Separate(GameFramework.World.Entity entity)` — 캐릭터 마스크 겹침을 `separationScale` 배율로 밀어냄.
- Produces: `MotionBridge(int envMask, int charMask, float separationScale)` 생성자.

### 2a — 플러밍(포트+구현+배선, 컴파일 유지, 아직 미호출)

- [ ] **Step 1: IMotionBridge에 Separate 추가**

`GameFramework/Runtime/Scripts/World/IMotionBridge.cs` — `Depenetrate`와 `PushMotion` 사이에 추가:

```csharp
    public interface IMotionBridge
    {
        void SyncTransforms();
        void Depenetrate(Entity entity);
        void Separate(Entity entity);
        void PushMotion(Entity entity);
    }
```

XML 주석의 페이즈 설명도 갱신(선택): `... → Depenetrate → Separate → (KinematicMoveSystem) → PushMotion`.

- [ ] **Step 2: MotionBridge를 2패스 + 생성자 주입으로 교체**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionBridge.cs` — 하드코딩 `_layerMask`를 생성자 주입 3개로 바꾸고 `Depenetrate`(지형 full)/`Separate`(캐릭터 scaled)를 공통 헬퍼로:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World 모션을 Unity 물리 바디에 반영하는 공유 브릿지(포트 구현 1개 — UnityCollisionQuery와 동형).
    /// 겹침 해소는 2패스: Depenetrate(지형 full) + Separate(캐릭터 reciprocal, per-side 배율).
    /// World.Transform이 진실원본, Rigidbody는 팔로워(kinematic이면 위치·회전 직접 밀어넣음).
    /// </summary>
    public class MotionBridge : GameFramework.World.IMotionBridge
    {
        private readonly int _envMask;
        private readonly int _charMask;
        private readonly float _separationScale;

        public MotionBridge(int envMask, int charMask, float separationScale)
        {
            _envMask = envMask;
            _charMask = charMask;
            _separationScale = separationScale;
        }

        public void SyncTransforms() => Physics.SyncTransforms();

        public void Depenetrate(GameFramework.World.Entity entity)
        {
            // 지형 겹침(스폰 flush 등)에서 캡슐을 밖으로 — 지면은 안 움직이니 전부 해소.
            ApplyPushOut(entity, _envMask, 1f);
        }

        public void Separate(GameFramework.World.Entity entity)
        {
            // 캐릭터끼리 부드럽게 밀어냄. 배율=per-side(서버 0.5 상호분리 / 클라 1.0 내가 다 빠짐).
            ApplyPushOut(entity, _charMask, _separationScale);
        }

        private void ApplyPushOut(GameFramework.World.Entity entity, int layerMask, float scale)
        {
            var body = entity.Get<PhysicsBody>();
            var transform = entity.Get<GameFramework.World.Transform>();
            if (body == null || body.Collider == null || transform == null)
            {
                return;
            }
            Vector3 push = KinematicDepenetration.ComputePushOut(body.Collider, layerMask);
            if (push.sqrMagnitude > 0f)
            {
                transform.Position = (transform.Position.ToUnity() + push * scale).ToNumerics();
            }
        }

        public void PushMotion(GameFramework.World.Entity entity)
        {
            var body = entity.Get<PhysicsBody>();
            var transform = entity.Get<GameFramework.World.Transform>();
            if (body == null || body.Rigidbody == null || transform == null)
            {
                return;
            }
            Rigidbody rb = body.Rigidbody;
            if (rb.isKinematic)
            {
                rb.position = transform.Position.ToUnity();
                rb.rotation = transform.Rotation.ToUnity();
                return;
            }
            var velocity = entity.Get<GameFramework.World.Velocity>();
            if (velocity != null)
            {
                rb.linearVelocity = velocity.Linear.ToUnity();
            }
            rb.rotation = transform.Rotation.ToUnity();
        }
    }
}
```

- [ ] **Step 3: SpyBridge(테스트 페이크)에 Separate 구현 추가**

`LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs`의 `SpyBridge`(23-31줄)에 `separated` 기록 + `Separate` 추가:

```csharp
        private class SpyBridge : GameFramework.World.IMotionBridge
        {
            public int syncCount;
            public readonly System.Collections.Generic.List<string> depenetrated = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> separated = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> pushed = new System.Collections.Generic.List<string>();
            public void SyncTransforms() => syncCount++;
            public void Depenetrate(GameFramework.World.Entity e) => depenetrated.Add(e.Id);
            public void Separate(GameFramework.World.Entity e) => separated.Add(e.Id);
            public void PushMotion(GameFramework.World.Entity e) => pushed.Add(e.Id);
        }
```

- [ ] **Step 4: 클라 GameLifetimeScope — 배율 1.0으로 주입**

`LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:54`:

```csharp
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
```

(변경 전: `builder.Register<GameFramework.World.IMotionBridge, MotionBridge>(Lifetime.Singleton);`)

- [ ] **Step 5: 서버 GameLifetimeScope — 배율 0.5로 주입**

`LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs:54`:

```csharp
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 0.5f), Lifetime.Singleton);
```

- [ ] **Step 6: 컴파일 확인**

`refresh_unity` + `read_console`(클라·서버 각각). Expected: 컴파일 에러 0. (이 시점 MotionBridge.Separate는 존재하지만 LOPWorld가 아직 호출 안 함 → 동작은 Task 1과 동일=통과.)

- [ ] **Step 7: 기존 EditMode 스위트 green 확인**

UnityMCP `run_tests`(EditMode, 클라 인스턴스 — LOP-Shared 테스트 포함). Expected: 기존 테스트 전부 pass(SpyBridge 인터페이스 충족 확인).

- [ ] **Step 8: repo별 커밋** (GameFramework / Shared / 클라 / 서버)

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/World/IMotionBridge.cs
git commit -m "feat: IMotionBridge.Separate 포트 추가(캐릭터 소프트 분리 패스)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/MotionBridge.cs Tests/EditMode/LOPWorldTests.cs
git commit -m "feat: MotionBridge 디펜 2패스(지형 full + 캐릭터 scaled) + per-side 배율 주입

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat: MotionBridge 배율 1.0 주입(클라=내 캐릭이 전부 빠져나옴)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat: MotionBridge 배율 0.5 주입(서버=상호 반씩 분리)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### 2b — TDD: LOPWorld가 Separate를 호출

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs`에 추가(첫 테스트 `Tick_RunsKinematicPhase_ForSimulatedEntities` 아래):

```csharp
        [Test]
        public void Tick_RunsSeparatePhase_ForSimulatedEntities()
        {
            var registry = new EntityRegistry();
            var bridge = new SpyBridge();
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                new AbilitySystem(new ManaSystem()), new StatusEffectSystem(new StatsSystem()),
                new AbilityEffectExecutor(null), new KinematicMoveSystem(new FakeQuery(), ~0), bridge);

            var entity = new Entity("e1");
            entity.Add(new Simulated());
            registry.Add(entity);

            world.Tick(0, 0.05f);

            Assert.That(bridge.separated, Does.Contain("e1"), "Simulated 엔티티마다 Separate 호출");
        }
```

- [ ] **Step 2: 실패 확인**

UnityMCP `run_tests`(EditMode, `Tick_RunsSeparatePhase_ForSimulatedEntities`). Expected: **FAIL** — `bridge.separated`가 비어 있음(LOPWorld가 아직 Separate 미호출).

- [ ] **Step 3: LOPWorld 모션 페이즈에 Separate 추가**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs` 모션 페이즈 루프(65-73줄) — `Depenetrate` 다음에 `Separate`:

```csharp
            _motionBridge.SyncTransforms();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _motionBridge.Depenetrate(entity);
                    _motionBridge.Separate(entity);
                    _kinematicMoveSystem.Tick(entity, deltaTime);
                    _motionBridge.PushMotion(entity);
                }
            }
```

- [ ] **Step 4: 테스트 통과 확인**

`run_tests`(EditMode, 해당 테스트 + 기존 전체). Expected: **PASS** 전부.

- [ ] **Step 5: In-play 검증 (핵심)**

클라+서버 Play:
- 두 캐릭터 정면 접근 → **통과하지 않고 부드럽게 서로 비껴남**(wedge 없음, 앞뒤 진동 없음).
- 몹 여러 마리 한 곳에 스폰(또는 뭉치게) → **서로 안 겹치고 자연스럽게 퍼짐**.
- 내가 가만있는 상대에게 걸어 들어감 → 내가 상대를 스쳐 미끄러짐(클라 배율 1.0 = 내가 빠져나옴), 상대 반응은 서버 스냅으로.
- 공격 명중·스폰 flush·지형 충돌 회귀 없음 재확인.
- (선택 계측) 클러스터 수렴이 이상하면 `ApplyPushOut`에 `Debug.Log(push.magnitude)` 임시 로그 → 확인 후 revert(커밋 X).

- [ ] **Step 6: Shared 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/LOPWorldTests.cs
git commit -m "feat: LOPWorld 모션 페이즈에 Separate 호출 — 소프트 상호 분리 활성 + EditMode 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 마무리 — 문서 갱신 + 브랜치 통합

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md` (파킹 → 완료 이관)
- Modify: 메모리 `character-stuck-debugging-map.md` (③ 해소 반영)

- [ ] **Step 1: ROADMAP 갱신**

`docs/ROADMAP.md` 파킹 표의 "캐릭터끼리 충돌 wedge" 행을 완료 이관(Done 원장에 한 줄 + spec/plan 링크), 파킹에서 제거.

- [ ] **Step 2: 메모리 갱신**

`character-stuck-debugging-map.md`의 "③ 캐릭터끼리 충돌 wedge — 파킹"을 "✅ 수정됨(07-16, 전용 Character 레이어 + 디펜 2패스 소프트 분리)"로 갱신. MEMORY.md 포인터도 반영.

- [ ] **Step 3: ROADMAP/메모리 커밋 (클라 repo)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md
git commit -m "docs: ROADMAP — 캐릭터 소프트 분리 완료 이관(wedge 해소)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 4: 4개 repo main 머지**

각 repo에서 `--no-ff`로 main 머지(사용자 확인 후):

```bash
# 각 repo에서:
git checkout main && git merge --no-ff feature/character-soft-separation
```

(GameFramework/Shared 먼저, 그 다음 클라/서버 — 패키지 의존 순서. 실제 머지는 finishing-a-development-branch 스킬로 사용자 승인 하에.)

---

## Self-Review (작성자 체크)

**Spec coverage:**
- 전용 Character 레이어(§설계1) → Task 1 Step 1-4 ✅
- sweep 제외로 wedge 소멸(§2) → Task 1(레이어 이동만으로 자동) ✅
- 디펜 2패스 + per-side 배율(§3) → Task 2a Step 2,4,5 ✅
- 전투 마스크 재조준(§4) → Task 1 Step 5 ✅
- 결정론/예측(§5) → 클라 배율 1.0 = 내 캐릭 예측(Task 2a Step 4), in-play 검증(2b Step 5) ✅
- 충돌 매트릭스 미편집(§6) → Global Constraints 명시, 어느 태스크도 안 건드림 ✅
- 공유 코드 유지 → 분리 로직 전부 MotionBridge/LOPWorld/KinematicDepenetration(공유), per-side는 주입 상수뿐 ✅
- 테스트(§검증) → EditMode 오케스트레이션 테스트(2b) + in-play 물리 검증(1 Step 7, 2b Step 5) + 기존 스위트 green(2a Step 7) ✅

**Placeholder scan:** 모든 코드 스텝에 실제 코드/명령 포함, TODO/TBD 없음 ✅

**Type consistency:** `Separate(GameFramework.World.Entity)` 시그니처가 IMotionBridge/MotionBridge/SpyBridge/LOPWorld 호출부에서 일치. `MotionBridge(int,int,float)` 생성자가 클·서 등록부와 일치. `ComputePushOut(CapsuleCollider, int)` 재사용(무수정). ✅
