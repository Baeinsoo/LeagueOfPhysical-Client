# 판치기 슬라이스 3 — 타격

> **한 문장**: 판을 끌어서 놓으면 동전이 튀고 구르고 뒤집힌다. 원본의 힘 계산은 그대로 옮기지 않고,
> *같은 결과를 내면서 훨씬 싼* 커널로 다시 만든다.

**상위 spec**: [`2026-08-24-panchigi-game-mode-design.md`](2026-08-24-panchigi-game-mode-design.md) —
게임 모드 전체. 이 문서는 그 §10의 3번 슬라이스를 푼 것이다.

**전제**: 슬라이스 1~2 완료(입장·동전 스폰·PhysX→World 되읽기). 2026-08-24 8레포 머지·배포·두 클라
검증 완료.

---

## 1. 끝났다는 증거

판을 내려다보는 화면에서 **원기둥 동전들이 보이고**, 판을 끌어서 놓으면 **동전이 튀고 구르고
뒤집힌다.** 두 클라가 같은 것을 본다.

**턴은 없다.** 아무나 아무 때나 여러 번 친다 — 이 슬라이스의 목적은 물리 감각 튜닝이고, 자유롭게
반복해서 쳐 봐야 튜닝이 된다. 턴 상태 기계는 슬라이스 4가 가져간다.

---

## 2. 두 단계로 나눈다

| | 무엇 | 기능 변화 |
|---|---|---|
| **3-0** | 물리 포트 정리 — 물리 질문과 엔티티 찾기를 분리 | **없음** (리팩터) |
| **3-1** | 타격 입력 + 힘 커널 + 임시 몸·카메라 | 본편 |

3-0을 먼저 두는 이유: 타격 판정이 "이 자리 위 첫 동전이 누구냐"를 물어야 하는데, 그 질문을 넣을
자리가 지금 잘못돼 있다(아래 §3). 본편 코드를 잘못된 자리 위에 얹지 않는다.

---

## 3. 슬라이스 3-0 — 물리 포트 정리

### 3.1 지금 무엇이 어긋나 있나

`IOverlapQuery`가 **두 가지 다른 일을 한 덩어리로** 한다.

```csharp
// GameFramework/Runtime/Scripts/Physics/IOverlapQuery.cs
string[] OverlapSphere(Vector3 center, float radius);   // (1) 물리에 묻고 (2) 엔티티 이름으로 답한다
```

(2)(콜라이더 → 엔티티) 때문에 이 포트는 **사이드별 구체**를 요구한다. 실제 구현
(`LOPOverlapQuery`)은 맞은 콜라이더마다 `GetComponentInParent<LOPActor>()`로 부모를 거슬러 올라간다.

**유니티는 이렇게 하지 않는다.** `Physics.Raycast`는 `RaycastHit`을 주고 거기 `.collider`가 붙어
있을 뿐, "그게 네 게임의 무엇인지"는 유니티가 모른다. 매핑은 부르는 쪽 일이다.

> **박제 — 잘못 짚었던 것.** 설계 중 "이 분할은 클·서가 서로 다른 코드를 써야 해서 생긴 의도적
> 구분"이라고 설명했는데 **사실이 아니었다.** `IOverlapQuery` 구현은 **서버에 하나뿐**이고 클라
> 것은 만들어진 적이 없다(데미지 판정이 서버에서만 나서 필요가 없었다). 파일 주석의 "각 레포에
> 존재(의도적 사이드 분기)"는 *계획*이지 현실이 아니다. 근거를 확인하지 않고 구조를 정당화한
> 것이라 여기 남긴다.

### 3.2 바꿀 모양

**층 1 — 물리는 유니티가 하는 그대로** (`GameFramework.Physics`, 클·서 공유 구체 한 벌)

```csharp
public readonly struct CollisionHit
{
    public readonly bool HasHit;
    public readonly float Distance;
    public readonly Vector3 Normal;
    public readonly Vector3 Point;
    public readonly Collider Collider;   // 추가 — 유니티 RaycastHit.collider와 같은 자리
}

public interface ICollisionQuery
{
    CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                             Vector3 direction, float distance, int layerMask);      // 있음
    CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask);  // 추가
    CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask);       // 이사
}
```

- 메서드 이름은 전부 **유니티에서 차용**한다(`Physics.CapsuleCast` / `Physics.Raycast` /
  `Physics.OverlapSphere`). 기존 두 개가 이미 그렇게 돼 있어 짝이 맞는다.
- `GameFramework.Physics`는 이미 `UnityEngine.Vector3`를 쓴다 — 엔진 비의존 어셈블리가 아니므로
  `Collider`를 담아도 규약 위반이 아니다. (엔진 비의존은 `GameFramework.World` 쪽 이야기다.)
- `OverlapSphere`가 `Collider[]`가 아니라 `CollisionHit[]`인 이유: 반환 타입이 하나로 통일돼야
  아래 확장 메서드가 세 질문 모두에 걸린다. `Physics.OverlapSphere`는 접촉 좌표를 주지 않으므로
  `Point`/`Normal`/`Distance`는 0으로 채우고 `Collider`만 담는다 — **이 규약을 XML 주석에 명시한다.**

**층 2 — 엔티티는 그때그때 찾는다** (LOP-Shared)

```csharp
public static class CollisionHitExtensions
{
    /// <summary>맞은 몸의 엔티티 id. 엔티티가 아닌 것(판·지형)을 맞았으면 null.</summary>
    public static string GetEntityId(this GameFramework.Physics.CollisionHit hit)
        => hit.Collider != null
            ? hit.Collider.GetComponentInParent<EntityActor>()?.entityId
            : null;
}
```

**표(레지스트리)를 만들지 않는다.** 콜라이더 instanceId → entityId 표를 두면 조회는 빨라지지만
**몸이 사라질 때 지우는 관리 포인트**가 생기고, 안 지우면 유니티가 id를 재사용할 때 엉뚱한 엔티티가
나온다. 호출 빈도가 타격당 수백 회(분당 몇 번)·어빌리티당 몇 회 수준이라 그 대가를 치를 이유가 없다.
필요해지면 그때 넣는다.

**`EntityActor`** — `entityId`를 들고 있는 부분만 LOP-Shared로 올린다. 지금은 클·서에 `LOPActor`가
한 벌씩 있고 그 부분이 **완전히 같다**(클라 것만 뷰 관련이 더 붙어 있다). 양쪽 `LOPActor`가
`EntityActor`를 상속하고, 클라는 지금처럼 뷰를 더 붙인다.

```csharp
// LeagueOfPhysical-Shared/Runtime/Scripts/Entity/EntityActor.cs
namespace LOP
{
    /// <summary>엔티티에 붙는 몸의 신원. 물리 히트에서 엔티티를 되찾는 실마리다.</summary>
    public class EntityActor : UnityEngine.MonoBehaviour
    {
        public string entityId { get; private set; }
        public void SetEntityId(string entityId) => this.entityId = entityId;
    }
}
```

### 3.3 없어지는 것

- `GameFramework/Runtime/Scripts/Physics/IOverlapQuery.cs` — 삭제
- `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPOverlapQuery.cs` — 삭제
- 서버 `GameplayInstaller`의 `IOverlapQuery` 등록 — 삭제
- 양쪽 `LOPActor`의 `entityId`/`SetEntityId` 본문 — `EntityActor`로 올라감

### 3.4 옮겨 앉는 것 — `DamageEffectHandler`

```csharp
// 전
string[] hitIds = overlapQuery.OverlapSphere(casterTransform.Position, effect.Range);
foreach (string id in hitIds) { ... }

// 후
var hits = collisionQuery.OverlapSphere(casterTransform.Position.ToUnity(), effect.Range, CharacterLayerMask);
var seen = new HashSet<string>();
foreach (var hit in hits)
{
    string id = hit.GetEntityId();
    if (id == null || seen.Add(id) == false) continue;   // 엔티티 아닌 것 / 같은 엔티티 중복
    ...
}
```

- **중복 제거는 이제 호출부가 한다.** 옛 `LOPOverlapQuery`는 `HashSet`으로 중복을 없앴다 — 한
  엔티티가 콜라이더를 여럿 가질 수 있기 때문이다. 그 책임이 포트에서 나오므로 `DamageEffectHandler`가
  같은 일을 한다. **이 줄을 빠뜨리면 한 대상이 여러 번 맞는다.**
- 레이어 마스크가 포트 인자로 나온다. 옛 구현이 안에 박아두던 `LayerMask.GetMask("Character")`를
  호출부 상수로 옮긴다.
- `DamageEffectHandlerTests`의 가짜 구현도 새 시그니처로 바꾼다.

### 3.5 3-0이 끝났다는 증거

- 클·서 컴파일 통과, `IOverlapQuery`를 부르는 곳 0
- LOP-Shared EditMode 전부 초록 — 특히 `DamageEffectHandlerTests`가 **고치기 전과 같은 결과**
- 실플레이에서 근접 공격이 전과 똑같이 맞는다(기능 변화 0이 이 단계의 계약)

---

## 4. 슬라이스 3-1 — 타격

### 4.1 조작

원본 조작을 유지한다: **판 위 한 점을 누르고 → 끌고 → 뗀다.**

| 무엇 | 어디서 나오나 |
|---|---|
| **타격점** | 뗀 순간의 화면 좌표를 판으로 레이캐스트한 월드 점 |
| **수평 힘** | (뗀 점 − 누른 점), 판 평면 위 변위 |
| **수직 힘** | 누른 시간(초). **상한을 둔다** |

`holdTime` 상한은 원본에 없어서 10초 누르면 힘이 10이 됐다. `TbPanchigiConfig.HoldTimeMax`로 자른다.

입력은 **New Input System의 `Pointer.current`** 로 받는다 — 마우스와 터치가 한 코드로 처리되고,
LOP 규약이 레거시 `Input`을 금지한다. 원본의 `#if UNITY_EDITOR / #elif UNITY_IOS||UNITY_ANDROID`
분기는 쓰지 않는다(그 분기 때문에 원본은 데스크톱 빌드에서 입력이 통째로 죽었다).

조준 중에는 **누른 점 → 지금 점 선분과 세기 게이지**를 로컬로 그린다. 서버 왕복 없음.

### 4.2 와이어

```proto
// LeagueOfPhysical-Shared/Protos/PanchigiStrikeToS.proto
syntax = "proto3";
import "ProtoVector3.proto";

message PanchigiStrikeToS
{
    ProtoVector3 strike_point = 1;   // 판 위 월드 좌표
    ProtoVector3 drag_delta   = 2;   // 판 평면 변위 (y = 0)
    float        hold_time    = 3;   // 초. 클라가 이미 상한 적용
}
```

- **reliable 단발.** 턴에 한 번뿐이라 매 틱 흐르는 입력 스트림에 실을 이유가 없다.
- **신원은 싣지 않는다** — `ClientMessage<T>.Session`에서 나온다(`StatAllocationToS`와 같은 계약).
- MessageId는 다음 빈 번호(현재 14까지 사용). `MessageIds.cs` 재생성 시 **기존 번호가 밀리지
  않는지 diff로 확인**한다 — 부모 스크립트가 `MessageIds.cs`를 지워 ID가 밀린 전례가 있다.

### 4.3 서버 — 검증

`PanchigiStrikeMessageHandler`(서버, `MessageHandlerBase` 상속)가 받는다.

| 검사 | 어긋나면 |
|---|---|
| 이 세션이 이 매치 참가자인가 | 무시 + `LogWarning` |
| `strikePoint`가 판 사각형 안인가 | 무시 + `LogWarning` |
| `holdTime`이 `[0, HoldTimeMax]`인가 | 무시 + `LogWarning` |
| `dragDelta` 길이가 `StrikePowerMax` 이하인가 | 무시 + `LogWarning` |

클라가 이미 상한을 적용해 보내지만 **서버는 클라를 믿지 않는다.** 클램프가 아니라 **거절**이다 —
클램프하면 조작된 값이 조용히 게임에 들어오고 로그도 안 남아 치팅 시도를 못 본다.

판 사각형은 `Board` GameObject의 콜라이더 bounds에서 읽는다(하드코딩 금지).

### 4.4 힘 커널 — 순수 함수

**위치**: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs` (static, 순수).
컨텍스트 없는 순수 커널이므로 `*System` 이름을 붙이지 않는다(프로젝트 컨벤션).

#### 원본이 실제로 한 계산

```csharp
// ForceElement.AddForce(Vector3 force, Vector3 forcePos)
foreach (동전 in collisionMap)
  foreach (point in 그 동전 밑 그리드 접촉점)          // gridDivisions = 100 x 160 = 16,000
      falloff = 1 / (1 + rate * |point.xz - forcePos.xz|^2)
      rb.AddForceAtPosition(force * multiplier * falloff, forcePos)   // 적용 지점이 항상 forcePos
```

**모든 부분 힘이 같은 지점에 걸린다.** 그러므로 합은

```
AddForceAtPosition(force * multiplier * Sum(falloff),  forcePos)
```

와 **정확히 같다.** 즉 그리드는 회전을 만든 게 아니라 **스칼라 배수 Sum(falloff) 하나**를 만들고
있었다. 회전은 원래부터 "동전 중심에서 벗어난 지점에 힘을 준 것"에서 나온다.

그 Sum(falloff)가 담고 있던 정보는 **"동전이 판에 실제로 닿은 면적을, 타격점까지의 거리로 가중한
값"** 이다. 이것이 격자가 존재한 진짜 이유다:

- 동전이 **판 끄트머리에 반만 걸침** → 겹치는 사각형이 작아 점이 적다 → 힘이 작다. **원본에서 동작함**
- 동전 **위에 동전** → 위로 쏜 레이가 아래 동전을 먼저 맞는다 → 위 동전은 점이 없어야 한다.
  **원본 버그로 동작하지 않음**

두 번째가 새는 이유: `points.Add(hit.point)`가 **맞은 게 누구인지 확인하지 않는다.** B를 계산하는
중에 레이가 A를 맞아도 그 점이 B의 목록에 들어간다(`hit.collider.gameObject == collision.gameObject`
검사 없음). 그래서 포개진 동전이 바닥에 온전히 닿은 것처럼 힘을 다 받는다.

#### 새 커널

```
힘벡터 F = ( dragDelta.x * HorizontalForceMultiplier,
             holdTime    * ForceMultiplier,
             dragDelta.z * HorizontalForceMultiplier )

동전마다:
    발자국 원(반지름 DiscShape.Radius) 위에 고정 K개 샘플         K = CoverageSamples
    각 샘플 p:
        p가 판 사각형 밖이면                        탈락      (끄트머리 걸침)
        p에서 아래로 짧게 Raycast, 첫 히트 != 판     탈락      (포개짐 — 원본 버그 수정)
        살아남으면 falloff = 1 / (1 + FalloffRate * |p.xz - strikePoint.xz|^2)
    덮임 = Sum(falloff) / K                                    0 ~ 1

    임펄스 = F * 덮임
    적용 지점 = strikePoint                                     원본 그대로
```

> **묻는 것은 "내가 제일 위인가"가 아니라 "내가 판에 닿아 있나"다.** 이 둘은 동전이 포개지는
> 순간 정반대 답을 낸다 — 판에 깔린 동전은 위에 뭔가 있어서 "제일 위"가 아니고, 얹힌 동전이야말로
> "제일 위"다. 그래서 **샘플 자리(동전 중심 높이)에서 아래로 짧게** 쏘고, **처음 맞은 것이 판일
> 때만** 살린다:
>
> - 자기 자신은 걸리지 않는다 — PhysX는 **콜라이더 안에서 출발한 레이**에 그 볼록 콜라이더를
>   돌려주지 않는다. id 비교가 필요 없다.
> - `GetEntityId() == null` 이 "엔티티가 아닌 것 = 판"이라는 뜻이다. 엔티티 id가 나오면 다른 동전
>   위에 얹혀 있다는 뜻이라 탈락.
> - `HasHit == false`(아무것도 못 맞음)는 **판에 닿은 것으로 치지 않는다.** 이 검사가 없으면 허공에
>   뜬 동전이 `GetEntityId() == null` 하나만으로 통과해 버린다.
> - 사거리는 **동전 몸에서 뽑는다** — 상수가 아니다. 판에 닿아 있다면 중심이 몸의 대각 절반
>   (`|(r, t/2, r)|`)보다 높이 뜰 수 없으므로 그만큼만 쏜다. 이보다 짧으면 **모로 선 동전이 영영
>   안 맞고**(중심이 반지름만큼 올라간다), 길면 얹혀 있는 동전도 그 아래 판까지 닿아 통과한다.

**얻는 것 세 가지:**

| | |
|---|---|
| **세기 ↔ 정밀도 분리** | K가 상수이고 합을 K로 나누므로, 샘플을 늘려도 세기가 안 변한다. 원본은 `gridDivisions`가 세기까지 바꿔 튜닝이 불가능했다 |
| **포개짐 수정** | 샘플마다 "이 자리에서 판에 닿아 있나"를 직접 확인한다 |
| **비용** | 16,000회 x **매 물리 스텝** → K x 동전수 = 약 128회 x **타격당 1번**. 네 자릿수 차이 |

**적용 지점을 `strikePoint`로 두는 이유**: 원본과 같은 감각을 유지하기 위해서다. 동전 중심에서 멀리
떨어진 지점에 힘을 주면 토크 팔이 길어져 많이 돈다 — "타격점에서 멀수록 약하게, 중심에서 어긋날수록
많이 돈다"가 이것이다. *물리적으로 더 옳은* 선택(접촉 패치 무게중심)은 감각이 달라지므로 채택하지
않는다. 감각이 이상하면 그때 바꾼다.

#### 샘플 배치 — Vogel 나선(황금각)

K개 샘플은 **동전 발자국 원 위에 결정론적으로** 깐다. 난수를 쓰지 않는다 — 같은 타격이 같은 결과를
내야 하고, 클라가 나중에 조준 미리보기를 붙일 여지를 남긴다.

```
i번째 샘플 (i = 0 .. K-1):
    r     = radius * sqrt((i + 0.5) / K)
    theta = i * 2.39996323          // 황금각(라디안) = pi * (3 - sqrt(5))
    p     = coinCenter + (r*cos(theta), 0, r*sin(theta))
```

**Vogel 나선(해바라기 배치, Vogel 1979)** 은 원판 위에 K개를 고르게 까는 표준 방법이다. `sqrt`가
반지름 방향 밀도를 균일하게 만들고(면적 비례), 황금각이 각도 방향을 겹치지 않게 벌린다. 링 방식과
달리 **K가 무엇이든 성립**하므로 `CoverageSamples`를 테이블에서 자유롭게 튜닝할 수 있고, "지원하지
않는 K" 같은 예외 경로가 아예 생기지 않는다.

`K <= 0`만 **예외를 던진다** — 0으로 나누기이고, 테이블이 잘못 채워졌다는 뜻이다.

#### 시그니처

```csharp
public static class PanchigiStrikeKernel
{
    /// <summary>동전 하나에 줄 임펄스. 살아남은 샘플이 없으면 Vector3.Zero(칠 필요 없음).</summary>
    public static System.Numerics.Vector3 ComputeImpulse(
        in StrikeInput input,                     // strikePoint, dragDelta, holdTime
        in StrikeTuning tuning,                   // 배수 3종 + FalloffRate
        System.Numerics.Vector3[] liveSamples,    // 살아남은 샘플들의 월드 좌표
        int liveCount,                            // liveSamples 중 유효 개수
        int totalSamples);                        // K

    /// <summary>동전 발자국 위 샘플 좌표(결정론). 호출부가 이걸로 판 밖·포개짐을 걸러 낸다.</summary>
    public static void BuildSamples(System.Numerics.Vector3 coinCenter, float radius,
                                    System.Numerics.Vector3[] buffer);
}
```

**레이캐스트는 커널 밖**이다 — 호출부(서버)가 `ICollisionQuery.Raycast` + `GetEntityId()`로 걸러
살아남은 샘플만 넘긴다. 그래야 커널이 순수 함수로 남아 EditMode 테스트가 붙는다.

#### 테스트 (LOP-Shared EditMode)

| 무엇 | 기대 |
|---|---|
| 살아남은 샘플 0 | 임펄스 0 |
| 전부 살아남고 타격점 = 동전 중심 | 최대 세기(falloff = 1) |
| 타격점이 멀수록 | 임펄스 크기 단조 감소 |
| **K를 2배로, 살아남은 비율 동일** | **임펄스 불변** — 원본이 못 하던 것 |
| `holdTime` 0 | 수직 성분 0(수평만) |
| 같은 입력 두 번 | 완전히 같은 결과 |
| `BuildSamples` | K개 전부 발자국 원 안, 같은 입력이면 같은 배치, K=1이어도 동작 |
| `K <= 0` | 예외 |

### 4.5 서버 — 임펄스 적용

`PhysicsBody`에 메서드 하나를 추가한다. **지금은 `SetVelocity`밖에 없어 회전을 줄 수단이 없다** —
뒤집기가 여기에 달렸다.

```csharp
// GameFramework/Runtime/Scripts/World/PhysicsBody.cs
/// <summary>월드 좌표 한 점에 임펄스를 준다. 중심에서 벗어난 지점이면 회전이 함께 생긴다.</summary>
public abstract void AddImpulseAtPosition(Vector3 impulse, Vector3 worldPoint);

// UnityPhysicsBody (LOP-Shared)
public override void AddImpulseAtPosition(Vector3 impulse, Vector3 worldPoint)
    => _rigidbody.AddForceAtPosition(impulse.ToUnity(), worldPoint.ToUnity(), ForceMode.Impulse);
```

`ForceMode.Impulse`인 이유: 타격은 한 순간의 충격이지 지속되는 힘이 아니다. `Force`로 주면 한 물리
스텝 동안만 적용돼 결과가 `deltaTime`에 끌려간다.

**키네마틱 몸에는 무시하고 경고한다** — 다이나믹이 아니면 PhysX가 임펄스를 무시하는데, 조용히
넘어가면 "쳤는데 안 움직인다"의 원인을 런타임에 추적해야 한다.

### 4.6 임시 몸과 카메라

슬라이스 1~2가 미뤄둔 것 중 **조준에 필요한 최소치만** 여기서 처리한다. 아트 에셋 도입과 전용
`PanchigiMap.unity`는 계속 미룬다.

| 무엇 | 어떻게 |
|---|---|
| **동전이 보이게** | `Cylinder` 프리미티브. 치수는 `DiscShape`(반지름 0.15 / 두께 0.04) 그대로 |
| **판에 두께** | 지금 두께 0 `Plane` + `MeshCollider` → **얇은 Box**. 레이 시작점이 면의 어느 쪽인지 부동소수점에 달리는 문제를 없애고, 슬라이스 4의 "낙" 판정도 명확해진다 |
| **카메라** | 판을 45도쯤에서 내려다보는 고정 위치. 지금은 `(0,1,-10)` 회전 0이라 판을 거의 옆에서 본다 |
| **플레이어 스폰** | `(0,-10,0)` 임시값 유지 — 아바타가 없어 화면에 영향이 없다 |

동전 프리미티브는 `PanchigiRuleSystem`의 `CoinVisualId` 상수를 채우는 방식으로 넣는다(그 자리가
이미 "아트가 들어오면 이 상수만 채우면 된다"로 남겨져 있다).

### 4.7 마스터데이터

`#PanchigiConfig.xlsx`에 커널 노브 5개를 추가한다(id = 1 단일 행, 클·서 공용 — 조준 UI가 세기를
미리 보여주려면 클라도 같은 상수를 알아야 한다).

| 컬럼 | 뜻 | 시작값 |
|---|---|---|
| `force_multiplier` | 수직(누른 시간) 배수 | 튜닝 |
| `horizontal_force_multiplier` | 수평(끈 거리) 배수 | 튜닝 |
| `falloff_rate` | 거리 감쇠율 — 클수록 급격히 약해짐 | 튜닝 |
| `coverage_samples` | 동전당 샘플 개수 K | 13 |
| `hold_time_max` | 누른 시간 상한(초) | 튜닝 |

기존 컬럼(`strike_power_max`, `rest_*`, `aim_timeout_sec`, `match_turn_limit`, `drop_out_limit`)은
그대로 둔다.

> **테이블 절차**: `gen.sh`는 클라 패키지 · 서버 패키지 · `lop-backend` **세 곳**에 쓴다. 컬럼만
> 추가하는 것이므로 `TableFiles` 목록 갱신은 필요 없다(새 테이블이 아님). `.meta`는 유니티 재스캔을
> 기다린 뒤 add한다.

---

## 5. 배포

**게임서버 코드가 바뀌므로 `gameserver-deploy`를 반드시 함께 돌린다.** 백엔드 배포에 게임서버는
딸려오지 않는다 — 슬라이스 1~2에서 이걸 빠뜨려 "매칭은 되는데 방이 4초 만에 Error"를 겪었다.
마스터데이터도 바뀌므로 `backend-deploy`(매칭서버)도 함께.

---

## 6. 범위 밖

- **턴 상태 기계 / 면 판정 / 낙·탈락 / 승패** — 슬라이스 4
- **HUD(누구 차례·남은 시간) / 결과 화면** — 슬라이스 5
- **동전 아트 · 전용 `PanchigiMap.unity` · `formation` 해석** — 아트 도입 시
- **`DiscShape.Thickness`가 콜라이더에 안 닿는 문제** — 유니티가 `CapsuleCollider`를 구로 뭉갠다.
  타격 감각에 영향이 있으면 그때 실린더 메시 콜라이더로 바꾼다
- **클라 예측** — 판치기는 예측이 하나도 없는 게임이다(상위 spec 3절). 클라는 조준선만 로컬로 그린다

---

## 7. 산업 표준 매핑

- **물리 히트가 좌표와 "맞은 것"을 함께 나르고, 게임 오브젝트로의 매핑은 부르는 쪽이 한다** —
  유니티 `RaycastHit.collider`, 언리얼 `FHitResult::GetActor()`와 같은 배치. 물리 계층이 게임
  엔티티를 알지 않는다.
- **메서드 이름 차용** — `Raycast` / `OverlapSphere` / `CapsuleCast`는 `UnityEngine.Physics`의 이름
  그대로. 기존 두 개가 이미 그렇게 돼 있어 짝이 맞는다.
- **한 번의 충격은 `ForceMode.Impulse`** — 유니티 표준. 지속력(`Force`)과 구분된다.
- **접촉 면적을 샘플로 근사** — 물리 엔진의 접촉 패치 계산과 같은 발상을, 필요한 정밀도만큼만.
- **순수 커널 + 엔진 쿼리 포트 분리** — 이 프로젝트의 기존 배치와 같다
  (`MovementSystem.ProcessMovement` ↔ `ICollisionQuery`).

---

## 8. 열린 것

- **배수·감쇠율의 실제 숫자** — 붙여보고 튜닝. 테이블 자리는 4.7절에서 잡는다
- **적용 지점을 접촉 패치 무게중심으로 옮길지** — 지금은 원본대로 `strikePoint`. 끄트머리에 걸친
  동전이 기울어지지 않는 게 어색하면 그때 검토
- **`ICollisionQuery`가 이제 세 질문을 갖는데 이름이 `Collision`인 것** — 유니티는 이 전부를
  `Physics` 하나에 둔다. 지금은 기존 이름을 유지한다(리네임이 판치기가 요구하는 일이 아님)
