# Entity System Design

## Overview

월드 엔티티 데이터 모델 시스템. CBD(Component-Based Design) 기반으로, Entity는 빈 껍데기이고 능력(IEntityComponent)을 조합하여 캐릭터, 몬스터, 보스, NPC, 오브젝트, 이펙트 영역 등을 구성한다.

## Requirements

- 상호작용이 있는 모든 월드 객체를 엔티티로 취급 (렌더링만 하는 배경 객체 제외)
- 엔티티 공통: Id
- CBD 컴포지션 방식: Entity는 빈 컨테이너, 능력은 IEntityComponent 구현체로 조합
- 컴포넌트는 자신이 속한 Entity(Owner)를 알고 있음
- Anemic Domain Model: 컴포넌트는 데이터와 파생 속성(읽기 전용 계산값)만 소유. 생성자에서 구조적 무결성(필수 초기값 설정)은 처리하되, 상태 변경 로직은 두지 않는다. 모든 처리 로직은 System에 둔다
- 스탯: Base + Modifiers(출처 추적, 해제 가능) = Current
- 구현 범위: 순수 C# 데이터 모델 (MonoBehaviour, 전투 시스템 연결은 이후)

## Architecture: CBD 컴포지션 (Dictionary 기반)

### Core 레이어

Entity와 IEntityComponent는 다른 피처에서도 사용되므로 Core에 배치한다.

#### IEntityComponent

모든 엔티티 컴포넌트의 기반 인터페이스. 자신이 속한 `Entity Owner` 참조를 노출하며, Owner는 `Entity.Add` 시 자동으로 설정된다.

#### Entity

엔티티의 빈 컨테이너. 다음을 갖는다.

- **Id**: 엔티티 식별자 (불변)
- **컴포넌트 저장소**: 컴포넌트 타입(`Type`)을 키로 하는 딕셔너리. 타입당 하나의 컴포넌트만 보유
- **Add**: 컴포넌트를 추가하고 해당 컴포넌트의 Owner를 자기 자신으로 설정
- **Get**: 타입으로 컴포넌트를 조회. 없으면 null 반환
- **Has**: 해당 타입 컴포넌트의 존재 여부 반환
- **Remove**: 타입으로 컴포넌트를 제거하고, 제거된 컴포넌트의 Owner를 null로 해제

#### EntityRegistry

월드의 엔티티 보관소(Core 레이어 순수 C# 컨테이너). `Entity.Id`를 키로 모든 엔티티를 관리한다.

- **Add**: 엔티티 등록. null 엔티티 / null Id / 중복 Id에는 예외 (레지스트리는 진실원본 — 중복은 버그)
- **Get / TryGet**: Id로 조회. 없으면 null / false
- **Remove**: 제거 및 제거 여부 반환
- **Contains**: 존재 여부
- **Count / All**: 총 개수 / 전체 열거

엔티티의 생성·등록은 게임 측(LOP 등)의 크리에이터/팩토리가 수행하고, 외부 시스템(뷰·UI·netcode)은 이 레지스트리를 통해 entityId로 엔티티에 접근한다.

### Core Enums

#### EntityStatType

스탯 종류 enum. 자유롭게 확장 가능하며, 아이템 효과 등으로 새로운 스탯이 필요하면 값을 추가한다.

- MaxHp
- Attack
- Defense
- Speed
- Mana
- CriticalRate
- EvasionRate

### Features/Entity 레이어

엔티티 전용 컴포넌트 구현체와 관련 타입.

#### ModifierSource (Enum)

스탯 모디파이어의 출처 종류.

- Equipment
- Buff
- Debuff
- Passive

#### EntityStatModifier (Struct)

스탯 보정값 하나를 표현하는 직렬화 가능한 구조체. 다음 필드를 갖는다.

- **StatType**: 어떤 스탯에 적용되는지 (`EntityStatType`)
- **Value**: 보정 수치
- **Source**: 출처 종류 (`ModifierSource`)
- **SourceId**: 출처 식별자 — 같은 출처에서 부여한 모디파이어를 일괄 해제할 때 사용

### 컴포넌트 구현체 (Features/Entity/Models)

컴포넌트는 Anemic Domain Model 원칙을 따른다: 데이터와 파생 속성만 소유하고, 상태 변경 로직은 두지 않는다. 다른 컴포넌트를 직접 참조하지 않는다.

#### Stats

- **BaseStats**: 스탯 종류별 기본값 딕셔너리
- **Modifiers**: 적용 중인 스탯 모디파이어 목록

#### Health

- **CurrentHp**: 현재 체력
- **MaxHp**: 최대 체력
- **IsAlive / IsDead**: 체력 기반 파생 속성(읽기 전용)
- 생성 시 CurrentHp를 MaxHp로 초기화

#### Combat

- **IsInCombat**: 전투 중 여부

#### Dialogue

- **DialogueId**: 대화 식별자 (생성 시 지정, 불변)

#### Interactable

- **InteractionType**: 상호작용 종류 (생성 시 지정, 불변)
- **IsInteractable**: 상호작용 가능 여부 (생성 시 기본 true)

### 엔티티 조합 예시

| 엔티티 | 컴포넌트 조합 |
|---|---|
| 플레이어 | Stats + Health + Combat |
| 몬스터 | Stats + Health + Combat |
| 보스 | Stats + Health + Combat |
| NPC | Dialogue (+ 필요시 Stats + Health) |
| 상자/문 | Interactable |
| 힐 영역 | Interactable + Stats (효과 수치용) |

### Systems (Features/Entity/Systems)

컴포넌트 데이터를 읽고 쓰는 모든 처리 로직은 System에 둔다.

#### HealthSystem

Health 컴포넌트의 상태를 변경하는 로직.

- **TakeDamage**: CurrentHp 감소. 0 미만으로 내려가지 않도록 클램프
- **Heal**: CurrentHp 증가. MaxHp를 초과하지 않도록 클램프
- **SetMaxHp**: MaxHp 변경. 새 MaxHp보다 CurrentHp가 크면 CurrentHp를 조정

#### StatsSystem

Stats 컴포넌트의 조회와 모디파이어 관리.

- **GetValue**: 특정 스탯의 최종값 반환 = BaseStats 값 + 해당 스탯의 모든 모디파이어 합. 없는 스탯은 0
- **AddModifier**: 모디파이어 추가
- **RemoveModifiersBySourceId**: 특정 SourceId의 모디파이어 일괄 제거
- **RemoveModifiersBySource**: 특정 출처 종류(`ModifierSource`)의 모디파이어 일괄 제거

#### EntityFactory

자주 쓰는 컴포넌트 조합을 만들어 주는 편의 메서드. 직접 Entity 생성 + Add로 커스텀 조합도 가능하다.

- **CreateCombatEntity**: Stats + Health + Combat 조합 생성
- **CreateNpc**: Dialogue 컴포넌트 보유 엔티티 생성
- **CreateInteractable**: Interactable 컴포넌트 보유 엔티티 생성

## Folder Structure

```
Assets/Scripts/
  Core/
    Enums/
      EntityStatType.cs
    Interfaces/
      IEntityComponent.cs
    Entity/
      Entity.cs

  Features/
    Entity/
      Models/
        Stats.cs
        Health.cs
        Combat.cs
        Dialogue.cs
        Interactable.cs
      Enums/
        ModifierSource.cs
      Structs/
        EntityStatModifier.cs
      Systems/
        EntityFactory.cs
        HealthSystem.cs
        StatsSystem.cs

Assets/Tests/
  EditMode/
    Entity/
      EntityTests.cs
      StatsSystemTests.cs
      HealthSystemTests.cs
```

## Assembly Definition References

기존 레이어 asmdef를 사용한다.

- **Core**: Entity, IEntityComponent, EntityStatType 추가
- **Features**: 컴포넌트 구현체, EntityFactory 추가
- **Tests.EditMode**: 테스트 추가

Core asmdef는 `noEngineReferences: true`를 유지한다 — Entity와 IEntityComponent는 순수 C#.

## Test Strategy

EditMode 단위 테스트 (순수 C# 모델).

### Entity Tests
- Add로 컴포넌트 추가 후 Get으로 조회
- Add 시 Owner 자동 설정
- Has로 존재 여부 확인
- Remove로 제거 후 Has == false
- Remove 시 Owner가 null로 해제
- 존재하지 않는 컴포넌트 Get 시 null 반환

### StatsSystem Tests
- BaseStats 설정 후 GetValue = Base 값 반환
- Modifier 추가 후 GetValue = Base + Modifier 합산
- 같은 StatType Modifier 여러 개 합산
- RemoveModifiersBySourceId로 특정 출처 수정자 제거
- RemoveModifiersBySource로 출처 종류별 일괄 제거
- 없는 StatType 조회 시 0 반환

### HealthSystem Tests
- TakeDamage: CurrentHp 감소
- TakeDamage: CurrentHp가 0 이하로 내려가지 않음
- Heal: CurrentHp 증가
- Heal: MaxHp 초과하지 않음
- SetMaxHp: CurrentHp가 새 MaxHp 초과 시 조정

### Health 컴포넌트 Tests
- 생성 시 CurrentHp == MaxHp
- IsAlive/IsDead 파생 속성 검증

### Combat 컴포넌트 Tests
- IsInCombat 기본값 false
- 직접 값 설정 후 상태 확인

### Interactable 컴포넌트 Tests
- 생성 시 기본 IsInteractable == true
- InteractionType 설정 확인

### EntityFactory Tests
- CreateCombatEntity: Stats + Health + Combat 조합 확인
- CreateNpc: Dialogue 컴포넌트 확인
- CreateInteractable: Interactable 컴포넌트 확인

## Open Decisions

- [ ] MonoBehaviour 컴포넌트 (월드 배치, 렌더링, 물리)
- [ ] 전투 시스템 (System 레이어 — 데미지 계산, 턴 진행 등)
- [ ] NPC의 HP 보유 여부 (전투 가능 NPC)
- [ ] 엔티티 설정 데이터용 ScriptableObject (EntityData 등)
