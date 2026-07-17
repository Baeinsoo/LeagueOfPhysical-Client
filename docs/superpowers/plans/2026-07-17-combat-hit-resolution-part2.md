# 전투 히트 해소 Part 2 — 크리/회피 상수 MasterData 승격 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPCombatSystem`에 하드코딩된 회피/크리 확률·배수 상수(0.05/0.95, 0.05/0.50, 1.25/1.75)를 전역 MasterData 테이블 `TbCombatConfig`로 빼서, 밸런스를 Excel/Luban 데이터로 조정 가능하게 한다.

**Architecture:** 새 Luban 테이블 `TbCombatConfig`(단일 행, `mode=map` id=1, **서버 group `s`**)를 저작·생성 → 서버 MasterData 패키지에 `CombatConfig` 행 클래스 + `tbcombatconfig.bytes` 생성 → 서버 side-local `CombatConfigProvider`가 LOP-Shared `CombatConfig` struct로 매핑(`AbilityData` 패턴) → `LOPCombatSystem`이 `CombatConfig`를 주입받아 하드코딩 대신 사용.

**Tech Stack:** Luban 4.9.0(`dotnet Luban.dll`, 런타임 8.0.22 확인됨), python 3.14 + openpyxl 3.1.5(Excel 저작), C#(LOP-Shared/Server), VContainer(서버 DI), NUnit EditMode.

## Global Constraints

- **⚠️ spec 정련(Part 2 §): `TbCombatConfig` = 서버 group `s`(서버 전용)** — spec은 "클·서 공통 projection"이라 했으나, 실제 런타임은 `LOPCombatSystem`이 **서버에서만 등록/인스턴스화**(클라 combat 미소비, [[damage-prediction-dropped]])이고 파이프라인이 side별 테이블을 group으로 처리(SkinAsset=`c` 선례). → 클라 cruft 없이 서버 전용. (미래 클라 예측전투 착수 시 group을 blank로 flip + 클라 배선 추가.)
- **작업 저장소 4개**: `infrastructure`(테이블 저작), `LeagueOfPhysical-MasterData-Server`(생성물 + TableFiles), `LeagueOfPhysical-Shared`(CombatConfig struct + LOPCombatSystem), `LeagueOfPhysical-Server`(provider + DI). 피처 브랜치 `feature/combat-config-masterdata`.
- **MasterData 재생성 = `infrastructure/table/gen.sh`**(`dotnet Luban.dll`, 클·서 양쪽 target 생성). 클라 target도 돌지만 group `s`라 클라 패키지엔 `TbCombatConfig` 없음 → 클라 MasterData 패키지는 무변경(diff 없어야 함; 있으면 조사).
- **하드코딩 값 = config 기본값**: dodge_chance_min=0.05, dodge_chance_max=0.95, crit_chance_min=0.05, crit_chance_max=0.50, crit_mult_min=1.25, crit_mult_max=1.75. 동작 무변화(값 동일).
- **`TableFiles` 하드코딩 배열 갱신 필수**: `MasterData-Server/Runtime/Scripts/LOPMasterData.cs`의 `TableFiles`에 `"tbcombatconfig"` 추가 안 하면 로더가 새 테이블을 안 읽음(런타임 누락). 클라 LOPMasterData는 group `s`라 **안 건드림**.
- **Excel 저작 = python openpyxl 스크립트**(`infrastructure/table/_gen_scratch/author_ability.py` 패턴). 바이너리 직접 편집 금지 — 스크립트로 생성/갱신.
- **네임스페이스**: Luban 생성 `LOP.MasterData.CombatConfig`(행 타입) vs LOP-Shared `CombatConfig`(struct). 프로바이더가 MasterData 타입을 항상 `LOP.MasterData.` 풀 한정(기존 `AbilityDataProvider`와 동일) → 이름 충돌 없음.
- **EditMode**: UnityMCP `run_tests`(EditMode), `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`, 필터는 **풀 한정명**(`LOP.Tests.LOPCombatSystemTests`; 맨 클래스명은 0개 반환). 서버 컴파일/플레이 검증은 서버 에디터+사람 게이트(Task 3, 이 세션 미연결 시 deferred).

---

## 파일 구조

**infrastructure/table:**
- Create `_gen_scratch/author_combatconfig.py` — `#CombatConfig.xlsx`(embedded-schema 단일행) 생성 스크립트
- Create `Datas/#CombatConfig.xlsx` — 위 스크립트 산출
- Modify `Datas/__tables__.xlsx` — `TbCombatConfig` 행 추가(python 스크립트로)

**LeagueOfPhysical-MasterData-Server:**
- 재생성: `Runtime.Generated/Scripts/MasterData/CombatConfig.cs`, `Tables.cs`; `Runtime.Generated/StreamingAssets/MasterData/tbcombatconfig.bytes`
- Modify `Runtime/Scripts/LOPMasterData.cs` — `TableFiles`에 `"tbcombatconfig"` 추가

**LeagueOfPhysical-Shared:**
- Create `Runtime/Scripts/Game/CombatConfig.cs` — 순수 struct(6 float)
- Modify `Runtime/Scripts/Game/LOPCombatSystem.cs` — ctor에 `CombatConfig`, IsDodged/IsCritical/크리배수가 config 사용
- Test: `Tests/EditMode/LOPCombatSystemTests.cs`

**LeagueOfPhysical-Server:**
- Create `Assets/Scripts/Game/CombatConfigProvider.cs`
- Modify `Assets/Scripts/Game/GameLifetimeScope.cs` — provider + CombatConfig 등록

---

## Task 1: TbCombatConfig 저작 + 재생성 (infrastructure + MasterData-Server)

Luban 단일행 config 테이블을 저작·생성하고, 서버 로더 배열에 등록한다.

**Files:**
- Create: `infrastructure/table/_gen_scratch/author_combatconfig.py`, `infrastructure/table/Datas/#CombatConfig.xlsx`
- Modify: `infrastructure/table/Datas/__tables__.xlsx` (python)
- Regenerate: `LeagueOfPhysical-MasterData-Server/Runtime.Generated/{Scripts/MasterData,StreamingAssets/MasterData}/*`
- Modify: `LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs`

**Interfaces:**
- Produces: Luban-generated `LOP.MasterData.CombatConfig` (fields `DodgeChanceMin/Max`, `CritChanceMin/Max`, `CritMultMin/Max` — all float, PascalCase) accessible via `md.Tables.TbCombatConfig.Get(1)` (server package only).

- [ ] **Step 1: 브랜치 생성 (4 저장소)**

Run:
```bash
for r in infrastructure LeagueOfPhysical-MasterData-Server LeagueOfPhysical-Shared LeagueOfPhysical-Server; do
  git -C /c/Users/re5na/workspace/LOP/$r checkout -b feature/combat-config-masterdata
done
```
Expected: 각 `Switched to a new branch`.

- [ ] **Step 2: 저작 스크립트 작성**

Create `infrastructure/table/_gen_scratch/author_combatconfig.py`:
```python
# Datas/#CombatConfig.xlsx 를 Luban embedded-schema 단일행으로 author.
#   전역 전투 튜닝(회피/크리 확률·배수 clamp). mode=map(id=1) 단일행 — repo가 전부 map(one 미사용).
import os
from openpyxl import Workbook

OUT = os.path.join(os.path.dirname(__file__), "..", "Datas")

NAMES  = ["id", "dodge_chance_min", "dodge_chance_max",
          "crit_chance_min", "crit_chance_max", "crit_mult_min", "crit_mult_max"]
TYPES  = ["int", "float", "float", "float", "float", "float", "float"]
GROUPS = ["", "", "", "", "", "", ""]   # 전부 blank = 이 데이터 파일 컬럼은 테이블 group(s)을 따름
HUMAN  = list(NAMES)

# (id, dodge_min, dodge_max, crit_min, crit_max, mult_min, mult_max) — 현 하드코딩과 동일 값
ROWS = [
    (1, 0.05, 0.95, 0.05, 0.50, 1.25, 1.75),
]

wb = Workbook(); ws = wb.active
ws.append(["##var"]   + NAMES)
ws.append(["##type"]  + TYPES)
ws.append(["##group"] + GROUPS)
ws.append(["##"]      + HUMAN)
for r in ROWS:
    ws.append([""] + list(r))
wb.save(os.path.join(OUT, "#CombatConfig.xlsx"))
print("[OK] wrote Datas/#CombatConfig.xlsx")
```

- [ ] **Step 3: 데이터 파일 생성**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table/_gen_scratch && python author_combatconfig.py
```
Expected: `[OK] wrote Datas/#CombatConfig.xlsx`.

- [ ] **Step 4: __tables__.xlsx에 TbCombatConfig 행 추가**

Run (python 인라인 — 기존 행 보존하며 1행 append):
```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/__tables__.xlsx')
ws = wb.active
# 컬럼: full_name, value_type, read_schema_from_file, input, index, mode, group, comment, tags, output
ws.append([None, 'TbCombatConfig', 'CombatConfig', 'TRUE', '#CombatConfig.xlsx', 'id', 'map', 's', 'CombatConfig(전역 전투 튜닝, 서버 전용)', None, None])
wb.save('Datas/__tables__.xlsx')
print('[OK] appended TbCombatConfig (group s)')
"
```
Expected: `[OK] appended TbCombatConfig (group s)`.

- [ ] **Step 5: 재생성 실행**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh 2>&1 | tail -5
```
Expected: `[done]` (에러 없이). `dotnet Luban.dll`로 클·서 target 생성.

- [ ] **Step 6: 생성물 검증 (서버 有, 클라 無)**

Run:
```bash
cd /c/Users/re5na/workspace/LOP
echo "서버 CombatConfig.cs:"; ls LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/CombatConfig.cs
echo "서버 bytes:"; ls LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/tbcombatconfig.bytes
echo "서버 Tables.cs에 TbCombatConfig:"; grep -c "TbCombatConfig" LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/Tables.cs
echo "클라엔 없어야(group s):"; ls LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/CombatConfig.cs 2>&1 | grep -c "No such" || echo "0(있으면 문제)"
echo "클라 패키지 무변경 확인:"; git -C LeagueOfPhysical-MasterData-Client status --short | head
```
Expected: 서버 `CombatConfig.cs`·`tbcombatconfig.bytes` 존재, `Tables.cs`에 TbCombatConfig ≥1, 클라엔 CombatConfig.cs 없음, 클라 MasterData 패키지 git status 빈(또는 meta-only) — 실 변경 있으면 중단·조사(gen 재현성 이슈). 생성 필드명(`DodgeChanceMin` 등 PascalCase)을 `cat CombatConfig.cs`로 확인해 Task 2에 반영.

- [ ] **Step 7: 서버 LOPMasterData TableFiles에 tbcombatconfig 추가**

In `LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs`, `TableFiles` 배열에 항목 추가:
```csharp
        private static readonly string[] TableFiles =
        {
            "tbcharacter", "tbskin", "tbitem", "tbstatuseffect", "tbability", "tbcombatconfig"
        };
```

- [ ] **Step 8: Commit (infrastructure + MasterData-Server)**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && git add table/_gen_scratch/author_combatconfig.py table/Datas/#CombatConfig.xlsx table/Datas/__tables__.xlsx && git commit -m "feat(masterdata): TbCombatConfig 전역 전투 튜닝 테이블(서버 group s) 저작"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add Runtime.Generated/Scripts/MasterData Runtime.Generated/StreamingAssets/MasterData Runtime/Scripts/LOPMasterData.cs && git commit -m "feat(masterdata): TbCombatConfig 생성물 + TableFiles 로더 등록"
```
(생성물 `.meta` 포함 — `git add <dir>`가 신규 .meta도 스테이징. 서버 에디터 미연결이라 .meta는 다음 에디터 오픈 시 생성될 수 있음 → 그 경우 Task 3 서버 컴파일 게이트에서 함께 커밋.)

---

## Task 2: CombatConfig struct + LOPCombatSystem 사용 (LOP-Shared, TDD)

LOP-Shared에 순수 `CombatConfig` struct를 만들고, `LOPCombatSystem`이 그것을 주입받아 하드코딩 clamp/배수 대신 사용한다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/CombatConfig.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPCombatSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPCombatSystemTests.cs`

**Interfaces:**
- Consumes: (없음 — struct는 신규)
- Produces: `LOP.CombatConfig` struct `(float DodgeChanceMin, DodgeChanceMax, CritChanceMin, CritChanceMax, CritMultMin, CritMultMax)`; `LOPCombatSystem(WorldEventBuffer, HealthSystem, StatsSystem, CombatConfig)` — 4th 인자. `IsDodged`/`IsCritical`/크리배수가 config 사용. Task 3(서버 provider)이 사용.

- [ ] **Step 1: 실패 테스트 작성 (config가 확률에 반영)**

Add to `LeagueOfPhysical-Shared/Tests/EditMode/LOPCombatSystemTests.cs`. 먼저 `NewCombat` 헬퍼가 config를 받도록 갱신 + 신규 테스트:
```csharp
        // 기본 config = 현 하드코딩 값(동작 무변화)
        private static CombatConfig DefaultConfig()
            => new CombatConfig(0.05f, 0.95f, 0.05f, 0.50f, 1.25f, 1.75f);

        private static (WorldEventBuffer buf, LOPCombatSystem combat, StatsSystem stats) NewCombatCfg(CombatConfig cfg)
        {
            var buf = new WorldEventBuffer();
            var stats = new StatsSystem();
            var combat = new LOPCombatSystem(buf, new HealthSystem(), stats, cfg);
            return (buf, combat, stats);
        }

        [Test]
        public void Config_forces_always_dodge_when_min_is_one()
        {
            // dodge clamp [1,1] → 항상 회피
            var cfg = new CombatConfig(1f, 1f, 0.05f, 0.50f, 1.25f, 1.75f);
            var (buf, combat, stats) = NewCombatCfg(cfg);
            var target = Player("B", stats, 10, 10, 100);
            combat.Attack(Player("A", stats, 20, 10), target, 10, 5, 0, 12345UL, new AttackHitContext());
            var evt = (DamageDealtEvent)buf.Snapshot[0];
            Assert.IsTrue(evt.isDodged);
            Assert.AreEqual(0, evt.amount);
        }

        [Test]
        public void Config_forces_never_dodge_when_max_is_zero()
        {
            // dodge clamp [0,0] → 절대 회피 안 함
            var cfg = new CombatConfig(0f, 0f, 0.05f, 0.50f, 1.25f, 1.75f);
            var (buf, combat, stats) = NewCombatCfg(cfg);
            var target = Player("B", stats, 10, 10, 100);
            combat.Attack(Player("A", stats, 20, 10), target, 10, 5, 0, 12345UL, new AttackHitContext());
            var evt = (DamageDealtEvent)buf.Snapshot[0];
            Assert.IsFalse(evt.isDodged);
            Assert.Greater(evt.amount, 0);
        }
```
그리고 파일 상단 기존 `NewCombat()` 헬퍼를 `DefaultConfig()`를 넘기도록 갱신:
```csharp
        private static (WorldEventBuffer buf, LOPCombatSystem combat, StatsSystem stats) NewCombat()
            => NewCombatCfg(DefaultConfig());
```

- [ ] **Step 2: 테스트 실패 확인**

UnityMCP `run_tests`(EditMode, `test_names=LOP.Tests.LOPCombatSystemTests`, client 인스턴스).
Expected: FAIL — `CombatConfig` 타입 없음 / `LOPCombatSystem` 4-인자 ctor 없음(컴파일 에러).

- [ ] **Step 3: CombatConfig struct 구현**

Create `LeagueOfPhysical-Shared/Runtime/Scripts/Game/CombatConfig.cs`:
```csharp
namespace LOP
{
    /// <summary>전역 전투 튜닝(회피/크리 확률·배수 clamp). MasterData TbCombatConfig에서 side provider가 채워
    /// LOPCombatSystem에 주입. 순수 데이터 — Shared는 MasterData 패키지 비참조라 plain struct로 전달받는다.</summary>
    public readonly struct CombatConfig
    {
        public readonly float DodgeChanceMin;
        public readonly float DodgeChanceMax;
        public readonly float CritChanceMin;
        public readonly float CritChanceMax;
        public readonly float CritMultMin;
        public readonly float CritMultMax;

        public CombatConfig(float dodgeChanceMin, float dodgeChanceMax,
                            float critChanceMin, float critChanceMax,
                            float critMultMin, float critMultMax)
        {
            DodgeChanceMin = dodgeChanceMin;
            DodgeChanceMax = dodgeChanceMax;
            CritChanceMin = critChanceMin;
            CritChanceMax = critChanceMax;
            CritMultMin = critMultMin;
            CritMultMax = critMultMax;
        }
    }
}
```

- [ ] **Step 4: LOPCombatSystem이 config 사용**

In `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPCombatSystem.cs`:

(a) 필드 + ctor 4번째 인자 추가:
```csharp
        private readonly CombatConfig config;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem,
            CombatConfig config)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
            this.config = config;
        }
```

(b) `IsDodged`의 clamp를 config로:
```csharp
            dodgeChance = Mathf.Clamp(dodgeChance, config.DodgeChanceMin, config.DodgeChanceMax);
```

(c) `IsCritical`의 clamp를 config로:
```csharp
            critChance = Mathf.Clamp(critChance, config.CritChanceMin, config.CritChanceMax);
```

(d) `Attack`의 크리 배수를 config로:
```csharp
                    damage = Mathf.RoundToInt(damage * critRng.Range(config.CritMultMin, config.CritMultMax));
```

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, `test_names=LOP.Tests.LOPCombatSystemTests`, client 인스턴스) + 전체 Shared EditMode 그린.
Expected: 신규 2 PASS + 기존 회귀 없음(DefaultConfig가 기존 하드코딩과 동일 값이라 기존 결정론 테스트 유지).

- [ ] **Step 6: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/CombatConfig.cs Runtime/Scripts/Game/CombatConfig.cs.meta \
        Runtime/Scripts/Game/LOPCombatSystem.cs Tests/EditMode/LOPCombatSystemTests.cs
git commit -m "feat(combat): CombatConfig 주입 — 회피/크리 clamp·배수를 데이터 구동"
```

---

## Task 3: 서버 CombatConfigProvider + DI (LOP-Server)

서버 side-local 프로바이더가 `TbCombatConfig` 행(id=1)을 `CombatConfig`로 매핑하고, DI가 그것을 빌드해 `LOPCombatSystem`에 주입한다.

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/CombatConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.CombatConfig`(Task 2), `LOP.MasterData.CombatConfig`(Task 1 생성물), `md.Tables.TbCombatConfig.Get(1)`.
- Produces: `LOPCombatSystem`이 실제 MasterData 값으로 동작(현재 값과 동일 = 무변화, 이후 Excel 조정 가능).

- [ ] **Step 1: CombatConfigProvider 작성**

Create `LeagueOfPhysical-Server/Assets/Scripts/Game/CombatConfigProvider.cs`:
```csharp
using VContainer;

namespace LOP
{
    /// <summary>Luban <c>TbCombatConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="CombatConfig"/>로 매핑하는
    /// 서버 side-local 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="AbilityDataProvider"/> 대칭.)</summary>
    public class CombatConfigProvider
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public CombatConfig Get()
        {
            var r = md.Tables.TbCombatConfig.Get(1);
            return new CombatConfig(
                r.DodgeChanceMin, r.DodgeChanceMax,
                r.CritChanceMin, r.CritChanceMax,
                r.CritMultMin, r.CritMultMax);
        }
    }
}
```
(⚠️ Task 1 Step 6에서 확인한 실제 생성 필드명/접근자로 정정 — Luban `Get(int)` 또는 `GetOrDefault(int)`, 필드 PascalCase. `md.Tables.TbCombatConfig`의 정확한 접근 메서드는 생성된 `Tables.cs`/`TbCombatConfig` 클래스로 확인.)

- [ ] **Step 2: GameLifetimeScope에 등록**

In `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`, `LOPCombatSystem` 등록 근처에 추가(그 앞에):
```csharp
            builder.Register<CombatConfigProvider>(Lifetime.Singleton);
            builder.Register<CombatConfig>(c => c.Resolve<CombatConfigProvider>().Get(), Lifetime.Singleton);
```
`LOPCombatSystem` 등록은 그대로(`builder.Register<LOPCombatSystem>(Lifetime.Singleton)`) — VContainer가 새 4번째 인자 `CombatConfig`를 위 팩토리로 자동 해소. (팩토리는 `LOPCombatSystem` 첫 resolve 시 실행되며, 그 시점엔 md.LoadAsync 완료 후 게임 스코프라 `Tables` 준비됨 — Entrance에서 선로드.)

- [ ] **Step 3: 서버 컴파일 확인 (서버 에디터)**

서버 Unity 에디터 연결 시 `refresh_unity` + `read_console`(server 인스턴스)로 0 에러 + 생성 .meta 커밋 확인. **미연결 시 deferred** — 정적으로: provider가 `md.Tables.TbCombatConfig.Get(1)` 읽어 CombatConfig 반환, DI 팩토리가 LOPCombatSystem에 주입. Task 1의 생성 필드명과 일치해야 함.
Expected: 0 에러(또는 deferred).

- [ ] **Step 4: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/CombatConfigProvider.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(combat): 서버 CombatConfigProvider + DI — TbCombatConfig 주입"
```

- [ ] **Step 5: 플레이 검증 (서버 에디터 + 사람)**

클·서 동시 Play, 공격 반복: 회피/크리 빈도가 종전과 동일(값 무변화 확인). 이후 `#CombatConfig.xlsx`에서 dodge_chance_max를 0으로 바꿔 재생성→서버 재시작 시 회피가 사라지는지로 데이터 구동 확인(선택).
Expected: 동작 무변화 + 데이터 구동 성립. (서버 에디터+사람 필요 → 최종 human 게이트.)

---

## 완료 기준 (Definition of Done)

- [ ] `LOPCombatSystemTests` 신규 2(config 강제 dodge/no-dodge) + 기존 회귀 없음.
- [ ] `TbCombatConfig` 서버 MasterData 패키지에 생성(클라엔 없음), 서버 `TableFiles` 등록.
- [ ] `LOPCombatSystem`이 하드코딩 대신 `CombatConfig` 사용(기본값=기존 값이라 동작 무변화).
- [ ] 서버 클린 컴파일 + 플레이 회피/크리 빈도 무변화(human 게이트).
- [ ] 클라 MasterData 패키지 무변경(group s).

## 통합 / 머지

4 저장소(infrastructure/MasterData-Server/Shared/Server) 각 `feature/combat-config-masterdata`를 `--no-ff`로 main 머지. 순서: infrastructure → MasterData-Server → Shared → Server. 플레이 게이트 통과 후.

## 잔여 (후속)

- 넉백 Luban `Range/Angle` vestigial 정리(Part 1에서 C#·매퍼 미사용으로 남긴 것) — `__beans__.xlsx`의 KnockbackEffect bean에서 range/angle 제거 + `#Ability.xlsx` 넉백 셀 갱신 + 재생성. 이 Part 2에 함께 넣지 않음(별도 정리 — bean 편집은 KnockbackEffect 다형 bean 손대는 별개 리스크). 필요 시 후속.
