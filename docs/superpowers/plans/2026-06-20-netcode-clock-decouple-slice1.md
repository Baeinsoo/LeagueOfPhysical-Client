# Netcode Clock Decouple Slice ① (INetworkTime abstraction seam) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라·서버 양쪽의 시간동기(네트워크 클럭) 소스를 `Mirror.NetworkTime` 직접 의존에서 `INetworkTime` 추상화 뒤로 격리한다(동작 보존).

**Architecture:** GameFramework에 `INetworkTime` 인터페이스(`ServerNow`/`PredictedTime`/`Rtt`) + 파생 계산 extension(`RemoteBackTime`, 순수·테스트 가능) + `GameEngine.NetworkTime` static facade를 신설. `GameEngineBase`가 `protected virtual INetworkTime CreateNetworkTime()` 템플릿 메서드로 시간 소스를 획득(기본 null). LOP-Client와 LOP-Server 각각에 `MirrorNetworkTime`(평범 POCO, `Mirror.NetworkTime` 위임, 클·서 값 다름) + `LOPGameEngine.CreateNetworkTime()` override. `Mirror.NetworkTime`을 직접 읽던 호출부를 facade로 라우팅.

**Tech Stack:** C# / Unity / GameFramework(file: 패키지) / Mirror / NUnit(EditMode) / UnityMCP(컴파일 검증).

---

## 설계 결정 (spec Open Questions 해소)

| Open Q | 결정 | 근거 |
|---|---|---|
| 1. MirrorNetworkTime 컴포넌트 vs 평범 클래스 | **평범 POCO + `CreateNetworkTime()` 팩토리 override** | 씬/프리팹 편집 불필요, POCO라 테스트 쉬움, 클·서 각자 override. |
| 2. LOPTickUpdater facade vs 직접 ref | **facade(`GameEngine.NetworkTime`)** | 호출부 균일. static getter 위임이라 핫패스 비용 무시 가능. |
| 3. 파생 테스트 위치 | **GameFramework `Tests/Runtime` EditMode** | 파생을 `INetworkTime` extension(순수)로 빼서 `GameEngine.current` 없이 테스트. |
| 4. MirrorNetworkTime 부착 위치 | **해당 없음** — 컴포넌트 아님(클라·서버 `LOPGameEngine.CreateNetworkTime()`가 생성) |

## File Structure

**GameFramework (공유 패키지, `C:/Users/re5na/workspace/LOP/GameFramework/`)**
- Create: `Runtime/Scripts/Game/INetworkTime.cs` — 인터페이스(`ServerNow`, `PredictedTime`, `Rtt`)
- Create: `Runtime/Scripts/Game/NetworkTimeExtensions.cs` — 파생 계산(`RemoteBackTime`)
- Modify: `Runtime/Scripts/Game/IGameEngine.cs` — `INetworkTime networkTime { get; }` 추가
- Modify: `Runtime/Scripts/Game/GameEngineBase.cs` — `networkTime` 프로퍼티 + `CreateNetworkTime()` + init/teardown 배선
- Modify: `Runtime/Scripts/Game/GameEngine.cs` — `GameEngine.NetworkTime` facade 추가
- Create: `Tests/Runtime/NetworkTimeExtensionsTests.cs` — 파생 단위 테스트

**LOP-Client (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/`)**
- Create: `Assets/Scripts/Game/MirrorNetworkTime.cs` — `INetworkTime` 구현(Mirror 위임, 클라 값)
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs` — `CreateNetworkTime()` override
- Modify: `Assets/Scripts/Game/LOPTickUpdater.cs:15` — facade로 (`predictedTime`)
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs:22,25` — facade로
- Modify: `Assets/Scripts/Entity/SnapInterpolator.cs:34` — facade로

**LOP-Server**
- Create: `Assets/Scripts/Game/MirrorNetworkTime.cs` — `INetworkTime` 구현(Mirror 위임, 서버 값)
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs` — `CreateNetworkTime()` override
- Modify: `Assets/Scripts/Game/LOPTickUpdater.cs` — `GameEngine.NetworkTime.serverNow` 로 전환

> **사이드 영향**: GameFramework 변경은 가산적(기본 null)이라 자동 컴파일 통과. 서버·클라 양쪽 모두 `CreateNetworkTime()`을 override해 `MirrorNetworkTime`을 제공한다.

## UnityMCP 인스턴스 타깃팅 (검증 스텝 공통)

컴파일 검증 전, 클라 인스턴스 id를 해석한다:
1. `mcpforunity://instances` 읽기 → `name == "LeagueOfPhysical-Client"`인 항목의 full `id`(`Name@hash`) 확보.
2. 이후 모든 UnityMCP 호출에 `unity_instance="LeagueOfPhysical-Client@<hash>"` 명시.

---

### Task 1: `INetworkTime` 인터페이스 + 파생 extension (GameFramework, TDD)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/INetworkTime.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/NetworkTimeExtensions.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/NetworkTimeExtensionsTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

Create `Tests/Runtime/NetworkTimeExtensionsTests.cs`:

```csharp
using NUnit.Framework;

namespace GameFramework
{
    public class NetworkTimeExtensionsTests
    {
        private class FakeNetworkTime : INetworkTime
        {
            public double ServerNow     { get; set; }
            public double PredictedTime { get; set; }
            public double Rtt           { get; set; }
        }

        [Test]
        public void RemoteBackTime_IsHalfRtt()
        {
            var nt = new FakeNetworkTime { ServerNow = 9.9, PredictedTime = 10.0, Rtt = 0.2 };
            Assert.AreEqual(0.1, nt.RemoteBackTime(), 1e-9);
        }

        [Test]
        public void ServerNow_IsDirectMember_NotDerived()
        {
            // ServerNow is a first-class interface member — verify the fake surfaces it.
            var nt = new FakeNetworkTime { ServerNow = 9.9, PredictedTime = 10.0, Rtt = 0.2 };
            Assert.AreEqual(9.9, nt.ServerNow, 1e-9);
        }

        [Test]
        public void Invariant_PredictedTime_EqualsServerNowPlusHalfRtt()
        {
            var nt = new FakeNetworkTime { ServerNow = 9.9, PredictedTime = 10.0, Rtt = 0.2 };
            Assert.AreEqual(nt.PredictedTime, nt.ServerNow + nt.Rtt * 0.5, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인 (컴파일 에러 = INetworkTime/extension 부재)**

UnityMCP `run_tests`(mode EditMode, 필터 `NetworkTimeExtensionsTests`, `unity_instance` 명시) 또는 컴파일 에러 확인.
Expected: FAIL — `INetworkTime` / `RemoteBackTime` 미정의로 컴파일 실패.

- [ ] **Step 3: `INetworkTime` 인터페이스 작성**

Create `Runtime/Scripts/Game/INetworkTime.cs`:

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

- [ ] **Step 4: 파생 extension 작성**

Create `Runtime/Scripts/Game/NetworkTimeExtensions.cs`:

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

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, `NetworkTimeExtensionsTests`, `unity_instance` 명시).
Expected: PASS (3 tests).

- [ ] **Step 6: 커밋 (GameFramework 저장소)**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/Game/INetworkTime.cs Runtime/Scripts/Game/NetworkTimeExtensions.cs Tests/Runtime/NetworkTimeExtensionsTests.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(network-time): add INetworkTime abstraction + RemoteBackTime extension

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> ⚠️ `.meta` 파일은 Unity가 생성한다. 커밋 전 Unity가 새 `.cs`의 `.meta`를 만들었는지 확인하고, 있으면 함께 `git add`한다(`INetworkTime.cs.meta`, `NetworkTimeExtensions.cs.meta`, `NetworkTimeExtensionsTests.cs.meta`).

---

### Task 2: 엔진 배선 + `GameEngine.NetworkTime` facade (GameFramework)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/IGameEngine.cs`
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/GameEngineBase.cs`
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/GameEngine.cs`

- [ ] **Step 1: `IGameEngine`에 `networkTime` 추가**

`IGameEngine.cs` — `tickUpdater` 다음 줄에 추가:

```csharp
        IEntityManager entityManager { get; }
        ITickUpdater tickUpdater { get; }
        INetworkTime networkTime { get; }
```

- [ ] **Step 2: `GameEngineBase`에 `networkTime` 프로퍼티 + 팩토리 + 배선**

`GameEngineBase.cs` 변경:

(a) 필드 추가 — `tickUpdater` 프로퍼티 다음:

```csharp
        public IEntityManager entityManager { get; private set; }
        public ITickUpdater tickUpdater { get; private set; }
        public INetworkTime networkTime { get; private set; }
```

(b) `InitializeAsync` 안, `entityManager` 획득 다음에 추가:

```csharp
            entityManager = GetComponent<IEntityManager>() ?? throw new ArgumentNullException(nameof(IEntityManager));

            networkTime = CreateNetworkTime();   // tickUpdater와 달리 null 허용. 클라·서버 양쪽 override.

            initialized = true;
```

(c) `DeinitializeAsync` 안, `tickUpdater = null;` 근처에 추가:

```csharp
            tickUpdater.onTick -= OnTick;
            tickUpdater = null;
            entityManager = null;
            networkTime = null;
```

(d) 클래스 끝부분(예: `DispatchEvent` 메서드 뒤)에 팩토리 추가:

```csharp
        /// <summary>
        /// 사이드별 네트워크 시간 소스를 생성한다. 기본 null.
        /// 클라·서버 각자가 override해 MirrorNetworkTime을 제공한다.
        /// </summary>
        protected virtual INetworkTime CreateNetworkTime() => null;
```

- [ ] **Step 3: `GameEngine.NetworkTime` facade 추가**

`GameEngine.cs` — `Time` nested 클래스 다음에 `NetworkTime` nested 클래스 추가:

```csharp
        public static class NetworkTime
        {
            public static double serverNow     => GameEngine.current.networkTime.ServerNow;
            public static double predictedTime => GameEngine.current.networkTime.PredictedTime;
            public static double rtt           => GameEngine.current.networkTime.Rtt;

            /// <summary> 원격 보간 back-time = RTT/2 (소비자가 clamp). </summary>
            public static double remoteBackTime => GameEngine.current.networkTime.RemoteBackTime();
        }
```

- [ ] **Step 4: 컴파일 확인 (GameFramework — 클라 인스턴스)**

UnityMCP `refresh_unity`(scope=all) 후 `read_console`(`unity_instance` 명시, errors).
Expected: 에러 0. (facade는 `current.networkTime`을 읽지만 아직 호출자 없음 → 런타임 NPE 없음.)

- [ ] **Step 5: 커밋 (GameFramework 저장소)**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/Game/IGameEngine.cs Runtime/Scripts/Game/GameEngineBase.cs Runtime/Scripts/Game/GameEngine.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(network-time): wire INetworkTime into engine (CreateNetworkTime template) + GameEngine.NetworkTime facade

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: `MirrorNetworkTime` + 클라·서버 `CreateNetworkTime()` override (LOP-Client / LOP-Server)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/MirrorNetworkTime.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGameEngine.cs`
- Create: `(서버) Assets/Scripts/Game/MirrorNetworkTime.cs`
- Modify: `(서버) Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: 클라 `MirrorNetworkTime` 작성 (평범 POCO)**

Create `Assets/Scripts/Game/MirrorNetworkTime.cs` (LOP-Client):

```csharp
using GameFramework;

namespace LOP
{
    /// <summary>
    /// Mirror NetworkTime을 INetworkTime으로 위임하는 클라 구현. 동작 보존(현 predictedTime/rtt 그대로).
    /// ServerNow = predictedTime − RTT/2 (서버 현재 시간 추정).
    /// </summary>
    public class MirrorNetworkTime : INetworkTime
    {
        public double ServerNow     => Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5;
        public double PredictedTime => Mirror.NetworkTime.predictedTime;
        public double Rtt           => Mirror.NetworkTime.rtt;
    }
}
```

- [ ] **Step 2: 클라 `LOPGameEngine`에서 override**

`LOPGameEngine.cs` (LOP-Client) — `UpdateEngine()` 위(또는 클래스 적당한 위치)에 추가:

```csharp
        protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();
```

(`using GameFramework;`는 이미 파일 상단에 존재 — `INetworkTime` 해석됨.)

- [ ] **Step 3: 서버 `MirrorNetworkTime` 작성 (평범 POCO)**

Create `Assets/Scripts/Game/MirrorNetworkTime.cs` (LOP-Server):

```csharp
using GameFramework;

namespace LOP
{
    /// <summary>
    /// Mirror NetworkTime을 INetworkTime으로 위임하는 서버 구현.
    /// 서버는 자기 권위 클럭 — ServerNow == PredictedTime, Rtt == 0.
    /// </summary>
    public class MirrorNetworkTime : INetworkTime
    {
        public double ServerNow     => Mirror.NetworkTime.time;
        public double PredictedTime => Mirror.NetworkTime.time;
        public double Rtt           => 0.0;
    }
}
```

- [ ] **Step 4: 서버 `LOPGameEngine`에서 override**

`LOPGameEngine.cs` (LOP-Server) — 클라와 동일 패턴:

```csharp
        protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();
```

- [ ] **Step 5: 컴파일 확인 (클라 인스턴스)**

UnityMCP `refresh_unity`(scope=all) 후 `read_console`(`unity_instance` 명시, errors).
Expected: 에러 0.

- [ ] **Step 6: 커밋 (LOP-Client 저장소, feature 브랜치)**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/MirrorNetworkTime.cs Assets/Scripts/Game/MirrorNetworkTime.cs.meta Assets/Scripts/Game/LOPGameEngine.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(network-time): MirrorNetworkTime (INetworkTime over NetworkTime) + LOPGameEngine.CreateNetworkTime override

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> ⚠️ `MirrorNetworkTime.cs.meta`는 Unity 생성. 없으면 refresh 후 생성될 때까지 기다린 뒤 add.

---

### Task 4: 호출부를 facade로 마이그레이션 (LOP-Client / LOP-Server)

**Files:**
- Modify: `Assets/Scripts/Game/LOPTickUpdater.cs:15` (클라)
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs:22,25`
- Modify: `Assets/Scripts/Entity/SnapInterpolator.cs:34`
- Modify: `(서버) Assets/Scripts/Game/LOPTickUpdater.cs`

- [ ] **Step 1: 클라 `LOPTickUpdater` 변경**

`LOPTickUpdater.cs` (클라) line 15:

변경 전:
```csharp
            double target = Mirror.NetworkTime.predictedTime + AheadMargin;
```
변경 후:
```csharp
            double target = GameEngine.NetworkTime.predictedTime + AheadMargin;
```

- [ ] **Step 2: 서버 `LOPTickUpdater` 변경**

서버 `LOPTickUpdater.cs` — Mirror 시간 직접 참조를 facade로:

변경 전 (예):
```csharp
            double target = Mirror.NetworkTime.time;
```
변경 후:
```csharp
            double target = GameEngine.NetworkTime.serverNow;
```

- [ ] **Step 3: `DebugHudViewModel` 변경**

`DebugHudViewModel.cs` line 22:

변경 전:
```csharp
        public double RttMs => Mirror.NetworkTime.rtt * 1000;
```
변경 후:
```csharp
        public double RttMs => GameEngine.NetworkTime.rtt * 1000;
```

`DebugHudViewModel.cs` line 24-25:

변경 전:
```csharp
        // 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)((Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5) / GameEngine.Time.tickInterval);
```
변경 후:
```csharp
        // 서버 현재 tick 추정 ≈ serverNow/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)(GameEngine.NetworkTime.serverNow / GameEngine.Time.tickInterval);
```

- [ ] **Step 4: `SnapInterpolator` 변경**

`SnapInterpolator.cs` line 34:

변경 전:
```csharp
            float targetBackTime = Mathf.Clamp((float)Mirror.NetworkTime.rtt * 0.5f, MIN_BACK_TIME, MAX_BACK_TIME);
```
변경 후:
```csharp
            float targetBackTime = Mathf.Clamp((float)GameEngine.NetworkTime.remoteBackTime, MIN_BACK_TIME, MAX_BACK_TIME);
```

- [ ] **Step 5: LOP 코드에 `Mirror.NetworkTime` 직접 참조가 없는지 확인**

Grep(`Mirror\.NetworkTime`, path `Assets/Scripts`).
Expected: `MirrorNetworkTime.cs` 외 **0 matches** (격리 지점이므로 의도된 단일 출현).

- [ ] **Step 6: 컴파일 확인 (클라 인스턴스)**

UnityMCP `refresh_unity`(scope=all) 후 `read_console`(`unity_instance` 명시, errors).
Expected: 에러 0.

- [ ] **Step 7: 커밋 (LOP-Client 저장소)**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/LOPTickUpdater.cs Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/Entity/SnapInterpolator.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "refactor(network-time): route NetworkTime call sites through GameEngine.NetworkTime facade

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: 종합 검증 (동작 보존 + 양 사이드 컴파일)

**Files:** (없음 — 검증 전용)

- [ ] **Step 1: 클라 EditMode 테스트 전체 통과**

UnityMCP `run_tests`(EditMode, `unity_instance`=클라) — 적어도 `NetworkTimeExtensionsTests` 통과 + 기존 테스트 무회귀.
Expected: 신규 3 PASS, 기존 무회귀.

- [ ] **Step 2: 서버 컴파일 확인 (GameFramework 공유 변경 영향)**

GameFramework이 공유 패키지라 서버 에디터도 재컴파일된다. **서버 인스턴스가 연결돼 있고 사용자가 허용하면**, `read_console`(`unity_instance`=서버, errors)로 에러 0 확인.
> CLAUDE.md: 서버 인스턴스 조작은 사용자 명시 허용 시에만. 미허용이면 이 스텝은 사용자에게 위임(서버 에디터에서 콘솔 확인 요청).

- [ ] **Step 3: 수동 동작 보존 확인 (parity)**

에디터 2-인스턴스 + Mirror LatencySimulation(현실적 RTT 주입)로 게임 실행:
- DebugHud의 **RTT / Lead / ServerTick 추정 / Recon 값**이 변경 전과 동일 범위인지 확인.
- 공중 이동/점프 시 비주얼 거동이 변경 전과 동일(추상화는 값을 안 바꿈)한지 육안 확인.
Expected: 변경 전후 차이 없음(순수 격리).

- [ ] **Step 4: 완료 — finishing 옵션 안내**

구현·검증 완료. `superpowers:finishing-a-development-branch`로 main `--no-ff` 머지 여부를 결정한다.
> ⚠️ GameFramework 변경은 **별도 저장소**에 이미 커밋됨(Task 1·2). LOP-Client feature 브랜치 머지와 별개로, GameFramework 커밋의 push/안정화 시점은 finishing 단계에서 함께 판단.

---

## Self-Review

**Spec coverage:**
- `INetworkTime`(ServerNow/PredictedTime/Rtt) → Task 1 ✓
- 파생(`remoteBackTime`) extension → Task 1 + Task 2(facade) ✓
- `ServerNow` = 인터페이스 멤버(extension 아님) → Task 1 인터페이스, facade `serverNow` 직접 포워딩 ✓
- MirrorNetworkTime(클라: predictedTime−RTT/2 / predictedTime / rtt; 서버: time / time / 0) → Task 3 ✓
- `GameEngine.NetworkTime` facade + 엔진 배선 → Task 2(`CreateNetworkTime` 기본 null) + Task 3(클·서 override) ✓
- 클라 호출부 마이그레이션 → Task 4 ✓
- 서버 `LOPTickUpdater` → `serverNow` 전환 → Task 4 ✓
- 동작 보존 / `Mirror.NetworkTime` 직접 참조 격리 → Task 4 Step 5 + Task 5 Step 3 ✓
- 서버 컴파일 검증 → Task 5 Step 2 ✓
- 테스트(GameFramework EditMode, `FakeNetworkTime`) → Task 1 ✓
- 산업 표준 매핑 / Out-of-scope(②③, ClockDilator) → 설계 문서(spec)에 박제, plan은 ① 구현만 ✓

**Placeholder scan:** 모든 코드 스텝에 완전한 코드 블록. "적절한 처리" 류 없음. ✓

**Type consistency:** `INetworkTime { ServerNow, PredictedTime, Rtt }` (double) — Task 1·2·3 일관. extension명 `RemoteBackTime()` — Task 1 정의 = Task 2 facade 호출 일치. facade `serverNow/predictedTime/rtt/remoteBackTime` — Task 2 정의 = Task 4 호출 일치. `CreateNetworkTime()` — Task 2 정의 = Task 3 override 일치. ✓
