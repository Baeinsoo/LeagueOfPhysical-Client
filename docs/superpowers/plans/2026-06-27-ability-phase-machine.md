# 어빌리티 페이즈 머신 Implementation Plan (dash/attack 어빌리티화 Slice 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 어빌리티에 `Startup → Active → Recovery` **틱 기반 페이즈 머신**을 추가한다. `TryActivate`가 즉시 효과 적용 대신 머신을 시작하고, 신규 `AbilitySystem.Tick`이 페이즈를 전진하며 **Active 진입 시 StatusEffect를 적용**한다. 엔티티당 동시 1 발동(`ActiveAbility`)·busy 게이팅. **거동 보존**: 헤이스트를 `startup0/active1/recovery0`로 두어 HASTE 버튼/H키 결과가 3e와 동일. dash 임펄스·attack 데미지(side behavior)는 후속 슬라이스가 Active 창에 꽂는다(이 슬라이스 범위 밖).

**Spec:** `docs/superpowers/specs/2026-06-27-ability-phase-machine-design.md`
**선행:** 3e(TbAbility 데이터 드리븐 + id 기반 입력) main 머지. AbilitySystem·StatusEffectSystem 클·서 등록(3d), 서버 ManaSystem 등록(3d).

**Architecture:** 코어 전부 **LOP-Shared**(순수 C# + EditMode). 클·서는 `AbilityDataProvider.Map`이 새 프레임 필드를 기본값으로 채우는 1줄 변경만(TbAbility 컬럼 외부화는 dash/attack 슬라이스). `AbilityActivator`/`GameLifetimeScope` **무변경**(TryActivate 시그니처 유지, AbilitySystem 이미 등록).

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 컴파일·플레이) / EditMode.

---

## 레포 / 도구 참조
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared` (코어 + 테스트)
- **Client/Server:** `AbilityDataProvider.cs` 1줄 + 문서(Client)
- **GameFramework:** **무변경**.
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 재확인. 모든 호출 `unity_instance` 명시.

**⚠️ EOL:** mass sed 금지. 신규=Write/편집=Edit. 신규 `.cs`는 `refresh_unity`가 `.meta` 생성.
**픽스처:** `git add -A` 금지. (Client: Room.unity/ProjectSettings. Server: Room.unity/ConfigureRoomComponent/GameRuleSystem.)
**컨벤션:** Microsoft C#. World 타입은 풀 네임스페이스(`GameFramework.World.X`); LOP 타입(`AbilityData`/`AbilitySystem`/`Abilities`/`AbilityPhase`/`ActiveAbility`)은 `namespace LOP` 단축.

---

## 확정된 설계 결정 (spec 기반)
1. **`ActiveAbility` = readonly struct(값 의미, 롤백 친화)**, `Abilities.ActiveAbility`는 `ActiveAbility?`(null ⇔ Ready). 매 틱 struct 교체(기존 AbilitySlot idiom).
2. **페이즈 경계 = 발동 시 절대 틱 3개 계산**(`StartupEndTick`/`ActiveEndTick`/`RecoveryEndTick`) → `Tick`은 `AbilityData` 없이 경계 비교만(코어 self-contained).
3. **효과 적용 = Startup→Active 전이 시 1회**(`StatusEffectSystem.Apply(PendingEffects)`). `TryActivate`는 더 이상 즉시 적용 안 함.
4. **프레임 데이터 기본값**: TbAbility 컬럼 미추가 → `AbilityDataProvider.Map`이 `startup0/active1/recovery0`. (실 프레임 값 외부화는 dash/attack.)
5. **busy 게이트**: `CanActivate`에 `ActiveAbility == null` 추가.
6. **`Tick` 구동**: `LOPWorld.Mutation` sweep에 `AbilitySystem.Tick` 추가(StatusEffectSystem.Tick 앞). `LOPWorld` ctor에 `AbilitySystem` 주입(이미 등록).

---

## Task 0: 브랜치 (LOP-Shared / Client / Server)
- [ ] **Step 1: 상태 확인** — 3 레포 main, 픽스처 외 깨끗(Client/Server 픽스처 + 이번 plan). 예상 밖이면 멈추고 보고.
- [ ] **Step 2: 브랜치**
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/ability-phase-machine; done
```

---

## Task 1: LOP-Shared — 프레임 데이터 + 페이즈 머신

- [ ] **Step 1: `AbilityData.cs` — 프레임 필드** (Edit). `CastTimeTicks` → `StartupTicks` 리네임 + `ActiveTicks`/`RecoveryTicks` 추가(ctor도). 최종:
```csharp
public readonly struct AbilityData
{
    public readonly int AbilityId;
    public readonly long CooldownTicks;
    public readonly int MpCost;
    public readonly long StartupTicks;    // 윈드업(=캐스트). 0=즉시 Active
    public readonly long ActiveTicks;     // 판정 창 길이
    public readonly long RecoveryTicks;   // 후딜
    public readonly TargetingMode TargetingMode;
    public readonly float Range;
    public readonly int[] ProducesEffectIds;

    public AbilityData(int abilityId, long cooldownTicks, int mpCost,
                       long startupTicks, long activeTicks, long recoveryTicks,
                       TargetingMode targetingMode, float range, int[] producesEffectIds)
    { /* 대입 */ }
}
```
- [ ] **Step 2: `AbilityPhase` + `ActiveAbility`** — `Abilities.cs`에 추가(Edit). `AbilitySlot`은 유지, `Abilities`에 nullable 필드:
```csharp
public enum AbilityPhase { Ready, Startup, Active, Recovery }

/// <summary>진행 중인 어빌리티 발동(transient). 엔티티당 1. null ⇔ Ready. Phase ∈ {Startup,Active,Recovery}.</summary>
public readonly struct ActiveAbility
{
    public readonly int AbilityId;
    public readonly AbilityPhase Phase;
    public readonly long StartupEndTick;
    public readonly long ActiveEndTick;
    public readonly long RecoveryEndTick;
    public readonly Entity Target;
    public readonly StatusEffectData[] PendingEffects;

    public ActiveAbility(int abilityId, AbilityPhase phase, long startupEndTick, long activeEndTick,
                         long recoveryEndTick, Entity target, StatusEffectData[] pendingEffects)
    { /* 대입 */ }

    public ActiveAbility WithPhase(AbilityPhase phase)
        => new ActiveAbility(AbilityId, phase, StartupEndTick, ActiveEndTick, RecoveryEndTick, Target, PendingEffects);
}
```
그리고 `Abilities` 컴포넌트에 `public ActiveAbility? ActiveAbility { get; set; }` 추가.
- [ ] **Step 3: `AbilitySystem.cs` — busy 게이트 + 머신 시작 + Tick** (Edit).
  - `CanActivate`: 기존 체크 앞/뒤에 busy 추가:
```csharp
            var abilities = caster.Get<Abilities>();
            if (abilities == null || !abilities.Slots.TryGetValue(data.AbilityId, out var slot)) return false;
            if (abilities.ActiveAbility != null) return false;            // busy
            if (currentTick < slot.CooldownEndTick) return false;
            // (자원 체크 유지)
```
  - `TryActivate`: Commit 후 **효과 즉시 적용 제거**, 대신 머신 시작:
```csharp
            // Commit (자원 + 쿨다운) — 유지
            if (data.MpCost > 0) _manaSystem.Spend(caster.Get<Mana>(), data.MpCost);
            var abilities = caster.Get<Abilities>();
            abilities.Slots[data.AbilityId] = new AbilitySlot(data.AbilityId, currentTick + data.CooldownTicks);

            // 페이즈 머신 시작(효과는 Active 진입 시 Tick이 적용)
            long startupEnd  = currentTick + data.StartupTicks;
            long activeEnd   = startupEnd + data.ActiveTicks;
            long recoveryEnd = activeEnd + data.RecoveryTicks;
            abilities.ActiveAbility = new ActiveAbility(data.AbilityId, AbilityPhase.Startup,
                startupEnd, activeEnd, recoveryEnd, target, producedEffects);
            return true;
```
  - 신규 `Tick`:
```csharp
        /// <summary>진행 중 ActiveAbility의 페이즈를 전진. Active 진입 시 StatusEffect 적용, Recovery 종료 시 Ready.</summary>
        public void Tick(Entity entity, long currentTick)
        {
            var abilities = entity.Get<Abilities>();
            if (abilities?.ActiveAbility == null) return;

            var a = abilities.ActiveAbility.Value;
            switch (a.Phase)
            {
                case AbilityPhase.Startup:
                    if (currentTick >= a.StartupEndTick)
                    {
                        if (a.PendingEffects != null)
                            foreach (var effect in a.PendingEffects)
                                _statusEffectSystem.Apply(a.Target, effect, entity.Id, currentTick);
                        abilities.ActiveAbility = a.WithPhase(AbilityPhase.Active);
                    }
                    break;
                case AbilityPhase.Active:
                    if (currentTick >= a.ActiveEndTick) abilities.ActiveAbility = a.WithPhase(AbilityPhase.Recovery);
                    break;
                case AbilityPhase.Recovery:
                    if (currentTick >= a.RecoveryEndTick) abilities.ActiveAbility = null;
                    break;
            }
        }
```
> `TryActivate` 시그니처(`caster, in AbilityData, target, StatusEffectData[] producedEffects, currentTick`) **불변** → side `AbilityActivator` 무변경.
- [ ] **Step 4: `LOPWorld.cs` — ctor + Tick 구동** (Edit). ctor에 `AbilitySystem` 추가, `Mutation` sweep에 `AbilitySystem.Tick` 추가(기존 `StatusEffectSystem.Tick` **앞**):
```csharp
        private readonly AbilitySystem _abilitySystem;
        private readonly StatusEffectSystem _statusEffectSystem;

        public LOPWorld(GameFramework.World.EntityRegistry entityRegistry,
                        GameFramework.World.WorldEventBuffer eventBuffer,
                        AbilitySystem abilitySystem,
                        StatusEffectSystem statusEffectSystem)
            : base(entityRegistry, eventBuffer)
        { _abilitySystem = abilitySystem; _statusEffectSystem = statusEffectSystem; }

        protected override void Mutation(long tick, float deltaTime)
        {
            foreach (var entity in EntityRegistry.All)
            {
                _abilitySystem.Tick(entity, tick);
                _statusEffectSystem.Tick(entity, tick);
            }
        }
```
> 현재 LOPWorld ctor가 `(EntityRegistry, WorldEventBuffer, StatusEffectSystem)`(3a)면 거기에 `AbilitySystem` 추가. VContainer가 `Register<IWorld, LOPWorld>`로 자동 주입(AbilitySystem 3d 등록). **실행 시 현재 ctor 시그니처 확인 후 정합.**
- [ ] **Step 5: LOP-Shared 컴파일(타입)** — 클·서 `refresh_unity`(scope=all, force) → `read_console`. **이 시점엔 클·서 `AbilityDataProvider`가 옛 ctor(`CastTimeTicks`)를 써 에러 예상** — Task 3·4에서 닫음. LOP-Shared 자체 타입 에러만 0 확인.

---

## Task 2: LOP-Shared — 테스트 갱신 + 페이즈 테스트

- [ ] **Step 1: `Tests/EditMode/AbilitySystemTests.cs` 갱신** (Edit). **TryActivate 의미 변경 반영** — 효과가 *즉시* 적용된다고 검증하던 테스트는 *Tick으로 Active 도달 후* 적용으로 수정. `AbilityData` ctor 호출도 새 시그니처(startup/active/recovery)로. (현 파일 Read 후 깨지는 단언 보정.)
- [ ] **Step 2: `Tests/EditMode/LOPWorldTests.cs` 갱신** (Edit, 3a 산출). `new LOPWorld(registry, buffer, statusEffects)` → `new LOPWorld(registry, buffer, abilitySystem, statusEffects)`. `var abilitySystem = new AbilitySystem(new GameFramework.World.ManaSystem(), statusEffects);` (ManaSystem ctor 실행 시 확인).
- [ ] **Step 3: 페이즈 머신 테스트 추가** (`AbilitySystemTests.cs`에 또는 신규 `AbilityPhaseTests.cs` Write). 케이스:
  - 즉발(0/1/0): TryActivate → ActiveAbility=Startup, 효과 미적용 → Tick(T) Active진입·효과적용·쿨다운설정 → … → ActiveAbility=null(Ready).
  - startup>0: 효과가 startup 경과·Active 도달 전엔 미적용.
  - busy: ActiveAbility 진행 중 `CanActivate`/`TryActivate` false.
  - recovery: recovery 동안 busy, 종료 후 Ready.
- [ ] **Step 4: EditMode 실행** — `run_tests`(`baegames.LOP.Shared.Tests.EditMode`) green. 신규 `.meta` 확인(있으면).

---

## Task 3: Client — AbilityDataProvider 프레임 기본값
- [ ] **Step 1: `Assets/Scripts/Game/AbilityDataProvider.cs` — `Map` 갱신** (Edit). 새 `AbilityData` ctor에 프레임 기본값(`startup0/active1/recovery0`):
```csharp
        private static AbilityData Map(LOP.MasterData.Ability r)
        {
            var targeting = (TargetingMode)System.Enum.Parse(typeof(TargetingMode), r.TargetingMode);
            int[] effects = r.ProducesEffectId == 0 ? System.Array.Empty<int>() : new[] { r.ProducesEffectId };
            // 프레임 데이터는 TbAbility 컬럼 외부화 전까지 기본값(즉발 등가). dash/attack 슬라이스에서 컬럼화.
            return new AbilityData(r.Id, r.CooldownTicks, r.MpCost,
                startupTicks: 0, activeTicks: 1, recoveryTicks: 0,
                targeting, r.Range, effects);
        }
```
> `r.CastTimeTicks`는 사용 안 함(TbAbility엔 `cast_time_ticks` 컬럼 남아있지만 프레임은 기본값 사용 — 컬럼↔프레임 매핑은 dash/attack에서 정리).
- [ ] **Step 2: 컴파일** — Client `refresh_unity` → `read_console`(error) 0.

---

## Task 4: Server — 동형
- [ ] **Step 1: `Assets/Scripts/Game/AbilityDataProvider.cs`** — Task 3 Step 1과 동일 `Map` 갱신.
- [ ] **Step 2: 컴파일** — Server `refresh_unity` → error 0.

---

## Task 5: 종합 검증
- [ ] **Step 1: 양쪽 재스캔** — error 0.
- [ ] **Step 2: EditMode** — `baegames.LOP.Shared.Tests.EditMode` green(갱신 + 신규 페이즈).
- [ ] **Step 3: 플레이 무회귀(사용자)** — HASTE 버튼/H키 → +30% 100틱 후 복귀(3e 동일). 이동/점프/전투/스폰 등 *이전과 동일*. NRE 0. (페이즈 도입했으나 haste=즉발 등가라 체감 변화 없어야 함.)

---

## Task 6: 커밋 + 머지
- [ ] **Step 1: LOP-Shared** — `git add` AbilityData.cs/Abilities.cs/AbilitySystem.cs/LOPWorld.cs + 테스트(.cs/.meta).
- [ ] **Step 2: Client** — `git add Assets/Scripts/Game/AbilityDataProvider.cs` + `docs/superpowers/specs/2026-06-27-ability-phase-machine-design.md docs/superpowers/plans/2026-06-27-ability-phase-machine.md` (픽스처 제외).
- [ ] **Step 3: Server** — `git add Assets/Scripts/Game/AbilityDataProvider.cs` (픽스처 제외).
- [ ] **Step 4: 머지(사용자 요청 시)** — LOP-Shared → Client → Server `--no-ff`.

커밋 메시지(LOP-Shared 예):
```
feat(world): ability phase machine (startup/active/recovery) [dash/attack slice 1]

AbilityData gains startup/active/recovery frame ticks; Abilities holds a
nullable ActiveAbility. TryActivate starts the phase machine instead of
applying effects immediately; new AbilitySystem.Tick (driven by LOPWorld)
advances phases and applies StatusEffects on Active entry. CanActivate gates
on busy (ActiveAbility != null). Haste = 0/1/0 (instant-equivalent) ->
behavior unchanged. Damage/impulse behaviors plug into the Active window in
later slices.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

## Self-Review (작성자 체크)
- **Spec 커버리지:** 프레임필드=T1S1 / enum+ActiveAbility=T1S2 / busy+TryActivate+Tick=T1S3 / LOPWorld 구동=T1S4 / 테스트=T2 / provider 기본값=T3·4. ✅
- **거동 보존:** haste 0/1/0 → Active 진입(=발동 틱)에 효과 적용 + 짧은 busy(체감 0). 데미지/임펄스 미도입. ✅
- **시그니처 안정:** TryActivate 불변 → AbilityActivator/입력경로 무변경. LOPWorld ctor만 변경(DI 자동). ✅
- **표준 명명:** Startup/Active/Recovery·AbilityPhase·ActiveAbility·Grant/CanActivate/TryActivate/Commit/Tick — GAS·격투 정합(검증 완료). ✅
- **결정론:** 틱 경계 절대값, Mutation 순서(Ability→StatusEffect). side behavior 폴은 후속. ✅
- **범위 정직:** Damage/Impulse·프레임 컬럼 외부화·채널/중단/콤보·캐릭터 로드아웃 Out of Scope. ✅
- **테스트 깨짐 처리:** TryActivate 의미변경으로 기존 AbilitySystemTests/LOPWorldTests 갱신 명시. ✅
- **EOL/픽스처:** Edit/Write, .cs+.meta, 픽스처 제외. ✅
```
