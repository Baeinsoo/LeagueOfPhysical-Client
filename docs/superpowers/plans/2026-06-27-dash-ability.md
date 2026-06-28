# Dash 어빌리티 Implementation Plan (dash/attack 어빌리티화 Slice 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 레거시 `Dash`(액션 컴포넌트, 전방 단발 임펄스)를 **어빌리티의 Active 창 behavior**로 옮긴다 — 페이즈 머신(Slice 1)의 **Active 페이즈를 side가 폴해 임펄스를 발화**하는 첫 구현. dash는 `TbAbility` 행(id 2)으로 데이터화하고, 신규 side `AbilityImpulseSystem`이 매 틱 `ActiveAbility.Phase==Active` & `impulse_force>0`인 엔티티에 `AddForce`(전방). GamePad DASH 버튼을 새 어빌리티 id로 재배선. **결정론 코어 상태 폴 → side 발화**(spec의 콜백-없는 폴 방식 첫 구현).

**Spec:** `docs/superpowers/specs/2026-06-27-ability-phase-machine-design.md` (Slice 2 = Dash). **선행:** Slice 1(페이즈 머신) main 머지. 3e(TbAbility·id 입력).

**Architecture:** **임펄스는 물리(side) 개념** → 결정론 공유코어 `AbilityData`에 넣지 않음(LOP-Shared 무변경). `TbAbility`에 frame(startup/active/recovery, → `AbilityData` via provider) + `impulse_force`(side가 직접 읽음) 컬럼 추가. side `AbilityImpulseSystem`이 `LOPRunner`의 `world.Tick`(페이즈 전진) **후** · `SimulatePhysics` **전**에 임펄스 적용. 클·서 양쪽(예측+권위, 점프 패턴).

**Tech Stack:** Luban / Unity / C# / VContainer / UnityMCP.

---

## 레포 / 도구 참조 (5 레포 — LOP-Shared 무변경)
- **infra:** `.../infrastructure/table` — `source/Ability.xlsx` 컬럼+dash 행 + gen
- **MasterData-Client/Server:** 생성물 regen (로더/래퍼 무변경 — 테이블 이미 등록)
- **Client/Server:** `AbilityDataProvider`(frame 컬럼 읽기) + 신규 `AbilityImpulseSystem` + `GameLifetimeScope` 등록 + `LOPRunner` 배선 + `CharacterCreator` dash grant; **Client만** GamePad DASH 버튼 재배선
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`).

**⚠️ 도구:** `dotnet`은 `/c/Program Files/dotnet`(bash PATH 밖). gen 후 `rm -rf` `.meta` 삭제→**refresh 전 `git checkout` 복원**(3c 절차) + openpyxl `#*.xlsx` churn 복원. 생성물 수기편집 금지.
**픽스처:** `git add -A` 금지. (Client: Room.unity/ProjectSettings. Server: Room.unity/ConfigureRoomComponent/GameRuleSystem.)

---

## 확정된 설계 결정
1. **dash = 전방 단발 임펄스**(레거시 `dash_001` Duration0 = AddForce 1회 재현). 프레임 `startup0/active1/recovery0`(haste와 같은 타이밍), behavior=임펄스. 지속 대시는 `active_ticks` 늘리면 데이터로(impulse 매 active 틱).
2. **frame 컬럼 외부화(이 슬라이스에서 시작)**: `TbAbility`의 `cast_time_ticks` → **`startup_ticks`** 리네임 + **`active_ticks`/`recovery_ticks`** 추가 → `AbilityDataProvider.Map`이 실제 읽음(하드코딩 `0/1/0` 졸업). haste 행=`0/1/0`(거동 동일).
3. **`impulse_force` = side 전용 컬럼**(`TbAbility`). 물리는 결정론 공유 sim이 아님(LOPWorld는 물리 안 함; 점프도 host-side) → **`AbilityData`(공유 코어)에 안 넣음**. `AbilityImpulseSystem`이 `md.Tables.TbAbility.Get(id).ImpulseForce`를 직접 읽음. **LOP-Shared 무변경.**
4. **side 발화 = `AbilityImpulseSystem`**(클·서 동일): 매 틱 `entityManager.GetEntities<LOPEntity>()` 순회 → `entityRegistry.Get(id).Get<Abilities>().ActiveAbility`가 `Phase==Active` & 그 어빌리티 `ImpulseForce>0` → `pc.entityRigidbody.AddForce(forward*force, Impulse)`. `world.Tick` 후 / `SimulatePhysics` 전 호출.
5. **dash grant**: `CharacterCreator`(클·서)가 dash(id 2)도 `Grant`(haste처럼 전체, TEMP).
6. **트리거**: GamePad `Dash()` → `SetAbilityId(2)`(현 `SetActionCode("dash_001")` 레거시 대체). 레거시 `Dash`/`dash_001`은 더 이상 트리거 안 됨(3f 제거).
7. **dash 행**: id2/code"dash"/cooldown_ticks(placeholder, 튜닝)/0·1·0/targeting"Direction"/produces 0/impulse 10.

---

## Task 0: 브랜치 (5 레포)
- [ ] **Step 1: 상태 확인** — 5 레포 main, 픽스처 외 깨끗.
- [ ] **Step 2: 브랜치**
```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/dash-ability; done
```

---

## Task 1: infra — TbAbility 컬럼 + dash 행 + gen
- [ ] **Step 1: `source/Ability.xlsx` 재작성** (openpyxl). 컬럼: `id,code,name,description,cooldown_ticks,mp_cost,startup_ticks,active_ticks,recovery_ticks,targeting_mode,range,produces_effect_id,impulse_force`. 타입: `int,string,string,string,long,int,long,long,long,string,float,int,float`. 행:
  - haste: `1,haste,haste,"이동속도 +30% 버프 발동",0,0,0,1,0,Self,0,1,0`
  - dash: `2,dash,dash,"전방 대시",12,0,0,1,0,Direction,0,0,10`
```python
from openpyxl import Workbook; import os
wb=Workbook(); ws=wb.active
ws.append(["int","string","string","string","long","int","long","long","long","string","float","int","float"])
ws.append(["id","code","name","description","cooldown_ticks","mp_cost","startup_ticks","active_ticks","recovery_ticks","targeting_mode","range","produces_effect_id","impulse_force"])
ws.append([1,"haste","haste","이동속도 +30% 버프 발동",0,0,0,1,0,"Self",0,1,0])
ws.append([2,"dash","dash","전방 대시",12,0,0,1,0,"Direction",0,0,10])
wb.save(os.path.join("source","Ability.xlsx")); print("[OK]")
```
> convert 스크립트 `TABLES`의 Ability 엔트리(index id, field_groups description=c)는 그대로. 컬럼만 바뀜.
- [ ] **Step 2: 변환 + gen** — `python scripts/convert_source_to_luban.py` → `export PATH="$PATH:/c/Program Files/dotnet" && ./gen.sh`.
- [ ] **Step 3: 생성 확인** — 클·서 `Ability.cs`에 `StartupTicks`/`ActiveTicks`/`RecoveryTicks`(was CastTimeTicks)·`ImpulseForce` 필드. dash 행은 `tbability.bytes`에 `dash`/`Direction` 직렬화.
- [ ] **Step 4: churn 복원** — 삭제된 `.meta`(D) + 무관 `#*.xlsx` 바이트 churn `git checkout` 복원. 남는 변경 = `__tables__` 무변경(컬럼만)·`#Ability.xlsx`·`source/Ability.xlsx`·각 패키지 `Ability.cs`/`tbability.bytes`(+Tables.cs 무변경).

---

## Task 2: Client — provider + 임펄스 시스템 + 배선 + 버튼 + grant
- [ ] **Step 1: `AbilityDataProvider.Map` — frame 컬럼 읽기** (Edit). 하드코딩 `0/1/0` → `r.StartupTicks/r.ActiveTicks/r.RecoveryTicks`:
```csharp
            return new AbilityData(r.Id, r.CooldownTicks, r.MpCost,
                r.StartupTicks, r.ActiveTicks, r.RecoveryTicks,
                targeting, r.Range, effects);
```
> impulse는 `AbilityData`에 없음(side 직접). frame만 graduate.
- [ ] **Step 2: `AbilityImpulseSystem.cs` 생성** (Write, `Assets/Scripts/Game/`):
```csharp
using GameFramework.World;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// Active 페이즈인 어빌리티 중 impulse_force>0인 것에 전방 임펄스를 적용하는 side 시스템.
    /// 결정론 코어(ActiveAbility 페이즈)를 폴해 발화 — 점프처럼 클·서 양쪽 적용(예측+권위).
    /// world.Tick(페이즈 전진) 후 / 물리 sim 전에 매 틱 호출.
    /// </summary>
    public class AbilityImpulseSystem
    {
        [Inject] private IEntityManager entityManager;
        [Inject] private EntityRegistry entityRegistry;
        [Inject] private LOP.MasterData.LOPMasterData md;

        public void ApplyImpulses(long currentTick)
        {
            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                var abilities = entityRegistry.Get(entity.entityId)?.Get<Abilities>();
                var active = abilities?.ActiveAbility;
                if (active == null || active.Value.Phase != AbilityPhase.Active)
                {
                    continue;
                }

                float force = md.Tables.TbAbility.GetOrDefault(active.Value.AbilityId)?.ImpulseForce ?? 0f;
                if (force <= 0f)
                {
                    continue;
                }
                if (entity.TryGetEntityComponent<PhysicsComponent>(out var pc) == false)
                {
                    continue;
                }

                Vector3 forward = Quaternion.Euler(entity.rotation) * Vector3.forward;
                pc.entityRigidbody.AddForce(forward * force, ForceMode.Impulse);
            }
        }
    }
}
```
> `IEntityManager.GetEntities<LOPEntity>()`·`entity.entityId`·`entity.rotation`(Vector3 euler)·`entity.TryGetEntityComponent<PhysicsComponent>`·`pc.entityRigidbody` 실측 확인됨(레거시 Dash·서버 LOPRunner 동일 패턴). `currentTick`은 시그니처 보존용(현재 미사용 — 폴은 페이즈 상태로 충분).
- [ ] **Step 3: `GameLifetimeScope.cs` — 등록** (Edit). `AbilityActivator` 다음:
```csharp
            builder.Register<AbilityImpulseSystem>(Lifetime.Singleton);
```
- [ ] **Step 4: `LOPRunner.cs` — 배선** (Edit). `[Inject] AbilityImpulseSystem abilityImpulseSystem;` 추가, `UpdateRunner`에서 `world.Tick(...)`(L93)와 `SimulatePhysics()`(L95) **사이**:
```csharp
            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
            abilityImpulseSystem.ApplyImpulses(Runner.Time.tick);   // Active 창 임펄스 — 물리 sim 전
            SimulatePhysics();
```
- [ ] **Step 5: `CharacterCreator.cs` — dash grant** (Edit). `abilitySystem.Grant(worldEntity, 1);` 다음:
```csharp
            abilitySystem.Grant(worldEntity, 2);   // dash (TEMP 전체 부여)
```
- [ ] **Step 6: `GamePadViewModel.cs` — DASH 버튼 재배선** (Edit). `HasteAbilityId` 옆에 `private const int DashAbilityId = 2;`, `Dash()` 변경:
```csharp
        public void Dash() => _playerInputManager.SetAbilityId(DashAbilityId);
```
- [ ] **Step 7: 컴파일** — Client `refresh_unity`(scope=all, force) → error 0. 신규 `.cs.meta` 확인.

---

## Task 3: Server — provider + 임펄스 시스템 + 배선 + grant (버튼 없음)
- [ ] **Step 1: `AbilityDataProvider.Map`** — Task 2 Step 1과 동일.
- [ ] **Step 2: `AbilityImpulseSystem.cs`** — Task 2 Step 2와 동일 본문(서버 `LOPEntity`/`PhysicsComponent` 동형).
- [ ] **Step 3: `GameLifetimeScope.cs` 등록** — 동일.
- [ ] **Step 4: `LOPRunner.cs` 배선** — `world.Tick`(L118)와 `SimulatePhysics()`(L120) 사이에 `abilityImpulseSystem.ApplyImpulses(Runner.Time.tick);`. `[Inject] AbilityImpulseSystem` 추가.
- [ ] **Step 5: `CharacterCreator.cs` dash grant** — 동일.
- [ ] **Step 6: 컴파일** — Server `refresh_unity` → error 0.

---

## Task 4: 종합 검증
- [ ] **Step 1: 양쪽 재스캔** — error 0.
- [ ] **Step 2: EditMode** — `baegames.LOP.Shared.Tests.EditMode` green 유지(LOP-Shared 무변경이라 25 그대로).
- [ ] **Step 3: 플레이(사용자)** — **DASH 버튼 → 전방 대시(임펄스)**가 *이전(레거시)과 비슷*. HASTE도 정상. 이동/점프/전투 무회귀. NRE 0. (dash가 새 어빌리티 경로로 — 쿨다운/거동 체감.)

---

## Task 5: 커밋 + 머지
- [ ] infra → MasterData-Client → MasterData-Server → Server → Client(docs) 순. 명시 파일만(churn 복원 후). `--no-ff`, 사용자 요청 시.
- [ ] Client docs: 이 plan 추가.

---

## Out of Scope (후속)
- **Attack 어빌리티**(Slice 3) — active 진입 hit 검사(서버 combat)+타게팅. frame 컬럼은 이 슬라이스가 깔아둠(attack이 startup/active 실값 사용).
- **발동 cosmetic 와이어**(Slice 4) — dash/attack 애니. 현재 dash는 물리만(레거시도 dash 뷰 없음)이라 무관.
- **3f 레거시 은퇴** — `Dash.cs`/`dash_001`/`Action`/`LOPActionManager`/`TbAction`/`action_code`.
- **캐릭터별 로드아웃·AI dash·쿨다운 UI** — 잔여.

---

## Self-Review
- **spec 커버리지(Slice 2):** dash=TbAbility 행+impulse / Active 창 임펄스=AbilityImpulseSystem(폴) / frame 외부화 시작=provider graduate / 버튼 재배선. ✅
- **레이어:** 임펄스=side(물리, 비결정론 호스트)라 `AbilityData`(공유 결정론 코어) 밖 → **LOP-Shared 무변경**. frame=시뮬 타이밍이라 AbilityData. ✅
- **삽입 지점:** `world.Tick`(페이즈→Active) 후 / `SimulatePhysics` 전 — 같은 틱에 Active 폴 가능, 임펄스가 이 틱 물리에 적분. 클·서 동일. ✅
- **거동 보존:** haste 행 `0/1/0` 동일; dash=레거시 단발 임펄스(force 10) 재현. ✅
- **결정론/예측:** 페이즈 타이밍=공유 코어, 임펄스 발화=양쪽 폴(점프와 동일 예측). ✅
- **표준:** side가 결정론 상태(ActiveAbility) 폴해 behavior 발화 = spec 박제 방식(콜백 없음). ✅
- **churn/픽스처:** gen `.meta`·xlsx 복원, 생성물 수기편집 금지, 픽스처 제외. ✅
```
