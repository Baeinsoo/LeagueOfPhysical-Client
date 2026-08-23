# B2-d2 — 클라가 자기 새를 난다 구현 계획

> **에이전트 실행자에게:** 필수 서브스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 각 스텝은 체크박스(`- [ ]`)다.

**목표:** 플레이어가 화면을 눌러 새를 띄우고, 그 새를 클라가 스스로 예측(시뮬)하며, 시뮬이 쓰는 몸과
물리 팔로워가 만드는 몸이 같은 치수를 갖게 한다.

**접근:** ① 캡슐 치수를 **엔티티가 들고 있는 데이터**(`GameFramework.World.CapsuleShape`)로 옮겨 세 곳에
흩어진 값을 하나로 만든다 — 붙이는 쪽은 게임을 아는 크리에이터, 읽는 쪽은 게임을 모르는 시뮬·팔로워.
② 클라 새에 `Simulated`를 붙여 예측·되감기 대상으로 만든다. ③ 플랩을 누를 화면(`FlapPadView`)을
새로 만들어 기존 `PlayerInputManager.SetJump` 경로에 연결한다. 와이어·서버 입력 버퍼는 손대지 않는다.

**기술 스택:** Unity 6.3 / C# · VContainer(DI) · UI Toolkit(UXML/USS) · NUnit EditMode · Mirror(와이어)
· Luban 마스터데이터(`TbFlappyConfig`)

**스펙:** `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` (§6-2가 이 계획의 범위,
§6 "정정 3"이 몸 규격 문제의 실측, §7이 검증표)

---

## 전역 제약

- **저장소 4개를 함께 고친다** — GameFramework · LeagueOfPhysical-Shared · LeagueOfPhysical-Client ·
  LeagueOfPhysical-Server. 패키지는 `file:` 로컬 참조라 로컬에서는 즉시 반영되지만, **머지·푸시는
  GameFramework → LOP-Shared → Client/Server 순서**로 한다(참조 방향).
- **푸시는 `CLAUDE.md`의 "푸시 규약"만 따른다** — `fetch` → `rebase --autostash origin/main` →
  `checkout main` → `merge --ff-only origin/main` → `merge --no-ff <feature>` → `push`. 한 줄씩 결과를
  확인한다. `--force` 계열 금지.
- **유니티 레포는 워크트리를 쓰지 않는다** — `git switch -c <branch>`로 그 자리에서 브랜치를 판다.
  전환 전 미커밋 작업물을 확인하고, 사용자 로컬 픽스처(폰트 에셋·ProjectSettings·아트 서브모듈
  포인터·URP 볼륨 프로파일)는 임의로 커밋하지 않는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 `git status --short`로 확인한다.
- **`.meta` 파일은 유니티가 만든다.** 새 `.cs`/`.uxml`/`.uss`를 만든 뒤 유니티에 임포트시키고
  생성된 `.meta`를 **함께 커밋**한다. 직접 만들지 않는다.
- **유니티 CLI 사용법**: 비대화형 셸에서는 PATH에 없다 — 매번 `export PATH="$HOME/.unity/bin:$PATH"`.
  에디터를 닫을 필요 없이 떠 있는 에디터에 붙는다(`unity cmd ...`, 프로젝트는 cwd로 감지).
  **에디터가 Play 모드면 재컴파일이 끝나지 않는다** — 먼저 `EditorApplication.isPlaying`을 확인하고
  켜져 있으면 사용자에게 정지를 요청한다(임의로 끄면 편집분이 날아간다).
  - **실제로 있는 명령**(2026-08-23 실행 중 확인): 임포트·컴파일은 `unity cmd import_asset` +
    `unity cmd recompile` → `unity cmd recompile_status`, 테스트는 `unity cmd run_tests` →
    `unity cmd test_status`. (`refresh_unity`는 이 CLI 버전에 없다.)
  - **`unity test`(배치모드)는 그 프로젝트의 에디터가 떠 있으면 못 쓴다** — 프로젝트를 단독
    점유해야 하기 때문. 에디터가 떠 있는 프로젝트는 `unity cmd run_tests`를 쓴다.
  - **⚠️ 조용한 오라우팅**: 대상 프로젝트의 에디터가 안 떠 있으면 그 디렉터리에서 `unity cmd`를
    돌려도 **에러 없이 다른(떠 있는) 인스턴스로 라우팅된다.** 서버 쪽을 확인할 때는 서버 에디터가
    실제로 떠 있는지 보거나(`unity status`), 배치모드 `unity test`로 돌린다.
- **테스트는 EditMode 전량이 돈다**(`filter` 인자가 먹지 않는다) — 몇 분 걸린다. 기준선(B2-d1 종료 시점): 클라 **527개**, 서버 **506개** 전부 통과. 두 프로젝트가 도는 테스트
  집합이 다르므로(각자 자기 테스트 + 패키지 테스트) **개수를 외우지 말고 시작 전에 한 번 재 두고
  그 수와 비교한다** — 판정 기준은 "실패 0건 + 새로 넣은 테스트가 목록에 있음"이다.
- **새 테스트는 반드시 일부러 깨뜨려 실패를 본 뒤 되돌린다.** 통과만으로 검증됐다고 말하지 않는다.
- **대시는 이 슬라이스에 넣지 않는다** — 시뮬에 대시 규칙이 없다(스펙 §1 "빼는 것"). UI에 대시 버튼
  자리도 만들지 않는다. 다음 슬라이스에서 규칙과 함께 붙인다.
- 주석 규약: 코드로 자명한 것은 적지 않고 **왜**만 짧게, 일상어로.

---

## 파일 구조

### GameFramework (앱 비종속 코어)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/World/Components/CapsuleShape.cs` (신규) | 엔티티 몸 캡슐 치수(반지름·전체높이) 데이터. Anemic — 생성자에서 값 유효성만 본다 |
| `Tests/World/CapsuleShapeTests.cs` (신규) | 값 왕복 + 잘못된 치수 거부 |

### LeagueOfPhysical-Shared (클·서 공통 시뮬)

| 파일 | 변경 |
|---|---|
| `Runtime/Scripts/Game/KinematicMoveSystem.cs` | 상수 `Radius`/`Height` 삭제 → 엔티티의 `CapsuleShape`를 읽는다 |
| `Runtime/Scripts/Game/FlappyWorld.cs` | `FlappyConfig` 생성자 인자 삭제 → sweep 치수를 엔티티의 `CapsuleShape`에서 읽는다 |
| `Runtime/Scripts/Game/BodySizes.cs` (신규) | FlapWang 캐릭터·아이템 몸 치수 상수 한 곳(클·서 크리에이터가 같은 값을 붙이도록) |
| `Tests/EditMode/FlappyWorldTests.cs` | `Bird()` 헬퍼가 `CapsuleShape`를 붙인다 + sweep이 그 값을 쓰는지 보는 테스트 추가 |
| `Tests/EditMode/FlappyWorldDeterminismTests.cs` | 생성자 인자 변경 반영 + `CapsuleShape` 부여 |
| `Tests/EditMode/KinematicMoveSystemTests.cs` / `KinematicMoveSystemGroundStateTests.cs` | 엔티티에 `CapsuleShape` 부여 |
| `Tests/EditMode/LOPWorldTests.cs` / `LOPWorldSaveLoadTests.cs` / `LOPWorldInputActivationTests.cs` | 이동이 도는 엔티티에 `CapsuleShape` 부여 |

### LeagueOfPhysical-Client

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Entity/PhysicsFollower.cs` | 캡슐 치수 하드코딩(0.35/1.5) 삭제 → 엔티티의 `CapsuleShape`를 쓴다. 없으면 즉시 예외 |
| `Assets/Scripts/Entity/CharacterCreator.cs` | `CapsuleShape(BodySizes.Character*)` 추가 |
| `Assets/Scripts/Entity/ItemCreator.cs` | `CapsuleShape(BodySizes.Item*)` 추가 |
| `Assets/Scripts/Entity/FlappyBirdCreator.cs` | `FlappyConfig` 주입 → `CapsuleShape` 추가 · 내 새에 **`Simulated`** 추가 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | `FlappyWorld` 생성자 인자 정리 + 플랩 UI·코디네이터 등록 |
| `Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs` (신규) | 플랩 커맨드 · Space 키 · 카메라 드래그 전달 |
| `Assets/Scripts/UI/FlapPad/FlapPadView.cs` (신규) | 전체화면 입력면 바인더(누르는 순간 플랩, 끌면 카메라) |
| `Assets/Scripts/Game/FlappyHudCoordinator.cs` (신규) | 내 새가 생기면 FlapPad·DebugHud를 연다 |
| `Assets/UI/FlapPad/FlapPad.uxml` · `FlapPad.uss` (신규) | 전체화면 입력면 + 안내 문구 |
| `Assets/UI/UIViewCatalog.asset` | `FlapPadView` 엔트리 추가 |

### LeagueOfPhysical-Server

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Entity/PhysicsFollower.cs` | 클라와 같은 변경(트리거 감지 로직은 그대로) |
| `Assets/Scripts/Entity/CharacterCreator.cs` · `ItemCreator.cs` · `FlappyBirdCreator.cs` | `CapsuleShape` 추가 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | `FlappyWorld` 생성자 인자 정리 |

---

## Task 1: `CapsuleShape` 컴포넌트 — 몸 치수를 엔티티가 갖는다

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Components/CapsuleShape.cs`
- Test: `GameFramework/Tests/World/CapsuleShapeTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Component`(기존 추상 클래스), `GameFramework.World.Entity`
- Produces: `GameFramework.World.CapsuleShape` — `CapsuleShape(float radius, float height)`,
  읽기 전용 프로퍼티 `float Radius`, `float Height`. 이후 모든 태스크가 이 타입을 쓴다.

- [ ] **Step 1: 브랜치를 판다 (4개 저장소 전부)**

각 저장소가 원격 main 최신인지 먼저 확인한다. 클라는 이미 `feature/flappy-b2d2-fly`에 있다(이 계획서가
그 브랜치에 있다).

```bash
cd ~/workspace/LOP
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Server; do
  git -C $r fetch origin
  git -C $r status --short          # 미커밋 확인 — 사용자 픽스처는 건드리지 않는다
  git -C $r switch -c feature/flappy-b2d2-fly
  git -C $r rev-list --left-right --count origin/main...HEAD   # 0	0 이어야 한다
done
```

그리고 **손대기 전 테스트 기준선을 잰다** — 앞으로 모든 스텝이 이 수와 비교한다.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client && unity cmd run_tests   # 에디터가 떠 있으면 이쪽 (예상 527)
cd ~/workspace/LOP/LeagueOfPhysical-Server && unity test            # 에디터가 없으면 배치모드 (예상 506)
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`GameFramework/Tests/World/CapsuleShapeTests.cs`:

```csharp
using NUnit.Framework;

namespace GameFramework.World.Tests
{
    public class CapsuleShapeTests
    {
        [Test]
        public void AttachesToEntityAndRoundTrips()
        {
            var entity = new Entity("e1");
            entity.Add(new CapsuleShape(0.45f, 0.9f));

            Assert.AreEqual(0.45f, entity.Get<CapsuleShape>().Radius, 1e-4f);
            Assert.AreEqual(0.9f, entity.Get<CapsuleShape>().Height, 1e-4f);
            Assert.AreSame(entity, entity.Get<CapsuleShape>().Owner);
        }

        [Test]
        public void RejectsNonPositiveSize()
        {
            // 0이나 음수 캡슐은 sweep이 아무것도 못 맞히는 몸이 된다 — 만들 때 막는다.
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CapsuleShape(0f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CapsuleShape(0.5f, -1f));
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 본다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd import_asset --path <새 파일 경로>    # 새 파일이면 .meta 생성
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status   # 에디터가 떠 있는 프로젝트
```

기대: `CapsuleShape` 타입이 없어 **컴파일 실패**(`CS0246: CapsuleShape을 찾을 수 없습니다`).

- [ ] **Step 4: 컴포넌트를 만든다**

`GameFramework/Runtime/Scripts/World/Components/CapsuleShape.cs`:

```csharp
using System;

namespace GameFramework.World
{
    /// <summary>
    /// 엔티티 몸의 캡슐 치수. 맵에 부딪히는지 보는 sweep과, 물리 엔진에 세우는 콜라이더가
    /// 이 하나를 같이 본다 — 두 곳이 다른 값을 들고 있으면 시뮬이 모르는 위치 보정이 매 틱 끼어든다.
    /// 값은 게임이 정한다(엔티티를 만드는 쪽이 붙인다).
    /// </summary>
    public class CapsuleShape : Component
    {
        public float Radius { get; }

        /// <summary>발밑부터 정수리까지 전체 높이.</summary>
        public float Height { get; }

        public CapsuleShape(float radius, float height)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "몸 반지름은 0보다 커야 한다.");
            }
            if (height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "몸 높이는 0보다 커야 한다.");
            }

            Radius = radius;
            Height = height;
        }
    }
}
```

- [ ] **Step 5: 테스트가 통과하는지 본다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건, 목록에 `CapsuleShapeTests` 2개가 새로 보인다(기준선 + 2).

- [ ] **Step 6: 테스트가 진짜 실패할 수 있는지 확인한다**

`CapsuleShape` 생성자의 `radius <= 0f` 검사를 잠시 지우고 테스트를 다시 돌려
`RejectsNonPositiveSize`가 **실패**하는지 본다. 확인했으면 되돌린다.

- [ ] **Step 7: 커밋**

```bash
cd ~/workspace/LOP/GameFramework
git add Runtime/Scripts/World/Components/CapsuleShape.cs \
        Runtime/Scripts/World/Components/CapsuleShape.cs.meta \
        Tests/World/CapsuleShapeTests.cs Tests/World/CapsuleShapeTests.cs.meta
git status --short
git commit -m "$(cat <<'EOF'
feat(world): 몸 캡슐 치수를 엔티티가 갖는다

지금까지 캡슐 치수는 물리 팔로워·이동 커널·Flappy 튜닝값 세 곳에 따로 있었고,
값이 어긋나면 시뮬이 모르는 위치 보정이 매 틱 끼어든다. 치수를 엔티티 데이터로 옮겨
읽는 쪽이 하나를 보게 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 시뮬이 엔티티의 몸을 쓴다 (LOP-Shared)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs:15-16,29-41`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldTests.cs`
- Test(수정): `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldDeterminismTests.cs`,
  `KinematicMoveSystemTests.cs`, `KinematicMoveSystemGroundStateTests.cs`,
  `LOPWorldTests.cs`, `LOPWorldSaveLoadTests.cs`, `LOPWorldInputActivationTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.CapsuleShape`(Task 1)
- Produces: `FlappyWorld` 생성자에서 **`FlappyConfig config` 인자가 사라진다** →
  `FlappyWorld(EntityRegistry, WorldEventBuffer, FlappyMoveSystem, FlappyBodyCollisionSystem,
  ICollisionQuery, IMotionBridge, int layerMask)`. Task 4·5의 스코프 등록이 이 시그니처를 쓴다.

> 새끼리 몸싸움(`FlappyBodyCollisionSystem`)은 계속 `FlappyConfig`를 쓴다. 짝별로 다른 치수를 받으려면
> 테스트가 붙은 순수함수 `FlappyBodyOverlap.TryCompute`의 시그니처를 바꿔야 하는데, 새 치수의 출처가
> 어차피 같은 `TbFlappyConfig` 한 행이라 어긋날 수 없다. 이번엔 건드리지 않는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`FlappyWorldTests.cs`의 `Bird()` 헬퍼를 고쳐 몸을 붙이고(치수를 인자로 받게), sweep이 **엔티티의**
치수를 쓰는지 보는 테스트를 추가한다. 기존 `Bird` 호출부는 기본값으로 그대로 돈다.

```csharp
        static Entity Bird(string id, Vector3 position, bool simulated, float radius = 0.45f, float height = 0.9f)
        {
            var entity = new Entity(id);
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new Velocity());
            entity.Add(new CapsuleShape(radius, height));
            if (simulated)
            {
                entity.Add(new Simulated());
            }
            return entity;
        }

        static FlappyWorld World(EntityRegistry registry, GameFramework.World.IMotionBridge bridge)
            => new FlappyWorld(registry, new WorldEventBuffer(),
                               new FlappyMoveSystem(Config()),
                               new FlappyBodyCollisionSystem(Config()),
                               new EmptySkyQuery(), bridge, layerMask: ~0);
```

그리고 새 테스트 두 개:

```csharp
        [Test]
        public void 맵_sweep은_엔티티가_들고_있는_몸_치수를_쓴다()
        {
            var registry = new EntityRegistry();
            // 튜닝값(0.45)과 일부러 다른 몸을 준다 — 어느 쪽을 읽는지 구분되는 값이어야 한다.
            var bird = Bird("bird-1", Vector3.zero, simulated: true, radius: 0.2f, height: 0.4f);
            registry.Add(bird);

            var wallQuery = new WallAheadQuery(hitDistance: 0.5f, normal: Vector3.left);
            var world = new FlappyWorld(registry, new WorldEventBuffer(),
                                        new FlappyMoveSystem(Config()),
                                        new FlappyBodyCollisionSystem(Config()),
                                        wallQuery, new NoopMotionBridge(), layerMask: ~0);

            world.Tick(1, 0.1f);

            Assert.AreEqual(0.2f, wallQuery.LastRadius, Tolerance);
        }

        [Test]
        public void 몸이_없는_엔티티는_맵_이동을_하지_않는다()
        {
            var registry = new EntityRegistry();
            var noBody = new Entity("bird-1");
            noBody.Add(new GameFramework.World.Transform());
            noBody.Add(new Velocity());
            noBody.Add(new Simulated());
            registry.Add(noBody);

            // 속도는 정해지지만(전진·중력) 위치를 옮기는 단계는 몸 없이는 돌 수 없다.
            World(registry, new NoopMotionBridge()).Tick(1, 0.1f);

            Assert.AreEqual(Vector3.zero, PositionOf(noBody));
        }
```

- [ ] **Step 2: 테스트가 실패하는지 본다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: **컴파일 실패** — `FlappyWorld` 생성자가 아직 `FlappyConfig`를 요구한다(`CS1729`).

- [ ] **Step 3: `FlappyWorld`가 엔티티 몸을 읽게 고친다**

생성자에서 `FlappyConfig config` 인자와 `_config` 필드를 지우고, `MoveThroughMap`을 이렇게 바꾼다:

```csharp
        private void MoveThroughMap(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var body = entity.Get<GameFramework.World.CapsuleShape>();
            if (transform == null || velocity == null || body == null)
            {
                return;
            }

            _motionBridge.Depenetrate(entity);

            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                body.Radius, body.Height, deltaTime, _layerMask), _collisionQuery);

            transform.Position = result.position.ToNumerics();
            velocity.Linear = result.velocity.ToNumerics();

            _motionBridge.PushMotion(entity);
        }
```

- [ ] **Step 4: `KinematicMoveSystem`도 같게 고친다**

상수 `Radius`/`Height`를 지우고(중력 상수 `Gravity`는 남긴다) `Tick`을:

```csharp
        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var body = entity.Get<GameFramework.World.CapsuleShape>();
            if (transform == null || velocity == null || body == null)
            {
                return;
            }

            Vector3 vel = velocity.Linear.ToUnity();
            vel.y += Gravity * deltaTime;   // 중력 = 분리된 수직 스텝(컨트롤러 레이어). mover는 이걸 모름.

            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), vel, body.Radius, body.Height, deltaTime, _layerMask), _query);

            transform.Position = result.position.ToNumerics();
            velocity.Linear = result.velocity.ToNumerics();

            var groundState = entity.Get<GameFramework.World.GroundState>();
            if (groundState != null)
            {
                groundState.IsGrounded = result.grounded;
            }
        }
```

- [ ] **Step 5: 몸 치수 상수를 한 곳에 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/BodySizes.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 몸 캡슐 치수. 클·서 크리에이터가 같은 값을 붙여야 예측이 서버 권위와 갈리지 않으므로 한 곳에 둔다.
    /// (Flappy의 새는 이 상수가 아니라 마스터데이터 <c>TbFlappyConfig</c> 값을 쓴다.)
    /// </summary>
    public static class BodySizes
    {
        public const float CharacterRadius = 0.35f;
        public const float CharacterHeight = 1.5f;

        /// <summary>바닥에 놓인 아이템 — 이 캡슐이 곧 줍기 판정 범위다(트리거).</summary>
        public const float ItemRadius = 0.35f;
        public const float ItemHeight = 1.5f;
    }
}
```

- [ ] **Step 6: 이동이 도는 테스트 엔티티에 몸을 붙인다**

몸이 없으면 이제 이동 단계가 통째로 건너뛰어지므로, 기존 테스트가 만드는 엔티티에 `CapsuleShape`를
추가한다. 대상과 값:

| 파일 | 어디 | 넣을 것 |
|---|---|---|
| `KinematicMoveSystemTests.cs` | 엔티티 생성 헬퍼(26~27줄 근처) | `e.Add(new GameFramework.World.CapsuleShape(0.35f, 1.5f));` |
| `KinematicMoveSystemGroundStateTests.cs` | `MakeCharacter()`와 67줄 근처 엔티티 | 같은 줄 |
| `LOPWorldTests.cs` · `LOPWorldSaveLoadTests.cs` · `LOPWorldInputActivationTests.cs` | `Simulated`를 붙이는 엔티티마다 | `entity.Add(new CapsuleShape(0.35f, 1.5f));` |
| `FlappyWorldDeterminismTests.cs` | 새 생성 + `FlappyWorld` 생성자 호출 | `CapsuleShape(0.45f, 0.9f)` 추가, 생성자에서 `Config()` 인자 제거 |

> `LOPWorld*` 테스트 중 이동 결과를 단언하지 않는 것(브릿지 호출 횟수만 세는 것)은 몸이 없어도
> 통과할 수 있다. 그래도 붙인다 — "이동이 도는 엔티티는 몸을 갖는다"가 이제 규칙이고, 테스트가
> 실제 엔티티와 다른 모양이면 다음 사람이 헷갈린다.

- [ ] **Step 7: 클·서 스코프의 `FlappyWorld` 생성 호출을 고친다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`와
`LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` 양쪽에서
`c.Resolve<FlappyConfig>(),` 줄을 **`FlappyWorld` 생성자 인자에서만** 지운다
(`FlappyMoveSystem`/`FlappyBodyCollisionSystem`은 여전히 `FlappyConfig`를 받으므로 등록 자체는 남긴다).

```csharp
            builder.Register<GameFramework.World.IWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyBodyCollisionSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton);
```

> 이 두 파일은 **Task 3에서 함께 커밋한다**(클·서 저장소의 다른 변경과 한 덩어리다). Task 2의 커밋에는
> LOP-Shared 것만 들어간다 — 그래서 이 태스크가 끝나도 클·서 워킹트리에는 이 수정이 남아 있다.

- [ ] **Step 8: 테스트가 통과하는지 본다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status
unity cmd run_tests && unity cmd test_status
```

기대: 실패 0건(기준선 + 4). 컴파일 에러가 남아 있으면 Task 3에서 고칠 크리에이터·팔로워
때문일 수 있으니, `PhysicsFollower`/크리에이터 관련 에러라면 Task 3까지 이어서 하고 여기서 커밋하지
않는다. 그 외 에러는 여기서 고친다.

- [ ] **Step 9: 테스트가 진짜 실패할 수 있는지 확인한다**

`FlappyWorld.MoveThroughMap`에서 `body.Radius`를 `0.45f`로 잠깐 되돌려 놓고 테스트를 다시 돌려
`맵_sweep은_엔티티가_들고_있는_몸_치수를_쓴다`가 **실패**하는지 본다. 확인했으면 되돌린다.

- [ ] **Step 10: 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/FlappyWorld.cs Runtime/Scripts/Game/KinematicMoveSystem.cs \
        Runtime/Scripts/Game/BodySizes.cs Runtime/Scripts/Game/BodySizes.cs.meta \
        Tests/EditMode/
git status --short
git commit -m "$(cat <<'EOF'
refactor(sim): 시뮬이 엔티티의 몸 치수를 읽는다

이동 커널과 Flappy 월드가 각자 상수·튜닝값으로 들고 있던 캡슐 치수를 엔티티의
CapsuleShape에서 읽게 바꾼다. 물리 팔로워가 세우는 몸과 같은 값을 보게 하는 것이 목적이다.

FlappyWorld는 더 이상 FlappyConfig를 받지 않는다 — 남은 쓰임이 없다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 크리에이터가 몸을 붙이고, 물리 팔로워가 그 몸을 세운다 (클·서)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/PhysicsFollower.cs:38-43`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/PhysicsFollower.cs` (같은 자리)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/CharacterCreator.cs`,
  `ItemCreator.cs`, `FlappyBirdCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/CharacterCreator.cs`,
  `ItemCreator.cs`, `FlappyBirdCreator.cs`

**Interfaces:**
- Consumes: `GameFramework.World.CapsuleShape`(Task 1), `LOP.BodySizes`(Task 2), `LOP.FlappyConfig`
- Produces: 월드에 등록되는 **모든** 엔티티가 `CapsuleShape`를 갖는다는 불변식. 없으면
  `PhysicsFollower.Initialize`가 `InvalidOperationException`으로 즉시 실패한다.

> **왜 폴백이 아니라 예외인가**: 조용히 기본값으로 굴러가면 "두 곳이 다른 값"이라는 지금 고치는
> 문제가 그대로 재현된다. 팔로워는 엔티티 생성 직후(`EntityBinder`, 동기 발행) 한 번 도므로,
> 빠뜨린 크리에이터는 스폰 즉시 크게 드러난다.

- [ ] **Step 1: 클라 `PhysicsFollower`를 고친다**

`Initialize` 안 캡슐 만드는 부분:

```csharp
            var body = worldEntity.Get<GameFramework.World.CapsuleShape>();
            if (body == null)
            {
                // 몸 치수는 엔티티를 만드는 쪽(게임)이 정한다 — 여기서 기본값을 지어내면
                // 시뮬이 쓰는 몸과 다시 어긋난다.
                throw new System.InvalidOperationException(
                    $"[PhysicsFollower] {worldEntity.Id}에 CapsuleShape이 없다 — 크리에이터가 붙여야 한다.");
            }

            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = body.Radius;
            capsuleCollider.height = body.Height;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
```

- [ ] **Step 2: 서버 `PhysicsFollower`에 같은 변경을 한다**

서버 파일은 트리거 감지(`TriggerDetector` 배선)가 뒤에 더 있다 — 그 부분은 그대로 두고 캡슐
만드는 부분만 위와 똑같이 바꾼다.

- [ ] **Step 3: 여섯 크리에이터가 몸을 붙이게 한다**

`CharacterCreator`(클·서) — `MotionContributions` 추가 줄 옆에:

```csharp
            worldEntity.Add(new GameFramework.World.CapsuleShape(
                BodySizes.CharacterRadius, BodySizes.CharacterHeight));
```

`ItemCreator`(클·서) — `Appearance` 추가 줄 다음에:

```csharp
            worldEntity.Add(new GameFramework.World.CapsuleShape(
                BodySizes.ItemRadius, BodySizes.ItemHeight));
```

`FlappyBirdCreator`(클·서) — 생성자에 `FlappyConfig`를 주입받아(필드 `private readonly FlappyConfig config;`)
`MotionContributions` 옆에:

```csharp
            // 새 몸은 시뮬이 쓰는 그 값(TbFlappyConfig)에서 온다 — 물리 팔로워가 다른 몸을 세우면
            // 겹침 밀어내기가 시뮬이 모르는 위치 점프를 만든다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight));
```

클라 `FlappyBirdCreator` 생성자는 이렇게 된다:

```csharp
        public FlappyBirdCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            FlappyConfig config)
```

서버 쪽은:

```csharp
        public FlappyBirdCreator(GameFramework.World.EntityRegistry entityRegistry, FlappyConfig config)
```

> DI 등록은 손댈 것이 없다 — `FlappyConfig`는 이미 양쪽 `FlappyRaceLifetimeScope`에 싱글턴으로
> 등록돼 있고, `FlappyBirdCreator`도 같은 스코프에서 만들어진다.

- [ ] **Step 4: 클·서 컴파일과 테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client && unity cmd recompile && unity cmd recompile_status && unity cmd run_tests
# 서버는 에디터가 안 떠 있으면 `unity cmd`가 조용히 클라로 라우팅된다 — 배치모드로 돈다
cd ~/workspace/LOP/LeagueOfPhysical-Server && unity test
```

기대: 양쪽 다 **실패 0건**. 개수는 기준선 + 이번에 넣은 테스트 수만큼 늘어 있어야 한다
(클라는 Task 1의 2개 + Task 2의 2개, 서버도 패키지 테스트를 함께 돌리므로 같은 4개가 더해진다 —
실제 수는 Task 1 Step 1에서 재 둔 기준선과 대조해 확인한다).

> 크리에이터·팔로워에는 EditMode 테스트를 붙이지 않는다 — 둘 다 Unity `GameObject`/DI에 묶여 있어
> 단위 테스트로 감싸려면 프로덕션 코드를 테스트용으로 비트는 편이 더 크다. 이 태스크의 검증은
> **컴파일 + Task 6·7의 런타임 관찰**이다(스폰이 되면 몸이 붙은 것이고, 안 되면 예외가 즉시 터진다).

- [ ] **Step 5: 커밋 (클·서 각각)**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/PhysicsFollower.cs Assets/Scripts/Entity/CharacterCreator.cs \
        Assets/Scripts/Entity/ItemCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short
git commit -m "$(cat <<'EOF'
fix(entity): 물리 팔로워가 엔티티의 몸 치수로 캡슐을 세운다

팔로워가 FlapWang 치수(0.35/1.5)를 박아 두고 있어서, 새(0.45/0.9)는 시뮬보다 0.6m 높은
몸으로 겹침 검사를 받았다. 맵 콜라이더가 솔리드가 된 B2-d1 이후로는 이게 매 틱
시뮬이 모르는 위치 점프를 만든다. 치수는 이제 크리에이터가 엔티티에 붙인다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

서버도 같은 방식으로(`Assets/Scripts/Entity/*.cs`, `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`) 커밋한다.

---

## Task 4: 클라가 자기 새를 시뮬한다 (`Simulated`)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/FlappyBirdCreator.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Simulated`
- Produces: 클라 월드에서 **내 새만** `Simulated`. 이후 `FlappyWorld.Mutation`이 내 새를 매 틱 굴리고,
  `WorldBase.SaveState`가 그 위치·속도를 틱마다 보관하며, `Reconciler`의 오차 게이트가 동작한다.

> 스펙 §6-2가 함께 결정하라고 남긴 *"시뮬하지 않는 엔티티를 되감기에서 뺄 것인가"* 는 **따로 할 일이
> 없다.** `Reconciler`는 `playerContext.entityId`(내 새) 하나만 본다. 지금 매 스냅 하드 보정이 도는 건
> `Simulated`가 없어 `world.TryGetSavedMotion`이 실패하고 오차 게이트 블록을 통째로 건너뛰기
> 때문이다. 남의 새는 애초에 `Reconciler`를 타지 않고 `RemoteEntityInterpolator`(스냅샷 보간)로 간다.

- [ ] **Step 1: 한 줄 추가**

클라 `FlappyBirdCreator.Create`의 `isUserEntity` 블록:

```csharp
            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 내 새만 예측한다 — 남의 새는 서버 스냅샷 보간에 맡긴다(원격 표준).
                worldEntity.Add(new InputBuffer());
                worldEntity.Add(new GameFramework.World.Simulated());
            }
```

- [ ] **Step 2: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status && unity cmd run_tests
```

기대: 실패 0건, 개수 변화 없음(크리에이터는 테스트 밖이다).

- [ ] **Step 3: 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/FlappyBirdCreator.cs
git commit -m "$(cat <<'EOF'
feat(flappy): 클라가 자기 새를 시뮬한다

내 새에 Simulated을 붙여 예측·상태 보관 대상으로 만든다. 이게 없어서 되감기 기록이
아예 남지 않았고, 그 결과 되감기 통계의 Average=0이 "예측이 완벽"이 아니라
"기록이 없음"을 뜻하고 있었다(B2-c 결과).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: 플랩을 누를 화면 (`FlapPadView`)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Create: `LeagueOfPhysical-Client/Assets/UI/FlapPad/FlapPad.uxml`, `FlapPad.uss`
- Modify: `LeagueOfPhysical-Client/Assets/UI/UIViewCatalog.asset`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `PlayerInputManager.SetJump(bool)`(공통 등록, 이미 있음), `CameraController.ProcessTouchInput(Vector2)`,
  `IWindowManager.Open<T>()` / `RegisterViewFactory<T>()`, `LOP.UI.UIView`, `LOP.UI.UILayer`
- Produces: `LOP.UI.FlapPadView`(카탈로그 키 이름도 `FlapPadView`), `LOP.UI.FlapPadViewModel`,
  `LOP.FlappyHudCoordinator`

> **손맛 결정**: 플랩은 **누르는 순간**(`PointerDownEvent`) 나간다. 떼는 걸 기다리면 그만큼 늦게 뜬다.
> 그래서 카메라를 돌리려고 끌면 그 시작점에서 플랩이 한 번 같이 나간다 — 자리싸움 게임이라 손해가
> 크지 않다고 보고 단순한 쪽을 택했다. 거슬리면 "일정 거리 이상 끌면 그 플랩을 취소"를 나중에 넣는다.

- [ ] **Step 1: ViewModel을 만든다**

`Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면 ViewModel. 화면을 누르면 날갯짓, 끌면 카메라가 돈다.
    /// 표시할 라이브 상태가 없는 입력 전용 화면이라 R3 없이 커맨드 타깃 역할만 한다(GamePadViewModel과 같은 짝).
    /// </summary>
    public class FlapPadViewModel
    {
        private readonly PlayerInputManager _playerInputManager;
        private readonly CameraController _cameraController;

        public FlapPadViewModel(PlayerInputManager playerInputManager, CameraController cameraController)
        {
            _playerInputManager = playerInputManager;
            _cameraController = cameraController;
        }

        /// <summary>날갯짓. 와이어에는 기존 Jump 입력으로 실린다 — 서버 입력 버퍼는 그대로 쓴다.</summary>
        public void Flap() => _playerInputManager.SetJump(true);

        /// <summary>데스크톱 편의: Space. View가 매 프레임 부른다.</summary>
        public void PollKeyboard()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Flap();
            }
        }

        public void CameraLook(Vector2 delta) => _cameraController.ProcessTouchInput(delta);
    }
}
```

- [ ] **Step 2: View를 만든다**

`Assets/Scripts/UI/FlapPad/FlapPadView.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면. 화면 전체가 입력면이고, 누르는 순간 날갯짓이 나간다
    /// (떼는 걸 기다리면 그만큼 늦게 뜬다). 같은 손가락을 끌면 카메라가 돈다.
    /// ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class FlapPadView : UIView
    {
        private readonly FlapPadViewModel _viewModel;

        private VisualElement _surface;
        private IVisualElementScheduledItem _tick;

        private int _pointerId = -1;
        private Vector2 _lastPosition;   // panel 좌표

        public FlapPadView(FlapPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _surface = Root.Q<VisualElement>("flap-surface");
            _surface.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _surface.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _surface.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 키보드를 본다.
            _tick = Root.schedule.Execute(_ => _viewModel.PollKeyboard()).Every(0);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_pointerId != -1)
            {
                return;   // 이미 다른 손가락이 잡고 있다
            }

            _pointerId = evt.pointerId;
            _surface.CapturePointer(evt.pointerId);
            _lastPosition = (Vector2)evt.position;

            _viewModel.Flap();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            Vector2 current = (Vector2)evt.position;
            Vector2 delta = current - _lastPosition;
            _lastPosition = current;

            // panel Y는 아래로 증가 — 카메라 쪽 부호(위로 증가)에 맞춰 뒤집는다.
            _viewModel.CameraLook(new Vector2(delta.x, -delta.y));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            _surface.ReleasePointer(evt.pointerId);
            _pointerId = -1;
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                }
            }

            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 3: UXML/USS를 만든다**

`Assets/UI/FlapPad/FlapPad.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="FlapPad.uss" />
    <ui:VisualElement name="flap-root" class="flap-root" picking-mode="Ignore">
        <ui:VisualElement name="flap-surface" class="flap-surface" />
        <ui:Label name="flap-hint" class="flap-hint" text="화면을 누르면 날갯짓 (Space)" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/FlapPad/FlapPad.uss`:

```css
.flap-root {
    flex-grow: 1;
}

/* 화면 전체가 입력면 — 위 밴드(팝업 등)가 먼저 입력을 가져간다 */
.flap-surface {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
}

.flap-hint {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 40px;
    -unity-text-align: middle-center;
    font-size: 28px;
    color: rgba(255, 255, 255, 0.55);
}
```

- [ ] **Step 4: 유니티에 임포트해 `.meta`를 만들고 GUID를 읽는다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd import_asset --path Assets/UI/FlapPad/FlapPad.uxml
unity cmd import_asset --path Assets/UI/FlapPad/FlapPad.uss
sleep 5
grep guid Assets/UI/FlapPad/FlapPad.uxml.meta Assets/UI/FlapPad/FlapPad.uss.meta
```

- [ ] **Step 5: 카탈로그에 엔트리를 추가한다**

`Assets/UI/UIViewCatalog.asset`의 `entries:` 목록 끝에 붙인다. `fileID`는 이 카탈로그의 모든 엔트리가
공유하는 상수(uxml `9197481963319205126`, uss `7433441132597879392`)이고, `guid`만 Step 4에서 읽은
값으로 바꾼다.

```yaml
  - viewName: FlapPadView
    uxml: {fileID: 9197481963319205126, guid: <FlapPad.uxml GUID>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <FlapPad.uss GUID>, type: 3}
```

- [ ] **Step 6: 코디네이터를 만든다**

`Assets/Scripts/Game/FlappyHudCoordinator.cs`:

```csharp
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 내 새가 생기면 Flappy 인게임 화면(입력면 + 디버그 HUD)을 연다.
    /// 엔티티 생성과 화면 띄우기를 분리한다 — 화면 교체는 "큰 흐름"이라 코디네이터 책임
    /// (아키텍처 가이드라인 "흐름의 경계"). FlapWang의 <see cref="PlayerHudCoordinator"/>와 같은 짝이다.
    /// </summary>
    public class FlappyHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public FlappyHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 입력면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            windowManager.Open<FlapPadView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
    }
}
```

- [ ] **Step 7: 스코프에 등록한다**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` — `using`에 `LOP.UI`, `System`,
`System.Collections.Generic`을 더하고, `ConfigureGame` 끝에:

```csharp
            builder.RegisterEntryPoint<FlappyHudCoordinator>();
            builder.Register<FlapPadViewModel>(Lifetime.Transient);
            builder.Register<FlapPadView>(Lifetime.Transient);
```

그리고 클래스에 팩토리 등록을 추가한다:

```csharp
        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<FlapPadView>(() => container.Resolve<FlapPadView>()));
        }
```

- [ ] **Step 8: 컴파일·테스트**

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd ~/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile && unity cmd recompile_status && unity cmd run_tests
```

기대: 실패 0건, 개수 변화 없음.

> 이 태스크에는 EditMode 테스트를 새로 붙이지 않는다. `FlapPadViewModel`이 하는 일은 호출 두 개를
> 그대로 넘기는 것뿐이고, 그걸 감싸려면 `PlayerInputManager`(생성자에서 러너에 자기를 등록하는 구체
> 클래스)와 `CameraController`(MonoBehaviour)에 테스트용 이음매를 새로 파야 한다 — 지금 얻는 것보다
> 비용이 크다. 이 화면의 검증은 Task 6의 런타임 관찰이다.

- [ ] **Step 9: 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/UI/FlapPad Assets/Scripts/Game/FlappyHudCoordinator.cs \
        Assets/Scripts/Game/FlappyHudCoordinator.cs.meta Assets/UI/FlapPad \
        Assets/UI/UIViewCatalog.asset Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short     # .meta가 빠지지 않았는지 확인
git commit -m "$(cat <<'EOF'
feat(flappy): 날갯짓을 누를 화면을 만든다

지금까지 PlayerInputManager에 입력을 넣어 주는 것은 FlapWang 게임패드뿐이라
Flappy에서는 사람이 플랩을 시킬 방법 자체가 없었다. 화면 전체가 입력면인
작은 화면을 새로 만들고, 누르는 순간 기존 Jump 입력으로 내보낸다.
와이어·서버 입력 버퍼는 그대로다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: 런타임 검증 — 혼자 날기

**Files:** 없음(관찰). 고칠 것이 나오면 해당 태스크로 돌아간다.

**Interfaces:**
- Consumes: Task 3·4·5의 결과 전부 + 로컬 kind 클러스터에 배포된 서버 이미지

- [ ] **Step 1: 서버를 배포한다**

서버 코드(크리에이터·팔로워·스코프)가 바뀌었으므로 콘텐츠만으로는 안 되고 **게임서버 이미지**를
새로 굽는다. 이 CI는 이 맥의 self-hosted 러너에서 도니 **유니티 에디터를 닫고** 돌리는 편이 안전하다
(Burst AOT 단계가 에디터와 라이선스를 다툰다).

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy
gh run watch
```

성공하면 infrastructure의 `GAME_SERVER_IMAGE`가 자동 bump되고 ArgoCD가 롤아웃한다.
**직접 `kubectl apply`하거나 파드를 지우지 않는다.**

- [ ] **Step 2: 클라를 띄워 방에 들어간다**

에디터 Play → 로그인 → 로비에서 **Flappy Race** 선택 → 매칭(플래피는 `MinPlayers=1`이라 혼자 잡힌다).

접속이 `Connection refused`면 **도커의 UDP 7000 바인딩**을 먼저 의심한다(이 프로젝트에서 두 번
발생). `lsof -nP -iUDP:7000`이 0줄이면 `docker restart lop-control-plane`.

- [ ] **Step 3: 나는지 본다**

확인할 것:

| 확인 | 기대 |
|---|---|
| 화면을 누르거나 Space | 새가 위로 뜬다(`FlapImpulse`만큼) |
| 안 누르면 | 중력으로 떨어지고 `MaxFallSpeed`에서 낙하가 멈춘다 |
| 파이프 사이 | **틈으로 지나간다** — B2-d1이 못 본 항목 |
| 파이프·바닥에 부딪히면 | 뚫고 가지 않고 막혀 미끄러진다 |
| 콘솔 | `CapsuleShape이 없다` 예외 0건, NRE 0건 |

- [ ] **Step 4: 예측이 실제로 도는지 숫자로 본다**

DebugHud의 reconciliation 값을 본다. **`CorrectionCount`가 오르는데 `Average=0`이면 안 된다** —
그건 `Simulated`가 아직 없어 기록이 안 남던 옛 증상이다(B2-c 결과). `Average`가 0이 아닌 작은 값이면
예측이 돌고 있다는 뜻이다.

- [ ] **Step 5: FlapWang 회귀를 본다**

로비로 나가 **FlapWang**으로 한 판 들어가서, 걷기·점프·대시가 전과 같은지 본다. 캡슐 치수는
`BodySizes`로 같은 값을 넘겼으므로 달라질 이유가 없다 — 달라 보이면 Task 3에서 값을 잘못 넘긴 것이다.

- [ ] **Step 6: 관찰한 것을 적어 둔다**

이 단계에서 본 것(위치·속도 수치, 콘솔 로그, 안 되던 것)을 Task 8의 스펙 결과 절에 쓸 수 있게
메모해 둔다. 안 된 것을 "잘 됨"으로 적지 않는다.

---

## Task 7: 런타임 검증 — 2인 몸싸움

**Files:** 없음(관찰)

- [ ] **Step 1: 두 번째 클라를 띄운다**

MPPM 가상 플레이어를 쓴다(유니티 6.3 에디터 내장). 알려진 함정:

- **가상 플레이어는 메인 에디터와 다른 환경 설정을 들고 있을 수 있다.** 두 인스턴스 콘솔에서
  `[LOP] environment=… lobby=…` 줄이 같은지 확인하고, 다르면 **클론을 재시작**한다.
  다르면 서로 다른 백엔드에 붙어 매칭이 아예 안 잡힌다.
- 가상 플레이어는 CLI로 조작할 수 없다 — 큐잉을 자동으로 하려면 임시 스크립트가 필요하다
  (B2-b 검증 때 그렇게 했다).
- **검증 중 매치 파드를 지우지 않는다.** 클라는 같은 방으로 재입장하므로 서버가 사라지면 못 붙는다.

- [ ] **Step 2: 둘이 부딪히게 한다**

두 새를 같은 높이로 겹치게 몰아 확인할 것:

| 확인 | 기대 |
|---|---|
| 겹칠 때 | 서로 통과하지 않고 밀려난다 |
| 위아래로 부딪힐 때 | 세로 속도를 주고받는다(하나가 눌리고 하나가 뜬다) |
| 밀린 뒤 | 양쪽 화면에서 위치가 크게 어긋난 채 남지 않는다(스냅 보정이 따라잡는다) |

- [ ] **Step 3: 관찰한 것을 적어 둔다**

기대와 다른 것은 그대로 적는다. 몸싸움 규칙 자체(B2-b)의 문제인지, 이번 슬라이스가 만든 문제인지
가릴 수 없으면 "가릴 수 없었다"고 적는다.

---

## Task 8: 스펙에 결과를 남기고 네 저장소를 머지한다

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md`
  (§6-2에 "결과" 절 추가, §2 슬라이스 표의 B2-d2에 완료 표시)

- [ ] **Step 1: 스펙에 결과를 쓴다**

B2-d1 결과 절(§6 "결과 (2026-08-23, 완료)")과 같은 모양으로 §6-2 끝에 붙인다. 담을 것:

- 네 저장소의 머지 커밋 해시와 각각 담긴 것
- 씬/코드가 실제로 어떻게 바뀌었는지(전·후 표)
- 런타임에서 **관측된 것**(Task 6·7) — 특히 "틈을 통과한다"가 이번에 채점됐는지
- **검증의 한계** — 못 본 것, 재현 못 한 것, 가리지 못한 것
- 실행하며 배운 것(다음 사람을 위해)

- [ ] **Step 2: 계획서의 체크박스를 실제 진행대로 맞춘다**

계획과 다르게 한 것이 있으면 계획서에도 반영한다(다음에 읽는 사람이 실제 이력을 보게).

- [ ] **Step 3: 커밋**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git add docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md \
        docs/superpowers/plans/2026-08-23-flappy-b2d2-fly.md
git commit -m "$(cat <<'EOF'
docs(spec): B2-d2 결과와 검증의 한계를 남긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: GameFramework를 머지·푸시한다**

`CLAUDE.md`의 푸시 규약 그대로, **한 줄씩 결과를 확인하며**:

```bash
cd ~/workspace/LOP/GameFramework
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-b2d2-fly
git push origin main
```

- [ ] **Step 5: LOP-Shared를 같은 순서로 머지·푸시한다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-b2d2-fly
git push origin main
```

- [ ] **Step 6: Client를 머지·푸시한다**

유니티 레포다 — 리베이스 전에 로컬 픽스처를 빼 둔다.

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Client
git status --short
git stash push -u -m "b2d2-local-fixtures"    # 사용자 픽스처(폰트·ProjectSettings·아트 포인터)
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-b2d2-fly
git push origin main
git stash pop
```

- [ ] **Step 7: Server를 머지·푸시한다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Server
git status --short
git stash push -u -m "b2d2-local-fixtures"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-b2d2-fly
git push origin main
git stash pop
```

- [ ] **Step 8: 머지된 코드로 마지막 배포·확인**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy
gh run watch
```

배포된 이미지로 Task 6의 확인표를 한 번 더 빠르게 훑는다(머지 과정에서 뭔가 빠지지 않았는지).

---

## 자체 점검 (계획을 쓴 뒤 스펙과 대조)

| 스펙 §6-2 항목 | 담긴 태스크 |
|---|---|
| 몸 규격을 한 곳으로 (Finding E, 첫 항목) | Task 1·2·3 |
| 클·서 `FlappyBirdCreator`에 `Simulated`(클라=내 새만) | Task 4 (서버는 이미 있음 — 확인함) |
| 시뮬하지 않는 엔티티를 되감기에서 뺄 것인가 | Task 4 서두 — **따로 할 일 없음**(근거 기재) |
| 플랩을 누를 수단 (전용 UI 신설) | Task 5 |
| 런타임 검증: 날고 파이프에 막히는지 | Task 6 |
| 런타임 검증: MPPM 2인 몸싸움 | Task 7 |
| 스펙 §7 "나는 감각·충돌" | Task 6 Step 3 |

**범위 밖(이번에 하지 않는 것)**: 대시(시뮬 규칙 없음, 스펙 §1 "빼는 것") · 결승선·순위 판정(슬라이스 D)
· `FlappyBodyOverlap`을 짝별 치수로 확장 · 카메라를 Flappy 전용 시점으로 바꾸기.
