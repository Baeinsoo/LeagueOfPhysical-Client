# Stage④ 첫 슬라이스 — 원격 엔티티 kinematic 장애물화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 원격(남) 플레이어 클라 rigidbody를 dynamic → kinematic으로 바꿔 "스냅 위치로 세팅되는 장애물"로 만든다. 내 캐릭만 dynamic으로 남아 롤백 재시뮬의 전제(재스텝해도 남 불변)를 성립시킨다.

**Architecture:** 변경은 클라 2파일뿐. (1) `CharacterCreator`가 원격 캐릭터를 kinematic으로 생성. (2) `LOPEntity.SyncPhysics`가 kinematic 바디에선 rb→World 되읽기를 스킵(World가 권위). 원격 position은 기존 reactive 경로(`PhysicsComponent.OnPropertyChange`)가 이미 처리하고, 브릿지의 velocity 푸시는 kinematic서 무시되므로 그 둘은 무변경.

**Tech Stack:** Unity 6000.3 (PhysX, `SimulationMode.Script` 수동 스텝), VContainer, UnityMCP(컴파일 검증).

## Global Constraints

- **클라 단독** — 서버(`LeagueOfPhysical-Server`) 무변경. 넷코드 프로토콜/와이어(EntitySnap) 무변경.
- **로컬 내 캐릭 무변경** — dynamic 유지, `SnapReconciler`·브릿지 velocity 경로 유지. 이 슬라이스는 로컬 예측/롤백을 건드리지 않는다.
- **유닛 테스트 없음(의도적)** — 클라 코드는 Assembly-CSharp라 asmdef EditMode 테스트 불가 + 물리 통합. 검증 = **컴파일 클린 + 플레이 관찰**. (기존 클라 슬라이스 관행 동일.)
- **UnityMCP 타깃 고정** — 모든 호출에 `unity_instance="LeagueOfPhysical-Client@<hash>"`. 클라 인스턴스 id는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client` 이름으로 조회.
- **커밋** — 피처 브랜치 `feature/stage4-remote-kinematic`(이미 spec 커밋됨). main 직접 커밋 금지.

---

### Task 1: 원격 캐릭터 kinematic 전환 + SyncPhysics 가드

두 편집은 **함께 착지해야** 동작 상태가 유지된다(캐릭터만 kinematic으로 바꾸고 SyncPhysics 가드가 없으면 원격 velocity가 0으로 덮여 run 애니가 깨진다). 따라서 한 태스크.

**Files:**
- Modify: `Assets/Scripts/Entity/LOPEntity.cs` (`SyncPhysics` 메서드)
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs` (`Create` 메서드, 물리 초기화부)

**Interfaces:**
- Consumes: `PhysicsComponent.entityRigidbody` (`UnityEngine.Rigidbody`, 기존), `PhysicsComponent.Initialize(bool isKinematic, bool isTrigger)` (기존), `gameDataStore.userEntityId` / `creationData.entityId` (기존).
- Produces: (외부 시그니처 변화 없음 — 순수 동작 변경. 후속 슬라이스가 의존하는 계약 = "원격/아이템 rb는 kinematic, 내 캐릭 rb만 dynamic".)

- [ ] **Step 1: `LOPEntity.SyncPhysics`에 kinematic 가드 추가**

`Assets/Scripts/Entity/LOPEntity.cs`의 `SyncPhysics()`를 아래로 교체:

```csharp
        public void SyncPhysics()
        {
            PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();

            if (physicsComponent == null)
            {
                return;
            }

            // kinematic 바디(원격 캐릭·아이템)는 World가 권위 — rb→World 되읽기는 rb.linearVelocity(=0)를
            // entity.velocity에 덮어 run 애니·smoothing을 망친다. 스킵한다(rb는 World를 따르는 follower).
            if (physicsComponent.entityRigidbody.isKinematic)
            {
                return;
            }

            position = physicsComponent.entityRigidbody.position;
            rotation = physicsComponent.entityRigidbody.rotation.eulerAngles;
            velocity = physicsComponent.entityRigidbody.linearVelocity;
        }
```

- [ ] **Step 2: `CharacterCreator`에서 원격을 kinematic으로 생성**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`의 물리 초기화부(현재 line 57-59)를 아래로 교체. `bool isUserEntity` 선언을 물리 초기화 **앞으로 이동**하고 Initialize 인자를 `!isUserEntity`로:

교체 전:
```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(false, false);
```

교체 후:
```csharp
            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            // 내 캐릭만 dynamic(예측 대상). 남은 kinematic 장애물 — 스냅 위치로 세팅, 서버 권위.
            physicsComponent.Initialize(!isUserEntity, false);
```

- [ ] **Step 3: 중복이 된 기존 `isUserEntity` 선언 제거**

같은 파일에서 아래로 내려가면 (현재 line 69) 기존 선언이 있다. Step 2에서 위로 옮겼으므로 이 줄만 삭제(주변 `if (isUserEntity)`는 유지):

삭제할 줄:
```csharp
            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
```

이 삭제 후 흐름은 `view.SetEntity(view);` 다음에 바로 `if (isUserEntity)`가 오게 된다.

- [ ] **Step 4: 클라 컴파일 검증**

클라 인스턴스 id 조회 후 force 리프레시 + 콘솔 확인.

Run (UnityMCP):
1. `mcpforunity://instances` 읽어 `LeagueOfPhysical-Client@<hash>` 확보.
2. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)`
3. `read_console(action="get", types=["error"], unity_instance=<client>)`

Expected: `read_console`가 **에러 0건**(`"Retrieved 0 log entries"`). 특히 `isUserEntity` 미정의/중복 선언 컴파일 에러가 없어야 함.

- [ ] **Step 5: 플레이 검증 (사용자 구동)**

클라·서버 에디터로 룸 접속 후 관찰(에이전트는 플레이 불가 → 사용자에게 체크리스트 제시):

1. **원격 플레이어 이동/보간이 부드러운가** — 다른 플레이어가 끊김/떨림 없이 움직이는가. run 애니가 정상 재생되는가(velocity 0-덮어쓰기 제거 확인).
2. **내 캐릭이 남과 부딪혀 막히는가** — 다른 플레이어에게 걸어 들어가면 통과하지 않고 막히는가(kinematic 원격이 장애물로 작동).
3. **내 캐릭 무회귀** — 이동/점프/대시/방향전환·정지, reconciliation(러버밴딩 없음) 정상.
4. **아이템 정상** — 아이템 표시·트리거(획득) 정상.
5. **콘솔 신규 에러 0**.

Expected: 1~5 모두 정상. (문제 시 원격 kinematic 전환의 어느 지점인지 이 체크리스트로 국소화.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/EntityCreator/CharacterCreator.cs
git commit -m "$(cat <<'EOF'
feat(stage4): remote characters kinematic; skip SyncPhysics for kinematic bodies

원격(남) 캐릭터를 kinematic 장애물로(내 캐릭만 dynamic) — 롤백 재시뮬 전제(재스텝해도 남 불변).
SyncPhysics는 kinematic서 스킵(rb.velocity=0 덮어쓰기 방지). position은 기존 reactive 경로, 브릿지 velocity는 kinematic서 무시.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review

**1. Spec coverage:**
- 설계 ① 원격 kinematic 생성 → Task 1 Step 2·3. ✅
- 설계 ② `SyncPhysics` kinematic 스킵 → Task 1 Step 1. ✅
- 설계 ③ 아이템 무변경 → 편집 대상 아님(아이템은 이미 kinematic; SyncPhysics 스킵 대상이 되나 velocity 미사용이라 무해, 설계에 명시). ✅
- 안 바뀌는 것(로컬/서버/프로토콜/단일 씬) → 편집이 2파일·클라뿐이라 자동 충족. ✅
- 검증(컴파일+플레이) → Step 4·5. ✅

**2. Placeholder scan:** "TBD/TODO/적절히 처리" 없음. 모든 편집이 실제 코드 블록. ✅

**3. Type consistency:** `PhysicsComponent.Initialize(bool, bool)`·`entityRigidbody.isKinematic`(UnityEngine.Rigidbody)·`gameDataStore.userEntityId`·`creationData.entityId` — 전부 기존 시그니처 그대로 사용, 신규 타입 도입 없음. ✅

## Execution Handoff

(작성 완료 후 사용자에게 실행 방식 제시 — 이 plan은 단일 태스크라 인라인 실행이 자연스러움.)
