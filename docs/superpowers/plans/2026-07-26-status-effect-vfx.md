# 상태이상 연출(슬라이스 3) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 상태이상을 "때린 상대"에게도 걸 수 있게 만들고, 클라로 복제해, 몸에 붙는 이펙트로 보이게 한다.

**Architecture:** 효과의 대상을 데이터(`TargetType`)로 구분 → 슬로우 데이터 추가 → 상태이상 목록을 스냅샷에 실어 복제(**남의 캐릭터는 연출용, 내 캐릭터는 예측이 맞도록 스탯까지**) → 클라 뷰가 그 목록을 매 프레임 읽어 VFX를 켜고 끈다. 이펙트 에셋은 더미(유니티 기본 파티클)이고, 진짜 아트가 오면 Excel의 주소 한 줄만 바꾼다.

**Tech Stack:** C#(순수 코어=LOP-Shared / GameFramework), Unity 6000.3(URP, Addressables), Luban 마스터데이터, Protobuf 와이어, VContainer DI, UnityMCP(컴파일·테스트 구동).

spec: `docs/superpowers/specs/2026-07-26-status-effect-vfx-design.md`

## Global Constraints

- **main 직접 커밋 금지.** 모든 저장소에서 브랜치 `feature/status-effect-vfx`로 작업한다. (클라 저장소는 이미 이 브랜치에 있다.)
- **World 타입은 풀 네임스페이스로 한정한다** — LOP 측 파일은 `using UnityEngine;`을 쓰므로 `GameFramework.World.Entity`처럼 적는다(`Component` 이름 충돌 회피). World 어셈블리 *내부* 파일은 예외.
- **새 Luban 테이블을 추가하면 `LOPMasterData.TableFiles` 배열도 갱신한다.** 안 하면 Entrance에서 `KeyNotFoundException`으로 죽는다. EditMode `TableFileManifestTests`가 지킨다.
- **`gen.sh`는 생성 폴더를 통째로 지우고 다시 만든다** → `.meta`가 전부 사라진다. Unity 재스캔(아래 컴파일 검증)을 **먼저** 돌린 뒤 `git add` 한다.
- **컴파일 검증은 UnityMCP로 한다.** 인스턴스를 반드시 명시한다:
  - 클라 `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`
  - 서버 `unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c"`
  - 해시가 바뀌었으면 `mcpforunity://instances`에서 이름으로 다시 찾는다.
  - 절차: `refresh_unity(scope="all", mode="force", compile="request", wait_for_ready=true)` → `read_console(types=["error"])`가 **0건**.
- **클라 `Assets/Scripts` 코드는 EditMode 테스트를 쓸 수 없다**(전부 `Assembly-CSharp`라 asmdef 참조 불가). 클라 검증은 컴파일 + 인게임이다. LOP-Shared·GameFramework는 EditMode로 테스트한다.
- **커밋 메시지는 한국어**, 본문에 *왜*를 남긴다. 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.
- 기존 콘솔 잡음 `There can be only one active Event System.`(파킹된 기존 항목)은 에러가 아니므로 무시한다.

---

## File Structure

| 파일 | 책임 | Task |
|---|---|---|
| `LOP-Shared/Runtime/Scripts/Game/Ability/AbilityEffect.cs` | `TargetType` enum + `StatusEffectApplyEffect.Target` 필드 | 1 |
| `LOP-Shared/Runtime/Scripts/Game/Ability/StatusEffectApplyEffectHandler.cs` | 대상 분기(자기 / 명중자 전원) | 1 |
| `LOP-Shared/Tests/EditMode/StatusEffectApplyTargetTypeTests.cs` | 위 분기의 회귀 테스트 | 1 |
| `infrastructure/table/Datas/__beans__.xlsx` | `target_type` 컬럼 | 2 |
| 클·서 `AbilityDataProvider.cs` | Luban 행 → 코어 effect 매핑 | 2 |
| 클·서 `GameLifetimeScope.cs` | 핸들러에 `EntityRegistry` 주입 | 2 |
| `infrastructure/table/Datas/#StatusEffect.xlsx`, `#Ability.xlsx` | 슬로우 정의 + attack에 부여 | 3 |
| `LOP-Shared/Protos/ProtoActiveEffect.proto`, `EntitySnap.proto` | 와이어 표현 | 4 |
| 서버 `EntitySnapshotBroadcastSystem.cs` | 스냅샷에 상태이상 채우기 | 4 |
| 클라 `Netcode/EntitySnap.cs`, `GameEntityMessageHandler.cs` | 수신·원격 반영 | 4 |
| `LOP-Shared/.../StatusEffectSystem.cs` | `ApplyAuthoritativeState` — 서버 목록으로 맞추기 | 5 |
| 클라 `Netcode/Reconciler.cs` | 내 캐릭 상태이상을 앵커에서 서버 값으로 덮기 | 5 |
| `클라 Assets/Art_Placeholder/Vfx/*` | 더미 이펙트 프리팹 2개 + 머티리얼 | 6 |
| `infrastructure/table/Datas/#StatusEffectView.xlsx` | 상태이상 id → 이펙트 주소 | 7 |
| `클라 Assets/Scripts/Entity/StatusEffectVfxView.cs` | 상태 목록을 읽어 VFX를 켜고 끈다 | 7 |
| `클라 Assets/Scripts/Entity/EntityBinder.cs` | 캐릭터 스폰 시 위 컴포넌트 부착 | 7 |

---

## Task 1: `TargetType` + 핸들러 대상 분기 (LOP-Shared)

지금 상태이상은 **시전자 자신에게만** 걸린다. "때린 상대를 느리게"가 불가능하다. 대상을 데이터로 정하게 만든다. 넉백이 이미 같은 모양(히트 정의자=데미지가 명중자를 기록 → on-hit 라이더가 읽음)이라 그 규칙을 그대로 따른다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffect.cs` (`StatusEffectApplyEffect` 클래스)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/StatusEffectApplyEffectHandler.cs` (전체 교체)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectApplyTargetTypeTests.cs` (신규)

**Interfaces:**
- Consumes: `LOP.AttackHitContext.LandedTargets`(`IReadOnlyCollection<string>`), `GameFramework.World.EntityRegistry`, `LOP.StatusEffectSystem.Apply(Entity, StatusEffectData, string sourceEntityId, long tick)`
- Produces:
  - `public enum LOP.TargetType { Self, HitTargets }`
  - `LOP.StatusEffectApplyEffect(int statusEffectId, TargetType target = TargetType.Self)` — 읽기 전용 필드 `int StatusEffectId`, `TargetType Target`
  - `LOP.StatusEffectApplyEffectHandler(StatusEffectSystem, Func<int, StatusEffectData?>, GameFramework.World.EntityRegistry)` — **생성자에 레지스트리 추가**(기존 2인자 → 3인자)

- [ ] **Step 1: 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/status-effect-vfx
```

- [ ] **Step 2: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectApplyTargetTypeTests.cs`:

```csharp
using System;
using NUnit.Framework;
using GameFramework.World;

namespace LOP.Tests.EditMode
{
    public class StatusEffectApplyTargetTypeTests
    {
        private const int SlowId = 100;

        private static StatusEffectData SlowData() => new StatusEffectData(
            SlowId, DurationPolicy.Duration, 60,
            new[] { new StatusModifierSpec((int)EntityStatType.MoveSpeed, -0.3f, ModifierType.PercentAdd) },
            StatusStackPolicy.Refresh, 1);

        private static Entity MakeActor(string id)
        {
            var e = new Entity(id);
            e.Add(new StatusEffects());
            e.Add(new Stats());
            return e;
        }

        private static bool HasSlow(Entity e) =>
            e.Get<StatusEffects>().Effects.Exists(x => x.EffectId == SlowId);

        private (StatusEffectApplyEffectHandler handler, EntityRegistry registry) Build()
        {
            var registry = new EntityRegistry();
            var handler = new StatusEffectApplyEffectHandler(
                new StatusEffectSystem(new StatsSystem()), _ => SlowData(), registry);
            return (handler, registry);
        }

        [Test]
        public void SelfAppliesToCasterOnly()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            var victim = MakeActor("victim");
            registry.Add(caster);
            registry.Add(victim);

            var hit = new AttackHitContext();
            hit.MarkLanded("victim");
            var ctx = new AbilityEffectContext(caster, caster, 10, 0, hit);

            handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetType.Self));

            Assert.IsTrue(HasSlow(caster));
            Assert.IsFalse(HasSlow(victim));
        }

        [Test]
        public void HitTargetsAppliesToLandedOnly()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            var hitVictim = MakeActor("hit");
            var missedVictim = MakeActor("missed");
            registry.Add(caster);
            registry.Add(hitVictim);
            registry.Add(missedVictim);

            var hit = new AttackHitContext();
            hit.MarkLanded("hit");
            var ctx = new AbilityEffectContext(caster, caster, 10, 0, hit);

            handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetType.HitTargets));

            Assert.IsTrue(HasSlow(hitVictim));
            Assert.IsFalse(HasSlow(missedVictim));
            Assert.IsFalse(HasSlow(caster));
        }

        [Test]
        public void HitTargetsWithNoLandedTargetsDoesNothing()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            registry.Add(caster);

            var ctx = new AbilityEffectContext(caster, caster, 10, 0, new AttackHitContext());

            Assert.DoesNotThrow(() =>
                handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetType.HitTargets)));
            Assert.IsFalse(HasSlow(caster));
        }

        [Test]
        public void DefaultTargetIsSelf()
        {
            Assert.AreEqual(TargetType.Self, new StatusEffectApplyEffect(SlowId).Target);
        }
    }
}
```

> 테스트가 `handler.OnActiveEnter(...)`를 밖에서 부를 수 있는 이유: `AbilityEffectHandler<T>`가
> `public void OnActiveEnter(AbilityEffectContext, AbilityEffect)` 래퍼를 갖고 있고(타입 캐스팅용),
> 우리가 재정의하는 `protected virtual OnActiveEnter(ctx, T)`는 그 안에서 불린다. `DamageEffectHandlerTests`가
> 이미 같은 방식으로 호출한다 — 접근 제한자를 테스트 편의로 열 필요 없다.

> `StatusEffectSystem`의 생성자는 `StatusEffectSystem(StatsSystem statsSystem)` 하나뿐이다.
> `StatusModifierSpec`·`StatusEffectData`의 인자 순서는 기존 `StatusEffectSystemTests.cs`를 참고해 맞춘다.

- [ ] **Step 3: 테스트가 실패하는지 확인**

UnityMCP `run_tests`(EditMode, `test_names=["StatusEffectApplyTargetTypeTests"]`, 클라 인스턴스).
기대: **컴파일 실패** — `TargetType` 없음 + 핸들러 생성자 인자 수 불일치.

- [ ] **Step 4: `TargetType` + effect 필드 추가**

`AbilityEffect.cs`의 `StatusEffectApplyEffect`를 교체한다:

```csharp
    /// <summary>상태효과를 누구에게 걸지. 발동 전에 지목하는 대상(AbilityActivation.Target)과는 다른 축이다.</summary>
    public enum TargetType
    {
        /// <summary>시전자 자신.</summary>
        Self,
        /// <summary>이번 발동에서 명중한 대상 전원(AttackHitContext).</summary>
        HitTargets,
    }

    /// <summary>상태효과를 건다(버프/디버프). 적용된 효과는 독립 <see cref="StatusEffects"/> 컴포넌트로 살아간다(수명 분리).</summary>
    public sealed class StatusEffectApplyEffect : AbilityEffect
    {
        public readonly int StatusEffectId;     // TbStatusEffect 참조(런타임 데이터는 핸들러가 resolve)
        public readonly TargetType Target;

        public StatusEffectApplyEffect(int statusEffectId, TargetType target = TargetType.Self)
        {
            StatusEffectId = statusEffectId;
            Target = target;
        }
    }
```

- [ ] **Step 5: 핸들러 대상 분기 구현**

`StatusEffectApplyEffectHandler.cs` 전체를 교체한다:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// <see cref="StatusEffectApplyEffect"/> 핸들러(코어). Active 진입 시 효과 id를 설정으로 resolve해
    /// <see cref="StatusEffectSystem.Apply"/>. 적용된 효과는 독립 <see cref="StatusEffects"/>로 살아간다(수명 분리).
    /// <para>대상은 effect의 <see cref="TargetType"/>가 정한다 — Self는 시전자, HitTargets는 이번 발동에서
    /// 명중한 대상 전원(넉백과 같은 on-hit 라이더).</para>
    /// <para>resolve(MasterData)는 <c>resolver</c> 델리게이트 심으로 주입 — 코어는 MasterData를 직접 참조하지 않는다.</para>
    /// </summary>
    public class StatusEffectApplyEffectHandler : AbilityEffectHandler<StatusEffectApplyEffect>
    {
        private readonly StatusEffectSystem _statusEffectSystem;
        private readonly Func<int, StatusEffectData?> _resolver;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;

        public StatusEffectApplyEffectHandler(StatusEffectSystem statusEffectSystem,
                                              Func<int, StatusEffectData?> resolver,
                                              GameFramework.World.EntityRegistry entityRegistry)
        {
            _statusEffectSystem = statusEffectSystem;
            _resolver = resolver;
            _entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, StatusEffectApplyEffect effect)
        {
            var data = _resolver(effect.StatusEffectId);
            if (data == null)
            {
                return;
            }

            if (effect.Target == TargetType.Self)
            {
                if (ctx.Caster != null)
                {
                    _statusEffectSystem.Apply(ctx.Caster, data.Value, ctx.Caster.Id, ctx.CurrentTick);
                }
                return;
            }

            if (ctx.HitContext == null || ctx.Caster == null)
            {
                return;
            }
            foreach (string id in ctx.HitContext.LandedTargets)
            {
                GameFramework.World.Entity target = _entityRegistry.Get(id);
                if (target != null)
                {
                    _statusEffectSystem.Apply(target, data.Value, ctx.Caster.Id, ctx.CurrentTick);
                }
            }
        }
    }
}
```

- [ ] **Step 6: 생성자가 바뀌어 깨진 기존 테스트 고치기**

`StatusEffectApplyEffectHandler`를 **2인자로 부르는 기존 테스트 2곳**이 컴파일 실패한다:

- `Tests/EditMode/AbilityReplayDeterminismTests.cs:26` — `new StatusEffectApplyEffectHandler(_status, Resolve)`
- `Tests/EditMode/AbilitySystemTests.cs:29` — `new StatusEffectApplyEffectHandler(_statusEffects, Resolve)`

각 테스트가 이미 갖고 있는 `EntityRegistry`가 있으면 그것을 넘기고, 없으면 그 자리에서 만들어 넘긴다:

```csharp
            var statusHandler = new StatusEffectApplyEffectHandler(_statusEffects, Resolve, new EntityRegistry());
```

> 두 테스트 모두 `TargetType.Self`(기본값) 경로만 쓰므로 레지스트리 내용은 비어 있어도 된다 —
> Self 분기는 레지스트리를 보지 않는다.

- [ ] **Step 7: 테스트 통과 확인 + 회귀**

UnityMCP `run_tests`(EditMode, `test_names=["StatusEffectApplyTargetTypeTests"]`). 기대: **4/4 PASS**.
이어서 필터 없이 EditMode 전체를 돌려 회귀 0을 확인한다(직전 기준선 332/332 + 신규 4 = 336).

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/Ability/AbilityEffect.cs \
        Runtime/Scripts/Game/Ability/StatusEffectApplyEffectHandler.cs \
        Tests/EditMode/StatusEffectApplyTargetTypeTests.cs Tests/EditMode/StatusEffectApplyTargetTypeTests.cs.meta \
        Tests/EditMode/AbilityReplayDeterminismTests.cs Tests/EditMode/AbilitySystemTests.cs
git commit -m "$(cat <<'EOF'
feat(ability): 상태효과 부여 대상을 TargetType로 데이터화

지금까지 상태효과는 시전자 자신에게만 걸려 "때린 상대를 느리게"가 불가능했다.
대상을 데이터로 정하게 해 명중자 디버프를 연다 — 명중 판정은 데미지가 이미
기록해두므로(AttackHitContext) 넉백과 같은 on-hit 라이더 규칙을 따른다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

> `.meta`가 아직 없으면 UnityMCP `refresh_unity`(클라)로 Unity에 새 파일을 인식시킨 뒤 add 한다.

---

## Task 2: `target_type` 마스터데이터 컬럼 + provider 매핑 (5 저장소)

Task 1이 코드에 연 자리를 데이터로 채운다. 기존 `duration_policy`/`stack_policy`가 string 컬럼 + 런타임 `Enum.Parse` 방식이므로 **동일하게 간다**(Luban enum 타입을 새로 만들지 않는다).

**Files:**
- Modify (Excel): `infrastructure/table/Datas/__beans__.xlsx` (`StatusEffectApplyEffect` bean)
- Modify (Excel): `infrastructure/table/Datas/#Ability.xlsx` (haste 행에 `Self` 명시)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs:50-51`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs` (같은 case)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:49` 부근
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs` (같은 블록)

**Interfaces:**
- Consumes: `LOP.TargetType`, `LOP.StatusEffectApplyEffect(int, TargetType)` (Task 1)
- Produces: `LOP.MasterData.StatusEffectApplyEffect.TargetType` (string 프로퍼티)

- [ ] **Step 1: bean 파일 구조 이해**

`__beans__.xlsx`는 **필드가 열이 아니라 행**이다. 시트명은 `Sheet`, 구조는 이렇다:

```
행1  ##var | full_name | parent | ... | *fields(10열부터)
행2  ##var |           |        | ... | name(10) alias(11) type(12) group(13) comment(14) tags(15) variants(16)
행3        | AbilityEffect            | (polymorphic base)
행4        | StatusEffectApplyEffect  | parent=AbilityEffect | ... | status_effect_id | | int
행5        | MotionEffect             | ...                          speed             | | float
행6        | DamageEffect             | ...                          amount            | | int
행7        |                          |                              range             | | float   ← 필드 추가는 새 행
행8        |                          |                              angle             | | float
```

즉 **필드를 하나 더 주려면 그 bean 아래에 빈 행을 끼워** 10·12열만 채운다(DamageEffect의 `range`/`angle`이 그 예).

- [ ] **Step 2: `target_type` 필드 행 삽입**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/__beans__.xlsx')
ws = wb['Sheet']
assert ws.cell(row=4, column=2).value == 'StatusEffectApplyEffect', ws.cell(row=4, column=2).value
assert ws.cell(row=4, column=10).value == 'status_effect_id', ws.cell(row=4, column=10).value
ws.insert_rows(5)
ws.cell(row=5, column=10, value='target_type')
ws.cell(row=5, column=12, value='string')
wb.save('Datas/__beans__.xlsx')
for i, r in enumerate(ws.iter_rows(values_only=True), 1):
    cells = [('' if c is None else str(c)) for c in r]
    while cells and cells[-1] == '':
        cells.pop()
    if cells and i <= 10:
        print(i, cells[:3], '|', cells[9:13])
"
```

기대: 4행이 `StatusEffectApplyEffect`/`status_effect_id`, **5행이 빈 bean 칸 + `target_type`/`string`**,
6행부터 `MotionEffect`가 밀려 내려감.

> `assert`가 실패하면 파일 구조가 바뀐 것이다 — 덤프를 다시 떠서 행 번호를 맞춘 뒤 진행한다.

- [ ] **Step 3: 기존 haste 데이터에 `Self` 명시**

bean에 컬럼이 하나 늘었으므로 데이터 문자열도 하나 늘어야 한다. `#Ability.xlsx` haste 행(5행)의
`effects` 칸(현재 11번째 컬럼 — `cue` 제거 후 위치):

```
현재: StatusEffectApplyEffect,1
변경: StatusEffectApplyEffect,1,Self
```

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#Ability.xlsx')
ws = wb.active
header = [c.value for c in ws[1]]
col = header.index('effects#sep=,') + 1
print('effects col =', col, 'before =', ws.cell(row=5, column=col).value)
ws.cell(row=5, column=col, value='StatusEffectApplyEffect,1,Self')
wb.save('Datas/#Ability.xlsx')
print('after =', 'StatusEffectApplyEffect,1,Self')
"
```

- [ ] **Step 4: 재생성**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

기대: 마지막 줄 `[done]`.

- [ ] **Step 5: 클·서 provider 매핑 수정**

**두 파일 모두** 같은 case를 바꾼다:

```csharp
                    case LOP.MasterData.StatusEffectApplyEffect s:
                        result.Add(new StatusEffectApplyEffect(
                            s.StatusEffectId,
                            (TargetType)System.Enum.Parse(typeof(TargetType), s.TargetType)));
                        break;
```

- [ ] **Step 6: DI 등록에 레지스트리 추가**

클·서 `GameLifetimeScope.cs`가 핸들러를 람다로 만들고 있다. 세 번째 인자를 추가한다:

```csharp
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
```

> 기존 람다의 두 인자 표현이 위와 다르면 **기존 표현을 유지**하고 세 번째 인자만 덧붙인다.

- [ ] **Step 7: 컴파일 검증**

클·서 각각 `refresh_unity` → `read_console(types=["error"])` **0건**.

- [ ] **Step 8: 커밋 (5 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && git checkout -b feature/status-effect-vfx
git add table/Datas/ && git commit -m "feat(masterdata): StatusEffectApplyEffect.target_type 컬럼

효과를 자기에게 걸지 명중자에게 걸지를 데이터로 정한다. 기존 haste는 Self 명시.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git checkout -b feature/status-effect-vfx
git add -A && git commit -m "chore(gen): target_type 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git checkout -b feature/status-effect-vfx
git add -A && git commit -m "chore(gen): target_type 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/AbilityDataProvider.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(ability): target_type 매핑 + 핸들러에 레지스트리 주입

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git checkout -b feature/status-effect-vfx
git add Assets/Scripts/Game/AbilityDataProvider.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(ability): target_type 매핑 + 핸들러에 레지스트리 주입

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

> MasterData 저장소는 `git add -A` — `gen.sh`가 지웠다 되살린 `.meta`까지 담아야 한다.
> 서버 저장소에 로컬 픽스처(`GameRuleSystem.cs`·`ConfigureRoomComponent.cs`·`DefaultVolumeProfile.asset`)가
> 수정돼 있으면 **커밋하지 않는다** — 파일을 명시해 add 한다.

---

## Task 3: 슬로우 데이터 + attack에 부여 효과

**Files:**
- Modify (Excel): `infrastructure/table/Datas/#StatusEffect.xlsx`
- Modify (Excel): `infrastructure/table/Datas/#Ability.xlsx`

**Interfaces:**
- Produces: `TbStatusEffect`에 id=2 슬로우 행, 캐릭터별 attack 어빌리티(3·5·6)의 effects에 `StatusEffectApplyEffect,2,HitTargets`

- [ ] **Step 1: 슬로우 상태이상 행 추가**

haste 행이 5행이므로 6행에 쓴다:

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#StatusEffect.xlsx')
ws = wb.active
row = ['', 2, 'slow', '이동속도 -30% 감소', 'Duration', 60,
       'MoveSpeed', -0.3, 'PercentAdd', 'Refresh', 1]
for col, value in enumerate(row, start=1):
    ws.cell(row=6, column=col, value=value)
wb.save('Datas/#StatusEffect.xlsx')
for r in ws.iter_rows(values_only=True):
    print([('' if c is None else c) for c in r])
"
```

**−30% / 60틱(≈2초)** — 눈으로 확인하는 게 목적이라 체감되는 세기로 둔다. 밸런스는 나중에 Excel에서 조정.

- [ ] **Step 2: 세 캐릭터의 attack에 부여 효과 추가**

공격 어빌리티는 캐릭터별로 갈라져 있다 — **3(Knight) / 5(Necromancer) / 6(Archer)** 세 행 모두 바꿔야
한 캐릭터만 슬로우를 거는 일이 없다. `global_attack`(4)은 테스트용이라 건드리지 않는다.

**효과 순서 주의**: `DamageEffect`가 명중자를 기록하므로 `StatusEffectApplyEffect`는 **데미지보다 뒤**에
와야 한다(넉백과 같은 규칙). 아래 문자열이 그 순서를 지킨다.

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#Ability.xlsx')
ws = wb.active
header = [c.value for c in ws[1]]
col = header.index('effects#sep=,') + 1
ids = header.index('id') + 1
for r in range(5, ws.max_row + 1):
    if str(ws.cell(row=r, column=ids).value) in ('3', '5', '6'):
        cur = ws.cell(row=r, column=col).value
        ws.cell(row=r, column=col, value=cur + ',StatusEffectApplyEffect,2,HitTargets')
        print(r, '->', ws.cell(row=r, column=col).value)
wb.save('Datas/#Ability.xlsx')
"
```

기대 출력: 3개 행이 각각
`DamageEffect,10,2,90,KnockbackEffect,5,12,0.8,StatusEffectApplyEffect,2,HitTargets`

- [ ] **Step 3: 재생성 + 컴파일 검증**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

클·서 `refresh_unity` → `read_console(types=["error"])` **0건**.

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/ && git commit -m "feat(masterdata): 슬로우 상태이상 + 세 캐릭터 attack에 명중자 부여

캐릭터별로 갈린 공격(3/5/6) 전부에 넣어야 한 캐릭터만 거는 일이 없다.
효과 순서는 데미지 뒤 — 명중자를 데미지가 기록하기 때문(넉백과 같은 규칙).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add -A && git commit -m "chore(gen): 슬로우 데이터

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add -A && git commit -m "chore(gen): 슬로우 데이터

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 상태이상 와이어 — proto + 서버 채움 + 클라 반영

클라는 상태이상을 아예 모른다. 스냅샷에 실어 보낸다. **스냅샷이 전량 권위**라 매 틱 통째로 교체한다(HP와 같은 규칙).

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/ProtoActiveEffect.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs` (`BuildAllEntitySnaps`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `LOP.StatusEffects.Effects`(`List<ActiveEffect>`), `LOP.ActiveEffect(int effectId, long expireTick, int stackCount, string sourceEntityId, string sourceId)`
- Produces: wire 필드 `EntitySnap.status_effects`(**필드 번호 11**, repeated `ProtoActiveEffect`), 클라 DTO `LOP.EntitySnap.statusEffects`(`List<ActiveEffect>`)

- [ ] **Step 1: `ProtoActiveEffect.proto` 생성**

`ProtoMotionContribution.proto`와 같은 형식(top-level 패킷이 아니므로 `@auto_generate` 주석 **없음**):

```protobuf
syntax = "proto3";

message ProtoActiveEffect
{
	int32 effect_id   = 1;
	int64 expire_tick = 2;
	int32 stack_count = 3;
}
```

- [ ] **Step 2: `EntitySnap.proto`에 목록 추가**

`import` 한 줄과 필드 한 줄을 넣는다. 현재 최대 필드 번호가 10이므로 **11**을 쓴다:

```protobuf
import "ProtoActiveEffect.proto";
```

```protobuf
	repeated ProtoActiveEffect status_effects = 11;
```

- [ ] **Step 3: 재생성 + MessageId 무변경 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
cd ..
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

기대: **출력이 비어 있음**(MessageId가 바뀌면 와이어가 깨진다).

- [ ] **Step 4: 서버가 채운다**

`BuildAllEntitySnaps`의 시전 상태 블록(`snap.ActiveAbilityId` / `snap.AbilityEndTick`) **아래**에:

```csharp
                var statusEffects = worldEntity.Get<StatusEffects>();
                if (statusEffects != null)
                {
                    foreach (var e in statusEffects.Effects)
                    {
                        snap.StatusEffects.Add(new ProtoActiveEffect
                        {
                            EffectId = e.EffectId,
                            ExpireTick = e.ExpireTick,
                            StackCount = e.StackCount,
                        });
                    }
                }
```

> 변수명(`worldEntity`/`snap`)이 실제 코드와 다르면 그 파일의 이름을 따른다.

- [ ] **Step 5: 클라 DTO에 필드 추가**

`Assets/Scripts/Netcode/EntitySnap.cs`의 `abilityEndTick` 아래:

```csharp
        // AutoMapper 대상 아님 — 핸들러가 수동으로 채운다(contributions와 같은 이유).
        public List<ActiveEffect> statusEffects { get; set; } = new List<ActiveEffect>();
```

- [ ] **Step 6: 수신한 목록을 DTO에 채운다 (내 캐릭·원격 공통 경로)**

`GameEntityMessageHandler`에서 `entitySnap.contributions`를 채우는 블록 **바로 아래**,
**내 캐릭/원격으로 갈라지는 `if` 앞**에 넣는다. 내 캐릭터 분기는 이 DTO를 그대로 `Reconciler`에
넘기므로(Task 5가 여기서 읽는다), 갈라진 뒤에 채우면 내 캐릭터 쪽이 영영 빈 목록이 된다:

```csharp
                entitySnap.statusEffects.Clear();
                foreach (var pe in serverEntitySnap.StatusEffects.OrEmpty())
                {
                    entitySnap.statusEffects.Add(new ActiveEffect(
                        pe.EffectId, pe.ExpireTick, pe.StackCount,
                        sourceEntityId: null, sourceId: $"se:{pe.EffectId}"));
                }
```

- [ ] **Step 7: 클라가 원격 엔티티에 반영**

`GameEntityMessageHandler`의 `remoteAbilities` 블록(시전 상태 복원) **아래**에 —
위에서 채운 DTO를 그대로 쓴다(같은 변환을 두 번 하지 않는다):

```csharp
                    StatusEffects remoteEffects = remoteEntity?.Get<StatusEffects>();
                    if (remoteEffects != null)
                    {
                        // 스냅샷이 전량 권위 — 통째로 교체한다(HP와 같은 규칙).
                        remoteEffects.Effects.Clear();
                        remoteEffects.Effects.AddRange(entitySnap.statusEffects);
                    }
```

> 원격 엔티티는 클라가 시뮬하지 않으므로 **스탯 모디파이어를 다시 적용하지 않는다** — 느려진 결과는
> 이미 서버가 보낸 위치·속도 스냅샷에 들어 있다. 클라는 "무엇이 걸렸는지"만 알면 그림을 그릴 수 있다.
> (내 캐릭터는 직접 움직이므로 모디파이어까지 맞춰야 한다 — Task 5.)

- [ ] **Step 8: 컴파일 검증**

클·서 `refresh_unity` → `read_console(types=["error"])` **0건**.

- [ ] **Step 9: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/ Runtime.Generated/
git commit -m "feat(wire): EntitySnap에 상태이상 목록 추가

클라가 상태이상을 아예 몰라 그림을 그릴 수 없었다. 스냅샷이 전량 권위.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "feat(snapshot): 상태이상 브로드캐스트

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(snapshot): 원격 상태이상 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: 소유자(내 캐릭터) 상태이상 권위 동기화

내 클라는 **내 캐릭터만** 시뮬레이션하므로, 내가 건 헤이스트는 예측해서 알지만 **남이 나에게 건 슬로우는
계산조차 하지 않아 모른다.** 서버 AI가 플레이어를 공격하므로(`EnemyBrain`) 바로 밟히는 경로다.
모르면 이펙트가 안 뜨는 것보다 **서버만 −30%로 나를 움직여 매 틱 위치가 어긋나는 것**이 더 크다(러버밴딩).

**넉백과 같은 축**으로 푼다 — 넉백(`MotionContributions`)도 서버가 가한 것이라 클라가 예측하지 않고,
`Reconciler`가 서버 스냅에서 복원한다. 상태이상도 같은 부류다. **와이어는 추가하지 않는다** — Task 4가
넣은 `EntitySnap.status_effects`를 내 캐릭터 분기도 쓰는 것뿐이다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/StatusEffect/StatusEffectSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectAuthoritativeStateTests.cs` (신규)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/Reconciler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs` (필요 시 — `Reconciler` 의존 추가)

**Interfaces:**
- Consumes: `LOP.ActiveEffect`, `LOP.StatusEffectData`, `LOP.StatsSystem.RemoveModifiersBySourceId`, 클라 `StatusEffectDataProvider.Get(int) → StatusEffectData?`
- Produces: `LOP.StatusEffectSystem.ApplyAuthoritativeState(GameFramework.World.Entity, IReadOnlyList<ActiveEffect>, Func<int, StatusEffectData?>)`

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectAuthoritativeStateTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using GameFramework.World;

namespace LOP.Tests.EditMode
{
    public class StatusEffectAuthoritativeStateTests
    {
        private const int SlowId = 100;
        private const int HasteId = 101;

        private static StatusEffectData Data(int id, float value) => new StatusEffectData(
            id, DurationPolicy.Duration, 60,
            new[] { new StatusModifierSpec((int)EntityStatType.MoveSpeed, value, ModifierType.PercentAdd) },
            StatusStackPolicy.Refresh, 1);

        private static StatusEffectData? Resolve(int id)
        {
            if (id == SlowId) { return Data(SlowId, -0.3f); }
            if (id == HasteId) { return Data(HasteId, 0.3f); }
            return null;
        }

        private static Entity MakeActor()
        {
            var e = new Entity("me");
            e.Add(new StatusEffects());
            var stats = new Stats();
            stats.BaseStats[(int)EntityStatType.MoveSpeed] = 10f;
            e.Add(stats);
            return e;
        }

        private static (StatusEffectSystem sys, StatsSystem stats) Build()
        {
            var statsSystem = new StatsSystem();
            return (new StatusEffectSystem(statsSystem), statsSystem);
        }

        private static List<ActiveEffect> Server(params int[] ids)
        {
            var list = new List<ActiveEffect>();
            foreach (int id in ids)
            {
                list.Add(new ActiveEffect(id, 200, 1, "server", "se:" + id));
            }
            return list;
        }

        [Test]
        public void AddsEffectTheClientDidNotPredict()
        {
            var (sys, statsSystem) = Build();
            var me = MakeActor();

            sys.ApplyAuthoritativeState(me, Server(SlowId), Resolve);

            Assert.IsTrue(me.Get<StatusEffects>().Effects.Exists(e => e.EffectId == SlowId));
            Assert.AreEqual(7f, statsSystem.GetValue(me.Get<Stats>(), (int)EntityStatType.MoveSpeed), 0.001f);
        }

        [Test]
        public void RemovesEffectTheServerNoLongerHas()
        {
            var (sys, statsSystem) = Build();
            var me = MakeActor();
            sys.Apply(me, Data(SlowId, -0.3f), "server", 0);

            sys.ApplyAuthoritativeState(me, Server(), Resolve);

            Assert.IsFalse(me.Get<StatusEffects>().Effects.Exists(e => e.EffectId == SlowId));
            // 모디파이어까지 떨어져 이동속도가 원래대로 돌아와야 한다.
            Assert.AreEqual(10f, statsSystem.GetValue(me.Get<Stats>(), (int)EntityStatType.MoveSpeed), 0.001f);
        }

        [Test]
        public void KeepsEffectBothSidesAgreeOnWithoutDoublingModifier()
        {
            var (sys, statsSystem) = Build();
            var me = MakeActor();
            sys.Apply(me, Data(HasteId, 0.3f), "me", 0);      // 클라가 예측해둔 헤이스트

            sys.ApplyAuthoritativeState(me, Server(HasteId), Resolve);

            Assert.AreEqual(1, me.Get<StatusEffects>().Effects.Count);
            // 13f — 모디파이어가 두 번 붙었다면 16f가 된다.
            Assert.AreEqual(13f, statsSystem.GetValue(me.Get<Stats>(), (int)EntityStatType.MoveSpeed), 0.001f);
        }

        [Test]
        public void AddsAndRemovesInOneCall()
        {
            var (sys, _) = Build();
            var me = MakeActor();
            sys.Apply(me, Data(HasteId, 0.3f), "me", 0);

            sys.ApplyAuthoritativeState(me, Server(SlowId), Resolve);

            var effects = me.Get<StatusEffects>().Effects;
            Assert.IsTrue(effects.Exists(e => e.EffectId == SlowId));
            Assert.IsFalse(effects.Exists(e => e.EffectId == HasteId));
        }

        [Test]
        public void UnknownEffectIdIsSkipped()
        {
            var (sys, _) = Build();
            var me = MakeActor();

            Assert.DoesNotThrow(() => sys.ApplyAuthoritativeState(me, Server(999), Resolve));
            Assert.AreEqual(0, me.Get<StatusEffects>().Effects.Count);
        }
    }
}
```

> `StatsSystem.GetValue`의 계산은 `(Base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)`이므로
> base 10 · PercentAdd −0.3 → 7, +0.3 → 13이다. 기대값이 다르면 실제 계산식을 먼저 확인한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `run_tests`(EditMode, `test_names=["StatusEffectAuthoritativeStateTests"]`).
기대: **컴파일 실패** — `ApplyAuthoritativeState` 없음.

- [ ] **Step 3: `ApplyAuthoritativeState` 구현**

`StatusEffectSystem.cs`의 `Remove` 아래에 추가한다:

```csharp
        /// <summary>
        /// 서버가 보낸 효과 목록으로 이 엔티티의 상태이상을 맞춘다(스냅샷이 권위).
        /// 없는 건 걸고, 사라진 건 떼고, 스택이 다르면 다시 계산한다 — 스탯 모디파이어까지 함께 맞춘다.
        /// <para>내 캐릭 예측은 *내가 건* 효과만 안다. 남이 건 것(슬로우 등)은 계산조차 하지 않으므로
        /// 서버가 알려줘야 하고, 안 그러면 서버만 나를 느리게 움직여 위치가 어긋난다.
        /// 넉백 기여를 스냅에서 복원하는 것과 같은 축이다.</para>
        /// <para><paramref name="resolver"/>로 설정을 찾는다 — 와이어엔 id·만료틱·스택만 실리고
        /// 모디파이어 명세는 마스터데이터에 있다(코어는 MasterData를 직접 참조하지 않는다).</para>
        /// </summary>
        public void ApplyAuthoritativeState(Entity entity,
                                            System.Collections.Generic.IReadOnlyList<ActiveEffect> authoritative,
                                            System.Func<int, StatusEffectData?> resolver)
        {
            var effects = entity.Get<StatusEffects>();
            if (effects == null)
            {
                return;
            }
            var stats = entity.Get<Stats>();

            // 1) 서버에 없는 것 제거(모디파이어도 함께)
            for (int i = effects.Effects.Count - 1; i >= 0; i--)
            {
                var local = effects.Effects[i];
                bool stillActive = false;
                for (int j = 0; j < authoritative.Count; j++)
                {
                    if (authoritative[j].EffectId == local.EffectId)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (stillActive == false)
                {
                    if (stats != null)
                    {
                        _statsSystem.RemoveModifiersBySourceId(stats, local.SourceId);
                    }
                    effects.Effects.RemoveAt(i);
                }
            }

            // 2) 서버에 있는 것 추가/갱신
            for (int a = 0; a < authoritative.Count; a++)
            {
                var server = authoritative[a];
                string sourceId = SourceIdFor(server.EffectId);
                int idx = effects.Effects.FindIndex(e => e.EffectId == server.EffectId);

                if (idx < 0)
                {
                    var data = resolver(server.EffectId);
                    if (data == null)
                    {
                        continue;   // 설정을 모르는 효과 — 무시(구버전 데이터 등)
                    }
                    effects.Effects.Add(new ActiveEffect(
                        server.EffectId, server.ExpireTick, server.StackCount, server.SourceEntityId, sourceId));
                    AddModifiers(stats, data.Value, sourceId, server.StackCount);
                    continue;
                }

                var current = effects.Effects[idx];
                if (current.StackCount != server.StackCount)
                {
                    var data = resolver(server.EffectId);
                    if (data != null && stats != null)
                    {
                        _statsSystem.RemoveModifiersBySourceId(stats, current.SourceId);
                        AddModifiers(stats, data.Value, current.SourceId, server.StackCount);
                    }
                }
                // 만료 틱은 서버 값으로 덮는다(리프레시된 지속시간을 내가 모를 수 있다).
                effects.Effects[idx] = new ActiveEffect(
                    server.EffectId, server.ExpireTick, server.StackCount, current.SourceEntityId, current.SourceId);
            }
        }
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, `test_names=["StatusEffectAuthoritativeStateTests"]`). 기대: **5/5 PASS**.
이어서 LOP-Shared EditMode 전체를 돌려 회귀 0.

- [ ] **Step 5: `Reconciler`가 복원 시 호출**

`Reconciler.cs`에서 `abilityState.RestoreTo(worldEntity);` **바로 다음**에 넣는다.
**순서가 중요하다** — `RestoreTo`가 예측 상태를 되돌리므로, 그 위에 서버 값을 덮어야 한다.

```csharp
            // 남이 나에게 건 효과(슬로우 등)는 내가 예측할 수 없다 → 서버 목록이 진실.
            // 위 RestoreTo가 되돌린 예측값 위에 덮는다(넉백 기여를 스냅에서 복원하는 것과 같은 축).
            // 앵커에서 맞춰두면 이어지는 재생이 현재 틱까지 밀어 올린다.
            statusEffectSystem.ApplyAuthoritativeState(worldEntity, snap.statusEffects, statusEffectDataProvider.Get);
```

필드·생성자에 두 의존을 추가한다(기존 주입 스타일을 그대로 따른다):

```csharp
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly StatusEffectDataProvider statusEffectDataProvider;
```

> `StatusEffectSystem`·`StatusEffectDataProvider`는 이미 `GameLifetimeScope`에 등록돼 있다
> (어빌리티 효과 핸들러가 쓴다). 등록이 없으면 그때 추가한다.
> `statusEffectDataProvider.Get`의 시그니처가 `Func<int, StatusEffectData?>`와 안 맞으면
> 람다로 감싼다: `id => statusEffectDataProvider.Get(id)`.

- [ ] **Step 6: 컴파일 검증**

클라 `refresh_unity` → `read_console(types=["error"])` **0건**.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/StatusEffect/StatusEffectSystem.cs \
        Tests/EditMode/StatusEffectAuthoritativeStateTests.cs Tests/EditMode/StatusEffectAuthoritativeStateTests.cs.meta
git commit -m "feat(status): 서버 목록으로 상태이상을 맞추는 ApplyAuthoritativeState

내 캐릭 예측은 내가 건 효과만 안다 — 남이 건 슬로우는 계산조차 하지 않아 모른다.
모르면 서버만 나를 느리게 움직여 위치가 어긋난다(러버밴딩). HealthSystem과 같은 이름·역할.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/Reconciler.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(netcode): 재조정 시 내 캐릭 상태이상을 서버 값으로 맞춘다

넉백 기여를 스냅에서 복원하는 것과 같은 축. RestoreTo(예측 복원) 다음에 덮어야
다음 재조정에 지워지지 않는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: 더미 이펙트 프리팹 + Addressables 등록 (클라)

진짜 아트가 오기 전까지 쓸 **더미**를 만든다. 유니티 기본 파티클만 써서 외부 의존·라이선스가 없다.
`Assets/Art`(디자이너 소유 서브모듈)와 **폴더를 분리**해, 교체 시 폴더째 지우면 끝나게 한다.

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Art_Placeholder/Vfx/PlaceholderParticle.mat`
- Create: `LeagueOfPhysical-Client/Assets/Art_Placeholder/Vfx/StatusEffect_Haste.prefab`
- Create: `LeagueOfPhysical-Client/Assets/Art_Placeholder/Vfx/StatusEffect_Slow.prefab`
- Modify: `LeagueOfPhysical-Client/Assets/AddressableAssetsData/AssetGroups/Vfx.asset` (스크립트가 생성)

**Interfaces:**
- Produces: Addressables 주소 두 개 —
  `Assets/Art_Placeholder/Vfx/StatusEffect_Haste.prefab`,
  `Assets/Art_Placeholder/Vfx/StatusEffect_Slow.prefab`
  (주소 = 에셋 경로. 기존 캐릭터 프리팹과 같은 관행)

- [ ] **Step 1: 프리팹·머티리얼 생성**

UnityMCP `execute_code`(클라 인스턴스)로 실행한다:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

const string Dir = "Assets/Art_Placeholder/Vfx";
Directory.CreateDirectory(Dir);
AssetDatabase.Refresh();

// URP 프로젝트라 빌트인 파티클 머티리얼은 자홍색으로 뜬다 → URP 파티클 셰이더로 직접 만든다.
Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
if (shader == null) { shader = Shader.Find("Sprites/Default"); }
Debug.Log($"[vfx] shader = {(shader == null ? "NULL" : shader.name)}");

string matPath = $"{Dir}/PlaceholderParticle.mat";
var mat = new Material(shader);
AssetDatabase.CreateAsset(mat, matPath);

GameObject Make(string name, Color color, float speed)
{
    var go = new GameObject(name);
    var ps = go.AddComponent<ParticleSystem>();

    var main = ps.main;
    main.loop = true;
    main.duration = 1f;
    main.startLifetime = 0.7f;
    main.startSpeed = speed;              // 음수면 아래로 흐른다
    main.startSize = 0.12f;
    main.startColor = color;
    main.simulationSpace = ParticleSystemSimulationSpace.Local;
    main.playOnAwake = true;

    var emission = ps.emission;
    emission.rateOverTime = 25f;

    var shape = ps.shape;
    shape.enabled = true;
    shape.shapeType = ParticleSystemShapeType.Circle;
    shape.radius = 0.45f;
    shape.rotation = new Vector3(-90f, 0f, 0f);   // 원판을 바닥에 눕힌다

    var renderer = go.GetComponent<ParticleSystemRenderer>();
    renderer.material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    renderer.renderMode = ParticleSystemRenderMode.Billboard;

    string path = $"{Dir}/{name}.prefab";
    PrefabUtility.SaveAsPrefabAsset(go, path);
    Object.DestroyImmediate(go);
    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
}

Make("StatusEffect_Haste", new Color(1f, 0.85f, 0.2f, 0.9f), 1.2f);   // 노란 입자가 위로
Make("StatusEffect_Slow",  new Color(0.3f, 0.6f, 1f, 0.9f), -0.8f);   // 파란 입자가 아래로

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
Debug.Log("[vfx] placeholder prefabs created");
```

기대: 콘솔에 `[vfx] shader = ...`(NULL 아님)과 `[vfx] placeholder prefabs created`.

- [ ] **Step 2: Addressables `Vfx` 그룹에 등록**

UnityMCP `execute_code`(클라 인스턴스):

```csharp
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

var settings = AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vfx");
if (group == null)
{
    group = settings.CreateGroup("Vfx", false, false, true, null,
        typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
}

foreach (string path in new[]
{
    "Assets/Art_Placeholder/Vfx/StatusEffect_Haste.prefab",
    "Assets/Art_Placeholder/Vfx/StatusEffect_Slow.prefab",
})
{
    string guid = AssetDatabase.AssetPathToGUID(path);
    var entry = settings.CreateOrMoveEntry(guid, group);
    entry.address = path;                       // 주소 = 경로(기존 캐릭터 프리팹과 같은 관행)
    Debug.Log($"[vfx] addressable: {entry.address}");
}

settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
AssetDatabase.SaveAssets();
```

기대: `[vfx] addressable: ...` 2줄. 에러 0건.

- [ ] **Step 3: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Art_Placeholder Assets/AddressableAssetsData
git commit -m "chore(vfx): 상태이상 더미 이펙트 프리팹 2개 + Addressables Vfx 그룹

유니티 기본 파티클만 써서 외부 의존·라이선스가 없다. Assets/Art(디자이너 서브모듈)와
폴더를 분리해, 진짜 아트가 오면 폴더째 지우고 주소만 바꾸면 되게 했다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

`.meta`가 안 잡히면 `refresh_unity`(클라)를 먼저 돌린다.

---

## Task 7: `TbStatusEffectView` + `StatusEffectVfxView`

마지막 조각. 상태이상 id → 이펙트 주소 테이블을 만들고, 그것을 읽어 VFX를 켜고 끄는 뷰 컴포넌트를 붙인다.

**Files:**
- Create (Excel): `infrastructure/table/Datas/#StatusEffectView.xlsx`
- Modify (Excel): `infrastructure/table/Datas/__tables__.xlsx`
- Modify: `LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs` (`TableFiles`)
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/StatusEffectVfxView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/EntityBinder.cs` (캐릭터 장식 뷰 블록)

**Interfaces:**
- Consumes: `LOP.StatusEffects.Effects`, `GameFramework.World.EntityRegistry`, `LOP.MasterData.LOPMasterData`
- Produces:
  - `LOP.MasterData.Tables.TbStatusEffectView.GetOrDefault(int)` → `StatusEffectView { int Id; string VfxAddress; }`
  - `LOP.StatusEffectVfxView` — `void SetEntityId(string)`, `void Cleanup()`

- [ ] **Step 1: 테이블 생성 + 등록**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
for r in [
    ['##var',   'id',  'vfx_address'],
    ['##type',  'int', 'string'],
    ['##group', '',    ''],
    ['##',      'id',  'vfx_address'],
    ['',        1,     'Assets/Art_Placeholder/Vfx/StatusEffect_Haste.prefab'],
    ['',        2,     'Assets/Art_Placeholder/Vfx/StatusEffect_Slow.prefab'],
]:
    ws.append(r)
wb.save('Datas/#StatusEffectView.xlsx')

wb = openpyxl.load_workbook('Datas/__tables__.xlsx')
ws = wb.active
ws.append(['', 'TbStatusEffectView', 'StatusEffectView', 'TRUE', '#StatusEffectView.xlsx', 'id', 'map', 'c', 'StatusEffectView', '', ''])
wb.save('Datas/__tables__.xlsx')
for r in ws.iter_rows(values_only=True):
    print([('' if c is None else c) for c in r])
"
```

**`group`(8번째 칸)이 반드시 `c`** — 클라 전용이라 서버 타깃 생성에서 빠져야 한다.

- [ ] **Step 2: 재생성 + 클라 전용인지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
ls ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/ | grep -i statuseffectview
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ | grep -i statuseffectview
```

기대: **클라에만** `StatusEffectView.cs`/`TbStatusEffectView.cs`, **서버는 출력 없음**.

- [ ] **Step 3: 로더 목록 갱신**

`LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs`:

```csharp
        public static readonly System.Collections.Generic.IReadOnlyList<string> TableFiles = new[]
        {
            "tbcharacter", "tbskin", "tbskinasset", "tbitem", "tbstatuseffect", "tbability",
            "tbcharacterloadout", "tbabilityview", "tbstatuseffectview"
        };
```

- [ ] **Step 4: 로더 목록 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, `test_names=["TableFileManifestTests"]`, 클라). 기대: PASS.
빠뜨렸으면 여기서 실패한다(게임 실행 시 Entrance에서 죽는 것을 대신 막아준다).

- [ ] **Step 5: `StatusEffectVfxView` 작성**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/StatusEffectVfxView.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 이 캐릭터에 걸린 상태이상을 몸에 붙는 이펙트로 보여준다.
    /// 상태 목록을 매 프레임 읽어 맞춘다 — 새로 걸린 것만 켜고, 풀린 것만 끈다(걷기 애니와 같은 방식).
    /// </summary>
    public class StatusEffectVfxView : MonoBehaviour, ICleanup
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        private string entityId;

        // 켜둔(또는 켜는 중인) 이펙트 하나.
        private class Vfx
        {
            public GameObject instance;                     // 아직 로딩 중이면 null
            public AsyncOperationHandle<GameObject> handle;
        }

        private readonly Dictionary<int, Vfx> vfxByEffectId = new Dictionary<int, Vfx>();

        // 매 프레임 재사용 — 프레임마다 새로 만들면 쓰레기가 쌓인다.
        private readonly HashSet<int> activeIds = new HashSet<int>();
        private readonly List<int> removedIds = new List<int>();

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        private void Update()
        {
            if (entityId == null)
            {
                return;
            }

            var statusEffects = entityRegistry.Get(entityId)?.Get<StatusEffects>();
            activeIds.Clear();
            if (statusEffects != null)
            {
                foreach (var effect in statusEffects.Effects)
                {
                    activeIds.Add(effect.EffectId);
                }
            }

            foreach (int id in activeIds)
            {
                if (vfxByEffectId.ContainsKey(id) == false)
                {
                    Spawn(id);
                }
            }

            removedIds.Clear();
            foreach (var pair in vfxByEffectId)
            {
                if (activeIds.Contains(pair.Key) == false)
                {
                    removedIds.Add(pair.Key);
                }
            }
            foreach (int id in removedIds)
            {
                Despawn(id);
            }
        }

        private void Spawn(int effectId)
        {
            var view = masterData.Tables.TbStatusEffectView.GetOrDefault(effectId);
            if (view == null || string.IsNullOrEmpty(view.VfxAddress))
            {
                return;   // 연출을 정해두지 않은 상태이상
            }

            var vfx = new Vfx();
            vfxByEffectId[effectId] = vfx;    // 자리를 먼저 잡아 같은 효과를 두 번 로드하지 않는다
            vfx.handle = Addressables.LoadAssetAsync<GameObject>(view.VfxAddress);
            vfx.handle.Completed += handle =>
            {
                // 로딩이 끝나기 전에 상태이상이 풀렸거나 캐릭터가 사라졌을 수 있다(슬로우는 2초).
                // 그러면 받아온 것을 그대로 놓아준다.
                bool stillWanted = this != null
                    && vfxByEffectId.TryGetValue(effectId, out Vfx current)
                    && ReferenceEquals(current, vfx);
                if (stillWanted == false || handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Addressables.Release(handle);
                    return;
                }

                // 모델(스킨) 밑이 아니라 루트에 붙인다 — 스킨이 갈릴 때 딸려 파괴되지 않게.
                vfx.instance = Instantiate(handle.Result, transform);
            };
        }

        private void Despawn(int effectId)
        {
            if (vfxByEffectId.TryGetValue(effectId, out Vfx vfx) == false)
            {
                return;
            }
            vfxByEffectId.Remove(effectId);

            if (vfx.instance != null)
            {
                Destroy(vfx.instance);
            }
            // 아직 로딩 중이면 여기서 놓지 않는다 — 위 완료 콜백이 "이미 풀렸다"를 보고 대신 놓는다(이중 해제 방지).
            if (vfx.handle.IsValid() && vfx.handle.IsDone)
            {
                Addressables.Release(vfx.handle);
            }
        }

        public void Cleanup()
        {
            removedIds.Clear();
            foreach (var pair in vfxByEffectId)
            {
                removedIds.Add(pair.Key);
            }
            foreach (int id in removedIds)
            {
                Despawn(id);
            }
            entityId = null;
        }
    }
}
```

- [ ] **Step 6: `EntityBinder`가 붙이도록 배선**

`EntityBinder.cs`의 캐릭터 장식 뷰 블록(`CharacterNameplate` 등록 **바로 뒤**)에 추가한다:

```csharp
                StatusEffectVfxView statusEffectVfx = root.AddComponent<StatusEffectVfxView>();
                objectResolver.Inject(statusEffectVfx);
                statusEffectVfx.SetEntityId(entityCreated.entityId);
```

> `ICleanup`이므로 엔티티 파괴 시 `EntityBinder.OnEntityDestroyed`의 `GetComponentsInChildren<ICleanup>`
> 스윕이 자동으로 정리한다 — 별도 해제 배선이 필요 없다.

- [ ] **Step 7: 컴파일 검증**

클라 `refresh_unity` → `read_console(types=["error"])` **0건**.
이어서 EditMode 전체를 돌려 회귀 0을 확인한다.

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/ && git commit -m "feat(masterdata): TbStatusEffectView — 상태이상 id → 이펙트 주소

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git add -A && git commit -m "chore(gen): TbStatusEffectView 생성물 + 로더 목록 갱신

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git add -A && git commit -m "chore(gen): 재생성 반영

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/StatusEffectVfxView.cs Assets/Scripts/Entity/StatusEffectVfxView.cs.meta \
        Assets/Scripts/Entity/EntityBinder.cs
git commit -m "feat(view): 상태이상을 몸에 붙는 이펙트로 표시

상태 목록을 매 프레임 읽어 맞춘다. 로딩이 끝나기 전에 상태이상이 풀릴 수 있어
(슬로우 2초) 도착 시점에 아직 필요한지 다시 확인한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

> 서버 MasterData 저장소에 실질 변화가 없고 `.meta` GUID만 바뀌었으면 커밋하지 말고
> `git checkout -- .`로 되돌린다(무의미한 잡음).

---

## 인게임 검증 (사용자, 2에디터)

- [ ] **1. 헤이스트** — 발동 → **내 몸에 노란 이펙트**, 100틱(≈3초) 뒤 사라짐
- [ ] **2. 슬로우 걸기** — 상대를 때림 → **상대 몸에 파란 이펙트**, **내 몸엔 안 뜸**
      (= `TargetType`이 동작한다는 증거). 상대가 눈에 띄게 느려짐
- [ ] **3. 슬로우 당하기(가장 깨지기 쉬움)** — 몬스터에게 맞음 → **내 몸에 파란 이펙트 + 내가 느려짐**,
      그리고 **캐릭터가 뒤로 끌리지 않음**(= 소유자 동기화가 동작한다는 증거).
      끌린다면 Task 5의 `ApplyAuthoritativeState` 호출 위치를 먼저 의심한다(`RestoreTo` 다음이어야 함)
- [ ] **4. 동시** — 헤이스트 중에 맞음 → **두 이펙트가 같이** 보임
- [ ] **5. 정리** — 슬로우가 풀리기 전에 캐릭터가 죽음 → 이펙트가 화면에 남지 않음
- [ ] **6. 원격** — 다른 클라 화면에서도 1~4가 동일하게 보임
- [ ] **7. 회귀** — 이동·공격 모션·넉백·데미지 숫자가 이전과 같음

## 머지

6개 검증이 통과하면 각 저장소의 `feature/status-effect-vfx`를 `--no-ff`로 main에 머지하고,
`docs/ROADMAP.md`에 슬라이스 3 완료를 기록한다(무엇을 확인했는지 + 더미 에셋 교체 경로).
