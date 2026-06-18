# World Core 뷰 이행 — 레거시 HealthComponent 제거, World.Health로 진실원본 일원화

**Date:** 2026-06-18
**Branch:** `feature/world-core-health-replace-legacy`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Entity System Design](../../entity-system-design.md) · [Slice A spec](2026-06-16-world-core-view-slice-a-design.md) · [Slice 3 spec](2026-05-30-world-core-slice3-design.md)

## Goal

레거시 `HealthComponent`(MonoBehaviour, `LOPComponent`)를 **제거**하고, HP의 단일 진실원본을 순수 C# 코어 **`World.Health`**(`GameFramework.World`)로 일원화한다. 클라의 남은 `HealthComponent` 사용처 3곳(쓰기 2 + 읽기 1)을 `World.Health`로 repoint한 뒤 컴포넌트를 삭제한다.

Slice A([2026-06-16](2026-06-16-world-core-view-slice-a-design.md))가 `CharacterNameplate`의 HP **읽기**를 `World.Health`로 옮긴 데 이은 후속으로, 이 슬라이스가 남은 소비자를 정리해 레거시 컴포넌트를 걷어낸다. inside-out 이행(Model A)에서 "reader를 코어로 옮기고 레거시 제거"의 한 칸.

동작 변화 없음(화면 HP 표시는 이전과 동일) — 가치는 *레거시 store 제거 + 진실원본 일원화*다.

## 현재 상태 (조사 결과)

`World.Health`는 이미 **쓰기 측이 살아서 동작**한다 — `CharacterCreator`가 스폰 시 등록하고, 데미지 흐름(`DamageEventToC` → `WorldEventBuffer` → `LOPGameEngine.ProcessEvent` → `WorldEventApplicator.Apply` → `HealthSystem.ApplyDamageDealt`)이 매 데미지마다 `World.Health.Current`를 갱신한다. 다만 그 갱신값을 읽는 소비자가 `CharacterNameplate`의 스폰 1회 read뿐이라 shadow에 가깝다.

레거시 `HealthComponent`의 남은 사용처(클라 전수):

| 종류 | 위치 | 내용 |
|---|---|---|
| 쓰기 ① | `EntityCreator/CharacterCreator.cs:47-49` | 스폰 시 `AddEntityComponent` + `Initialize(maxHP, currentHP)` |
| 쓰기 ② | `Game/MessageHandler/Game.Entity.MessageHandler.cs:170-171` | `OnUserEntitySnapToC` — 유저 본인 HP를 권위적 스냅샷으로 직접 덮어씀 |
| 읽기 ① | `UI/CharacterHud/CharacterHudViewModel.cs:18,47` | 생성 시 1회 초기 maxHP/currentHP (이후 라이브 HP는 `EntityDamage` 이벤트로 갱신) |

`HealthComponent.TakeDamage`는 이미 사문(LOP 호출처 없음). `CharacterNameplate`는 Slice A에서 이미 `World.Health` read로 이전됨.

### 이 대체가 해소하는 잠재 불일치

현재 **유저 본인** HP는 두 store로 갈라져 쓰인다 — 데미지 흐름은 `World.Health`(`ApplyDamageDealt`)에, 권위 스냅샷은 `HealthComponent`(쓰기 ②)에. 둘을 `World.Health`로 모으면 단일 진실원본이 되어 갈라짐이 사라진다.

## 설계 결정 (브레인스토밍 합의)

| 결정 | 선택 | 이유 |
|---|---|---|
| 접근 | **최소 store-swap, 동작 보존** | 사용자 의도("레거시를 새 구현부로 대체")에 정합. 회귀 0. 라이브 일관성 개선(B)·진실원본 완전 승격(C)은 defer |
| 스냅샷 쓰기 방식 | **`HealthSystem` Application 메서드 경유** | Anemic 원칙(상태 변경은 System) 준수. 핸들러를 logic-free로 유지. 컴포넌트 setter를 핸들러에서 직접 찌르지 않음 |
| HUD 라이브 HP | `EntityDamage` 구독 **그대로** | 현 동작 보존. 스냅샷 발 비데미지 HP 변화의 라이브 반영은 현재도 없음 → 범위 밖 |
| 범위 | **클라 전용, Health만** | 서버 `HealthComponent`는 별개 이행(CLAUDE.md). Mana/Level/Stats/User 레거시 컴포넌트는 유지 |

## Scope (In)

### Core (`GameFramework.World`, 순수 C#)

**`HealthSystem`에 Application 메서드 추가** — 권위적 상태 덮어쓰기(스냅샷):

```csharp
/// <summary>
/// 권위 스냅샷 등으로 Max/Current를 통째로 덮어쓴다. 결정/계산/가드 없음 (Application 메서드).
/// Current는 [0, Max] 범위로 클램프해 무결성만 보장.
/// </summary>
public void ApplyAuthoritativeState(Health health, int max, int current)
{
    health.Max = max;
    health.Current = Math.Clamp(current, 0, max);
}
```

> 클램프는 무결성 가드(음수/초과 방지)일 뿐 도메인 분기가 아니다. 서버 권위 값은 정상 범위를 전제하므로 실질 no-op이지만, `SetMax`가 이미 `Current > Max` 보정을 하는 것과 같은 수준의 데이터 무결성 보장이다.

### Client LOP

**`Game/MessageHandler/Game.Entity.MessageHandler.cs`** (쓰기 ②):
- `[Inject] private GameFramework.World.EntityRegistry entityRegistry;` 추가 (World 타입 풀 네임스페이스 한정 — 프로젝트 컨벤션).
- `[Inject] private GameFramework.World.HealthSystem healthSystem;` 추가.
- `OnUserEntitySnapToC`의 HP 두 줄을 교체:
  ```csharp
  // 제거
  playerContext.entity.GetComponent<HealthComponent>().currentHP = userEntitySnapToC.CurrentHP;
  playerContext.entity.GetComponent<HealthComponent>().maxHP = userEntitySnapToC.MaxHP;
  // 대체
  GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entity.entityId);
  GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
  if (health != null)
  {
      healthSystem.ApplyAuthoritativeState(health, userEntitySnapToC.MaxHP, userEntitySnapToC.CurrentHP);
  }
  ```
- **Mana/Level/User 등 나머지 줄(172-176)은 그대로 유지** — 이번은 Health만.

**`UI/CharacterHud/CharacterHudViewModel.cs`** (읽기 ①):
- `HealthComponent _health` 필드 제거.
- 생성자에 `EntityRegistry` 주입 추가, 생성 시 `World.Health`에서 초기 maxHP/currentHP 읽기:
  ```csharp
  GameFramework.World.Entity worldEntity = entityRegistry.Get(_entity.entityId);
  GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
  if (health != null) { _hp.Value = health.Current; _maxHp.Value = health.Max; }
  ```
- 라이브 갱신(`EntityDamage` 구독 → `_hp`)은 그대로. `PushAll`에서 Health 분기만 교체, Mana/Level 분기는 유지.
- null 가드: 미등록 시 기존 폴백(`_hp=0`, `_maxHp=1`)과 동등.

**`EntityCreator/CharacterCreator.cs`** (쓰기 ①):
- `HealthComponent` 생성 3줄(47-49) 제거. `World.Health` 등록(96-97줄)은 그대로 진실원본 역할.

**삭제:**
- `Assets/Scripts/Component/HealthComponent.cs` (+ `.meta`).

### DI 배선

`HealthSystem`은 `GameLifetimeScope`에 이미 Singleton 등록되어 있음(Slice 3) — `Game.Entity.MessageHandler` 주입에 추가 등록 불필요. `EntityRegistry`도 Singleton 등록됨. ViewModel은 Transient 등록됨 → 생성자 인자에 `EntityRegistry` 추가 시 자동 주입.

## 데이터 흐름 (대체 후)

```
유저 본인 HP:
  데미지     → DamageEventToC → WorldEventBuffer → ProcessEvent
                → HealthSystem.ApplyDamageDealt → World.Health.Current      ┐
  권위 스냅샷 → UserEntitySnapToC                                            ├→ World.Health (단일 store)
                → HealthSystem.ApplyAuthoritativeState → World.Health.Max/Current ┘

  표시: World.Health(스폰 초기값) + EntityDamage 이벤트(라이브) → HUD / Nameplate HP바
```

스폰 초기값 read는 `World.Health`, 라이브 HP는 `EntityDamage` 프레젠테이션 이벤트 — Slice A의 Nameplate와 동일 패턴으로 HUD도 통일된다.

## Out of scope (defer)

- **라이브 일관성 개선(접근 B)** — 스냅샷 발 비데미지 HP 변화(힐/리젠)를 HUD/Nameplate에 라이브 반영. 현재도 없는 동작이라 범위 밖.
- **진실원본 완전 승격(접근 C)** — `EntityDamage` 표시까지 `World.Health` pull로 재구성. Stage ④ 영역.
- **서버 측 `HealthComponent` 이행** — 별개 작업(CLAUDE.md — 서버는 명시 요청 시).
- **Mana/Level/Stats/User 등 나머지 레거시 컴포넌트** — 각자 별도 슬라이스.

## GUID / .meta 정책

- **삭제**: `HealthComponent.cs`+`.meta`는 `git rm`으로 함께 제거. `HealthComponent`는 엔티티에 코드로 `AddEntityComponent`되는 컴포넌트(씬/prefab의 SerializedField가 GUID로 직접 참조하지 않음 — `CharacterCreator`가 런타임 생성) → GUID 참조 끊김 0.
- **수정 대상 3파일**: 파일·클래스명 유지 → `.meta` 영향 없음.

## 검증

- **컴파일**: 클라 0에러 (UnityMCP `refresh_unity` + `read_console`, 클라 인스턴스 핀). 코어(`HealthSystem`) 변경은 *추가만*(`ApplyAuthoritativeState`)이라 비파괴 — 서버도 file: 참조하므로 양쪽 컴파일 확인 권장.
- **코어 EditMode 테스트**(`baegames.GameFramework.World.Tests`): `HealthSystem.ApplyAuthoritativeState` 추가 — Max/Current 덮어쓰기 + Current 클램프(음수→0, 초과→Max) 검증. 순수 C#이라 저비용·고ROI → 추가.
- **런타임 플레이(수동)**: 유저 HUD HP 초기값 정상, 피격 시 HP바·HUD 숫자 감소, 네임플레이트 HP바 갱신, 사망, 콘솔 에러 0. World Core slice 3 회귀(`[World] Death entity ...`) 정상. 동작 변화 없음(회귀 0)이 곧 성공.

## 진행

- [x] 현재 구조 조사 (사용처 3곳 + World.Health 쓰기 측 동작 확인 + 잠재 불일치 식별)
- [x] 접근(A: 최소 swap) + 스냅샷 쓰기 방식(HealthSystem 경유) 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] 구현 → 검증 → 머지

> 후속: 진실원본 완전 승격(접근 C, Stage ④ 결합) + Mana/Level 등 나머지 레거시 컴포넌트 이행. 각자 별도 spec.
