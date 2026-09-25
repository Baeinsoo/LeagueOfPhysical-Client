# 서버 Health 이행 — Slice 2: writer flip + 사망 발행 재배치 + 레거시 HealthComponent 제거

**Date:** 2026-06-18
**Branch (docs):** `feature/server-health-slice2` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [Slice 1a spec](2026-06-18-server-creationdata-di-design.md) · [Slice 1b spec](2026-06-18-server-health-read-flip-design.md) · [Health 대체 슬라이스(클라)](2026-06-18-world-core-health-replace-legacy-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md)

## Goal

서버 데미지 **writer**를 레거시 `HealthComponent.TakeDamage`(MonoBehaviour) → 코어 `GameFramework.World.Health` + `HealthSystem.TakeDamage`로 뒤집고, `HealthComponent.TakeDamage` 안에 묻혀 있던 **`EntityDeath` 발행을 `LOPCombatSystem`으로 재배치**한 뒤, 레거시 `HealthComponent`를 **삭제**한다.

**동작 보존**(behavior-preserving): HP 값·디스폰·경험치구슬 스폰 결과가 이전과 동일하다. 가치는 *HP 진실원본을 `World.Health`로 완전 일원화 + 레거시 store 제거*이며, 서버 Health 3슬라이스(1a→1b→2)의 **마지막 칸**이다.

## 위치 — 왜 이 슬라이스가 마지막인가

1b까지는 *읽기*를 `World.Health`로 옮겼지만(snapshot·생성데이터), **쓰기는 여전히 레거시**였다(`LOPCombatSystem`이 `legacy.TakeDamage`를 호출해 `legacy.currentHP`를 깎고, `World.Health`는 `WorldEventApplicator`가 `remaining`으로 추종). 이 슬라이스가 writer를 `World.Health`로 뒤집으면 레거시 `HealthComponent`를 읽거나 쓰는 곳이 0이 되어 컴포넌트를 제거할 수 있다.

핵심 제약: `HealthComponent.TakeDamage`는 HP가 0 이하가 될 때 **`EntityDeath`(victimId/killerId/**position**)를 발행하는 유일한 곳**이고, 이 이벤트가 `LOPGame.HandleEntityDeath`를 통해 **디스폰 + 경험치구슬 스폰을 구동**한다. 컴포넌트를 지우려면 이 발행을 잃지 않게 먼저 재배치해야 한다.

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 |
|---|---|---|
| ① | writer 대상 | `LOPCombatSystem`이 `World.Health`를 직접 깎는다 — `entityRegistry.Get(target).Get<Health>()` + `healthSystem.TakeDamage(health, dealtAmount)`. `remaining = health.Current`, `isDead = health.IsDead`. (Generation이 mutate + Application[`ApplyDamageDealt`]이 `Current = remaining`으로 재적용 = 멱등. server-authoritative의 의도된 중복.) |
| ② | 사망 발행 재배치 | **옵션 A** — 레거시 `EntityDeath` 발행을 `HealthComponent.TakeDamage`에서 `LOPCombatSystem`의 `isDead` 감지 지점으로 옮긴다. position은 살아있는 `target.position`에서 읽는다. **디스폰 경로(`LOPGame.HandleEntityDeath`)·`EntityDeath` 구조체·`World.DeathEvent`는 무변경.** |
| ③ | 범위 | **HP만.** MP/Exp/Level 등 다른 레거시 컴포넌트는 유지(각자 별도). |
| ④ | 디스폰 채널 통일(`World.DeathEvent` 구동) | **후속 Slice 3로 분리** — 이번엔 안 함(아래 Out of scope). load-bearing 디스폰 경로 재설계는 단일관심사 슬라이스로 따로. |

### 왜 옵션 A인가 (옵션 B 기각 — Slice 3로)

옵션 B(디스폰을 정식 `World.DeathEvent` 소비로 재배선)는 최종 아키텍처 방향이지만, (1) `World.DeathEvent`에 `position`이 없어 GameFramework 공유 패키지 수정(→ 클라 영향) 또는 소비 시점 역조회가 필요하고, (2) `WorldEventBuffer` 드레인에서 디스폰을 트리거하는 새 fan-out 경로 신설 + `LOPGame`의 `SpawnExpMarble`/`DeleteEntityById` 재배치가 따라온다. 이는 **load-bearing 디스폰 경로**를 건드리는 일이라, "HP writer flip + 레거시 제거"라는 이 슬라이스의 관심사와 묶으면 디스폰이 깨졌을 때 원인 분리가 안 된다. 1a/1b의 "최소·동작보존·단일관심사" 결을 지켜 **A로 끝내고, B는 behavior-preserving한 전용 후속 슬라이스(Slice 3)** 로 둔다.

## Scope (In) — 서버 변경 (3파일 + 1삭제)

> World 타입은 풀 네임스페이스 한정(프로젝트 컨벤션). 변경은 모두 서버 로컬 + GameFramework 무변경.

### 1. `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` — writer flip + 사망 재배치

생성자에 `EntityRegistry`, `HealthSystem` 주입 추가(현재 `WorldEventBuffer`만). `Attack(attacker, target)` 변경:

| 현재 (레거시) | 변경 후 (World.Health) |
|---|---|
| `target.TryGetEntityComponent<HealthComponent>(out var hc)` 존재 가드 | `var health = entityRegistry.Get(target.entityId)?.Get<GameFramework.World.Health>();` `if (health == null) return;` |
| 이미사망 가드 `hc.currentHP <= 0` | `if (health.IsDead) return;` |
| `if (!isDodged) hc.TakeDamage(attacker.entityId, damage);` | `if (!isDodged) healthSystem.TakeDamage(health, dealtAmount);` |
| `isDead = hc.currentHP <= 0;` | `isDead = health.IsDead;` |
| `DamageDealtEvent(... remaining: hc.currentHP, isDead: isDead ...)` | `DamageDealtEvent(... remaining: health.Current, isDead: isDead ...)` |
| `DeathEvent(victimId, attackerId)` append | **유지** (정식 채널, 여전히 inert) |
| *(없음 — 발행은 `HealthComponent.TakeDamage` 안에 있었음)* | `if (isDead)` 블록에 **레거시 발행 추가**: `EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(target.entityId, attacker.entityId, target.position));` |

> `dealtAmount = isDodged ? 0 : damage`(기존 라인 유지)를 `TakeDamage` 인자로 사용. dodge면 `TakeDamage` 호출 안 함(데미지 0) → `isDead` false → 사망 발행 없음(기존 동작 동일).

### 2. `Assets/Scripts/EntityCreator/CharacterCreator.cs` — 레거시 생성 제거

`AddEntityComponent<HealthComponent>()` + `Initialize(maxHP, currentHP)` 3줄 제거. `World.Health` 등록(기존 84-94줄)이 진실원본으로 그대로 남는다.

### 3. `Assets/Scripts/Game/GameLifetimeScope.cs` — DI 확인

`EntityRegistry`/`HealthSystem`은 이미 Singleton 등록됨(Slice 3). `LOPCombatSystem` 생성자 인자 추가 시 VContainer가 자동 주입 → **등록 추가 불필요**(확인만).

### 4. 삭제 — `Assets/Scripts/Component/HealthComponent.cs` (+ `.meta`)

위 변경 후 참조 0. `git rm`으로 `.cs`+`.meta` 함께 제거.

## 데이터 흐름 (전환 후)

```
데미지 → LOPCombatSystem.Attack(attacker, target)
   health = registry.Get(target).Get<World.Health>()      (null/IsDead 가드)
   healthSystem.TakeDamage(health, dealtAmount)            ← writer flip (World.Health 직접 mutate)
   remaining = health.Current, isDead = health.IsDead
   worldEventBuffer.Append(DamageDealtEvent(remaining, isDead))
   if (isDead):
       worldEventBuffer.Append(DeathEvent(victimId, attackerId))           (정식 채널, inert)
       EventBus.Publish(EntityDeath(victimId, attackerId, target.position)) ← 재배치된 디스폰 신호
                                                                              │
   ProcessEvent(틱): WorldEventApplicator.ApplyDamageDealt → Current=remaining (멱등)
                     WireBroadcaster.Broadcast → DamageEventToC(네트워크) + DeathEvent(log만)
                                                                              │
   LOPGame.HandleEntityDeath (구독, 무변경) ───────────────────────────────────┘
       DespawnEntity(victimId) → entityManager.DeleteEntityById (지연, EndUpdate에서 Destroy)
       SpawnExpMarble(position)
```

디스폰을 구동하는 신호(`EntityDeath`)·소비자(`LOPGame`)·payload(victimId+position)는 이전과 **동일**. 발행 위치만 `HealthComponent` → `LOPCombatSystem`으로 이동. `World.Health`가 HP의 단일 store가 됨.

## Out of scope (defer)

- **디스폰을 `World.DeathEvent`로 통일(옵션 B)** = **후속 Slice 3**. 레거시 `EntityDeath` 제거 + `LOPGame` 디스폰을 `World.DeathEvent` 소비로 재배선 + position 갭 해소(DeathEvent 확장 vs 역조회). behavior-preserving 전용 슬라이스.
- position/rotation/velocity 진실원본 승격, 예측/롤백/commit-gate/Snapshot-Restore = **Stage ④**.
- MP/Exp/Level/Stats 등 HP 외 레거시 컴포넌트 이행 — 각자 별도.
- 클라 변경 0(이 writer·발행은 서버 전용).

## 조사 근거 (구현 사실)

- 1a/1b 후 서버 `HealthComponent` 참조는 **정확히 3곳**(+정의): `CharacterCreator`(생성), `LOPCombatSystem`(존재 가드 + 이미사망 가드 + `TakeDamage` write + `remaining`/`isDead` 산출). 1b가 뒤집은 두 reader(`LOPGameEngine`의 `UserEntitySnapToC`, `CharacterCreationDataCreator`)는 더 이상 `HealthComponent`를 안 만짐(확인).
- `HealthComponent.TakeDamage`(서버)는 `currentHP -= damage` 후 `<= 0`이면 `EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(entity.entityId, attackerId, entity.position))` — **`EntityDeath`의 유일 발행처**.
- `EntityDeath` 구조체(`Assets/Scripts/Entity/Event.Entity.cs`): `victimId`, `killerId`, `position`. 구독자 = `LOPGame`(유일): `HandleEntityDeath` → `DespawnEntity(victimId)` + `SpawnExpMarble(position)`. `killerId`는 현재 소비 안 함.
- `LOPCombatSystem`은 `ICombatSystem` Singleton(`GameLifetimeScope`), 생성자 주입(`WorldEventBuffer`). 이미 `isDead`를 감지해 `DamageDealtEvent`/`DeathEvent`를 append 중 — 사망 발행을 여기 더하는 게 최소 변경.
- `HealthSystem.TakeDamage(Health, int)`(GameFramework): `Current = Math.Max(0, Current - amount)`. attackerId·death 이벤트·max 클램프 없음(순수). → attacker 링크는 `EntityDeath`/`DeathEvent` payload로 전달.
- `Health`: `Max`/`Current`(int), `IsAlive => Current > 0`, `IsDead => !IsAlive`.
- `WorldEventApplicator.ApplyDamageDealt`는 `Current = e.remaining` 재적용(멱등 — `remaining == health.Current`).
- 디스폰: `DeleteEntityById`는 지연 마크, `DestroyMarkedEntities`(EndUpdate)가 실제 파괴 + `entityRegistry.Remove`(이미 World 정리됨) + `EntityDespawnToC` 송신. → 어느 채널이 트리거하든 World 엔티티 정리는 보장됨.
- `HealthComponent`는 `CharacterCreator`가 코드로 `AddEntityComponent`하는 컴포넌트 — 씬/prefab의 SerializedField가 GUID로 직접 참조하지 않음 → 삭제 시 참조 끊김 0.

## 검증

- **컴파일**: 서버 0에러 (UnityMCP `refresh_unity` + `read_console`, **서버 인스턴스** `LeagueOfPhysical-Server@<hash>` 핀 — 사용자 지시 서버 작업). GameFramework 불변(추가/수정 0) → 클라 영향 0.
- **런타임(수동, 서버 플레이)**:
  1. 피격 시 HP 정상 감소(`World.Health` writer) + 클라 HUD/네임플레이트 HP바 갱신.
  2. **치명타 사망**: 엔티티 디스폰(1회, 더블-데스 없음) + 경험치구슬이 **사망 위치**에 스폰.
  3. 이미 죽은 대상에 추가 데미지 → 두 번째 `EntityDeath` 안 뜸(이미사망 가드).
  4. `[World] Death entity ...` broadcaster 로그 정상(slice 3 회귀).
  5. 콘솔 에러 0. `HealthComponent` 완전 제거(grep 0).
- **자동화 테스트 신규 없음**(단일 Assembly-CSharp). `HealthSystem.TakeDamage`는 GameFramework EditMode에 기존 커버.

## GUID / .meta 정책

- **삭제**: `HealthComponent.cs`+`.meta` `git rm` 동반.
- **수정 3파일**: 파일·클래스명 유지 → `.meta` 영향 없음.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo**(설계 허브) 피처 브랜치 `feature/server-health-slice2`, **코드는 서버 repo** 피처 브랜치. 옵션 A는 `LOPGame.cs`를 **건드리지 않으므로** 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`)와 충돌 없음 — 그래도 stash 보존·커밋 금지 원칙 유지. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 1a/1b 완료 후 Slice 2 설계 + 옵션 A(combat 재배치) vs B(World.DeathEvent 구동) 검토 → A 채택, B는 Slice 3로 분리
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] 구현(서버 repo) → 검증 → 머지

> 후속: Slice 3(디스폰을 `World.DeathEvent`로 통일 — 옵션 B, behavior-preserving) → 그 다음 Stage ④(진실원본 승격·물리·netcode).
