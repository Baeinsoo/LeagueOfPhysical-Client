# 이동 모터 (never-brake + 마찰, 힘 기반) Implementation Plan (이동 재설계 Slice 1)

> ⚠️ **갱신(2026-06-28) — never-brake 단일채널 폐기, 채널 분리(B) 채택.** 단일 velocity의 드리프트-vs-외력 딜레마(spec 참고)로 never-brake를 버리고 **입력 채널 brake-to-desired + 외력 채널**로 회귀. 이 plan의 never-brake/friction 서술은 **무효**.
> **Slice 1(완료)** = 하이브리드: 방향 입력 시 `MoveTowards`(brake-to-desired, 드리프트 수정), 무입력은 손 안 댐 + 엔진 `linearDamping`(drag) 정지. movement는 입력 있을 때만(netcode baseline). 커널/테스트(29 green)·클·서 컴파일 0. **Slice 2** = 외력 채널 + dash. 정식 설계는 `docs/superpowers/specs/2026-06-28-movement-motor-redesign-design.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 수평 이동을 **즉시 velocity 세팅(임시) → 표준 모터**(never-brake 가속 + 별도 마찰)로, **적용을 힘(AddForce VelocityChange)** 으로 바꾼다. 점프도 표준(`vy = jumpSpeed` 세팅)으로. movement는 더 이상 `entity.velocity`를 세팅하지 않고 **리지드바디에 힘만** 넣어 PhysX가 합산·적분(외력 합성 토대). 

**Spec:** `docs/superpowers/specs/2026-06-28-movement-motor-redesign-design.md`
**선행:** MoveSpeed 스탯(3b). 페이즈 머신(dash는 후속 슬라이스).

**Architecture:** `MovementSystem` 커널(LOP-Shared, 결정론 공유) = 마찰+never-brake 가속, *새 velocity 반환*. host `LOPMovementManager`(클·서) = `delta = new − current`를 `AddForce(VelocityChange)`, 점프 `vy` 세팅. `PhysicsComponent` `linearDamping=0`(커널이 수평 마찰 소유). **velocity 권위 = 리지드바디**(movement는 미세팅), entity.velocity = read-back 미러(기존 보정/스냅샷 무변경).

**Tech Stack:** Unity / C# / VContainer / UnityMCP / EditMode.

> ⚠️ **거동 변화 있음**(의도): 이동 가감속·점프가 바뀜. `maxAcceleration` 거의 즉발로 시작해 현 스냅감 근사. 검증=플레이 체감 + 공중 점프 갭 무회귀.

---

## 레포 / 도구 참조 (3 레포)
- **LOP-Shared:** `MovementSystem.cs` + 테스트
- **Client/Server:** `LOPMovementManager.cs`(바이트 동일 — 같은 Edit) + `PhysicsComponent.cs`(drag 0)
- **UnityMCP:** Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`).

**⚠️ EOL:** mass sed 금지. 편집=Edit.
**픽스처:** `git add -A` 금지. (Client: Room.unity/ProjectSettings. Server: Room.unity/ConfigureRoomComponent/GameRuleSystem.)
**컨벤션:** Microsoft C#. World 타입 풀 네임스페이스.

---

> ⚠️ **구현 중 단순화(friction→drag).** 최종 검증 결과 Unity 표준은 "모터(밀기) + 엔진 `linearDamping`(drag)로 정지". 커스텀 마찰 제거 — 커널은 **never-brake 가속만**, 정지·외력 감쇠는 **엔진 drag**(`linearDamping` 복원). 아래 Task의 `friction`/`drag=0` 서술은 *이 단순화로 대체*(MovementInput에 friction 없음, PhysicsComponent drag 유지). spec이 최신.

## 확정된 설계 결정
1. **모터 = never-brake 가속만(밀기), 정지=엔진 drag**. 입력=입력방향 moveSpeed까지 *채우기만*(외력 초과분 안 깎음), 정지·외력 감쇠=Rigidbody `linearDamping`(Unity 표준, 커스텀 마찰 없음). (왼쪽8+왼쪽→8 유지 / 왼쪽8+오른쪽→채움.)
2. **적용 = `AddForce(delta, VelocityChange)`**(수평만). movement는 `entity.velocity` **미세팅** → 외력(미래 대시/넉백)이 힘으로 합쳐짐(클로버 제거).
3. **점프 = `vy = jumpSpeed` 세팅**(`AddForce(up×(jumpSpeed−vy), VelocityChange)`). `jumpSpeed`=`masterData.JumpPower`(mass 1 가정상 기존 임펄스와 근사). grounded 게이트는 **이 슬라이스 미도입**(현재도 무게이트 — 후속).
4. **마찰 소유 = 커널**, `PhysicsComponent.linearDamping = 0`(이중 마찰 방지). 수직은 순수 중력(공기저항 0 — 표준, 낙하 미세 변화).
5. **velocity 권위 = 리지드바디**. movement 미세팅 → 보정(`SnapReconciler`/`ServerStateReconciler`)만 entity.velocity 세팅(→ Rigidbody). read-back(`AfterPhysicsSimulation`) 무변경.
6. **튜닝 상수**: `maxAcceleration`(거의 즉발, 예 100), `friction`(빠른 정지, 예 10) — host 상수, 후에 stat 가능.

---

## Task 0: 브랜치 (LOP-Shared / Client / Server)
- [ ] **Step 1: 상태 확인** — 3 레포 main, 픽스처 외 깨끗.
- [ ] **Step 2: 브랜치**
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/movement-motor; done
```

---

## Task 1: LOP-Shared — `MovementSystem` 모터 커널
- [ ] **Step 1: `Runtime/Scripts/Game/MovementSystem.cs` 교체** (Edit). `MovementInput`에 `maxAcceleration`/`friction`/`deltaTime` 추가, `ProcessMovement`를 마찰+never-brake 가속으로. `MovementResult`는 *새 수평 velocity*(Y passthrough) + 회전:
```csharp
public readonly struct MovementInput
{
    public readonly Vector3 currentVelocity;
    public readonly float horizontal, vertical, speed;
    public readonly float maxAcceleration, friction, deltaTime;
    public MovementInput(Vector3 currentVelocity, float horizontal, float vertical, float speed,
                         float maxAcceleration, float friction, float deltaTime) { /* 대입 */ }
}

public readonly struct MovementResult
{
    public readonly Vector3 velocity;      // 마찰+가속 적용된 새 velocity (Y는 currentVelocity.y passthrough)
    public readonly bool hasRotation;
    public readonly Vector3 rotation;
    public MovementResult(Vector3 velocity, bool hasRotation, Vector3 rotation) { /* 대입 */ }
}

public static MovementResult ProcessMovement(in MovementInput input)
{
    Vector3 horiz = new Vector3(input.currentVelocity.x, 0, input.currentVelocity.z);

    // 1) 마찰 — 전체 수평 속도를 0으로 서서히 (외력 초과분·정지)
    float speed = horiz.magnitude;
    if (speed > 0f)
    {
        float ns = Mathf.Max(speed - speed * input.friction * input.deltaTime, 0f);
        horiz *= ns / speed;
    }

    // 2) 가속(never-brake) — 입력 방향으로 moveSpeed까지만 채움, 절대 안 깎음
    Vector3 dir = new Vector3(input.horizontal, 0, input.vertical);
    bool hasRotation = dir.sqrMagnitude > 0f;
    Vector3 rotation = Vector3.zero;
    if (hasRotation)
    {
        dir.Normalize();
        float cur = Vector3.Dot(horiz, dir);
        float addSpeed = input.speed - cur;
        if (addSpeed > 0f)
        {
            horiz += dir * Mathf.Min(input.maxAcceleration * input.deltaTime, addSpeed);
        }
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        rotation = new Vector3(0, angle, 0);
    }

    return new MovementResult(new Vector3(horiz.x, input.currentVelocity.y, horiz.z), hasRotation, rotation);
}
```
- [ ] **Step 2: 컴파일(타입)** — 클·서 `refresh_unity`(scope=all, force) → `read_console`. host가 옛 시그니처를 써 에러 예상(Task 2·3에서 닫음). LOP-Shared 타입 에러 0 확인.

---

## Task 2: LOP-Shared — `MovementSystem` EditMode 갱신
- [ ] **Step 1: `Tests/EditMode/MovementSystemTests.cs` 갱신** (Edit, 기존 4 테스트). 새 `MovementInput`(maxAccel/friction/dt) 시그니처 + 모터 의미로 보정. 추가 케이스:
  - never-brake: currentVel 왼쪽8 + 왼쪽 입력 → 결과 수평속도 ≈ 8(마찰만큼만 감소), *5로 안 깎임*.
  - 반대전환: 왼쪽8 + 오른쪽 입력, maxAccel 큼 → 1틱에 오른쪽 ~5.
  - 마찰: 입력 없음 + currentVel → 속도 감소(friction·dt).
  - 가속 캡: 정지 + 입력, maxAccel 작음 → moveSpeed 미만(점진), 큼 → moveSpeed 1틱.
- [ ] **Step 2: EditMode 실행** — `run_tests`(`baegames.LOP.Shared.Tests.EditMode`) green.

---

## Task 3: Client — host 적용(AddForce) + 점프 + drag 0
- [ ] **Step 1: `LOPMovementManager.cs` — 힘 기반 적용** (Edit). `entity.velocity = result.velocity` 제거 → delta를 `AddForce(VelocityChange)`. 점프 `vy` 세팅. 상수 추가:
```csharp
        private const float MaxAcceleration = 100f;   // 거의 즉발(튜닝)
        private const float Friction = 10f;            // 정지 빠르기(튜닝)
        // ... ProcessInput 내부:
        var result = MovementSystem.ProcessMovement(new MovementInput(
            entity.velocity, horizontal, vertical, speed, MaxAcceleration, Friction, (float)Runner.Time.tickInterval));

        Vector3 delta = result.velocity - entity.velocity;      // 수평 delta (Y passthrough라 0)
        physicsComponent.entityRigidbody.AddForce(new Vector3(delta.x, 0, delta.z), ForceMode.VelocityChange);
        if (result.hasRotation) entity.rotation = result.rotation;

        if (jump)   // grounded 게이트는 후속
        {
            float jumpSpeed = characterComponent.masterData.JumpPower;
            var rb = physicsComponent.entityRigidbody;
            rb.AddForce(Vector3.up * (jumpSpeed - rb.linearVelocity.y), ForceMode.VelocityChange);
        }
```
> `Runner.Time.tickInterval` 사용(host는 Runner facade 접근 가능). `entity.velocity`는 더 이상 세팅 안 함.
- [ ] **Step 2: `PhysicsComponent.cs` — drag 0** (Edit). `entityRigidbody.linearDamping = 0.1f;` → `= 0f;` (커널이 마찰 소유).
- [ ] **Step 3: 컴파일** — Client `refresh_unity` → error 0.

---

## Task 4: Server — 동형 (바이트 동일 Edit)
- [ ] **Step 1: `LOPMovementManager.cs`** — 클·서 동일성 재확인 후 Task 3 Step 1과 같은 Edit.
- [ ] **Step 2: `PhysicsComponent.cs`** — drag 0 (서버 사본).
- [ ] **Step 3: 컴파일** — Server `refresh_unity` → error 0.

---

## Task 5: 종합 검증
- [ ] **Step 1: 양쪽 재스캔** — error 0.
- [ ] **Step 2: EditMode** — green(Movement 갱신 + 기존).
- [ ] **Step 3: 플레이(사용자)** — 이동이 *현재와 비슷한 반응성*(즉발 튜닝)이되, 가감속/마찰 자연스러움. 점프 정상. **공중 점프 갭 무회귀**(netcode), 보정 러버밴딩 무회귀. NRE 0. (필요 시 `MaxAcceleration`/`Friction`/`jumpSpeed` 튜닝.)

---

## Task 6: 커밋 + 머지
- [ ] LOP-Shared(MovementSystem+테스트) → Client(LOPMovementManager·PhysicsComponent·plan) → Server(동형) 순. `--no-ff`, 사용자 요청 시.

---

## Out of Scope (후속 슬라이스)
- **Dash** — 페이즈 머신 Active 창에서 `AddForce(Impulse)` forward(이제 모터가 안 지움). TbAbility dash 행 + 버튼.
- **넉백·바람·모래늪** — 외력 AddForce 사용처.
- **grounded 접지 판정·공중 정밀제어** — 표준 접지 도입(점프 게이트).
- **maxAcceleration/friction을 stat/마스터데이터로**.

---

## Self-Review
- **spec 커버리지(Slice 1):** 모터=Task1 / 테스트=Task2 / 힘 적용·점프·drag0=Task3·4 / 검증=Task5. ✅
- **never-brake:** dot-product addSpeed≤0이면 0(안 깎음) → 외력 초과분 유지. 마찰만 전체 깎음. ✅
- **클로버 제거:** movement가 entity.velocity 미세팅, AddForce delta만 → 외력 합성 토대. ✅
- **velocity 권위:** 리지드바디(힘 적분), entity.velocity=read-back/보정. 보정 메커니즘 무변경. ✅
- **거동 변화 정직:** 의도된 변화(가감속/점프). maxAccel 즉발로 근사, 플레이 튜닝. ✅
- **netcode:** 보정=관측 delta라 메커니즘 영향0, 공중점프갭 무회귀 검증. ✅
- **마찰 단일화:** 커널 마찰 + rb drag 0(이중 방지). 수직=순수 중력. ✅
```
