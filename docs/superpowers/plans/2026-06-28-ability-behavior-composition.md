# Plan — 어빌리티 behavior 조합 (B 전환)

**Spec:** [2026-06-28-ability-behavior-composition-design.md](../specs/2026-06-28-ability-behavior-composition-design.md)
**Branch:** `feature/ability-behavior-composition` (LOP-Shared / Client / Server / infrastructure / MasterData-Client·Server)
**핵심:** 평면 덕타이핑(`motion_speed`/`produces_effect_id`) → **타입 있는 `AbilityEffect` 리스트** + **실행기+타입 핸들러 디스패치**. 라이프사이클 2/3 혼합은 *이미 맞음, 유지*.

> 명명 확정: `AbilityEffect`(베이스) / `DamageEffect` / `StatusEffectApplyEffect` / `MotionEffect` / `IAbilityEffectHandler` / `AbilityEffectExecutor`.

---

## Step 0 — Luban 다형 list 검증 (게이트) ✅ 완료(2026-06-28)

**VERDICT: YES** — Luban 4.9.0이 다형 bean 리스트 컬럼을 생성·직렬화(격리 샌드박스 스파이크). `[Motion, Damage]` 섞인 리스트까지 OK. → **다형 bean 리스트 채택, 자식 테이블 폴백 불요.**

- [x] 0a. Luban 4.9.0 확인 (상속/`$type`/`list,bean` 지원).
- [x] 0b. 샌드박스 스파이크 성공 — `abstract AbilityEffect` + 3 서브타입 + `Ability.Effects:List<AbilityEffect>` + `tbability.bytes` 생성.
- [x] 0c. 채택 + spec 반영.

### ⭐ 검증된 작성 레시피 (B0.2에서 그대로 사용)
- **`__beans__.xlsx`**: 베이스 `AbilityEffect`(full_name만, parent·fields 없음 → 자식 있으면 **자동 abstract**) + 자식 `DamageEffect{amount:int}`/`StatusEffectApplyEffect{status_effect_id:int}`/`MotionEffect{speed:float}`(각 `parent=AbilityEffect`). 자식 필드는 **같은 시트의 `*fields` 멀티컬럼 그룹**에 인라인(서브컬럼 name/alias/type/…).
  - ⚠️ **`*fields` 헤더 셀은 서브컬럼들 가로로 *병합* 필수**(openpyxl `merge_cells`). 미병합 시 `bean '__FieldInfo__' 缺失 列:'alias'` 에러. (현 `__beans__`엔 `*fields` 컬럼 자체가 없음 — 우리가 첫 수기 bean 도입.)
- **`#Ability.xlsx`(임베디드 데이터)**: 컬럼 헤더 `##var=effects#sep=,`(4.x는 `&` 아님 **`#`**), `##type=list,AbilityEffect`. 데이터 셀 = `Subtype,field…`(첫 토큰=`$type`=bean 이름), **리스트 원소는 다음 *열*로** 나열(예: id3 → B열 `MotionEffect,15`, C열 `DamageEffect,30`). `#sep=,`는 한 원소 셀 *안*(타입+필드)만 분리.
- **생성 C#**: `abstract AbilityEffect:Luban.BeanBase`(ReadInt 타입-id 디스패치) + 서브타입 `__ID__` 상수 + `Ability.Effects:List<AbilityEffect>`.

### ⚠️ convert 파이프라인 결정 (B0.2)
현 `convert_source_to_luban.py`는 source 평면 → bean 자동유도라 다형 bean을 못 만든다. **Ability가 첫 수기 정의 bean**이 된다. B0.2에서 택1:
- (권장) convert가 **Ability만 특수 처리** — effects 다형 스키마(`__beans__` 행 + 병합 `*fields` + `#Ability.xlsx` list 컬럼)를 author하도록 확장.
- (대안) Ability를 convert 자동흐름에서 빼고 `__beans__`/`#Ability.xlsx`를 **수기 유지**(나머지 평면 테이블만 convert).

샌드박스(`_gen_scratch/luban-spike/`, gitignored)는 남겨둠/삭제 무방. 실제 Datas/source/MasterData 무변경 확인됨.

---

## B0 — 조합 모델 도입 + 기존 이전 (거동 보존, 새 게임플레이 0)

헤이스트·대시를 타입 리스트 + 실행기로 옮긴다. **결과 거동 동일**(헤이스트 +30% 100틱, 대시 동일).

### B0.1 — 코어 (LOP-Shared)
- [ ] `AbilityEffect`(베이스) + `DamageEffect{Amount,Range,Angle}`·`StatusEffectApplyEffect{StatusEffectId}`·`MotionEffect{Speed}` (순수 데이터, 엔진 비참조). C# 형태(추상클래스/인터페이스) 확정 — 다형 리스트라 참조형.
- [ ] `IAbilityEffectHandler`(포트): `void OnActiveEnter(ctx, effect)` + `void OnActiveTick(ctx, effect)` (cadence 두 훅) — 또는 단일 메서드+cadence 질의. ctx = 시전자 Entity + currentTick 등.
- [ ] `AbilityEffectExecutor`(순수): effect 리스트를 순회, 타입→핸들러 디스패치. 핸들러 레지스트리는 `IReadOnlyDictionary<Type, IAbilityEffectHandler>` 주입(side가 채움). 핸들러 없는 타입은 무시(서버권위 데미지를 클라가 무시하는 경로).
- [ ] `AbilityData`: `ProducesEffectIds`/`TargetingMode`/`Range` 제거 → `AbilityEffect[] Effects`.
- [ ] `Abilities`/`ActiveAbility`: `PendingEffects(StatusEffectData[])` → resolve된 `AbilityEffect[] Effects` 보유.
- [ ] `AbilitySystem`: `TryActivate`가 effects를 `ActiveAbility`에 실음. `Tick`이 Startup→Active 진입 시 `executor.OnActiveEnter` 1회, Active 매 틱 `executor.OnActiveTick`. (StatusEffect 직접 Apply 코드 → `StatusEffectApplyEffect` 핸들러로 이동.)
- [ ] **EditMode**(LOP-Shared): executor 디스패치(타입별 호출), cadence(enter 1회 vs tick 매 틱), 미등록 타입 무시, 페이즈 머신 회귀(헤이스트 0/1/0 등가).

### B0.2 — 마스터데이터 (infra → MasterData-Client/Server)
- [ ] `source/Ability.xlsx`: `motion_speed`/`produces_effect_id` 컬럼 제거 → `effects` 다형 리스트. haste=`[StatusEffectApply{statusEffectId:1}]`, dash=`[Motion{speed:15}]`. (Step 0 채택 형태대로.)
- [ ] convert 스크립트(`scripts/convert_source_to_luban.py`): 다형 bean/리스트 표현 지원(필요 시 Ability만 수기 스키마 경로).
- [ ] `gen.sh` 재생성 → `Ability.cs`+effect bean 클래스들·`tbability.bytes`. churn/`.meta` 정리(대시 절차 동일).

### B0.3 — side 핸들러 + 배선 (Client/Server)
- [ ] `StatusEffectApplyEffectHandler`(클·서): `StatusEffectId` resolve(`StatusEffectDataProvider`) → `StatusEffectSystem.Apply`. (오래 사는 효과 = 적용 후 독립 `StatusEffect` 컴포넌트로 = 방식 3 유지.)
- [ ] `MotionEffectHandler`(클·서): `OnActiveTick`에 전방 push — 기존 `AbilityMotionSystem.ApplyMotion` 로직 흡수. `AbilityMotionSystem` 제거(또는 핸들러로 대체).
- [ ] `AbilityDataProvider`: `TbAbility.effects` → `AbilityEffect[]` 매핑(다형). 기존 `MotionSpeed`/`ProducesEffectId` 읽기 제거.
- [ ] `AbilityActivator`: resolve된 effects 전달(타입 추출 안 함 — executor/handler 책임).
- [ ] `GameLifetimeScope`(클·서): 핸들러들 + executor 등록. 클라는 `DamageEffectHandler` 미등록(B1), 서버는 전체. `LOPRunner`의 `AbilityMotionSystem.ApplyMotion` 호출 → executor 경로로 정리.
- [ ] **검증**: 클·서 `refresh_unity`(force/all) 0에러 + EditMode green + **플레이 무회귀**(헤이스트 버튼/H키 +30%, 대시 동일).

---

## B1 — Attack = DamageEffect (페이즈 머신 slice 3 본체)

조합 모델 위에 데미지 effect를 얹어 공격을 어빌리티화. 데미지=서버권위 유지.

### B1.1 — 데이터
- [ ] 공격 어빌리티 행 + `effects=[Damage{Amount,Range,Angle}]`. 레거시 `Attack.cs` 값 이식(`range=2`/`angle=90`). 발동 시점=Active 진입(startup/active 틱으로). 캐릭터별(knight/archer/necromancer)은 일단 단일/대표 1행(로드아웃은 Out of Scope).

### B1.2 — 서버 핸들러
- [ ] `DamageEffectHandler`(서버만 등록): `OnActiveEnter`에 `DamageEffect`의 Range/Angle로 `Physics.OverlapSphere`+cone(레거시 `IsInAttackSector` 이식) → `LOPCombatSystem.Attack(attacker, target)`. 결과는 기존 `WorldEventBuffer→WorldEventSink→DamageEventToC`+HP 스냅샷 그대로.
- [ ] 서버 `GameLifetimeScope` 등록. 클라 미등록(executor가 Damage 무시 = 연출만).

### B1.3 — 부여 + 발동 트리거
- [ ] `CharacterCreator`(클·서): 공격 어빌리티 `Grant`(grant-all TEMP 유지).
- [ ] 입력: 공격을 `ability_id` 경로로(GamePad Attack 버튼/키 → `SetAbilityId`). 레거시 attack `actionCode` 경로와 **병행(strangler-fig)** — slice 5에서 레거시 제거.
- [ ] **검증**: 공격 시 데미지/사망/연출이 레거시와 동등(애니 트리거 포함). 클·서 0에러.

---

## 이후 (이 plan 밖 — 페이즈 머신 spec 로드맵)
- **slice 4** — 발동 cosmetic 와이어(GameplayCue 대응, `ActionStartToC` 대체).
- **slice 5 (3f)** — 레거시 `Action`/`LOPActionManager`/`TbAction`/`Spawn`/액션 와이어 + 병행 트리거 은퇴. (B1의 콘솔 `Attack 01` 애니 에러도 여기서 정리.)

## 커밋/머지 규칙
- 영향 레포: B0=LOP-Shared+infra+MD-Client/Server+Client+Server (최대 6). B1=Client+Server(+데이터면 infra/MD).
- 각 슬라이스 후 컴파일·EditMode·플레이 검증. 머지는 사용자 요청 시 `--no-ff`(의존순: Shared→MD→Client/Server).
- 픽스처(Room.unity/ProjectSettings/ConfigureRoomComponent 등) 커밋 제외.
- EOL: 신규=Write/편집=Edit(mass sed 금지), 커밋 후 worktree CRLF는 `git checkout`으로 복원(메모리 교훈).

## 진행
- [x] Step 0 — Luban 다형 list 검증 (게이트) ✅ YES, 다형 bean 리스트 채택
- [x] **B0 완료** — 0.1 코어 + 0.2 데이터 + 0.3 side+배선. 컴파일 0 + EditMode 33/33 + 플레이 검증 OK(대시/헤이스트/게임시작). ⚠️ B0.3에서 DI 순환 발견 → **executor host-driven + 핸들러는 ctx로 entityManager**(서비스로케이터 아님)로 해소. `ctx.EntityManager`는 **interim**(Stage④ "접근 B"=World.Entity velocity 권위로 이전 시 제거) — spec "구현 정정" 절 박제.
- [ ] B1.1 데이터(DamageEffect 다중필드 bean 추가) → B1.2 서버 DamageEffectHandler(타게팅+LOPCombatSystem) → B1.3 부여+트리거 → B1 검증
