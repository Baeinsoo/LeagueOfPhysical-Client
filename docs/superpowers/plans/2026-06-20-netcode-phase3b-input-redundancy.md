# Netcode Phase 3b — 입력 Unreliable 전환 + Sliding-Window Redundancy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 입력을 reliable → **unreliable 채널**로 보내 head-of-line blocking 지연을 없애고, 패킷마다 **최근 N틱 입력을 함께(sliding-window redundancy)** 실어 유실을 무대기로 복구한다.

**Architecture:** LOP-Shared proto `PlayerInputToS`에 `repeated PlayerInputEntry recent_inputs` 추가(기존 필드 유지). 클라 `PlayerInputManager`가 최근 N틱(3)을 ring buffer로 모아 매 전송 시 `recent_inputs`에 담고 **unreliable**로 송신. 서버 핸들러가 `recent_inputs` 각 엔트리를 `AddInput`으로 투입(이미 있는 tick은 dedup). 채널 선택은 `ISession.Send<T>(T, bool reliable = true)`(GameFramework 중립, Mirror 비의존)로 표현하고 클·서 `LOPSession`이 `Channels.Reliable/Unreliable`로 매핑.

**Tech Stack:** C# (Unity), Mirror(KCP unreliable 채널), Protobuf(LOP-Shared codegen `generate_protos.sh`), VContainer, UnityMCP 컴파일 검증. 자동 테스트 없음(클·서 단일 Assembly-CSharp, LOP 글루) — 컴파일 + 수동 플레이.

**Related spec:** `docs/superpowers/specs/2026-06-20-netcode-phase3-input-buffer-design.md` (Slice 3b). 선행: 3a(서버 command-frame 정렬, 머지 완료).

**Resolved Unity instances (매 UnityMCP 호출에 명시 — HTTP stateless):**
- Client: `mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 `id`(현재 `LeagueOfPhysical-Client@de70658b9450cbb4`, 변동 가능).
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(변동 시 재해석). 사용자 지시 netcode 작업 — 서버 인스턴스 조작 인가.
- GameFramework/LOP-Shared는 `file:` 공유라 클라(또는 서버) 에디터에서 컴파일 검증.

> **잠긴 결정 (spec이 plan에 위임):**
> - proto: `PlayerInputEntry {int64 tick=1; PlayerInput player_input=2;}` 신설(@auto_generate 없음 — 필드 타입용, MessageId 불필요) + `PlayerInputToS`에 `repeated PlayerInputEntry recent_inputs=5` 추가. **기존 필드(session_id/tick/player_input/entity_transform) 유지** — session_id는 라우팅에 계속 사용, 나머지는 호환용. 서버는 **recent_inputs만** 소비(top-level tick/player_input 무시 — 중복 방지, recent_inputs가 현재 포함).
> - 채널 API: `ISession.Send<T>(T message, bool reliable = true)` — GameFramework 중립(Mirror 타입 비노출). 기존 호출(`Send(x)`)은 reliable=true로 그대로 동작. 입력만 `reliable:false`.
> - N(redundancy window) = **3**(현재 포함 최근 3틱).
> - `entity_transform`은 서버 미사용(LOPMovementManager 무시) — recent_inputs 엔트리엔 미포함, 서버가 per-entry 구성 시 null이어도 안전(AutoMapper null→null).

> **픽스처:** 서버 `LOPGame.cs`/`ConfigureRoomComponent.cs`(dirty 로컬 픽스처)는 **미접촉** → 커밋 시 제외만. `.git/index.lock` 에러 시 `rm -f .git/index.lock` 후 재시도. 클라 무관 dirty(`UIRoot.prefab`/`Room.unity`[측정 설정] 등) 미접촉.

---

## File Structure

- **Modify (LOP-Shared):** `Protos/PlayerInputToS.proto` — `PlayerInputEntry` + `recent_inputs`. 재생성 산출물(`Runtime.Generated/Scripts/Protobuf/*`) 커밋.
- **Modify (GameFramework):** `Runtime/Scripts/Session/ISession.cs` — `Send<T>(T, bool reliable = true)`.
- **Modify (Client):** `Assets/Scripts/Room/LOPSession.cs` — Send 시그니처+채널 매핑. `Assets/Scripts/Game/PlayerInputManager.cs` — ring buffer + recent_inputs + `reliable:false`.
- **Modify (Server):** `Assets/Scripts/Room/LOPSession.cs` — Send 시그니처+채널 매핑. `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs` — recent_inputs 투입. `Assets/Scripts/Component/EntityInputComponent.cs` — `AddInput` 중복/redundancy 경고 quiet.

순서: **Task 1(proto) + Task 2(ISession)** 선행(둘 다 클·서 컴파일 전제) → Task 3(클라) → Task 4(서버). 1·2는 상호 독립.

---

## Task 1: LOP-Shared proto — recent_inputs 추가 + 재생성

**Files:** Modify `LeagueOfPhysical-Shared/Protos/PlayerInputToS.proto`, regenerate.

- [ ] **Step 1: Shared 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" checkout -b feature/netcode-phase3-input-redundancy
```
(dirty 있으면 확인.)

- [ ] **Step 2: `Protos/PlayerInputToS.proto` 전체 교체**
```proto
syntax = "proto3";

import "PlayerInput.proto";
import "ProtoTransform.proto";

// 패킷당 redundancy로 묶어 보낼 단일 입력 엔트리(필드 타입 — wire 직접 전송 안 함, @auto_generate 없음).
message PlayerInputEntry
{
	int64 tick = 1;
	PlayerInput player_input = 2;
}

// @auto_generate
message PlayerInputToS
{
	string session_id = 1;
	int64 tick = 2;
	PlayerInput player_input = 3;
	ProtoTransform entity_transform = 4;
	repeated PlayerInputEntry recent_inputs = 5;  // sliding-window redundancy: 최근 N틱(현재 포함)
}
```

- [ ] **Step 3: proto 재생성**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts" && bash ./generate_protos.sh
```
Expected: "All proto-related scripts executed successfully." 산출물 `../Runtime.Generated/Scripts/Protobuf/PlayerInputToS.cs`에 `RecentInputs`(RepeatedField) + 신규 `PlayerInputEntry.cs` 생성. `MessageIds.cs`는 PlayerInputToS ID 불변(PlayerInputEntry는 @auto_generate 없어 ID 미부여).
> bash/protoc 환경 문제로 스크립트 실패 시: 사용자에게 `Scripts/generate_protos.sh` 수동 실행 요청(이 단계만 위임), 산출물 생성 확인 후 진행.

- [ ] **Step 4: 클라 컴파일로 산출물 검증** (file: 공유 → 클라가 새 proto 코드 봄)
- 클라 id 해석 후 `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")` + `read_console(types=["error"], unity_instance="<클라 id>")` → 0 errors. (아직 사용처 없음 — proto 코드만 추가라 비파괴.)

- [ ] **Step 5: Shared 커밋** (proto + 재생성 산출물)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add Protos/PlayerInputToS.proto Runtime.Generated/Scripts/Protobuf/
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" status --short
```
- 산출물 변경(`PlayerInputToS.cs`, 신규 `PlayerInputEntry.cs`+`.meta`) 확인 후:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add -A Runtime.Generated/
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" commit -m "feat(netcode): PlayerInputToS.recent_inputs (sliding-window redundancy) + PlayerInputEntry

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 6:** `git -C "..." show --stat HEAD` — `PlayerInputToS.proto` + Generated 산출물만. 무관 파일 없으면 OK.

---

## Task 2: GameFramework — ISession.Send 채널 인자

**Files:** Modify `GameFramework/Runtime/Scripts/Session/ISession.cs`

- [ ] **Step 1: GameFramework 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" status --short
git -C "C:/Users/re5na/workspace/LOP/GameFramework" checkout -b feature/netcode-phase3-input-redundancy
```

- [ ] **Step 2: `ISession.cs` Send 시그니처 변경** — 현재:
```csharp
        void Send<T>(T message) where T : IMessage;
```
→
```csharp
        // reliable=false면 unreliable 채널로(시간민감 입력용). 채널 매핑은 구현체(LOPSession) 책임 — GameFramework는 Mirror 비의존.
        void Send<T>(T message, bool reliable = true) where T : IMessage;
```

- [ ] **Step 3: GameFramework 컴파일 0에러** — GameFramework엔 ISession 구현체가 없어 단독 컴파일 통과. 클라 에디터로 확인:
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")` + `read_console(types=["error"], unity_instance="<클라 id>")`.
> ⚠️ 이 시점엔 **클라/서버 LOPSession이 새 인터페이스 미구현이라 클라 컴파일 에러가 날 수 있음**(Task 3에서 클라 LOPSession 수정하면 해소). 에러가 `LOPSession이 Send<T>(T,bool)을 구현 안 함`류 1건이면 정상 — Task 3로 진행. 그 외 에러는 중단·점검.

- [ ] **Step 4: GameFramework 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/Session/ISession.cs
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(netcode): ISession.Send gains optional reliable flag (channel select, Mirror-neutral)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 5:** `git -C "..." show --stat HEAD | head -6` — `ISession.cs` 1파일만.

---

## Task 3: 클라 — LOPSession 채널 매핑 + PlayerInputManager redundancy/unreliable

**Files:** Modify `Assets/Scripts/Room/LOPSession.cs`, `Assets/Scripts/Game/PlayerInputManager.cs`

- [ ] **Step 1: 클라 피처 브랜치** (plan이 이미 이 브랜치에 커밋돼 있음 — checkout만)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" branch --show-current   # feature/netcode-phase3-input-redundancy 기대
```
(아니면 `git checkout feature/netcode-phase3-input-redundancy`. 무관 dirty 미접촉.)

- [ ] **Step 2: `LOPSession.cs` Send 시그니처+채널 매핑** — 현재:
```csharp
        public void Send<T>(T message) where T : IMessage
        {
            if (isConnected == false)
            {
                return;
            }

            networkConnection.Send(new CustomMirrorMessage
            {
                payload = message,
            });
        }
```
→
```csharp
        public void Send<T>(T message, bool reliable = true) where T : IMessage
        {
            if (isConnected == false)
            {
                return;
            }

            int channelId = reliable ? Channels.Reliable : Channels.Unreliable;
            networkConnection.Send(new CustomMirrorMessage
            {
                payload = message,
            }, channelId);
        }
```
(`using Mirror;` 이미 있음 — `Channels`/`NetworkConnection` 참조. `networkConnection.Send`의 2번째 인자가 channelId.)

- [ ] **Step 3: `PlayerInputManager.cs` — ring buffer + recent_inputs + unreliable.** 클래스에 필드 추가(다른 필드 옆):
```csharp
        private const int RedundancyWindow = 3;  // 패킷당 최근 N틱 입력(현재 포함) — sliding-window redundancy
        private readonly System.Collections.Generic.List<PlayerInputEntry> recentInputs = new System.Collections.Generic.List<PlayerInputEntry>();
```

- [ ] **Step 4: `PlayerInputManager.cs` 송신부 교체** — 현재(라인 ~45-66):
```csharp
                PlayerInputToS playerInputToS = new PlayerInputToS();
                playerInputToS.Tick = GameEngine.Time.tick;
                playerInputToS.SessionId = playerContext.session.sessionId;
                playerInputToS.PlayerInput = new global::PlayerInput
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    Horizontal = playerInput.horizontal,
                    Vertical = playerInput.vertical,
                    Jump = playerInput.jump,
                    ActionCode = playerInput.actionCode ?? "",
                };

                EntityTransform entityTransform = new EntityTransform
                {
                    position = playerContext.entity.position,
                    rotation = playerContext.entity.rotation,
                    velocity = playerContext.entity.velocity,
                };
                playerInputToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);

                // Send to server.
                playerContext.session.Send(playerInputToS);
```
→
```csharp
                PlayerInputToS playerInputToS = new PlayerInputToS();
                playerInputToS.Tick = GameEngine.Time.tick;
                playerInputToS.SessionId = playerContext.session.sessionId;
                playerInputToS.PlayerInput = new global::PlayerInput
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    Horizontal = playerInput.horizontal,
                    Vertical = playerInput.vertical,
                    Jump = playerInput.jump,
                    ActionCode = playerInput.actionCode ?? "",
                };

                EntityTransform entityTransform = new EntityTransform
                {
                    position = playerContext.entity.position,
                    rotation = playerContext.entity.rotation,
                    velocity = playerContext.entity.velocity,
                };
                playerInputToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);

                // sliding-window redundancy: 최근 N틱(현재 포함)을 함께 실어 패킷 유실에 대비.
                recentInputs.Add(new PlayerInputEntry
                {
                    Tick = playerInputToS.Tick,
                    PlayerInput = playerInputToS.PlayerInput,
                });
                while (recentInputs.Count > RedundancyWindow)
                {
                    recentInputs.RemoveAt(0);
                }
                playerInputToS.RecentInputs.AddRange(recentInputs);

                // Send to server (unreliable — 시간민감 입력. 유실은 redundancy로 복구, head-of-line blocking 회피).
                playerContext.session.Send(playerInputToS, reliable: false);
```
(`PlayerInputEntry`/`RecentInputs`는 Task 1 proto 산출물. `RecentInputs`는 RepeatedField라 `AddRange` 가능. recentInputs 리스트의 각 엔트리는 그 틱의 고유 PlayerInput 인스턴스를 참조 — 매 틱 새 PlayerInput을 new 하므로 aliasing 없음.)

- [ ] **Step 5: 클라 컴파일 0에러** — `refresh_unity(...)` + `read_console(types=["error"], unity_instance="<클라 id>")` → 0 errors. (Task 1 proto + Task 2 ISession 반영됨 → LOPSession이 새 인터페이스 구현 → Task 2에서 났던 에러 해소.)

- [ ] **Step 6: 클라 커밋** (이 2파일만)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Room/LOPSession.cs Assets/Scripts/Game/PlayerInputManager.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(netcode): client sends input unreliable + sliding-window redundancy (recent N ticks) (Phase 3b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 7:** `git -C "..." show --stat HEAD | head -6` — `LOPSession.cs` + `PlayerInputManager.cs` 2파일만(무관 dirty 제외).

---

## Task 4: 서버 — LOPSession 매핑 + 핸들러 redundancy 투입 + AddInput quiet

**Files:** Modify `Assets/Scripts/Room/LOPSession.cs`, `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs`, `Assets/Scripts/Component/EntityInputComponent.cs`

- [ ] **Step 1: 서버 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/netcode-phase3-input-redundancy
```
Expected dirty: `LOPGame.cs`+`ConfigureRoomComponent.cs`(픽스처, 미접촉).

- [ ] **Step 2: 서버 `LOPSession.cs` Send 시그니처+채널 매핑** — 클라 Task 3 Step 2와 동일 패턴. 현재 `public void Send<T>(T message) where T : IMessage` 본문을 reliable 파라미터 추가형으로:
```csharp
        public void Send<T>(T message, bool reliable = true) where T : IMessage
        {
            // ... 기존 isConnected 가드 등 본문 유지 ...
            int channelId = reliable ? Channels.Reliable : Channels.Unreliable;
            networkConnection.Send(new CustomMirrorMessage
            {
                payload = message,
            }, channelId);
        }
```
(서버 LOPSession 본문 형태에 맞춰 시그니처에 `bool reliable = true` 추가 + `networkConnection.Send(..., channelId)`. 서버는 입력 수신만이라 기능상 reliable 유지 — 인터페이스 구현 일치 위해 시그니처만 맞춤. `using Mirror;` 확인.)

- [ ] **Step 3: `Game.Input.MessageHandler.cs` — recent_inputs 투입.** 현재 `OnPlayerInputToS`의 입력 투입부:
```csharp
            ISession session = sessionManager.GetSessionById(playerInputToS.SessionId);
            LOPEntity entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(session.userId);
            entity.GetEntityComponent<EntityInputComponent>().AddInput(playerInputToS);
```
→
```csharp
            ISession session = sessionManager.GetSessionById(playerInputToS.SessionId);
            LOPEntity entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(session.userId);
            EntityInputComponent inputComponent = entity.GetEntityComponent<EntityInputComponent>();

            // sliding-window redundancy: recent_inputs의 각 틱을 투입(이미 있는 tick은 AddInput이 dedup).
            // 유실된 틱이 다음 패킷의 redundancy로 채워진다.
            foreach (var entry in playerInputToS.RecentInputs)
            {
                inputComponent.AddInput(entry.Tick, entry.PlayerInput);
            }
```
(상단 `PlayerInput playerInput = new PlayerInput {...}` 지역 변수 블록은 이제 미사용이면 제거. recent_inputs가 비어 올 일은 없음 — 클라가 항상 현재 포함해 채움. 방어로 비었으면 no-op.)

- [ ] **Step 4: `EntityInputComponent.cs` — `AddInput(long, PlayerInput)` 오버로드 + redundancy quiet.** 현재 `AddInput(PlayerInputToS input)`을 다음으로 교체(시그니처 변경 + 중복/redundancy 경고 제거):
```csharp
        public void AddInput(long tick, PlayerInput playerInput)
        {
            // redundancy로 같은 입력이 여러 번 와도 정상 — 이미 처리됐거나 버퍼에 있으면 조용히 무시(dedup).
            if (playerInput.SequenceNumber <= lastProcessedSequence)
            {
                return;
            }

            if (inputBuffer.ContainsKey(tick) == false)
            {
                inputBuffer.Add(tick, new PlayerInputToS
                {
                    Tick = tick,
                    PlayerInput = playerInput,
                });
                expectedNextSequence = playerInput.SequenceNumber + 1;
            }
        }
```
- 기존 `AddInput(PlayerInputToS input)`는 제거(호출처는 핸들러뿐 — Task 3 step3에서 새 오버로드 호출로 전환). 만약 다른 호출처가 있으면(grep 확인) 함께 전환.
- 버퍼 저장형은 `SortedDictionary<long, PlayerInputToS>` 유지(GetInput/ProcessInput 호환). 엔트리에서 `new PlayerInputToS{Tick, PlayerInput}` 구성(session_id/entity_transform 미설정 — 서버 GetInput/ProcessInput에서 entity_transform은 미사용[LOPMovementManager 무시], AutoMapper null→null 안전).
- **경고 제거 근거**: redundancy로 중복/이미처리 입력이 *정상적으로 자주* 옴 → 기존 `Debug.LogWarning`(이미 처리/중복 tick/시퀀스 갭)은 스팸이 되므로 제거. dedup은 조용히.

- [ ] **Step 5: 호출처 확인** — `grep "AddInput" Assets/Scripts`로 `AddInput(PlayerInputToS)` 잔존 호출 없는지 확인. 핸들러(Step 3)만 있어야 함.

- [ ] **Step 6: 서버 컴파일 0에러** — `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` + `read_console(types=["error"], unity_instance="...")` → 0 errors. (Task 1 proto + Task 2 ISession 반영.)

- [ ] **Step 7: 서버 커밋** (3파일 — 픽스처 제외)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Room/LOPSession.cs Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs Assets/Scripts/Component/EntityInputComponent.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(netcode): server consumes recent_inputs redundancy (dedup) + channel-aware Send (Phase 3b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 8:** `git -C "..." show --stat HEAD | head -8` — 3파일만. `LOPGame.cs`/`ConfigureRoomComponent.cs` 포함되면 중단.

---

## Task 5: 런타임 수동 검증 (사용자)

클·서 플레이, **LatencySimulation에 packet loss % 주입**(예: latency 100ms + loss 10~20%):

1. **입력 유실이 화면에 안 보임** — loss를 줘도 캐릭터가 끊김 없이 이동(redundancy가 다음 패킷에서 복구). loss 없이도 정상.
2. **head-of-line blocking 지연 감소** — reliable 때보다 입력 반응이 매끄러움. recon 동등/개선.
3. **경고 스팸 없음** — 서버 콘솔에 redundancy로 인한 "이미 처리/중복 tick" 경고가 안 뜸(Step 4에서 quiet).
4. **이동/액션/점프 정상**, 지각 입력 잔여 없음.
5. **콘솔 에러 0** (양쪽).

비교: loss를 0%→20%로 올렸을 때, reliable(이전)이라면 끊김/지연이 보였을 상황에서 unreliable+redundancy는 매끄러우면 성공. 측정 후 LatencySimulation 원복(Room.unity 커밋 안 함).

---

## 완료 기준

- [ ] LOP-Shared `feature/netcode-phase3-input-redundancy`: proto + 산출물 커밋.
- [ ] GameFramework: ISession 커밋.
- [ ] 클라: LOPSession + PlayerInputManager 커밋. 서버: LOPSession + 핸들러 + EntityInputComponent 커밋.
- [ ] 클·서 컴파일 0에러. 서버 픽스처 보존(미커밋), 무관 dirty 미접촉.
- [ ] 수동 플레이: packet loss에도 입력 매끄러움, 경고 스팸 없음, recon 동등/개선.

이후: 사용자 검증 후 **LOP-Shared + GameFramework + 클·서** 각 `feature/netcode-phase3-input-redundancy` → main `--no-ff` 머지(클라는 spec/plan 커밋도 함께; 서버는 LOPGame 미접촉이라 stash 댄스 불필요). netcode Phase 3 완료. 이후 Phase 4(lead 동적 피드백)/Phase 5(점프 임펄스)/Stage④.
