# Archery 후속 — 발사를 사건으로 보낸다

> **For agentic workers:** 이 문서가 요구사항이다. 값과 이름은 여기 적힌 그대로 쓴다.

**Goal:** 남이 쏜 화살이 내 화면에도 보이게 한다.

**Spec:** `docs/superpowers/specs/2026-09-11-archery-game-mode-design.md` (§7 넷코드)

---

## 1. 왜 필요한가 — 실측으로 확정된 것

슬라이스 1은 남의 발사를 **입력 메아리**(서버가 남의 입력을 되뿌리는 채널)로 나르기로 했다.
그 경로는 **닿기는 하는데 읽히지 않는다.** 2026-09-11 실측:

```
[RemoteProbe] 2 myTick=2332 buf=65 ticks=2258~2322 lag=10 current=null sim=True
```

- 남의 입력은 **항상 9~10틱 늦게** 온다(정상이다 — 남의 클라 → 서버 → 나).
- 그런데 꺼내는 코드가 **현재 틱과 정확히 같은 것만** 찾는다:
  `buffer.Current = buffer.Commands.TryGetValue(tick, ...) ? ... : null`
- 그래서 **매번 null**이고, 발사 불리언이 실린 그 칸을 아무도 열지 않는다.

**다른 모드가 멀쩡한 이유**: 늦은 입력의 진짜 소비처는 **되감기 재생**이다
(`Reconciler`: `for (t = anchor+1 .. now) { remoteInput.ApplyAll(t); world.Tick(t); }` —
주석에 *"남의 입력이 늦게 도착하면 바로 이 재생이 그 구간을 정확한 궤적으로 고쳐 준다"*).
Flappy·Skydive는 내가 움직여 예측이 계속 어긋나니 재생이 늘 돈다. **활쏘기는 아무도 안 움직여
재생이 사실상 안 돈다** — 그래서 이 모드만 정면으로 부딪힌다.

### 업계 기준

| 성격 | 어떻게 나르나 | 어긋나면 |
|---|---|---|
| 연속·곧 덮이는 것(위치·자세) | 스냅샷이 진실, 입력 재생은 보조 | 묻고 간다(다음 스냅이 재고정) |
| **이산·결과가 남는 것(발사·명중·사망)** | **서버가 판정해 사건으로 내려보낸다** | 묻고 가면 안 된다 |

발사는 아래쪽인데 위쪽 방식에 얹혀 있었다. 이 문서가 그걸 바로잡는다.

---

## 2. 설계 — 기존 폴리모픽 이벤트 통로를 쓴다

**새 와이어 메시지를 만들지 않는다.** 이 프로젝트엔 이미 단일 폴리모픽 봉투가 있다:

```
서버: WorldEventBuffer → WorldEventSink → WorldEventWire.ToWire → WorldEventBatchToC (세션당 1패킷)
클라: GameWorldEventMessageHandler → 코어 이벤트로 재수화
```

`WorldEventToC.proto` 주석: *"새 WorldEvent 타입 = oneof에 한 줄 추가"*, 그리고
*"top-level 패킷 아님(@auto_generate 없음)"* — 즉 **MessageId를 다시 생성하지 않는다.**
(그 재생성이 기존 id를 밀어 와이어를 조용히 깨뜨리는 사고가 이 프로젝트에 있었다.)

### 흐름

```
서버 ArcheryWorld: 발사 확정 → shots에 추가 + ArcheryShotFiredEvent를 EventBuffer에 append
   → WorldEventSink가 배치에 실어 전원에게 1패킷
클라: 배치 수신 → 내가 쏜 것이면 버린다(이미 예측해 그리고 있다)
                 → 남이 쏜 것이면 ArcheryWorld.IngestRemoteShot(shot)
   → 기존 ArcheryArrowView가 그대로 그린다(코드 변경 없음)
```

**내 화살은 지금도 잘 보인다** — 예측으로 만들고 그린다. 이 작업은 **남의 것만** 채운다.

---

## 3. File Structure

| 파일 | 책임 |
|---|---|
| **LOP-Shared** | |
| `Runtime/Scripts/Game/ArcheryShotFiredEvent.cs` | 발사 사건 레코드(불변) |
| `Runtime/Scripts/Game/ArcheryWorld.cs` | (수정) 발사 확정 시 이벤트 append + `IngestRemoteShot` |
| `Protos/ArcheryShotToC.proto` | 사건 payload |
| `Protos/WorldEventToC.proto` | (수정) oneof에 한 줄 |
| `Runtime/Scripts/.../WorldEventWire.cs` | (수정) 코어↔와이어 변환 — **실제 경로는 grep으로 찾을 것** |
| **LOP-Client** | |
| `Assets/Scripts/Game/MessageHandler/ArcheryRemoteShotHandler.cs` | 배치에서 발사 사건만 골라 월드에 넣는다 |
| `Assets/Scripts/Game/ArcheryLifetimeScope.cs` | (수정) 위 핸들러 등록 |
| **LOP-Server** | |
| (없음 — `WorldEventSink`는 `WorldEventWire`만 부르므로 공유 변환만 고치면 된다. **확인할 것**) |

---

## Task 1: 사건 타입과 와이어

- [ ] **Step 1: 변환기 위치를 먼저 확인한다**

```bash
grep -rn "class WorldEventWire" --include=*.cs C:/Users/re5na/workspace/LOP
grep -rn "ToWire\|FromWire" --include=*.cs C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared | head
```

양방향(서버 송신 `ToWire` / 클라 수신)이 한 파일에 있는지, 클라 쪽이 따로인지 확인하고 그 구조를 따른다.

- [ ] **Step 2: 사건 레코드를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryShotFiredEvent.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살 한 발이 떠났다는 사실. <b>서버가 확정해 내려보내는 이산 사건</b>이다 —
    /// 남의 발사를 입력 재생으로 되살리려 하면 늦게 도착한 입력이 영영 안 읽혀서 실패한다
    /// (2026-09-11 실측: lag 9~10틱, current 항상 null).
    /// </summary>
    public sealed record ArcheryShotFiredEvent(
        string shooterId,
        long fireTick,
        Vector3 origin,
        Vector3 velocity
    ) : GameFramework.World.WorldEvent;
}
```

- [ ] **Step 3: proto payload + oneof 한 줄**

`LeagueOfPhysical-Shared/Protos/ArcheryShotToC.proto` (새 파일):

```proto
syntax = "proto3";
import "ProtoVector3.proto";

// 폴리모픽 래퍼 안에 담기는 payload — top-level 패킷 아님(@auto_generate 없음).
message ArcheryShotToC
{
	string       shooter_id = 1;
	int64        fire_tick  = 2;
	ProtoVector3 origin     = 3;
	ProtoVector3 velocity   = 4;
}
```

`WorldEventToC.proto` 수정 — import 추가 + oneof에 **비어 있는 다음 번호**로 한 줄:

```proto
		ArcheryShotToC      archery_shot      = 3;
```

- [ ] **Step 4: 생성 — MessageIds가 안 움직이는 것을 확인한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
bash Scripts/compile_protos.sh
git diff --stat Runtime.Generated/Scripts/MessageIds.cs    # ← 반드시 0줄
```

**0줄이 아니면 거기서 멈추고 보고한다.** (새 top-level 메시지를 만들지 않았으므로 0이어야 한다.)

- [ ] **Step 5: 변환기에 양방향을 더한다**

Step 1에서 찾은 곳에 `ArcheryShotFiredEvent ↔ ArcheryShotToC`를 더한다. 기존 `DamageDealtEvent`/
`AbilityActivatedEvent` 항목과 **같은 모양**으로. `Vector3 ↔ ProtoVector3` 변환은 기존 코드가 쓰는
방식을 그대로 쓴다(직접 지어내지 말 것 — `ProtoTransform`/`ProtoVector3`를 쓰는 곳을 보고 따른다).

- [ ] **Step 6: 커밋** (Shared 레포, 경로 지정)

---

## Task 2: 월드가 사건을 내고, 남의 것을 받아들인다

- [ ] **Step 1: `ArcheryWorld`가 발사 시 사건을 낸다**

`Mutation`에서 `shots.Add(shot.Value)` 하는 자리 바로 뒤에:

```csharp
                    // 남은 이 사건으로만 내 발사를 안다 — 입력 메아리는 늦게 와서 안 읽힌다.
                    EventBuffer.Add(new ArcheryShotFiredEvent(
                        shot.Value.ShooterId, shot.Value.FireTick, shot.Value.Origin, shot.Value.Velocity));
```

`EventBuffer`의 실제 이름·추가 메서드는 `WorldBase`/`WorldEventBuffer`에서 확인해 맞춘다
(`Add`가 아니라 다른 이름일 수 있다).

- [ ] **Step 2: 남의 발사를 받아들이는 입구를 연다**

```csharp
        /// <summary>
        /// 서버가 확정한 남의 발사를 받아들인다. 내 발사는 예측으로 이미 목록에 있으므로
        /// 부르는 쪽이 걸러서 넣는다(같은 발이 두 번 그려지지 않게).
        /// </summary>
        public void IngestRemoteShot(in ArcheryShot shot)
        {
            shots.Add(shot);
        }
```

- [ ] **Step 3: 테스트 — 사건이 실제로 나오는지**

`Tests/EditMode/ArcheryWorldTests.cs`에 추가. **사건을 안 내면 빨개지는** 단언이어야 한다:

```csharp
        [Test]
        public void 쏘면_발사_사건이_이벤트_버퍼에_쌓인다()
        {
            var (world, _, archer) = Make();

            Feed(archer, drawing: true, release: false);
            world.Tick(1, TickInterval);
            Feed(archer, drawing: false, release: true);
            world.Tick(2, TickInterval);

            int fired = 0;
            foreach (var e in world.EventBuffer.Snapshot)   // 실제 조회 API에 맞출 것
            {
                if (e is ArcheryShotFiredEvent) { fired++; }
            }
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void 받아들인_남의_발사가_목록에_들어간다()
        {
            var (world, _, _) = Make();

            world.IngestRemoteShot(new ArcheryShot("archer-9", 5, Vector3.zero, new Vector3(0f, 0f, 30f)));

            Assert.AreEqual(1, world.Shots.Count);
            Assert.AreEqual("archer-9", world.Shots[0].ShooterId);
        }
```

- [ ] **Step 4: 컴파일·테스트** — 기준선 **1217** + 신규 2 = **1219**
- [ ] **Step 5: 커밋**

---

## Task 3: 클라가 남의 발사를 받아 넣는다

- [ ] **Step 1: 핸들러를 만든다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/ArcheryRemoteShotHandler.cs`.
**같은 폴더의 `GameWorldEventMessageHandler.cs`를 정답지로** 삼아 같은 모양(구독·해제)으로 쓴다.

핵심 규칙 셋:

1. `WorldEventBatchToC`를 구독해 `archery_shot` 레코드만 고른다.
2. **`shooterId == playerContext.entityId`면 버린다** — 내 발은 이미 예측으로 목록에 있다.
   안 거르면 **같은 발이 두 번 그려진다.**
3. 나머지는 `archeryWorld.IngestRemoteShot(...)`.

- [ ] **Step 2: 스코프에 등록한다**

`ArcheryLifetimeScope.ConfigureGame`에 `builder.RegisterEntryPoint<ArcheryRemoteShotHandler>();`
(형제 메시지 핸들러가 어떻게 등록되는지 확인하고 그 방식에 맞춘다.)

- [ ] **Step 3: 컴파일 확인** (클·서 양쪽)
- [ ] **Step 4: 커밋** (클라 레포)

---

## 알려진 한계 (이번엔 고치지 않는다)

- **되감기가 일어나면 받아들인 남의 화살이 사라질 수 있다** — `LoadGameState`가 `shots`를 그 틱의
  목록으로 되돌리기 때문. 활쏘기는 아무도 안 움직여 되감기가 사실상 안 도는 것이 이 문제의 출발점이라,
  지금은 부딪히지 않는다. 움직임이 생기는 슬라이스에서 다시 본다.
- **진단 로그 `[RemoteProbe]`** (`RemoteInputSystem.cs`)는 원인 확인용 임시다. 이 작업이 끝나고
  실측으로 확인되면 **지운다.**

## 완료 기준

- [ ] `MessageIds.cs` diff 0줄
- [ ] EditMode 1219개 통과
- [ ] 클·서 컴파일 에러 0
- [ ] **Player 2가 쏜 화살이 Player 1 화면에 보인다** (실측)
- [ ] 내 화살이 **두 번 그려지지 않는다**
