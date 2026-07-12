# A2.2b — 히트 판정(OverlapSphere+부채꼴)을 LOP-Shared 공유화

**Date:** 2026-07-12
**Branch (제안):** `feature/a2-2b-hit-detection-shared` (GameFramework + LOP-Shared + Server)
**Related:** [ROADMAP](../../ROADMAP.md) (Next #1 A, A2.2b 접근 확정) · A2.2a spec `2026-07-12-a2-2a-combat-resolution-shared-design` · [lop-repo-topology](../../lop-repo-topology.md) (시뮬=구체 공유 / I/O=인터페이스) · [world-core-connection-architecture](../../world-core-connection-architecture.md) (`ICollisionQuery` 짝 포트)

## Goal

서버 전용이던 전투 **히트 판정**(어택 범위 안의 대상 찾기 = `Physics.OverlapSphere` + 부채꼴 필터)을 **LOP-Shared로 옮겨 클·서 공유**한다. 엔진 물리(PhysX)에 직결된 부분만 **사이드별 포트**(`IOverlapQuery`)로 빼고, 판정 드라이버(부채꼴·자기제외·Attack 루프)는 공유 concrete로 만든다. A2.2a(해소 공유)에 이어 **판정까지 공유**되면, A2.4에서 클라가 판정을 재구현하지 않고 같은 코드로 예측 히트를 낼 수 있다.

이 슬라이스는 **공유화 + 서버 재배선까지** — 클라 등록/예측 소비는 A2.4.

## 배경 / 동기

### A2.2 = 공유화, 이 슬라이스 = 판정(b)

A2(클라 예측 전투)에서 클라가 히트를 로컬 예측하려면 **서버와 동일한 판정+해소 코드**를 돌려야 한다. A2.2는 이를 위한 공유화이고 두 조각:
- **A2.2a (완료)** — 전투 **해소**(`LOPCombatSystem`, 계산+RNG+이벤트)를 LOP-Shared 공유 concrete로.
- **A2.2b (이 슬라이스)** — 히트 **판정**(범위 검색+부채꼴)을 공유. 엔진 물리 포트가 필요한 마지막 조각.

### 현재 상태 (실측, post-A2.2a)

서버 `DamageEffectHandler.OnActiveEnter`(`LeagueOfPhysical-Server/Assets/Scripts/Game/DamageEffectHandler.cs`):

```
① attacker = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id)   // 사이드 타입 룩업 (position/collider 용)
② Physics.OverlapSphere(attacker.position, effect.Range, "Default")  → Collider[]   // 엔진 물리
③ foreach collider:
     - name=="Plane" skip
     - IsInAttackSector(attacker, hit.transform.position, Range, Angle) 아니면 skip   // Unity Vector3/Quaternion 부채꼴
     - collider → hit.transform.parent.parent.GetComponentInChildren<LOPEntity>() → target   // 사이드 매핑
     - target==null || target.entityId==attacker.entityId skip                       // 자기제외
     - targetWorld = entityRegistry.Get(target.entityId)                             // World.Entity 변환
     - combatSystem.Attack(ctx.Caster, targetWorld, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value)   // 해소(공유, A2.2a)
```

**공유 blocker = 정확히 두 곳:**
1. **②③의 엔진 물리 + collider→entityId 매핑** — `Physics.OverlapSphere`와 `LOPEntity`(사이드 타입) 의존.
2. **matchSeed** — `MatchSeed`가 사이드별 concrete(서버=Guid 생성, 클라=수신 보관).

**이미 공유 가능한 것:** attacker 위치·회전은 `ctx.Caster`(World.Entity)의 `World.Transform`(진실원본, numerics)에서 읽을 수 있고 — 현재 `attacker.position`도 사실 `worldTransform.Position.ToUnity()`라 **같은 값** — 부채꼴 수학은 numerics로 재작성 가능. `IsInAttackSector`의 target 위치만 collider transform 대신 target의 `World.Transform.Position`을 쓰면 된다(더 정확·엔진 프리). 해소(`Attack`)는 A2.2a에서 이미 공유.

## 설계

### 1. `IOverlapQuery` — 엔진 오버랩 포트 (GameFramework)

`ICollisionQuery`(캡슐 sweep)의 **짝**. 구 안의 엔티티 id들을 반환한다.

```csharp
namespace GameFramework
{
    /// <summary>범위(구) 안에 겹치는 엔티티들의 id를 반환하는 오버랩 쿼리 포트.
    /// ICollisionQuery(캡슐 sweep)의 짝. 구체는 사이드별(LOPOverlapQuery)로,
    /// 엔진 물리(Physics.OverlapSphere) + collider→엔티티 매핑을 담당한다.</summary>
    public interface IOverlapQuery
    {
        string[] OverlapSphere(System.Numerics.Vector3 center, float radius);
    }
}
```

- **반환 = entityId(string[])** — `World.Entity.Id`와 같은 문자열. 포트는 "엔진 → id"까지만(최소). World.Entity 변환은 공유 드라이버가 `EntityRegistry.Get`으로.
- **위치 = GameFramework** — `ICollisionQuery`와 같은 자리(엔진 물리 쿼리 추상). GameFramework는 이미 `EntityRegistry`/string Id 어휘를 가지므로 "겹치는 엔티티 id" 반환이 자연스럽다.
- 왜 매핑이 포트 안이냐: collider→`LOPEntity`→entityId 매핑은 Unity·사이드 특화라 공유 불가 → 포트 구체가 흡수.

### 2. `LOPOverlapQuery` — 사이드별 구체 (Server, 클라는 A2.4)

`UnityCollisionQuery`가 GameFramework에 사는 것과 달리, 이 구체는 `LOPEntity`(사이드 타입)를 알아야 하므로 **각 레포에 존재**(의도적 사이드 분기 — `LOPEntity`/`LOPRunner`와 같은 케이스).

```csharp
// LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs (클라는 A2.4에서 동형 추가)
public sealed class LOPOverlapQuery : GameFramework.IOverlapQuery
{
    public string[] OverlapSphere(System.Numerics.Vector3 center, float radius)
    {
        LayerMask mask = LayerMask.GetMask("Default");
        Collider[] hits = Physics.OverlapSphere(center.ToUnity(), radius, mask);
        var ids = new HashSet<string>();               // 한 엔티티 다중 콜라이더 → 중복 제거
        foreach (var hit in hits)
        {
            var entity = hit.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
            if (entity != null) ids.Add(entity.entityId);
        }
        return System.Linq.Enumerable.ToArray(ids);
    }
}
```

- **"Plane" skip 자연 소멸** — Plane은 `LOPEntity`가 없으니 매핑에서 빠져 반환 안 됨. 명시적 name 체크 불필요.
- **중복 제거(HashSet)** — 한 엔티티에 콜라이더가 여러 개면 기존 코드는 대상당 `Attack`을 중복 호출(이중 데미지 = 버그). 키 기반 RNG는 (attacker,target,tick,effectIndex)마다 독립이라 **순서·중복 무관** → dedup이 안전하고 더 정확. (콜라이더 매핑을 `parent.parent` 관례 그대로 이식 — 프리팹 구조 전제 불변.)

### 3. `IMatchSeed` — 씨앗 공유 인터페이스 (LOP-Shared)

양쪽 `MatchSeed`가 구현하는 얇은 인터페이스. 공유 핸들러가 사이드 무지로 씨앗을 읽는다.

```csharp
namespace LOP
{
    public interface IMatchSeed { ulong Value { get; } }
}
```

- **위치 = LOP-Shared** — "매치 씨앗"은 LOP 도메인 개념(GameFramework의 엔진-비종속 어휘 아님). 공유 핸들러가 참조하므로 shared에 둔다.
- 서버 `MatchSeed`(get-only) / 클라 `MatchSeed`(Set 보관) 둘 다 `Value { get; }`를 이미 노출 → `: IMatchSeed`만 추가(동작 무변경). 클라도 지금 붙인다(대칭·무비용).

### 4. `DamageEffectHandler` → LOP-Shared 공유 concrete

파일을 서버 `Assets/Scripts/Game/DamageEffectHandler.cs` → LOP-Shared `Runtime/Scripts/Game/DamageEffectHandler.cs`(`namespace LOP` 유지). **서버 사본 삭제.**

```csharp
public class DamageEffectHandler : AbilityEffectHandler<DamageEffect>
{
    private readonly LOPCombatSystem combatSystem;          // 공유 concrete (A2.2a)
    private readonly GameFramework.IOverlapQuery overlapQuery;
    private readonly IMatchSeed matchSeed;
    private readonly GameFramework.World.EntityRegistry entityRegistry;

    // ctor 주입 …

    protected override void OnActiveEnter(AbilityEffectContext ctx, DamageEffect effect)
    {
        var casterTransform = ctx.Caster.Get<GameFramework.World.Transform>();
        if (casterTransform == null) return;

        string[] hitIds = overlapQuery.OverlapSphere(casterTransform.Position, effect.Range);
        foreach (var id in hitIds)
        {
            if (id == ctx.Caster.Id) continue;                       // 자기제외
            var target = entityRegistry.Get(id);
            if (target == null) continue;
            var targetTransform = target.Get<GameFramework.World.Transform>();
            if (targetTransform == null) continue;
            if (!IsInAttackSector(casterTransform, targetTransform.Position, effect.Range, effect.Angle)) continue;
            combatSystem.Attack(ctx.Caster, target, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value);
        }
    }

    // 부채꼴 판정 — System.Numerics(World.Transform 진실원본). Unity Vector3/Quaternion 대신.
    private static bool IsInAttackSector(GameFramework.World.Transform caster,
                                         System.Numerics.Vector3 targetPos, float range, float angle)
    {
        System.Numerics.Vector3 toTarget = targetPos - caster.Position;
        if (toTarget.Length() > range) return false;
        System.Numerics.Vector3 forward =
            System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, caster.Rotation);
        float dot = System.Numerics.Vector3.Dot(
            System.Numerics.Vector3.Normalize(forward), System.Numerics.Vector3.Normalize(toTarget));
        float targetAngle = (float)System.Math.Acos(System.Math.Clamp(dot, -1.0, 1.0)) * (180f / (float)System.Math.PI);
        return targetAngle <= (angle * 0.5f);
    }
}
```

- **attacker 위치·회전 = `ctx.Caster`의 `World.Transform`** — `ctx.EntityManager`/`LOPEntity` 룩업 **제거**(핸들러가 사이드 타입 무지). 이게 판정 공유의 핵심.
- **forward = `Vector3.Transform(UnitZ, Rotation)`** — 레거시 `Quaternion.Euler(rotation)*Vector3.forward`의 numerics 등가. `World.Transform.Rotation`(진실원본)에서 직접.
- **target 위치 = target의 `World.Transform.Position`** — 레거시는 collider transform(`hit.transform.position`)을 썼다. 엔티티 원점(진실원본)으로 바뀌어 collider offset만큼 기준점이 이동(부채꼴엔 무시 가능·더 정확 — 아래 Open 참고).
- `Acos` 인자를 `[-1,1]`로 Clamp — 정규화 오차로 인한 NaN 방지(레거시엔 없던 방어, 무해).

### 5. 서버 DI 등록

서버 `GameLifetimeScope`:
- `Register<GameFramework.IOverlapQuery, LOPOverlapQuery>()` (신규).
- `Register<IMatchSeed, MatchSeed>()` (기존 `MatchSeed` 등록을 인터페이스로 노출; 그대로 concrete 등록 + `As<IMatchSeed>()` 유지도 가능 — plan에서 기존 등록 형태에 맞춤).
- `DamageEffectHandler`는 이제 shared에서 오지만 등록은 use-side. 주입 4종(`LOPCombatSystem`, `IOverlapQuery`, `IMatchSeed`, `EntityRegistry`)이 다 서버 스코프에 있음.
- **클라: 등록/사용 안 함**(A2.2a와 동일 — 공유 코드를 컴파일만). `LOPOverlapQuery` 클라 구체 + 등록 + 예측 소비는 **A2.4**.

## 테스트 (LOP-Shared EditMode — 신규 커버리지)

판정 드라이버가 **처음으로 단위테스트 가능**해진다(서버 Assembly-CSharp였고 엔진 물리라 불가였음). 가짜 `IOverlapQuery`(미리 지정한 id들 반환) + 가짜 `IMatchSeed`로 판정 로직만 격리 테스트. `Tests/EditMode/DamageEffectHandlerTests.cs`:

- **자기제외:** 오버랩 반환에 caster.Id 포함 → caster는 `Attack` 대상 아님.
- **부채꼴 각:** 정면(forward) 내 대상 → 히트 / 정반대(뒤) 대상 → 미히트 / 각 경계 바로 밖 → 미히트.
- **범위:** range 밖(오버랩엔 있으나 narrow-phase에서 걸러짐) → 미히트. (오버랩 포트가 broad-phase, 부채꼴이 narrow-phase 이중 확인.)
- **World.Transform 없는 id / EntityRegistry에 없는 id** → 스킵(크래시 없음).
- **끝-끝:** 정면·범위 내 대상 → `LOPCombatSystem.Attack`이 실제로 불려 `Health.Current` 감소 + `WorldEventBuffer`에 `DamageDealtEvent` append(A2.2a 테스트와 조립 재사용).
- **회전 반영:** caster의 `World.Transform.Rotation`을 돌리면 같은 대상이 히트↔미히트로 바뀜(forward 계산 검증).

> 오버랩 broad-phase(`Physics.OverlapSphere` + collider 매핑)는 엔진이라 가짜 포트 뒤에 두고, **판정 로직(부채꼴·자기제외·range narrow)만** 단위테스트. 엔진 물리 자체 회귀는 서버 실행 검증(공격→명중/데미지 정상).

## 산업 표준 매핑

- **시뮬 로직 = 구체 공유 / I/O = 인터페이스** — `lop-repo-topology`·`world-core-connection-architecture` "시뮬 코드 형태" 컨벤션. 판정 드라이버(부채꼴·루프)는 공유 concrete, 엔진 물리만 `IOverlapQuery`(사이드 어댑터). `ICollisionQuery`/`IPhysicsSimulator`/`IEventSink`와 같은 "I/O만 인터페이스" 정합.
- **포트 명명 = `IOverlapQuery`** — Unity `Physics.OverlapSphere`/`OverlapCapsule` 계열의 표준 용어("overlap query"). `ICollisionQuery`(sweep/cast)와 짝. 임의 명명 아님.
- Quake/Source/Overwatch "shared game code": 히트 판정 드라이버가 shared module, 엔진 물리 broad-phase가 사이드 어댑터 — 정확히 이 분리(A2.2a의 해소=shared와 대칭).

## Out of Scope

- **클라 예측 히트 생성**(클라 `LOPOverlapQuery` 구체 + `DamageEffectHandler` 등록·호출) — A2.4.
- **예측/확정 reconcile(B)** — A2.5.
- **`KnockbackEffectHandler` 공유화** — 같은 오버랩 패턴이지만 이번 범위 밖(넉백은 서버 잔류). `IOverlapQuery`가 생기면 후속에서 동형 이관 가능(그때 shared로).
- 전투 수식/밸런스/데미지 변경 — 없음.

## Open Questions (plan에서 해소)

- **`World.Transform.Position` vs collider transform 위치 차이** — target 기준점이 collider world 위치 → 엔티티 원점으로 바뀜. 캐릭터 프리팹에서 collider가 원점 근처면 무시 가능. 서버 실행 시 부채꼴 명중이 레거시와 체감 동일한지 확인(경계 케이스 육안). 유의미하면 원점 offset 보정 검토(하지만 진실원본이 더 옳음).
- **`World.Transform.Rotation`이 실제 facing과 정합인가**(키네마틱 이행 후) — 이동이 `World.Transform`을 직접 쓰므로 회전도 진실원본일 것. `attacker.rotation`(레거시, worldTransform 경유)과 같은 값인지 확인.
- **`IMatchSeed` 서버 등록 형태** — 기존 `Register<MatchSeed>()`(concrete)를 `IMatchSeed`로도 resolve 되게 할지(`As<IMatchSeed>()`), 아니면 `DamageEffectHandler`가 concrete `MatchSeed`를 받되 인터페이스는 클라 대칭용으로만 둘지. 서버 다른 `MatchSeed` 사용처(`LOPCombatSystem` 호출 배선) grep 후 결정.
- **`GetComponentInChildren<LOPEntity>()` `parent.parent` 관례** — 프리팹 구조 전제. 클라 프리팹도 동일 구조인지 A2.4 전 확인(구체가 사이드별이라 각자 맞추면 됨).

## 진행

- [x] 브레인스토밍 (접근 1 = 오버랩 포트 + 공유 드라이버; ROADMAP 박제 2026-07-12)
- [x] spec self-review (핸들러 이동이 executor 디스패치 무영향 확인 — `As<IAbilityEffectHandler>()` base 등록; 시그니처/의존/범위 일관)
- [x] 사용자 spec 리뷰 (접근 확정 재확인 + 포트 반환타입 = string[] entityId 유지)
- [x] `writing-plans`로 구현 plan (`plans/2026-07-12-a2-2b-hit-detection-shared`)
- [x] 구현 완료 (subagent-driven, 5태스크). GameFramework `IOverlapQuery`(7a0a898) + LOP-Shared `IMatchSeed`·공유 `DamageEffectHandler`+EditMode 7테스트(da54c57/0f84fa7) + Server 사본삭제·`LOPOverlapQuery`·DI(f29f794) + Client `MatchSeed:IMatchSeed`(0f31c60). 태스크별 리뷰 + opus 최종 4레포 통합리뷰 clean(7 패리티 검사 PASS), 플레이 검증 통과. 부수: Plane 명시skip 자연소멸 + 콜라이더 다중 시 이중타격 dedup 교정.
