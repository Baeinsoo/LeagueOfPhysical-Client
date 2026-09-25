# 서버 Health 이행 — Slice 1b: HP read flip (legacy → World.Health)

**Date:** 2026-06-18
**Branch (docs):** `feature/server-health-read-flip` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [Slice 1a spec](2026-06-18-server-creationdata-di-design.md) · [Health 대체 슬라이스(클라)](2026-06-18-world-core-health-replace-legacy-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

서버에서 HP를 읽는 두 소비자를 레거시 `HealthComponent` → 코어 `World.Health`로 전환한다. **동작 보존**(behavior-preserving): 서버 `World.Health`는 이미 `WorldEventApplicator`가 매 틱 legacy와 동기화하고(== legacy), HP를 읽는 두 소비자 모두 `ProcessEvent`(applicator 드레인) *후* 단계에서 실행되므로, 읽는 값이 이전과 동일하다 — 회귀 0.

서버 Health 3슬라이스(1a→1b→2)의 둘째 칸. 1a(`EntityCreationDataFactory` DI화)가 깔아둔 DI 덕에 `CharacterCreationDataCreator`가 이제 `[Inject] EntityRegistry`를 받을 수 있어 깔끔하게 World.Health에 접근한다.

## 위치 — 왜 read를 먼저 옮기나

서버 Health는 **writer↔reader 결합**(combat이 World.Health만 쓰면 legacy reader가 stale)이라 한 번에 옮겨야 하는 것처럼 보이지만, **`World.Health == legacy`(applicator 동기화)** 라는 사실이 분해를 가능케 한다: reader를 *먼저* 동일값인 World.Health로 옮겨도 동작이 안 변한다(1b). 그 뒤 Slice 2에서 writer를 옮기고 legacy를 제거하면 reader는 이미 World.Health를 보고 있어 안전하다. 1b는 그 behavior-preserving 첫 발이다.

## 잠긴 결정 (브레인스토밍 합의)

- **HP만 이전** — MaxHP/CurrentHP만 World.Health로. MP/Exp/Level/StatPoints 등 다른 스냅샷·생성데이터 필드는 legacy 컴포넌트 유지(각자 별도 이행). "Health 슬라이스" 범위 일관.
- **null 가드 = `Debug.LogWarning` + 안전값** — 캐릭터는 항상 World.Health 보유(불변식)지만, 방어적으로 `World.Health == null`이면 경고 로그 + 안전 기본값(0). legacy fallback은 쓰지 않음(Slice 2에서 제거할 결합 재도입 회피). 클라 Health 슬라이스(`UserEntitySnapToC` 핸들러)와 동일 패턴.
- **writer/death/legacy 제거는 Slice 2** — 이번엔 *읽는 소스만* 바꾼다. legacy `HealthComponent`는 그대로 존재·생성·mutate.

## Scope (In) — 서버 변경 (2)

> World 타입은 풀 네임스페이스 한정(프로젝트 컨벤션).

| 파일 (서버) | 변경 |
|---|---|
| `Game/LOPGameEngine.cs` | `[Inject] GameFramework.World.EntityRegistry` 추가. `EndUpdate`의 `UserEntitySnapToC` HP 두 줄(`CurrentHP`/`MaxHP`)을 `entityRegistry.Get(entity.entityId)?.Get<GameFramework.World.Health>()`의 `Current`/`Max`로. null이면 `Debug.LogWarning` + 0. MP/Exp/Level/StatPoints 줄은 변경 없음. |
| `EntityCreationDataFactory/CharacterCreationDataCreator.cs` | `[Inject] GameFramework.World.EntityRegistry` 추가(1a로 DI 가능). `CharacterCreationData`의 `MaxHP`/`CurrentHP`를 World.Health에서. null이면 `Debug.LogWarning` + 0. MP/Level 등 다른 필드는 변경 없음. |

### 타이밍 안전 (확인됨)

- **UserEntitySnapToC**: `LOPGameEngine.UpdateEngine`이 `ProcessEvent`(applicator가 World.Health 갱신) → `EndUpdate`(스냅샷 빌드) 순. 따라서 스냅샷 시점에 World.Health는 그 틱 최신값 == legacy. 대상은 플레이어(캐릭터, World.Health 보유).
- **CharacterCreationDataCreator**: 스폰 경로는 `LOPEntityManager.CreateEntity`(→ `CharacterCreator.Create`가 World.Entity+Health 등록) 반환 *후* `entityCreationDataFactory.Create(entity)` 호출 → 읽기 시 World.Health 존재. 늦은 접속(`GetAllEntityCreationDatas`)도 기존 등록 엔티티라 존재. 대상은 캐릭터(`ItemCreationDataCreator`는 HP 미read).

## 데이터 흐름 (전환 후)

```
데미지 → LOPCombatSystem(legacy.TakeDamage; Slice2까지 유지) → DamageDealtEvent(remaining=legacy)
       → ProcessEvent: WorldEventApplicator.ApplyDamageDealt → World.Health.Current = remaining (== legacy)
       → EndUpdate: UserEntitySnapToC.CurrentHP/MaxHP ← World.Health   (read flip)
스폰/늦은접속 → CharacterCreationDataCreator: MaxHP/CurrentHP ← World.Health   (read flip)
```

읽는 값은 이전(legacy)과 **동일**(World.Health == legacy). 출처만 코어로.

## Out of scope (defer)

- **writer flip**(`LOPCombatSystem`→`HealthSystem.TakeDamage(worldHealth)`) + **사망 재배치**(legacy `TakeDamage`의 `EntityDeath` 발행 → combat으로, World.Health 기준) + **레거시 `HealthComponent` 제거** = **Slice 2**.
- MP/Exp/Level/Stats/Player 등 HP 외 필드의 World 이행 — 각자 별도.
- 클라 변경 0(이 두 reader는 서버 전용).

## 조사 근거 (구현 사실)

- 서버 HP **reader는 정확히 2곳**: `LOPGameEngine.cs`의 `UserEntitySnapToC`(EndUpdate) + `CharacterCreationDataCreator`. `LOPEntityManager.GetAllEntitySnaps`(EntitySnapsToC)는 position/rotation/velocity만 — HP 미포함. `LOPGame.cs`의 maxHP/currentHP는 스폰 *입력 리터럴*(읽기 아님).
- 서버 `World.Health`는 `CharacterCreator`가 등록(캐릭터만), `LOPGameEngine.ProcessEvent`의 `WorldEventApplicator.Apply`(`ApplyDamageDealt` → `Current = remaining`)가 매 틱 갱신. `LOPCombatSystem`이 `remaining = legacy.currentHP`로 이벤트를 만들어 World.Health가 legacy를 추종.
- `LOPGameEngine`은 `[DIMonoBehaviour]`(필드 주입) — `EntityRegistry` 주입 가능. `CharacterCreationDataCreator`는 1a로 DI 등록됨 — `[Inject]` 가능.
- `Health.Current`/`Health.Max`는 `int` 노출. `entityRegistry.Get(id)`는 `World.Entity`(없으면 null), `.Get<Health>()`는 Health(없으면 null).

## 검증

- **컴파일**: 서버 0에러 (UnityMCP `refresh_unity`+`read_console`, **서버 인스턴스** 핀 — 사용자 지시 서버 작업). GameFramework 불변 → 클라 영향 0.
- **런타임(수동)**: 서버 플레이 — (a) 유저 HUD HP 초기/피격 정상(스냅샷 경로 → 클라 표시), (b) 피격으로 HP 깎인 캐릭터에 **늦게 접속**한 클라가 올바른 현재 HP 수신(`GetAllEntityCreationDatas` 경로), (c) `Health not found` 경고가 정상 플레이 중 **안 떠야** 함(불변식), (d) 콘솔 에러 0. **값은 이전과 동일**(회귀 0)이 성공.
- 자동화 테스트 신규 없음(단일 Assembly-CSharp).

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/server-health-read-flip`, **코드는 서버 repo** 피처 브랜치. 서버 working-tree의 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)는 **stash 보존**, 커밋·`git restore` 금지. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 1a 완료 후 1b 설계 + null 가드(LogWarning+안전값) 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] `writing-plans`로 구현 plan
- [ ] 구현(서버 repo) → 검증 → 머지 → Slice 2

> 후속: Slice 2(writer flip + 사망 재배치 + 레거시 `HealthComponent` 제거) — 서버 Health 이행 마지막 칸.
