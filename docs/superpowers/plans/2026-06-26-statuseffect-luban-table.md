# StatusEffect Luban 테이블 (헤이스트 데이터 외부화) Implementation Plan (Phase 3c)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Phase 1에서 LOP-Shared 순수 C# 구조체로 만든 `StatusEffectData`를 **Luban 테이블 `TbStatusEffect`로 외부화**한다. 검증용 효과 = **헤이스트 1행**(이동속도 `MoveSpeed` +30% PercentAdd, 100틱 지속). 테이블을 클·서 MasterData 패키지로 생성·번들하고 런타임에 로드되게 한다. **읽는 코드는 아직 없음**(어댑터·발동은 3d) → **거동 변화 0**.

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 1: "Luban `TbStatusEffect` 테이블은 인게임 적용(배선) 단계로 미룸" — 그 단계가 이 슬라이스)
**선행:** Phase 1(StatusEffect 코어, `StatusEffectData` 구조체) · 3a(틱 스캐폴드) · 3b(이동→MoveSpeed 스탯) main 머지 완료.

**Architecture:** infrastructure/table에 source + 변환 등록 → `gen.sh`가 클·서 MasterData 패키지에 `StatusEffect.cs`/`TbStatusEffect.cs`/`Tables.cs`/`tbstatuseffect.bytes` 생성 → 각 패키지 래퍼 `LOPMasterData.cs`의 `TableFiles`에 로더 키 1줄 수동 추가. LOP 프로젝트(Client/Server Assembly-CSharp)·GameFramework·LOP-Shared **무변경**.

**Tech Stack:** Luban v4.9.0 / Excel-embedded(openpyxl) / dotnet / UnityMCP(클·서 컴파일·로드 검증).

---

## 레포 / 도구 참조

이 슬라이스는 **3개 레포**만 건드린다 (LOP 프로젝트 4종 무관):

- **infrastructure:** `C:\Users\re5na\workspace\LOP\infrastructure\table` — source/StatusEffect.xlsx(신규) + scripts/convert_source_to_luban.py(등록) + gen 실행
- **MasterData-Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-MasterData-Client` — 생성물 + `Runtime/Scripts/LOPMasterData.cs` TableFiles 수동 1줄
- **MasterData-Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-MasterData-Server` — 생성물(서버 projection) + `Runtime/Scripts/LOPMasterData.cs` TableFiles 수동 1줄
- **문서:** 이 plan은 Client 레포 `docs/`에 커밋.
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 `mcpforunity://instances` 재확인. 모든 호출에 `unity_instance` 명시.

**⚠️ 도구 주의:**
- `dotnet`이 bash PATH에 없음(`/c/Program Files/dotnet`에 존재). gen 실행 전 `export PATH="$PATH:/c/Program Files/dotnet"` 후 `./gen.sh`, 또는 `cmd //c gen.bat`.
- 생성물(`Tables.cs`/`StatusEffect.cs`/`TbStatusEffect.cs`/`*.bytes`)은 **Luban 생성 — 수기 편집 금지.** 수기 편집은 래퍼 `TableFiles` 1줄뿐.
- `.bytes`·신규 `.cs`의 `.meta`는 Unity `refresh_unity`가 생성 → `.cs`+`.cs.meta`, `.bytes`+`.bytes.meta` 함께 커밋.

**픽스처/working-tree 주의 (Task 0에서 기록):**
- MasterData-Client에 **이번 작업과 무관한 기존 `*.cs.meta` 수정분**이 떠 있음(`Action.cs.meta` 등 ~10개). 이 슬라이스가 만든 변경과 섞이지 않게 커밋 시 **명시 파일만 `git add`**. `git add -A`/`commit -am` 금지.

---

## 확정된 설계 결정 (실측 기반)

1. **테이블 컬럼 = 머지된 `StatusEffectData` 6필드에 정확히 맞춤** (spec의 Period/DoT 필드는 Phase 1 머지본에 **없음** — 넣지 않는다). 구조체:
   `int EffectId / DurationPolicy / long DurationTicks / StatusModifierSpec[] Modifiers / StatusStackPolicy / int MaxStacks`.
2. **인덱스 = `int id` (산업 표준 — 일탈 아님).** 마스터데이터 기본키=정수 id가 표준(TrinityCore item/spell/creature, Luban 관용 `TbItem.Get(12)`); 사람이 읽는 이름은 `name`/`code` *컬럼*. 코어가 이미 `EffectId`=int, `AbilityData.ProducesEffectIds`=int[]라 정합 → cross-ref churn 0. **기존 string `code` 테이블(Character/Action/Item/Skin)이 오히려 비표준 레거시**(지금 마이그레이션 안 함, 손댈 때 수렴). 규약 문서: `docs/lop-repo-topology.md` "마스터데이터 키 규약".
3. **enum은 string 컬럼**(`duration_policy`/`stack_policy`/`mod_type`/`mod_stat_type`) — `Action.category`가 string("Dash")인 기존 컨벤션 그대로. Luban enum/bean **신설 안 함**(`__enums__`/`__beans__` 비어 있음 유지). 파싱은 3d 어댑터.
4. **`Modifiers[]` = 인라인 단일 모디파이어**(`mod_stat_type`/`mod_value`/`mod_type` 3컬럼). 어떤 테이블도 list/bean 안 씀 + 헤이스트는 모디파이어 1개. 빈 `mod_stat_type` = 모디파이어 0개(3d 어댑터가 1원소 배열 or 빈 배열로 매핑). **다중 모디파이어는 실제 필요 시 bean 도입**(YAGNI, 이 slice 밖).
5. **그룹 분기**: 테이블 group="" (클·서 둘 다), `description` 필드만 group="c"(client-only) — `Action`/`Character`와 동형. 서버 `StatusEffect.cs`엔 `Description` 없음.
6. **범위 = 데이터 외부화만.** 어댑터(Luban→`StatusEffectData`)·발동·뷰·와이어 **전부 3d**. 3c는 *읽는 코드 0* → 헤이스트 행이 `.bytes`로 실려도 아무도 안 읽어 **거동 0**. 파이프라인(regen/bytes/로더키) 리스크를 게임플레이 배선과 분리해 단독 검증.
7. **검증** = ① 클·서 컴파일 0 에러, ② 마스터데이터 로드 성공 + `Tables.TbStatusEffect.Get(1)`이 헤이스트 행(mod_value 0.3 등) 반환(`execute_code` or 플레이 로그). 신규 EditMode 없음(LOP-Shared 무변경).

### 헤이스트 행 (검증 데이터)

| id | name | description | duration_policy | duration_ticks | mod_stat_type | mod_value | mod_type | stack_policy | max_stacks |
|---|---|---|---|---|---|---|---|---|---|
| 1 | haste | 이동속도 +30% 버프 | Duration | 100 | MoveSpeed | 0.3 | PercentAdd | Refresh | 1 |

> 컬럼 타입(source row1): `int, string, string, string, long, string, float, string, string, int`.
> `description`만 group `c`. duration_ticks 100·mod_value 0.3은 디자이너 튜닝 대상(임의 시드).

---

## Task 0: 브랜치 + working-tree 기록 (3 레포)

- [ ] **Step 1: 상태 확인 + 무관 churn 기록**
```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; echo "branch: $(git -C C:/Users/re5na/workspace/LOP/$r branch --show-current)"; done
```
Expected: 셋 다 main. infrastructure/Server 깨끗, **Client 패키지에 기존 `*.cs.meta` M 수정분(~10)** — 이번 작업과 무관. 이 목록을 기록해 두고, 커밋 시 *건드리지 않는다*(명시 파일만 add). 다른 예상 밖 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성** (3 레포)
```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/statuseffect-luban-table; done
```

---

## Task 1: infrastructure — source 테이블 + 변환 등록 + 생성

- [ ] **Step 1: `source/StatusEffect.xlsx` 생성** (openpyxl 스크립트). source 포맷 = row1 타입 / row2 이름 / row3+ 데이터 (선두 빈 컬럼 없음 — `read_source`는 col A부터 읽음).
```python
from openpyxl import Workbook
import os
SRC = r"C:\Users\re5na\workspace\LOP\infrastructure\table\source\StatusEffect.xlsx"
wb = Workbook(); ws = wb.active
ws.append(["int","string","string","string","long","string","float","string","string","int"])   # types
ws.append(["id","name","description","duration_policy","duration_ticks",
           "mod_stat_type","mod_value","mod_type","stack_policy","max_stacks"])                    # names
ws.append([1,"haste","이동속도 +30% 버프","Duration",100,"MoveSpeed",0.3,"PercentAdd","Refresh",1]) # haste
wb.save(SRC); print("[OK]", SRC)
```
- [ ] **Step 2: `scripts/convert_source_to_luban.py` — `TABLES`에 StatusEffect 등록** (Edit). `"Item": {...},` 다음(dict 끝)에:
```python
    "StatusEffect": {"value_type": "StatusEffect", "index": "id", "table_group": "",
                     "field_groups": {"description": "c"}},
```
> `FIELD_RENAME`은 불필요(예약어 컬럼 없음). index="id"(int).
- [ ] **Step 3: 변환 실행** — `Datas/#StatusEffect.xlsx` 생성 + `Datas/__tables__.xlsx`에 `TbStatusEffect` 행 추가.
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table && python scripts/convert_source_to_luban.py
```
Expected 로그에 `[OK] Datas/#StatusEffect.xlsx` + `[OK] Datas/__tables__.xlsx`. 확인:
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table/Datas && python -c "
from openpyxl import load_workbook
for f in ['#StatusEffect.xlsx','__tables__.xlsx']:
    ws=load_workbook(f,data_only=True).active; print('==',f,'=='); [print([('' if c is None else c) for c in r]) for r in ws.iter_rows(values_only=True)]"
```
`#StatusEffect.xlsx`에 `##var/##type/##group/##` 4행 + 헤이스트 1행, `##group`행의 description 위치만 `c`. `__tables__`에 `TbStatusEffect | StatusEffect | ... | #StatusEffect.xlsx | id | map | '' | StatusEffect`.
- [ ] **Step 4: Luban 코드/데이터 생성** — 양 패키지에 산출.
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table && export PATH="$PATH:/c/Program Files/dotnet" && ./gen.sh
```
Expected: `[gen] target=client` / `target=server` / `[done]` 무에러. (실패 시 `cmd //c gen.bat` 대안.)
- [ ] **Step 5: 생성물 확인** — 클·서 패키지에 신규 파일.
```bash
for s in Client Server; do echo "== $s =="; \
 ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-$s/Runtime.Generated/Scripts/MasterData/ | grep -i status; \
 ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-$s/Runtime.Generated/StreamingAssets/MasterData/ | grep -i status; done
```
Expected: 각 패키지에 `StatusEffect.cs`, `TbStatusEffect.cs`, `tbstatuseffect.bytes` + `Tables.cs`에 `TbStatusEffect` 프로퍼티 추가됨. 서버 `StatusEffect.cs`엔 `Description` 필드 **없음**(group c 제외) 확인.
- [ ] **Step 6: 회귀 점검** — 기존 테이블 산출물이 의미 변경 없는지. `git -C <each MasterData repo> status --short`로 변경 파일이 **StatusEffect 신규 + Tables.cs + 기존 .cs의 재생성(내용 동일이면 무변경)**에 한정되는지 확인. 기존 `Action.cs` 등 본문이 바뀌면 멈추고 조사(예상은 무변경 — 동일 입력 재생성).

---

## Task 2: MasterData-Client — 로더 키 추가

- [ ] **Step 1: `Runtime/Scripts/LOPMasterData.cs` — `TableFiles`에 `"tbstatuseffect"`** (Edit). 배열을:
```csharp
            "tbcharacter", "tbskin", "tbskinasset", "tbaction", "tbitem", "tbstatuseffect"
```
> 키는 `Tables.cs`의 `loader("tbstatuseffect")` + `.bytes` 스템과 정확히 일치해야 함(Task 1이 생성).
- [ ] **Step 2: 컴파일 + .meta 생성** — Client `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) 0. 신규 `StatusEffect.cs.meta`/`TbStatusEffect.cs.meta`/`tbstatuseffect.bytes.meta` 생성 확인.

---

## Task 3: MasterData-Server — 로더 키 추가 (클라와 동형)

- [ ] **Step 1: `Runtime/Scripts/LOPMasterData.cs` — `TableFiles`에 `"tbstatuseffect"`** (Edit). 서버 배열(skinasset 없음)을:
```csharp
            "tbcharacter", "tbskin", "tbaction", "tbitem", "tbstatuseffect"
```
- [ ] **Step 2: 컴파일 + .meta 생성** — Server `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) 0. 서버 신규 `.meta` 생성 확인.

---

## Task 4: 로드 검증 (클·서)

- [ ] **Step 1: 마스터데이터 로드 + 행 조회** — 각 인스턴스에서 `execute_code`로 테이블이 로드·조회되는지(또는 플레이 진입 후 로그). 검증 코드(개략, 실제는 DI 싱글톤/로드 시점에 맞춰 보정):
```csharp
// 진입(LoadMasterDataComponent.LoadAsync) 이후 시점에서
var md = /* resolve LOP.MasterData.LOPMasterData */;
var e = md.Tables.TbStatusEffect.Get(1);
UnityEngine.Debug.Log($"[3c] haste mod={e.ModStatType} {e.ModValue} {e.ModType} dur={e.DurationTicks} stack={e.StackPolicy}");
```
Expected 로그: `MoveSpeed 0.3 PercentAdd dur=100 stack=Refresh`. (서버는 `Description` 미보유 — 조회엔 무관.)
> `execute_code`로 로드 시점 재현이 어려우면, **플레이 진입 무회귀**(NRE 0, 기존 캐릭/액션/아이템 정상)로 갈음하고 행 조회는 다음 슬라이스(3d 어댑터)에서 자연 검증. 단 **로드 자체가 새 `.bytes`에서 실패하지 않음**은 반드시 확인(TableFiles 키 불일치 시 로드 예외).
- [ ] **Step 2: 무회귀** — 입장→플레이→종료에서 이동/점프/전투/스폰/디스폰·기존 마스터데이터 의존(캐릭터 Speed, 액션 등)이 *이전과 동일*. 거동 변화·예외 0(테이블을 읽는 코드 없음).

---

## Task 5: 커밋 (레포별)

> 각 레포 **명시 파일만 add**. MasterData-Client의 무관 `*.cs.meta` 기존 수정분은 **제외**(Task 0 기록과 대조).

- [ ] **Step 1: infrastructure** — `git add table/source/StatusEffect.xlsx table/scripts/convert_source_to_luban.py table/Datas/#StatusEffect.xlsx table/Datas/__tables__.xlsx`
```
feat(masterdata): add StatusEffect Luban table (haste row) [Phase 3c]

source/StatusEffect.xlsx + convert registration -> Datas/#StatusEffect.xlsx
and __tables__ entry. int `id` index (matches core int EffectId), string
enum columns + inline single modifier (project convention). One haste row
(MoveSpeed +30% PercentAdd, 100 ticks) for verification.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: MasterData-Client** — 생성물 + 래퍼. `git add Runtime/Scripts/LOPMasterData.cs Runtime.Generated/Scripts/MasterData/StatusEffect.cs* Runtime.Generated/Scripts/MasterData/TbStatusEffect.cs* Runtime.Generated/Scripts/MasterData/Tables.cs Runtime.Generated/StreamingAssets/MasterData/tbstatuseffect.bytes*`
  - 만약 Task 1 Step 6에서 기존 테이블 `.cs`가 재생성으로 *동일 내용*이면 무변경(add 불필요). 변경됐다면 조사 후 포함 여부 결정.
  - `Tables.cs.meta` 등 무관 기존 수정분은 **add 안 함**.
```
feat(masterdata): generate TbStatusEffect + register loader key [Phase 3c]

Luban-generated StatusEffect/TbStatusEffect/Tables + tbstatuseffect.bytes;
add "tbstatuseffect" to LOPMasterData.TableFiles. Table loads at runtime;
no consumer yet (adapter + activation in 3d) -> behavior unchanged.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: MasterData-Server** — Client Step 2와 동형(서버 projection, Description 없음). 변경 파일만.
- [ ] **Step 4: Client 문서** — `git add docs/superpowers/plans/2026-06-26-statuseffect-luban-table.md` (Client 레포에서 커밋; 픽스처 Room.unity/ProjectSettings 제외).

---

## Task 6: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 클·서 `read_console` 에러 0.
- [ ] **Step 2: 로드 확인(사용자)** — 입장 시 마스터데이터 로드 성공(NRE 0), 기존 거동 동일. (헤이스트는 아직 발동 안 함 — 정상.)
- [ ] **Step 3: 머지/푸시(사용자 요청 시)** — infrastructure → MasterData-Client → MasterData-Server → Client(문서) 순. `--no-ff`. 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지(Phase 1 "Luban 테이블은 인게임 적용 단계로 미룸"):** source+등록=Task1 / 생성=Task1 / 클·서 로더키=Task2·3 / 검증=Task4. ✅
- **거동 보존:** 테이블을 읽는 코드 0(어댑터·발동=3d). `.bytes`만 추가 로드 → inert. 런타임 거동 0. ✅
- **구조체 정합:** 컬럼 = 머지된 `StatusEffectData` 6필드(Period/DoT 없음 — spec과 달리 머지본 기준). ✅
- **컨벤션 일관:** enum=string 컬럼(`Action.category` 패턴), Luban enum/bean 신설 없음, description=group c. 인덱스 int=산업 표준(레거시 string code가 비표준 — 토폴로지 문서에 규약 박제). ✅
- **모디파이어:** 인라인 단일(헤이스트 1개). 다중=bean 도입 YAGNI. ✅
- **레포 격리:** infrastructure + MasterData 클·서만. GameFramework/LOP-Shared/Client·Server 프로젝트 무변경. ✅
- **도구/픽스처:** dotnet PATH 주의, 생성물 수기편집 금지(래퍼 1줄만), 무관 .meta churn 제외, .cs+.meta·.bytes+.meta 함께. ✅
- **검증 정직:** `execute_code` 로드시점 재현 난도 → 행 조회 어려우면 무회귀+로드무예외로 갈음, 정밀 조회는 3d. ✅
```
