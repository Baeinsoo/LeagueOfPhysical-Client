# 넉백 MasterData 승격 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 슬라이스 2가 남긴 넉백 TEMP 코드 배선(서버 `AbilityDataProvider` 하드코딩)을 정식 Luban MasterData 데이터(`KnockbackEffect` bean + `#Ability.xlsx` 데이터)로 승격한다.

**Architecture:** Luban 다형 effect 파이프라인에 `KnockbackEffect` bean을 추가(DamageEffect와 동형)하고, attack 어빌리티 데이터가 `[DamageEffect, KnockbackEffect]`를 담게 한다. bean 스키마의 진실원본은 convert 스크립트의 `write_beans_index()`(엑셀 `__beans__.xlsx`는 파생물), Ability 데이터의 진실원본은 hand-authored `#Ability.xlsx`. 재생성(gen.sh)이 양쪽 MasterData 패키지에 코드+bytes를 뽑고, 클·서 `AbilityDataProvider`가 새 case로 매핑하며 서버 TEMP를 제거한다. 도메인/핸들러/넷코드는 무변경.

**Tech Stack:** Luban 1.0.0(`infrastructure/table/tools/Luban`), Python 3 + openpyxl(스키마/데이터 엑셀 편집), dotnet(Luban 실행), C#/Unity, VContainer.

## Global Constraints

- **브랜치:** 5개 레포 모두 `feature/knockback-masterdata-promotion`(main 직접 커밋 금지). LOP-Client는 이미 생성됨(spec 커밋 있음).
- **진실원본:** effect bean = `infrastructure/table/scripts/convert_source_to_luban.py`의 `write_beans_index()` (엑셀 `Datas/__beans__.xlsx`는 파생 — 직접 편집 금지). Ability 데이터 = `Datas/#Ability.xlsx` 직접 편집(convert 미대상).
- **그룹 태그:** `KnockbackEffect` bean과 모든 필드는 **그룹 공란**(= 클·서 양쪽 생성). 기존 DamageEffect와 동일.
- **필드 순서 = 데이터 콤마 순서 = 생성 ctor 읽기 순서**, 셋이 반드시 일치: `strength`(float), `range`(float), `angle`(float), `duration_ticks`(int), `decay_per_tick`(float).
- **넉백 튜닝값(현행 유지, 무회귀):** strength=5, range=2, angle=90, duration_ticks=12, decay_per_tick=0.8.
- **구분자:** 현재 `effects#sep=,`는 Luban 다형 리스트를 **필드 개수로 소비**하므로(현 단일-effect 셀이 그 증거) effect 2개를 같은 콤마로 이어붙여도 파싱됨 → **컬럼 헤더 sep 변경 없음이 기본**. gen.sh가 파싱 실패 시에만 2단 구분자 폴백(Task 3).
- **regen 격리:** `__beans__.xlsx`만 타깃 재생성(전체 convert 실행 금지 — `source/` 기반 다른 테이블 건드림). regen 후 `git status`로 의도 파일만 변경됐는지 확인.
- **도메인/핸들러/넷코드 무변경:** LOP-Shared `KnockbackEffect`/`KnockbackEffectHandler`/`MotionContribution*`/Reconciler/wire — 슬라이스 2 그대로.
- **.meta:** 새 생성 `.cs`(`KnockbackEffect.cs`)의 `.meta`는 Unity가 refresh 시 생성 → 컨트롤러가 커밋.

경로 약칭: `INFRA=C:\Users\re5na\workspace\LOP\infrastructure\table`, `MDC=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-MasterData-Client`, `MDS=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-MasterData-Server`, `SERVER=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`, `CLIENT=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`.

Unity 인스턴스(컨트롤러 검증용): client=`LeagueOfPhysical-Client@de70658b9450cbb4`, server=`LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시 라이브 확인).

---

### Task 1: KnockbackEffect bean 스키마 추가 (infrastructure)

`write_beans_index()`에 KnockbackEffect bean을 DamageEffect와 동형으로 추가하고, `__beans__.xlsx`만 타깃 재생성한다.

**Files:**
- Modify: `INFRA\scripts\convert_source_to_luban.py` (`write_beans_index`, ~104행 뒤)
- Regenerate: `INFRA\Datas\__beans__.xlsx`

**Interfaces:**
- Produces: `LOP.MasterData.KnockbackEffect`(재생성 후, Task 3에서 gen) bean 정의 — 필드 `strength/range/angle/duration_ticks/decay_per_tick` 순서.

- [ ] **Step 1: write_beans_index에 KnockbackEffect bean 추가**

`convert_source_to_luban.py`에서 DamageEffect의 angle 연속행(`ws.append([..., "angle", "", "float", ...])`, 104행) **바로 뒤**, `ws.merge_cells("J1:P1")`(105행) **앞**에 삽입:

```python
    # KnockbackEffect: 다중 필드(strength/range/angle/duration_ticks/decay_per_tick). DamageEffect와 동형.
    ws.append(["", "KnockbackEffect", "AbilityEffect", "", "", "", "넉백 — 공격자 반대로 밀기(지수 감쇠)", "", "",
               "strength", "", "float", "", "", "", ""])
    ws.append(["", "", "", "", "", "", "", "", "",
               "range", "", "float", "", "", "", ""])
    ws.append(["", "", "", "", "", "", "", "", "",
               "angle", "", "float", "", "", "", ""])
    ws.append(["", "", "", "", "", "", "", "", "",
               "duration_ticks", "", "int", "", "", "", ""])
    ws.append(["", "", "", "", "", "", "", "", "",
               "decay_per_tick", "", "float", "", "", "", ""])
```

- [ ] **Step 2: __beans__.xlsx만 타깃 재생성**

전체 convert(main)는 `source/` 기반 다른 테이블도 재생성하므로 **write_beans_index만** 호출:

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python -c "import sys; sys.path.insert(0,'scripts'); import convert_source_to_luban as c; c.write_beans_index()"
```
Expected: 에러 없이 종료(`Datas/__beans__.xlsx` 재작성).

- [ ] **Step 3: 재생성 결과 검증**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python -c "from openpyxl import load_workbook; ws=load_workbook(r'Datas/__beans__.xlsx',data_only=True).active; [print(list(r)) for r in ws.iter_rows(values_only=True)]"
git status --short Datas/
```
Expected: 출력에 `KnockbackEffect` bean 행 + strength/range/angle/duration_ticks/decay_per_tick 필드 행 존재. `git status`는 `Datas/__beans__.xlsx`만 수정(다른 Datas 파일 변경 없음).

- [ ] **Step 4: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
git checkout -b feature/knockback-masterdata-promotion 2>/dev/null || git checkout feature/knockback-masterdata-promotion
git add scripts/convert_source_to_luban.py Datas/__beans__.xlsx
git commit -m "feat(masterdata): KnockbackEffect bean 추가 (write_beans_index + __beans__ 재생성)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: attack 어빌리티에 KnockbackEffect 데이터 추가 (infrastructure)

hand-authored `#Ability.xlsx`의 attack(id=3) effects 셀에 KnockbackEffect를 이어붙인다.

**Files:**
- Modify: `INFRA\Datas\#Ability.xlsx` (attack 행 effects 셀)

**Interfaces:**
- Consumes: Task 1의 `KnockbackEffect` bean 정의(필드 순서).
- Produces: attack 어빌리티가 effects=`[DamageEffect, KnockbackEffect]`를 담음(gen 후 Task 3에서 bytes로).

- [ ] **Step 1: attack effects 셀 편집**

현재 attack(id=3) effects = `DamageEffect,10,2,90`. KnockbackEffect를 같은 콤마로 이어붙인다(값=현행 튜닝):

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python - <<'PY'
from openpyxl import load_workbook
p = r"Datas/#Ability.xlsx"
wb = load_workbook(p); ws = wb.active
for row in ws.iter_rows(min_row=5):        # 데이터 행(헤더 4행 이후)
    if row[1].value == 3:                  # col B = id == 3 (attack)
        row[11].value = "DamageEffect,10,2,90,KnockbackEffect,5,2,90,12,0.8"  # col L = effects
        break
wb.save(p)
print("updated attack effects cell")
PY
```

- [ ] **Step 2: 편집 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python -c "from openpyxl import load_workbook; ws=load_workbook(r'Datas/#Ability.xlsx',data_only=True).active; print([list(r) for r in ws.iter_rows(values_only=True)][6])"
```
Expected: attack 행의 마지막 셀 = `DamageEffect,10,2,90,KnockbackEffect,5,2,90,12,0.8`. 다른 셀(id/code/description 등) 무변경.

- [ ] **Step 3: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
git add "Datas/#Ability.xlsx"
git commit -m "feat(masterdata): attack에 KnockbackEffect 데이터 (Damage + Knockback)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: 재생성 → MasterData 패키지 (gen.sh) + 구분자 경험적 확인

Luban 코드/데이터를 재생성해 양쪽 MasterData 패키지를 갱신한다. gen.sh 성공 = 다형 리스트가 단일 콤마로 2-effect 파싱됨(실패 시 2단 구분자 폴백).

**Files:**
- Regenerate → Create/Modify: `MDC\Runtime.Generated\Scripts\MasterData\KnockbackEffect.cs`(신규), `AbilityEffect.cs`(switch), `MDC\Runtime.Generated\StreamingAssets\MasterData\*.bytes`; 동일하게 `MDS\...`

**Interfaces:**
- Consumes: Task 1(bean), Task 2(데이터).
- Produces: 양쪽 패키지에 `LOP.MasterData.KnockbackEffect { float Strength; float Range; float Angle; int DurationTicks; float DecayPerTick; }` + `AbilityEffect.DeserializeAbilityEffect` switch에 `case KnockbackEffect.__ID__`.

- [ ] **Step 1: gen 실행**

`gen.sh`는 `dotnet tools/Luban/Luban.dll ...`를 client/server 두 타깃으로 실행해 양쪽 패키지에 출력한다. Git Bash에 `dotnet`이 없으면 PowerShell로 `gen.bat` 실행:

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table" && bash gen.sh
```
(dotnet 미탐지 시 PowerShell: `cd C:\Users\re5na\workspace\LOP\infrastructure\table; .\gen.bat`)

Expected: `[gen] target=client ...` / `[gen] target=server ...` / `[done]`, **에러 없이 종료**. **gen이 attack effects 파싱에 실패하면**(예: `KnockbackEffect`를 int 필드로 읽으려다 타입 에러) 여기서 에러 → Step 1b 폴백.

- [ ] **Step 1b: (폴백 — gen이 파싱 에러일 때만) 2단 구분자 적용**

gen이 attack effects 파싱 에러를 내면, 리스트 항목 구분자를 필드 구분자와 분리한다. `#Ability.xlsx` 헤더 셀 `effects#sep=,` → `effects#sep=,|`(추가 문자 `|`=리스트 구분자), attack 셀을 `DamageEffect,10,2,90|KnockbackEffect,5,2,90,12,0.8`로:

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python - <<'PY'
from openpyxl import load_workbook
p = r"Datas/#Ability.xlsx"; wb = load_workbook(p); ws = wb.active
# 헤더(0-indexed row0)의 effects 열(col L=index11) sep 확장
hdr = ws.cell(row=1, column=12)
if hdr.value == "effects#sep=,": hdr.value = "effects#sep=,|"
for row in ws.iter_rows(min_row=5):
    if row[1].value == 3:
        row[11].value = "DamageEffect,10,2,90|KnockbackEffect,5,2,90,12,0.8"
        break
wb.save(p); print("applied two-level sep fallback")
PY
cd "C:/Users/re5na/workspace/LOP/infrastructure/table" && bash gen.sh
```
Expected: gen 성공. (문자·순서가 이 Luban 버전과 안 맞으면 `,|`↔`|,` 교차 시도. 성공하면 이 `#Ability.xlsx` 변경도 Task 2 커밋에 amend 하거나 후속 커밋.)

- [ ] **Step 2: 생성물 검증**

```bash
ls "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/KnockbackEffect.cs"
ls "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/KnockbackEffect.cs"
grep -c "KnockbackEffect.__ID__" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/AbilityEffect.cs"
grep -E "Strength|Range|Angle|DurationTicks|DecayPerTick" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/KnockbackEffect.cs"
```
Expected: `KnockbackEffect.cs` 양쪽 패키지에 존재; `AbilityEffect.cs`에 `KnockbackEffect.__ID__` case 1개; `KnockbackEffect.cs`에 `Strength`(float)/`Range`(float)/`Angle`(float)/`DurationTicks`(int)/`DecayPerTick`(float) 프로퍼티 — **선언 순서가 strength→range→angle→duration_ticks→decay_per_tick** 인지 확인(ctor `ReadFloat/ReadFloat/ReadFloat/ReadInt/ReadFloat` 순서 = 데이터 콤마 순서와 일치).

- [ ] **Step 3: 생성 diff 범위 확인 (스퍼리어스 churn 없음)**

gen.sh는 `rm -rf` 후 전체 재생성하므로, 결정론 codegen이면 diff는 KnockbackEffect 관련만이어야 한다:

```bash
for R in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  echo "=== $R ==="; git -C "C:/Users/re5na/workspace/LOP/$R" status --short | head -30
done
```
Expected: 각 패키지에서 `KnockbackEffect.cs`(신규), `AbilityEffect.cs`(수정), `StreamingAssets/MasterData`의 attack 관련 `.bytes`(수정)만. 무관 파일이 대량 변경되면(codegen 비결정) 중단하고 원인 조사.

- [ ] **Step 4: Unity refresh로 .meta 생성 + 컴파일 확인 (컨트롤러)**

> 이 스텝은 컨트롤러가 UnityMCP로 수행(구현 subagent는 Unity 미실행). 클라 인스턴스 refresh → MasterData 패키지의 `KnockbackEffect.cs.meta` 생성 + Shared/클라 컴파일 확인. 서버 인스턴스도 동일.

Expected: 양쪽 Unity 컴파일 에러 0. `KnockbackEffect.cs.meta`가 양쪽 패키지에 생성됨.

- [ ] **Step 5: 두 MasterData 패키지 커밋 (.meta 포함)**

```bash
for R in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  cd "C:/Users/re5na/workspace/LOP/$R"
  git checkout -b feature/knockback-masterdata-promotion 2>/dev/null || git checkout feature/knockback-masterdata-promotion
  git add Runtime.Generated/
  git commit -m "chore(masterdata): KnockbackEffect 생성물 (Luban regen) + attack bytes

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
done
```
Expected: 양쪽 패키지에 각각 커밋 1개.

---

### Task 4: AbilityDataProvider 매핑 + 서버 TEMP 은퇴 (LOP-Server + LOP-Client)

런타임이 데이터의 `KnockbackEffect`를 도메인 `KnockbackEffect`로 매핑하게 하고, 서버의 하드코딩 TEMP를 제거한다.

**Files:**
- Modify: `SERVER\Assets\Scripts\Game\AbilityDataProvider.cs` (case 추가 + TEMP 제거)
- Modify: `CLIENT\Assets\Scripts\Game\AbilityDataProvider.cs` (case 추가)

**Interfaces:**
- Consumes: `LOP.MasterData.KnockbackEffect { Strength, Range, Angle, DurationTicks, DecayPerTick }`(Task 3), 도메인 `KnockbackEffect(float strength, float range, float angle, int durationTicks, float decayPerTick)`(LOP-Shared, 슬라이스 2).
- Produces: attack 어빌리티가 도메인 effect `[DamageEffect, KnockbackEffect]`로 매핑됨. 서버에 TEMP 하드코딩 없음.

- [ ] **Step 1: 서버 AbilityDataProvider — TEMP 제거 + case 추가**

`SERVER\Assets\Scripts\Game\AbilityDataProvider.cs`의 `MapEffects` switch에서 DamageEffect case의 TEMP 넉백 조립을 제거하고, 별도 KnockbackEffect case를 추가한다. 해당 case 블록을 아래로 교체:

```csharp
                    case LOP.MasterData.DamageEffect d:
                        result.Add(new DamageEffect(d.Amount, d.Range, d.Angle));
                        break;
                    case LOP.MasterData.KnockbackEffect k:
                        result.Add(new KnockbackEffect(k.Strength, k.Range, k.Angle, k.DurationTicks, k.DecayPerTick));
                        break;
```
(= DamageEffect case에서 TEMP `result.Add(new KnockbackEffect(strength: 5f, ...))` 두 줄 + TEMP 주석 삭제, 그 아래 KnockbackEffect case 신설.)

- [ ] **Step 2: 클라 AbilityDataProvider — case 추가**

`CLIENT\Assets\Scripts\Game\AbilityDataProvider.cs`의 `MapEffects` switch, `case LOP.MasterData.DamageEffect d:` 블록(53-55행) 바로 뒤에 추가:

```csharp
                    case LOP.MasterData.KnockbackEffect k:
                        result.Add(new KnockbackEffect(k.Strength, k.Range, k.Angle, k.DurationTicks, k.DecayPerTick));
                        break;
```
(클라 executor엔 KnockbackEffectHandler 없어 무시되지만, MasterData 양쪽 존재 + DamageEffect 선례대로 대칭 매핑.)

- [ ] **Step 3: 컴파일 확인 (컨트롤러)**

> 컨트롤러가 UnityMCP로 서버·클라 인스턴스 각각 refresh + `read_console`(error). Expected: 양쪽 에러 0.

- [ ] **Step 4: 커밋 (각 레포)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/knockback-masterdata-promotion 2>/dev/null || git checkout feature/knockback-masterdata-promotion
git add Assets/Scripts/Game/AbilityDataProvider.cs
git commit -m "refactor(knockback): 데이터 KnockbackEffect 매핑 + TEMP 하드코딩 은퇴 (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Game/AbilityDataProvider.cs
git commit -m "feat(knockback): 데이터 KnockbackEffect 매핑 (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: 끝-끝 플레이 검증

넉백이 이제 데이터에서 오고, 손맛이 슬라이스 2 TEMP와 동일하며 무회귀임을 확인한다.

**Files:** (없음 — 관측/검증)

- [ ] **Step 1: 서버·클라 Play + 로컬 룸 접속**

(메모리 [local-test-auth-fixture]: DB 리셋 시 서버 playerList에 현재 게스트 uuid 추가 후 재시작.) Expected: 스폰·이동 정상.

- [ ] **Step 2: attack 넉백 재현 (데이터 경로)**

대상을 attack으로 때린다 → **뒤로 밀렸다 감쇠**하는지. 슬라이스 2 TEMP(strength 5)와 **동일 손맛**인지 육안 확인. 값이 코드가 아니라 `#Ability.xlsx`에서 온다는 게 요지(코드엔 TEMP 없음).
Expected: 원격 대상 밀림·감쇠, 로컬 대상 피격 시 러버밴딩 없이 밀림(슬라이스 2와 동일).

- [ ] **Step 3: 무회귀 확인**

haste(StatusEffectApply)·dash(Motion) 어빌리티가 정상 발동(1-effect 어빌리티가 파싱 변경에 영향 없음), 걷기/정지/점프 정상.
Expected: 기존 어빌리티·이동 무회귀.

- [ ] **Step 4: 후기 기록 + 커밋**

`CLIENT\docs\superpowers\specs\2026-07-06-knockback-masterdata-promotion-design.md` 하단에 "구현 후기"(구분자 단일콤마/폴백 여부, 검증 결과) 추가 후 커밋.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add docs/superpowers/specs/2026-07-06-knockback-masterdata-promotion-design.md
git commit -m "docs(masterdata): 넉백 승격 구현 후기 + 검증 결과

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## 통합 / 머지

5개 레포(infrastructure, MasterData-Client, MasterData-Server, LOP-Server, LOP-Client)를 각자 `feature/knockback-masterdata-promotion`에서 작업. 순서: infra(Task 1·2) → gen(Task 3, MasterData 양쪽) → 런타임(Task 4) → 검증(Task 5). 플레이 통과 후 각 레포 main에 `--no-ff` 머지(superpowers:finishing-a-development-branch). file: 참조라 브랜치 상태로 검증 가능.

## Out of Scope (spec과 동일)

- 넉백 도메인/핸들러/넷코드(슬라이스 2 완료), attack 외 어빌리티에 넉백 추가, targeting 공유, Override 완전경직/CC, Y축 넉백, convert/`source/` 층 리팩터.

## Self-Review 결과

- **Spec 커버리지:** §A(bean)=Task 1, §B(구분자)=Task 2+Task 3 경험적 확인/폴백, §C(attack 데이터)=Task 2, §D(재생성)=Task 3, §E(매핑)=Task 4, §F(TEMP 은퇴)=Task 4 Step 1, 검증=Task 5. 전부 커버.
- **Placeholder:** 없음(구분자 폴백은 구체 명령+판정 기준 있는 경험적 절차라 placeholder 아님).
- **타입 일관:** `KnockbackEffect(strength,range,angle,durationTicks,decayPerTick)` 도메인 ctor ↔ MasterData `Strength/Range/Angle/DurationTicks/DecayPerTick` PascalCase 프로퍼티 ↔ bean 필드 순서 strength/range/angle/duration_ticks/decay_per_tick, 셋 전부 일치.

## 진행

- [ ] Task 1~5 구현
- [ ] 최종 리뷰 + 머지
