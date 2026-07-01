# Motion 권위 → World.Entity (파사드) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 캐릭터/아이템 모션(position/rotation/velocity)의 진실원본을 클라 `LOPEntity` 필드 → 순수 C# `World.Entity`(Transform/Velocity)로 이전. `LOPEntity.position` 등 바깥 API는 유지하고 *저장소만* World 컴포넌트로 위임(파사드). 동작 보존. Stage④(예측/롤백/4e)의 keystone.

**Architecture:** GameFramework에 `ToUnity`(Numerics→Unity) 역변환 추가 → `LOPEntity`가 캐시한 `World.Transform`/`World.Velocity`에 접근자 위임 + `LinkWorldMotion()` → `CharacterCreator`/`ItemCreator`가 World.Entity의 Transform/Velocity를 *먼저* 만들어 link한 뒤 Initialize → 수동 미러 `WorldMotionSync` 삭제. 서버 무변경(자기 Rigidbody 권위 + 자기 WorldMotionSync 유지).

**Tech Stack:** Unity, C#, VContainer, System.Numerics↔UnityEngine 변환, UnityMCP(컴파일 검증). 2 git repo(GameFramework / LeagueOfPhysical-Client).

**Spec:** `docs/superpowers/specs/2026-07-01-stage4-motion-world-authority-design.md`

---

## 작업 규약 (모든 Task 공통)

- **동작 보존 슬라이스(의도적 무테스트):** `LOPEntity`/creator/`EntityBinder`는 클라 Assembly-CSharp라 asmdef 단위테스트 불가. GameFramework `ToUnity`는 trivial 래핑. 검증 = **컴파일 클린 + 플레이 동작 동일**(이동/회전/점프/대시/아이템/reconciliation). 가짜 테스트 만들지 말 것. 페이오프는 Stage④.
- **UnityMCP 인스턴스 타게팅(필수):** 모든 UnityMCP 호출에 `unity_instance` 명시(**클라만**). `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client`의 full id(`Name@hash`) 확인 후 사용(작성 시점 `LeagueOfPhysical-Client@de70658b9450cbb4`, 해시 변할 수 있음). **서버 인스턴스는 건드리지 않는다.**
- **.meta:** 새 파일 없음(ToUnity는 기존 파일에 추가). 삭제되는 `WorldMotionSync.cs`는 `.cs`+`.meta` 함께 `git rm`.
- **Git(LOP 패턴):** 각 repo 피처 브랜치 `feature/stage4-motion-world-authority` → main `--no-ff`. main 직접 커밋 금지. **변경 파일만 선택적 `git add`**(절대 `git add -A`/`.`/`-am` 금지) — 미커밋 픽스처(있다면) 보존. push 안 함(사용자 지시 시).
- **EOL(교훈):** 편집=Edit 툴(mass `sed` 금지 — 이 git-bash에서 CRLF flip 유발). 커밋 후 필요 시 committed 파일 `git checkout --`로 worktree CRLF 복원.
- **순서 의존:** Task 1(GameFramework `ToUnity`) 먼저 — 파사드가 참조. Task 1 컴파일 클린 후 Task 2~5(클라, 런타임 상호의존이라 한 배치로 편집 후 한 번 컴파일). Task 6 플레이 검증.
- **범위 = 클라.** 서버 `CharacterCreator`/`ItemCreator`/`LOPEntity`/`WorldMotionSync`는 **손대지 않는다**(서버는 Rigidbody 권위 유지).

---

## Task 1: GameFramework — `ToUnity` 역변환 추가

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Extensions/Numerics.Extensions.cs`
- (Repo: `C:/Users/re5na/workspace/LOP/GameFramework`, asmdef `baegames.GameFramework.Runtime`)

- [ ] **Step 1: `ToUnity` 2개 추가**

기존 `ToNumerics`(Vector3/Quaternion) 뒤, 클래스 닫기 전에 추가:
```csharp
public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
public static Quaternion ToUnity(this System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
```
(파일 상단 주석의 "역변환은 후속 슬라이스에서 추가" 문구는 이 슬라이스에서 실현됐으니, 그 한 줄 주석은 제거하거나 "추가됨"으로 정리.)

- [ ] **Step 2: 두 에디터 refresh + 컴파일 검증**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance=<Client id>)`
- `read_console(action="get", types=["error"], unity_instance=<Client id>)` → 에러 0. (같은 file: 패키지라 클라가 컴파일; 서버 에디터도 열려 있으면 동일 확인 가능하나 서버 코드 변경 아님.)

Expected: 에러 0. `System.Numerics.Vector3.ToUnity()` / `Quaternion.ToUnity()` 해석.

- [ ] **Step 3: 커밋 (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/stage4-motion-world-authority
git add Runtime/Scripts/Extensions/Numerics.Extensions.cs
git status --short
git commit -m "feat(extensions): add Numerics->Unity ToUnity conversions

Pair of ToNumerics; needed by the pull consumer (LOPEntity motion facade
reading World.Transform/Velocity). Additive, unused by server.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/stage4-motion-world-authority -m "Merge feature/stage4-motion-world-authority: ToUnity conversions"
git branch -d feature/stage4-motion-world-authority
git log --oneline -2
```

> **주의:** 이 브랜치명은 Client repo에서도 재사용(각 repo 독립 브랜치). GameFramework은 여기서 머지, Client는 Task 5 후 머지.

---

## Task 2: Client `LOPEntity` — 파사드 전환

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPEntity.cs`

- [ ] **Step 1: 필드 → World 참조 + `LinkWorldMotion` + 파사드 접근자**

`_position/_rotation/_velocity` 3필드 + 각 `SetProperty` 접근자를 **전부 교체**. 변경 후 (10-38행 영역):
```csharp
        private GameFramework.World.Transform worldTransform;
        private GameFramework.World.Velocity worldVelocity;

        /// <summary>크리에이터가 이 엔티티의 World.Entity 모션 컴포넌트를 연결한다(파사드 백킹). Initialize 전에 호출.</summary>
        public void LinkWorldMotion(GameFramework.World.Transform transform, GameFramework.World.Velocity velocity)
        {
            this.worldTransform = transform;
            this.worldVelocity = velocity;
        }

        public override Vector3 position
        {
            get => worldTransform.Position.ToUnity();
            set
            {
                var current = worldTransform.Position.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Position = value.ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(position)));
            }
        }

        public override Vector3 rotation
        {
            get => worldTransform.Rotation.ToUnity().eulerAngles;
            set
            {
                var current = worldTransform.Rotation.ToUnity().eulerAngles;
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Rotation = Quaternion.Euler(value).ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(rotation)));
            }
        }

        public override Vector3 velocity
        {
            get => worldVelocity.Linear.ToUnity();
            set
            {
                var current = worldVelocity.Linear.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldVelocity.Linear = value.ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(velocity)));
            }
        }
```
- `RaisePropertyChanged`(40-43행)·`Initialize`(45-51행)·`UpdateEntity`/`UpdateStatuses`·`SyncPhysics`(66-78행)는 **무변경**. `SyncPhysics`가 `position/rotation/velocity =`로 쓰는 건 이제 파사드 통해 World로 감(의도).
- `using System.ComponentModel;`(3행) 유지(`PropertyChangedEventArgs`). `using GameFramework;`(1행)가 `Vector3EqualityComparer`·`ToUnity`·`ToNumerics` 커버(확인: 미해석 시 네임스페이스 점검).

> 이 시점 컴파일은 되지만 런타임은 creator가 link하기 전까지 NRE(worldTransform null). Task 3/4에서 link 배선하므로 **Task 2~5를 한 배치로 편집 후 한 번 컴파일**(중간 실행 금지).

---

## Task 3: Client `CharacterCreator` — 생성 순서 재배치 + link

**Files:**
- Modify: `.../Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: worldEntity + Transform/Velocity를 LOPEntity 생성 *앞*으로, link 추가**

현재 30-32행:
```csharp
            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
```
변경 후 (worldEntity를 먼저 만들고 Transform/Velocity 부착 → LOPEntity 생성 → link → Initialize):
```csharp
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform());
            worldEntity.Add(new GameFramework.World.Velocity());

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
            entity.Initialize(creationData);   // position/rotation/velocity가 파사드 통해 World에 시드
```

- [ ] **Step 2: 하단 World Core 블록에서 worldEntity 재선언·Transform/Velocity Add·register 정리**

현재 78-99행 블록에서:
- `var worldEntity = new GameFramework.World.Entity(creationData.entityId);`(79행) **제거**(위에서 이미 생성).
- Transform Add(91-95행)·Velocity Add(96행) **제거**(위로 이동, Initialize가 시드).
- 나머지(Health/Mana/Level/Stats/Abilities/StatusEffects Add, `entityRegistry.Add(worldEntity)`(99행), `abilitySystem.Grant`)는 **유지**. (`worldEntity`는 이제 상단 선언을 참조.)

변경 후 블록 형태(발췌):
```csharp
            // --- World Core (병렬·추가) ---
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(...) {...});
            worldEntity.Add(new GameFramework.World.Level {...});
            var worldStats = new GameFramework.World.Stats();
            ... (Stats 시드, characterComponent.masterData.Speed 포함 — 유지)
            worldEntity.Add(worldStats);
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            entityRegistry.Add(worldEntity);
            abilitySystem.Grant(worldEntity, 1); ... Grant 2,3
            Debug.Log(...);
```

> register(`entityRegistry.Add`)는 기존 위치(모든 컴포넌트 부착 후)에 유지 — "완성 후 등록" 보존. link는 registry 아닌 직접 참조라 이 순서 OK.

---

## Task 4: Client `ItemCreator` — 아이템에도 World.Entity(Transform/Velocity) + register

**Files:**
- Modify: `.../Assets/Scripts/EntityCreator/ItemCreator.cs`

- [ ] **Step 1: `EntityRegistry` 주입 필드 추가**

`[Inject] private IObjectResolver objectResolver;`(9-10행) 뒤에:
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;
```

- [ ] **Step 2: worldEntity(Transform/Velocity)를 LOPEntity 앞에 + link + 등록**

현재 18-20행:
```csharp
            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
```
변경 후:
```csharp
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform());
            worldEntity.Add(new GameFramework.World.Velocity());

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
            entity.Initialize(creationData);
```
그리고 `return entity;`(51행) 직전에:
```csharp
            entityRegistry.Add(worldEntity);
```
(아이템 World.Entity는 Transform/Velocity만 — Health/Abilities 없음. `LOPWorld.Mutation`의 `AbilitySystem.Tick`/`StatusEffectSystem.Tick`이 컴포넌트 null 가드로 no-op → 안전.)

- [ ] **Step 3: 레지스트리 순회 안전성 확인(코드 점검)**

클라에서 `EntityRegistry.All`/registry 순회하는 곳이 아이템(캐릭터 아닌 엔티티)을 컴포넌트 가정 없이 다루는지 grep 점검(`EntityRegistry.All`, `foreach.*entityRegistry`). 알려진 소비자 = `LOPWorld.Mutation`(가드 확인됨). 신규 위반 발견 시 STOP → 보고.

---

## Task 5: Client `WorldMotionSync` 삭제 + `EntityBinder` 정리

**Files:**
- Delete: `.../Assets/Scripts/World/WorldMotionSync.cs` (+ `.meta`)
- Modify: `.../Assets/Scripts/Game/EntityBinder.cs`

- [ ] **Step 1: `EntityBinder`에서 WorldMotionSync 생성 제거**

53-55행 3줄 제거:
```csharp
            WorldMotionSync worldMotionSync = root.CreateChildWithComponent<WorldMotionSync>();
            objectResolver.Inject(worldMotionSync);
            worldMotionSync.SetEntity(entity);
```
클래스 XML 주석(9-12행)의 `WorldMotionSync` 언급도 정리(장식 뷰만 남김).

- [ ] **Step 2: `WorldMotionSync.cs` 삭제**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git rm Assets/Scripts/World/WorldMotionSync.cs Assets/Scripts/World/WorldMotionSync.cs.meta
```
(**클라만.** 서버 `WorldMotionSync`는 유지.)

- [ ] **Step 3: refresh + 컴파일 검증 (클라)**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance=<Client id>)`
- `read_console(action="get", types=["error"], unity_instance=<Client id>)` → 에러 0.

Expected: 에러 0. 특히 `WorldMotionSync` 미참조, `LOPEntity`에 `_position` 등 잔재 없음, `ToUnity`/`Vector3EqualityComparer` 해석, creator link 배선 OK.

- [ ] **Step 4: 커밋 (Client repo)**

스테이징 = LOPEntity.cs, CharacterCreator.cs, ItemCreator.cs, EntityBinder.cs, WorldMotionSync.cs(삭제)+.meta. 픽스처 있으면 제외.
```bash
git checkout -b feature/stage4-motion-world-authority
git add Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Game/EntityBinder.cs Assets/Scripts/World/WorldMotionSync.cs Assets/Scripts/World/WorldMotionSync.cs.meta
git status --short
git commit -m "refactor(entity): motion authority -> World.Entity via LOPEntity facade

position/rotation/velocity now back onto World.Transform/Velocity (single
source of truth) instead of LOPEntity fields; outer API unchanged. Items
get a minimal World.Entity (Transform/Velocity). Delete redundant one-way
WorldMotionSync mirror. Client-only; server keeps Rigidbody authority.
Stage4 keystone (unblocks 4e + snapshot/restore).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git status --short   # 픽스처가 unstaged/untracked로 남았는지 확인
```
(머지는 Task 6 검증 후.)

---

## Task 6: 통합 검증 + 머지

**Files:** (없음 — 실행 검증)

- [ ] **Step 1: 서버 → 클라 플레이**

서버 Play → 클라 Play 룸 접속.

- [ ] **Step 2: 모션 동작 동일 확인**

- **이동/회전**: 걷기·방향전환이 이전과 시각 동일. (회전 euler↔quat 왕복 nuance — facing 시각 동일 확인.)
- **점프/대시**: 정상.
- **reconciliation**: 본인 캐릭(SnapReconciler)·타인(ServerStateReconciler) 러버밴딩 무회귀.
- **아이템**: 표시·물리·터치(exp) 정상(신규 World.Entity).
- 클·서 콘솔 신규 에러 0.

Expected: 동작 차이 0(회전 raw값 제외 — 시각 동일), 에러 0. 차이 시 — 파사드 변환(ToUnity/ToNumerics), link 순서(Initialize 전 link), Vector3EqualityComparer 발화 조건 점검.

- [ ] **Step 3: 머지 (사용자 확인 후)**

```bash
# Client repo
git checkout main
git merge --no-ff feature/stage4-motion-world-authority -m "Merge feature/stage4-motion-world-authority: motion authority to World.Entity (client)"
git branch -d feature/stage4-motion-world-authority
```
(GameFramework은 Task 1에서 이미 머지. push는 사용자 지시 시.)

- [ ] **Step 4: 완료**

Motion 권위 → World.Entity 이전 완료(클라). 서버 flip·4e는 후속. spec/메모리 상태 갱신.

---

## Self-Review (작성자 확인)

- **Spec 커버리지:** ToUnity 추가(Task1) / LOPEntity 파사드 3접근자+Link(Task2) / CharacterCreator 재배치(Task3) / ItemCreator World.Entity+register(Task4) / WorldMotionSync 삭제+EntityBinder(Task5) / 클라단독·서버무변경(규약) / 동작보존 검증(Task6) — 전부 대응. ✅
- **순서/링크 정확성:** worldEntity Transform/Velocity를 LOPEntity 생성 전에 만들고 `LinkWorldMotion`을 `Initialize` 전에 호출 → position 접근 시 worldTransform 존재 보장. ✅
- **동등성 보존:** 파사드 setter가 `Vector3EqualityComparer.instance`로 변경 시에만 발화 = 기존 `SetProperty` 동치. ✅ (회전은 euler↔quat 왕복으로 raw값 mod-360 등가 — 시각/기능 무영향, 검증 항목으로 명시.)
- **안전성:** 아이템 registry 편입은 Tick 가드(null-return)로 no-op — 실측 확인. Task4 Step3에서 타 순회처 재점검. ✅
- **Placeholder:** `<Client id>`만 규약에서 해소, 그 외 실제값. ✅
