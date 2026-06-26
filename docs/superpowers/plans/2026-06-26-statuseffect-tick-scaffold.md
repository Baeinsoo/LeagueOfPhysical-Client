# StatusEffect 틱 스캐폴드 Implementation Plan (Phase 3a)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 신 World 시스템을 *처음으로 게임 틱에 배선*한다 — (1) 엔티티 생성 시 `Abilities`+`StatusEffects` 컴포넌트를 부여하고, (2) `LOPWorld`가 매 틱 `StatusEffectSystem.Tick`(만료 구동)을 돌린다. **발동·적용·Luban 없음** → 효과가 아직 아무 데서도 적용되지 않아 **거동 변화 0**. 하지만 `world.Tick`이 처음으로 실제 일을 한다(4c 이정표). 

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 3 = 컷오버/통합, 그 첫 슬라이스)
**선행:** Phase 1(StatusEffect 코어)·Phase 2(Ability 코어) main 머지 완료.

**Architecture:** LOP-Shared `LOPWorld`에 `StatusEffectSystem` 주입 + `Mutation` override로 만료 sweep. 클·서 `GameLifetimeScope`에 `StatusEffectSystem` 등록 + `CharacterCreator`에서 컴포넌트 부여. `world.Tick` 호출부(LOPRunner)는 무변경 — sweep이 기존 `world.Tick`을 타고 흐름.

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 컴파일·플레이 검증).

---

## 레포 / 도구 참조
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared` (`LOPWorld` + 테스트)
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` (`GameLifetimeScope` + `CharacterCreator` + 문서)
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` (`GameLifetimeScope` + `CharacterCreator`)
- **GameFramework:** 이번 Phase **무변경**.
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 `mcpforunity://instances` 재확인. 모든 호출에 `unity_instance` 명시.

**⚠️ EOL:** mass `sed` 금지. 신규=Write, 편집=Edit.
**.meta:** 신규 `.cs`는 `refresh_unity`가 `.meta` 생성. `.cs`+`.cs.meta` 함께 커밋.
**픽스처 보존:** `git add -A`/`commit -am` 금지. Task 명시 파일만 add. (Client/Server 픽스처: Room.unity/ProjectSettings/Art/ConfigureRoomComponent 등 커밋 금지.)
**컨벤션:** Microsoft C#. LOP 파일은 World 타입을 풀 네임스페이스(`GameFramework.World.X`)로, LOP 타입(`Abilities`/`StatusEffects`/`StatusEffectSystem`)은 `namespace LOP`라 단축.

---

## 확정된 설계 결정

1. **틱 구동 위치 = `LOPWorld.Mutation` override.** 상태이상 만료는 시간기반 상태정리(mutation). `world.Tick`(Collection→Mutation→Detection) 중 Mutation에서 `EntityRegistry.All` sweep. → `world.Tick`이 처음 실내용물을 가짐(4c 목표). 호스트(LOPRunner) 무변경.
2. **`LOPWorld` 생성자에 `StatusEffectSystem` 추가** — VContainer가 `Register<IWorld, LOPWorld>` 시 ctor 자동 주입(StatusEffectSystem만 등록하면 해소).
3. **sweep = 직접 foreach(`EntityRegistry.All`), ToList 없음** — `StatusEffectSystem.Tick`은 효과 리스트만 만지고 레지스트리를 변경하지 않음 → 열거 중 안전. `StatusEffects` 없는 엔티티(아이템 등)는 `Tick` 내부 가드로 no-op.
4. **3a는 `StatusEffectSystem`만 등록·구동.** `AbilitySystem`은 서버에 `ManaSystem` 미등록이라 3a 제외 — 발동 컷오버(3d)에서 `AbilitySystem`+서버 `ManaSystem` 함께. (단 컴포넌트 `Abilities`는 이번에 부여 — 빈 컨테이너, 3d까지 inert. CharacterCreator 1회 터치.)
5. **클·서 양쪽 sweep** — `LOPWorld` 공유라 양 runner의 `world.Tick`이 다 sweep. 발동이 없어 양쪽 다 빈 리스트 inert. *클라이언트가 서버권위 효과를 자체 만료하면 안 되는* 문제는 효과가 실제 흐르는 3d/3e의 설계로 미룸(지금은 무해).
6. **검증** — LOPWorld의 만료 sweep을 **EditMode 단위 테스트**(순수 C#). DI/생성/배선은 컴파일 + 플레이 무회귀로 검증.

---

## Task 0: 피처 브랜치 (LOP-Shared / Client / Server)
- [ ] **Step 1: working tree 확인** (모두 main, 픽스처 외 깨끗)
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; echo "branch: $(git -C C:/Users/re5na/workspace/LOP/$r branch --show-current)"; done
```
Expected: LOP-Shared 깨끗(main); Client 픽스처 + 이번 plan(untracked); Server 픽스처. 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/statuseffect-tick-scaffold; done
```

---

## Task 1: LOP-Shared — `LOPWorld`가 StatusEffect 만료 구동

- [ ] **Step 1: `Runtime/Scripts/Game/LOPWorld.cs` 교체** (Edit — 전체 본문)
```csharp
namespace LOP
{
    public class LOPWorld : GameFramework.World.WorldBase
    {
        private readonly StatusEffectSystem _statusEffectSystem;

        public LOPWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            StatusEffectSystem statusEffectSystem)
            : base(entityRegistry, eventBuffer)
        {
            _statusEffectSystem = statusEffectSystem;
        }

        // 4c 3a: 상태이상 만료를 매 틱 구동 — world.Tick의 첫 실내용물. 발동/적용/캐스케이드는 후속 슬라이스.
        protected override void Mutation(long tick, float deltaTime)
        {
            foreach (var entity in EntityRegistry.All)
            {
                _statusEffectSystem.Tick(entity, tick);
            }
        }
    }
}
```
> 기존 ctor `(EntityRegistry, WorldEventBuffer)` → `+StatusEffectSystem`. `StatusEffectSystem`은 `namespace LOP`라 단축. `EntityRegistry.All`은 `WorldBase`가 노출(`EntityRegistry` 프로퍼티).

- [ ] **Step 2: 컴파일 검증** — 클·서 `refresh_unity`(scope=all, force) → `read_console`(error). **이 시점엔 클·서 DI가 새 ctor를 못 채워 에러 가능**(StatusEffectSystem 미등록) — Task 3·4 후 해소. LOP-Shared 자체 컴파일(타입) 에러만 0인지 확인하고, DI/사용처 에러는 Task 3·4에서 닫는다.

---

## Task 2: LOP-Shared — `LOPWorld` 만료 EditMode 테스트

- [ ] **Step 1: `Tests/EditMode/LOPWorldTests.cs` 생성** (Write)
```csharp
using GameFramework.World;
using LOP;
using NUnit.Framework;

namespace LOP.Tests
{
    public class LOPWorldTests
    {
        [Test]
        public void Tick_ExpiresDurationEffect_ViaMutationSweep()
        {
            var registry = new EntityRegistry();
            var buffer = new WorldEventBuffer();
            var statusEffects = new StatusEffectSystem(new StatsSystem());
            var world = new LOPWorld(registry, buffer, statusEffects);

            var entity = new Entity("e1");
            entity.Add(new Stats());
            entity.Add(new StatusEffects());
            registry.Add(entity);

            // 5틱 지속 효과(모디파이어 없음 — 만료 경로만) 적용
            statusEffects.Apply(entity,
                new StatusEffectData(1, DurationPolicy.Duration, 5, null, StatusStackPolicy.Refresh, 1),
                "src", 0);
            Assert.That(entity.Get<StatusEffects>().Effects.Count, Is.EqualTo(1));

            world.Tick(4, 0.05f);   // 아직 만료 전
            Assert.That(entity.Get<StatusEffects>().Effects.Count, Is.EqualTo(1));

            world.Tick(5, 0.05f);   // Mutation sweep → 만료
            Assert.That(entity.Get<StatusEffects>().Effects.Count, Is.EqualTo(0));
        }

        [Test]
        public void Tick_EntityWithoutStatusEffects_NoThrow()
        {
            var registry = new EntityRegistry();
            var world = new LOPWorld(registry, new WorldEventBuffer(), new StatusEffectSystem(new StatsSystem()));
            registry.Add(new Entity("bare"));   // StatusEffects 없음

            Assert.DoesNotThrow(() => world.Tick(1, 0.05f));   // 가드로 no-op
        }
    }
}
```
> `WorldEventBuffer` 파라미터리스 ctor 가정(아니면 plan 실행 시 시그니처 확인 후 보정). 테스트는 Mutation sweep이 만료를 구동함을 검증.

---

## Task 3: Client — `StatusEffectSystem` 등록 + 컴포넌트 부여

- [ ] **Step 1: `Assets/Scripts/Game/GameLifetimeScope.cs` — `StatusEffectSystem` 등록** (Edit). `StatsSystem`(L34) 다음 줄에:
```csharp
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
```
> `Register<IWorld, LOPWorld>`(L30)가 새 ctor의 `StatusEffectSystem`을 이 등록으로 자동 주입. (`StatusEffectSystem`은 `namespace LOP`라 단축 — 파일이 `namespace LOP`. 모호하면 `LOP.StatusEffectSystem`.)

- [ ] **Step 2: `Assets/Scripts/EntityCreator/CharacterCreator.cs` — 컴포넌트 부여** (Edit). `entityRegistry.Add(worldEntity);`(L93) **직전**에:
```csharp
            worldEntity.Add(new Abilities());        // 3d까지 빈 컨테이너(inert)
            worldEntity.Add(new StatusEffects());
```
> `Abilities`/`StatusEffects`는 `namespace LOP`. (파일이 `namespace LOP`면 단축, 아니면 `LOP.` 한정.)

- [ ] **Step 3: 컴파일 검증** — Client `refresh_unity` → `read_console`(error) 0.

---

## Task 4: Server — `StatusEffectSystem` 등록 + 컴포넌트 부여 (클라와 동형)

- [ ] **Step 1: `Assets/Scripts/Game/GameLifetimeScope.cs` — `StatusEffectSystem` 등록** (Edit). `StatsSystem`(L24) 다음 줄에:
```csharp
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
```
> 서버는 `StatusEffectSystem` ctor 의존 `StatsSystem`(L24) 보유 → 해소 OK. (`ManaSystem`은 서버 미등록이나 StatusEffectSystem은 불필요.)

- [ ] **Step 2: `Assets/Scripts/EntityCreator/CharacterCreator.cs` — 컴포넌트 부여** (Edit). `entityRegistry.Add(worldEntity);`(L85) **직전**(Ownership 블록 뒤)에:
```csharp
            worldEntity.Add(new Abilities());        // 3d까지 빈 컨테이너(inert)
            worldEntity.Add(new StatusEffects());
```

- [ ] **Step 3: 컴파일 검증** — Server `refresh_unity` → `read_console`(error) 0.

---

## Task 5: 종합 컴파일 + 테스트
- [ ] **Step 1: 양쪽 재스캔** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) **0**. (Task 1의 DI 에러가 Task 3·4 등록으로 닫혔는지 확인.)
- [ ] **Step 2: EditMode 테스트** — `run_tests`(assembly `baegames.LOP.Shared.Tests.EditMode`) green (LOPWorld 2 + Ability 7 + StatusEffect 7 + Movement 4 = 20).
- [ ] **Step 3: 신규 .meta 확인** — LOP-Shared `LOPWorldTests.cs.meta`.

---

## Task 6: 커밋 (repo별)
- [ ] **Step 1: LOP-Shared** — `git add Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/LOPWorldTests.cs*`
```
feat(world): LOPWorld drives StatusEffect expiry each tick (Phase 3a)

LOPWorld.Mutation sweeps EntityRegistry calling StatusEffectSystem.Tick
— world.Tick now does real work (first 4c content). Ctor takes
StatusEffectSystem. No activation/application yet (behavior unchanged).
EditMode test verifies expiry via the Mutation sweep.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: Client** — `git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/EntityCreator/CharacterCreator.cs` (픽스처 제외)
```
feat(world): register StatusEffectSystem + grant Abilities/StatusEffects (Phase 3a)

Register StatusEffectSystem (resolves LOPWorld's new ctor dep). Grant
Abilities (inert until activation) + StatusEffects components at entity
creation. No behavior change yet.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: Server** — Client Step 2와 동형. 변경 파일만(픽스처 제외).
- [ ] **Step 4: Client 문서** — `git add docs/superpowers/plans/2026-06-26-statuseffect-tick-scaffold.md`

---

## Task 7: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0. 테스트 green.
- [ ] **Step 2: 플레이 무회귀(사용자)** — 입장→플레이→종료, 이동/점프/전투/스폰/디스폰 *이전과 동일*(효과 미적용이라 거동 변화 없어야 함). NRE/예외 0.
- [ ] **Step 3: 머지/푸시(사용자 요청 시)** — LOP-Shared → Client → Server 순 `--no-ff`. 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지(3a):** 틱 구동=Task1 / 테스트=Task2 / 클 등록·부여=Task3 / 서 등록·부여=Task4 / 검증=Task5. ✅
- **거동 보존:** 효과를 적용하는 코드가 아직 없음(발동 미배선) → sweep이 빈 리스트 inert. 컴포넌트 부여는 데이터만. 런타임 거동 0. ✅
- **4c 이정표:** `world.Tick`이 no-op → 첫 실내용물(StatusEffect 만료 sweep). ✅
- **DI 정합:** `Register<IWorld, LOPWorld>` 자동 ctor 주입 + `StatusEffectSystem` 등록(의존 StatsSystem 양쪽 보유). 서버 ManaSystem 부재 무관(AbilitySystem 미등록). ✅
- **열거 안전:** `StatusEffectSystem.Tick`이 레지스트리 비변경 → `EntityRegistry.All` 직접 foreach 안전. 컴포넌트 없는 엔티티는 가드 no-op. ✅
- **범위 정직:** Ability 발동·Luban·MoveSpeed·와이어·뷰·레거시 은퇴 = 3b~3f. Abilities 컴포넌트는 부여만(inert). 클라 서버권위 만료 문제=3d/3e. ✅
- **EOL/.meta/픽스처:** Write=신규/Edit=기존, mass sed 없음, .cs+.meta, 픽스처 제외. ✅
```
