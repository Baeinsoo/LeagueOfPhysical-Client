# 판치기 — 세 번째 게임 모드

> **한 문장**: 별도 프로토타입으로 있던 판치기(`re5nardo/panchigi`)를 FlapWang · Flappy Race와
> 같은 자리에 꽂아 **턴제 대전 게임 모드**로 만든다. 동전 물리는 서버만 굴리고 클라는 보기만 한다.

## 1. 무엇을 가져오나

`github.com/re5nardo/panchigi` — Unity 6000.0.36f1 단독 프로젝트, 스크립트 10개 907줄.
동전을 대형으로 스폰하고, 판(`ForceElement`)을 터치·드래그·홀드로 쳐서 동전을 날린다.

**아직 게임이 아니다.** 점수도 승패도 턴도 없다. 동전이 장외로 나가면 판을 통째로 리셋할 뿐이고,
뒤집혔는지조차 세지 않는다. 그래서 이 작업의 큰 부분은 *포팅*이 아니라 **규칙을 만드는 것**이다.

## 2. 규칙 (확정)

| | |
|---|---|
| **인원** | 2~4명 |
| **진행** | 턴제 — 한 명씩 번갈아 한 번 친다 |
| **시작** | 동전은 **전부 같은 면**으로 놓인다 (그 면이 "시작 면") |
| **종료** | 모든 동전이 **시작 면의 반대**로 통일되는 순간 |
| **승자** | 그 상태를 만든 사람(= 마지막에 친 사람) |
| **나머지 등수** | 공동 꼴등 |
| **장외(낙)** | 나간 동전만 **초기 세팅**(스폰 위치·시작 면)으로 복귀 + 친 사람에게 **낙 1회** |
| **탈락** | 낙이 **N회** 쌓이면 그 플레이어는 탈락 — 턴 순서에서 빠진다. N은 설정값 |
| **아바타** | 없음 — 카메라가 판을 비추고, 자기 차례에 화면을 터치해 친다 |
| **판이 안 끝나면** | 매치 상한(턴 수 또는 시간)을 넘기면 **전원 무승부**(동일 등수) |
| **조준 지연** | 조준 제한시간을 넘기면 그 턴은 그냥 패스 |

동전은 언제든 다시 돌아갈 수 있다. 3개를 맞춰놨는데 마지막 하나를 뒤집으면서 다른 하나가 돌아가는
일이 게임의 긴장이다. 대신 판이 길어질 수 있어 위의 매치 상한이 짝으로 필요하다.

**낙 규칙의 결정 사항**

- 한 턴에 **여러 개가 나가도 낙 1회** — 한 번의 실수이므로
- 탈락자는 턴 순서에서 빠지고 남은 사람끼리 계속한다. **한 명만 남으면 그 사람이 승자**
- **등수는 탈락을 반영하지 않는다** — 승자 1등 / 나머지 공동 꼴등 그대로. 탈락은 "더 이상 이길 수
  없게 되는 것"이지 순위를 가르는 축이 아니다

낙 규칙이 있어야 **세게만 치는 전략**이 억제된다. 이게 없으면 힘껏 쳐서 운에 맡기는 것이 늘 이득이다.

**왜 이 규칙인가**: 소유권 이전도 점수판도 없이 **종료 조건 하나가 곧 승패**라, 서버가 물리를 다
굴린 뒤 세어야 할 것이 "모든 동전의 면" 하나뿐이다. 판정이 단순하면 순수 함수가 되고, 순수 함수면
테스트가 붙는다(§7).

## 3. 네트워크 — 서버가 굴리고 클라는 본다

### 3.1 왜 이 결정이 어려웠나

LOP는 *"PhysX는 같은 입력에도 매번 미세하게 다른 결과를 낸다"* 는 이유로 캐릭터 이동을 통째로
키네마틱 컨트롤러로 옮긴 프로젝트다(`netcode-redesign.md`, `world-core-connection-architecture.md`의
"이동 substrate"). 그런데 판치기의 재미는 **동전이 튀고 구르고 뒤집히는 다이나믹 물리 자체**다.

**턴제가 이 긴장을 푼다.** 한 번에 한 명만 치고, 결과는 물리가 멎은 뒤 확정되므로 예측도 롤백도
필요 없다. 즉 결정론이 필요한 이유 자체가 사라진다.

### 3.2 결정 — 예측 없음, 스냅샷만

```
클라 ──타격 1회(reliable)──> 서버
                              ├ 힘을 주고 PhysX를 굴린다
                              ├ 매 틱 동전 위치·회전을 스냅샷으로 뿌린다
                              └ 멎으면 면을 세어 승패·턴을 정한다
클라 <──────스냅샷──────────┘   보간해서 그린다
```

**판치기는 예측이 하나도 없는 첫 게임**이 된다. 클라는 손이 내려가는 연출만 즉시 로컬로 보여주고,
동전은 서버 스냅을 기다린다. 물리를 예측하면 클라마다 다른 궤적을 보다가 마지막에 순간이동한다 —
데미지 예측을 짓지 않기로 한 것과 같은 논리다.

검토했다가 접은 대안:

- **서버가 정지까지 다 굴린 뒤 궤적을 통째로 전송**(리플레이) — 손실에 강하고 결과가 이미 확정이지만,
  치고 나서 화면이 잠깐 멈춘다. 판치기는 "탁!" 하고 바로 튀는 맛이 크다.
- **클라도 같은 물리를 굴려 즉시 보여주고 서버 결과로 보정** — PhysX 비결정론 때문에 마지막에
  순간이동한다. `netcode-redesign.md`가 이미 배제한 길.

### 3.3 기존 동기화 파이프라인을 그대로 탄다

동전 하나 = `World.Entity` 하나. **와이어도 보간기도 새로 만들지 않는다.**

- 서버 `EntitySnapshotBroadcastSystem`은 **레지스트리의 모든 엔티티**를 순회한다(`entityRegistry.All`).
  동전을 등록만 하면 자동으로 실리고, MTU 서브셋 청킹도 이미 붙어 있다.
- `EntitySnap.rotation`은 오일러각이지만 클라가 다시 쿼터니언으로 만들어 `Quaternion.Slerp`로
  최단경로 보간한다(`SnapshotEntityInterpolator`). **구르는 동전도 정상 동작한다.**
- HP·어빌리티·상태이상 필드는 동전에서 0/빈 값이라 protobuf가 싣지 않는다.

> ⚠️ **레지스트리에 들어가는 모든 것은 `Transform` + `Velocity`를 가져야 한다.**
> `BuildAllEntitySnaps`가 부르는 `GetVelocity()`/`GetRotation()`에 null 가드가 없다. 동전도 플레이어도
> 해당된다.

### 3.4 동기화 모드 — `AllInterpolatedSyncPolicy`

`2026-08-23-entity-sync-mode-design.md`가 만든 이음새를 그대로 쓴다.

| 게임 | 내 것 | 남의 것 | 그 외 |
|---|---|---|---|
| FlapWang | Predicted | Interpolated | Interpolated |
| Flappy Race | Predicted | Predicted | Interpolated |
| **판치기** | — (아바타 없음) | — | **동전 = Interpolated** |

```csharp
/// 판치기는 서버가 굴린 물리를 보기만 한다 — 클라가 굴릴 규칙이 없다.
public class AllInterpolatedSyncPolicy : IEntitySyncPolicy
{
    public EntitySyncMode For(GameFramework.World.Entity entity) => EntitySyncMode.Interpolated;
}
```

`Simulated` 표식은 클라에서 크리에이터가 붙이지 않는다 — `EntityBinder`가 정책을 보고 붙인다(같은
spec의 결정). 판치기 클라에서는 아무에게도 안 붙고, 그래서 클라는 아무것도 굴리지 않는다.

**서버에서도 동전에는 붙이지 않는다.** 다른 게임의 서버 크리에이터는 *"서버는 모든 몸을 시뮬한다"* 며
`Simulated`을 붙이지만, 판치기 동전을 굴리는 것은 우리 시뮬이 아니라 PhysX다. 표식을 붙이면 시뮬이
동전을 자기가 굴릴 대상으로 착각한다.

### 3.5 방향이 반대다 — 판정은 이미 있는 값으로

캐릭터는 *우리가 계산 → rb에 밀어넣기*다(`MotionBridge`: "World.Transform이 진실원본, 물리 바디는
팔로워"). 동전은 **그 반대**여야 한다.

**그냥 두면 매 틱 PhysX와 싸운다.** `PhysicsSimulationSystem`이 `Simulated`로 거르지 않고
**모든 엔티티**를 밀어넣은 뒤 PhysX를 돌리기 때문이다:

```csharp
foreach (var entity in entityRegistry.All)   // ← 거르지 않는다
{
    motionBridge.PushMotion(entity);          // World.Transform → rb
}
physicsSimulator.Simulate(deltaTime);
```

동전은 kinematic이 아니므로 `PushMotion`이 else 가지로 간다 — `body.SetVelocity(velocity.Linear)`와
`body.SetRotation(transform.Rotation)`. 즉 **한 틱 낡은 World 값으로 rb의 속도와 회전을 매 틱
덮어쓴다.** 동전이 제대로 구르지 못한다.

> 이 else 가지는 **지금 죽은 코드**다. `PhysicsBodyFactory.Create` 호출부는 클·서 바인더 둘뿐이고
> 둘 다 `isKinematic: true`를 넘긴다 — 즉 현재 게임의 모든 몸이 kinematic이다. 판치기가 첫 사용자라
> **우리가 이 가지의 동작을 정의한다.**

#### 결정 — 새 표식을 만들지 않는다

"누가 이 엔티티를 굴리나"를 위한 마커(`PhysicsDriven` 등)를 검토했으나 **접었다.** 유니티 자신의
`isKinematic` 의미가 이미 *"내가 움직이냐 PhysX가 움직이냐"* 이고, 그 값은 **추상 포트
`PhysicsBody.IsKinematic`에 이미 있다.**

```csharp
// PhysicsSimulationSystem
foreach (var entity in entityRegistry.All)
{
    var body = entity.Get<PhysicsBody>();
    if (body != null && body.IsKinematic == false)
    {
        continue;   // PhysX가 진실원본 — 밀어넣지 않는다
    }
    motionBridge.PushMotion(entity);
}

physicsSimulator.Simulate(deltaTime);

foreach (var entity in entityRegistry.All)
{
    var body = entity.Get<PhysicsBody>();
    if (body != null && body.IsKinematic == false)
    {
        ReadBackMotion(entity);   // rb 위치·회전·속도 → World.Transform / Velocity
    }
}
```

읽어오려면 포트에 `GetPosition`/`GetRotation`/`GetVelocity`를 더해야 한다(지금은 `Set*`만 있다).

**두 표식이 각자 한 축씩 맡는다.**

| 엔티티 | 몸 | `Simulated` | 모션 출처 |
|---|---|---|---|
| 내 캐릭터·새 | kinematic | ✔ | 우리 코어 |
| 클라의 남·NPC | kinematic | ✘ | 스냅샷 보간 |
| **판치기 동전(서버)** | **dynamic** | ✘ | **PhysX** |

kinematic 몸 안에서 `Simulated` 유무가 *코어냐 보간이냐* 를 가르고, dynamic이면 그 질문 자체가
사라진다(PhysX가 가져간다).

> **`rb → World` 읽어오기는 우리 고유다.** 엔진들은 rb가 곧 진실원본이라 이 단계가 없다 — Unity DOTS는
> 물리 스텝이 `LocalTransform`을 직접 갱신하고, 클래식 Unity는 스크립트가 `rb.position`을 그냥 읽는다.
> 우리는 `World.Transform`이 **별도의 순수 C# 미러**라서 복사가 생긴다. 표준에서 빌려온 것이 아니라
> **엔진 비의존 코어를 둔 대가**다.

**정지 판정**: 모든 동전의 속도·각속도가 문턱 아래로 연속 N틱. 문턱과 N은 튜닝 값(§6).

## 4. 몸을 세우는 설정 — `PhysicsConfig`

동전은 **원반 + 회전 자유 + 다이나믹**인데, 지금 클·서 `EntityBinder`가 몸 설정을 하드코딩한다:

```csharp
bool isItem = kind.Kind == EntityType.Item;
worldEntity.Add<PhysicsBody>(PhysicsBodyFactory.Create(root, worldEntity, true, isItem));
//                                                                        ↑     ↑ 둘 다 바인더가 정한다
```

그리고 `PhysicsBodyFactory`는 캡슐 + 회전 고정 + Character 레이어 전용이고, `CapsuleShape`이 없으면
예외를 던진다.

### 4.1 검토했다가 접은 안 — 게임이 고르는 몸 팩토리

`IEntityBodyFactory`를 게임 스코프가 등록하는 형태(= `IEntitySyncPolicy`와 같은 모양)를 먼저
검토했으나 **접었다.** 컨벤션과 어긋난다:

> 인터페이스는 사이드가 **달라야** 하는 I/O 어댑터에만. … 시뮬 = `Register<Concrete>`,
> I/O = `Register<IFoo, Foo>`로 "동일해야/달라야"를 인코딩한다.
> — `world-core-connection-architecture.md`

`PhysicsBodyFactory` 자신의 주석도 *"클·서가 **같은 몸**을 써야 예측과 권위가 어긋나지 않는다"* 고
못박는다. 인터페이스를 끼우면 클·서에 구현체가 한 벌씩 생겨 **둘이 어긋날 여지를 새로 연다** —
그게 정확히 저 규칙이 막으려는 것이다.

`IEntitySyncPolicy`가 인터페이스인 건 **클라에만 존재하는 개념**이라서고, `IGameRuleSystem`은
**서버 전용**이라서다. 몸 세우기는 클·서가 같아야 하는 것이라 축이 다르다.

### 4.2 결정 — 엔진 설정을 한 컴포넌트로 모은다

경계는 **"엔진을 아는가"** 로 긋는다.

| 컴포넌트 | 담는 것 | 누가 읽나 |
|---|---|---|
| `CapsuleShape` / `DiscShape` | **순수 기하만** — 반지름·높이 등 | 코어 sweep(`KinematicMover`) + 콜라이더 치수 |
| **`PhysicsConfig`** (신규) | 엔진에 몸을 세울 때의 설정 **전부** | `PhysicsBodyFactory` |
| `PhysicsBody` | 몸 **그 자체**(런타임 핸들, 포트) | 이동·물리 시스템 |
| `Simulated` | **우리 코어**가 틱을 소유하나 | 월드 `Tick` / `SaveState` |

```csharp
namespace GameFramework.World
{
    public enum BodyKind { Static, Kinematic, Dynamic }

    /// <summary>이 엔티티를 물리 엔진에 어떻게 세울지. 게임(크리에이터)이 정한다.</summary>
    public class PhysicsConfig : Component
    {
        public BodyKind Kind { get; }
        public bool FreezeRotation { get; }
        public bool IsTrigger { get; }
        // 슬라이스 3에서 동전 질량·각감쇠·최대각속도가 여기 붙는다
    }
}
```

**왜 shape에 합치지 않나**: `CapsuleShape`은 순수 코어의 sweep도 읽는 값이다. 거기에 엔진 플래그를
얹으면 엔진을 모르는 코어 컴포넌트가 엔진 관심사를 지게 된다. 반대로 `PhysicsConfig`가 유니티
`Rigidbody`와 `Collider` **둘 다에** 적용되는 것은 문제가 아니다 — 그 둘로 나누는 것은 팩토리 안의
구현 세부고, 저작하는 쪽에서는 *"이 엔티티를 엔진에 어떻게 세우나"* 하나의 질문이다.

### 4.3 필수다 — 기본값을 지어내지 않는다

`PhysicsConfig`가 없으면 `PhysicsBodyFactory`는 **예외를 던진다.** 이 코드베이스가 이미 같은 입장을
취하고 있다:

> 몸 치수는 엔티티를 만드는 쪽(게임)이 정한다 — **여기서 기본값을 지어내면** 시뮬이 쓰는 몸과 다시
> 어긋난다. — `PhysicsBodyFactory`의 `CapsuleShape` 검사

조용한 기본값을 없앤 `targetMmr` 정리(2026-08-23)와 같은 원리다. 읽는 사람이 *"이 몸은 우리가 민다"* 를
크리에이터에서 바로 보게 되는 것이 진짜 이득이다.

**영향**: 기존 크리에이터 6곳(클·서 각 `CharacterCreator` / `FlappyBirdCreator` / `ItemCreator`)에
한 줄씩 추가. 지금 동작을 명시적으로 적는 것이라 **거동 변화는 없다.**

```csharp
// 기존 캐릭터·새
worldEntity.Add(new PhysicsConfig(BodyKind.Kinematic, freezeRotation: true, isTrigger: false));
// 기존 아이템
worldEntity.Add(new PhysicsConfig(BodyKind.Kinematic, freezeRotation: true, isTrigger: true));
// 판치기 동전 (서버) — 값은 TbPanchigiConfig에서
worldEntity.Add(new PhysicsConfig(BodyKind.Dynamic, freezeRotation: false, isTrigger: false));
```

### 4.4 `isTrigger`도 지금 옮긴다

지금은 바인더가 `kind.Kind == EntityType.Item`으로 트리거 여부를 정한다 — `isKinematic`과 **정확히 같은
하드코딩**이다. 판치기가 요구하는 것은 아니지만, 설정을 필수로 만들면서 절반만 데이터로 옮기면
**몸 설정의 절반은 크리에이터, 절반은 바인더**가 되어 일관성이 깨진다. `ItemCreator` 두 곳에 한 줄이면
끝나므로 같이 옮긴다.

> 서버 바인더의 `isItem`은 **`ItemTouchDetector` 부착**에도 쓰인다. 그건 몸이 아니라 감지기 얘기라
> 남지만, 거기도 `IsTrigger`를 보게 하면 그 자리의 주석(*"트리거인 것만 접촉을 감지할 수 있다"*)과
> 정확히 맞는다.

### 4.5 곁가지

- `UnityPhysicsBody` 생성자가 `CapsuleCollider`로 타입이 박혀 있어 **`Collider`로 넓혀야 한다.**
  `ComputePushOut`은 캡슐 전제라 동전에는 쓰지 않는다(겹침 밀어내기는 PhysX가 한다).
- `PhysicsBodyFactory`는 `root.layer`를 무조건 `Character`로 둔다. 동전은 다른 레이어여야 하므로
  레이어도 `PhysicsConfig`가 들거나 shape 옆에 자리가 필요하다 — 슬라이스 2에서 확정한다.

**게임 스코프에 새로 등록할 것은 없다.** 크리에이터가 붙이는 컴포넌트가 늘 뿐이다.

### 4.6 산업 표준 매핑

- **모양과 몸을 나누는 것** — 유니티가 `Collider`와 `Rigidbody`를 별개 컴포넌트로 두는 선과 같다.
  Unity Physics(DOTS)도 `PhysicsCollider` / `PhysicsMass` / `PhysicsVelocity`로 나눈다.
- **`BodyKind { Static, Kinematic, Dynamic }`** — Box2D `b2BodyType`, Unity `RigidbodyType2D`와 같은
  세 값·같은 어휘.
- **`IsTrigger`** — Box2D의 `isSensor`에 해당. 엔진마다 이름만 다르고 개념은 같다.

## 5. 입력 — 타격

원본 조작을 유지한다: 화면을 터치해 판 위 지점을 찍고, **끄는 방향**이 수평 힘, **누른 시간**이
수직 힘이다.

**턴에 딱 한 번**이라 매 틱 흐르는 입력 스트림(command frame)에 실을 이유가 없다.

```
클라 ──PanchigiStrikeToS { 타격점, 방향, 세기 }──> 서버   (reliable, 단발)
```

서버가 셋을 검증한다: **내 차례인가 / 판 위를 쳤나 / 세기가 범위 안인가.** 어긋나면 무시하고 로그를
남긴다. 조준 UI가 세기를 미리 보여주려면 클라도 같은 상수를 알아야 하므로 `TbPanchigiConfig`는 클·서
공용으로 둔다.

### 5.1 원본 물리는 그대로 옮기지 않는다

원본 `ForceElement`를 그대로 포팅하면 서버가 죽는다. 읽으면서 확인한 것:

**심각**

| | |
|---|---|
| **레이캐스트 폭주** | `gridDivisions = (100, 160)` = 충돌 한 건당 **16,000회**, 그것도 `OnCollisionStay`라 매 물리 스텝마다. `enableGridRaycast` 플래그가 있지만 `ForceElement`가 **아예 보지 않는다** |
| **세기가 해상도에 비례** | `AddForce`가 접촉점마다 힘을 더해, *정밀도* 노브인 `gridDivisions`가 **총 세기까지 바꾼다.** 이 상태로는 튜닝이 불가능 |
| **엉뚱한 지점에 힘** | 레이캐스트에 레이어 마스크가 없어 다른 동전·판을 맞을 수 있는데, 그 `hit.point`를 원래 대상의 힘 적용점으로 쓴다 |
| **전역 설정 변경** | `Physics.gravity`(10배) · `defaultSolverIterations` · `targetFrameRate`를 전역으로 바꾼다. LOP에선 다른 모드와 아이템 물리까지 끌려간다 |

**버그**

- `collisionMap.Add()` — 같은 키가 있으면 예외(인덱서여야 함)
- `OnCollisionExit`의 `contactPointMap[key].Clear()` — Enter 없이 Exit이 오면 `KeyNotFoundException`
- `InputController.UpdateInput/EndInput` — `TryGetValue` 결과를 안 보고 씀 → NRE
- `contactPoints` 필드 — Clear만 되고 아무것도 안 담기는 죽은 코드
- `CalculateForceFalloff`가 양쪽 호출부에서 주석 처리됨 → `forceFalloffType`·`maxForceDistance`·
  `minForceDistance`·커브가 전부 죽은 노브
- `holdTime` 상한 없음 → 10초 누르면 힘 10

**LOP 규약 위반**

- 레거시 `Input` 클래스 — LOP는 New Input System 강제(`architecture-guidelines.md`)
- `#if UNITY_EDITOR` / `#elif UNITY_IOS||UNITY_ANDROID` → **데스크톱 빌드는 입력이 아예 없다**
- `PanchigiSettings`가 자기를 스스로 생성하는 `DontDestroyOnLoad` 싱글턴 — LOP는 VContainer +
  마스터데이터

### 5.2 결정 — 동전당 임펄스 하나 + 토크

접촉점 수천 개에 `AddForceAtPosition`을 넣는 대신, **동전마다 임펄스 하나와 그 적용 지점의 오프셋**으로
간다. 원본이 접촉점을 잘게 나눈 이유는 회전(토크)을 얻으려는 것으로 보이는데, 그건 중심에서 어긋난
지점에 임펄스 하나만 줘도 나온다.

얻는 것:

- 레이캐스트 **16,000회 → 동전당 1회**
- **세기 노브와 정밀도 노브가 분리**되어 튜닝이 가능해진다
- 계산이 **순수 함수**가 되어 LOP-Shared에 놓고 EditMode 테스트를 붙일 수 있다

감각은 유지된다 — 타격점에서 멀수록 약하게(거리 falloff), 중심에서 어긋날수록 많이 돈다.

## 6. 마스터데이터와 씬

| 무엇 | 내용 |
|---|---|
| `TbGameMode` | 행 1개 — `Panchigi`, 2~4명, `Assets/Scenes/Panchigi.unity` |
| `TbMap` | 행 1개 — 그 모드에 속한 맵 (없으면 로비 목록에 안 뜬다) |
| `TbPanchigiSetup` | **id = 참가 인원** → 동전 수, 대형 |
| `TbPanchigiConfig` | id = 1 단일 행 → 세기 상한 · 정지 문턱 · 조준 제한시간 · 매치 상한 · **탈락 낙 횟수(N)** |
| 씬 | 클·서 각 1개 + 각각 `PanchigiLifetimeScope` |

**동전 수는 인원마다 다르다.** 지금은 배선만 하고 실제 숫자는 붙여본 뒤 엑셀에서 조정한다 —
`TbFlappyConfig`(단일 행 튜닝 테이블)와 같은 방식이고, 숫자를 바꾸는 데 코드 변경이 없다.

`PlayableGameProvider`가 *scene_path 있고 맵 있는* 모드를 자동으로 로비 목록에 올리므로 **로비 쪽은
손댈 것이 없다.**

## 7. 이 게임이 쓰는 이음새 — 전부 기존 것

새로 만드는 인터페이스는 없다.

| | 왜 인터페이스인가 | 판치기가 등록할 것 |
|---|---|---|
| `IWorld` | 게임마다 시뮬이 다름 | `PanchigiWorld` |
| `ICharacterCreator` | 게임마다 플레이어 몸이 다름 | `PanchigiPlayerCreator` (몸 없음, `Transform`+`Velocity`+`Ownership`만) |
| `IGameRuleSystem` | **서버 전용** 개념 | `PanchigiRuleSystem` |
| `IEntitySyncPolicy` | **클라 전용** 개념 | `AllInterpolatedSyncPolicy` |

## 8. 턴 상태 기계 (서버)

```
[조준 대기] ──타격 도착──> [굴러가는 중] ──정지 감지──> [판정]
     ↑                                                    │
     │                                          전부 뒤집혔나?
     │                                             ├ 예 → [종료] 친 사람 승
     └────────── 다음 사람 차례 ────────────────────┴ 아니오
```

**판정 단계** (순서대로)

1. 장외로 나간 동전이 있으면 **초기 세팅**(스폰 위치·시작 면)으로 복귀시키고, 방금 친 사람에게
   **낙 1회**를 매긴다(몇 개가 나갔든 1회)
2. 그 사람의 낙이 **N회**에 도달하면 **탈락** — 턴 순서에서 뺀다. 한 명만 남으면 그 사람이 승자
3. 각 동전의 면을 읽는다 — 업벡터가 위를 보면 앞, 아래면 뒤
4. **전부 시작 면의 반대**면 → 방금 친 사람이 승자, 나머지 공동 꼴등
5. 아니면 다음 (탈락하지 않은) 사람으로 턴 넘김

> 순서가 중요하다. **낙 판정이 승리 판정보다 먼저**다 — 동전을 떨어뜨리면서 남은 것들이 우연히
> 전부 뒤집히는 경우, 그 판은 이긴 것이 아니라 낙이다(장외 동전이 초기 세팅으로 돌아오므로 애초에
> "전부 뒤집힘"이 성립하지 않는다).

승패가 물리 결과로 정해지고 그 물리는 서버만 굴리므로, 상태 기계는 서버에만 둔다. 클라는 "지금 누구
차례인지"만 알면 되고 그건 스냅샷에 실어 보낸다.

## 9. 테스트

핵심 판정이 전부 순수 함수라 LOP-Shared EditMode로 덮인다.

| 무엇 | 입력 → 출력 |
|---|---|
| 면 판정 | 회전(쿼터니언) → 앞/뒤 |
| 종료 판정 | 면 배열 + 시작 면 → 끝났나 |
| 턴 순서 | 인원 · 현재 차례 · 탈락자 → 다음 차례 |
| 탈락 판정 | 낙 횟수 · N → 탈락했나 / 한 명만 남았나 |
| 힘 커널 | 타격점 · 방향 · 세기 · 동전 위치 → 임펄스 · 토크 |
| 정지 판정 | 속도·각속도 배열 → 멎었나 |

클라 쪽은 `AllInterpolatedSyncPolicy`를 기존 `EntitySyncPolicyTests` 옆에 붙인다.

## 10. 슬라이스

각 단계가 눈으로 확인된다.

| | 무엇 | 끝났다는 증거 |
|---|---|---|
| **1** | 마스터데이터 + 빈 씬 + 스코프 | 로비에 "판치기"가 뜨고 **입장이 된다**(빈 판) |
| **2** | `PhysicsConfig`(+`DiscShape`) 도입 + 동전 스폰 + PhysX→World 되읽기 | 동전이 떨어져 쌓이는 게 **두 클라 모두에** 똑같이 보인다 |
| **3** | 타격 입력 + 힘 커널(원본 물리 재작업) | 칠 수 있고 동전이 튄다 |
| **4** | 턴 상태 기계 + 면·종료 판정 + 낙·탈락 | **게임이 된다** — 이기고 진다 |
| **5** | HUD(누구 차례·남은 시간) + 결과 화면 연동 | 완성 |

**1번이 특히 값어치가 크다** — 배선이 맞는지를 게임 로직 없이 먼저 증명하고 들어간다.

## 11. 범위 밖

- **관전** — 자기 차례가 아닐 때 할 일. 지금은 그냥 본다
- **동전 종류별 차이**(무게·크기) — 원본에 소스가 4개 있지만 전부 같은 물성으로 시작
- **대형 선택 UI** — 대형은 인원에서 자동으로 온다
- **라운드 로테이션** — `MatchSceneResolver.CurrentRoundIndex`가 아직 항상 첫 라운드다(전 모드 공통)

## 12. 산업 표준 매핑

- **턴제 + 서버 권위 물리 + 상태 스트리밍** — 당구·볼링류 온라인 게임의 표준 구성. 결정론이 필요 없는
  이유가 "한 번에 한 명"이라는 점도 같다.
- **`EntitySyncMode.Interpolated` 전용 게임** — Unity Netcode for Entities의 `GhostMode.Interpolated`만
  쓰는 구성에 대응. 예측이 필요한 상호작용이 없는 게임의 정석.
- **서버=다이나믹 / 클라=kinematic + 보간** — Photon Fusion의 프록시 기본값과 같다
  (*"프록시에서는 리지드바디를 kinematic으로 둔다 — 모든 물리 상호작용은 authority에서 처리되도록"*).
- **몸 세우기는 데이터로, 팩토리는 하나** — 클·서 공유 구체가 같은 몸을 만들게 하는 배치.
  인터페이스로 사이드를 가르지 않는다(`world-core-connection-architecture.md`의 "구체 공유" 규칙).
  컴포넌트 단위 매핑은 §4.6 참고.

## 13. 열린 것

- **동전 수·대형의 실제 숫자, 탈락 낙 횟수(N)** — 붙여보고 튜닝. 테이블 자리는 §6에서 잡는다
- **매치 상한을 턴 수로 할지 시간으로 할지** — 4번 슬라이스에서 붙여보고 결정
- **카메라** — 판 전체를 비추는 고정 카메라로 시작. 조준이 답답하면 그때 검토
