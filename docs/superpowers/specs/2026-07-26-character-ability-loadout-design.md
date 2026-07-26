# 캐릭터별 어빌리티 소유 + 슬롯 장착 설계

어빌리티를 캐릭터 단위로 분리하고, 호출자(입력·AI)가 어빌리티 id 대신 **슬롯**으로 참조하게 한다.
함께 어빌리티 3층 구조의 이름을 바로잡는다.

## 1. 배경 — 무엇이 막혔나

애니메이션 동기화 작업(`2026-07-25-animation-state-sync-design.md`) Task 10에서 막혔다.
클라 전용 뷰 테이블을 `어빌리티 id → 애니 스테이트 이름`으로 설계했는데, 실제 캐릭터를 열어보니
**같은 `attack`(id 3)을 쓰는 세 캐릭터의 스테이트 이름이 전부 달랐다**:

| 캐릭터 | 코드 | 공격 스테이트 이름 |
|---|---|---|
| Knight (플레이어) | `character_001` | `Attack 01` |
| Necromancer | `monster_001` | `Melee Attack` |
| Archer | `monster_002` | `Attack` |

셋 다 실제 스폰 대상이고 셋 다 id 3을 부여받는다. 단일 키로는 하나만 맞고 둘이 틀린다.

현재 코드가 이름 후보 세 개를 전부 던져보는 꼼수(`"Attack 01"` / `"Attack"` / `"Melee Attack"`)를
쓰고 있던 이유가 정확히 이것이다.

**해법 방향**: 뷰 테이블 키를 `(캐릭터, 어빌리티)`로 늘리는 대신, **어빌리티 자체를 캐릭터별로
분리**한다. 그러면 어빌리티 id가 이미 캐릭터를 구분하므로 뷰 테이블은 단일 키로 성립한다.

## 2. 궁극 형태와의 관계

목표 형태는 **"캐릭터가 스킬을 보유하고, 그중 일부를 장착한다"** 이다. 업계도 이 둘을 나눈다:

| 층 | Unreal GAS | TrinityCore (WoW) | RPG 스키마 표준 |
|---|---|---|---|
| **보유** (뭘 아는가) | `GiveAbility`로 부여 | `character_spell` / `playercreateinfo_spell_custom` | `class_ability(class_id, ability_id)` |
| **장착** (어디에 놓였나) | 스펙의 `InputID` | `playercreateinfo_action`(슬롯 id가 키) | `character_equipment(slot_id, …)` |

이번에는 **장착 층만** 만든다. 현재는 보유한 것을 전부 장착하므로 두 층을 나눌 데이터가 없다
(YAGNI). 보유 풀이 필요해지면 **테이블을 하나 더하는 것**으로 끝나며, 장착 테이블의 모양은
바뀌지 않는다.

## 3. 어빌리티 3층 — 구조는 이미 표준, 이름만 어긋남

LOP에는 이미 세 층이 다 있고 GAS와 1:1로 대응한다. 고칠 것은 이름 둘이다.

| 층 | 현재 이름 | **새 이름** | 하는 일 | 수명 |
|---|---|---|---|---|
| 정의 | `AbilityData` | (그대로) | 쿨다운·마나·준비/판정/후딜 길이·효과 목록 | 게임 데이터. 항상 존재 |
| 부여 + 상태 | `AbilitySlot` | **`GrantedAbility`** | **보유 증명 + 쿨다운 + 슬롯** | 캐릭터 생성 시 발급, 소멸까지 |
| 진행 중 | `ActiveAbility` | **`AbilityActivation`** | 지금 쓰는 중인 발동 하나(페이즈 경계) | 발동 시 생성, 종료 시 소멸 |

### 왜 `AbilitySlot` → `GrantedAbility` 인가

현재 이름이 "자리"를 뜻하는 것처럼 보이지만, 실제 내용은 자리가 아니라 **보유 기록**이다:

```csharp
public readonly struct AbilitySlot {
    public readonly int AbilityId;
    public readonly long CooldownEndTick;
}
```

`CanActivate`가 검사하는 네 가지 중 **둘이 이 기록에서 나온다**:

| 검사 | 출처 |
|---|---|
| 가졌나? | **기록의 존재 자체** (`Slots.TryGetValue`) |
| 지금 다른 걸 하는 중인가? | 진행 중인 발동 |
| 쿨다운 끝났나? | **`CooldownEndTick`** |
| 마나 충분한가? | `Mana` 컴포넌트 |

즉 단순 id 목록이 아니라 **per-캐릭터 런타임 상태 레코드**다. 여기에 슬롯이 더해지면
"가졌다 + 쿨다운 + 몇 번 버튼"이 되며, 이는 GAS 스펙이 담는 것
(*"클래스·레벨·입력 바인딩 + 인스턴스 바깥에 둬야 하는 런타임 상태"*)과 정확히 같다.

이 이름을 비워야 "슬롯"이라는 단어를 **장착 자리**라는 표준 의미로 쓸 수 있다.

### 왜 `ActiveAbility` → `AbilityActivation` 인가

현재 이름은 세 가지로 읽힌다:

1. **능동형 스킬**(패시브의 반대) — 카테고리로 오독
2. 진행 중인 발동 — 실제 의미
3. **`Active` 페이즈에 있는 어빌리티** — 페이즈 enum에 `Active`가 있어 생기는 충돌

3번이 특히 나쁘다. 코드에 `ActiveAbility.Phase == AbilityPhase.Startup`가 정상 상태로 존재한다 —
같은 단어가 한 줄에서 두 뜻으로 쓰인다.

`Activation`은 이 값을 만드는 동사(`TryActivate`)와 짝이 맞고, 패시브 카테고리와도 `Active`
페이즈와도 겹치지 않는다. 나중에 동시 발동이 필요해지면 **하나에서 목록으로** 바뀔 뿐 이름은
그대로 유효하다.

> **`Ability`로 줄이지 않는 이유**: GAS가 이름 하나로 되는 것은 객체지향이라 정의(클래스)와
> 실행(인스턴스)이 같은 타입이기 때문이다. LOP는 데이터 지향이라 둘이 **다른 타입**이며,
> 도메인에서 가장 일반적인 단어를 진행 중인 쪽이 가져가면 바로 옆 `AbilityData`가 "Ability에
> 관한 데이터"로 읽혀 정의의 이름이 망가진다. 길이가 부담이면 **필드를 짧게** 한다
> (`abilities.Current?.Phase`) — 타입 이름은 선언부에만 나온다.

### 컴포넌트 필드 이름

```csharp
public class Abilities : Component
{
    public Dictionary<int, GrantedAbility> Granted { get; }   // 구 Slots. 키는 어빌리티 id 유지
    public AbilityActivation? Current { get; set; }           // 구 ActiveAbility
}
```

`Granted`의 키를 어빌리티 id로 유지하는 이유: `CanActivate`의 쿨다운 조회가 id로 들어오는 가장
잦은 경로다. 슬롯 조회는 발동 시 1회뿐이라 순회로 충분하다.

## 4. 슬롯 — 무엇이고 어디 사는가

**슬롯 = 버튼 자리 번호.** 호출자가 어빌리티 id를 모른 채 "내 캐릭터의 기본 공격"을 가리키는 수단.

현재 하드코딩이 깨지는 지점이 이것이다:

```csharp
// 클라 GamePadViewModel
private const int AttackAbilityId = 3;      // Archer면 틀림
// 서버 EnemyBrain
private const int AttackAbilityId = 3;      // 주석: "grant-all로 모든 캐릭터 보유"
```

### 슬롯 배정

| 슬롯 | 의미 | 비고 |
|---|---|---|
| 1 | 기본 공격 | 모든 캐릭터. AI도 이것을 씀 |
| 2 | 대시 | |
| 3 | 헤이스트 | |
| 4 | 전역 공격(테스트) | 플레이어 전용 |

슬롯 0은 **"입력에 붙지 않음"** 을 뜻한다(GAS의 `InputID = INDEX_NONE` 대응). 상시형 패시브처럼
부여만 되고 발동되지 않는 어빌리티가 여기 해당한다.

### 슬롯은 부여 기록에 산다

GAS가 `InputID`를 부여된 스펙에 박아두는 것과 같다.

```
부여 시점:  로드아웃 표를 읽어 Grant(entity, abilityId, slot)
발동 시점:  표 조회 없음 — 엔티티의 부여 기록에서 slot이 일치하는 것을 찾음
```

런타임에 마스터데이터 의존이 생기지 않고, 롤백 스냅샷에도 값으로 그대로 실린다.

## 5. 데이터 — `TbCharacterLoadout`

```
TbCharacterLoadout   (기본키 = int id, group = 공용[클·서 모두 필요])
  id │ character_code │ slot │ ability_id
  ───┼────────────────┼──────┼────────────
  1  │ character_001  │  1   │ 3   (knight attack)
  2  │ character_001  │  2   │ 2   (dash)
  3  │ character_001  │  3   │ 1   (haste)
  4  │ character_001  │  4   │ 4   (global attack, 테스트)
  5  │ monster_001    │  1   │ 5   (necro attack)
  6  │ monster_001    │  2   │ 2
  7  │ monster_001    │  3   │ 1
  8  │ monster_002    │  1   │ 6   (archer attack)
  9  │ monster_002    │  2   │ 2
  10 │ monster_002    │  3   │ 1
```

**클·서 양쪽이 부여하므로 그룹은 공용**이다 — `__tables__.xlsx`의 `group` 칸을 **빈칸으로**
둔다(`TbCharacter`/`TbAbility`와 같음). `c`로 두면 서버가 어빌리티를 부여하지 못한다.

### 어빌리티 행 추가

`TbAbility`에 캐릭터별 공격 2행을 더한다. 기존 id 3은 Knight가 그대로 쓴다.
현재 테이블의 최대 id가 4(`global_attack`)이므로 5·6이 비어 있다.

| id | code | 비고 |
|---|---|---|
| 3 | `attack` | Knight. 기존 행 유지 |
| 5 | `necro_attack` | id 3 값 복사로 시작 |
| 6 | `archer_attack` | id 3 값 복사로 시작 |

지금은 수치가 동일하지만 **행이 갈려 있으므로 언제든 개별 조정이 가능**하다 — 이것이 캐릭터별
분리의 실질적 이득이다.

## 6. 흐름

### 부여 (캐릭터 생성)

```
현재:  Grant(e, 1); Grant(e, 2); Grant(e, 3);        // 코드에 박힘, 모든 캐릭터 동일
       if (내 캐릭) Grant(e, 4);

변경:  TbCharacterLoadout에서 character_code가 일치하는 행들을 읽어
       각각 Grant(e, ability_id, slot)
```

플레이어 전용 전역 공격(슬롯 4)은 `character_001` 행으로 표현되므로 `isUserEntity` 분기가
사라진다. 몬스터가 이 캐릭터 코드를 쓰지 않기 때문이다.

### 발동

```
게임패드 공격 버튼 → TryActivateSlot(entity, slot: 1)
서버 AI          → TryActivateSlot(entity, slot: 1)
```

슬롯 해소는 **두 곳에 나눠 둔다.** 코어(`AbilitySystem`)는 MasterData를 참조하지 않으므로
어빌리티 id로 설정을 찾는 일을 할 수 없기 때문이다 — 그 일은 이미 side-local
`AbilityActivator`가 `AbilityDataProvider`로 하고 있다.

```csharp
// 코어 (LOP-Shared, MasterData 비참조) — 순수 조회
public bool TryGetAbilityIdBySlot(Entity caster, int slot, out int abilityId);

// side-local (클·서 각자의 AbilityActivator) — 기존 흐름에 슬롯 입구만 추가
public bool TryActivateSlot(string casterEntityId, int slot, long currentTick);
//   → TryGetAbilityIdBySlot로 id를 얻고
//   → 기존 TryActivate(casterEntityId, abilityId, currentTick) 경로로 합류
```

기존 `AbilitySystem.TryActivate(caster, in AbilityData, target, currentTick)`과
`AbilityActivator.TryActivate(casterEntityId, abilityId, currentTick)`은 **그대로 둔다** —
슬롯은 그 앞단의 입구 하나가 늘어나는 것이다.

## 7. 와이어는 건드리지 않는다

`InputCommand.AbilityId`(클라 → 서버)는 **어빌리티 id 그대로** 보낸다. 클라가 슬롯을 자기 부여
기록으로 풀어 id를 얻고, 서버는 같은 로드아웃 데이터로 같은 것을 부여했으므로 결과가 일치한다.

- **예측 정합성**: 클·서가 같은 id를 보므로 예측·재생이 어긋나지 않는다.
- **치팅**: 클라가 임의 id를 보내도 서버 `CanActivate`의 보유 검사(`Granted`에 기록이 있는가)가
  거부한다. 기존 방어가 그대로 유효하다.
- **스냅샷**: 이미 머지된 시전 상태 필드(`active_ability_id`)는 새 id들도 그대로 실어 나른다.
  변경 없음.

## 8. 범위 밖

| 항목 | 왜 지금 안 하나 |
|---|---|
| **보유 풀 테이블** (장착과 분리) | 현재는 보유=장착이라 구분할 데이터가 없다. 필요 시 테이블 추가만 |
| **반응형 패시브** ("체력 30% 이하면 방어력↑") | 조건 감시 로직이 필요. GAS는 자동 발동 어빌리티로 처리하지만 **LOP는 동시 발동 1개**라 그 자리를 영구 점유하면 다른 모든 어빌리티가 막힌다. 별도 트랙 |
| **상시형 패시브** ("공격력 +10") | **이미 된다** — `StatusEffects` + `DurationPolicy.Infinite`("명시 제거까지") |
| **동시 발동 여러 개** | 현재는 완전 배타(격투게임식 커밋). 확장하려면 업계 표준은 GAS식 태그 규칙(`ActivationBlockedTags`/`BlockAbilitiesWithTag`/`CancelAbilitiesWithTag`) |
| 슬롯 재배치 UI | 콘텐츠 없음 |

## 9. 산업 표준 매핑

| 개념 | LOP (이 설계) | Unreal GAS | Unity GAS 포팅 | TrinityCore |
|---|---|---|---|---|
| 정의 | `AbilityData` | `UGameplayAbility` 클래스 | `AbilityScriptableObject` | `Spell.dbc` |
| 부여 + 상태 | `GrantedAbility` | `FGameplayAbilitySpec` | `AbilitySpec` | `character_spell` + `character_spell_cooldown` |
| 진행 중 | `AbilityActivation` | 어빌리티 인스턴스 | — | 시전 상태 |
| 장착 자리 | `slot` | `InputID` | — | `playercreateinfo_action`의 슬롯 |
| 장착 표 | `TbCharacterLoadout` | — | — | `playercreateinfo_action` |

**의도적으로 다르게 간 곳과 그 이유:**

| 차이 | 이유 |
|---|---|
| 동시 발동 **1개** (GAS는 다중 + 태그 규칙) | 격투게임식 커밋 설계(startup/active/recovery). 확장 경로는 GAS식 태그 규칙 |
| 쿨다운을 **부여 기록에** (GAS는 별도 GameplayEffect + 태그) | TrinityCore식(`character_spell_cooldown`). 절대 틱 하나라 스냅샷·롤백에 값으로 그대로 실린다. GAS가 효과로 하는 것은 복제·예측 인프라를 재사용하려는 것 |
| 이름 **`GrantedAbility`** (GAS·Unity 포팅은 `Spec`) | 우리 정의가 `AbilityData`라 `Spec`이 동의어로 읽힌다. `granted`는 GAS 자신의 용어(`GiveAbility`, *"granted GameplayAbilities that the ASC owns"*) |

## 10. 슬라이스

| | 슬라이스 | 내용 | 동작 변화 |
|---|---|---|---|
| **1** | 이름 정리 | `AbilitySlot` → `GrantedAbility`, `ActiveAbility` → `AbilityActivation`, 컴포넌트 필드 `Slots` → `Granted` / `ActiveAbility` → `Current` | **없음** (순수 리네임) |
| **2** | 슬롯 도입 | `GrantedAbility`에 `Slot` 추가, `Grant(e, abilityId, slot)`, `TryActivateSlot`, 호출자(게임패드·AI)를 슬롯 기반으로 전환. 부여는 아직 하드코딩(슬롯만 명시) | **없음** (같은 어빌리티로 해소) |
| **3** | 로드아웃 데이터 | `TbCharacterLoadout` 신설 + 캐릭터별 attack 행 2개 + `CharacterCreator`가 표 기반 부여 | **여기서 갈린다** — 캐릭터마다 다른 공격 |

슬라이스 1·2가 동작을 바꾸지 않으므로, 문제가 생기면 원인이 3에 있음이 자명하다.

리네임 규모: `AbilitySlot` 10곳/4파일(Shared만), `ActiveAbility` 약 76곳/16파일(Shared 14 · 클 1 ·
서 1). 전부 컴파일러가 잡는 기계적 변경이다.

`PredictedAbilityState`(롤백 스냅샷)는 부여 목록을 통째로 깊은 복사하므로 슬롯 추가가 자동으로
따라간다 — 추가 작업 없음.

## 11. 검증

### 자동 테스트 (EditMode, LOP-Shared)

| 대상 | 케이스 |
|---|---|
| `TryGetAbilityIdBySlot` | 슬롯에 부여된 것이 있으면 그 id / 없는 슬롯이면 false / 슬롯 0은 조회 대상 아님 |
| `Grant`의 슬롯 저장 | 부여 후 기록에 슬롯이 남는가, 같은 어빌리티 재부여 시 슬롯 갱신 |
| 기존 어빌리티 스위트 | 리네임 후 전량 통과(회귀 없음) |

`AbilityActivator.TryActivateSlot`은 클·서 `Assembly-CSharp`에 있어 EditMode 단위 테스트가
불가능하다 — 인게임 검증으로 덮는다.

현재 EditMode 기준선은 **318개 통과**다.

### 인게임

| 시나리오 | 기대 |
|---|---|
| 슬라이스 1·2 후 | 공격·대시·헤이스트·G키가 이전과 **완전히 동일**하게 동작 |
| 슬라이스 3 후 | 세 캐릭터가 각자 다른 공격 어빌리티를 발동(로그로 id 확인) |
| AI | 몬스터가 자기 공격을 발동(id 5 / 6) |
| 회귀 | 쿨다운·마나 소모·대시 중 입력 잠금·롤백 재생 |

## 12. 후속

이 작업이 끝나면 **애니메이션 동기화 슬라이스 2를 재개**한다. `TbAbilityView`는 원래 설계대로
**어빌리티 id 단일 키**로 성립한다 — 캐릭터별로 id가 갈렸기 때문이다.

```
TbAbilityView
  ability_id │ anim_state    │ anim_layer
  ───────────┼───────────────┼───────────
  3          │ Attack 01     │ 1    (Knight)
  5          │ Melee Attack  │ 1    (Necromancer)
  6          │ Attack        │ 1    (Archer)
```

## Open Decisions

- [ ] **동시 발동 확장** — "공격 중 버프 사용" 같은 요구가 생기면 GAS식 태그 규칙 도입. 현재는
  전부가 전부를 막는다(attack은 38틱 점유)
- [ ] **보유 풀 분리 시점** — 플레이어가 스킬을 갈아끼우는 콘텐츠가 생길 때
- [ ] **몬스터의 대시·헤이스트 부여 유지 여부** — 현재 AI가 쓰지 않지만 부여는 되어 있다. 이번엔
  기존 동작 보존을 위해 유지하며, 정리는 별건
