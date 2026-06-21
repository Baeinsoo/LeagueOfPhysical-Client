# Netcode 시간동기 Mirror 분리 — Slice ① `INetworkTime` 추상화 seam

**Date:** 2026-06-20
**Branch:** `feature/netcode-clock-decouple-slice1`
**Related:** [netcode-redesign.md](../../netcode-redesign.md) §9.4·§9.5 · [world-core-connection-architecture.md](../../world-core-connection-architecture.md) (I/O 어댑터) · [lop-repo-topology.md](../../lop-repo-topology.md) (GameFramework vs use-side)

## Goal

클라이언트와 서버 양쪽의 **시간동기(네트워크 클럭) 소스를 `Mirror.NetworkTime`에 직접 의존하지 않도록**, `INetworkTime` 추상화 뒤로 격리한다. Mirror-backed 구현체 하나(`MirrorNetworkTime`)를 두고, 현재 `Mirror.NetworkTime`을 직접 읽는 호출부가 이 추상화를 통해 읽게 한다.

**동작은 그대로**(여전히 `predictedTime`/`rtt` 값) — 이 슬라이스는 *순수 격리(behavior-preserving)* 다. Mirror가 swappable해지고, 네트워크 시간 소스가 한 곳으로 모이는 것이 산출물.

## 맥락 — 3-슬라이스 분해 (Mirror 시간동기 분리)

이 작업은 "Mirror 시간동기 의존 제거 + 고도화 여지 확보"를 세 슬라이스로 나눈 것의 **첫 번째**다. 전체 그림:

| 슬라이스 | 내용 | 비용/위험 | 상태 |
|---|---|---|---|
| **① 추상화 seam** (이 문서) | `INetworkTime` 추상 + Mirror 구현체 + facade. 호출부 라우팅. **동작 보존.** | 작음 / 낮음 | 설계 중 |
| **② 네이티브 clock sync** | `predictedTime` 내부(offset 추정 + 서버 피드백 폐루프)를 우리 ping/pong으로 *대체*. Mirror time 의존 실제 제거. **RTT-추적 동적 lead가 여기 내장.** | 중 / 중~높음 | 예정 (별도 spec) |
| **③ buffer-occupancy 동적 lead 튜닝** | 서버 input-buffer 점유 신호로 lead margin 미세조정(원래 Phase 4의 본체). | 중 / 중 | 예정 — ② 검증 후 측정해서 도입 판단 |

**핵심 분리 원칙**: `INetworkTime` 인터페이스 = ②/③이 갈아끼울 **안정된 seam**. `predictedTime`/`rtt`를 *생산하는 기계*(offset 추정·서버 피드백·dilation)는 인터페이스 *뒤의 구현*이고, ②(네이티브 대체)·③(buffer 튜닝)은 **이 인터페이스를 안 건드리고 뒤의 구현만 교체/확장**한다. 그래서 인터페이스는 하나로 충분하다 (값별로 쪼개지 않는다).

### "동적 lead"의 3겹 — 어디가 이 슬라이스 밖인가

"lead 동적 조절"은 한 덩어리가 아니라 3겹이며, 이 슬라이스는 셋 다 **건드리지 않는다**(동작 보존):

1. **offset 추정**(서버 now 추적 = RTT/2 앞서기) — 현재 Mirror `predictedTime`이 제공. **②에서 네이티브로 대체 = RTT-추적 동적 lead가 ②에 내장**(deferrable 아님).
2. **서버 피드백 offset 보정**(RTT 비대칭 교정, 폐루프) — Mirror `OnServerPing`이 제공. ② 안에서 정확도 위해 거의 같이 구현.
3. **buffer-occupancy margin 튜닝**(Overwatch time dilation) — Mirror가 *원래도 안 줌*. **③에서만** 측정 후 도입.

> 이 슬라이스 ①은 위 3겹을 *그대로 둔 채* 소스 접근만 추상화 뒤로 옮긴다.

### LatencySimulation은 영향 없음

이 작업은 Mirror **transport**가 아니라 Mirror **time/clock-sync API(`NetworkTime`)** 만 추상화한다. `LatencySimulationTransport`는 transport 레이어 기능이라 **그대로 유지**되며, ②에서 네이티브 ping/pong을 짜도 그 메시지는 Mirror transport를 타고 나가 LatencySimulation이 적용된다. 측정 baseline(§9.6) 유효.

## 현재 Mirror 시간 결합면 (LOP 코드 한정 — 4 호출부 / 3 파일)

| 파일:라인 | 읽는 것 | 용도 |
|---|---|---|
| `Assets/Scripts/Game/LOPTickUpdater.cs:15` | `NetworkTime.predictedTime` | 내 캐릭 sim 틱 타깃 (client-ahead 클럭) |
| `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs:22` | `NetworkTime.rtt` | HUD RTT 표시 |
| `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs:25` | `NetworkTime.predictedTime - NetworkTime.rtt * 0.5` | HUD server-now (lead 계산) |
| `Assets/Scripts/Entity/SnapInterpolator.cs:34` | `NetworkTime.rtt * 0.5` | 원격 보간 back-time |

> 나머지 `NetworkTime.*` 출현은 전부 Mirror 내부/예제 코드이며 LOP 소유가 아니다. **우리가 분리할 표면은 위 4곳뿐이다.**
>
> `NetworkTime.time`(스냅샷 보간 클럭, 서버보다 bufferTime 뒤)은 LOP 코드에서 **직접 읽지 않는다**(원격 보간은 Mirror 내부가 처리). 그래서 `INetworkTime` surface에 포함하지 않는다.

## 설계

### `INetworkTime` (GameFramework)

```csharp
namespace GameFramework
{
    /// <summary>
    /// 네트워크 동기 시간 소스. 서버 현재 시간·client-ahead 예측 클럭·왕복지연을 노출한다.
    /// 시간동기 메커니즘(offset 추정·서버 피드백·dilation)은 구현체의 책임이며,
    /// 이 인터페이스는 그 출력(읽기 전용)만 노출한다.
    /// 불변식: PredictedTime = ServerNow + Rtt/2.
    /// </summary>
    public interface INetworkTime
    {
        /// <summary>
        /// 현재 서버 시간 (초).
        /// 서버=Mirror.NetworkTime.time(권위 클럭), 클라=predictedTime − RTT/2(추정).
        /// </summary>
        double ServerNow { get; }

        /// <summary>
        /// client-ahead 예측 클럭 (초). 서버에서는 ServerNow와 일치한다.
        /// </summary>
        double PredictedTime { get; }

        /// <summary> 왕복지연 (초). 서버에서는 0. </summary>
        double Rtt { get; }
    }
}
```

- **3개 멤버**: `ServerNow`(서버에서는 권위 클럭, 클라에서는 `predictedTime − RTT/2`), `PredictedTime`(클라 ahead; 서버에서는 `ServerNow`와 일치), `Rtt`(서버에서는 0). 불변식: `PredictedTime = ServerNow + Rtt/2`.
- 파생값(원격 back-time)은 `NetworkTimeExtensions`에 extension으로 둔다 — 인터페이스는 최소·안정으로 유지.
- 위치: GameFramework `Runtime/Scripts/Game/` (기존 `ITickUpdater`와 동일 레이어 — 앱 비종속 시간 인프라).
- 이름 `NetworkTime`은 `GameEngine.Time`(로컬 sim/tick 클럭) 짝패 + Unity 생태계 관용어(Mirror의 `Mirror.NetworkTime`, NGO의 `NetworkTime`/`NetworkTimeSystem`)와 맞춘 것이다 — 아래 "산업 표준 매핑" 참고.

### `NetworkTimeExtensions` (GameFramework)

```csharp
namespace GameFramework
{
    public static class NetworkTimeExtensions
    {
        /// <summary> 원격 보간 back-time = RTT/2 (소비자가 clamp). </summary>
        public static double RemoteBackTime(this INetworkTime networkTime)
            => networkTime.Rtt * 0.5;
    }
}
```

- `ServerNow`가 인터페이스 멤버로 승격됐으므로 extension에는 `RemoteBackTime()`만 남는다.

### `MirrorNetworkTime : INetworkTime` (LOP-Client / LOP-Server 양쪽)

클라와 서버 모두 `MirrorNetworkTime`을 구현하되, 제공하는 값이 다르다:

```csharp
// Assets/Scripts/Game/MirrorNetworkTime.cs  (클라)
public class MirrorNetworkTime : INetworkTime
{
    public double ServerNow      => Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5;
    public double PredictedTime  => Mirror.NetworkTime.predictedTime;
    public double Rtt            => Mirror.NetworkTime.rtt;
}
```

```csharp
// Assets/Scripts/Game/MirrorNetworkTime.cs  (서버)
public class MirrorNetworkTime : INetworkTime
{
    // 서버는 자기 권위 클럭 — ServerNow == PredictedTime, lead 없음.
    public double ServerNow      => Mirror.NetworkTime.time;
    public double PredictedTime  => Mirror.NetworkTime.time;
    public double Rtt            => 0.0;
}
```

- Mirror `NetworkTime`을 위임 — **동작 보존**.
- **평범 POCO**(MonoBehaviour 아님). 클라 `LOPGameEngine.CreateNetworkTime()` / 서버 `LOPGameEngine.CreateNetworkTime()` override 각자가 `new MirrorNetworkTime()`으로 생성·제공(아래 "엔진 배선"). `Mirror.NetworkTime`은 **`MirrorNetworkTime.cs` 한 파일 안에서만** 참조된다.

### facade — `GameEngine.NetworkTime` (GameFramework)

`GameEngine.Time`(로컬 sim/tick 클럭 값)과 **나란히** `GameEngine.NetworkTime`(네트워크 동기 소스 + 지연)을 추가:

```csharp
public static class GameEngine
{
    public static IGameEngine current { get; internal set; }

    public static class Time { /* 기존: tick/interval/elapsedTime ... */ }

    public static class NetworkTime
    {
        public static double serverNow      => current.networkTime.ServerNow;
        public static double predictedTime  => current.networkTime.PredictedTime;
        public static double rtt            => current.networkTime.Rtt;

        /// <summary> 원격 보간 back-time = RTT/2 (소비자가 clamp). </summary>
        public static double remoteBackTime => current.networkTime.RemoteBackTime();
    }
}
```

- **`Time` vs `NetworkTime` 구분**: `Time` = 로컬 sim/tick 시간(tick·interval·elapsed). `NetworkTime` = 네트워크 동기 시간 소스 + 지연. 둘은 다른 축이라 facade를 분리한다(섞으면 "로컬 sim 시간" facade가 흐려짐).
- `remoteBackTime`은 파생 extension의 편의 래퍼로 노출. `serverNow`는 인터페이스 멤버를 직접 포워딩.

### 엔진 배선 — `IGameEngine.networkTime` + `CreateNetworkTime()` (GameFramework)

`GameEngineBase`가 `InitializeAsync`에서 **`protected virtual INetworkTime CreateNetworkTime()` 템플릿 메서드**로 시간 소스를 획득(기본 null):

```csharp
// IGameEngine
INetworkTime networkTime { get; }

// GameEngineBase
public INetworkTime networkTime { get; private set; }
protected virtual INetworkTime CreateNetworkTime() => null;

// GameEngineBase.InitializeAsync
networkTime = CreateNetworkTime();   // tickUpdater(GetComponent+throw)와 달리 null 허용.
                                     // 클라·서버 양쪽이 override해 MirrorNetworkTime 제공.
```

- **컴포넌트(GetComponent)가 아니라 팩토리 메서드** — `MirrorNetworkTime`을 평범 POCO로 둘 수 있고(테스트 쉬움) 씬/프리팹 편집이 불필요하다. 클라·서버 `LOPGameEngine` 각각이 `CreateNetworkTime()`을 override해 `new MirrorNetworkTime()` 반환.
- **null 허용** — 향후 Mirror 없는 환경(순수 시뮬·테스트)에서 엔진이 override 없이도 로딩 가능하도록.
- teardown 시 `networkTime = null` (기존 필드들과 동일).

### 호출부 마이그레이션 (4곳, 동작 보존)

| 파일 | 변경 전 | 변경 후 |
|---|---|---|
| `LOPTickUpdater.cs:15` (클라) | `Mirror.NetworkTime.predictedTime + AheadMargin` | `GameEngine.NetworkTime.predictedTime + AheadMargin` |
| `LOPTickUpdater.cs` (서버) | `Mirror.NetworkTime.time` (서버 권위 클럭) | `GameEngine.NetworkTime.serverNow` |
| `DebugHudViewModel.cs:22` | `Mirror.NetworkTime.rtt * 1000` | `GameEngine.NetworkTime.rtt * 1000` |
| `DebugHudViewModel.cs:25` | `(NetworkTime.predictedTime - NetworkTime.rtt*0.5) / tickInterval` | `GameEngine.NetworkTime.serverNow / tickInterval` |
| `SnapInterpolator.cs:34` | `(float)NetworkTime.rtt * 0.5f` (clamp 유지) | `(float)GameEngine.NetworkTime.remoteBackTime` (clamp 유지) |

- 마이그레이션 후 LOP 코드에 `Mirror.NetworkTime` 직접 참조 **0** (목표; `MirrorNetworkTime.cs` 단일 격리 지점 제외).

## 사이드 영향

- **클라**: `MirrorNetworkTime` POCO 1개 + `LOPGameEngine.CreateNetworkTime()` override + 호출부 변경. 동작 보존.
- **서버**: `MirrorNetworkTime` POCO 1개 + `LOPGameEngine.CreateNetworkTime()` override. 서버 `LOPTickUpdater`가 `GameEngine.NetworkTime.serverNow`로 전환. 동작 보존.
- **GameFramework**: `INetworkTime` 인터페이스 + `NetworkTimeExtensions`(RemoteBackTime), `IGameEngine.networkTime`, `GameEngineBase`의 `CreateNetworkTime()`/배선/teardown, `GameEngine.NetworkTime` facade 추가.

## 산업 표준 매핑

architecture-guidelines("Microsoft C# 컨벤션 + 검증된 표준 패턴")과 netcode-redesign §9의 "산업 표준 매핑" 관행을 따른다.

| 우리 선택 | 업계 대응 |
|---|---|
| `INetworkTime` (네트워크 동기 시간 추상) | **NGO**: `NetworkTime` struct(LocalTime/ServerTime), `NetworkTimeSystem`(동기). **FishNet**: `TimeManager`(틱/RTT/서버-now). **Mirror**: `Mirror.NetworkTime`(predictedTime/rtt/time 공존). **Unreal**: synced network clock (Josh Sutphin / Vorixo). |
| 단일 타입에 ServerNow·PredictedTime·Rtt 공존, 서버에서 ServerNow==PredictedTime | NGO `LocalTime`(클라 ahead) vs `ServerTime`(권위) 두 값이 같은 타입에 공존 — 서버에선 일치. FishNet Tick/LocalTick 동형. |
| 클럭 소스/추정을 틱 구동에서 분리 | **NGO**: `NetworkTimeSystem`(동기) + `NetworkTickSystem`(구동) 분리 / **FishNet** 멤버 분리 (§9.5가 처방한 바로 그 seam) |
| 추상 인터페이스(GameFramework) + 사이드별 구현(use-side) | connection-architecture I/O 어댑터 모델(`IInputSource`/`IEventSink`/`ITickUpdater`) |
| 이름 `NetworkTime` | Mirror의 `Mirror.NetworkTime`, NGO의 `NetworkTime`/`NetworkTimeSystem`과 맞춘 Unity 생태계 관용어. `GameEngine.Time`(로컬 sim 클럭) 짝패로 직교 의미 구분. |
| `I` 접두 인터페이스 + PascalCase | Microsoft C# Coding Conventions |

> **§9.5 정합**: §9.5는 `LOPTickUpdater`가 (1)클럭 소스/추정 (2)틱 구동 (3)시간값 보유를 혼재한다고 지적하며 "Phase 2에서 lead/dilation 붙이는 시점에 (1)을 별도 seam으로 추출하라"고 처방했다. **이 슬라이스가 정확히 (1)의 추출**이다 — (2)틱 구동은 `TickUpdaterBase`에, (3)값 보유는 `GameEngine.Time`에 그대로 둔다.

## Testing

- **`INetworkTime` mock 단위 테스트**(EditMode, GameFramework Tests): 고정 `ServerNow`/`PredictedTime`/`Rtt`를 주는 fake `INetworkTime`(`FakeNetworkTime`)을 써서 `NetworkTimeExtensions.RemoteBackTime() == rtt/2` 파생 계산 검증. `GameEngine.current`에 꽂아 facade 파생도 검증.
  - 파생 계산을 **GameFramework 측 EditMode**로 두는 게 깔끔 — facade가 GameFramework에 있고 fake만 있으면 됨. 클라 테스트 인프라 제약(클라 코드가 Assembly-CSharp라 asmdef 참조 불가 — memory) 회피.
- **동작 보존 회귀**: 변경 전후 HUD의 RTT/lead 표시와 sim 틱 타깃이 동일해야 함(여전히 `predictedTime`). 수동: 에디터 2-인스턴스 + LatencySimulation으로 HUD 값 동일 확인.

## Out of Scope

- **②/③ 본체** — 네이티브 clock sync(ping/pong·offset·서버 피드백), buffer-occupancy 튜닝. 각자 별도 spec.
- **`TickUpdater` 네이밍/구조 추가 분리**(§9.5의 (2)(3) 분리, `TimeSync` vs `TickDriver` rename) — 이 슬라이스는 (1) 추출만. rename은 ② 이후 재논의.
- **DI 정밀화** — 지금은 static facade(`GameEngine.NetworkTime`). `[Inject] INetworkTime` 경로는 필요해지면 facade 위에 얹음.
- **원격 엔티티 타임라인**(`ServerStateReconciler`/`SnapInterpolator`의 보간 정책) — back-time 소스만 facade로 옮기고 정책은 그대로.
- **`ClockDilator`** — 별도 rate-dilation 유틸리티, 이 슬라이스에서 건드리지 않는다.

## Open Questions — plan에서 해소됨

> 아래 4개는 `docs/superpowers/plans/2026-06-20-netcode-clock-decouple-slice1.md`에서 확정됨.

- ~~`MirrorNetworkTime` 컴포넌트 vs 평범 클래스~~ → **평범 POCO + `CreateNetworkTime()` 팩토리 override**(씬 편집 불필요, 테스트 쉬움, 클라·서버 각자 override).
- ~~`LOPTickUpdater` facade vs 직접 ref~~ → **facade**(호출부 균일).
- ~~파생 테스트 위치~~ → **GameFramework `Tests/Runtime` EditMode**(파생을 순수 `INetworkTime` extension으로 빼서 `GameEngine.current` 없이 테스트).
- ~~`MirrorNetworkTime` 부착 위치~~ → **해당 없음**(컴포넌트 아님 — `LOPGameEngine.CreateNetworkTime()`가 생성).

## 진행

- [x] 브레인스토밍 합의 (3-슬라이스 분해, ① 범위, static facade, `INetworkTime` 이름)
- [x] 이 spec 작성
- [x] spec self-review
- [x] `writing-plans`로 구현 plan 작성 (`plans/2026-06-20-netcode-clock-decouple-slice1.md`)
- [ ] 사용자 plan/spec 리뷰 → 구현 착수
