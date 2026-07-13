# 공격 어빌리티 이동 정책 — 페이즈별 이동 배율 + 점프 차단

**Date:** 2026-07-14
**Branch (제안):** `feature/attack-movement-policy`
**Related:** [entity-system-design](../../entity-system-design.md) · [world-core-connection-architecture](../../world-core-connection-architecture.md) · [netcode-redesign](../../netcode-redesign.md) · `2026-06-28-ability-behavior-composition` · `2026-07-13-unified-world-tick-client-sim-scope-design`

## Goal

공격 중 캐릭터가 **움직이며·미끄러지며 때리는 어색함**을 없애고, 공격에 **커밋(commitment)** 을 부여한다. 어빌리티마다 **페이즈별 이동 배율**을 데이터로 선언해 소울류(완전 정지)·감속·젤다식(어빌리티별 차등)을 같은 시스템 위에 얹는다. **공격 중 점프도 차단**할 수 있게 한다.

한 줄 요약: **어빌리티가 "준비/타격/마무리 각 토막에서 이동을 얼마나 허용할지"를 데이터로 들고, 공유 이동 시뮬이 목표속도에 그 배율을 곱한다.** 회전(조준)은 그대로 자유.

## 배경 / 문제

현재 공격은 어빌리티(`DamageEffect`)이지만 이동을 전혀 제한하지 않는다:

- **플레이어**: 걷던 관성/입력이 공격 중에도 살아 있어 **이동+미끄러짐**. 걷기 모터가 brake-to-desired라 입력만 0이면 칼정지지만, 공격은 입력을 막지 않음.
- **AI 몬스터**: `EnemyBrain`이 "사거리 밖=이동 / 사거리 안=공격"으로 **배타적**. 접근하다 공격에 들어가면 이동 branch를 안 타서 **직전 velocity가 안 지워지고 그대로 미끄러짐**.

기존에 이동을 막는 유일한 경로는 **대시 전용**이다: `AbilitySystem.HasActiveMotionEffect`(Active 페이즈 + `MotionEffect` 보유)일 때 클라 `PlayerInputManager`가 이동 입력을 0으로 만든다. 공격류는 `MotionEffect`가 없어 이 게이트에 안 걸린다.

→ **"이동 잠금 = MotionEffect 보유"라는 커플링을 "어빌리티의 이동 정책(데이터)"으로 일반화**하는 것이 이 작업의 본질.

## 업계 표준 매핑

새 발명이 아니라 두 업계 정석의 조합이다:

| 이 설계 | 업계 표준 | 근거 |
|---|---|---|
| 페이즈 = Startup / Active / Recovery | **격투게임 프레임데이터** — 공격의 3토막. 특히 *"Recovery = 캐릭터가 다시 움직일 수 있기까지의 구간"* 이 `RecoveryMoveScale`과 정확히 일치 | Dustloop, CritPoints, 2XKO |
| 페이즈별 **이동속도 배율** | **Unreal GAS** — 어빌리티 활성 중 이동속도 attribute에 modifier(감속/정지). "차징=이속 감소", "스턴=이속 0"이 문헌 예시. 배율 0.4=차징식, 0=스턴식 | GASDocumentation, Gamebreaking |
| `BlockJump` 플래그 | **GAS Activation Blocked Tags** — 공격 중 점프/이동 어빌리티 차단 | GASDocumentation |

**뉘앙스(정직):** 소울류 *원본*은 **루트 모션**(공격 애니가 위치를 직접 밀어냄)을 쓴다. 우리는 **속도 배율**(코드가 속도 계산)을 쓰는데, 이게 우리에게 맞는 표준이다 — LOP는 이미 **공유 키네마틱 컨트롤러**(예측=권위)라 루트 모션과 안 맞고, 루트 모션+롤백은 넷코드에서 어렵다. 예측형 게임(오버워치·MOBA)은 코드 구동 속도 제어를 쓴다. 즉 소울의 *느낌*을 넷코드-친화적 *방식*으로 재현.

출처: Dustloop *Using Frame Data*, CritPoints *Frame Data Patterns*, 2XKO *Frame Data Guide*, tranek *GASDocumentation*, Gamebreaking *Ability Systems*.

## 스코프

| | 포함 여부 | 비고 |
|---|---|---|
| **① 페이즈별 이동 배율** (0~1) | ✅ v1 | 소울류(0/0/0)·감속(0.4)·젤다(어빌별) 커버. 플레이어+AI. |
| **② 점프 차단** (`BlockJump`) | ✅ v1 | 공격 발동 중 점프 무시. 플레이어 대상(AI는 점프 안 함). |
| **③ AI 루팅** | ✅ v1 | 공유 틱 덕에 별도 코드 0(아래). `activeMove=0`은 완결, 분수 배율은 AI 행동 확장 필요. |
| **④ 회전(조준) 자유** | ✅ v1 | 배율은 속도에만 → 방향은 raw 입력 유지. |
| **⑤ 이동-캔슬**(벌처 컨트롤: 이동 입력이 후딜 조기종료) | ⏸ **v2 (후속 슬라이스)** | 페이즈 조기종료 + 넷코드 재생 결정론 → 별도 brainstorm/검증. |

## 설계

### 접근법 결정 — 배율을 어디서 강제하나

**공유 `MovementSystem.Tick`에서 수평속도에 곱한다.** 후보 비교:

- **✅ 공유 시뮬**: 클라 예측·서버 권위·`Reconciler` 재생이 같은 코드 → **라이브==재생 자동**(넷코드 결정론 공짜). 서버에선 AI도 이 틱을 타므로 플레이어+AI 한 곳에서 처리.
- **❌ 클라 입력 캡처에서 입력값 깎기**(대시 방식): 입력의 horizontal/vertical을 깎으면 **회전까지 얼어붙음**(회전이 같은 입력에서 파생 — 아래). "회전 허용"과 충돌. 클라 전용이라 AI 미적용.

대시가 쓰는 입력-제로화는 대시 전용으로 그대로 둔다(대시는 방향도 잠그는 게 맞음).

### 데이터 모델

`TbAbility`(Luban 테이블)에 **4칸 추가**:

| 컬럼 | 타입 | 뜻 | 기본값 |
|---|---|---|---|
| `StartupMoveScale` | float | 준비동작(Startup) 중 이동배율 | **1.0** |
| `ActiveMoveScale` | float | 타격(Active) 중 이동배율 | **1.0** |
| `RecoveryMoveScale` | float | 마무리(Recovery) 중 이동배율 | **1.0** |
| `BlockJump` | bool | 발동 중 점프 차단 여부 | **false** |

- 배율 0 = 완전 정지, 0.4 = 40% 속도, 1.0 = 자유. 기본 1.0/false → **데이터 미기입 시 기존 동작 그대로**(하위호환).
- 예: 소울 검 `0/0/0 + BlockJump` · 빠른 잽 `0.5/0/0.3` · 감속 `0.4/0.4/0.4` · 젤다 가벼운 베기 `0.7/0.3/0.5`.

흐름:
```
TbAbility(엑셀) → Luban 생성 Ability(.cs) → AbilityDataProvider.Map
   → AbilityData(4필드 추가) → TryActivate → ActiveAbility(4필드 실림)
```
`AbilityData`·`ActiveAbility` 두 struct에 4필드 추가(기존 페이즈 경계틱·Effects 옆). `ActiveAbility`는 발동 시 `AbilityData`에서 복사하며, `WithPhase`(페이즈 전진 시 새 struct 생성)도 4필드를 그대로 옮겨야 한다.

### 조회 API (`AbilitySystem`)

기존 `TryGetActiveMotionEffect`와 동일하게 **페이즈 enum이 아니라 경계틱으로** 판정(틱 정확, 시스템 실행순서 무관):

```csharp
// 진행중 어빌리티 없으면 1.0(자유). 있으면 현재 틱이 속한 토막의 배율.
public static float GetMovementMultiplier(Entity entity, long currentTick)
{
    var active = entity?.Get<Abilities>()?.ActiveAbility;
    if (active == null) return 1f;
    var a = active.Value;
    if (currentTick < a.StartupEndTick)  return a.StartupMoveScale;
    if (currentTick < a.ActiveEndTick)   return a.ActiveMoveScale;
    if (currentTick < a.RecoveryEndTick) return a.RecoveryMoveScale;
    return 1f;
}

// 발동 창(어느 페이즈든, RecoveryEnd 전) 안이고 BlockJump면 true.
// currentTick 경계로 판정 → GetMovementMultiplier와 대칭(페이즈 전진 순서 무관, 종료틱 1틱 잔류 방지).
public static bool IsJumpBlocked(Entity entity, long currentTick)
{
    var active = entity?.Get<Abilities>()?.ActiveAbility;
    return active != null && active.Value.BlockJump && currentTick < active.Value.RecoveryEndTick;
}
```

### `MovementSystem.Tick` 변경 (2곳)

**① 이동 배율 — 수평속도에 곱하기(전원 공통, 넉백 folding 직전):**

지금은 입력 블록 안에서 목표속도를 계산한다. 배율을 *입력 블록 밖*, `baseHorizontal` 최종값에 곱해 **플레이어(모터 결과)와 AI(잔류 velocity)를 한 줄로** 처리:

```csharp
// (입력 블록은 기존대로 full speed로 desired 계산 + 회전 세팅)
...
baseHorizontal *= AbilitySystem.GetMovementMultiplier(entity, currentTick);   // ← 추가(전원)
// 이후 MotionContributions(넉백) Resolve — 배율 뒤라 넉백은 안 깎임(맞음)
```

- 배율 0 → `baseHorizontal` 0 → 미끄러짐 없이 정지. 회전은 위에서 이미 세팅돼 살아있음.
- 대시 분기는 `baseHorizontal`을 자기 모션으로 override → 대시 어빌리티 배율은 기본 1.0이라 무영향.

**② 점프 게이트:**

```csharp
if (input.Jump && !AbilitySystem.IsJumpBlocked(entity, currentTick))
{
    velocity.y = statsSystem.GetValue(stats, JumpPower);
}
```

### 회전(조준) 분리 — 자동으로 성립

`ProcessMovement`가 이미 `desired = dir × speed`(속도)와 `rotation = atan2(dir)`(방향)을 **분리 계산**한다. 배율은 `baseHorizontal`(속도)에만 곱하므로 **회전 입력은 그대로** → 배율 0이어도 캐릭터는 제자리에서 입력 방향으로 돈다. "이동은 막되 조준은 허용"이 코드 변경 없이 성립.

### AI 루팅 — 공유 틱 덕에 별도 코드 0

서버에선 AI도 `Simulated` + `Velocity`라 이미 공유 `MovementSystem.Tick`을 탄다(InputBuffer만 없어 입력 블록만 스킵). 배율을 입력 블록 *밖*에서 곱하므로:

- **AI가 공격(`activeMove=0`) 중** → `baseHorizontal`(직전 이동 잔류 velocity) × 0 = 0 → **미끄러짐 소멸**. 서버 자동, 별도 코드 없음.
- **클라의 AI/원격 엔티티**는 `Simulated` 아님(스냅 팔로워) → 서버가 루팅한 결과가 스냅샷으로 와 보간됨. 자동.

**한계(정직):** `activeMove` **0(정지)** 은 AI에 정확. 하지만 **분수 배율(예: 0.4)** 은 AI에서 "40% 이동"이 아니라 "잔류 velocity 감쇠"로 나온다 — 현재 `EnemyBrain`이 공격 중 이동 의도를 표현하지 않기(이동 XOR 공격) 때문. "AI가 때리며 부분 이동"은 AI 행동 로직 확장(이 작업 밖). **대부분 공격은 정지(0)라 실사용 문제없음.** 플레이어는 분수 배율까지 완전 지원.

### 넷코드 결정론

- 배율·점프 게이트가 **공유 `MovementSystem.Tick`** 안 → 클라 예측·서버 권위·재생 동일.
- 배율은 `(어빌리티 상태, 현재 틱)`의 **순수 함수** — 부수효과·랜덤 없음. 재생 시 어빌리티 상태(`PredictedAbilityState`)가 스냅샷/복원되므로 배율도 그대로 재현.
- **캔슬(조기종료)이 없어 새 결정론 리스크 0** — v2로 미룬 이유.

### 기본값 / 하위호환 / 대시 / 넉백

- 기본 `1.0`/`false` → 기존 어빌리티(공격·대시) 동작 무변경.
- 대시: 배율 경로보다 우선(자기 모션 override). 배율 기본 1.0이라 대시 무영향. 모션 어빌리티에 분수 배율을 넣는 건 의도된 용법 아님.
- 넉백: 배율 뒤 합성 → 공격 중에도 밀림(외력이라 맞음).

## 엣지케이스 & Open Decisions

- **`BlockJump` 적용 범위**: v1은 발동 중 *전 페이즈*(Startup~Recovery). 페이즈별 점프 차단(예: Recovery만 점프-캔슬 허용)은 캔슬(v2)과 함께 볼 사안.
- **AI 분수 배율**: 위 "한계" — 필요해지면 `EnemyBrain`이 공격 중 이동 의도를 표현하도록 확장(별도 작업).
- **캔슬(v2)**: 이동 입력이 캔슬 가능 페이즈를 조기종료 → 벌처 컨트롤. 넷코드 재생 결정론 검증 필요.

## 영향 파일 (개략 — 세부는 plan)

- **데이터 파이프라인**: `infrastructure/table` Luban 스키마에 `TbAbility` 4컬럼 + 데이터 → 클·서 MasterData 패키지 regen.
- **LOP-Shared**: `AbilityData`(4필드), `Abilities.cs`의 `ActiveAbility`(4필드 + `WithPhase` 유지), `AbilitySystem`(`GetMovementMultiplier`/`IsJumpBlocked` + `TryActivate`에서 복사), `MovementSystem.Tick`(배율 곱 + 점프 게이트).
- **Client/Server**: `AbilityDataProvider.Map`(Luban 행 → `AbilityData` 4필드 복사).
- **Tests**: LOP-Shared EditMode.

## 테스트 (EditMode, LOP-Shared — 순수 로직)

1. `GetMovementMultiplier` — 틱이 startup/active/recovery 창일 때 각 배율, 어빌리티 없을 때 1.0, 경계틱 정확.
2. `IsJumpBlocked` — `BlockJump` true/false × 발동 중/후.
3. `MovementSystem.Tick`(플레이어) — `activeMove=0` 공격 발동 중 수평속도 0 수렴 **AND** 회전은 입력 따라 갱신(회전 살아있음). 점프 입력이 `BlockJump`면 velocity.y 미변경.
4. `MovementSystem.Tick`(AI 모사: InputBuffer 없음 + 잔류 velocity) — `activeMove=0` 발동 중 수평속도 0으로. 넉백 기여가 있으면 배율 뒤 합성돼 살아있음.

## 산업 표준 매핑 (네이밍 근거)

- `StartupMoveScale`/`ActiveMoveScale`/`RecoveryMoveScale`: 기존 `StartupTicks`/`ActiveTicks`/`RecoveryTicks`와 **prefix 짝**(Startup*/Active*/Recovery*). GAS의 이동속도 attribute modifier(0~1 배율)에 대응.
- `BlockJump`: GAS *Activation Blocked Tags*(어빌리티 활성 중 특정 액션 차단)에 대응.
- `GetMovementMultiplier`/`IsJumpBlocked`: 기존 `HasActiveMotionEffect`/`TryGetActiveMotionEffect`와 같은 **경계틱 기반 순수 읽기** 짝.

## Out of Scope

- **이동-캔슬(벌처 컨트롤, v2)** — 별도 spec.
- **루트 모션**(애니 구동 이동) — 넷코드상 채택 안 함(위 뉘앙스).
- **AI 행동 로직 개선**(공격 중 이동 의도, 경로탐색, `EnemyBrain` 회전 버그) — 무관한 AI 품질 트랙.
- **점프 페이즈별 차등** — 캔슬(v2)과 함께.

## 진행

- [x] 브레인스토밍 (스코프=배율+점프+AI루팅 / 캔슬=v2, 세밀도=페이즈별 A, 회전 허용, 업계표준 웹 확인)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
