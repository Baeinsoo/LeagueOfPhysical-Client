# 클라 키네마틱 예측 (슬라이스 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라이언트 로컬 플레이어(내 캐릭) 예측을 서버와 **같은** 키네마틱 이동(`KinematicMoveSystem` + `Depenetrate`)으로 바꿔, 예측 = 서버 권위가 되게 한다 → 지면에서 보이던 자잘한 reconciliation을 없앤다.

**Architecture:** 슬라이스 2에서 만든 공유 `KinematicMoveSystem`을 클라 호스트(`LOPRunner`)의 전방 틱과 `Reconciler` 재생 루프에서 재사용한다. 내 캐릭을 dynamic→kinematic으로 바꾸고, 매 예측 틱마다 `Depenetrate`+`KinematicMoveSystem.Tick`으로 이동시킨다(기존 PhysX 캐릭터 적분 대체). **로컬 플레이어만** — 원격은 스냅 팔로워 유지. 클라 단독(서버·공유 패키지 무변경).

**Tech Stack:** Unity 6, C#, VContainer, UnityMCP(컴파일·플레이), Git.

## Global Constraints

- **작업 브랜치 = `feature/kinematic-server-wiring`** (클라 레포, 슬라이스 2 문서가 있는 그 브랜치에 이어서). **main 직접 커밋 금지.**
- **클라 레포 단독** — `LeagueOfPhysical-Client`만. 서버·GameFramework·LOP-Shared 무변경(슬라이스 2에서 완성).
- **UnityMCP는 매 호출에 `unity_instance` 명시** — 클라 = `LeagueOfPhysical-Client@de70658b9450cbb4`(최신 hash는 `mcpforunity://instances`로 확인). 컴파일 검증은 클라 인스턴스.
- **World 타입 풀 네임스페이스 한정** (`GameFramework.World.Entity` 등).
- **게이트 = 로컬 플레이어(`playerContext.entity`)만.** 원격 엔티티는 스냅 팔로워(ServerStateReconciler/보간)라 로컬 키네마틱 이동을 걸지 않는다.
- **중력·캡슐 상수는 서버와 동일**(`KinematicMoveSystem` 내부 `-9.81*2`, radius 0.35, height 1.5). 클라 `Physics.gravity=(0,-19.62,0)`·`autoSyncTransforms=false`는 이미 서버와 동일.
- **커밋 메시지 끝**: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

> **⚠️ Task 2 검증은 사람 플레이.** Task 1(DI+Depenetrate)은 컴파일로 자동 검증되지만, Task 2(내 캐릭 kinematic + 전방틱 + reconciler 재생)는 **사용자 수동 플레이 검증**이 완료 조건 — 특히 "지면 걷기 recon이 사라졌는지". 서브에이전트는 코드+컴파일까지.

---

## 파일 구조

- **Modify** `Assets/Scripts/Game/GameLifetimeScope.cs` — `ICollisionQuery`·`KinematicMoveSystem` DI 등록.
- **Modify** `Assets/Scripts/Component/PhysicsComponent.cs` — `Depenetrate` 추가(서버 미러).
- **Modify** `Assets/Scripts/EntityCreator/CharacterCreator.cs:62` — 내 캐릭도 kinematic.
- **Modify** `Assets/Scripts/Game/LOPRunner.cs` — `KinematicMoveSystem` 주입 + `MoveLocalPlayer()` 전방 틱 스텝.
- **Modify** `Assets/Scripts/Entity/Reconciler.cs` — 재생 루프의 PhysX 적분을 키네마틱 이동으로 교체 + `KinematicMoveSystem` 주입, 미사용 `physicsSimulator` 제거.

---

## Task 1: 클라 인프라 — DI 등록 + PhysicsComponent.Depenetrate

`ICollisionQuery`·`KinematicMoveSystem`을 클라 컨테이너에 등록하고, 서버와 동일한 `Depenetrate`를 클라 `PhysicsComponent`에 추가한다. 아직 호출하지 않음 — 컴파일만 검증(내 캐릭은 이 Task에선 dynamic 유지).

**Files:**
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Component/PhysicsComponent.cs`

**Interfaces:**
- Consumes: `GameFramework.ICollisionQuery`/`UnityCollisionQuery`, `LOP.KinematicMoveSystem(ICollisionQuery, int)`(슬라이스 1/2, main에 병합됨).
- Produces: 클라 컨테이너에서 `KinematicMoveSystem` resolve 가능; `PhysicsComponent.Depenetrate(int layerMask)`.

- [ ] **Step 1: DI 등록 추가**

`Assets/Scripts/Game/GameLifetimeScope.cs`에서 `IPhysicsSimulator` 등록 줄(현재 line 50) **바로 다음**에 추가:

```csharp
            builder.Register<GameFramework.ICollisionQuery, GameFramework.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.ICollisionQuery>(), LayerMask.GetMask("Default")), Lifetime.Singleton);
```

- [ ] **Step 2: `PhysicsComponent.Depenetrate` 추가(서버 미러)**

`Assets/Scripts/Component/PhysicsComponent.cs`의 `OnDetach()` 메서드 **바로 다음**에 추가:

```csharp
        /// <summary>
        /// 겹친 지오메트리(스폰 시 지면과 붙음·끼임)에서 캡슐을 밖으로 밀어낸다.
        /// sweep 이동은 "시작부터 겹친" 콜라이더를 무시하므로, 겹친 채로는 지면을 못 잡아 통과한다 —
        /// 그래서 이동 전에 실제 콜라이더로 밀어내 겹침을 푼다(PhysX가 하던 일을 대신).
        /// </summary>
        public void Depenetrate(int layerMask)
        {
            var capsule = (CapsuleCollider)entityColliders[0];
            Vector3 ownPos = capsule.transform.position;
            Quaternion ownRot = capsule.transform.rotation;

            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            float radius = capsule.radius;
            float halfSpan = Mathf.Max(capsule.height * 0.5f - radius, 0f);
            Vector3 up = capsule.transform.up;
            Vector3 p1 = center - up * halfSpan;
            Vector3 p2 = center + up * halfSpan;

            Collider[] overlaps = Physics.OverlapCapsule(p1, p2, radius, layerMask, QueryTriggerInteraction.Ignore);
            Vector3 total = Vector3.zero;
            foreach (var other in overlaps)
            {
                if (other == capsule)
                {
                    continue;   // 자기 콜라이더 제외
                }
                if (Physics.ComputePenetration(capsule, ownPos, ownRot,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                {
                    total += dir * dist;
                }
            }

            if (total.sqrMagnitude > 0f)
            {
                entity.position += total;   // 파사드 → World.Transform + reactive rb.position
            }
        }
```

- [ ] **Step 3: 클라 컴파일 확인**

클라 인스턴스 id 해석(`mcpforunity://instances`) 후:
`refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`.
Expected: 신규 에러 0. `KinematicMoveSystem`·`ICollisionQuery` 해석됨.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Component/PhysicsComponent.cs
git commit -m "feat(game): 클라 DI에 ICollisionQuery+KinematicMoveSystem + PhysicsComponent.Depenetrate

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 클라 행동 배선 — 내 캐릭 kinematic + 전방틱 + reconciler 재생 (사람 플레이 검증)

내 캐릭을 kinematic으로 바꾸고, 전방 틱과 reconciler 재생 루프를 키네마틱 이동으로 교체한다. 셋이 함께 가야 이동이 안 끊긴다(kinematic인데 안 움직이면 얼어붙음). **완료 조건 = 사용자 플레이 검증.**

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs:62`
- Modify: `Assets/Scripts/Game/LOPRunner.cs`
- Modify: `Assets/Scripts/Entity/Reconciler.cs`

**Interfaces:**
- Consumes: `KinematicMoveSystem.Tick(GameFramework.World.Entity, float)`, `PhysicsComponent.Depenetrate(int)`(Task 1), `playerContext.entity`, `entity.PushMotionToPhysics()`.

- [ ] **Step 1: 내 캐릭도 kinematic 생성**

`Assets/Scripts/EntityCreator/CharacterCreator.cs:61-62` 교체:

```csharp
            // 모든 캐릭터 kinematic — 우리가 직접 이동시킨다. 내 캐릭=예측(KinematicMoveSystem), 남=스냅 팔로워.
            physicsComponent.Initialize(true, false);
```

- [ ] **Step 2: `LOPRunner`에 `KinematicMoveSystem` 주입 + `MoveLocalPlayer()` 스텝**

`Assets/Scripts/Game/LOPRunner.cs` 주입 필드에 추가(기존 `[Inject]` 블록 옆):

```csharp
        [Inject] private KinematicMoveSystem kinematicMoveSystem;
```

`UpdateRunner()`에서 `DriveAbilityEffects();` **다음**, `SimulatePhysics();` **앞**에 호출 추가:

```csharp
            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
            DriveAbilityEffects();

            MoveLocalPlayer();

            SimulatePhysics();
```

그리고 `DriveAbilityEffects()` 메서드 아래에 추가:

```csharp
        // 내 캐릭(예측 대상)만 키네마틱 이동(중력+collide-and-slide)시킨다 — 서버와 같은 KinematicMoveSystem.
        // 원격은 스냅 팔로워(ServerStateReconciler/보간)라 여기서 안 움직인다.
        private void MoveLocalPlayer()
        {
            LOPEntity entity = playerContext.entity;
            if (entity == null)
            {
                return;
            }
            Physics.SyncTransforms();   // 캐스트가 최신 콜라이더 포즈를 보도록(autoSyncTransforms=false)
            int layerMask = LayerMask.GetMask("Default");
            entity.GetEntityComponent<PhysicsComponent>().Depenetrate(layerMask);
            kinematicMoveSystem.Tick(entityRegistry.Get(entity.entityId), (float)tickUpdater.interval);
            entity.PushMotionToPhysics();
        }
```

- [ ] **Step 3: `Reconciler` 재생 루프를 키네마틱 이동으로 교체**

`Assets/Scripts/Entity/Reconciler.cs` 주입 필드에서 `physicsSimulator`를 **제거**하고(재생 루프 외 미사용) `KinematicMoveSystem`을 추가:

```csharp
        // 제거: [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;
        [Inject] private KinematicMoveSystem kinematicMoveSystem;
```

재생 루프 안에서 기존 PhysX 3줄을 키네마틱 이동으로 교체:

```csharp
                // 기존:
                //   entity.PushMotionToPhysics();
                //   physicsSimulator.Simulate(deltaTime);
                //   entity.SyncPhysics();
                // 교체(서버 MoveCharacters와 동일한 키네마틱 한 틱):
                Physics.SyncTransforms();
                entity.GetEntityComponent<PhysicsComponent>().Depenetrate(layerMask);
                kinematicMoveSystem.Tick(worldEntity, deltaTime);
                entity.PushMotionToPhysics();
```

재생 루프 시작 전에 `layerMask`를 한 번 계산한다(루프 위, `for (long t = ...)` 직전):

```csharp
            int layerMask = LayerMask.GetMask("Default");
```

- [ ] **Step 4: 클라 컴파일 확인**

`refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`.
Expected: 신규 에러 0.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/EntityCreator/CharacterCreator.cs \
        Assets/Scripts/Game/LOPRunner.cs \
        Assets/Scripts/Entity/Reconciler.cs
git commit -m "feat(netcode): 클라 내 캐릭 키네마틱 예측 — 전방틱+reconciler 재생을 KinematicMoveSystem으로

내 캐릭 kinematic + 전방 틱(MoveLocalPlayer)·reconciler 재생 루프의 PhysX 적분을
서버와 같은 KinematicMoveSystem+Depenetrate로 교체. 예측=권위 → 지면 recon 소멸.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 사람 플레이 검증 (완료 조건 — 사용자 수행)**

서버+클라 실행 후 확인:

1. **지면 걷기 recon 소멸** — 슬라이스 2에서 보이던 지면 자잘한 러버밴드/보정이 **사라졌는지**(핵심 목표).
2. **걷기/정지/점프/낙하** — 슬라이스 2와 동일하게 정상, 지면 통과 없음.
3. **벽 막힘 / 각진 벽 미끄러짐** — 정상.
4. **원격 플레이어** — 여전히 부드럽게 보간되고, 내 캐릭이 원격과 부딪혀 막히는지(원격 kinematic 장애물).
5. **대시/넉백** — 예측·보정 정상(회귀 없음).
6. **콘솔** — 서버·클라 신규 에러/경고 0.

> 슬라이스 2에서 "지면에서만 보이던 recon"이 사라지면 성공. 공중/충돌에서 미세 보정은 서버 고유(PhysX 쿼리 비결정성) 잔여라 소량 남을 수 있음 — 심한 러버밴드만 아니면 통과.

---

## Self-Review

**1. Spec coverage (슬라이스 3):**
- 내 캐릭 kinematic → Task 2 Step 1. ✓
- 클라 DI(ICollisionQuery+KinematicMoveSystem) → Task 1 Step 1. ✓
- 클라 Depenetrate → Task 1 Step 2(서버 미러). ✓
- 전방 틱 로컬 플레이어 키네마틱 이동(게이트=playerContext.entity) → Task 2 Step 2. ✓
- reconciler 재생 루프 키네마틱 교체 → Task 2 Step 3. ✓
- 원격 무변경(스냅 팔로워) → 게이트가 로컬만 → ✓.
- 서버·공유 무변경 → 클라 레포만 수정. ✓

**2. Placeholder scan:** 모든 스텝 실제 코드·명령·기대결과. Task 2 Step 6은 사용자 플레이 체크리스트(구체 6항목). "TBD"/"적절히" 없음. ✓

**3. Type consistency:**
- `KinematicMoveSystem(ICollisionQuery, int)`/`.Tick(Entity, float)` — 슬라이스 2 정의, DI·MoveLocalPlayer·Reconciler 호출 일치. ✓
- `PhysicsComponent.Depenetrate(int)` — Task 1 정의, MoveLocalPlayer·Reconciler 호출 일치. ✓
- `playerContext.entity`(LOPEntity), `entity.GetEntityComponent<PhysicsComponent>()`, `entityRegistry.Get(id)`, `entity.PushMotionToPhysics()` — 기존 API. ✓
- Reconciler `worldEntity`(재생 루프 스코프의 World.Entity), `deltaTime` 파라미터 — 기존. ✓
- 중력 상수 `-9.81*2` = 클라 `Physics.gravity`. ✓

이상 없음.

---

## 다음 (슬라이스 3 이후)

슬라이스 1~3 완료 시 4레포(Client/GameFramework/LOP-Shared/Server) `feature/kinematic-server-wiring`를 **함께 main 머지**(main이 "서버만 키네마틱" 중간 상태를 안 겪도록). 이후 잔여: `Depenetrate` 공유 추출(클·서 중복 해소, YAGNI), Stage④ 잔여(확정 게이트·완전 결정론 등).
