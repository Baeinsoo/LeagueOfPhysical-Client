# 통합 World Tick — Sub-slice A (기반, 양쪽 동작 보존) Implementation Plan

> ✅ **완료·머지됨 (2026-07-13, 4 repo main).** 5개 Task 전부 구현, Shared EditMode 110/110 green, 클·서 컴파일 클린, 플레이 무회귀 확인. 넉백 부채(외력 공통 루프 이전)도 이 슬라이스로 정산(외력 resolve가 `MovementSystem.Tick` 이동 페이즈로 흡수). 다음 = Sub-slice B(물리 브리지 포트·키네마틱 흡수).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Simulated` 마커를 도입하고 `LOPWorld.Tick`이 그것만 순회하게 바꾼 뒤, driveeffects(어빌리티 효과 구동)와 외력(넉백) resolve를 `world.Tick`으로 흡수한다 — **클·서 양쪽 동작 무변경**.

**Architecture:** 시뮬 로직(`LOPWorld`, LOP-Shared)은 공유라 양쪽에 동시 적용된다. 마커 부착(`CharacterCreator`)만 사이드별. Sub-slice A는 양쪽이 *현재 틱하던 셋을 그대로 마킹*(서버=전 캐릭 / 클라=전 캐릭)해 동작을 보존하면서, 이후 슬라이스(B 물리 흡수 / C 클라 scope 축소 + Reconciler)의 기반을 놓는다.

**Tech Stack:** C# / Unity 6000.3 / VContainer DI / NUnit EditMode (LOP-Shared, GameFramework 패키지) / UnityMCP로 컴파일·테스트.

## Global Constraints

- **결정론 = 공유 구체 코드** — 시뮬 로직은 LOP-Shared/GameFramework 구체 클래스를 클·서가 같은 코드로 컴파일. 인터페이스 seam은 I/O 어댑터에만.
- **`*System` = 무상태 DI 인스턴스**, static은 순수 커널만.
- **World 타입 풀 네임스페이스 한정** — LOP 측 파일은 `GameFramework.World.Entity` 등 풀 한정(`UnityEngine.Component` 충돌 회피). `using GameFramework.World;` 추가 금지.
- **`.meta` 커밋** — 새 `.cs`는 Unity가 생성한 `.meta`를 함께 커밋. 직접 만들지 않음.
- **repo별 피처 브랜치** — LOP-Shared / LOP-Server / LOP-Client 각자 `feature/unified-tick-slice-a` 브랜치. main 직접 커밋 금지. 완료 후 `--no-ff` 머지.
- **UnityMCP 타깃 명시** — 매 호출 `unity_instance` 지정. 클라 `LeagueOfPhysical-Client@<hash>`, 서버 `LeagueOfPhysical-Server@<hash>` (hash는 `mcpforunity://instances`로 확인).
- **동작 무변경 = 검증 기준** — 각 태스크 후 Shared EditMode green + 양쪽 컴파일 클린. 최종 서버/클라 플레이 무회귀.

---

## 사전 준비 (태스크 아님)

3개 repo에 브랜치 생성:
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Server LeagueOfPhysical-Client; do
  ( cd "C:/Users/re5na/workspace/LOP/$r" && git checkout -b feature/unified-tick-slice-a )
done
```
> 서버/클라 working tree엔 커밋 금지 로컬 픽스처(`ConfigureRoomComponent.cs`/`GameRuleSystem.cs`/`*.asset`/`UIRoot.prefab`/`Art`)가 있다. **커밋 시 내가 만진 파일만 개별 `git add`.** 절대 `git add -A` 금지.

---

## Task 1: `Simulated` 마커 컴포넌트

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Components/Simulated.cs`

**Interfaces:**
- Produces: `GameFramework.World.Simulated : Component` — 필드 없는 태그. "로컬 시뮬이 이 엔티티의 틱을 소유한다"는 표식. `entity.Add(new Simulated())` / `entity.Has<Simulated>()`.

- [ ] **Step 1: 마커 컴포넌트 작성**

`GameFramework/Runtime/Scripts/World/Components/Simulated.cs`:
```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 로컬 시뮬이 이 엔티티의 틱(이동·어빌리티·상태·효과·물리)을 소유한다는 표식(태그 컴포넌트).
    /// host가 사이드 정책대로 부착: 서버=모든 캐릭터 / 클라=예측하는 내 캐릭. LOPWorld.Tick은 이것만 순회.
    /// 안 붙은 엔티티(클라의 남/NPC)는 시뮬 안 함 = 스냅샷 보간 전용.
    /// </summary>
    public class Simulated : Component
    {
    }
}
```

- [ ] **Step 2: 컴파일 + meta 생성**

UnityMCP(서버 인스턴스로 충분 — GameFramework은 양쪽 공유): `refresh_unity(scope=all, compile=request, unity_instance=서버)` → `read_console(types=[error])` = 0건 기대.

- [ ] **Step 3: 커밋 (GameFramework은 별도 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework" && git checkout -b feature/unified-tick-slice-a
git add Runtime/Scripts/World/Components/Simulated.cs Runtime/Scripts/World/Components/Simulated.cs.meta
git commit -m "feat: Simulated 마커 컴포넌트 (로컬 시뮬 대상 태그)"
```
> GameFramework도 `file:` 패키지라 자체 repo. Unity가 `.meta`를 생성했는지 확인 후 함께 커밋.

---

## Task 2: 양쪽 `CharacterCreator`가 캐릭터에 `Simulated` 부착

마커를 부착하되 `LOPWorld`는 아직 `All`을 순회하므로 **동작 무변경**(마커는 이 시점엔 미사용). 서버=전 캐릭, 클라=전 캐릭(현행 유지).

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/EntityCreator/CharacterCreator.cs:93` (Add(new MotionContributions()) 뒤, `entityRegistry.Add` 앞)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/EntityCreator/CharacterCreator.cs:105` (Add(new MotionContributions()) 뒤, `entityRegistry.Add` 앞)

**Interfaces:**
- Consumes: `GameFramework.World.Simulated` (Task 1)

- [ ] **Step 1: 서버 CharacterCreator에 마커 추가**

서버 `CharacterCreator.cs`에서 `worldEntity.Add(new MotionContributions());` 다음 줄에 삽입:
```csharp
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 캐릭터를 시뮬
```

- [ ] **Step 2: 클라 CharacterCreator에 마커 추가 (Sub-slice A는 전 캐릭 = 현행 보존)**

클라 `CharacterCreator.cs`에서 `worldEntity.Add(new MotionContributions());` 다음 줄에 삽입:
```csharp
            // Sub-slice A: 현행(전 캐릭 틱) 보존을 위해 전 캐릭 마킹. Sub-slice C에서 내 캐릭만으로 좁힌다.
            worldEntity.Add(new GameFramework.World.Simulated());
```

- [ ] **Step 3: 양쪽 컴파일 확인**

`refresh_unity` + `read_console(types=[error])` — 서버 인스턴스, 클라 인스턴스 각각. 0건 기대.

- [ ] **Step 4: 커밋 (각 repo 개별, 내 파일만)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git add Assets/Scripts/EntityCreator/CharacterCreator.cs && git commit -m "feat: 서버 캐릭터에 Simulated 마커 부착 (전 캐릭)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/EntityCreator/CharacterCreator.cs && git commit -m "feat: 클라 캐릭터에 Simulated 마커 부착 (전 캐릭, 현행 보존)"
```

---

## Task 3: `LOPWorld.Mutation`이 `Has<Simulated>`만 순회

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs:22-36` (Mutation)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs` (기존 3 테스트 엔티티에 Simulated 추가 + 신규 게이트 테스트)

**Interfaces:**
- Consumes: `GameFramework.World.Simulated`
- Produces: `LOPWorld.Mutation`이 `Has<Simulated>` 엔티티만 이동·어빌리티·상태 틱.

- [ ] **Step 1: 실패 테스트 작성 (마커 없으면 안 틱)**

`LOPWorldTests.cs`에 추가:
```csharp
        [Test]
        public void Tick_SkipsEntitiesWithoutSimulated()
        {
            var registry = new EntityRegistry();
            var statusEffects = new StatusEffectSystem(new StatsSystem());
            var abilitySystem = new AbilitySystem(new ManaSystem());
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()), abilitySystem, statusEffects);

            var entity = new Entity("e1");
            entity.Add(new Stats());
            entity.Add(new StatusEffects());
            registry.Add(entity);   // Simulated 없음

            statusEffects.Apply(entity,
                new StatusEffectData(1, DurationPolicy.Duration, 5, null, StatusStackPolicy.Refresh, 1), "src", 0);

            world.Tick(5, 0.05f);   // Simulated 없으니 만료 sweep 안 돎
            Assert.That(entity.Get<StatusEffects>().Effects.Count, Is.EqualTo(1), "Simulated 없으면 틱 스킵");
        }
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

UnityMCP `run_tests(mode=EditMode, assembly_names=["baegames.LOP.Shared.Tests.EditMode"], test_names=["LOP.Tests.LOPWorldTests.Tick_SkipsEntitiesWithoutSimulated"], unity_instance=클라)`. 현재는 마커 무시하고 틱하므로 Effects.Count==0 → **FAIL** 기대.

- [ ] **Step 3: Mutation 구현 변경**

`LOPWorld.cs` Mutation 교체:
```csharp
        protected override void Mutation(long tick, float deltaTime)
        {
            // 이동은 어빌리티 페이즈 전진보다 먼저 — 대시 발동 틱의 입력 게이트 타이밍이 이 순서에 걸려 있다.
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _movementSystem.Tick(entity, tick, deltaTime);
                }
            }

            // 어빌리티 페이즈 전진(Active 진입 시 효과 적용) + 상태이상 만료.
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _abilitySystem.Tick(entity, tick);
                    _statusEffectSystem.Tick(entity, tick);
                }
            }
        }
```

- [ ] **Step 4: 기존 3 테스트 엔티티에 `Simulated` 추가**

`LOPWorldTests.cs`의 `Tick_ExpiresDurationEffect_ViaMutationSweep`·`Tick_AdvancesAbilityPhase_ViaMutationSweep` 각 테스트에서 `registry.Add(entity);` **직전**에 `entity.Add(new Simulated());` 추가. `Tick_EntityWithoutStatusEffects_NoThrow`의 `registry.Add(new Entity("bare"));`는 그대로 둔다(no-throw 검증이라 스킵돼도 통과).

예 (`Tick_ExpiresDurationEffect_ViaMutationSweep`):
```csharp
            var entity = new Entity("e1");
            entity.Add(new Stats());
            entity.Add(new StatusEffects());
            entity.Add(new Simulated());   // Mutation이 Has<Simulated>만 순회
            registry.Add(entity);
```
(`Tick_AdvancesAbilityPhase_ViaMutationSweep`도 동일하게 `entity.Add(new Simulated());` 추가.)

- [ ] **Step 5: 전체 EditMode 실행 → green**

`run_tests(mode=EditMode, assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)` — 신규 게이트 테스트 PASS + 기존 전부 PASS.

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/LOPWorldTests.cs
git commit -m "feat: LOPWorld.Mutation이 Has<Simulated>만 순회 (양쪽 전 캐릭 마킹이라 무변경)"
```

---

## Task 4: `MovementSystem.Tick` 외력 통합 (입력 없는 Simulated도 resolve) + 서버 AI 분기 제거

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MovementSystem.cs:66-116` (Tick)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs` (MoveCharacters의 AI `ApplyToVelocity` 분기 제거 + 미사용 `motionContributionSystem` 주입 정리)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionContributionSystem.cs` (미사용된 `ApplyToVelocity` 제거)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/MovementSystemTests.cs`

**Interfaces:**
- Produces: `MovementSystem.Tick`이 입력 유무와 무관하게 Simulated 엔티티의 외력(MotionContributions)을 velocity에 folding. base = 입력 있으면 입력 기반 / 없으면 현재 수평 velocity.

> **동작 노트:** 서버 AI 넉백 resolve 위치가 `MoveCharacters`(driveeffects 후, 같은 틱) → 이동 페이즈(driveeffects 전, 1틱 이른)로 이동한다. 넉백은 다-틱 감쇠 push(≈12틱)라 1틱 시프트는 **체감 무변화**이고, 오히려 플레이어(원래 이동 페이즈서 resolve)와 **타이밍이 일치**한다. 클라 원격은 `MotionContributions`가 비어 있어 "같은 값 재기록"(무해, `RemoteEntityInterpolator`는 velocity 미사용).

- [ ] **Step 1: 실패 테스트 작성 (입력 없는 엔티티도 외력 resolve)**

`MovementSystemTests.cs`에 추가 (기존 파일의 using·헬퍼 스타일 확인 후 맞출 것):
```csharp
        [Test]
        public void Tick_InputlessEntity_ResolvesExternalForceIntoVelocity()
        {
            var movement = new MovementSystem(new GameFramework.World.StatsSystem(), new MotionContributionSystem());

            var e = new GameFramework.World.Entity("e1");
            e.Add(new GameFramework.World.Velocity { Linear = new System.Numerics.Vector3(2, 5, 0) });  // 브레인 속도
            var mc = new MotionContributions();
            mc.Items.Add(new MotionContribution(new System.Numerics.Vector3(0, 0, 8),
                MotionContributionMode.Additive, 0, 0, 100, 1f));   // 활성 넉백, decay 1
            e.Add(mc);
            // InputBuffer 없음 (AI/원격)

            movement.Tick(e, 0, 0.05f);

            var v = e.Get<GameFramework.World.Velocity>().Linear;
            Assert.Less(System.Numerics.Vector3.Distance(v, new System.Numerics.Vector3(2, 5, 8)), 1e-4f,
                "입력 없어도 외력 folding, y 보존");
        }
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

`run_tests(... test_names=["LOP.Tests.MovementSystemTests.Tick_InputlessEntity_ResolvesExternalForceIntoVelocity"], unity_instance=클라)`. 현재 Tick은 InputBuffer 없으면 early-return(velocity 미변경) → v.z==0 → **FAIL**.

- [ ] **Step 3: `MovementSystem.Tick` 재구성**

`MovementSystem.cs`의 `Tick` 전체를 교체:
```csharp
        public void Tick(GameFramework.World.Entity entity, long currentTick, float deltaTime)
        {
            var worldVelocity = entity.Get<GameFramework.World.Velocity>();
            if (worldVelocity == null)
            {
                return;   // 이동 없는 엔티티
            }
            Vector3 velocity = worldVelocity.Linear.ToUnity();   // Y 보존용
            Vector3 baseHorizontal = new Vector3(velocity.x, 0f, velocity.z);   // 기본 = 현재 수평(입력 없으면 유지)

            var input = entity.Get<InputBuffer>()?.Current;
            if (input != null)
            {
                if (AbilitySystem.TryGetActiveMotionEffect(entity, currentTick, out var motion))
                {
                    // 대시(파생 Override): 바라보는 방향으로 speed. 입력 무시(락) + 회전 미변경 + 점프 무시.
                    Vector3 forward = entity.Get<GameFramework.World.Transform>().Rotation.ToUnity() * Vector3.forward;
                    baseHorizontal = new Vector3(forward.x, 0f, forward.z).normalized * motion.Speed;
                }
                else
                {
                    var stats = entity.Get<GameFramework.World.Stats>();
                    float speed = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.MoveSpeed);
                    var result = ProcessMovement(new MovementInput(
                        velocity, input.Horizontal, input.Vertical, speed, MaxAcceleration, deltaTime));
                    baseHorizontal = new Vector3(result.velocity.x, 0f, result.velocity.z);
                    if (input.Jump)
                    {
                        velocity.y = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.JumpPower);
                    }
                    if (result.hasRotation)
                    {
                        entity.Get<GameFramework.World.Transform>().Rotation = Quaternion.Euler(result.rotation).ToNumerics();
                    }
                }
            }

            // 외부 기여(넉백 등) 합성 — 입력 유무 무관, 플레이어·AI 공통. 만료 프루닝.
            var contributions = entity.Get<MotionContributions>();
            motionContributionSystem.Prune(contributions, currentTick);
            Vector3 finalHorizontal = motionContributionSystem
                .Resolve(baseHorizontal.ToNumerics(), contributions, currentTick).ToUnity();

            velocity.x = finalHorizontal.x;
            velocity.z = finalHorizontal.z;
            worldVelocity.Linear = velocity.ToNumerics();
        }
```

- [ ] **Step 4: 테스트 실행 → green (신규 + 기존 MovementSystem 테스트)**

`run_tests(... assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)` — 신규 PASS + 기존 MovementSystem·MotionContributionSystem 테스트 전부 PASS.

- [ ] **Step 5: 서버 `MoveCharacters` AI 분기 제거**

서버 `LOPRunner.cs` `MoveCharacters`에서 아래 블록(Task 넉백에서 넣었던 임시 분기)을 제거:
```csharp
                var worldEntity = entityRegistry.Get(entity.entityId);
                // 입력 비조종(AI 등)은 world.Tick의 MovementSystem을 안 타므로 외력(넉백)을 여기서 folding.
                // 플레이어는 이미 MovementSystem.Tick이 입력 기반 base로 같은 Resolve를 태웠다.
                if (worldEntity.Get<InputBuffer>() == null)
                {
                    motionContributionSystem.ApplyToVelocity(worldEntity, tick);
                }
```
→ 다음으로 축약(worldEntity는 kinematic 호출에 계속 필요):
```csharp
                var worldEntity = entityRegistry.Get(entity.entityId);
```
그리고 이제 미사용이 된 `[Inject] private MotionContributionSystem motionContributionSystem;`와 `long tick = Runner.Time.tick;`(다른 데서 안 쓰면) 제거. `read_console`로 미사용 경고 확인 후 정리.

- [ ] **Step 6: 미사용 `MotionContributionSystem.ApplyToVelocity` 제거**

`MotionContributionSystem.cs`에서 `ApplyToVelocity` 메서드 삭제(이제 `MovementSystem.Tick`이 외력을 직접 resolve). `MotionContributionSystemTests.cs`의 `ApplyToVelocity*` 테스트 4개(`EntityWith` 헬퍼 포함) 삭제 — 그 커버리지는 Task 4 Step 1 테스트 + 기존 `Resolve*`/`Prune*`가 대체.

- [ ] **Step 7: 양쪽 컴파일 + EditMode green**

Shared EditMode(`run_tests`, 클라) green + 서버 `refresh_unity`/`read_console` 0건.

- [ ] **Step 8: 커밋 (Shared + Server 각각)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/MovementSystem.cs Runtime/Scripts/Game/MotionContributionSystem.cs Tests/EditMode/MovementSystemTests.cs Tests/EditMode/MotionContributionSystemTests.cs
git commit -m "feat: MovementSystem.Tick이 입력 없는 Simulated도 외력 resolve (ApplyToVelocity 흡수)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/LOPRunner.cs
git commit -m "refactor: MoveCharacters AI 넉백 임시분기 제거 (이동 페이즈로 흡수됨)"
```

---

## Task 5: driveeffects를 `LOPWorld.Tick`으로 흡수

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs` (ctor에 `AbilityEffectExecutor` 추가 + Mutation에 driveeffects 3번째 루프)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs:117` (`DriveAbilityEffects()` 호출 제거 + 메서드 제거)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs:103` (`DriveAbilityEffects()` 호출 제거 + 메서드 제거)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs` (ctor 3곳 + driveeffects 검증 테스트)

**Interfaces:**
- Consumes: `AbilityEffectExecutor.DriveActiveEntity(Entity caster, long currentTick)` (LOP-Shared, 이미 존재)
- Produces: `LOPWorld` ctor 시그니처 = `(EntityRegistry, WorldEventBuffer, MovementSystem, AbilitySystem, StatusEffectSystem, AbilityEffectExecutor)`. `Mutation`이 어빌리티 페이즈 전진 후 Simulated 전원의 active effect를 구동.

- [ ] **Step 1: 실패 테스트 작성 (Tick이 active 어빌리티 효과를 구동)**

`LOPWorldTests.cs`에 추가. 간이 스파이 핸들러로 검증:
```csharp
        private class SpyEffect : AbilityEffect { }
        private class SpyHandler : IAbilityEffectHandler
        {
            public int enterCount;
            public System.Type EffectType => typeof(SpyEffect);
            public void OnActiveEnter(AbilityEffectContext ctx, AbilityEffect effect) => enterCount++;
            public void OnActiveTick(AbilityEffectContext ctx, AbilityEffect effect) { }
        }

        [Test]
        public void Tick_DrivesActiveAbilityEffect_ViaAbsorbedPhase()
        {
            var registry = new EntityRegistry();
            var abilitySystem = new AbilitySystem(new ManaSystem());
            var spy = new SpyHandler();
            var executor = new AbilityEffectExecutor(new IAbilityEffectHandler[] { spy });
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                abilitySystem, new StatusEffectSystem(new StatsSystem()), executor);

            var entity = new Entity("e1");
            entity.Add(new Abilities());
            entity.Add(new Mana(100));
            entity.Add(new Stats());
            entity.Add(new StatusEffects());
            entity.Add(new Simulated());
            registry.Add(entity);

            abilitySystem.Grant(entity, 1);
            // startup0/active1/recovery0 + SpyEffect 1개 — Active 진입 틱에 OnActiveEnter 1회
            abilitySystem.TryActivate(entity,
                new AbilityData(1, 0, 0, 0, 1, 0, new AbilityEffect[] { new SpyEffect() }), entity, 0);

            world.Tick(0, 0.05f);   // Startup(0)->Active 진입 → driveeffects 페이즈가 OnActiveEnter 호출
            Assert.That(spy.enterCount, Is.EqualTo(1), "흡수된 driveeffects 페이즈가 active 효과 구동");
        }
```
> `AbilityData` 생성자 인자 순서·`AbilityEffect` 파생 방식은 기존 `AbilitySystemTests`/`AbilityEffectExecutorTests`에서 확인해 맞출 것.

- [ ] **Step 2: 테스트 실행 → 실패 (컴파일 실패: ctor 6인자 없음)**

`run_tests(... test_names=["...Tick_DrivesActiveAbilityEffect_ViaAbsorbedPhase"], unity_instance=클라)` → 컴파일 에러(LOPWorld ctor 5인자) 기대.

- [ ] **Step 3: `LOPWorld`에 executor 추가 + driveeffects 페이즈**

`LOPWorld.cs`:
```csharp
    public class LOPWorld : GameFramework.World.WorldBase
    {
        private readonly MovementSystem _movementSystem;
        private readonly AbilitySystem _abilitySystem;
        private readonly StatusEffectSystem _statusEffectSystem;
        private readonly AbilityEffectExecutor _abilityEffectExecutor;

        public LOPWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            MovementSystem movementSystem,
            AbilitySystem abilitySystem,
            StatusEffectSystem statusEffectSystem,
            AbilityEffectExecutor abilityEffectExecutor)
            : base(entityRegistry, eventBuffer)
        {
            _movementSystem = movementSystem;
            _abilitySystem = abilitySystem;
            _statusEffectSystem = statusEffectSystem;
            _abilityEffectExecutor = abilityEffectExecutor;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _movementSystem.Tick(entity, tick, deltaTime);
                }
            }

            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _abilitySystem.Tick(entity, tick);
                    _statusEffectSystem.Tick(entity, tick);
                }
            }

            // 페이즈 전진 후 active 창 effect 구동(대시 push·데미지·상태효과). 이전엔 host DriveAbilityEffects.
            // cross-entity 판정(서버 데미지/넉백)이 "전원 이동 후"를 보도록 별도 루프(페이즈 배리어).
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _abilityEffectExecutor.DriveActiveEntity(entity, tick);
                }
            }
        }
    }
```

- [ ] **Step 4: 기존 LOPWorldTests ctor 3곳에 executor 인자 추가**

`Tick_ExpiresDurationEffect_ViaMutationSweep`·`Tick_EntityWithoutStatusEffects_NoThrow`·`Tick_AdvancesAbilityPhase_ViaMutationSweep`·`Tick_SkipsEntitiesWithoutSimulated`의 `new LOPWorld(...)` 마지막 인자로 `new AbilityEffectExecutor(null)` 추가. 예:
```csharp
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()), abilitySystem, statusEffects,
                new AbilityEffectExecutor(null));
```

- [ ] **Step 5: 테스트 실행 → green**

`run_tests(... assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)` — 신규 driveeffects 테스트 + 기존 전부 PASS.

- [ ] **Step 6: 양쪽 `LOPRunner`에서 `DriveAbilityEffects()` 제거**

서버 `LOPRunner.cs`: `UpdateRunner`의 `DriveAbilityEffects();`(line ~117) 삭제 + `DriveAbilityEffects()` 메서드 정의 삭제.
클라 `LOPRunner.cs`: `UpdateRunner`의 `DriveAbilityEffects();`(line ~103) 삭제 + 메서드 정의 삭제.
> 순서 보존 확인: 이제 driveeffects는 `world.Tick` 마지막 페이즈 = 예전 "world.Tick 직후 DriveAbilityEffects"와 동일 위치.

- [ ] **Step 7: 양쪽 컴파일 확인**

서버·클라 각각 `refresh_unity` + `read_console(types=[error])` = 0건.

- [ ] **Step 8: 커밋 (Shared + Server + Client)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/LOPWorldTests.cs
git commit -m "feat: driveeffects를 LOPWorld.Tick으로 흡수 (Simulated 전원, 페이즈 배리어 유지)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git add Assets/Scripts/Game/LOPRunner.cs && git commit -m "refactor: 서버 DriveAbilityEffects 제거 (world.Tick 흡수)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Game/LOPRunner.cs && git commit -m "refactor: 클라 DriveAbilityEffects 제거 (world.Tick 흡수)"
```

---

## 최종 검증 (Sub-slice A 완료 게이트)

- [ ] **Shared EditMode 전체 green** — `run_tests(mode=EditMode, assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)`.
- [ ] **양쪽 컴파일 클린** — 서버·클라 `read_console(types=[error])` 0건.
- [ ] **서버 플레이 무회귀** — 룸 접속 → 이동/점프/대시/공격/**적 넉백** 정상(넉백은 이제 이동 페이즈 resolve, 1틱 시프트 체감 무). AI 이동 정상.
- [ ] **클라 플레이 무회귀** — 내 캐릭 이동/점프/대시 정상, 남 캐릭 보간 정상(러버밴딩 없음), 남 어빌리티 연출(cue) 정상.
- [ ] **머지** — 4개 repo(GameFramework/Shared/Server/Client) 각 `feature/unified-tick-slice-a`를 main에 `--no-ff` 머지.

> Sub-slice A는 여기까지 **동작 무변경**. 실제 scope 축소(클라 내 캐릭만)와 `#6` 종결은 Sub-slice C.

---

## Self-Review 결과 (작성자 점검)

- **Spec 커버리지:** Simulated 마커(Task1-2) / world.Tick 순회(Task3) / driveeffects 흡수(Task5) / 외력 통합·부채 정산(Task4) 매핑됨. 키네마틱 흡수·IMotionBridge는 Sub-slice B(이 plan 범위 밖, 명시). 클라 scope·Reconciler는 Sub-slice C.
- **Placeholder:** 없음(각 스텝 완전 코드). 단 Task5 Step1의 `AbilityData`/`AbilityEffect` 생성 형태는 "기존 테스트서 확인해 맞출 것"으로 지시 — 기존 `AbilitySystemTests:66-67`가 `new AbilityData(1,0,0,0,1,0,null)`, `AbilityEffectExecutorTests`가 `AbilityEffect` 파생 예를 제공.
- **타입 일관성:** `Simulated`(Task1)→소비(Task2,3,5) 일치. `AbilityEffectExecutor.DriveActiveEntity(entity, tick)` 2인자(#3-WC 정리 후)와 일치. `LOPWorld` ctor 6인자(Task5) 반영.
- **동작 보존 순서:** Task2(마커 부착, world는 아직 All)→Task3(Has<Simulated> 순회, 이미 마킹됨)로 각 태스크 후 동작 유지. Task4 외력은 클라 원격 무해(빈 contributions) 검증됨.
