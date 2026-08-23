# 엔티티 동기화 모드 — 원격을 보간할지 예측할지 게임이 고른다

> **한 문장**: 원격 엔티티를 "지연된 시간에서 보간해 보여줄지" "내 시간선에서 같이 굴릴지"를
> **게임이 고르는 설정**으로 만들고, Flappy Race는 후자를 골라 새끼리 몸싸움을 클라가 예측하게 한다.

## 1. 왜 하나

B2-d2 최종 리뷰에서 드러난 사실: **클라는 새끼리 몸싸움을 구조적으로 예측하지 않는다.**
`FlappyWorld.CollectBirds`가 `Simulated`만 모으는데 클라에선 그게 내 새 하나뿐이라, 몸싸움
이중 루프가 짝을 하나도 만들지 못한다. 그래서 몸이 겹치는 동안 클라에서는 내 새가 상대를 통과하고,
서버 스냅마다 하드 보정이 걸린다(육안으로는 자리싸움 중 러버밴딩).

짝만 만들어 주면 되는 문제가 아니다. **남의 새는 클라 월드에 있긴 하지만 시간선이 다르다** —
`RemoteEntityInterpolator`가 보간 지연만큼 *과거* 위치를 쓴다. 그 위치로 충돌을 풀면 "화면에 안 닿았는데
밀리는" 판정이 된다. 그래서 이 슬라이스의 진짜 질문은 **"남의 새를 어느 시점 위치로 볼 것인가"** 다.

## 2. 결정 — 원격도 예측한다 (그리고 그 선택을 게임이 한다)

접촉이 게임성인 장르는 원격을 과거에 두지 않는다.

- **로켓 리그**는 내 차뿐 아니라 남의 차와 공까지 전부 클라가 예측하고 사실상 매 프레임 재시뮬한다.
  남의 입력은 모르니 **input decay**로 감쇠시킨다(첫 예측 프레임 100% → 2/3 → 1/3 → 0).
- **Photon Fusion**은 프록시를 기본 kinematic으로 두어 상호작용을 아예 막는다. 상호작용을 원하면
  *Forecast Physics* — "받은 위치를 **모든 플레이어의 로컬 시간으로 외삽**하고 프록시도 예측 시간에 시뮬".
  엔진 문서가 "보간된 프록시로는 제대로 밀 수 없다"를 명시하는 셈이다.
- 고전적 함정도 확인됐다: **선형 dead reckoning은 도달 불가능한 위치(벽 너머)를 만든다.** 우리는 외삽에도
  맵 sweep을 함께 돌리므로 이 함정을 피한다(아래 §5).

**우리 게임은 조건이 유난히 좋다.** 새의 움직임은 전진 속도 상수 · 중력 상수 · 낙하 상한이라
**미지수가 플랩 타이밍 하나**뿐이다. "남의 새는 안 누른 걸로 굴린다"가 대부분 맞고, 틀리는 것은 상대가
누른 그 틱뿐이며 다음 스냅이 교정한다. 로켓 리그가 input decay로 감쇠시키는 자리에서 우리는
**감쇠할 입력조차 없다.**

그리고 이 선택은 **게임마다 다르다**. FlapWang은 지금의 보간이 맞고(접촉이 게임성이 아니다),
Flappy는 예측이 맞다. 그래서 하드코딩이 아니라 **게임이 고르는 설정**으로 만든다.

## 3. 구조 — 설정(모드)과 표식(Simulated)을 나눈다

### 3.1 모드와 정책

```csharp
public enum EntitySyncMode
{
    /// <summary>서버 스냅 두 개 사이를 지연된 시간에서 보간한다. 예측 없음.</summary>
    Interpolated,

    /// <summary>내 시간선에서 같이 굴린다. 스냅이 오면 그 틱으로 맞추고 지금까지 다시 굴린다.</summary>
    Predicted,
}

/// <summary>이 게임이 각 엔티티를 어떻게 따라갈지 정한다. 게임 스코프가 등록한다.</summary>
public interface IEntitySyncPolicy
{
    EntitySyncMode For(GameFramework.World.Entity entity);
}
```

정책은 **엔티티마다** 답한다. 같은 게임 안에서도 종류별로 달라야 하기 때문이다 — 아이템은 서버가
몰아주는 물건이라 클라가 굴릴 규칙이 없다.

정책 구현체는 "이게 내 엔티티인가"를 알아야 하므로 로컬 유저 컨텍스트(`IGameDataStore.userEntityId`)를
주입받는다. 판정 재료는 그 하나와 엔티티가 이미 들고 있는 것(`EntityKind`)뿐이다 — 정책이 게임 상태를
뒤지기 시작하면 그건 정책이 아니라 로직이다.

| 게임 | 내 캐릭터/새 | 남의 캐릭터/새 | 아이템 |
|---|---|---|---|
| FlapWang | `Predicted` | `Interpolated` | `Interpolated` |
| Flappy Race | `Predicted` | **`Predicted`** ← 이번에 바뀌는 것 | `Interpolated` |

### 3.2 `Simulated`은 남고, 정책에서 파생된다

`Simulated`(시뮬이 "이 엔티티를 굴린다"고 읽는 표식)과 위 모드는 **층이 다르다**:

| | `Simulated` | `EntitySyncMode` |
|---|---|---|
| 사는 곳 | World Core — 클·서 **공유 시뮬** | 클라 넷코드 |
| 뜻 | "이 월드가 이 엔티티를 굴린다" | "원격을 어떻게 따라갈까" |
| 서버에선 | **전원에게 붙는다**(예측이 아니라 그냥 시뮬) | 개념 자체가 없다 |

없앨 수 없는 이유가 여기 있다 — 시뮬이 매 틱 "이걸 굴릴까"를 클라 전용 정책에 물으면 공유 시뮬이
사이드를 알게 되고, 서버용 가짜 정책을 만들어 끼워야 한다.

**대신 손으로 붙이는 일을 없앤다.** 클라에서는 크리에이터가 `Simulated`을 직접 추가하지 않고,
게임 비종속인 `EntityBinder`가 정책을 보고 붙인다. 저작 지점이 정책 한 곳으로 모인다.

```
게임 스코프(FlapWang / FlappyRace)
   └─ IEntitySyncPolicy 등록
            ↓ 엔티티마다 물어봄
   EntityBinder (게임 비종속)
      · Predicted    → Simulated 부여 + PredictedEntityInterpolator 부착
      · Interpolated → 부여 안 함     + SnapshotEntityInterpolator 부착
            ↓
   GameEntityMessageHandler — 스냅을 모드에 따라 라우팅
      · Predicted    → Reconciler(배치 적용 + 재생)
      · Interpolated → SnapshotEntityInterpolator
```

**서버 크리에이터는 그대로다.** 서버는 전원을 시뮬하므로 `Simulated`을 계속 직접 붙인다.

### 3.3 컴포넌트 이름을 새 축에 맞춘다

지금 두 컴포넌트는 **"누구 것이냐"** 로 갈라져 있는데(`Local`/`Remote`), 새 축은 **"예측이냐 보간이냐"** 다.
남의 새가 예측 대상이 되는 순간 `RemoteEntityInterpolator`라는 이름이 거짓말을 한다.

| 지금 | 이후 | 하는 일 |
|---|---|---|
| `LocalEntityInterpolator` | `PredictedEntityInterpolator` | **예측된** 엔티티의 보이는 메시를 저장된 틱 스냅 사이로 부드럽게 그린다(내 것이든 남의 것이든) |
| `RemoteEntityInterpolator` | `SnapshotEntityInterpolator` | **서버 스냅** 두 개 사이를 지연 시간에서 보간한다 |

이 rename 덕에 예측된 남의 새는 새 렌더러를 만들 필요 없이 **기존 예측 렌더러를 그대로 재사용**한다.

## 4. 예측된 원격을 어떻게 맞추나 — 되감기를 넓힌다

지금 `Reconciler`는 내 새 스냅이 오면 `LoadState(그 틱)` → 권위 값 덮기 → `world.Tick`으로 재생한다.
**"내 새만"을 "스냅에 실린 예측 대상 전부"로 넓히면 끝이다.**

```
서버 스냅 도착(틱 T, 한 메시지에 여러 엔티티)
   → world.LoadState(T)                 ← 예측 대상 전부가 함께 되돌아감(WorldBase가 Simulated을 보관하므로)
   → 스냅의 권위 값을 각 엔티티에 덮기
   → T+1 … 현재 직전까지 world.Tick 재생   ← 내 입력은 기록에서, 남은 입력 없음(=플랩 안 함)
   → 각 엔티티의 렌더가 보정량을 부드럽게 흡수
```

**정직하게 적어 둘 비용 두 가지:**

1. **재생이 거의 매 틱 돈다.** 남의 플랩은 예측할 수 없어 스냅마다 어긋나기 때문이다. 로켓 리그도 같은
   이유로 사실상 항상 재시뮬한다. 새 몇 마리 규모라 비용은 작지만, "가끔 보정"에서 "상시 재생"으로
   성격이 바뀌는 것은 사실이다.
2. **남의 새가 플랩한 순간엔 위치가 튄다.** 그래서 렌더 스무딩이 내 새만이 아니라 **엔티티마다** 필요하다
   (지금 `RenderCorrectionSmoother`는 내 것 하나뿐이다).

## 5. 시뮬은 거의 바뀌지 않는다

Flappy 정책이 남의 새까지 `Predicted`로 정하면, 그 새들도 `Simulated`을 얻는다. 그러면:

- `CollectBirds`가 전원을 모은다 → **몸싸움 짝이 생긴다**
- 맵 sweep이 남의 새에도 돈다 → **파이프를 뚫는 유령이 안 생긴다**(§2의 dead reckoning 함정 회피)
- `FlappyMoveSystem`이 입력을 `entity.Get<InputBuffer>()?.Current`로 읽는데 남의 새엔 그 컴포넌트가 없다
  → **자동으로 "안 누른 것"** 이 된다. 추가 코드 0

**LOP-Shared와 서버는 이 슬라이스에서 변경하지 않는다.**

## 6. 산업 표준 매핑

| 우리 | 대응 | 비고 |
|---|---|---|
| `EntitySyncMode` | Unity Netcode for Entities `GhostMode`(Interpolated / Predicted / OwnerPredicted) | 우리는 두 값만 둔다(§7) |
| `IEntitySyncPolicy` | NfE의 고스트 authoring 설정, Fusion의 프록시 예측 설정 | 우리는 프리팹이 아니라 게임 스코프가 정한다 |
| `Simulated` | NfE `Simulate`(+`PredictedGhost`) — 런타임 태그, 서버에선 항상 켬 | NfE 문서: 이렇게 나눠야 "predicted gameplay 코드를 한 번만 써서 클·서 양쪽에서 돈다" |
| 원격 예측 + 스냅 보정 | Fusion *Forecast Physics*, 로켓 리그 전체 재시뮬 | |
| 남의 입력을 안 누른 것으로 | 로켓 리그 *input decay*의 극단(감쇠할 입력이 없음) | |

## 7. 범위 밖 (이번에 하지 않는 것)

- **내 캐릭터를 `Interpolated`로 고르는 경로** — 정책은 모든 엔티티에 답하므로 *자리는 열려 있지만*
  구현하지 않는다. 실제로 지원하려면 입력 경로(`PlayerInputManager`의 로컬 예측 트리거)까지 손대야 하고,
  두 게임 다 원하지 않는 값이다(Flappy에서 켜면 플랩이 RTT만큼 늦게 뜬다). **고르면 조용히 반쪽으로
  동작하지 않고 크게 실패시킨다.**
- **세 번째 모드 — 단순 외삽(dead reckoning)**. 검토했고 접었다. 근거는 아래 §7-1.
- **아이템 예측** — 서버가 몰아주는 물건이라 클라가 굴릴 규칙이 없다.
- **남의 플랩 예측**(입력 추정) — 미지수를 줄이려는 시도. 지금은 스냅 교정으로 충분한지 먼저 본다.

### 7-1. 단순 외삽(dead reckoning)을 왜 안 쓰나

"마지막 속도(+중력)로 위치만 늘린다"는 방식은 즉흥적인 대안이 아니라 **IEEE 1278 DIS가 9종 알고리즘으로
규격화한 정통 기법**이다(1차=속도, 2차=+가속도, 오차가 임계값을 넘으면 갱신 빈도를 올림). 실제 게임에서도
널리 쓰인다 — Source(CS)는 스냅이 유실되면 **0.25초까지만** 외삽하고 그 이상은 "예측 오차가 너무 커진다"며
자른다. Photon Bolt도 외삽 옵션을 제공한다.

**다만 쓰이는 자리가 다르다: 외삽은 "보여주기"의 표준이고, 예측은 "부딪히기"의 표준이다.** Photon Fusion은
프록시를 기본 보간으로 두어 상호작용을 막고, 상호작용이 필요하면 *Forecast Physics*로 프록시도 예측
시간에 시뮬하라고 안내한다. 로켓 리그는 아예 전부 예측·재시뮬한다.

우리 새에 대입하면 2차 외삽(속도 + 중력 + 낙하 상한)의 탄도는 시뮬과 거의 같다. 갈리는 곳은 셋뿐이다:

1. 파이프·바닥에 막히는 순간 — 스냅 사이에 일어나면 외삽은 뚫고 간다(다음 스냅이 복구)
2. **새끼리 밀림 — 외삽은 모른다**
3. 낙하 상한 — 외삽에 넣으면 된다

**2번이 이 슬라이스의 목적이다.** 상대를 외삽으로 두면 "내가 밀면 나만 밀리고 상대는 제자리"가 된다.
상대가 비켜 주지 않으니 내 새는 계속 밀려나고, 서버 스냅이 올 때 한꺼번에 어긋난다 — 상호작용의 절반만
예측하는 셈이다.

비용도 직관과 반대다. 외삽으로 가면 **공유 시뮬에 새 개념**이 필요하다 — *"충돌에는 참여하지만 내가
굴리지는 않는 엔티티"*. 지금은 `Simulated` 하나로 "굴린다 = 참여한다"가 맞아떨어지는데 그 둘을 갈라야
한다. 예측으로 가면 공유 시뮬은 한 줄도 바뀌지 않는다.

| | 단순 외삽(DR) | 시뮬 예측 |
|---|---|---|
| 공유 시뮬 변경 | "참여자 ≠ 작성자" 개념 신설 | **없음** |
| 클라 코드 | 외삽 컴포넌트 신설 | 기존 되감기 확장 |
| 상대가 밀리는 것 | 서버 확인 후에야 보임 | 즉시 보임 |
| 파이프 관통 | 스냅 사이에 생길 수 있음 | 없음 |
| CPU | 거의 0 | 상시 재생(새 몇 마리라 작음) |

부딪히는 것이 목적이 아니었다면 외삽이 더 나은 선택이었을 것이다. 그래서 이 결정은 **"외삽이 열등해서"가
아니라 "이 게임의 목적이 접촉이라서"** 다 — 접촉이 없는 게임 모드가 생기면 외삽 모드를 세 번째로 넣는 것이
맞다.

## 8. 테스트와 검증

| 대상 | 방법 |
|---|---|
| 정책 두 개 | **EditMode** — 순수 로직이다. FlapWang은 "내 것만 Predicted", Flappy는 "캐릭터 Predicted · 아이템 Interpolated". 이게 이 슬라이스의 핵심 계약 |
| 몸싸움이 클라에서 도는가 | **EditMode** — 두 새가 다 `Simulated`인 `FlappyWorld`에서 겹침이 풀린다(기존 테스트가 이미 이 모양) |
| FlapWang 회귀 | 이름만 바뀌고 동작은 그대로 — 2에디터로 걷기·점프·대시 감각 확인 |
| 남의 새가 매끄러운가 | **MPPM 2인** — 예측이라 프레임마다 갱신된다 |
| 부딪히면 양쪽에서 밀리는가 | **MPPM 2인** — 이 슬라이스의 대표 확인 |
| 상대 플랩 시 튐 | 같은 세션에서 육안 + 보정 통계 |

> **검증의 어려움**: 지난 슬라이스에서 "가상 플레이어로 새 두 마리를 부딪히게 만들기 어렵다"는 문제가
> 드러났다. 계획서에서 방법을 따로 궁리한다(예: 두 새를 가깝게 세우는 임시 스폰 설정).

## 9. 확정된 결정

| 항목 | 결정 | 이유 |
|---|---|---|
| 원격을 어떻게 | **예측(내 시간선에서 같이 굴림)** | 접촉이 게임성인 장르의 표준. 보간 위치로 충돌을 풀면 "안 닿았는데 밀림"이 된다 |
| 선택의 자리 | **게임 스코프가 정책을 DI 등록** | `IServerCorrectionHandler`와 같은 선례. 게임마다 답이 다르다 |
| 정책의 단위 | **엔티티마다 물어본다** | 같은 게임 안에서도 아이템과 캐릭터가 다르다 |
| `Simulated` | **남기고 정책에서 파생** | 시뮬(클·서 공유)이 읽는 표식이라 없앨 수 없다. 대신 손으로 붙이지 않는다 |
| 보정 방식 | **되감기를 스냅 배치 전체로 넓힘** | 이미 있는 기계를 그대로 쓴다. 로켓 리그와 같은 모양 |
| 컴포넌트 이름 | `Predicted*` / `Snapshot*`으로 축에 맞춤 | 남의 새가 예측되면 `Remote*`라는 이름이 거짓이 된다 |
| 렌더 스무딩 | **엔티티마다** | 남의 새도 튀므로 |

## 10. Open Decisions

- [ ] **정책을 어디에 두나** — 클라 전용이면 `Assets/Scripts/Netcode/`. 나중에 서버가 관전/리플레이에서
  같은 축을 쓰게 되면 GameFramework로 올릴지 재검토.
- [ ] **`Reconciler` 이름** — 이제 월드 전체를 되감으므로 `WorldReconciler`가 정확할 수 있다. 구현하며 판단.
- [ ] **보정 게이트** — 지금은 내 새 오차가 문턱 이하면 재생을 건너뛴다. 원격이 들어오면 사실상 매번
  재생하게 되는데, 게이트를 유지할지(내 새 기준) 없앨지는 실측 후 정한다.
- [ ] **남의 새 스냅이 없는 구간**(패킷 유실) — 예측이 계속 굴러가므로 오차가 누적된다. 최대 외삽 길이를
  두어 그 이상은 hold할지 결정.

## 10-1. 결과 (2026-08-23, 완료)

두 저장소에 머지됨. **GameFramework·서버는 변경 없음.**

| 저장소 | 커밋 |
|---|---|
| LOP-Shared | `cdd32ab` 모드·정책 + 단위 테스트 5개 |
| LOP-Client | `a4bb23f` 팔로워 rename · `c396a55` 엔티티별 스무딩 · `d7283e8` 되감기 배치 · `5f9967b` 정책 배선 · `6d0309b` 임시 로그 제거 |

**테스트**: 클라 EditMode 537/537(신규 5). 새 테스트는 일부러 깨뜨려 실패를 본 뒤 되돌렸다.

### 런타임 검증 — 목적이 관측됐다

메인 에디터와 MPPM 클론 두 클라를 **CLI로 직접 몰아** 확인했다(방법은 아래 "배운 것" 참고).

| 확인 | 결과 |
|---|---|
| 원격 예측 배선 | 두 클라 모두 `[1 simulated=True] [2 simulated=True]` — **남의 새까지 예측 대상** |
| **몸싸움이 클라에서 풀리는가** | 정지한 bird2(364.73, −49.80)에 bird1이 접근 → **접촉 순간 메인은 bird2를 (365.09, −50.24)로, 클론은 (367.74, −49.88)로 각자 즉시 밀어냄** → 다음 샘플에서 양쪽 모두 **(369.53, −49.92)로 수렴**. bird1은 x=364.73에서 막혀 전진 정지 |
| 되감기 비용 | corr 2541 / Recon avg **0.01m** — §4가 예측한 "상시 재생 + 작은 오차" 그대로 |
| FlapWang 회귀 | 양쪽 클라 `simulated=1`(내 캐릭만), Predicted 1 / Snapshot 101 — 종전 동작 유지 |
| 런타임 에러 | **0건**(두 클라 × 두 게임 세션) |

접촉 순간 두 클라의 값이 **서로 다르다가 곧 같아지는 것**이 이 설계의 모습이다 — 각자 예측하고 서버로 수렴한다.

### Open Decisions의 답

- **보정 게이트**(§10) — 없애지 않고 **판정을 배치 전체로 넓혔다**. 아무도 어긋나지 않은 틱은 여전히 건너뛴다.
  실측에서 약 44%의 틱에 보정이 걸렸고(corr 2541 / 약 5,750틱) 평균 오차는 0.01m였다 — 게이트가 자주 열리지만
  오차가 작아 유지할 값이 있다고 판단했다.
- **최대 외삽 길이**(§10) — 두지 않았다. 관측된 오차가 작아 지금은 필요가 없다. 스냅이 오래 끊기는
  상황에서 드리프트가 문제가 되면 그때 넣는다.
- **정책의 집** — LOP-Shared `Runtime/Scripts/Netcode/`. 순수 C#이라 단위 테스트가 붙는 것이 결정적이었다.
- **`Reconciler` 이름** — 그대로 두었다. 하는 일이 넓어졌지만(배치) 여전히 "예측을 권위와 맞춘다"라 이름이 거짓이 아니다.

### 검증의 한계 (정직하게)

- **예측되는 남의 엔티티는 비-모션 권위 값(HP·어빌리티·상태이상)을 받지 않는다** — 의도한 한계다.
  스냅 라우팅이 모드로 갈리는데, 비-모션 권위를 적용하던 코드가 보간 분기에만 있기 때문이다.
  Flappy 새에는 Health·Abilities·StatusEffects가 아예 없어 지금은 무해하다. 반대로 "모드와 무관하게 항상
  적용"으로 바꾸면 안 된다 — 그 코드가 어빌리티 발동을 연출용 재구성 값으로 덮어써서, 내가 예측 중인
  발동 상태가 뭉개진다. **어빌리티가 있는 게임이 원격 예측을 켜는 날 이 구멍을 메워야 한다.**
- ~~정책 평가 시점이 비대칭이다 — 현재 두 정책은 그런 조건을 만들지 않는다~~ → **틀린 판단이었고 고쳤다.**
  최종 리뷰가 지적했듯, 그 "안전하다"의 근거였던 불변식(로컬 id가 스폰보다 먼저 정해진다)은 **바로 아래
  '배운 것'에 적은 동시 큐잉 경합에서 이미 깨진 채로 관측됐다.** 그 경합에서 생성 시엔 `Interpolated`,
  스냅 도착 시엔 `Predicted`로 답이 갈리면, 팔로워는 보간기인데 스냅은 `Reconciler`로 가고 —
  `Reconciler`는 내 엔티티가 없는 배치를 버리므로 **그 엔티티가 보정을 하나도 못 받는다**(슬라이스 전에는
  보간으로라도 움직였다).
  **수정**: 판정을 한 번만 한다. 스냅 라우팅이 정책에 다시 묻지 않고 **생성 시 판정의 결과인 `Simulated`
  마커**를 읽는다(클라에서 그 마커를 붙이는 곳은 `EntityBinder` 한 곳뿐이라 어긋날 수 없다).
  `GameEntityMessageHandler`는 이제 정책을 주입받지 않는다.
- **관측한 몸싸움은 한 장면뿐이다** — 정지한 새를 미는 상황이었다. 둘 다 빠르게 움직이며 부딪히는 경우,
  여러 마리가 동시에 겹치는 경우는 보지 못했다.

### 미룬 것 (최종 리뷰가 남긴 판단)

- **정책이 공유 패키지에 산다** — `IEntitySyncPolicy`/`EntitySyncMode`/정책 2종이 LOP-Shared에 있는데,
  §3.2가 "서버에선 개념 자체가 없다"고 못박은 것이고 실제로 Shared 안에서 쓰는 코드는 0이다. 클라 레포에도
  EditMode 테스트 어셈블리가 있으므로 "테스트 때문에"라는 근거는 약하다. **토폴로지 문서의 코드 분배
  기준에서 벗어난 예외**이니, 다음에 이 근처를 손댈 때 클라로 옮길지 정한다.
- **바뀐 로직(되감기 배치 게이트·이탈 경로)에는 자동 테스트가 없다** — 테스트 5개는 정책만 덮는다.
  `Reconciler`는 순수 C#에 가까워 fake 몇 개로 게이트 표 테스트를 붙일 수 있다. 이번 계획이 요구하지
  않았을 뿐이다.
- **`userEntityId` 스폰 경합**(아래 "배운 것") — 원인 코드는 이번 슬라이스 밖(`CharacterCreator`)이라
  별도 슬라이스로 다룬다. 위 정책 판정 수정으로 **적어도 슬라이스 전만큼**으로는 되돌아간다.

### 실행하며 배운 것 (다음 사람을 위해)

- **MPPM 클론도 CLI로 조작할 수 있다.** 클론은 `<project>/Library/VP/<id>/Library/Pipeline/.unity-pipeline-port`에
  자기 포트를 열어 두므로 `unity cmd <명령> --project-path <project>/Library/VP/<id>`로 붙는다.
  `unity status`에는 안 잡히고 `--runtime-path`도 안 먹는다(그건 플레이어 빌드용이다). 이걸 알면 2인 검증을
  사람 손 없이 자동화할 수 있다 — 이번에 그렇게 했다.
- **DI 컨테이너를 통해 게임을 조작할 수 있다.** `LifetimeScope.Container.Resolve(typeof(...))`로
  `PlayerInputManager`·`EntityRegistry`·`IPlayerContext`를 꺼내 입력을 넣고 상태를 읽었다.
  (eval 스크립트에서는 제네릭 `Resolve<T>()`가 안 되고 `Resolve(typeof(T))`를 써야 한다.)
- **두 클라를 *동시에* 큐잉하면 기존 경합이 터진다** — 게임정보(`userEntityId`)가 스폰 메시지보다 늦게
  도착해 내 캐릭터가 인식되지 않는다(전 엔티티 `Interpolated`, HUD 미개방, 입력 불가). 이번 슬라이스와
  무관한 기존 문제다(내 엔티티를 식별하는 `CharacterCreator`는 손대지 않았다). 시차를 5초만 두면 정상.
  **별도로 다뤄야 할 버그다.**
- **아무도 안 누르면 새가 파이프 모서리에 낀다** — 그 자리에서는 플랩을 넣어도 빠져나오지 못하고, 방이
  살아 있어 재입장해도 같은 상태로 돌아온다. 검증은 입장 직후부터 날갯짓을 넣어야 한다.

## 11. 관련 문서

- `docs/superpowers/specs/2026-08-17-flappy-race-gameplay-b2-design.md` §6-2 — 이 문제가 드러난 자리
- `docs/netcode-redesign.md` — 예측·보정 구조, 클럭 동기
- `docs/world-core-connection-architecture.md` — `Simulated`, 시뮬 공유 원칙, 되감기 책임 위치

## 참고

- [Unity Netcode for Entities — Prediction(`Simulate`/`PredictedGhost`)](https://docs.unity3d.com/Packages/com.unity.netcode@1.4/manual/prediction-n4e.html)
- [Unity Netcode for Entities — Interpolation](https://docs.unity3d.com/Packages/com.unity.netcode@1.7/manual/interpolation.html)
- [Photon Fusion 2 — Physics(프록시 kinematic 기본)](https://doc.photonengine.com/fusion/current/manual/physics)
- [Photon Fusion — Physics Addon(Forecast Physics)](https://doc.photonengine.com/fusion/v2/addons/physics-addon-2.0)
- [Photon Bolt — Interpolation vs Extrapolation](https://doc.photonengine.com/bolt/current/in-depth/interpolation-vs-extrapolation)
- [Rocket League 넷코드 — 전체 재시뮬·input decay 논의](https://www.gamedev.net/forums/topic/713082-rollbacks-and-simulation-replay-performance/5452777/)
- [Gamasutra — Dead Reckoning: Latency Hiding for Networked Games](https://www.gamedeveloper.com/programming/dead-reckoning-latency-hiding-for-networked-games)
- [IEEE 1278 DIS — Dead Reckoning 알고리즘 9종(1차/2차)](https://github.com/open-dis/dis-tutorial/wiki/Dead-Reckoning)
- [Valve — Source Multiplayer Networking(`cl_extrapolate_amount` 0.25초 상한)](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
