# 입력-as-데이터 — 입력을 World 컴포넌트로 + 이동을 `LOPWorld.Tick`으로

**Date:** 2026-07-02
**Branch (제안):** `feature/input-as-data` (GameFramework + LOP-Shared + Client + Server — 원자적 한 묶음)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (4e·Stage④) · 선행 [Motion 권위 World 이전](2026-07-01-stage4-motion-world-authority-design.md) · [4e-1 속도 적용 World](2026-07-02-4e-velocity-apply-to-world-design.md) · [4e-2 대시 World 직접+브릿지](2026-07-02-4e-dash-world-direct-velocity-bridge-design.md) · 보류된 [4d IInputSource](2026-06-30-slice4-input-source-port-design.md) · 메모리 [[world-core-runner-world-naming]]

## Goal

플레이어 입력(h/v/jump)을 **World 컴포넌트(데이터)** 로 만들어 호스트가 매 틱 채우고, 이동 로직(현 `LOPMovementManager`)을 **`LOPWorld.Tick`(Mutation) 안으로 이주**한다. 이동이 "호스트가 메서드 인자로 입력을 넘겨 부르는 함수"에서 "시뮬이 엔티티의 입력 데이터를 읽어 스스로 도는 시스템"이 된다. 동작 100% 보존.

**왜 keystone인가:** 이동을 `LOPWorld`로 못 옮기던 유일한 이유 = 입력이 World 데이터가 아니라 메서드 인자였기 때문(4e 스코핑 벽1). 또 Stage④ 예측/롤백의 input replay는 "입력=저장·재생 가능한 데이터"가 전제 — velocity 권위 이전이 모션의 keystone이었듯, 이게 입력 쪽 keystone.

## 현재 상태 (실측 2026-07-02)

- **클라** `PlayerInputManager.ProcessInput`(틱마다, host `ProcessInput` 페이즈): 입력 있으면 → 대시 중 h/v 0화(캡처 게이트) → 서버 송신 → `ApplyInput` = `movementManager.ProcessInput(entity, …, h, v, jump)` + `abilityActivator.TryActivate` → seq 등록. 입력 없으면 → `movementManager.ProcessInput(…, 0, 0, false)`(제동) + redundancy 재전송.
- **서버** `LOPRunner.ProcessInput`: `EntityInputComponent` 보유 엔티티(플레이어만, AI 제외) 순회 → `GetInput(tick)` → miss면 0 → `movementManager.ProcessInput(…)` → 입력 있으면 어빌리티 발동 + seq 회신.
- **`LOPMovementManager`**(클·서 동일 2벌): 대시 Active면 early-return(적용 게이트) → Stats에서 MoveSpeed → 공유 커널 `MovementSystem.ProcessMovement` → `entity.velocity =`(파사드) 수평 세팅 + jump면 `velocity.y = characterComponent.masterData.JumpPower` + `entity.rotation =`(파사드). `MaxAcceleration = 100f` const.
- **`LOPWorld.Mutation`**(Shared): `EntityRegistry.All` 순회 → `abilitySystem.Tick` + `statusEffectSystem.Tick`. 이동은 없음.
- **회전 동기**: `entity.rotation =`(파사드) → PropertyChange → `PhysicsComponent.OnPropertyChange`가 `rb.rotation` 반응 세팅. velocity는 4e-2에서 반응 제거·브릿지(`PushVelocityToPhysics`, BeforePhysicsSimulation)로 전환됨. **`SyncPhysics`(AfterPhysicsSimulation)가 매 틱 rb.rotation→World로 되쓴다.**
- **스탯 시드**: 클·서 creator가 `worldStats.BaseStats[MoveSpeed] = masterData.Speed`. JumpPower는 스탯이 아니라 host가 masterData에서 직접 읽음.
- **로컬/플레이어 판별**: 클라 creator `isUserEntity`(69행), 서버 creator `isPlayer`(63행) 분기 이미 존재.

## 설계

### ① LOP-Shared — `PlayerInput` World 컴포넌트 신설

`Runtime/Scripts/Game/PlayerInput.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이번 틱의 조종 입력(데이터). 호스트가 world.Tick 전에 매 틱 채우고(무입력 틱=0),
    /// 이동 시스템이 Mutation에서 읽는다. 틱 사이 이월 없음 — 항상 이번 틱 값.
    /// 입력으로 조종되는 엔티티(클라=내 캐릭, 서버=플레이어)에만 붙는다.
    /// </summary>
    public class PlayerInput : GameFramework.World.Component
    {
        public float Horizontal { get; set; }
        public float Vertical { get; set; }
        public bool Jump { get; set; }
    }
}
```

- **명명 = `PlayerInput` (웹 검증 후 사용자 확정, 2026-07-02)**: 업계 두 계보 — Quake `usercmd_t`/Source `CUserCmd`("user command") vs Unity 시뮬 계열(DOTS Netcode 공식 예제가 문자 그대로 `PlayerInput : IInputComponentData`, Quantum `Input`). 우리 proto wire 메시지도 이미 `PlayerInput`이라 **컴포넌트=와이어 커맨드 쌍둥이**(DOTS에선 같은 타입) 관계가 이름에서 드러남. 검토했던 `InputCommand`는 어느 표준의 정확한 용어도 아니라 철회.
- **공통 도메인 커맨드 `LOP.UserCommand` 신설 (구현 중 사용자 구조 리뷰로 확장, 2026-07-03)**: "proto(와이어)를 게임 도메인에서 쓰면 안 된다"(connection-arch 와이어 격리 규칙) 지적에 따라, 와이어 독립 커맨드 레코드(seq/h/v/jump/ability)를 LOP-Shared에 신설. **클라 캡처 홀더(구 `LOP.PlayerInput` Model → 삭제)와 서버 버퍼 원소가 이 한 타입을 공유.** proto 변환은 송신 어댑터(PlayerInputManager 송신부)/수신 어댑터(Game.Input.MessageHandler)에서만. 명명 웹 검증: **Unity 공식 FPS Sample이 문자 그대로 `UserCommand`**(+최근 3커맨드 redundancy까지 동일 설계) · Quake `usercmd_t` · Source `CUserCmd` · Unreal Mover `InputCmd` · Overwatch "command" — command 계열 만장일치, 정확 식별자 일치는 UserCommand.
- **abilityId는 안 넣는다(이번 범위)**: 어빌리티 발동은 host 경로(`AbilityActivator.TryActivate`) 유지. 발동 예측 통합은 Stage④ 예측 슬라이스에서.

### ② GameFramework — `EntityStatType.JumpPower` 추가

이동이 World로 들어가면 JumpPower도 World 데이터여야 한다(공유 LOPWorld는 MasterData 패키지를 못 봄). **MoveSpeed와 똑같은 패턴**으로 스탯화:

- `EntityStatType`에 `JumpPower` 추가(GameFramework, additive).
- 클·서 creator가 `worldStats.BaseStats[JumpPower] = characterComponent.masterData.JumpPower` 시드(기존 MoveSpeed 시드 바로 옆).
- 부수 이점: 점프력 버프/디버프가 자연히 가능해짐(모디파이어).

### ③ LOP-Shared — `MovementSystem`을 시스템으로 (커널 유지 + 인스턴스 Tick 추가)

기존 static 커널 `ProcessMovement`는 그대로 두고, 같은 클래스를 non-static으로 바꿔 World-only 인스턴스 메서드를 추가한다(별도 클래스명을 새로 만들지 않음 — AbilitySystem처럼 DI 싱글톤):

```csharp
public class MovementSystem
{
    private const float MaxAcceleration = 100f;   // LOPMovementManager에서 이사

    private readonly GameFramework.World.StatsSystem statsSystem;

    public MovementSystem(GameFramework.World.StatsSystem statsSystem) { this.statsSystem = statsSystem; }

    /// <summary>PlayerInput를 읽어 이동을 적용한다(입력 조종 엔티티만). 무입력=0 제동 포함.</summary>
    public void Tick(GameFramework.World.Entity entity, float deltaTime)
    {
        var input = entity.Get<PlayerInput>();
        if (input == null) return;                                   // 비조종(AI/원격/아이템) — 스킵

        if (AbilitySystem.HasActiveMotionEffect(entity)) return;      // 대시 중 — 대시가 속도 주도(적용 게이트)

        var stats = entity.Get<GameFramework.World.Stats>();
        float speed = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.MoveSpeed);

        var velocity = entity.Get<GameFramework.World.Velocity>();
        var result = MovementSystem.ProcessMovement(new MovementInput(
            velocity.Linear.ToUnity(), input.Horizontal, input.Vertical, speed, MaxAcceleration, deltaTime));

        Vector3 v = velocity.Linear.ToUnity();
        v.x = result.velocity.x;
        v.z = result.velocity.z;
        if (input.Jump)
        {
            v.y = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.JumpPower);
        }
        velocity.Linear = v.ToNumerics();

        if (result.hasRotation)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            transform.Rotation = Quaternion.Euler(result.rotation).ToNumerics();
        }
    }

    public static MovementResult ProcessMovement(in MovementInput input) { /* 기존 그대로 */ }
}
```

- 기존 EditMode 29 테스트(`MovementSystem.ProcessMovement` static 호출)는 무변경으로 green 유지.
- `LOPWorld.Mutation`에 이동 패스를 **어빌리티 루프보다 먼저, 별도 루프로** 추가(현 순서 보존 — 지금도 이동[host ProcessInput]이 어빌리티 페이즈 전진[world.Tick]보다 먼저다. 대시 발동 틱의 게이트 타이밍이 이 순서에 걸려 있음):

```csharp
protected override void Mutation(long tick, float deltaTime)
{
    foreach (var entity in EntityRegistry.All) _movementSystem.Tick(entity, deltaTime);
    foreach (var entity in EntityRegistry.All) { _abilitySystem.Tick(entity, tick); _statusEffectSystem.Tick(entity, tick); }
}
```

### ④ 호스트 — 입력을 데이터로 쓰기만 (클·서)

**클라 `PlayerInputManager`** (`IMovementManager` 의존 제거):
- 입력 틱: `ApplyInput`의 `movementManager.ProcessInput(…)` → `entityRegistry.Get(id).Get<PlayerInput>()`에 h/v/jump 세팅. `TryActivate`·송신·seq·redundancy·캡처 게이트(대시 중 h/v 0화) 전부 무변경.
- 무입력 틱: `movementManager.ProcessInput(…, 0, 0, false)` → PlayerInput에 0/0/false 세팅. redundancy 재전송 무변경.

**서버 `EntityInputComponent` → `InputBufferComponent` rename (구현 중 사용자 결정, 2026-07-02)**: 역할이 정확히 per-client **input buffer**(Overwatch)/command buffer(DOTS)인데 이름에 buffer가 없었음 → 표준 용어 + 형제 컴포넌트(`XxxComponent`) 컨벤션으로 정정. 서버 5파일 기계적 치환, git mv로 meta GUID 보존.

**버퍼 원소 = 인풋 페이로드로 정정 (사용자 구조 리뷰, 2026-07-02)**: 종전 버퍼는 와이어 봉투(`PlayerInputToS`)째 저장(수신 핸들러가 풀었는데 도로 싸서 넣던 Phase 3 유물). 표준은 네트워크 경계에서 봉투를 버리고 **인풋 타입 자체를 버퍼링**(Quake usercmd 큐 / DOTS command 버퍼 원소 = 인풋 struct) → `SortedDictionary<long, global::PlayerInput>`로 변경, `GetInput`도 페이로드 반환. 봉투의 `EntityTransform`은 버퍼에 안 들어감(서버는 클라 보고 transform 미사용 — netcode §6.4 정합).

**서버 `LOPRunner.ProcessInput`** (`IMovementManager` 의존 제거):
- `movementManager.ProcessInput(…)` → 그 엔티티의 PlayerInput에 h/v/jump(miss=0) 세팅. `entityTransform` 지역변수는 이동에만 쓰였으므로 함께 제거. 어빌리티 발동·seq 회신 무변경.

**크리에이터** — 조종 엔티티에만 컴포넌트 부여(기존 분기 재사용):
- 클라 `CharacterCreator`: `if (isUserEntity) worldEntity.Add(new PlayerInput());` → 원격 캐릭터는 PlayerInput 없음 = 이동 패스 스킵 = ServerStateReconciler가 계속 주도(현행과 동일).
- 서버 `CharacterCreator`: `if (isPlayer) worldEntity.Add(new PlayerInput());` → AI는 없음 = EnemyBrain 직접 velocity 유지(현행과 동일).

**삭제**: `LOPMovementManager`(클·서) + DI 등록(`GameLifetimeScope` 클 65행/서 60행) + GameFramework `IMovementManager.cs`(유일 구현·유일 소비가 이번에 사라짐 — grep 재확인 후).

### ⑤ 회전 브릿지 — `PushVelocityToPhysics`에 rotation 포함 (클·서, 필수)

**발견한 함정:** 이동 패스가 `World.Transform.Rotation`에 *직접* 쓰면 PropertyChange가 안 울려 rb.rotation이 갱신 안 되는데, `SyncPhysics`(AfterPhysicsSimulation)가 매 틱 **rb.rotation(구값)→World로 되써서 새 회전을 덮어버린다.** 그래서 velocity처럼 rotation도 명시적 브릿지가 필요:

- `LOPEntity.PushVelocityToPhysics` → rotation도 push(`rb.rotation = Quaternion.Euler(rotation)`). 메서드명은 `PushMotionToPhysics`로 교체(velocity 단독이 아니게 되므로).
- `PhysicsComponent.OnPropertyChange`의 **rotation case 제거**(velocity와 동일 논리 — 4e-2가 "position/rotation도 브릿지로"를 후속 예고했던 그것의 rotation 절반). Simulate 전 rotation write = 이동 패스(Mutation) + AI(UpdateAI, world.Tick 전) → 브릿지(BeforePhysicsSimulation)가 둘 다 커버. reconciler의 rotation 보정(End)은 다음 틱 브릿지 반영 = 4e-2 velocity와 동일한 등가 논리.
- position은 이동이 안 쓰므로 반응 PropertyChange 유지(이번 무변경).

## 동작 보존 / 검증

- **값 등가**: 이동 계산·게이트·jump 세팅이 같은 코드(커널)와 같은 데이터(Stats/Abilities/Velocity — 모두 이미 World)로, 같은 틱 안 Simulate 전에 실행된다. 바뀌는 건 (a) 호출 위치가 host `ProcessInput` 페이즈 → `world.Tick` Mutation(그 사이 페이즈 = 클라 InterpolateEntity[빈]/UpdateEntity[Status 프레젠테이션]/UpdateAI[빈], 서버 UpdateEntity/UpdateAI — **plan에서 velocity/rotation 소비 없음 Explore 재확인**), (b) velocity/rotation 쓰기가 파사드 → World 직접(velocity는 이미 브릿지 경로, rotation은 ⑤로 커버).
- **PropertyChange 소비자**: `LOPEntityView`가 velocity/position 변경으로 Run 애니를 토글하는데, 이동의 직접 쓰기는 발화 안 함 → **`SyncPhysics`가 매 틱 파사드로 되쓰며 발화**(4e-2 대시가 이미 이 경로로 검증됨). 애니 갱신이 같은 틱 안 약간 뒤로 갈 뿐.
- **틱당 populate 불변식**: 호스트가 매 틱 PlayerInput를 덮어쓴다(무입력=0) → 이월 없음. 클라 무입력 제동·서버 miss=no-input 정책 그대로.
- **검증**: 4 repo 컴파일 클린(양쪽 `read_console`) + EditMode(기존 29 + 신규 MovementSystem.Tick 테스트) green. 플레이: 이동/회전(facing)/점프/대시(발동 틱 게이트 포함)/무입력 칼정지, reconciliation(본인+타인) 무회귀, 서버 AI 이동 정상, 콘솔 에러 0.
- **신규 테스트 (이번엔 가능)**: `MovementSystem.Tick`은 World-only 순수 → LOP-Shared EditMode에 추가 — PlayerInput 없으면 무변경 / 입력 → velocity·rotation 세팅 / jump → y=JumpPower 스탯 / 대시 Active면 스킵 / 무입력(0) → 수평 제동.

## 산업 표준 매핑

- **입력=엔티티 위 데이터 컴포넌트**: Quake `usercmd_t`(bg_pmove가 `playerState + usercmd → playerState'` — 모션 슬라이스가 playerState를, 이 슬라이스가 usercmd를 완성) · Source `CUserCmd` · Unity DOTS Netcode `ICommandData`/`IInputComponentData`(입력을 엔티티 컴포넌트로 저장, 시스템이 읽음) · Photon Quantum `Input`(폴링→시뮬 소비).
- **호스트=수집, 시뮬=소비**: connection-arch의 Phase 1 Collection(의도 큐잉, 상태 변경 X)—Phase 2 Mutation(적용) 분리 그대로. 보류했던 4d의 "적용을 source 밖으로"가 이 슬라이스로 실현되는 셈(포트 없이 데이터로).

## Out of Scope

- **abilityId의 PlayerInput 편입 / 발동 예측** — Stage④ 예측 슬라이스.
- **입력 히스토리 버퍼(input replay용 command 스트림)** — Stage④ Snapshot/Restore와 함께.
- **position 브릿지화(반응 PropertyChange 전폐)** — 후속.
- **Physics.Simulate의 sim 소유(4e-c)** · **DamageEffectHandler World화(벽2)** — 별도.
- **4d `IInputSource` 포트** — 입력이 데이터가 되면 "provider가 데이터를 준다" 모양이 자연 성립; 포트 신설은 Stage④에서 필요 시.

## Open Questions — 전부 해소 (사용자 확정 2026-07-02)

1. **컴포넌트 명명 = `PlayerInput`** — 웹 검증(DOTS Netcode 공식 예제 `PlayerInput : IInputComponentData` / Quantum `Input` / Quake `usercmd_t` / Source `CUserCmd`) 후 Unity 계열 + 자체 proto 짝 일관으로 확정. 클라 캡처 홀더는 `PlayerInputCapture`로 rename.
2. **JumpPower 스탯화** 확정 (MoveSpeed 짝 일관).
3. **LOPMovementManager/IMovementManager 완전 삭제** 확정.

## 진행

- [x] 브레인스토밍(이전 세션 — 4e 스코핑에서 슬라이스 확정) + 현재 코드 실측
- [x] 이 spec 작성
- [x] 사용자 리뷰 — 명명 웹 검증 반복. **최종 확정: `InputCommand`(순수 데이터) + `InputBuffer`(World 컴포넌트, 엔티티마다 커맨드 스트림 저장) + `InputBufferSystem`(Enqueue/Consume/Prune/Trim). 조달 정책은 사이드별 호스트(PlayerInputManager/LOPRunner). proto는 아직 `PlayerInput`/`PlayerInputToS` 유지(리네임은 별도 후속 — 재생성 리스크 격리).**
- [x] **구현 완료 + 컴파일 클린(클·서) + EditMode 44/44 + 플레이 검증 + 4 repo main 머지·push (2026-07-03).** GameFramework 453f094 / Shared 4c54fd1 / Client a732bb9 / Server ac7b3ca.

## 구현 후기 (설계 대비 진화)

- **최종 구조 = 3층**: `InputCommand`(순수 데이터, usercmd) → `InputBuffer`(World 컴포넌트, 엔티티 위 커맨드 스트림) → `InputBufferSystem`(버퍼 연산). 정책은 사이드 호스트. **spec 초안의 단일 `PlayerInput{h,v,jump}` 슬롯 컴포넌트를 폐기**하고, 사용자 지적("순수 인풋 데이터 vs 컴포넌트 혼재")으로 데이터/컴포넌트를 분리 + 버퍼를 엔티티 컴포넌트로 통일(DOTS command stream 정렬, Stage④ replay 대비).
- **서버 `InputBufferComponent`(구 LOPComponent) 폐기 → World `InputBuffer`로 통일.** 지터정렬(tick−2)/prune/timing은 서버 `LOPRunner.ProcessInput`에, dedup/window는 클라 `PlayerInputManager`에. `InputBufferSystem`은 얇은 버퍼 연산(HealthSystem↔Health처럼 InputBuffer↔그 시스템 짝).
- **proto 리네임(2단계) 미실시** — 동작무관 코스메틱 + MessageId 재생성 리스크라 별도 슬라이스로 분리. 도메인=InputCommand / 와이어=PlayerInput 공존(어댑터 변환).
- **Stage④ 이월(의도)**: (a) "커맨드 소비→Current 확정"을 호스트 → `LOPWorld.Tick` Collection 페이즈로(롤백 재소비). (b) reconciliation replay 소비자. (c) 클라 SetCurrent → Consume(틱별) 통일.
