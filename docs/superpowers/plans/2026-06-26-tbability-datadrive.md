# 어빌리티 데이터 드리븐 (TbAbility 테이블) Implementation Plan (Phase 3e)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 3d의 더미 두 개 — 하드코딩 `AbilityDataProvider`(헤이스트 고정) + `AbilityActivator`의 `code=="haste"` 단일 매칭 — 을 **Luban `TbAbility` 테이블 데이터 드리븐**으로 정식화한다(3c가 StatusEffect에 한 것의 Ability 버전). 입력 actionCode를 `TbAbility`의 `code`로 조회해 발동하므로, 새 어빌리티는 **테이블 행 추가만으로** 발동 가능해지고, 나중에 dash/attack도 code로 자연 편입된다. **거동 보존**(헤이스트만 존재 → 3d와 동일하게 HASTE 버튼/H키로 발동).

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 2 `AbilityData` Luban 매핑 + Phase 3 컷오버). **선행:** 3c(`TbStatusEffect`)·3d(어댑터·`AbilityActivator`·버튼) main 머지.

**Architecture:** infrastructure에 `TbAbility` source+생성(3c와 동일 파이프라인) → 클·서 MasterData 패키지에 `Ability.cs`/`TbAbility.cs`/`Tables.cs`/`tbability.bytes` + 로더 키. `AbilityDataProvider`(클·서)가 `TbAbility` 조회로 전환(`Get(int id)` + `TryGetByCode(string)`). `AbilityActivator`(클·서)가 code→어빌리티 조회로 일반화(haste-only 제거). LOP-Shared/GameFramework **무변경**.

**Tech Stack:** Luban v4.9.0 / Excel-embedded(openpyxl) / dotnet / Unity / C# / UnityMCP.

---

## 레포 / 도구 참조 (5 레포)
- **infrastructure:** `.../infrastructure/table` — `source/Ability.xlsx`(신규) + convert 등록 + gen
- **MasterData-Client/Server:** 생성물 + `LOPMasterData.TableFiles`에 `"tbability"` 1줄
- **Client/Server:** `AbilityDataProvider`·`AbilityActivator` 데이터 드리븐 + **id 기반 입력 전환**(모델/입력매니저/뷰/Runner) + 문서(Client)
- **LOP-Shared:** proto `PlayerInput`에 `ability_id` 필드 1개 추가(+`PlayerInput.cs` 재생성). `AbilityData`/`AbilitySystem`은 무변경.
- **GameFramework:** **무변경**.
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) — 해시 변동 시 재확인. 모든 호출 `unity_instance` 명시.

**⚠️ 도구:** `dotnet`은 bash PATH 밖(`export PATH="$PATH:/c/Program Files/dotnet"` 후 `./gen.sh`). 생성물 수기편집 금지(래퍼 키 1줄만). gen 후 `rm -rf`로 기존 `.meta` 삭제(`D`)됨 → **refresh 전 `git checkout`으로 복원**(3c와 동일, GUID 안정). 무관 openpyxl `#*.xlsx` 바이트 churn도 복원.
**EOL:** mass sed 금지. 신규=Write/편집=Edit. `.cs`+`.cs.meta`·`.bytes`+`.bytes.meta` 함께.
**픽스처:** `git add -A` 금지. 명시 파일만. (Client: Room.unity/ProjectSettings. Server: Room.unity/ConfigureRoomComponent/GameRuleSystem.)

---

## 확정된 설계 결정

1. **`TbAbility` = 신규 테이블(레거시 `TbAction` 리네임 아님).** `TbAction`은 레거시 액션(dash/attack/spawn)이 *아직 쓰는* 테이블 → 손대지 않음(은퇴=3f). 새 어빌리티 시스템은 별도 `TbAbility`로 시작, 레거시는 3f에서 드레인.
2. **키 = int `id` PK + string `code` alias** ([[masterdata-key-convention]] — TrinityCore식 "int PK + 읽는 별칭"). `id`=코어 `AbilityId`/cross-ref용, `code`=입력 actionCode/디자이너용("haste"). 라우팅은 code, 코어는 id.
3. **컬럼 = `AbilityData` 필드 매핑 + code/name/description**:
   `id`(int,PK) · `code`(string) · `name`(string) · `description`(string,group c) · `cooldown_ticks`(long) · `mp_cost`(int) · `cast_time_ticks`(long) · `targeting_mode`(string) · `range`(float) · `produces_effect_id`(int — **인라인 단일**, 0=없음→빈 배열; 다중은 YAGNI, StatusEffect 모디파이어와 동형).
   헤이스트 행: `1, haste, haste, "이동속도 +30% 버프 발동", 0, 0, 0, Self, 0, 1`.
4. **발동은 int `id` 기반 (런타임 표준 — 사용자 지적 + 웹 리서치).** 입력/커맨드는 어빌리티 **id(int)** 를 실어 나르고, 런타임은 id로만 라우팅한다. string `code`는 **데이터/에디터 별칭**일 뿐 런타임 hot-path에 쓰지 않는다(GAS `AbilityLocalInputPressed(int32 InputID)`·MOBA/WoW int handle·netcode "문자열은 비용↑, 정수 식별자 사용"·"문자열은 결정을 런타임으로 미룬다 → 로드/바인드 시 id 해소"). 따라서:
   - **proto `PlayerInput`에 `int32 ability_id = 6` 추가**(LOP-Shared, `@auto_generate` 아님 → MessageId 무관, 필드 추가 = 저위험). `PlayerInput.proto`만 protoc 재생성(마스터 regen 미실행 = MessageId 보존).
   - 클 입력 모델 `PlayerInput`에 `int abilityId`. `PlayerInputManager.SetAbilityId(int)` + ProcessInput이 proto `AbilityId` 매핑. 버튼/H키 = `SetAbilityId(1)`(어빌리티 슬롯=id 설정; code→id는 *설정 시점* 해소, 런타임 매 발동 스캔 아님).
5. **`AbilityDataProvider` = id 조회**: `bool TryGet(int abilityId, out AbilityData)` = `md.Tables.TbAbility.GetOrDefault(id)` 매핑(`targeting_mode` `Enum.Parse`, `produces_effect_id`→`int[]`). (code 스캔 `TryGetByCode` 없음.)
   **`AbilityActivator` = id 라우팅**: `TryActivate(casterId, int abilityId, tick)` = `TryGet(id)` → `ProducesEffectIds`마다 `statusEffectDataProvider.Get`로 효과 배열 → `abilitySystem.TryActivate`.
   **입력 분기**(클 `ApplyInput`·서 `LOPRunner.ProcessInput`): `abilityId != 0` → `AbilityActivator`(어빌리티) / else `actionCode` → 레거시 `TryStartAction`(dash/attack). 두 채널 분리(전환기).
6. **Grant는 유지(전체 헤이스트 부여, 여전히 TEMP).** 캐릭터별 어빌리티 로드아웃(마스터데이터 캐릭→어빌리티 매핑)은 별도 슬라이스 — 이번 범위 밖(`abilitySystem.Grant(worldEntity, 1)` 그대로).
7. **거동 보존:** 헤이스트만 테이블에 존재 → HASTE 버튼/H키 발동 결과 3d와 동일. 다른 actionCode(dash 등)는 `TryGetByCode` 실패 → 레거시 폴백(무변경).
8. **검증:** 컴파일 0 + `.bytes`에 `haste`/`Self` 직렬화 확인 + 플레이에서 HASTE 발동 동일(+30%, 100틱). (execute_code 클라 환경 깨짐 — 3c와 동일.)

---

## Task 0: 브랜치 (5 레포)
- [ ] **Step 1: 상태 확인** — 5 레포 `git status --short`. infra/MasterData 깨끗(클라 MasterData는 기존 `.meta` EOL churn 가능 — 기록). Client/Server 픽스처 + plan. 예상 밖이면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/tbability-datadrive; done
```

---

## Task 1: infrastructure — TbAbility source + 생성

- [ ] **Step 1: `source/Ability.xlsx` 생성** (openpyxl). row1=타입 / row2=이름 / row3+=데이터:
```python
from openpyxl import Workbook
import os
wb = Workbook(); ws = wb.active
ws.append(["int","string","string","string","long","int","long","string","float","int"])
ws.append(["id","code","name","description","cooldown_ticks","mp_cost","cast_time_ticks","targeting_mode","range","produces_effect_id"])
ws.append([1,"haste","haste","이동속도 +30% 버프 발동",0,0,0,"Self",0,1])
wb.save(os.path.join("source","Ability.xlsx")); print("[OK]")
```
> ⚠️ 기존에 `source/Action.xlsx`(레거시)가 있음 — **별개 파일**(Ability ≠ Action). 덮어쓰지 말 것.
- [ ] **Step 2: convert 스크립트 등록** (Edit `scripts/convert_source_to_luban.py` `TABLES`):
```python
    "Ability":   {"value_type": "Ability", "index": "id", "table_group": "",
                  "field_groups": {"description": "c"}},
```
- [ ] **Step 3: 변환 실행** — `python scripts/convert_source_to_luban.py` → `Datas/#Ability.xlsx` + `__tables__`에 `TbAbility` 행. 확인(openpyxl dump): `id` index, `produces_effect_id` 등 컬럼, 헤이스트 행.
- [ ] **Step 4: gen 실행** — `export PATH="$PATH:/c/Program Files/dotnet" && ./gen.sh`. 클·서 패키지에 `Ability.cs`/`TbAbility.cs`(int 키) + `Tables.cs`(TbAbility 배선) + `tbability.bytes`. 서버 `Ability.cs`엔 `Description` 없음(group c).
- [ ] **Step 5: churn 복원** — gen이 삭제한 무관 `.meta`(`D`) + openpyxl `#*.xlsx` 바이트 churn을 `git checkout`으로 복원(3c 절차). 남는 변경 = `__tables__.xlsx` + `#Ability.xlsx`/`source/Ability.xlsx`(신규) + convert.py + 각 패키지 `Tables.cs`(M) + `Ability.cs`/`TbAbility.cs`/`tbability.bytes`(신규).

---

## Task 2: MasterData 패키지 — 로더 키 (클·서)
- [ ] **Step 1: Client `Runtime/Scripts/LOPMasterData.cs`** — `TableFiles`에 `"tbability"` 추가.
- [ ] **Step 2: Server `Runtime/Scripts/LOPMasterData.cs`** — `TableFiles`에 `"tbability"` 추가.
- [ ] **Step 3: 컴파일** — 클·서 `refresh_unity`(scope=all, force) → `read_console`(error) 0. 신규 `.cs.meta`/`.bytes.meta` 생성.

---

## Task 3: Client — AbilityDataProvider + AbilityActivator 데이터 드리븐
- [ ] **Step 1: `AbilityDataProvider.cs` 교체** — `[Inject] LOP.MasterData.LOPMasterData md` 추가. 하드코딩 제거:
```csharp
public AbilityData Get(int abilityId) => Map(md.Tables.TbAbility.Get(abilityId));

public bool TryGetByCode(string code, out AbilityData data)
{
    foreach (var r in md.Tables.TbAbility.DataList)
        if (r.Code == code) { data = Map(r); return true; }
    data = default; return false;
}

private static AbilityData Map(LOP.MasterData.Ability r)
{
    var targeting = (TargetingMode)System.Enum.Parse(typeof(TargetingMode), r.TargetingMode);
    int[] effects = r.ProducesEffectId == 0 ? System.Array.Empty<int>() : new[] { r.ProducesEffectId };
    return new AbilityData(r.Id, r.CooldownTicks, r.MpCost, r.CastTimeTicks, targeting, r.Range, effects);
}
```
> 생성 필드명은 PascalCase(`ProducesEffectId`/`TargetingMode`/`CooldownTicks` 등) — gen 후 정확 확인. `DataList` 노출 확인(TbStatusEffect에 있던 것과 동형).
- [ ] **Step 2: `AbilityActivator.cs` 일반화** — haste-only 제거:
```csharp
public bool TryActivate(string casterEntityId, string code, long currentTick)
{
    if (abilityDataProvider.TryGetByCode(code, out var ability) == false) return false;  // 레거시 폴백
    var caster = entityRegistry.Get(casterEntityId);
    if (caster == null) return false;
    var effects = new StatusEffectData[ability.ProducesEffectIds.Length];
    for (int i = 0; i < effects.Length; i++) effects[i] = statusEffectDataProvider.Get(ability.ProducesEffectIds[i]);
    abilitySystem.TryActivate(caster, ability, caster, effects, currentTick);
    return true;
}
```
> `AbilityActivator`는 이미 `abilityDataProvider`/`statusEffectDataProvider`/`abilitySystem`/`entityRegistry` 주입 보유(3d). 시그니처 불변 → 호출부(`PlayerInputManager`) 무변경.
- [ ] **Step 3: 컴파일** — Client `refresh_unity` → error 0.

---

## Task 4: Server — 동형 전환
- [ ] **Step 1·2: `AbilityDataProvider.cs`·`AbilityActivator.cs`** — Task 3과 동일 본문(서버 `Ability`엔 Description 없음 — 매핑 안 써 무관).
- [ ] **Step 3: 컴파일** — Server `refresh_unity` → error 0.

---

## Task 5: 종합 검증
- [ ] **Step 1: 양쪽 재스캔** — error 0.
- [ ] **Step 2: `.bytes` 확인** — `tbability.bytes`에 `haste`/`Self` 직렬화(클·서).
- [ ] **Step 3: 플레이(사용자)** — HASTE 버튼/H키 → +30% 100틱 후 복귀(3d와 동일). dash/attack 등 레거시 폴백 동일. NRE 0.

---

## Task 6: 커밋 + 머지
- [ ] infra → MasterData-Client → MasterData-Server → Server → Client(docs) 순. 명시 파일만(3c churn 복원 후). 머지 `--no-ff`, 사용자 요청 시.

---

## Out of Scope (후속)
- **캐릭터별 어빌리티 로드아웃**(grant를 마스터데이터 캐릭→어빌리티로) — grant-all TEMP 잔존.
- **AI 발동**(`EnemyBrain`→AbilityActivator), **발동 cosmetic 와이어 + 쿨다운 UI**.
- **dash/attack/spawn → 어빌리티**(임펄스/데미지 효과 종류 신설) + **legacy 은퇴**(`TbAction`/`Action`/`LOPActionManager`) = 3f+.
- **다중 produces effect**(현 단일 inline) — 실제 필요 시.

---

## Self-Review
- **더미 제거:** `AbilityDataProvider` 하드코딩→`TbAbility`, `AbilityActivator` haste-only→code 조회. 3d TEMP 2/3 해소(grant-all만 잔존). ✅
- **키 규약:** int id PK + string code alias([[masterdata-key-convention]]). 라우팅=code, 코어=id. ✅
- **거동 보존:** 헤이스트만 행 존재 → 발동 3d 동일. 비-어빌리티 code=레거시 폴백 무변경. ✅
- **파이프라인:** 3c와 동일(source→convert→gen→복원→로더키). TbAction 무손상(별 테이블). ✅
- **호출부 불변:** `AbilityActivator` 시그니처 유지 → `PlayerInputManager`/`LOPRunner` 무변경. ✅
- **범위 정직:** grant-all·AI·와이어·dash/attack·legacy 은퇴 Out of Scope. ✅
```
