# A2.1 — 서버 전투 키 기반 결정론 RNG + 매치시드 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 `LOPCombatSystem`이 크리/회피를 A1 `DeterministicRandom`을 키 `hash(matchSeed,tick,attacker,target,effectIndex)`로 씨앗 삼아 굴리게 하고, 매치시드를 `GameInfo`로 클라에 동기(클라 보관만).

**Architecture:** GameFramework `Hashing`(FNV-1a 64 + Combine)로 씨앗 조립 → per-event `DeterministicRandom` 스트림(한 공격의 3굴림을 `ref`로 공유). matchSeed는 서버 매치당 1회 생성, `GameInfo.match_seed`로 전파. `effectIndex`(다단 히트 sub-stream 구분)는 `AbilityEffectContext`로 전달.

**Tech Stack:** C# / Unity / VContainer / NUnit / Protobuf(Luban 아님, proto). GameFramework·LOP-Shared = `file:` 패키지.

## Global Constraints

- **4개 저장소**, 각각 피처 브랜치 `feature/a2-1-server-combat-keyed-rng`로 작업·커밋(main 직접 금지):
  - GameFramework `C:/Users/re5na/workspace/LOP/GameFramework`
  - LOP-Shared `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`
  - Server `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`
  - Client `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client` (문서 + 클라 홀더)
- **World 타입 풀 네임스페이스 한정**(LOP 측 파일). `Hashing`/`DeterministicRandom`은 `GameFramework` 네임스페이스.
- **UnityMCP 호출은 항상 `unity_instance` 명시.** 클라=`LeagueOfPhysical-Client@de70658b9450cbb4`, 서버=`LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시 바뀌면 `mcpforunity://instances` 재해석). GameFramework/LOP-Shared 코드는 클라 인스턴스로 컴파일·테스트(둘 다 file: 참조).
- **`DeterministicRandom`은 struct** — 여러 굴림이 한 스트림을 공유하려면 반드시 `ref`로 전달(값 복사 시 스트림 안 감김).
- **스코프 = combat만.** `GameRuleSystem`은 `IRandom`(UnityRandom) 유지 — 서버 `IRandom` 등록 삭제 금지.
- Client working tree의 기존 무관 변경(`Assets/Art`, `UIRoot.prefab`, `GraphicsSettings.asset`)은 커밋 금지.

---

### Task 1: GameFramework `Hashing` 헬퍼 (TDD)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Game/Hashing.cs`
- Test: `GameFramework/Tests/Runtime/HashingTests.cs`

**Interfaces:**
- Produces: `static class GameFramework.Hashing` — `ulong Fnv1a64(string)`, `ulong Combine(ulong hash, ulong value)`. 결정론·순서 의존.

- [ ] **Step 1: 실패 테스트** — `HashingTests.cs`
```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class HashingTests
    {
        [Test]
        public void Fnv1a64_is_deterministic_for_same_string()
        {
            Assert.AreEqual(Hashing.Fnv1a64("entity-42"), Hashing.Fnv1a64("entity-42"));
        }

        [Test]
        public void Fnv1a64_differs_for_different_strings()
        {
            Assert.AreNotEqual(Hashing.Fnv1a64("a"), Hashing.Fnv1a64("b"));
        }

        [Test]
        public void Fnv1a64_null_is_safe_and_equals_empty()
        {
            Assert.AreEqual(Hashing.Fnv1a64(""), Hashing.Fnv1a64(null));
        }

        [Test]
        public void Combine_is_deterministic()
        {
            Assert.AreEqual(Hashing.Combine(123UL, 456UL), Hashing.Combine(123UL, 456UL));
        }

        [Test]
        public void Combine_is_order_sensitive()
        {
            Assert.AreNotEqual(Hashing.Combine(1UL, 2UL), Hashing.Combine(2UL, 1UL));
        }
    }
}
```

- [ ] **Step 2: red 확인** — `refresh_unity` + `run_tests(mode="EditMode", filter="HashingTests", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`. Expected: 컴파일 실패(`Hashing` 없음).

- [ ] **Step 3: 구현** — `Hashing.cs`
```csharp
namespace GameFramework
{
    /// <summary>결정론 씨앗 조립용 순수 해시 유틸(FNV-1a 64 + 폴딩 combine). 플랫폼 독립 정수 연산.</summary>
    public static class Hashing
    {
        private const ulong Fnv64Offset = 14695981039346656037UL;
        private const ulong Fnv64Prime  = 1099511628211UL;

        /// <summary>문자열의 FNV-1a 64비트 해시(엔티티 id 등 문자열 키 → ulong). null은 빈 문자열과 동일.</summary>
        public static ulong Fnv1a64(string s)
        {
            ulong hash = Fnv64Offset;
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= Fnv64Prime;
                }
            }
            return hash;
        }

        /// <summary>누적 해시에 값 하나를 접어 넣는다(씨앗 부품 결합, 순서 의존).</summary>
        // 곱셈 먼저 → XOR: XOR을 먼저 하면 교환법칙 때문에 Combine(a,b)==Combine(b,a)가 되어 순서 의존이 깨진다.
        public static ulong Combine(ulong hash, ulong value)
        {
            hash *= Fnv64Prime;
            hash ^= value;
            return hash;
        }
    }
}
```

- [ ] **Step 4: green 확인** — 5/5 PASS.
- [ ] **Step 5: 커밋** (GameFramework 브랜치)
```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/a2-1-server-combat-keyed-rng
git add Runtime/Scripts/Game/Hashing.cs Runtime/Scripts/Game/Hashing.cs.meta Tests/Runtime/HashingTests.cs Tests/Runtime/HashingTests.cs.meta
git commit -m "feat(game): Hashing (FNV-1a 64 + Combine) — 결정론 씨앗 조립 (A2.1)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: LOP-Shared `AbilityEffectContext.EffectIndex` + executor (TDD)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectContext.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectExecutor.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilityEffectExecutorTests.cs`

**Interfaces:**
- Produces: `AbilityEffectContext`에 `int EffectIndex` (ctor 5번째 인자). executor가 효과 리스트 인덱스로 채워 핸들러에 전달.

- [ ] **Step 1: 실패 테스트** — `AbilityEffectExecutorTests.cs` 에 (a) 기존 헬퍼 `Ctx()` 를 `new AbilityEffectContext(null, null, 0, null, 0)` 로 수정(5인자), (b) EffectIndex 캡처 핸들러 + 테스트 추가:
```csharp
        private class IndexCapturingHandler<T> : AbilityEffectHandler<T> where T : AbilityEffect
        {
            public readonly System.Collections.Generic.List<int> EnterIndices = new System.Collections.Generic.List<int>();
            protected override void OnActiveEnter(AbilityEffectContext ctx, T effect) => EnterIndices.Add(ctx.EffectIndex);
        }

        [Test]
        public void Dispatch_PassesEffectListIndexToHandler()
        {
            var damage = new IndexCapturingHandler<DamageEffect>();
            var executor = new AbilityEffectExecutor(new IAbilityEffectHandler[] { damage });
            // 같은 타입 효과 3개 → 각각 인덱스 0,1,2로 호출돼야 함.
            var effects = new AbilityEffect[]
            {
                new DamageEffect(10, 1f, 90f),
                new DamageEffect(10, 1f, 90f),
                new DamageEffect(10, 1f, 90f),
            };

            executor.OnActiveEnter(new AbilityEffectContext(null, null, 0, null, 0), effects);

            Assert.That(damage.EnterIndices, Is.EqualTo(new[] { 0, 1, 2 }));
        }
```

- [ ] **Step 2: red 확인** — `run_tests(mode="EditMode", filter="AbilityEffectExecutorTests", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`. Expected: 컴파일 실패(`AbilityEffectContext` 5인자 ctor 없음 / `EffectIndex` 없음).

- [ ] **Step 3a: `AbilityEffectContext`에 EffectIndex 추가**
```csharp
    public readonly struct AbilityEffectContext
    {
        public readonly Entity Caster;
        public readonly Entity Target;
        public readonly long CurrentTick;
        public readonly IEntityManager EntityManager;
        public readonly int EffectIndex;   // 효과 리스트 내 위치 — 결정론 RNG sub-stream 구분용

        public AbilityEffectContext(Entity caster, Entity target, long currentTick,
                                    IEntityManager entityManager, int effectIndex)
        {
            Caster = caster;
            Target = target;
            CurrentTick = currentTick;
            EntityManager = entityManager;
            EffectIndex = effectIndex;
        }
    }
```

- [ ] **Step 3b: `AbilityEffectExecutor` — 효과별 인덱스 ctx** — `DriveActiveEntity`의 base ctx는 effectIndex 0으로, `OnActiveEnter`/`OnActiveTick`은 인덱스 for로 효과별 ctx 재구성:
```csharp
        // DriveActiveEntity 내 base ctx 생성부:
        var ctx = new AbilityEffectContext(caster, active.Value.Target, currentTick, entityManager, 0);

        // OnActiveEnter (OnActiveTick도 동일 패턴):
        public void OnActiveEnter(AbilityEffectContext ctx, AbilityEffect[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                if (_handlers.TryGetValue(effects[i].GetType(), out var handler))
                {
                    var effectCtx = new AbilityEffectContext(
                        ctx.Caster, ctx.Target, ctx.CurrentTick, ctx.EntityManager, i);
                    handler.OnActiveEnter(effectCtx, effects[i]);
                }
            }
        }
```
(`OnActiveTick`도 `foreach`→인덱스 `for` + effectCtx로 동일하게.)

- [ ] **Step 4: green 확인** — 기존 executor 테스트 + 신규 인덱스 테스트 전부 PASS.
- [ ] **Step 5: 커밋** (LOP-Shared 브랜치)
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/a2-1-server-combat-keyed-rng
git add Runtime/Scripts/Game/Ability/AbilityEffectContext.cs Runtime/Scripts/Game/Ability/AbilityEffectExecutor.cs Tests/EditMode/AbilityEffectExecutorTests.cs
git commit -m "feat(ability): AbilityEffectContext.EffectIndex — 효과별 RNG sub-stream 구분 (A2.1)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: LOP-Shared proto `match_seed` 필드 (재생성 + MessageId 검증)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/GameInfo.proto`
- Regenerate: `Runtime.Generated/Scripts/Protobuf/GameInfo.cs` (+ 관련), **`MessageIds.cs`는 불변이어야 함**

- [ ] **Step 1: proto 필드 추가** — `GameInfo.proto` 의 `GameInfo` 메시지에:
```proto
  uint64 match_seed = 5;
```

- [ ] **Step 2: 재생성 스크립트 확인·실행** — LOP-Shared의 proto 생성 스크립트(예: `generate_protos.sh` 또는 그 하위 스크립트)를 찾아 읽는다. **⚠️ [[proto-message-id-regen-gotcha]]**: 통합 스크립트가 `MessageIds.cs`를 지웠다 재생성하면 기존 ID가 시프트될 수 있다 → **proto→cs 생성 하위 단계만** 돌리거나, 돌린 뒤 반드시 Step 3 검증. (protoc 툴링이 로컬에 없으면 BLOCKED로 보고 — controller가 처리.)

- [ ] **Step 3: 검증** —
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git diff Runtime.Generated/Scripts/MessageIds.cs      # ← 반드시 비어 있어야 함(ID 불변)
git diff Runtime.Generated/Scripts/Protobuf/GameInfo.cs # ← MatchSeed 프로퍼티 추가 확인
```
`MessageIds.cs`에 변경이 있으면 재생성 방식을 고쳐 ID를 원복(시프트 금지). 그 후 `refresh_unity` + `read_console`(클라 인스턴스)로 컴파일 클린 확인(`GameInfo.MatchSeed` 접근 가능).

- [ ] **Step 4: 커밋** (LOP-Shared 브랜치 — Task 2와 같은 브랜치에 이어서)
```bash
git add Protos/GameInfo.proto Runtime.Generated/Scripts/Protobuf/GameInfo.cs
# (재생성이 다른 파일도 건드렸으면 함께, 단 MessageIds.cs 변경 없어야 함)
git commit -m "feat(proto): GameInfo.match_seed 필드 (A2.1, MessageId 불변)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Server — MatchSeed 홀더 + GameInfo 기록 + combat 키화

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/MatchSeed.cs`
- Modify: `.../Game/GameLifetimeScope.cs` (MatchSeed 등록; IRandom 등록은 **유지**)
- Modify: `.../Game/MessageHandler/Game.Info.MessageHandler.cs` (GameInfo에 MatchSeed 기록)
- Modify: `.../CombatSystem/LOPCombatSystem.cs` (키 RNG)
- Modify: `.../CombatSystem/ICombatSystem.cs` (Attack 시그니처)
- Modify: `.../Game/DamageEffectHandler.cs` (tick·effectIndex 전달)

**Interfaces:**
- Consumes: `GameFramework.Hashing`(T1), `GameFramework.DeterministicRandom`(A1), `AbilityEffectContext.EffectIndex`(T2), `GameInfo.MatchSeed`(T3).
- Produces: `MatchSeed` 홀더(server), `ICombatSystem.Attack(LOPEntity, LOPEntity, long tick, int effectIndex)`.

- [ ] **Step 1: `MatchSeed` 홀더** — `MatchSeed.cs`
```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>매치당 1회 생성되는 결정론 RNG 씨앗(서버 권위). 재현용으로 생성 시 로그.</summary>
    public class MatchSeed
    {
        public ulong Value { get; }

        public MatchSeed()
        {
            var bytes = System.Guid.NewGuid().ToByteArray();
            Value = System.BitConverter.ToUInt64(bytes, 0);
            Debug.Log($"[MatchSeed] {Value}");
        }
    }
}
```

- [ ] **Step 2: 서버 DI 등록** — `GameLifetimeScope.cs`에 추가(예: `AbilityActivator` 줄 근처):
```csharp
            builder.Register<MatchSeed>(Lifetime.Singleton);
```
(`IRandom`/`UnityRandom` 등록은 그대로 둔다 — GameRuleSystem이 씀.)

- [ ] **Step 3: `GameInfo`에 MatchSeed 기록** — `Game.Info.MessageHandler.cs`: `[Inject] private MatchSeed matchSeed;` 추가하고, `new GameInfo { ... }` 초기화에 `MatchSeed = matchSeed.Value,` 추가.

- [ ] **Step 4: `ICombatSystem` 시그니처** — `Attack` 선언을 `void Attack(LOPEntity attacker, LOPEntity target, long tick, int effectIndex);` 로.

- [ ] **Step 5: `LOPCombatSystem` 키 RNG** —
  - `[Inject] IRandom rng` 필드 + ctor 인자 **제거**, `[Inject]` (혹은 ctor) 로 `MatchSeed matchSeed` **추가**.
  - `Attack(LOPEntity attacker, LOPEntity target, long tick, int effectIndex)`:
```csharp
        public void Attack(LOPEntity attacker, LOPEntity target, long tick, int effectIndex)
        {
            // ... (기존 player 체크·health null/dead 체크·stats 조회·damage 기본값 동일) ...

            ulong seed = GameFramework.Hashing.Combine(
                GameFramework.Hashing.Combine(
                    GameFramework.Hashing.Combine(
                        GameFramework.Hashing.Combine(matchSeed.Value, (ulong)tick),
                        GameFramework.Hashing.Fnv1a64(attacker.entityId)),
                    GameFramework.Hashing.Fnv1a64(target.entityId)),
                (ulong)effectIndex);
            var rng = new GameFramework.DeterministicRandom(seed);

            bool isDodged  = IsDodge(attackerDexterity, targetDexterity, ref rng);
            bool isCritical = IsCritical(attackerStrength, targetStrength, ref rng);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * rng.Range(1.25f, 1.75f));
            }
            // ... (이하 dealtAmount·TakeDamage·DamageDealtEvent/DeathEvent append 동일) ...
        }

        public bool IsDodge(int attackerDex, int targetDex, ref GameFramework.DeterministicRandom rng)
        {
            float dodgeChance = (float)targetDex / (attackerDex + targetDex);
            dodgeChance = Mathf.Clamp(dodgeChance, 0.05f, 0.95f);
            double roll = rng.Range(0.0f, 1.0f);
            return roll < dodgeChance;
        }

        public bool IsCritical(int attackerStr, int targetStr, ref GameFramework.DeterministicRandom rng)
        {
            float critChance = (float)attackerStr / (attackerStr + targetStr);
            critChance = Mathf.Clamp(critChance, 0.05f, 0.50f);
            double roll = rng.Range(0.0f, 1.0f);
            return roll < critChance;
        }
```
  > **struct `ref` 필수** — `rng`를 값으로 넘기면 IsDodge/IsCritical의 굴림이 Attack의 스트림을 안 감아 회피·크리·배수가 뒤엉킨다. 반드시 `ref`.

- [ ] **Step 6: `DamageEffectHandler` 호출부** — `combatSystem.Attack(attacker, target)` → `combatSystem.Attack(attacker, target, ctx.CurrentTick, ctx.EffectIndex)`.

- [ ] **Step 7: 다른 Attack 호출부 확인** — `grep -rn "\.Attack(" Assets/Scripts` 로 다른 호출자 있으면 새 시그니처로 갱신.

- [ ] **Step 8: 컴파일 + 서버 실행 검증** — `refresh_unity` + `read_console(types=["error"])` (서버 인스턴스 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`). 클라·서버 플레이 → 공격 → 콘솔에 `[MatchSeed] <n>` 1회, 전투(데미지/크리/회피) 정상 동작. 콘솔 신규 에러 0.

- [ ] **Step 9: 커밋** (Server 브랜치)
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/a2-1-server-combat-keyed-rng
git add Assets/Scripts/Game/MatchSeed.cs Assets/Scripts/Game/MatchSeed.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs Assets/Scripts/CombatSystem/ICombatSystem.cs Assets/Scripts/Game/DamageEffectHandler.cs
git commit -m "feat(combat): 서버 전투 키 기반 결정론 RNG + MatchSeed (A2.1)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 서버 working tree에 로컬 픽스처(LOPGame/ConfigureRoomComponent 등)가 있으면 커밋 금지 — 위 파일만 add.

---

### Task 5: Client — MatchSeed 홀더 저장 (A2.3 완료)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/MatchSeed.cs`
- Modify: `.../Game/GameLifetimeScope.cs` (등록)
- Modify: `.../Game/MessageHandler/Game.Info.MessageHandler.cs` (수신 시 저장)

**Interfaces:**
- Consumes: `GameInfo.MatchSeed`(T3). Produces: 클라 `MatchSeed` 홀더(A2.4에서 예측이 읽음).

- [ ] **Step 1: 클라 `MatchSeed` 홀더** — `MatchSeed.cs`
```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>서버가 GameInfo로 보낸 매치 씨앗 보관(클라). A2.1은 저장만 — 예측 소비는 A2.4.</summary>
    public class MatchSeed
    {
        public ulong Value { get; private set; }

        public void Set(ulong value)
        {
            Value = value;
            Debug.Log($"[MatchSeed] received {value}");
        }
    }
}
```

- [ ] **Step 2: 클라 DI 등록** — `GameLifetimeScope.cs`(클라)에 `builder.Register<MatchSeed>(Lifetime.Singleton);` 추가.

- [ ] **Step 3: 수신 저장** — 클라 `Game.Info.MessageHandler.cs`: `[Inject] private MatchSeed matchSeed;` 추가, `OnGameInfoToC`에서 `matchSeed.Set(gameInfoToC.GameInfo.MatchSeed);`.

- [ ] **Step 4: 컴파일 + 검증** — `refresh_unity` + `read_console(types=["error"])`(클라 인스턴스). 플레이 접속 → 콘솔에 `[MatchSeed] received <n>` (서버 로그 값과 동일) 확인.

- [ ] **Step 5: 커밋** (Client 브랜치)
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/a2-1-server-combat-keyed-rng
git add Assets/Scripts/Game/MatchSeed.cs Assets/Scripts/Game/MatchSeed.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs
git commit -m "feat(game): 클라 MatchSeed 수신·보관 (A2.1 / A2.3)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 기존 무관 working-tree 변경(Art/prefab/GraphicsSettings)은 add 금지.

---

### Task 6: spec/ROADMAP 마무리 + 4저장소 머지

- [ ] **Step 1: spec 진행 체크** — `docs/superpowers/specs/2026-07-12-a2-1-server-combat-keyed-rng-design.md` 사용자 리뷰/plan/구현 항목 체크.
- [ ] **Step 2: ROADMAP** — Done ledger에 A2.1 한 줄(07-12, spec/plan 링크). Next A 항목에 "A2.1 완료 → 다음 A2.2(전투 공유화 + 오버랩 포트)" 주석.
- [ ] **Step 3: 머지** — 각 저장소 `feature/a2-1-server-combat-keyed-rng` → main `--no-ff`. **순서: GameFramework → LOP-Shared → Server / Client**(패키지 먼저). 클라 문서 커밋 포함.
```bash
for R in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Server; do
  cd /c/Users/re5na/workspace/LOP/$R && git checkout main && git merge --no-ff feature/a2-1-server-combat-keyed-rng -m "Merge feature/a2-1-server-combat-keyed-rng (A2.1)"
done
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ && git commit -m "docs: A2.1 구현 완료 — spec 진행 + ROADMAP Done"
git checkout main && git merge --no-ff feature/a2-1-server-combat-keyed-rng -m "Merge feature/a2-1-server-combat-keyed-rng: A2.1 (spec/plan/ROADMAP + 클라 홀더)"
```

---

## Self-Review

**1. Spec coverage:** Hashing→T1 ✓ / AbilityEffectContext.EffectIndex+executor→T2 ✓ / proto match_seed→T3 ✓ / MatchSeed 홀더·GameInfo·LOPCombatSystem 키화·DamageEffectHandler→T4 ✓ / 클라 보관(A2.3)→T5 ✓ / combat-only(GameRule IRandom 유지)→T4 Step2 명시 ✓ / A2.2~A2.5 Out of Scope→태스크 없음 ✓.

**2. Placeholder scan:** proto 재생성은 기존 툴 실행(코드 placeholder 아님) + MessageId diff 검증 명시. 그 외 코드 스텝 전부 완전. TODO/TBD 없음.

**3. Type consistency:** `Attack(LOPEntity, LOPEntity, long, int)` — ICombatSystem(T4 S4)·LOPCombatSystem(S5)·DamageEffectHandler(S6) 일치 ✓. `IsDodge/IsCritical(... ref DeterministicRandom)` ref 일관 ✓. `AbilityEffectContext` 5인자 ctor — 정의(T2 S3a)·executor(S3b)·테스트 헬퍼 Ctx()·테스트 신규 케이스 일치 ✓. `Hashing.Fnv1a64/Combine` 시그니처 — 정의(T1)·사용(T4 S5) 일치 ✓. `MatchSeed.Value`(server get / client get+Set) 일치 ✓.

## Execution Handoff

(작성자가 실행 방식 선택 제시 — subagent-driven 권장. proto 재생성[T3]은 툴링 없으면 BLOCKED 가능 → controller 개입 지점.)
