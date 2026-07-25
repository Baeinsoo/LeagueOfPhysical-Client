# 지속 상태 복제 + 애니메이션 파생 설계

접지·어빌리티 시전·상태이상을 **스냅샷(지속 상태)** 으로 복제하고, 애니메이션을 그 상태에서
**파생**시킨다. 일회성 이벤트 → `SetTrigger` 방식을 걷어낸다.

## 1. 배경 — 무엇이 문제인가

서버가 매 틱 모든 엔티티에 대해 보내는 것은 위치·회전·속도·HP·외력 기여뿐이다(`EntitySnap`).
"지금 무엇을 하는 중인가"는 들어 있지 않다.

그래서 스킬 모션은 **일회성 이벤트**로만 전달된다:

```protobuf
message AbilityActivatedToC {   // 진행도 없음
    string entity_id = 1;
    int32  ability_id = 2;
}
```

이벤트는 **받은 순간에 접속해 있던 클라만** 볼 수 있다. 같은 원인으로 다음이 전부 깨진다:

| 상황 | 증상 |
|---|---|
| 늦게 접속 | 시전 중인 캐릭터가 가만히 서 있음 |
| 이벤트 패킷 유실 | 그 클라에서만 스킬 모션 누락 |
| 시야 밖에 있다가 복귀 | 시전 중인 것을 못 봄 |
| 롤백 재시뮬 | 트리거 중복 발사 |

접지도 같은 부류다. 클라는 남의 캐릭터에 대해 충돌 계산을 돌리지 않으므로(`Simulated` 마커가
내 캐릭에만 붙는다) 접지를 알 방법이 없고, 현재는 발밑에 `"Plane"`이라는 **이름**의 콜라이더가
있는지 뒤지는 임시 구현으로 때우고 있다(`LOPEntityView.IsGrounded`, 코드에 `TODO: 고도화 필요!`).

## 2. 판단 기준 — 이벤트인가 스냅샷인가

기존 결정(`world-core-connection-architecture.md`의 "Snapshot vs Event")을 그대로 적용한다.
기준은 연속/이산이 **아니라**:

> **"진행되는 동안 화면에 보이나 = 중간에 봐도 보여야 하나?"**
> 그렇다면 지속 상태(스냅샷). 순간 터지고 끝나면 이벤트.

| | 지속 시간 | 중간에 봐도 보여야 하나 | → |
|---|---|---|---|
| 스킬 시전 모션 | 0.8초 | 예 | **스냅샷** |
| 접지 | 계속 | 예 | **스냅샷** |
| 상태이상 | 수 초 | 예 | **스냅샷** |
| 피격 이펙트 튐 | 0.1초 | 아니오 | 이벤트 |
| 데미지 숫자 | 순간 | 아니오 | 이벤트 |

**스킬 시전을 이벤트로 분류한 것이 오류였다.** 발동하는 *순간*은 이산이지만, 시전하고 *있는
상태*는 지속이다.

이 문서가 바꾸는 것은 규칙이 아니라 **분류**다. 새 개념을 도입하지 않는다.

## 3. 결정 요약

| 결정 | 내용 |
|---|---|
| 접지를 World 상태로 승격 | 새 컴포넌트 `GameFramework.World.GroundState` |
| 세 값을 스냅샷에 추가 | 접지 · 시전 상태 · 상태이상 목록 |
| 어빌리티 와이어 최소화 | `ability_id` + `end_tick` 2필드. 페이즈 경계는 클라가 마스터데이터로 역산 |
| 클라는 원격 어빌리티를 **시뮬하지 않는다** | 서버가 준 절대 틱으로 진행도를 **계산**만 |
| 애니는 상태에서 파생 | `SetTrigger` → `Animator.Play(state, layer, 진행도)` |
| 연출 매핑은 클라 전용 테이블로 | `TbAbilityView` / `TbStatusEffectView` (Luban group `c`) |
| 상태이상 부여 대상을 데이터화 | `StatusEffectApplyEffect.target_mode` (`self` / `hit_targets`) |

## 4. 데이터 계약

### 4-1. `GroundState` 컴포넌트 (GameFramework)

`KinematicMover`가 이미 접지를 계산해 `KinematicMoveResult.grounded`로 돌려주지만,
`KinematicMoveSystem.Tick`이 `position`·`velocity`만 쓰고 **버리고 있다**. 담을 자리를 만든다.

```csharp
namespace GameFramework.World
{
    /// <summary>캐릭터의 지면 접촉 상태. 키네마틱 이동이 매 틱 갱신한다.</summary>
    public class GroundState : Component
    {
        public bool IsGrounded { get; set; }
    }
}
```

`KinematicMoveSystem.Tick` 끝에 한 줄:

```csharp
groundState.IsGrounded = result.grounded;   // 지금까지 버려지던 값
```

컴포넌트는 클·서 `CharacterCreator`가 붙인다(`Abilities`를 붙이는 곳과 같은 자리 —
양쪽 `CharacterCreator.cs:55`). 컴포넌트가 없는 엔티티(아이템 등)는 갱신하지 않는다 — 기존
`Transform`/`Velocity` null 가드와 같은 패턴.

### 4-2. 와이어

```protobuf
message EntitySnap {
    // 기존: entity_id, position, rotation, velocity, max_HP, current_HP, motion_contributions

    bool  grounded          = 8;
    int32 active_ability_id = 9;    // 0 = 시전 중 아님
    int64 ability_end_tick  = 10;   // 시전이 끝나는 절대 틱 (= RecoveryEndTick)
    repeated ProtoActiveEffect status_effects = 11;
}

message ProtoActiveEffect {
    int32 effect_id   = 1;
    int64 expire_tick = 2;
    int32 stack_count = 3;
}
```

**어빌리티를 2필드로 줄인 근거.** `ActiveAbility`는 페이즈 경계를 3개
(`StartupEndTick`/`ActiveEndTick`/`RecoveryEndTick`) 들고 있지만, 끝나는 틱 하나만 있으면
나머지는 클라가 역산할 수 있다. 각 페이즈 길이는 `TbAbility`에 있고 클·서가 같은 마스터데이터를
보기 때문이다.

```
발동 틱     = ability_end_tick − 전체 길이
진행도      = (현재 틱 − 발동 틱) / 전체 길이
현재 페이즈  = 진행도가 어느 구간에 속하는지
```

**크기 부담.** protobuf는 기본값(0/false/빈 목록)을 전송하지 않으므로:

| 상황 | 추가 바이트 |
|---|---|
| 평상시 (땅 위, 시전 X, 버프 X) | ~2 (`grounded=true`만) |
| 시전 중 | ~9 |
| 버프 1개당 | ~+14 |

`EntitySnapshotBroadcastSystem`의 청킹 예산(메시지당 1000바이트)에 실질적 영향이 없다.

### 4-3. 진실은 하나 — 기존 규칙을 그대로 적용

새 규칙을 만들지 않는다. 위치가 이미 따르는 규칙에 세 값을 태운다.

> **예측하는 것(내 캐릭터)은 로컬이 진실** — 스냅샷 필드 무시, 보정은 롤백(`Reconciler`)이 담당.
> **예측하지 않는 것(남의 캐릭터)은 스냅샷이 진실** — 받은 값을 그대로 반영.

| | 내 캐릭터 | 남의 캐릭터 |
|---|---|---|
| 위치·속도 | 로컬 예측 (기존) | 스냅샷 + 보간 (기존) |
| 접지 | 로컬 계산 (지연 0) | 스냅샷 |
| 시전 상태 | 로컬 예측 | 스냅샷 |
| 상태이상 | 로컬 예측 | 스냅샷 |

내 캐릭터는 `Simulated` 마커가 있어 `LOPWorld.Tick`이 이동·어빌리티·상태이상·키네마틱을 모두
돌린다. 즉 **세 값 모두 로컬에 이미 채워져 있으므로** 스냅샷 필드를 무시하면 된다. 남의 캐릭터는
그 반대라 스냅샷이 유일한 출처다.

각 값마다 진실이 정확히 하나이므로, 두 경로가 어긋날 여지가 구조적으로 없다.

**접지의 지연 우려에 대해.** 내 캐릭터는 로컬에서 즉시 계산하므로 지연이 0이다. 남의 캐릭터는
위치 자체가 이미 `RemoteInterpolationClock` 기준으로 과거를 재생 중이므로, 접지도 같은 과거
시점 값이어야 **정합적**이다. 접지만 현재 값을 쓰면 "공중에 뜬 위치인데 착지 애니"가 된다.

## 5. 흐름

### 5-1. 서버 — 스냅샷 채우기

`EntitySnapshotBroadcastSystem.BuildAllEntitySnaps`에 추가. HP·`MotionContributions`를 채우는
기존 코드와 같은 패턴이다.

```csharp
snap.Grounded = worldEntity.Get<GroundState>()?.IsGrounded ?? false;

var active = worldEntity.Get<Abilities>()?.ActiveAbility;
if (active != null)
{
    snap.ActiveAbilityId = active.Value.AbilityId;
    snap.AbilityEndTick  = active.Value.RecoveryEndTick;
}

var effects = worldEntity.Get<StatusEffects>();
if (effects != null)
{
    foreach (var e in effects.Effects)
    {
        snap.StatusEffects.Add(new ProtoActiveEffect
        {
            EffectId = e.EffectId, ExpireTick = e.ExpireTick, StackCount = e.StackCount,
        });
    }
}
```

`ActiveEffect.SourceEntityId`/`SourceId`는 보내지 않는다 — 연출에 필요 없다.

### 5-2. 클라 — 남의 캐릭터에 반영

`GameEntityMessageHandler.OnEntitySnapsToC`의 **원격 분기**(HP를 반영하는 `else` 블록)에
나란히 붙인다. 내 캐릭 분기(`reconciler.AddServerSnap`)는 **손대지 않는다**.

시전 상태를 되살릴 때 페이즈 경계를 마스터데이터로 역산하며, 부분 복원임을 이름으로 밝힌다:

```csharp
/// <summary>
/// 연출용 부분 복원 — 어빌리티 id와 페이즈 경계만 채운다.
/// 효과 목록·이동 스케일·점프 봉인 같은 시뮬 파라미터는 비운다: 클라는 원격 어빌리티를 실행하지 않는다.
/// </summary>
public static ActiveAbility ForPresentation(
    int abilityId, long startupEndTick, long activeEndTick, long recoveryEndTick)
```

`Target`은 현재 항상 시전자 자신이지만(§7 참고) 뷰가 쓰지 않으므로 채우지 않는다.

클라의 `Health`가 서버가 준 숫자만 갖고 데미지 계산 재료(공격자 스탯·주사위)는 모르는 것과
같은 상황이다. 권위 값만 받고 계산 재료는 모르는 것이 서버 권위 모델의 정상 상태다.

> **검토 후 기각한 대안**: 원격 전용 컴포넌트(`AbilityCastState` 등)를 따로 두는 안. 내 캐릭에서
> `ActiveAbility` → 그 컴포넌트로 매 틱 복사하는 단계가 생기고, 그 사본이 낡을 위험이 있다.
> 같은 타입을 쓰되 부분 복원임을 팩토리 이름으로 드러내는 쪽이 낫다.

### 5-3. 뷰 — 상태를 애니로

`LOPEntityView`가 매 프레임 상태를 읽어 그린다. **내 캐릭·남의 캐릭 구분 없는 코드 한 벌**이다.

```csharp
private void Update()
{
    UpdateLocomotion();   // velocity + GroundState → 걷기/공중
    UpdateAbility();      // ActiveAbility + TbAbilityView → Animator.Play
}
```

진행도 계산은 **순수 함수로 LOP-Shared에 둔다.** 클라 코드는 전부 `Assembly-CSharp`라 EditMode
단위 테스트가 불가능한데, 이 작업에서 틀리기 쉬운 유일한 계산(경계 틱 ↔ 진행도 환산)이 정확히
여기이기 때문이다.

```csharp
namespace LOP
{
    /// <summary>시전 진행도 환산 커널(순수). 컨텍스트 없는 계산이라 static.</summary>
    public static class AbilityPlayback
    {
        public static bool Solve(in ActiveAbility a, long currentTick, long totalTicks,
                                 out AbilityPhase phase, out float normalizedTime);
    }
}
```

컨벤션대로 순수 커널에는 `*System` 이름을 붙이지 않는다.

**기준 시각.** 남의 캐릭터가 위치와 **같은 시계**를 쓰기 때문에 "위치는 0.15초 전인데 애니는
지금"이라는 어긋남이 생기지 않는다.

| | 기준 시각 |
|---|---|
| 내 캐릭터 | 현재 예측 틱 (`runner.tickUpdater.tick`) |
| 남의 캐릭터 | `RemoteInterpolationClock.RenderTime / gameInfo.Interval` — 위치 보간과 같은 시계 |

### 5-4. 삭제되는 것

| 대상 | 위치 |
|---|---|
| `"Plane"` 이름으로 접지 판정 | `LOPEntityView.IsGrounded` |
| 하드코딩 cue → 트리거 후보 dict | `LOPEntityView.CueTriggers` |
| `AbilityActivated` 구독 (애니 트리거) | `LOPEntityView.Start` |
| `Cue` 컬럼 | `TbAbility` (→ `TbAbilityView`로 이전) |

> `AbilityActivatedEvent` 경로 자체(코어 이벤트 → `AbilityActivatedToC` 와이어 →
> `WorldEventSink`)는 **남긴다.** 발동 *순간*의 일회성 연출(시전 사운드, 캐스팅 이펙트 튐)은
> 여전히 이벤트가 맞는 자리이기 때문이다. 이 슬라이스 이후 **일시적으로 소비자가 없는 상태**가
> 되며, 사운드를 붙일 때 되살아난다. proto 재생성 리스크(MessageId 이동)를 피하는 실용적 이유도
> 있다.

## 6. 뷰 테이블 (클라 전용 마스터데이터)

연출 매핑을 코드에서 데이터로 옮긴다. 어빌리티 데이터가 자기 몽타주 참조를 들고 있는 언리얼
GAS의 모양과 같되, 클·서 분기를 위해 별도 테이블로 둔다(Luban group `c`).

```
TbAbilityView
  ability_id (int, PK) │ anim_state  │ anim_layer
  ─────────────────────┼─────────────┼───────────
  1  (attack)          │ "Attack01"  │ 1
  2  (dash)            │ ""          │ 0          ← 빈 값 = 연출 없음

TbStatusEffectView
  effect_id (int, PK)  │ vfx_address              ← 비면 no-op
```

기본키는 정수 `id` — 마스터데이터 키 규약을 따른다.

**트리거가 아니라 스테이트 이름을 담는 이유.** `SetTrigger`로는 "60% 지점부터 재생"을 표현할 수
없다. 중간 진입은 `Animator.Play(state, layer, normalizedTime)` 로만 가능하고, 이것이 이번
작업의 핵심 목표(늦게 봐도 보이게)를 성립시킨다.

**미결**: 캐릭터마다 애니메이터 스테이트 이름이 다르면 키가 `(캐릭터, 어빌리티)`가 되어야 한다.
현재 애니메이터 에셋이 더미 상태라 판단이 이르므로 **어빌리티 단위로 시작**하고, 실제 캐릭터가
들어올 때 필요하면 확장한다. (언리얼도 같은 스켈레톤을 공유해 어빌리티당 몽타주 하나로 간다.)

## 7. 상태이상 부여 대상 데이터화

`StatusEffectApplyEffectHandler`는 이미 존재하지만 **대상을 `ctx.Target`에서 고른다.** 그런데
클·서 `AbilityActivator`가 모두 `abilitySystem.TryActivate(caster, ability, caster, tick)`으로
호출하므로 **`Target`은 항상 시전자 자신**이다. 즉 현재 구조로는
**"때린 상대에게 상태이상 부여"가 원천적으로 불가능**하다.

명중자에게 무언가를 적용하는 경로는 따로 있다 — `ctx.HitContext.LandedTargets`이며,
`KnockbackEffectHandler`가 이 방식을 쓴다(넉백 = on-hit 라이더). `attack`은 광역 스윕이라
발동 시점에 대상이 없고 명중 후에야 대상이 정해지기 때문이다.

헤이스트가 지금 동작하는 이유는 자기 자신에게 거는 버프이기 때문이다.

### 대상은 두 종류 — 정해지는 시점이 다르다

| | 대상이 정해지는 시점 | 담기는 곳 | 해당 어빌리티 |
|---|---|---|---|
| **사전 지목** | 발동 **전** — 미리 대상을 골라 시전 | `ActiveAbility.Target` | **현재 없음** |
| **명중 판명** | 발동 **후** — 맞아서 대상이 정해짐 | `HitContext.LandedTargets` | `attack` (→ 데미지·넉백·슬로우) |

슬로우는 **명중 판명** 쪽이다. 공격을 휘두르는 순간에는 누가 맞을지 알 수 없고, 때린 뒤에야
대상이 나오기 때문이다. 따라서 슬로우를 추가해도 `Target`에는 여전히 시전자만 들어간다.

**`Target` 필드는 존치한다.** 이 슬라이스 이후 읽는 코드가 없어지지만, "발동 전에 대상을
지목하는" 스킬을 위한 자리로 예약해 둔다. 죽은 필드로 오해되지 않도록 `ActiveAbility.Target`에
그 취지를 XML 주석으로 남긴다:

```csharp
/// <summary>
/// 발동 전에 미리 지목한 대상. 현재 모든 어빌리티가 self 또는 광역 스윕이라 항상 시전자가
/// 들어가며 읽는 곳이 없다 — 대상 지목형 스킬이 생길 때를 위한 자리.
/// 명중해서 정해지는 대상은 여기가 아니라 <see cref="AttackHitContext.LandedTargets"/>에 있다.
/// </summary>
```

| 변경 | 내용 |
|---|---|
| 마스터데이터 | `StatusEffectApplyEffect`에 `target_mode` 컬럼 (`self` / `hit_targets`) |
| 코드 | `StatusEffectApplyEffectHandler`가 `target_mode`로 분기 — `self`는 `ctx.Caster`, `hit_targets`는 `ctx.HitContext.LandedTargets` 순회 |
| 데이터 | 슬로우 상태이상 행 추가 (`MoveSpeed` / `PercentAdd` / 음수) + `attack`에 부여 효과 추가 + 기존 헤이스트에 `self` 명시 |

기본값은 `self`로 둔다. 현재 `Target`이 항상 시전자이므로 `self`(→ `ctx.Caster`)는 기존 동작과
정확히 같고, 따라서 기존 데이터의 거동이 변하지 않는다.

이 변경으로 **예측되지 않는 디버프**(남이 나에게 건 것) 경로가 처음으로 생기며, 이번 작업이
고치려는 문제를 실제로 검증할 수 있게 된다. 헤이스트는 자기 버프라 로컬 예측으로 이미 보이므로
검증 대상이 아니다.

## 8. 구현 주의점 — Animator 개입 빈도

`Animator.Play(state, layer, progress)`를 **매 프레임 호출하면 안 된다.** 매번 그 지점으로
강제되면서 블렌딩·전이가 무력화된다.

| 시점 | 동작 |
|---|---|
| 시전 **시작** 감지 (직전 프레임과 어빌리티 id가 달라짐) | `Play(state, layer, 진행도)` — 늦게 들어왔으면 중간부터 |
| 시전 **중** | 개입하지 않음 — 애니메이터가 자기 시간으로 흐름 |
| 애니메이터의 `normalizedTime`이 계산된 진행도와 0.1 넘게 벌어짐 | 재동기 `Play` |
| 시전 **종료** | Locomotion으로 복귀 |

즉 **상태는 매 프레임 읽되, 애니메이터에는 어긋났을 때만 개입한다.** Mirror `NetworkAnimator`가
state hash 변경 시에만 `Play`하고 `normalizedTime` 오차가 클 때 보정하는 것과 같은 형태다.

## 9. 슬라이스

건드리는 저장소가 많다(GameFramework / LOP-Shared / MasterData-Client / infrastructure /
Client / Server). 각 슬라이스가 독립적으로 검증 가능하도록 잘게 유지한다.

| | 슬라이스 | 내용 | 검증 가능한 결과 |
|---|---|---|---|
| **1** | 접지 | `GroundState` + `KinematicMoveSystem` 한 줄 + 와이어 1필드 + 서버/클라 + 뷰 | 남의 캐릭터 점프·낙하 애니가 정상. `"Plane"` 판정 삭제 |
| **2** | 시전 상태 | 와이어 2필드 + `ForPresentation` + `AbilityPlayback` + `TbAbilityView` + 뷰 전환 | 유실·늦은 시점에도 스킬 모션이 보임. 트리거 방식 제거 |
| **3** | 상태이상 | 와이어 목록 + `target_mode` + 슬로우 데이터 + `TbStatusEffectView` 훅 | 남이 건 디버프를 클라가 인지 |

**슬라이스 1을 먼저 하는 이유**: 가장 작으면서
"World 상태 추가 → 와이어 → 서버 채움 → 클라 반영 → 뷰 읽기"라는 전체 배관을 한 바퀴 돈다.
2·3은 같은 길을 따라간다.

## 10. 검증

### 10-1. 자동 테스트 (EditMode — 테스트 가능한 패키지에만)

| 대상 | 위치 | 케이스 |
|---|---|---|
| `AbilityPlayback.Solve` | LOP-Shared | 페이즈 경계 정확히 위/아래, 시작 전, 종료 후, 진행도 0·1 |
| `StatusEffectApplyEffectHandler` | LOP-Shared | `self` → 시전자에게만, `hit_targets` → 명중자 전원, 명중 0명 |
| `KinematicMoveSystem` 접지 기록 | LOP-Shared | 착지/공중 |

### 10-2. 인게임 검증

매치 도중 접속이 지원되지 않으므로 "늦게 들어온 사람"을 직접 재현할 수 없다. **같은 원인의 다른
증상**으로 대체한다.

| 시나리오 | 방법 | 기대 |
|---|---|---|
| 연출 이벤트 유실 | Latency Simulation 손실 20~30% + 2에디터 공격 연타 | **전**: 상대 화면에서 모션이 가끔 통째 누락 / **후**: 스냅샷이 덮으므로 항상 보임 |
| **자족성** | 디버그 토글로 연출 이벤트 수신을 완전 차단 | 스냅샷만으로 스킬 모션 정상 재생 = 늦게 들어와도 되는 것 |
| 롤백 중복 | 손실 환경에서 내 캐릭 공격 연타 | 모션이 두 번 튀지 않음 |
| 타임라인 정합 | 상대가 이동하면서 공격 | 위치와 모션이 같은 시점 — 도착 전에 모션이 먼저 나오지 않음 |
| 디버프 | 상대를 때림 | 상대에게 슬로우가 걸리고, 내 화면에서 그 사실을 인지 |

**자족성 토글이 "늦게 접속"의 대역**이다. 이벤트 없이 상태만으로 굴러가면 목표를 달성한 것이다.

### 10-3. 회귀

걷기 / 점프 / 공격 / 대시 / 헤이스트 / 피격 리액션 스모크.

## 11. 산업 표준 매핑

| 우리 결정 | 대응하는 표준 |
|---|---|
| 시전 상태를 지속 상태로 복제 | Mirror `NetworkAnimator` / Photon `PhotonAnimatorView`가 state hash + `normalizedTime`을 주기 전송하는 것과 같은 목적(**재개 가능성**). 단 우리는 애니메이터 계층이 아니라 게임 상태 계층에서 한다 |
| 애니를 상태에서 파생 | 언리얼 — 애니 복제 컴포넌트가 없고 AnimBP가 복제된 `Velocity`/`MovementMode`를 읽는다. Photon Quantum — 뷰가 시뮬 상태를 읽어 구동 |
| 접지 복제 + 로컬 예측 병행 | 언리얼 `CharacterMovementComponent.MovementMode` 복제, Source `FL_ONGROUND` 네트워킹, Photon Fusion `NetworkCharacterController.Grounded`. 예측과 복제는 대안이 아니라 같은 값의 두 경로 |
| 어빌리티가 자기 연출 데이터를 참조 | 언리얼 GAS — `GameplayAbility`가 몽타주 참조를 보유 |
| `GroundState` 명명 | 언리얼 `MovementMode`가 자랄 자리. 현재는 지상/공중 두 상태뿐이라 `bool`로 시작 |

**우리 방식이 애니메이터 파라미터 복제(A)보다 나은 점**:

| | 애니메이터 파라미터 복제 | 이 설계 |
|---|---|---|
| 권위 | 클라 Animator 상태 (서버는 모름) | 서버 시뮬이 진실 |
| 롤백 | 트리거 중복 발사 | 상태가 되돌아가면 애니도 자동 |
| 판정 연동 | 애니와 히트박스가 따로 논다 | 페이즈가 곧 판정 |
| 크기 | 애니 클립 수에 비례 | ~9바이트 |

## 12. 범위 밖 (후속 D)

- 상태이상 실제 이펙트 에셋 · 상태 아이콘 UI (아트 미도착)
- 발 미끄러짐 보정 (애니 재생속도를 실제 수평속도에 맞추기)
- 애니 타이밍 미세 조정
- `AbilityActivatedEvent` 경로에 일회성 연출(시전 사운드·캐스팅 VFX) 붙이기
- 스냅샷 델타 압축·양자화
- **대상 지목형 스킬** — 발동 전에 적을 골라 시전하는 어빌리티. `ActiveAbility.Target`이 그
  자리로 예약돼 있다(§7). 그런 스킬이 실제로 생길 때 설계한다

## 13. Open Decisions

- [ ] `TbAbilityView` 키를 `(캐릭터, 어빌리티)`로 확장할지 — 실제 캐릭터 에셋이 들어올 때 판단
- [ ] `GroundState`를 `MovementMode` enum으로 승격할 시점 — 수영·비행이 생길 때
- [ ] 슬로우 수치 밸런스 — 검증용으로 약하게 시작 (예: `MoveSpeed` −5%, 1초)
