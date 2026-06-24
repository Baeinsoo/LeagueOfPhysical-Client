# 이동 결정론 커널 공유 Implementation Plan (Slice 4c #1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 클·서 각자 복사본인 이동 계산(`LOPMovementManager.ProcessInput`의 이동 속도·회전 계산)을 **LOP-Shared 순수 함수 1벌**(`MovementSystem.ProcessMovement`)로 추출. 양쪽 매니저가 이 한 벌을 호출 → 이동 로직 단일 진실원본. **동작 100% 보존**(계산식 그대로 이전, 적용·점프·물리 무변경).

**Spec:** `docs/superpowers/specs/2026-06-24-world-movement-kernel-design.md`

**Architecture:** 3-repo 추가·치환형. LOP-Shared에 신규 순수 타입 추가(기존 컴파일 비파괴), 클·서 `LOPMovementManager`가 계산부를 커널 호출로 교체. 클·서 매니저는 현재 **바이트 동일** — 같은 Edit을 양쪽에 적용.

**Tech Stack:** Unity / C# / VContainer DI(이 슬라이스는 DI 무변경) / UnityMCP(클·서 컴파일 검증).

---

## 레포 / 도구 참조
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`
- **GameFramework:** 이번 슬라이스 **무변경**(커널은 LOP 특화 → LOP-Shared).
- **UnityMCP:** `mcpforunity://instances`에서 Client/Server id(해시 변동). 모든 호출에 `unity_instance` 명시. (직전 기준 Client `@de70658b9450cbb4` / Server `@f99391fa2dbaaf3c` — 매번 resource로 재확인.)

**⚠️ EOL 교훈(직전 슬라이스):** 이 git-bash에서 `find -exec sed -i`가 모든 .cs를 CRLF→LF로 바꿈. **mass sed 금지** — 신규 파일은 Write, 기존 파일 편집은 Edit(정밀, 주변 EOL 보존). 신규 .cs는 LF로 생기지만 git이 LF 저장이라 무해(Unity가 저장 시 CRLF).

**.meta:** 신규 `.cs`는 `refresh_unity` 시 Unity가 `.meta` 생성. **`.cs`와 생성된 `.cs.meta`를 함께 커밋.** .meta 수기 생성 금지.

**픽스처 보존:** `git add -A`/`commit -am` 금지. Task에 명시된 파일만 add. Client 픽스처(Art/UIRoot.prefab/Room.unity/ProjectSettings)·Server 픽스처(Room.unity/ConfigureRoomComponent.cs/Art) 커밋 금지.

**컨벤션:** Microsoft C#. 커널은 순수 C#(상태 없음·부수효과 없음). 명명은 산업 표준(`ProcessMovement` = Source `CGameMovement::ProcessMovement`).

---

## 확정된 설계 결정 (spec의 Open Question 해소)

1. **커널 명명:** 타입 `MovementSystem`(코드베이스 `HealthSystem`/`StatsSystem` 짝), 메서드 `ProcessMovement`(Source 정합). 네임스페이스 `LOP`.
2. **점프 = host 100% 잔류:** 점프는 *결정 로직 없는 순수 PhysX 적용*(y리셋 + `AddForce` 임펄스)이라 커널에 넣지 않는다. 커널은 **이동 속도·회전 계산만.** host가 raw `jump` 입력으로 기존 분기 그대로 수행. (커널 입력에 `jump`/`jumpPower` 불포함 — YAGNI.)
3. **커널 입출력 (primitives only, 엔진 객체 미참조):**
   - `MovementInput { Vector3 currentVelocity, float horizontal, float vertical, float speed }`
   - `MovementResult { bool hasMove, Vector3 velocity, Vector3 rotation }`
4. **클·서 동일성:** 양쪽 `LOPMovementManager`가 현재 바이트 동일 → 동일 Edit. (확인은 Task 3 Step 1.)
5. **위치:** LOP-Shared `Runtime/Scripts/Game/MovementSystem.cs`(LOPWorld 옆, 한 파일에 두 struct + static class).

---

## Task 0: 3개 repo 피처 브랜치
- [ ] **Step 1: 3 repo working tree 확인** (픽스처 외 깨끗; main 최신)
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; git -C C:/Users/re5na/workspace/LOP/$r branch --show-current; done
```
Expected: Client/Server 픽스처만, 모두 `main`. 다른 변경 있으면 멈추고 보고. (Client에 이번 spec/plan 문서 2개는 untracked로 존재 — 정상.)
- [ ] **Step 2: 브랜치 생성**
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/world-movement-kernel; done
```

---

## Task 1: LOP-Shared — `MovementSystem` 커널

- [ ] **Step 1: `Runtime/Scripts/Game/MovementSystem.cs` 생성** (Write)
```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>이동 커널 입력 — primitives only (엔진 객체·MasterData 비참조).</summary>
    public readonly struct MovementInput
    {
        public readonly Vector3 currentVelocity;
        public readonly float horizontal;
        public readonly float vertical;
        public readonly float speed;

        public MovementInput(Vector3 currentVelocity, float horizontal, float vertical, float speed)
        {
            this.currentVelocity = currentVelocity;
            this.horizontal = horizontal;
            this.vertical = vertical;
            this.speed = speed;
        }
    }

    /// <summary>이동 커널 출력 — host가 적용. hasMove == false면 velocity/rotation 무시.</summary>
    public readonly struct MovementResult
    {
        public readonly bool hasMove;
        public readonly Vector3 velocity;
        public readonly Vector3 rotation;

        public MovementResult(bool hasMove, Vector3 velocity, Vector3 rotation)
        {
            this.hasMove = hasMove;
            this.velocity = velocity;
            this.rotation = rotation;
        }
    }

    /// <summary>
    /// 이동 결정론 커널 — 클·서 공유 1벌. 순수(상태 없음·부수효과 없음).
    /// 입력 → 이동 속도·회전 계산. 점프(PhysX 임펄스)·적용·연출은 host 책임.
    /// </summary>
    public static class MovementSystem
    {
        public static MovementResult ProcessMovement(in MovementInput input)
        {
            Vector3 direction = new Vector3(input.horizontal, 0, input.vertical).normalized;

            if (direction.sqrMagnitude > 0)
            {
                var move = direction * input.speed;
                var velocity = new Vector3(move.x, input.currentVelocity.y, move.z);
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var rotation = new Vector3(0, angle, 0);
                return new MovementResult(true, velocity, rotation);
            }

            return new MovementResult(false, input.currentVelocity, Vector3.zero);
        }
    }
}
```
> 계산식은 `LOPMovementManager`의 이동·회전 라인과 **글자 그대로 동일**(velocity=dir×speed·y유지, rotation=atan2 snap). asmdef 추가 불필요(LOP-Shared.Runtime은 이미 `UnityEngine` 사용 가능).

- [ ] **Step 2: 컴파일 검증** — 클·서 양쪽 인스턴스 `refresh_unity`(scope=all, force) → idle → `read_console`(types=error) 0. (LOP-Shared 변경이 클·서에 전파.) 신규 `MovementSystem.cs.meta` 생성 확인.

---

## Task 2: LOP-Shared — 커널 EditMode 테스트 (신규 가치)

- [ ] **Step 1: Tests.EditMode asmdef 확인** — `Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef` 존재 확인. (없으면 이 Task 스킵하고 Task 5 Self-Review에 기록.)
- [ ] **Step 2: `Tests/EditMode/MovementSystemTests.cs` 생성** (Write) — 커널이 순수 함수라 Unity 없이 검증 가능(이전엔 LOPEntity/Rigidbody 결합으로 불가했던 테스트).
  - `ProcessMovement_NoInput_ReturnsNoMove`: h=0,v=0 → `hasMove == false`.
  - `ProcessMovement_Forward_VelocityFromSpeed`: h=0,v=1,speed=5,currentVel=(0,3,0) → `velocity == (0,3,5)` (y 유지), `hasMove == true`.
  - `ProcessMovement_PreservesYVelocity`: currentVel.y가 결과 velocity.y로 보존됨.
  - `ProcessMovement_Rotation_FacesMoveDirection`: h=1,v=0 → `rotation == (0,90,0)` (atan2(1,0)=90°).
  - (부동소수 비교는 `Assert.That(..., Is.EqualTo(...).Within(1e-4f))`.)
- [ ] **Step 3: 테스트 실행** — `run_tests`(EditMode, LOP-Shared 테스트 필터) 또는 Test Runner로 전부 green 확인. `.cs`+`.cs.meta` 생성 확인.

---

## Task 3: Client + Server — 매니저가 커널 호출 (평행, 동일 Edit)

**Files:** `Assets/Scripts/Game/LOPMovementManager.cs` (Client / Server 각각 — **현재 바이트 동일**)

- [ ] **Step 1: 클·서 매니저 동일성 재확인** — 두 파일 diff. 다르면 멈추고 보고(다른 점이 커널 통합의 핵심 정보). 동일하면 같은 Edit 적용.
- [ ] **Step 2: `ProcessInput` 본문의 이동·회전 계산을 커널 호출로 교체** (Edit, 클·서 각각)

`direction` 계산 ~ `if (direction.sqrMagnitude > 0) { ... }` 블록(클 `LOPMovementManager.cs:21-32`)을 다음으로 교체:
```csharp
            var result = MovementSystem.ProcessMovement(new MovementInput(
                entity.velocity, horizontal, vertical, characterComponent.masterData.Speed));

            if (result.hasMove)
            {
                entity.velocity = result.velocity;   // EventBus 연출은 여기(host)
                entity.rotation = result.rotation;
            }
```
- **가드 무변경:** `CharacterComponent`/`PhysicsComponent` 존재 체크(:11-19) 그대로 잔류.
- **점프 무변경:** `if (jump) { ... AddForce ... }`(:34-39) 그대로 잔류 — 커널 미관여.
- **명시적 인터페이스 구현부**(`void IMovementManager.ProcessInput`, :42-52) 무변경.
- `using UnityEngine;`은 점프 분기(`Vector3`/`ForceMode`)가 남으니 유지.

- [ ] **Step 3: 컴파일 검증** — Client·Server 각 인스턴스 `read_console`(types=error) 0.

---

## Task 4: 종합 컴파일 + 스모크 검증
- [ ] **Step 1: 양쪽 재스캔 + 컴파일** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → idle → `read_console`(types=error) **0**.
- [ ] **Step 2: 신규 .meta 확인** — LOP-Shared `MovementSystem.cs.meta`(+ 테스트 추가 시 `MovementSystemTests.cs.meta`) 생성됨.

---

## Task 5: 커밋 (repo별)
- [ ] **Step 1: LOP-Shared** — `git add Runtime/Scripts/Game/MovementSystem.cs*` (+테스트 추가 시 `Tests/EditMode/MovementSystemTests.cs*`).
```
feat(world): shared deterministic movement kernel (client/server one copy)

Extract LOPMovementManager move/rotation math into LOP.MovementSystem.
ProcessMovement (pure, primitives in/out — no LOPEntity/Rigidbody/MasterData)
so client and server run one movement-logic copy (determinism by
construction). Jump (PhysX impulse) + apply + egress stay host-side.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: Client** — `git add Assets/Scripts/Game/LOPMovementManager.cs` (픽스처 제외).
```
feat(world): LOPMovementManager calls shared MovementSystem kernel

Move/rotation computation now delegates to LOP.MovementSystem.ProcessMovement
(LOP-Shared, one copy). Guards, jump impulse, velocity/rotation apply (+
EventBus egress) unchanged. Behavior preserved.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: Server** — Client Step 2와 동형(같은 변경). 변경 파일만(픽스처 제외).
- [ ] **Step 4: Client 문서 커밋** — `git add docs/superpowers/specs/2026-06-24-world-movement-kernel-design.md docs/superpowers/plans/2026-06-24-world-movement-kernel.md`.

---

## Task 6: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0. 커널 테스트 green.
- [ ] **Step 2: 플레이 검증(사용자)** — 이동/회전/점프 **이전과 동일**. 특히: 전후좌우 이동 속도·바라보는 방향, 점프(낙하 중 점프 포함 — §3.2 거동 불변), 정지 시 무동작.
- [ ] **Step 3: OK 시 머지/푸시(사용자 요청 시)** — LOP-Shared → Client → Server 순 `--no-ff` (file: 의존: Shared 먼저). 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지:** 커널=Task1 / 테스트=Task2 / 클·서 호출 교체=Task3 / 검증=Task4 / 커밋=Task5. ✅
- **동작 보존:** 계산식 글자 그대로 이전(velocity/rotation), 가드·점프·적용·EventBus 무변경. `hasMove==false`면 velocity/rotation 미터치 = 원본 무동작 분기와 동일. ✅
- **primitives in/out:** 커널이 `LOPEntity`/`Rigidbody`/`MasterData` 미참조 — host가 추출해 넘기고 받아 적용. bg_pmove 패턴. ✅
- **점프 host 잔류:** 결정 로직 없는 PhysX 적용이라 커널 미포함, raw `jump` 분기 그대로. ✅
- **클·서 1벌:** 양쪽 매니저가 같은 커널 호출 = 이동 로직 단일 진실원본(현재 바이트 동일한 복사본을 통합). ✅
- **EOL 안전:** 신규=Write, 편집=Edit, mass sed 없음. ✅
- **범위 정직:** LOPWorld 훅 미충전·접근 B(World 데이터·위치권위)=Stage④ 보류 명시(spec Out of Scope). 이번은 커널 통일만. ✅
- **결정론 범위:** 로직 단일화이지 bit-identical float 아님(reconcile가 float 오차 담당) — spec에 명시. ✅
```
