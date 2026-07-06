# 넉백 MasterData 승격 — TEMP 코드 배선 → Luban 데이터

**Date:** 2026-07-06
**Branch:** `feature/knockback-masterdata-promotion`
**Related:** [슬라이스 2 — 넉백](2026-07-05-velocity-knockback-slice2-design.md) · [ability-behavior-composition](2026-06-28-ability-behavior-composition-design.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [MasterData Luban 마이그레이션](2026-06-03-master-data-luban-migration-design.md)

## Goal

넉백 슬라이스 2가 남긴 **TEMP 코드 배선**(서버 `AbilityDataProvider`에서 "DamageEffect 있으면 무조건 `new KnockbackEffect(8f,...)` 하드코딩")을 **Luban MasterData 데이터**로 승격한다. 넉백을 정식 effect 타입(`KnockbackEffect` bean)으로 스키마에 넣고, 어떤 어빌리티가 얼마나 밀지를 **데이터(`#Ability.xlsx`)로 지정**한다. 다른 effect(Damage/Motion/StatusEffectApply)와 완전히 동형이 된다.

## 배경 — 현재 TEMP 상태

슬라이스 2에서 넉백 도메인 타입(`KnockbackEffect : AbilityEffect`)·핸들러·넷코드는 정식으로 만들었지만, **발동 트리거만 TEMP**로 뒀다:

```csharp
// 서버 AbilityDataProvider.MapEffects — TEMP(슬라이스2)
case LOP.MasterData.DamageEffect d:
    result.Add(new DamageEffect(d.Amount, d.Range, d.Angle));
    result.Add(new KnockbackEffect(strength: 5f, range: d.Range, angle: d.Angle,
        durationTicks: 12, decayPerTick: 0.8f));   // ← 하드코딩, 모든 공격에 일괄
    break;
```

문제: (1) 튜닝값이 코드에 박혀 디자이너가 못 만짐, (2) "모든 DamageEffect에 일괄"이라 어빌리티별 선택 불가, (3) MasterData에 `KnockbackEffect` bean이 없어 다른 effect와 비대칭. 이 스펙이 이를 정식 데이터 경로로 옮긴다.

## 파이프라인 모델 (진실원본 확인 — 조사 근거)

이 프로젝트 Luban 파이프라인은 **엑셀이 단일 원본이 아니라 3갈래 섞임**이다(2026-07-06 조사로 확정):

| 무엇 | 진짜 원본(사람이 편집) | 파생물(재생성) |
|---|---|---|
| Character/Item/Skin/StatusEffect | `infrastructure/table/source/*.xlsx` | → convert 스크립트 → `Datas/#Name.xlsx` |
| **effect bean 스키마** | **`scripts/convert_source_to_luban.py`의 `write_beans_index()`** | → `Datas/__beans__.xlsx` |
| **Ability 데이터** | **`Datas/#Ability.xlsx` 직접**(convert가 제외 — 다형 effects라 평면 흐름 밖) | (원본 자체) |

- convert 스크립트는 **살아있다**(`source/StatusEffect.xlsx`가 2026-06-26 수정됨 = 계속 사용 중). 주석의 `# One-shot`은 낡음.
- 따라서 **`__beans__.xlsx`를 직접 편집하면 다음 convert 때 덮어써진다** → bean은 반드시 **스크립트**에 추가.
- `#Ability.xlsx`는 hand-authored라 **직접 편집**이 맞다.
- `gen.sh`는 convert를 호출하지 않는다 → 워크플로는 **convert 먼저, 그다음 gen** (수동 2단계).

## 산업 표준 매핑

- **다형 effect bean + 데이터 조합 = Luban 관용 그대로.** `AbilityEffect` 추상 base + 서브클래스, 데이터 셀은 `<TypeTag>,<fields...>`. 신규 타입 추가 = bean 1개 + 데이터 값, dispatch(`DeserializeAbilityEffect` switch)는 **Luban이 타입-id 해시로 자동 생성**(등록 파일 없음). WoW `SpellEffect` / GAS `GameplayEffect` 조합 모델과 정합.
- **효과 = 데이터, 로직 = 핸들러** 분리 유지(슬라이스 2에서 확립). 이 스펙은 *데이터*만 정식화하고 도메인/핸들러/넷코드는 무변경.

## 설계

### A. `KnockbackEffect` bean 추가 (infrastructure — 스크립트)

`scripts/convert_source_to_luban.py`의 `write_beans_index()`에 `DamageEffect`와 동형으로 bean 행을 추가한다. 부모 `AbilityEffect`, **그룹 공란(클·서 양쪽)**, 필드 5개를 **순서대로**:

| 필드 | 타입 |
|---|---|
| `strength` | `float` |
| `range` | `float` |
| `angle` | `float` |
| `duration_ticks` | `int` |
| `decay_per_tick` | `float` |

(첫 필드는 bean 행에, 나머지 4개는 `full_name`/`parent` 공란 연속행 — DamageEffect의 amount/range/angle 3행 패턴과 동일.)

**그룹 = 공란(양쪽):** 기존 `DamageEffect`도 양쪽에 생성됨(클라도 매핑). 일관성 위해 KnockbackEffect도 양쪽. (넉백은 서버 권위라 클라가 *소비*는 안 하지만 — 클라 핸들러 없음 — 데이터는 Damage와 대칭으로 양쪽에 둔다. 서버 전용을 원하면 그룹 `s`지만 Damage와 비대칭이라 미채택.)

필드 순서 = 데이터 셀의 콤마 순서 = 생성된 ctor의 읽기 순서. 셋이 반드시 일치.

### B. `effects` 컬럼 2단 구분자 (infrastructure — `#Ability.xlsx`)

현재 `effects#sep=,`는 **effect 1개만** 담는다(리스트 sep과 필드 sep이 같은 콤마). attack이 Damage+Knockback **2개**를 가지려면 **2단 구분자**로 바꾼다: 리스트 항목은 별도 문자(예 `|`), 필드는 `,`.

- 변경: `effects#sep=,` → `effects#sep=<2char>` (정확한 문자·순서는 Luban 버전 문법 확인 후 plan에서 확정 — 리스트-레벨/필드-레벨 매핑).
- **하위호환:** 기존 1-effect 행(`StatusEffectApplyEffect,1` / `MotionEffect,15` / `DamageEffect,10,2,90`)은 리스트 구분자가 없어 그대로 단일 항목으로 파싱된다. 회귀 없음.

### C. attack 데이터 (infrastructure — `#Ability.xlsx`)

`id=3 attack` 행의 `effects` 셀을 Damage + Knockback으로:

```
DamageEffect,10,2,90 <listsep> KnockbackEffect,5,2,90,12,0.8
```

넉백 값 = **현재 조율값 그대로**(무회귀): `strength=5, range=2, angle=90`(데미지와 동일 형상), `duration_ticks=12`, `decay_per_tick=0.8`.

### D. 재생성 (infrastructure)

1. `python scripts/convert_source_to_luban.py` → `Datas/__beans__.xlsx` 재생성(KnockbackEffect bean 반영). `#Ability.xlsx`는 convert가 안 건드림 → C의 수기 편집 보존.
2. `./gen.sh`(또는 `gen.bat`) → 양쪽 MasterData 패키지에:
   - `Runtime.Generated/Scripts/MasterData/KnockbackEffect.cs` 신규 + `AbilityEffect.cs`의 dispatch switch 자동 갱신,
   - `StreamingAssets/MasterData/*.bytes` 재생성(attack의 2-effect 반영).

### E. AbilityDataProvider 매핑 (클·서 둘 다)

`MapEffects`의 switch에 `KnockbackEffect` case 추가(Damage와 동형):

```csharp
case LOP.MasterData.KnockbackEffect k:
    result.Add(new KnockbackEffect(k.Strength, k.Range, k.Angle, k.DurationTicks, k.DecayPerTick));
    break;
```

클라도 추가(MasterData 양쪽 존재 + Damage 선례). 클라 executor엔 핸들러 없어 무시되지만 대칭 유지.

### F. TEMP 은퇴 (서버)

서버 `AbilityDataProvider`의 DamageEffect case에서 하드코딩한 `result.Add(new KnockbackEffect(...))` + TEMP 주석 **제거**. 이제 넉백은 데이터에서 온다.

## 검증

- **재생성 산출물 유효:** convert + gen 후 `KnockbackEffect.cs`가 양쪽 패키지에 생기고 `AbilityEffect.DeserializeAbilityEffect` switch에 case 자동 추가됨 확인. `.bytes` 재생성됨.
- **클·서 컴파일:** 양쪽 Unity 컴파일 클린(신규 MasterData 타입 + AbilityDataProvider case).
- **데이터 파싱:** attack 어빌리티 로드 시 effects가 `[DamageEffect, KnockbackEffect]` 2개로 역직렬화되는지(런타임 로그/디버그 확인).
- **플레이(무회귀):** attack 공격 → **슬라이스 2 TEMP와 동일한 넉백 손맛**(strength 5 등 값이 이제 데이터에서 옴). 원격·로컬 대상 둘 다 이전과 동일. 다른 어빌리티(haste/dash) 무영향.
- **회귀:** 기존 1-effect 어빌리티(haste=StatusEffectApply, dash=Motion)가 2단 구분자 변경 후에도 정상 파싱.

## 영향 파일 (개략 — 세부는 plan)

- **infrastructure:** `scripts/convert_source_to_luban.py`(`write_beans_index`에 KnockbackEffect), `Datas/__beans__.xlsx`(재생성), `Datas/__tables__.xlsx`(재생성, 무변경 예상), `Datas/#Ability.xlsx`(구분자 + attack 행 수기).
- **MasterData-Client / -Server:** `Runtime.Generated/Scripts/MasterData/KnockbackEffect.cs`(신규), `AbilityEffect.cs`(재생성 switch), `StreamingAssets/MasterData/*.bytes`(재생성) — 각 패키지 + `.meta`.
- **LOP-Server:** `AbilityDataProvider.cs`(case 추가 + TEMP 제거).
- **LOP-Client:** `AbilityDataProvider.cs`(case 추가) + 이 spec.
- **무변경:** 도메인 `KnockbackEffect`(LOP-Shared), `KnockbackEffectHandler`, 넷코드/wire/Reconciler, `MotionContribution*` — 슬라이스 2 그대로.

## Out of Scope

- 넉백 도메인/핸들러/넷코드 변경(슬라이스 2에서 완료).
- 넉백을 attack 외 다른 어빌리티에 추가(지금은 현행 유지 = attack만). 추후 데이터로 자유 확장.
- effect별 targeting 공유(Damage↔Knockback 각자 OverlapSphere 유지), Override 완전경직/CC, Y축 넉백.
- convert 스크립트/`source/` 층 자체의 리팩터(살아있는 파이프라인 — 이번 범위 밖).

## Open Questions (plan에서 해소)

- **2단 구분자 정확 문법:** Luban `#sep=` 다중 문자의 리스트-레벨/필드-레벨 매핑 순서(예 `|,` vs `,|`). 이 저장소 Luban 버전(`tools/Luban`)으로 확인. 기존 행 하위호환 재확인.
- **convert 재실행 부작용:** convert가 `source/` 기반 다른 `#Name.xlsx`도 재생성 → `source/` 무변경이면 diff 0이어야. 재생성 후 `git diff`로 의도치 않은 변경 없음 확인.
- **`__ID__` 해시 안정성:** KnockbackEffect의 Luban 자동 type-id가 기존 effect id와 충돌하지 않는지(해시라 사실상 무충돌, 생성 후 확인).

## 진행

- [x] 브레인스토밍 합의 (파이프라인 모델 조사 → bean=스크립트/데이터=#Ability.xlsx 직접, 양쪽 그룹, 2단 구분자, 현행 값 유지)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
