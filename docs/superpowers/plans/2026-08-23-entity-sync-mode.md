# 엔티티 동기화 모드 구현 계획 — 원격 예측 + 게임별 선택

> **에이전트 실행자에게:** 필수 서브스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 각 스텝은 체크박스(`- [ ]`)다.

**목표:** 원격 엔티티를 "지연 보간"으로 볼지 "내 시간선에서 같이 굴릴지"를 게임이 고르게 만들고,
Flappy Race가 후자를 골라 새끼리 몸싸움을 클라가 예측하게 한다.

**접근:** ① 모드(`EntitySyncMode`)와 정책(`IEntitySyncPolicy`)을 LOP-Shared에 순수 C#으로 만들고
단위 테스트한다. ② 클라의 두 팔로워 컴포넌트 이름을 새 축(예측/보간)에 맞춘다. ③ 렌더 보정 스무딩을
엔티티마다 갖게 한다. ④ `Reconciler`가 한 틱 스냅 **배치 전체**를 되돌리고 재생하게 넓힌다.
⑤ `EntityBinder`가 정책을 보고 `Simulated` 부여와 팔로워 선택을 하고, 스냅 라우팅도 정책을 따른다.
공유 시뮬(`FlappyWorld`·`LOPWorld`)과 서버는 **한 줄도 바뀌지 않는다.**

**기술 스택:** Unity 6.3 / C# · VContainer(DI) · NUnit EditMode · Mirror(와이어)

**스펙:** `docs/superpowers/specs/2026-08-23-entity-sync-mode-design.md`

## 전역 제약

- **손대는 저장소는 둘** — LeagueOfPhysical-Shared(모드·정책), LeagueOfPhysical-Client(배선).
  **서버 저장소와 GameFramework는 변경하지 않는다.** 머지 순서는 LOP-Shared → Client.
- **푸시 규약**(`CLAUDE.md`): `fetch` → `rebase --autostash origin/main` → `checkout main` →
  `merge --ff-only origin/main` → `merge --no-ff <feature>` → `push`. 한 줄씩 결과 확인. force 금지.
- **유니티 레포는 워크트리를 쓰지 않는다** — `git switch -c`로 브랜치를 판다. 사용자 로컬 미커밋
  픽스처(`Assets/Art` 서브모듈 포인터, 폰트 에셋, `ProjectSettings/*`)는 절대 커밋하지 않는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 `git status --short`로 확인한다.
- **`.meta`는 유니티가 만든다.** 새 `.cs`를 만들면 임포트시켜 생성된 `.meta`를 함께 커밋한다.
  **파일 이름을 바꿀 때는 `.cs`와 `.meta`를 함께 `git mv`** 한다 — GUID가 보존돼 씬·프리팹 참조가 안 끊긴다.
- **유니티 CLI**: 매번 `export PATH="$HOME/.unity/bin:$PATH"`.
  - 클라(에디터가 떠 있다): `unity cmd recompile` → `unity cmd recompile_status` → `unity cmd run_tests`
    → `unity cmd test_status`. **완료 알림은 오지 않는다 — 상태 명령을 다시 불러 확인한다.**
  - 배치모드 `unity test`는 그 프로젝트의 에디터가 떠 있으면 못 쓴다. `refresh_unity`는 이 CLI에 없다.
  - 에디터가 Play 모드면 재컴파일이 끝나지 않는다 — 확인하고, Play 중이면 **끄지 말고** 보고한다.
- **테스트 판정 기준**은 "실패 0건 + 새로 넣은 테스트가 목록에 있음". 시작 전 기준선을 한 번 재 둔다
  (직전 슬라이스 종료 시점: 클라 532).
- **새 테스트는 반드시 일부러 깨뜨려 실패를 본 뒤 되돌린다.**
- **시뮬 코드(LOP-Shared의 `*World`, `*System`)는 이 슬라이스에서 건드리지 않는다.** 정책은 시뮬이 아니라
  넷코드 어휘이므로 시뮬 클래스가 정책을 참조하면 안 된다.
- 주석은 최소로, 일상어로, **왜**만.

---

## 파일 구조

### LeagueOfPhysical-Shared (모드·정책 — 순수 C#, 테스트 가능)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Netcode/EntitySyncMode.cs` (신규) | `Interpolated` / `Predicted` 두 값 |
| `Runtime/Scripts/Netcode/IEntitySyncPolicy.cs` (신규) | `EntitySyncMode For(Entity)` 한 메서드 |
| `Runtime/Scripts/Netcode/OwnerPredictedSyncPolicy.cs` (신규) | 내 엔티티만 예측(FlapWang). NfE `OwnerPredicted` 대응 |
| `Runtime/Scripts/Netcode/CharactersPredictedSyncPolicy.cs` (신규) | 캐릭터는 전부 예측·그 외 보간(Flappy) |
| `Tests/EditMode/EntitySyncPolicyTests.cs` (신규) | 두 정책의 계약 |

### LeagueOfPhysical-Client (배선)

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Netcode/LocalEntityInterpolator.cs` → `PredictedEntityInterpolator.cs` | 이름을 축에 맞춤 + 자기 스무더 소유 + `OnCorrection` 노출 |
| `Assets/Scripts/Netcode/RemoteEntityInterpolator.cs` → `SnapshotEntityInterpolator.cs` | 이름만 |
| `Assets/Scripts/Netcode/Reconciler.cs` | 한 틱 스냅 **배치**를 되돌리고 재생. 스무딩 통지는 엔티티별 컴포넌트로 |
| `Assets/Scripts/Entity/EntityBinder.cs` | 정책을 보고 `Simulated` 부여 + 팔로워 선택 |
| `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs` | 스냅을 정책에 따라 라우팅 |
| `Assets/Scripts/Entity/CharacterCreator.cs`, `FlappyBirdCreator.cs` | 손수 `Simulated` 부여 제거 |
| `Assets/Scripts/Game/GameplayInstaller.cs` | 스무더 등록을 Singleton → Transient |
| `Assets/Scripts/Game/FlapWangLifetimeScope.cs` | `OwnerPredictedSyncPolicy` 등록 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | `CharactersPredictedSyncPolicy` 등록 |

---

## Task 1: 모드와 정책 (LOP-Shared)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Netcode/EntitySyncMode.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Netcode/IEntitySyncPolicy.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Netcode/OwnerPredictedSyncPolicy.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Netcode/CharactersPredictedSyncPolicy.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/EntitySyncPolicyTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Entity`, `LOP.EntityKind`, `LOP.EntityType`(기존)
- Produces:
  - `enum LOP.EntitySyncMode { Interpolated, Predicted }`
  - `interface LOP.IEntitySyncPolicy { EntitySyncMode For(GameFramework.World.Entity entity); }`
  - `class LOP.OwnerPredictedSyncPolicy : IEntitySyncPolicy` — 생성자 `OwnerPredictedSyncPolicy(System.Func<string> localEntityId)`
  - `class LOP.CharactersPredictedSyncPolicy : IEntitySyncPolicy` — 생성자 인자 없음
  - 뒤 태스크가 이 네 타입을 그대로 쓴다.

- [ ] **Step 1: 브랜치를 판다 (두 저장소)**

클라는 이미 `feature/entity-sync-mode`에 있다(이 계획서가 그 브랜치에 있다). Shared만 만든다.

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git fetch origin
git status --short                       # 미커밋 확인 — 사용자 픽스처는 건드리지 않는다
git switch -c feature/entity-sync-mode
git rev-list --left-right --count origin/main...HEAD    # 0	0 이어야 한다
```

그리고 손대기 전 테스트 기준선을 잰다(뒤 스텝이 이 수와 비교한다).

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests && unity cmd test_status     # 통과 수를 적어 둔다 (예상 532)
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/EntitySyncPolicyTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class EntitySyncPolicyTests
    {
        static Entity Character(string id)
        {
            var entity = new Entity(id);
            entity.Add(new EntityKind(EntityType.Character));
            return entity;
        }

        static Entity Item(string id)
        {
            var entity = new Entity(id);
            entity.Add(new EntityKind(EntityType.Item));
            return entity;
        }

        [Test]
        public void 주인_예측_정책은_내_엔티티만_예측한다()
        {
            var policy = new OwnerPredictedSyncPolicy(() => "me");

            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("me")));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Character("other")));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Item("item-1")));
        }

        [Test]
        public void 내_엔티티가_아직_없으면_보간이다()
        {
            // 입장 직후엔 내 엔티티 id가 정해지기 전이다 — 그때 예측으로 새면 남의 몸을 내 것으로 굴린다.
            var policy = new OwnerPredictedSyncPolicy(() => null);

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Character("someone")));
        }

        [Test]
        public void 캐릭터_예측_정책은_캐릭터를_전부_예측한다()
        {
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("me")));
            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("other")));
        }

        [Test]
        public void 캐릭터가_아닌_것은_보간이다()
        {
            // 아이템은 서버가 몰아주는 물건이라 클라가 굴릴 규칙이 없다.
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Item("item-1")));
        }

        [Test]
        public void 종류를_모르는_엔티티는_보간이다()
        {
            // 안전한 기본값 — 모르는 것을 굴리면 서버와 갈린다.
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(new Entity("bare")));
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 본다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
```

기대: **컴파일 실패** — `EntitySyncMode`/`OwnerPredictedSyncPolicy`가 없다(`CS0246`).

- [ ] **Step 4: 모드와 인터페이스를 만든다**

`Runtime/Scripts/Netcode/EntitySyncMode.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 클라가 이 엔티티를 어떻게 따라갈지. 게임마다 답이 다르므로 게임 스코프가 정책으로 고른다.
    /// (Unity Netcode for Entities의 <c>GhostMode</c>에 대응 — 우리는 두 값만 둔다.)
    /// </summary>
    public enum EntitySyncMode
    {
        /// <summary>서버 스냅 두 개 사이를 지연된 시간에서 보간한다. 예측 없음.</summary>
        Interpolated,

        /// <summary>내 시간선에서 같이 굴린다. 스냅이 오면 그 틱으로 맞추고 지금까지 다시 굴린다.</summary>
        Predicted,
    }
}
```

`Runtime/Scripts/Netcode/IEntitySyncPolicy.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이 게임이 각 엔티티를 어떻게 따라갈지 정한다. 클라 게임 스코프가 구현체를 등록한다.
    /// 판정 재료는 로컬 유저 id와 엔티티가 이미 들고 있는 것뿐이다 — 게임 상태를 뒤지기 시작하면
    /// 그건 정책이 아니라 로직이다.
    /// </summary>
    public interface IEntitySyncPolicy
    {
        EntitySyncMode For(GameFramework.World.Entity entity);
    }
}
```

- [ ] **Step 5: 정책 두 개를 만든다**

`Runtime/Scripts/Netcode/OwnerPredictedSyncPolicy.cs`:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// 내 엔티티만 예측하고 나머지는 보간한다(FlapWang). Unity Netcode for Entities의
    /// <c>GhostMode.OwnerPredicted</c>에 대응한다.
    /// </summary>
    public class OwnerPredictedSyncPolicy : IEntitySyncPolicy
    {
        private readonly Func<string> _localEntityId;

        public OwnerPredictedSyncPolicy(Func<string> localEntityId)
        {
            _localEntityId = localEntityId;
        }

        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            string localEntityId = _localEntityId();
            if (string.IsNullOrEmpty(localEntityId))
            {
                return EntitySyncMode.Interpolated;
            }
            return entity.Id == localEntityId ? EntitySyncMode.Predicted : EntitySyncMode.Interpolated;
        }
    }
}
```

`Runtime/Scripts/Netcode/CharactersPredictedSyncPolicy.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 캐릭터는 전부 예측하고 그 외는 보간한다(Flappy Race). 몸싸움처럼 서로 부딪히는 게 게임성인
    /// 경우, 남을 지연된 위치에 두면 "화면에 안 닿았는데 밀리는" 판정이 된다.
    /// </summary>
    public class CharactersPredictedSyncPolicy : IEntitySyncPolicy
    {
        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            return entity.Get<EntityKind>()?.Kind == EntityType.Character
                ? EntitySyncMode.Predicted
                : EntitySyncMode.Interpolated;
        }
    }
}
```

- [ ] **Step 6: `.meta` 생성 + 테스트 통과 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, `EntitySyncPolicyTests` 5개가 목록에 새로 보인다(기준선 + 5).

- [ ] **Step 7: 테스트가 진짜 실패할 수 있는지 확인한다**

`CharactersPredictedSyncPolicy.For`의 `?.Kind == EntityType.Character`를 잠시
`!= EntityType.Item`으로 바꾸고 테스트를 다시 돌려 `종류를_모르는_엔티티는_보간이다`가 **실패**하는지
본다. 확인했으면 되돌린다.

- [ ] **Step 8: 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Netcode Tests/EditMode/EntitySyncPolicyTests.cs Tests/EditMode/EntitySyncPolicyTests.cs.meta
git status --short
git commit -m "$(cat <<'EOF'
feat(netcode): 엔티티 동기화 모드와 게임별 정책

원격을 지연 보간으로 볼지 내 시간선에서 같이 굴릴지는 게임마다 답이 다르다. 지금은 그 선택이
클라 코드에 하드코딩돼 있어 갈아 끼울 자리가 없다. 모드와 정책을 순수 C#으로 떼어내 게임이
고르게 하고, 판정 자체는 단위 테스트로 못박는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 팔로워 컴포넌트 이름을 축에 맞춘다 (Client)

**Files:**
- Rename: `Assets/Scripts/Netcode/LocalEntityInterpolator.cs` → `PredictedEntityInterpolator.cs` (+`.meta`)
- Rename: `Assets/Scripts/Netcode/RemoteEntityInterpolator.cs` → `SnapshotEntityInterpolator.cs` (+`.meta`)
- Modify: `Assets/Scripts/Entity/EntityBinder.cs:91,97,119`
- Modify: `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs:197`
- Modify: `Assets/Scripts/Game/TickSystems/WorldStateSaveSystem.cs:5` (주석의 옛 이름)

**Interfaces:**
- Produces: `LOP.PredictedEntityInterpolator`, `LOP.SnapshotEntityInterpolator` — 뒤 태스크가 이 이름을 쓴다.

> **왜 이름을 바꾸나**: 지금 이름은 "누구 것이냐"(Local/Remote)로 갈라져 있는데, 새 축은 "예측이냐
> 보간이냐"다. 남의 새가 예측 대상이 되는 순간 `RemoteEntityInterpolator`라는 이름이 거짓이 된다.
> **이 태스크는 이름만 바꾼다 — 동작 변경 0.**

- [ ] **Step 1: `git mv`로 짝을 함께 옮긴다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git mv Assets/Scripts/Netcode/LocalEntityInterpolator.cs Assets/Scripts/Netcode/PredictedEntityInterpolator.cs
git mv Assets/Scripts/Netcode/LocalEntityInterpolator.cs.meta Assets/Scripts/Netcode/PredictedEntityInterpolator.cs.meta
git mv Assets/Scripts/Netcode/RemoteEntityInterpolator.cs Assets/Scripts/Netcode/SnapshotEntityInterpolator.cs
git mv Assets/Scripts/Netcode/RemoteEntityInterpolator.cs.meta Assets/Scripts/Netcode/SnapshotEntityInterpolator.cs.meta
```

- [ ] **Step 2: 클래스 이름과 참조를 고친다**

- `PredictedEntityInterpolator.cs`: 클래스명 `LocalEntityInterpolator` → `PredictedEntityInterpolator`.
  XML 주석의 "내 캐릭터의 지연 렌더링"을 **"예측된 엔티티의 지연 렌더링(내 것이든 남의 것이든)"** 으로 고친다.
- `SnapshotEntityInterpolator.cs`: 클래스명 `RemoteEntityInterpolator` → `SnapshotEntityInterpolator`.
  XML 주석의 "원격 엔티티(남 캐릭·아이템)의"를 **"보간 모드 엔티티의"** 로 고친다.
- `EntityBinder.cs` 세 곳(91·97·119줄)의 타입 이름 교체.
- `GameEntityMessageHandler.cs:197`의 `GetComponent<RemoteEntityInterpolator>()` 교체.
- `WorldStateSaveSystem.cs:5` 주석의 `LocalEntityInterpolator` 교체.

- [ ] **Step 3: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, 개수 변화 없음(이름만 바뀌었다).

- [ ] **Step 4: GUID가 보존됐는지 확인한다**

```bash
git diff --cached --stat        # rename으로 잡혀야 한다(추가/삭제가 아니라)
git log --oneline -1 -- Assets/Scripts/Netcode/PredictedEntityInterpolator.cs.meta
grep guid Assets/Scripts/Netcode/PredictedEntityInterpolator.cs.meta
```

`.meta`의 guid가 그대로여야 한다 — 새로 생겼다면 `git mv`가 아니라 새 파일을 만든 것이니 되돌린다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Netcode/PredictedEntityInterpolator.cs Assets/Scripts/Netcode/PredictedEntityInterpolator.cs.meta \
        Assets/Scripts/Netcode/SnapshotEntityInterpolator.cs Assets/Scripts/Netcode/SnapshotEntityInterpolator.cs.meta \
        Assets/Scripts/Entity/EntityBinder.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs \
        Assets/Scripts/Game/TickSystems/WorldStateSaveSystem.cs
git status --short
git commit -m "$(cat <<'EOF'
refactor(netcode): 팔로워 이름을 예측/보간 축에 맞춘다

Local/Remote는 "누구 것이냐"의 축인데, 곧 남의 엔티티도 예측 대상이 된다. 그러면
RemoteEntityInterpolator라는 이름이 거짓이 되므로 미리 축에 맞춰 둔다. 동작 변경 없음.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 렌더 보정 스무딩을 엔티티마다 갖게 한다 (Client)

**Files:**
- Modify: `Assets/Scripts/Netcode/PredictedEntityInterpolator.cs`
- Modify: `Assets/Scripts/Netcode/Reconciler.cs:31,46,57,102`
- Modify: `Assets/Scripts/Game/GameplayInstaller.cs` (스무더 등록 Singleton → Transient)

**Interfaces:**
- Consumes: `LOP.PredictedEntityInterpolator`(Task 2)
- Produces: `PredictedEntityInterpolator.OnCorrection(System.Numerics.Vector3 before, System.Numerics.Vector3 after)`
  — Task 5의 `Reconciler`가 엔티티마다 이걸 부른다.

> **왜 필요한가**: 지금 `RenderCorrectionSmoother`는 DI 싱글턴 하나뿐이라 내 캐릭터 전용이다.
> 예측 대상이 여럿이 되면 각자 튄 만큼을 각자 흡수해야 한다.

- [ ] **Step 1: 스무더 등록을 Transient로 바꾼다**

`Assets/Scripts/Game/GameplayInstaller.cs`에서:

```csharp
            // 예측 대상마다 자기 것을 갖는다 — 튄 양이 엔티티마다 다르다.
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 3f), Lifetime.Transient);
```

- [ ] **Step 2: `PredictedEntityInterpolator`가 보정 통지를 받게 한다**

이 컴포넌트는 이미 `[Inject] RenderCorrectionSmoother`를 갖고 있다(이제 인스턴스마다 다른 것을 받는다).
공개 메서드를 하나 추가한다:

```csharp
        /// <summary>
        /// 시뮬 위치가 하드 보정으로 튀었음을 알린다. 보이는 메시가 그 차이를 부드럽게 흡수한다
        /// (시뮬에는 영향 없음). 크기별로 스냅/무시를 판단하는 것은 스무더 몫이다.
        /// </summary>
        public void OnCorrection(System.Numerics.Vector3 before, System.Numerics.Vector3 after)
        {
            renderCorrectionSmoother.OnCorrection(before, after);
        }
```

- [ ] **Step 3: `Reconciler`가 그 통지를 컴포넌트로 보내게 한다**

`Reconciler`에서 `RenderCorrectionSmoother` 주입(31·46·57줄)을 지우고, `ActorRegistry`를 주입받아
102줄 근처의 `NotifyRenderCorrection`을 이렇게 바꾼다:

```csharp
            void NotifyRenderCorrection()
            {
                if (actorRegistry.TryGet(entityId, out var actor) == false)
                {
                    return;
                }
                actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(
                    preCorrectionPos.ToNumerics(),
                    GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics());
            }
```

`ActorRegistry`는 이미 DI에 싱글턴으로 있다(`GameplayInstaller`).

- [ ] **Step 4: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, 개수 변화 없음.

> 이 태스크에는 EditMode 테스트를 새로 붙이지 않는다 — 바뀐 것이 DI 수명과 MonoBehaviour 배선이라
> 단위 테스트로 감싸려면 프로덕션 코드를 테스트용으로 비트는 비용이 더 크다. 검증은 컴파일 +
> Task 6의 런타임 관찰(내 캐릭터 보정이 전과 같이 부드러운가)이다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Netcode/PredictedEntityInterpolator.cs Assets/Scripts/Netcode/Reconciler.cs \
        Assets/Scripts/Game/GameplayInstaller.cs
git status --short
git commit -m "$(cat <<'EOF'
refactor(netcode): 렌더 보정 스무딩을 엔티티마다 갖는다

스무더가 DI 싱글턴이라 내 캐릭터 전용이었다. 곧 예측 대상이 여럿이 되고, 튄 양은 엔티티마다
다르므로 각자 자기 것을 갖고 흡수해야 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `Reconciler`가 스냅 배치를 되돌린다 (Client)

**Files:**
- Modify: `Assets/Scripts/Netcode/Reconciler.cs`

**Interfaces:**
- Consumes: `PredictedEntityInterpolator.OnCorrection`(Task 3), `IWorld.LoadState/SaveState/Tick/TryGetSavedMotion`(기존)
- Produces: `Reconciler.AddServerSnap(EntitySnap)`는 시그니처 그대로지만 **여러 엔티티의 스냅을 받는다.**
  같은 틱의 스냅들이 하나의 배치가 된다.

> **전제**: 서버는 한 틱의 스냅을 한 메시지(`EntitySnapsToC`)로 함께 보낸다. 그래서 "가장 새 틱의
> 배치"만 들고 있으면 된다. 이 전제가 깨지면(엔티티마다 다른 틱으로 온다면) 오래된 스냅이 버려지므로,
> 그때는 틱별 큐로 바꿔야 한다 — 주석으로 남긴다.

- [ ] **Step 1: 대기 스냅을 배치로 바꾼다**

`Reconciler`의 필드에서 `latestSnap`/`hasPending`을 지우고:

```csharp
        // 가장 새 틱의 스냅 배치(엔티티 id → 스냅). 서버가 한 틱 분을 한 메시지로 보내므로
        // 틱이 올라가면 앞 배치는 이미 처리됐거나 낡은 것이다.
        private readonly Dictionary<string, EntitySnap> pendingSnaps = new Dictionary<string, EntitySnap>();
        private long pendingTick = long.MinValue;
```

`AddServerSnap`을 이렇게 바꾼다:

```csharp
        /// <summary>서버 스냅 수신(예측 대상 전부). 가장 새 틱의 배치만 남긴다.</summary>
        public void AddServerSnap(EntitySnap snap)
        {
            if (snap.tick < pendingTick)
            {
                return;
            }
            if (snap.tick > pendingTick)
            {
                pendingSnaps.Clear();
                pendingTick = snap.tick;
            }
            pendingSnaps[snap.entityId] = snap;
        }
```

`Reconciler.cs` 맨 위에 `using System.Collections.Generic;`이 없으면 추가한다.

- [ ] **Step 2: 게이트를 배치 전체로 넓힌다**

> **여기서 스펙의 Open Decision 하나를 답한다** — "보정 게이트를 유지할까 없앨까". **유지하되 판정을
> 배치 전체로 넓힌다.** 아무도 어긋나지 않은 틱(남이 플랩하지 않은 틱)에는 여전히 건너뛴다.
> 실측에서 사실상 매번 열리는 것으로 나오면 그때 없앤다 — Task 7에서 관측 결과를 기록한다.

`Reconcile`의 앞부분에서 `long anchorTick = snap.tick;`을 `long anchorTick = pendingTick;`으로 바꾸고,
내 엔티티 하나만 보던 오차 판정을 **배치의 모든 엔티티**로 넓힌다.
통계·스파이크 로그는 지금처럼 내 엔티티에 대해서만 남긴다(그게 사람이 읽는 값이다).

```csharp
            bool allClose = true;
            foreach (var pair in pendingSnaps)
            {
                if (!world.TryGetSavedMotion(anchorTick, pair.Key, out var predicted))
                {
                    allClose = false;   // 기록이 없으면 비교할 수 없다 — 되돌린다
                    continue;
                }
                var authoritative = pair.Value.position.ToNumerics();
                float error = System.Numerics.Vector3.Distance(predicted.Position, authoritative);

                if (pair.Key == entityId)
                {
                    reconciliationStats.Record(error);   // HUD가 읽는 값은 내 것 하나뿐
                }
                if (GameFramework.Netcode.ReconcileGate.ShouldReconcile(predicted.Position, authoritative, Threshold))
                {
                    allClose = false;
                }
            }

            // 위치가 다 가까워도 게임 고유 상태가 다르면 되돌린다(무엇을 보는지는 게임이 안다).
            if (allClose && correction.Matches(anchorTick, pendingSnaps[entityId]))
            {
                pendingSnaps.Clear();
                return;
            }
```

> **주의**: `pendingSnaps[entityId]`는 내 엔티티 스냅이 배치에 있을 때만 유효하다. 없으면
> `correction.Matches` 호출을 건너뛰고 되돌리는 쪽으로 간다(안전한 방향).

- [ ] **Step 3: 권위 값을 배치 전체에 덮는다**

지금 내 엔티티 하나에 하던 것(위치·회전·속도·`MotionContributions`·`correction.ApplyAuthoritative`·
`motionBridge.PushMotion`)을 배치의 각 엔티티에 대해 돌린다. 보정 전 위치는 엔티티마다 따로 기억해
Step 4의 통지에 쓴다:

```csharp
            var preCorrectionPositions = new Dictionary<string, System.Numerics.Vector3>();
            foreach (var pair in pendingSnaps)
            {
                var target = entityRegistry.Get(pair.Key);
                if (target == null)
                {
                    continue;
                }
                preCorrectionPositions[pair.Key] =
                    GameFramework.World.EntityMotionExtensions.GetPosition(target).ToNumerics();
            }

            bool restored = world.LoadState(anchorTick);
            // (tooOld 판정은 지금 코드 그대로)

            foreach (var pair in pendingSnaps)
            {
                var target = entityRegistry.Get(pair.Key);
                if (target == null)
                {
                    continue;
                }
                EntitySnap snap = pair.Value;
                GameFramework.World.EntityMotionExtensions.SetPosition(target, snap.position);
                GameFramework.World.EntityMotionExtensions.SetRotation(target, snap.rotation);
                GameFramework.World.EntityMotionExtensions.SetVelocity(target, snap.velocity);

                var motionContributions = target.Get<MotionContributions>();
                if (motionContributions != null)
                {
                    motionContributions.Items.Clear();
                    motionContributions.Items.AddRange(snap.contributions);
                }

                correction.ApplyAuthoritative(target, snap);
                motionBridge.PushMotion(target);
            }
            Physics.SyncTransforms();
```

- [ ] **Step 4: 재생과 통지를 배치 기준으로 한다**

재생 루프는 그대로다 — `world.Tick`이 예측 대상 전부를 굴리므로 **입력을 넣는 것은 내 엔티티뿐**이고
남의 엔티티는 `InputBuffer`가 없어 자동으로 "안 누른 것"이 된다. 통지만 엔티티마다 돌린다:

```csharp
            void NotifyRenderCorrections()
            {
                foreach (var pair in preCorrectionPositions)
                {
                    if (actorRegistry.TryGet(pair.Key, out var actor) == false)
                    {
                        continue;
                    }
                    var target = entityRegistry.Get(pair.Key);
                    if (target == null)
                    {
                        continue;
                    }
                    actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(
                        pair.Value,
                        GameFramework.World.EntityMotionExtensions.GetPosition(target).ToNumerics());
                }
            }
```

빠져나가는 모든 경로에서 이 메서드를 부르고, 끝에 `pendingSnaps.Clear()`를 한다.

- [ ] **Step 5: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, 개수 변화 없음.

> **이 시점의 동작은 지금과 같아야 한다.** 아직 예측 대상이 내 엔티티 하나뿐이라 배치의 크기가 1이다
> (다음 태스크가 남의 새를 배치에 넣는다). Task 6의 런타임 검증에서 FlapWang이 전과 같은지 본다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Netcode/Reconciler.cs
git commit -m "$(cat <<'EOF'
refactor(netcode): 되감기가 한 틱 스냅 배치 전체를 다룬다

지금은 내 엔티티 스냅 하나만 되돌리고 재생한다. 예측 대상이 여럿이 되면 같은 틱의 상태를 함께
되돌려야 재생 결과가 서버와 같아진다. 배치 크기가 1이면 지금과 동작이 같다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: 정책 배선 — 게임이 고른 대로 따라간다 (Client)

**Files:**
- Modify: `Assets/Scripts/Entity/EntityBinder.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`
- Modify: `Assets/Scripts/Entity/CharacterCreator.cs`, `Assets/Scripts/Entity/FlappyBirdCreator.cs`
- Modify: `Assets/Scripts/Game/FlapWangLifetimeScope.cs`, `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.IEntitySyncPolicy`, `LOP.EntitySyncMode`, `LOP.OwnerPredictedSyncPolicy`,
  `LOP.CharactersPredictedSyncPolicy`(Task 1), `PredictedEntityInterpolator`/`SnapshotEntityInterpolator`(Task 2)
- Produces: 불변식 — **클라에서 `Simulated`은 정책이 `Predicted`라고 답한 엔티티에만 붙는다.**

- [ ] **Step 1: 게임 스코프가 정책을 등록한다**

`FlapWangLifetimeScope.ConfigureGame`에:

```csharp
            // 내 캐릭터만 예측한다 — 남을 밀어내는 것이 게임성이 아니라서 보간으로 충분하다.
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedSyncPolicy(() => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);
```

`FlappyRaceLifetimeScope.ConfigureGame`에:

```csharp
            // 새끼리 몸싸움이 게임성이라 남의 새도 내 시간선에서 같이 굴린다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);
```

- [ ] **Step 2: `EntityBinder`가 정책을 보고 배선한다**

생성자에 `IEntitySyncPolicy syncPolicy`를 추가하고(필드도), `OnEntityCreated`의 캐릭터/아이템 분기를
모드 기준으로 바꾼다. `LOPActor`·`PhysicsBody` 생성은 지금 위치 그대로 두고, **뷰 부착 전에** 모드를 정한다:

```csharp
            EntitySyncMode syncMode = syncPolicy.For(worldEntity);
            if (syncMode == EntitySyncMode.Predicted)
            {
                // 예측 대상 = 클라가 직접 굴리는 엔티티. 시뮬은 이 표식만 보고 누구를 굴릴지 정한다.
                worldEntity.Add(new GameFramework.World.Simulated());
            }
```

그 아래 캐릭터 분기에서 내 엔티티 판정(`playerContext.actor` 세팅)은 그대로 두되, 팔로워 선택은
모드로 한다:

```csharp
                if (syncMode == EntitySyncMode.Predicted)
                {
                    PredictedEntityInterpolator interpolator = root.AddComponent<PredictedEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                }
                else
                {
                    SnapshotEntityInterpolator interpolator = root.AddComponent<SnapshotEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                }
```

아이템 분기(119줄 근처)도 같은 모양으로 바꾼다 — 정책이 아이템에 `Interpolated`를 주므로 결과는 같지만,
분기 기준이 "아이템이라서"가 아니라 "모드가 그래서"가 된다.

- [ ] **Step 3: 스냅 라우팅을 모드로 가른다**

`GameEntityMessageHandler`에 `IEntitySyncPolicy syncPolicy`를 주입하고, 145줄 근처의
`if (playerContext.entityId == actor.entityId)` 분기를 이렇게 바꾼다:

**분기 조건만 바꾸고 두 분기의 본문은 그대로 둔다.** `else` 블록(HP·GroundState·Abilities·
StatusEffects 적용 + 보간기 전달)은 한 줄도 손대지 않는다 — 마지막 줄의 컴포넌트 이름만 Task 2에서
이미 바뀌어 있다.

```csharp
                GameFramework.World.Entity targetEntity = entityRegistry.Get(serverEntitySnap.EntityId);
                bool predicted = targetEntity != null && syncPolicy.For(targetEntity) == EntitySyncMode.Predicted;

                if (predicted)
                {
                    reconciler.AddServerSnap(entitySnap);
                }
                else
                {
                    // ... 기존 원격 분기 본문 그대로 (HP·GroundState·Abilities·StatusEffects) ...
                    actor.GetComponent<SnapshotEntityInterpolator>().AddServerEntitySnap(entitySnap);
                }
```

> **알려진 한계(의도한 것 — 스펙 결과에 기록한다)**: 이 분기 때문에 **예측되는 남의 엔티티는
> 비-모션 권위 값(HP·어빌리티·상태이상)을 받지 않는다.** 내 엔티티는 `UserEntitySnapToC`라는 별도
> 메시지로 받으므로 문제없고, Flappy 새에는 Health·Abilities·StatusEffects가 아예 없어 지금은 무해하다.
> **어빌리티가 있는 게임이 원격 예측을 켜는 날 이 구멍을 메워야 한다.**
> 반대로 "모드와 무관하게 항상 적용"으로 바꾸면 안 된다 — `else` 블록은 어빌리티 발동을
> `AbilityActivation.ForPresentation`(연출용 재구성)으로 덮어쓰는데, 그걸 내가 예측 중인 엔티티에
> 적용하면 예측한 발동 상태가 뭉개진다.

- [ ] **Step 4: 크리에이터에서 손수 `Simulated` 부여를 지운다**

- `Assets/Scripts/Entity/CharacterCreator.cs`: `worldEntity.Add(new GameFramework.World.Simulated());` 줄 삭제
  (같은 블록의 `InputBuffer` 추가는 **남긴다** — 입력은 내 것에만 있다).
- `Assets/Scripts/Entity/FlappyBirdCreator.cs`: 같은 줄 삭제, 주석도 정리.

**서버 크리에이터는 건드리지 않는다** — 서버는 전원을 시뮬하므로 그대로 직접 붙인다.

- [ ] **Step 5: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, 개수 변화 없음.

- [ ] **Step 6: 배선이 실제로 갈리는지 눈으로 확인한다**

에디터 콘솔에서 확인할 수 있게, `EntityBinder`가 모드를 한 줄 로그로 남기게 **임시로** 추가한다:

```csharp
            Debug.Log($"[Sync] {entityCreated.entityId} → {syncMode}");
```

Task 6의 런타임 검증에서 FlapWang은 내 것만 `Predicted`, Flappy는 새가 전부 `Predicted`로 찍히는지
확인한 뒤 **이 줄을 지운다**(진단용 임시 로그를 남기지 않는다).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Entity/EntityBinder.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs \
        Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs \
        Assets/Scripts/Game/FlapWangLifetimeScope.cs Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short
git commit -m "$(cat <<'EOF'
feat(netcode): 게임이 고른 동기화 모드대로 따라간다

원격을 보간할지 예측할지가 EntityBinder에 하드코딩돼 있어 게임이 고를 수 없었다. 정책을 물어
Simulated 부여와 팔로워 선택, 스냅 라우팅을 모두 그 답에 맞춘다. Flappy는 새를 전부 예측해
몸싸움이 클라에서도 풀린다.

Simulated은 이제 손으로 붙이지 않는다 — 정책에서 파생된다(서버는 전원 시뮬이라 그대로).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: 런타임 검증

**Files:** 없음(관찰). 고칠 것이 나오면 해당 태스크로 돌아간다.

- [ ] **Step 1: 배포는 필요 없다는 것을 확인한다**

이 슬라이스는 **클라와 공유 패키지만** 바꿨고 서버 코드는 그대로다. 다만 공유 패키지(LOP-Shared)가
바뀌었으므로 **서버 이미지에는 영향이 없다**(정책은 서버가 참조하지 않는 새 파일이다). 서버 재배포 없이
로컬 클러스터의 기존 이미지로 검증한다. 파드가 살아 있는지만 본다:

```bash
kubectl get pods -n default | grep room
```

- [ ] **Step 2: FlapWang 회귀부터 본다**

에디터 Play → 로그인 → FlapWang 입장. 확인:

| 확인 | 기대 |
|---|---|
| 콘솔 `[Sync]` 로그 | 내 캐릭터만 `Predicted`, 남·아이템은 `Interpolated` |
| 걷기·점프·대시 | 전과 같다 |
| 남의 캐릭터 | 전과 같이 부드럽게 보간된다 |
| 되감기 통계(DebugHud) | 전과 비슷한 범위 |

**여기서 어긋나면 Flappy를 보기 전에 멈춘다** — 이 슬라이스는 FlapWang 동작을 바꾸지 않아야 한다.

- [ ] **Step 3: Flappy 혼자 들어가 본다**

로비 → Flappy Race 입장(혼자서도 매칭된다). 확인:

| 확인 | 기대 |
|---|---|
| `[Sync]` 로그 | 내 새가 `Predicted` |
| 날기·파이프 충돌 | 직전 슬라이스와 같다 |

- [ ] **Step 4: 두 명으로 몸싸움을 본다**

MPPM 가상 플레이어로 2인 입장. **새를 서로 부딪히게 만드는 것이 어렵다는 게 지난 슬라이스의 발견이다.**
전진 속도가 같아 자연히 붙지 않기 때문이다. 그래서 이렇게 한다:

1. 두 클라 모두 입장 직후 **아무 입력도 하지 않는다** — 두 새 다 같은 속도로 떨어지며 나란히 간다.
2. 스폰 지점이 세로로 2칸 간격이므로, **아래쪽 새만 플랩**해 위 새의 배를 밀어 올린다.
3. 그 순간 두 화면에서 각각 확인한다:

| 확인 | 기대 |
|---|---|
| 미는 쪽 화면 | 상대가 **즉시** 밀려난다(서버 확인을 기다리지 않는다) |
| 밀리는 쪽 화면 | 자기가 밀린다 |
| 양쪽 위치 차이 | 접촉이 끝난 뒤 크게 어긋난 채 남지 않는다 |
| 상대 플랩 순간 | 위치가 튀더라도 부드럽게 흡수된다(끊겨 보이지 않는다) |

- [ ] **Step 5: 임시 로그를 지우고 관찰을 기록한다**

Task 5 Step 6에서 넣은 `[Sync]` 로그를 지우고 커밋한다. 관찰한 것(되는 것/안 되는 것)을 그대로 적어
Task 7의 스펙 결과 절에 쓴다. 안 된 것을 "잘 됨"으로 적지 않는다.

```bash
git add Assets/Scripts/Entity/EntityBinder.cs
git commit -m "$(cat <<'EOF'
chore(netcode): 배선 확인용 임시 로그를 지운다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: 스펙에 결과를 남기고 두 저장소를 머지한다

**Files:**
- Modify: `docs/superpowers/specs/2026-08-23-entity-sync-mode-design.md` (결과 절 추가)
- Modify: `docs/superpowers/plans/2026-08-23-entity-sync-mode.md` (실제 진행 반영)

- [ ] **Step 1: 스펙에 결과를 쓴다**

담을 것: 두 저장소의 커밋 · 실제로 바뀐 모양(전·후 표) · **런타임에서 관측된 것**(특히 "상대가 즉시
밀리는가") · **검증의 한계**(못 본 것, 재현 못 한 것) · 실행하며 배운 것 · Open Decisions의 답
(보정 게이트를 어떻게 했는지, 최대 외삽 길이를 두었는지).

- [ ] **Step 2: 계획서를 실제 진행대로 맞춘다**

계획과 다르게 한 것이 있으면 계획서에도 반영한다.

- [ ] **Step 3: 문서 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git add docs/superpowers/specs/2026-08-23-entity-sync-mode-design.md \
        docs/superpowers/plans/2026-08-23-entity-sync-mode.md
git commit -m "$(cat <<'EOF'
docs(spec): 엔티티 동기화 모드 결과와 검증의 한계를 남긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: LOP-Shared를 머지·푸시한다**

`CLAUDE.md`의 푸시 규약대로, **한 줄씩 결과를 확인하며**:

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/entity-sync-mode
git push origin main
```

- [ ] **Step 5: Client를 머지·푸시한다**

유니티 레포다 — 사용자 로컬 픽스처를 잃지 않도록 `--autostash`에 맡기고, 커밋에 섞이지 않았는지
`git status --short`로 확인한다.

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git status --short
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/entity-sync-mode
git push origin main
```

- [ ] **Step 6: 다른 머신이 올린 것과 부딪히지 않았는지 본다**

이 프로젝트는 머신이 둘이라 머지 직전에 원격이 움직이는 일이 잦다. `git fetch` 후
`git log --oneline HEAD..origin/main`이 비어 있지 않으면 **머지하지 말고 멈춰서 무엇이 올라왔는지
확인**한다(직전 슬라이스에서 실제로 같은 영역을 건드린 병렬 작업과 부딪혔다).

---

## 자체 점검 (계획을 쓴 뒤 스펙과 대조)

| 스펙 항목 | 담긴 태스크 |
|---|---|
| §3.1 모드와 정책 | Task 1 |
| §3.2 `Simulated`을 정책에서 파생 | Task 5 Step 2·4 |
| §3.3 컴포넌트 rename | Task 2 |
| §4 되감기를 배치로 넓힘 | Task 4 |
| §4 엔티티별 렌더 스무딩 | Task 3 |
| §5 시뮬 무변경 | 전 태스크 — 공유 시뮬 파일을 고치는 스텝이 없다 |
| §8 정책 단위 테스트 | Task 1 Step 2 |
| §8 FlapWang 회귀 | Task 6 Step 2 |
| §8 몸싸움 런타임 | Task 6 Step 4 |
| §10 Open Decisions 답 기록 | Task 7 Step 1 |

**범위 밖(이번에 하지 않는 것)**: 내 캐릭터를 `Interpolated`로 고르는 경로(자리만 열림) · 세 번째
외삽 모드 · 아이템 예측 · 남의 플랩 추정 · 최대 외삽 길이 제한(§10 — 관측 후 필요하면 별도 슬라이스).
