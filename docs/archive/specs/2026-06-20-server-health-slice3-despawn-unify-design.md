# 서버 Health Slice 3 — 디스폰을 World.DeathEvent로 통일 (옵션 B), 레거시 EntityDeath 제거

**Date:** 2026-06-20
**Branch (docs):** `feature/server-health-slice3` (클라 repo — 설계 허브)
**Branch (code):** 서버 repo 피처 브랜치 (구현 시 생성)
**Related:** [서버 Health Slice 2](2026-06-18-server-health-slice2-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Owner 이행](2026-06-19-world-core-owner-migration-design.md)

## Goal

서버 사망→디스폰 경로를 **World 코어 `DeathEvent`로 일원화**한다. 현재(Health Slice 2 = 옵션 A) 사망 시 `LOPCombatSystem`이 (a) `World.DeathEvent`를 버퍼에 append(현재 inert) + (b) 레거시 `EntityDeath`를 EventBus 발행(→ `LOPGame`이 디스폰 구동) **둘 다** 낸다. Slice 3 = **`World.DeathEvent`가 디스폰을 구동**하게 하고 **레거시 `EntityDeath`를 제거**한다.

동작 보존(behavior-preserving): 몬스터 사망 시 디스폰 + 경험치 구슬 스폰이 이전과 동일. 가치 = load-bearing 디스폰 경로를 World 이벤트 모델로 정리, 죽은 코드(inert DeathEvent) 활성화.

## 현재 구조 (조사 확인)

- **`LOPCombatSystem.Attack` 사망 블록**: `worldEventBuffer.Append(new World.DeathEvent(victimId, attackerId))`(position 없음) + `EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(victimId, killerId, target.position))`.
- **레거시 `EntityDeath`**(server `Event.Entity.cs`): `string victimId; string killerId; Vector3 position;`. **유일 소비자 = `LOPGame.HandleEntityDeath`**(구독 `LOPGame.cs`, Register/Unregister) → `DespawnEntity(victimId)`(마크) + `SpawnExpMarble(position)`.
- **`World.DeathEvent`**(GameFramework): `record DeathEvent(string victimId, string attackerId)` — **position 없음**.
- **드레인 = `LOPGameEngine.ProcessEvent`**: `worldEventApplicator.Apply(snapshot)` + `wireBroadcaster.Broadcast(snapshot)` + `buffer.Clear()`. Applicator의 `DeathEvent` = no-op, WireBroadcaster의 `DeathEvent` = Debug.Log only. **서버엔 WorldEvent→EventBus fan-out 없음**(클라엔 `WorldEventBridge` 있음).
- **틱 순서**(`UpdateEngine`): … `SimulatePhysics`(WorldMotionSync가 World.Transform에 position mirror) → `ProcessEvent`(드레인) → `EndUpdate`(스냅샷 + `DestroyMarkedEntities`=실제 파괴·`EntityRegistry.Remove`·`EntityDespawnToC` 송신). **디스폰은 지연**(DeleteEntityById=마크, 파괴는 EndUpdate).
- → **드레인 시점에 죽은 엔티티가 아직 entityManager/registry에 살아있음**(position 읽기 가능). 클라는 `DeathEvent`를 로그만(WorldEventBridge), 디스폰은 `EntityDespawnToC` wire로 구동(독립).

## 잠긴 결정 (브레인스토밍 합의)

| # | 축 | 결정 | 근거 |
|---|---|---|---|
| ① | position | **드레인 시점 조회**(victimId로 entityManager에서 position) — `DeathEvent`에 position 추가 안 함 | 디스폰 지연이라 드레인 시 엔티티 살아있음. GameFramework 공유 record/테스트/wire 변경 0. 클라는 position 불필요 |
| ② | fan-out 배선 | **서버 `WorldEventBridge` 신설**(클라 동형, EventBus fan-out). `ProcessEvent`에서 `Applicator + WireBroadcaster` 옆에 `Bridge.FanOut` | 양쪽 드레인 모양 동일(생성·적용·fan-out 같은 배선) → 로컬 서버/공유 시뮬 정합. WireBroadcaster(wire)와 별개 sink(내부 EventBus). 별도 applier 불필요 |
| ③ | 디스폰 소유 | `LOPGame`이 계속 소유 — 구독을 `EntityDeath` → `World.DeathEvent`로 전환 | 디스폰+marble은 게임 반응(cascade), application 아님. LOPGame이 entityManager/sessionManager 보유 |
| ④ | 범위 | **서버 전용, 단일 슬라이스.** GameFramework/클라/wire 무변경 | DeathEvent 불변, 클라 디스폰은 wire 독립 |
| ⑤ | 상주/이벤트 | DeathEvent는 이산 사건(WorldEventBuffer) — 기존 모델 그대로 | 연결 아키텍처 정합 |

> **아키텍처 명확화(durable):** 드레인 소비자 3역할 — **Application**(`WorldEventApplicator`, 상태 쓰기, 양쪽 shared) / **EventBus fan-out**(`WorldEventBridge`, 내부 반응, 양쪽 동형·핸들러만 사이드별) / **Wire 송신**(`WireBroadcaster`, 서버 전용). 클라 `WorldEventBridge` ↔ **서버 `WorldEventBridge`**가 진짜 대응(둘 다 EventBus fan-out); WireBroadcaster는 서버만 갖는 추가 sink. 디스폰은 *반응*이라 fan-out(Bridge)→EventBus→`LOPGame` 경로(Applicator 아님).

## Scope (In) — 서버 전용

### 신규 `Assets/Scripts/World/WorldEventBridge.cs` (서버, LOP — 클라 동형)
```csharp
namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 드레인 이벤트를 서버 내부 EventBus로 fan-out한다(게임 반응용).
    /// 클라 WorldEventBridge의 서버 대응 — wire 송신(WireBroadcaster)과 별개 sink.
    /// </summary>
    public class WorldEventBridge
    {
        public void FanOut(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DeathEvent death:
                        EventBus.Default.Publish(EventTopic.Entity, death);
                        break;
                }
            }
        }
    }
}
```
(초기엔 DeathEvent만 fan-out. 향후 cascade[loot 등]는 여기 case 추가.)

### `Assets/Scripts/Game/LOPGameEngine.cs` — ProcessEvent에 FanOut 추가
드레인에 `[Inject] WorldEventBridge worldEventBridge;` 주입, `wireBroadcaster.Broadcast(snapshot);` 다음에 `worldEventBridge.FanOut(snapshot);` 추가(Clear 전).

### `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` — 레거시 발행 제거
사망 블록의 `EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(...))` 2줄 제거. `worldEventBuffer.Append(new World.DeathEvent(...))`는 유지. (DamageDealtEvent/DeathEvent append 흐름 그대로.)

### `Assets/Scripts/Game/LOPGame.cs` — 구독 전환 + position 조회 (⚠️픽스처)
- 구독/해제: `EventBus.Default.Subscribe<Event.Entity.EntityDeath>(EventTopic.Entity, HandleEntityDeath)` → `Subscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath)` (Unregister도).
- 핸들러:
  ```csharp
  private void HandleDeath(GameFramework.World.DeathEvent deathEvent)
  {
      LOPEntity victim = gameEngine.entityManager.GetEntity<LOPEntity>(deathEvent.victimId);
      if (victim == null)
      {
          Debug.LogWarning($"[World] HandleDeath: victim {deathEvent.victimId} not found");
          return;
      }
      Vector3 position = victim.position;   // 드레인 시점 — 엔티티 아직 살아있음(디스폰은 EndUpdate 지연)

      DespawnEntity(deathEvent.victimId);
      SpawnExpMarble(position);
  }
  ```
  (`DespawnEntity`/`SpawnExpMarble` 본문 무변경. position 읽기를 DespawnEntity 전에 — 마크일 뿐이라 순서 무관하나 명확성 위해 먼저.)

### `Assets/Scripts/Entity/Event.Entity.cs` — 레거시 `EntityDeath` struct 삭제
LOPGame 전환 후 소비자 0 → struct 제거.

### `Assets/Scripts/Game/GameLifetimeScope.cs` — DI 등록
`builder.Register<WorldEventBridge>(Lifetime.Singleton);` 추가.

## 데이터 흐름 (전환 후)

```
combat 사망 → worldEventBuffer.Append(World.DeathEvent(victimId, attackerId))   [legacy EntityDeath 발행 제거]
ProcessEvent(드레인):
   worldEventApplicator.Apply(snapshot)        (DeathEvent no-op — 변화 없음)
   wireBroadcaster.Broadcast(snapshot)         (DeathEvent log only — 변화 없음)
   worldEventBridge.FanOut(snapshot)           ← 신규: DeathEvent → EventBus.Publish(EventTopic.Entity, deathEvent)
      → LOPGame.HandleDeath(deathEvent):
           position = entityManager.GetEntity(victimId)?.position
           DespawnEntity(victimId)   [마크]
           SpawnExpMarble(position)
   buffer.Clear()
EndUpdate → DestroyMarkedEntities (실제 파괴 + EntityRegistry.Remove + EntityDespawnToC 송신)  [기존 동일]
```

`World.DeathEvent`가 디스폰 단일 구동원. 클라 디스폰은 `EntityDespawnToC`로 그대로(독립).

## 동작 보존 메모

- 디스폰+구슬은 같은 틱에 발생(이전: combat 페이즈 / 이제: 드레인 페이즈 — 둘 다 EndUpdate 전, 같은 틱 DestroyMarkedEntities로 수렴).
- position: 이전 combat 시점 `target.position` → 이제 드레인 시점 조회. 물리 한 스텝 차이 무시 가능(오히려 post-physics).
- 마블 스폰(CreateEntity + EntitySpawnToC)이 드레인 페이즈로 이동 — 같은 틱, 클라 수신 동일.

## 검증

- **컴파일**: 서버 0에러(UnityMCP `refresh_unity`+`read_console`, 서버 인스턴스 핀 — 사용자 지시 서버 작업). GameFramework/클라 무변경.
- **런타임(수동, 서버 플레이)**:
  1. 몬스터 치명타 사망 → **디스폰(1회, 더블 없음) + 경험치 구슬이 사망 위치에 스폰**.
  2. 클라가 디스폰/구슬 정상 수신(EntityDespawnToC/EntitySpawnToC).
  3. `[World] Death entity ...`(WireBroadcaster 로그) 정상, `HandleDeath: victim not found` 경고 정상 플레이 중 안 뜸.
  4. 콘솔 에러 0. 데미지→사망→디스폰 전 경로 정상.
- 자동화 테스트 신규 없음(단일 Assembly-CSharp). 코어 무변경.

## GUID / .meta 정책

- **삭제**: 없음(레거시 `EntityDeath`는 `Event.Entity.cs` 내 struct — 파일 삭제 아님, struct만 제거). 신규 `WorldEventBridge.cs`는 Unity 생성 `.meta` 동반 커밋.

## 픽스처 정책

`LOPGame.cs`(구독+HandleDeath) 수정 → 서버 working-tree 로컬 픽스처(`LOPGame.cs`/`ConfigureRoomComponent.cs`) **stash 댄스**(선례 동일). 사망 블록은 `HandleEntityDeath`(≈L167)·Register(≈L71)이라 픽스처(SpawnEnemies)와 비겹침 — plan에서 확인. 커밋 금지·stash 보존.

## Out of scope (defer)

- **`DeathEvent`에 position 추가**(공유 계약/wire 변경) — 안 함(조회로 충분).
- **디스폰 결정의 공유 시뮬 이관**(클·서 동일 실행) = Slice 4 / Stage④.
- **추가 cascade**(DeathEvent → LootDropEvent 등) — 미래, 서버 `WorldEventBridge`에 case 추가로 확장.
- position 진실원본 승격/예측·롤백 = Stage④.

## 문서/브랜치 정책

선례대로 **spec·plan은 클라 repo** 피처 브랜치 `feature/server-health-slice3`, **코드는 서버 repo** 피처 브랜치. 서버 working-tree 로컬 픽스처 커밋 금지·stash 보존. 이 spec은 `CLAUDE.md` `@` 자동 로드 목록에 추가.

## 진행

- [x] 드레인 소비자 3역할 정리 + 옵션 B(서버 WorldEventBridge) + position 조회 합의
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] 구현(서버 repo) → 검증 → 머지

> 이 슬라이스로 서버 Health 이행의 *선택적 후속*까지 마무리. 디스폰이 World 이벤트 모델로 일원화됨. 남은 건 Stage④급(공유 시뮬/예측/통합 fan-out).
