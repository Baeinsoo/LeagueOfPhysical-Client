# 헤이스트 어빌리티 끝-끝 (가산적 증명) Implementation Plan (Phase 3d)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 3c에서 `TbStatusEffect`로 외부화한 헤이스트 데이터를 **런타임에 실제로 적용**해, 캐릭터가 인게임에서 한시적으로 빨라지는 것을 처음으로 관찰한다. 새 어빌리티/효과 체인 전체(어댑터 → `AbilitySystem.TryActivate` → `StatusEffectSystem.Apply` → `MoveSpeed` 모디파이어 → 이동 커널 → 위치 스냅샷)를 **가산적으로**(레거시 Action/Dash/Attack/Spawn 무손상) 끝-끝 검증한다.

**트리거 = 버튼 입력 발동(H키 + 온스크린 HASTE 버튼).** *(최초 계획은 스폰 자동발동이었으나, 초기 플레이어가 틱 시작 전 서버 init에 스폰돼 클라 접속 시 이미 만료 → 체감 불가. 버튼 트리거로 변경.)* 기존 `actionCode` 입력 와이어를 재사용(새 와이어 0): 버튼 → `SetActionCode("haste")` → 클라 예측 + 서버 권위 양쪽이 `AbilityActivator`로 라우팅해 `AbilitySystem.TryActivate`. AI 발동·proto/뷰 cosmetic·`TbAbility` 일반화는 여전히 3e.

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 3 "Ability↔Effect 이음새" + Phase 1 "이동→Stats 배선 선행"). **선행:** Phase 1·2(Ability/StatusEffect 코어)·3a(틱 스캐폴드)·3b(MoveSpeed 스탯)·3c(`TbStatusEffect` 테이블) 전부 main 머지.

**Architecture:** 클·서 각자 **side-local 어댑터**(`StatusEffectDataProvider`: Luban `TbStatusEffect`→`StatusEffectData`; `AbilityDataProvider`: 하드코딩 헤이스트 `AbilityData`, 3e에서 `TbAbility`로 교체) — LOP-Shared는 MasterData 비참조라 어댑터는 use-side. `GameLifetimeScope`에 `AbilitySystem`(+서버 `ManaSystem`)·`AbilityActivator` 등록. `CharacterCreator`(클·서)가 헤이스트 부여(`Grant`), 발동은 버튼→`AbilityActivator`(클 입력·서버 입력 처리에서 라우팅). LOP-Shared/GameFramework/MasterData **무변경**.

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(클·서 컴파일·플레이 검증).

---

## 레포 / 도구 참조
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` (어댑터 2 + GameLifetimeScope + CharacterCreator + 문서)
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` (어댑터 2 + GameLifetimeScope + CharacterCreator)
- **LOP-Shared / GameFramework / MasterData:** **무변경**(시스템·구조체·테이블 이미 존재).
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 `mcpforunity://instances` 재확인. 모든 호출에 `unity_instance` 명시.

**⚠️ EOL:** mass `sed` 금지. 신규=Write, 편집=Edit. 신규 `.cs`는 `refresh_unity`가 `.meta` 생성 → `.cs`+`.cs.meta` 함께 커밋.
**픽스처 보존:** `git add -A`/`commit -am` 금지. Task 명시 파일만 add. (Client: Room.unity/ProjectSettings 제외. Server: Room.unity/ConfigureRoomComponent 제외.)
**컨벤션:** Microsoft C#. World 타입(`Entity`/`Stats`/`Mana`/`StatsSystem`/`ManaSystem`)은 풀 네임스페이스(`GameFramework.World.X`). LOP 타입(`AbilitySystem`/`StatusEffectSystem`/`AbilityData`/`StatusEffectData`/`StatusModifierSpec`/enum)은 `namespace LOP`라 단축(파일이 `namespace LOP`면).

---

## 확정된 설계 결정 (실측 기반)

1. **슬라이스 범위 = 헤이스트 끝-끝 증명(가산적).** spec의 "3d=입력·AI 컷오버"를 *글자대로* 하지 않는다 — dash/attack는 stat-모디파이어 효과로 안 매핑되고(임펄스/데미지 효과 종류 신설=별도, 큼), 입력 경유 발동은 string actionCode↔int AbilityId 마찰 + 와이어가 낌. 그래서 **레거시(Action/Dash/Attack/Spawn) 전혀 안 건드리고**, 헤이스트만 새 체인으로 가산. 입력·AI 발동/와이어/뷰/legacy 은퇴는 후속(아래 Out of Scope).
2. **트리거 = 버튼 입력 발동(H키 + 온스크린 HASTE 버튼).** `CharacterCreator`(클·서)는 **부여(`Grant`)만** — 슬롯 존재로 `CanActivate` 통과. 발동은 신규 **`AbilityActivator`**(side-local): `actionCode=="haste"` → `AbilitySystem.TryActivate`, 그 외 코드는 레거시 액션으로 폴백(`TryStartAction`). 클 `PlayerInputManager.ApplyInput`(예측)·서버 `LOPRunner.ProcessInput`(권위) 양쪽이 이 라우터 경유 — **클·서 동일 적용** → MoveSpeed 일치(러버밴드 최소). 100틱 후 `LOPWorld.Mutation`(3a) sweep이 양쪽 만료 → 복귀. 버튼: `GamePadViewModel.Haste()`→`SetActionCode("haste")`, `GamePadView`가 `haste-button`(UXML/USS)에 바인딩 + `PollKeyboard` H키. 더미 부분 `// TEMP(3d)` 주석(`AbilityDataProvider` 하드코딩·`AbilityActivator` haste-only·전체 Grant) — 3e에서 `TbAbility`로 정식화.
3. **어댑터 = side-local 2개.**
   - `StatusEffectDataProvider.Get(int effectId)` → `md.Tables.TbStatusEffect.Get(effectId)`를 `StatusEffectData`로 매핑. enum은 string→파싱(`DurationPolicy`/`StatusStackPolicy`/`ModifierType`), `mod_stat_type` "MoveSpeed"→`(int)GameFramework.World.EntityStatType.MoveSpeed`, 모디파이어 1개를 `StatusModifierSpec[]`(빈 `mod_stat_type`이면 빈 배열). **이게 3c→런타임 실다리.**
   - `AbilityDataProvider.Get(int abilityId)` → 지금은 **하드코딩 헤이스트** `new AbilityData(1, cooldownTicks:0, mpCost:0, castTimeTicks:0, TargetingMode.Self, range:0, producesEffectIds:new[]{1})`. 3e에서 내부를 `TbAbility` 조회로 교체(호출부 불변). 
4. **부여 = `CharacterCreator`(클·서), 모든 생성 엔티티에 헤이스트(abilityId=1).** `AbilitySystem.Grant(entity, 1)`. 데모 가시성 위해 전체 부여(플레이어·AI 적). 캐릭터별 어빌리티 셋(마스터데이터 매핑)은 후속.
5. **DI 등록:** `AbilitySystem` 양쪽 신규(`Lifetime.Singleton`). **서버는 `GameFramework.World.ManaSystem`도 신규**(AbilitySystem ctor 의존, 현재 서버 미등록). 클라 ManaSystem은 이미 등록.
6. **currentTick** = 주입된 시간 facade(`Runner.Time.tick` — `Component/Action.cs`가 쓰는 그 경로)에서. `CharacterCreator`가 시간 소스를 주입받아 발동 틱에 전달. (정확한 주입 타입은 실행 시 확인 후 배선.)
7. **거동 영향:** 레거시 0(무손상). 새로: 모든 캐릭이 스폰 후 ~100틱 +30% 빠름 → 정상. 이게 의도된 *유일한* 가시 변화.
8. **검증:** 신규 어댑터 매핑은 **EditMode 가능하면**(Provider는 MasterData 의존이라 PlayMode/리플렉션 — 메모리 `client-test-infra-constraint`) 컴파일+플레이로 갈음. 핵심 검증 = 플레이에서 스폰 직후 빨라짐→복귀 육안 + NRE 0.

---

## Task 0: 피처 브랜치 (Client / Server)
- [ ] **Step 1: working tree 확인**
```bash
for r in LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; echo "branch: $(git -C C:/Users/re5na/workspace/LOP/$r branch --show-current)"; done
```
Expected: 둘 다 main, 픽스처 + 이번 plan(untracked)만. 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/haste-ability-end-to-end; done
```

---

## Task 1: Server — 어댑터 + 등록 + 부여·자동발동

> 서버 먼저(권위 측). 클라 Task 2는 동형(ManaSystem 등록만 제외).

- [ ] **Step 1: `StatusEffectDataProvider.cs` 생성** (Write, `Assets/Scripts/Game/` 또는 어댑터 폴더). Luban `TbStatusEffect` 행 → `StatusEffectData`. `[Inject] LOP.MasterData.LOPMasterData md`. 매핑:
```csharp
public StatusEffectData Get(int effectId)
{
    var r = md.Tables.TbStatusEffect.Get(effectId);
    var mods = string.IsNullOrEmpty(r.ModStatType)
        ? System.Array.Empty<StatusModifierSpec>()
        : new[] { new StatusModifierSpec(
            (int)System.Enum.Parse<GameFramework.World.EntityStatType>(r.ModStatType),
            r.ModValue,
            System.Enum.Parse<GameFramework.World.ModifierType>(r.ModType)) };
    return new StatusEffectData(r.Id,
        System.Enum.Parse<DurationPolicy>(r.DurationPolicy),
        r.DurationTicks, mods,
        System.Enum.Parse<StatusStackPolicy>(r.StackPolicy), r.MaxStacks);
}
```
> `ModifierType`/`EntityStatType`의 정확한 네임스페이스는 실행 시 확인(`GameFramework.World`). `Enum.Parse<T>`(string) 사용 — 표 값이 enum 이름과 일치(MoveSpeed/PercentAdd/Duration/Refresh).
- [ ] **Step 2: `AbilityDataProvider.cs` 생성** (Write). 하드코딩 헤이스트(3e에서 `TbAbility`로 교체):
```csharp
public AbilityData Get(int abilityId)
{
    // TEMP(3d): 하드코딩 헤이스트. 3e에서 TbAbility 조회로 교체(호출부 불변).
    return new AbilityData(abilityId, cooldownTicks: 0, mpCost: 0, castTimeTicks: 0,
        TargetingMode.Self, range: 0f, producesEffectIds: new[] { 1 });
}
```
- [ ] **Step 3: `GameLifetimeScope.cs` — `ManaSystem` + `AbilitySystem` + 어댑터 등록** (Edit). `StatusEffectSystem`(L25) 다음에:
```csharp
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
```
> `AbilitySystem` ctor = `(ManaSystem, StatusEffectSystem)` 둘 다 등록되어 자동 주입.
- [ ] **Step 4: `CharacterCreator.cs` — 부여 + 자동발동** (Edit). 시간 소스 + AbilitySystem + 어댑터 주입 추가, `worldEntity.Add(new StatusEffects());`(L87) 다음, `entityRegistry.Add(worldEntity);`(L85 직후 영역) **부여 시점**에:
```csharp
            // TEMP(3d): 스폰 시 헤이스트 부여 + 1회 자동발동 — 3e에서 실입력 발동으로 교체.
            abilitySystem.Grant(worldEntity, 1);
            var ability = abilityDataProvider.Get(1);
            var effects = new[] { statusEffectDataProvider.Get(1) };
            abilitySystem.TryActivate(worldEntity, ability, worldEntity, effects, /*currentTick*/ <time>.tick);
```
> 주입 필드: `[Inject] AbilitySystem abilitySystem; [Inject] AbilityDataProvider abilityDataProvider; [Inject] StatusEffectDataProvider statusEffectDataProvider;` + 시간 소스(`Runner.Time` 류 — 실행 시 정확 타입 확인). 발동 시점 = 엔티티가 `entityRegistry`에 등록되고 `Stats`/`Mana`/`Abilities`/`StatusEffects` 모두 부여된 *후*.
- [ ] **Step 5: 컴파일 검증** — Server `refresh_unity`(scope=all, force) → `read_console`(error) 0. 신규 2 `.cs.meta` 생성 확인.

---

## Task 2: Client — 어댑터 + 등록 + 부여·자동발동 (서버와 동형)

- [ ] **Step 1·2: 어댑터 2개 생성** (Write) — Task 1 Step 1·2와 동일 본문(클라 `MasterData.StatusEffect`엔 `Description` 있지만 매핑 안 씀 — 무관).
- [ ] **Step 3: `GameLifetimeScope.cs` — `AbilitySystem` + 어댑터 등록** (Edit). `StatusEffectSystem`(L35) 다음에:
```csharp
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
```
> 클라 `ManaSystem`은 이미 등록(L32) — 추가 안 함.
- [ ] **Step 4: `CharacterCreator.cs` — 부여 + 자동발동** (Edit). `worldEntity.Add(new StatusEffects());`(L95) 다음, Task 1 Step 4와 동일 배선(주입 + Grant + TryActivate). 클·서 양쪽 적용이 핵심(예측·권위 일치).
- [ ] **Step 5: 컴파일 검증** — Client `refresh_unity` → `read_console`(error) 0. 신규 `.cs.meta` 확인.

---

## Task 3: 종합 컴파일 + 인게임 검증
- [ ] **Step 1: 양쪽 재스캔** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) **0**.
- [ ] **Step 2: EditMode 회귀** — `run_tests`(`baegames.LOP.Shared.Tests.EditMode`) green 유지(LOP-Shared 무변경이라 기존 수치 그대로).
- [ ] **Step 3: 인게임(사용자)** — 입장 → 캐릭/적 스폰 직후 **눈에 띄게 빠르게 이동(+30%)하다가 ~100틱(≈5초) 후 정상 속도로 복귀**. 점프·전투·기존 액션(dash 등) *이전과 동일*. NRE/예외 0. 클·서 위치 일치(심한 러버밴드 없음).

---

## Task 4: 커밋 + 머지
- [ ] **Step 1: Server** — `git add` 신규 어댑터 2(.cs+.meta) + `GameLifetimeScope.cs` + `CharacterCreator.cs`(픽스처 제외).
```
feat(world): haste ability applied at spawn — first in-game ability/effect chain (Phase 3d)

Side-local adapters (StatusEffectDataProvider: TbStatusEffect->StatusEffectData;
AbilityDataProvider: hardcoded haste, TbAbility in 3e). Register AbilitySystem
(+server ManaSystem). CharacterCreator grants haste and self-casts once at spawn
(TEMP scaffold; real input/AI activation in 3e). Entities move +30% for ~100
ticks then revert. Legacy Action/Dash/Attack untouched.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: Client** — 동형(ManaSystem 등록 제외). 변경 파일만.
- [ ] **Step 3: Client 문서** — `git add docs/superpowers/plans/2026-06-26-haste-ability-end-to-end.md`.
- [ ] **Step 4: 머지(사용자 요청 시)** — Server → Client 순 `--no-ff`. 머지·push는 사용자 요청 시에만.

---

## Out of Scope (후속 슬라이스)
- **실입력/AI 발동 (3e):** 입력 actionCode→ability 라우팅(string↔int 해소), `EnemyBrain` 어빌리티화, `TbAbility` 테이블(`AbilityDataProvider` 내부 교체), 스폰 자동발동 스캐폴드 제거.
- **발동 cosmetic 와이어 + 쿨다운 UI (3e/3f):** "어빌리티 발동" 연출 이벤트(다른 클라에 표시) + 본인 쿨다운 스냅샷. 헤이스트 *효과*는 위치 스냅샷으로 이미 보이므로 3d는 와이어 0.
- **dash/attack/spawn → 어빌리티 이전:** 임펄스/데미지 효과 종류 신설 필요(stat-모디파이어로 안 매핑) — 별도 설계. spawn은 게임룰(어빌리티 아님, spec 확정).
- **legacy 은퇴 (3f):** `Action`/`Status`/`LOPActionManager`/`IActionManager`/`TbAction`.
- **캐릭터별 어빌리티 셋:** 마스터데이터 캐릭→어빌리티 매핑(현재 전체 헤이스트 부여).
- **클라 어빌리티 예측·롤백:** Stage④. 3d는 클·서 동일 적용으로 근사.

---

## Self-Review (작성자 체크)
- **3c→런타임 다리:** `StatusEffectDataProvider`가 `TbStatusEffect`(3c)를 `StatusEffectData`로 실매핑 → 헤이스트 데이터가 처음 소비됨. ✅
- **끝-끝 체인:** 부여→TryActivate→Apply→MoveSpeed 모디파이어(3b 배선)→이동 커널→위치 스냅샷. 모든 새 배선 운동. ✅
- **거동 보존:** 레거시 무손상(가산만). 유일 변화 = 스폰 후 한시적 가속(의도). ✅
- **클·서 정합:** 양쪽 동일 적용 + 양쪽 `LOPWorld.Mutation`(3a) 만료 → 예측·권위 일치, 러버밴드 최소. ✅
- **DI:** 서버 ManaSystem 공백 메움 + 양쪽 AbilitySystem·어댑터 등록. ctor 의존 충족. ✅
- **범위 정직:** 입력·AI·와이어·dash/attack·legacy 은퇴 전부 Out of Scope로 명시. 트리거=임시 스캐폴드 주석. ✅
- **표준 명명:** Provider(데이터 출처 어댑터, spec 어휘) / GAS 이음새 그대로. ✅
- **EOL/.meta/픽스처:** Write=신규/Edit=기존, .cs+.meta, 픽스처 제외. ✅
```
