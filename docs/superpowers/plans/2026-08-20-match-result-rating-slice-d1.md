# 슬라이스 D1 — 결과 화면 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 한 판이 끝나면 결과 화면에 참가자 등수표와 내 점수 변화가 뜬다.

**Architecture:** 게임서버는 이미 슬라이스 C에서 로비의 확정 응답(참가자별 `placement`/`mmrBefore`/`mmrAfter`)을
손에 쥔 채 `MatchEndedToC`를 뿌린다. 그 빈 메시지에 결과를 실어 보내되, **수신자마다 다른 메시지**를 만들어
등수는 전원 것을, 점수 변화는 본인 것만 담는다. 클라는 그걸 Root 스코프 `MatchResultDataStore`에 남기고
(씬 교체를 건너 살아남는 기존 경로), 로비 진입 직후 `FrontEndCoordinator`가 여는 결과 화면이 표시한다.

**Tech Stack:** Protobuf(protoc, LOP-Shared `Scripts/generate_protos.sh`) · Mirror 세션 전송 ·
VContainer · UI Toolkit(UXML/USS)

**Spec:** `docs/superpowers/specs/2026-08-17-match-result-rating-design.md` (§9 "클라 — 이미 빈 자리가 둘 있다")

---

## Global Constraints

- **레포 3개**: `LeagueOfPhysical-Shared`(proto) → `LeagueOfPhysical-Server`(채워 보내기) →
  `LeagueOfPhysical-Client`(표시). **infrastructure는 이번에 안 건드린다** — proto는 Shared에 산다
  (infrastructure에 있는 건 MasterData 표다).
- **`MessageIds.cs`의 기존 ID는 한 개도 바뀌면 안 된다.** `MatchEndedToC = 2`가 유지돼야 한다
  (배포된 클·서와 wire desync). `generate_protos.sh`는 이 파일을 지우지 않고 기존 ID를 보존하도록
  이미 고쳐져 있다 — 그래도 **재생성 후 diff로 확인**한다.
- **와이어에 남의 mmr을 싣지 않는다.** UI에서만 가리는 것은 결정 위반이다.
- **보고 실패 시에도 방 닫기·클라 통보·배수·종료는 그대로 진행된다.** 이 성질을 깨면 클라가 끝난 방에
  갇힌다. 결과가 없으면 빈 결과로 물러선다.
- **새 asmdef를 만들지 않는다.** 아래 "테스트를 포기하는 결정" 참조.
- **`Component` 모호성**: 클라·서버 파일은 `using GameFramework.World;`를 추가하지 않고 World 타입을
  풀 네임스페이스로 한정한다.
- **`.cs`와 짝 `.meta`를 함께 커밋한다.** 새 파일은 Unity가 만든 `.meta`를 반드시 포함.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로
  스테이지된 것이 의도한 파일뿐인지 확인한다. 세 레포 모두 워킹트리에 **커밋하면 안 되는 로컬 픽스처**가
  떠 있다(클라: `Assets/Art`, `Jua-Regular SDF.asset` / 서버: `DefaultVolumeProfile.asset`,
  `ConfigureRoomComponent.cs`, `FlapWangRuleSystem.cs`).
- **표시 문자열은 한국어.** 기존 UI(`"결과"`, `"매치 종료"`, `"확인"`)와 결을 맞춘다.
- **브랜치 이름:** 세 레포 모두 `feature/match-result-display`.

### 테스트를 포기하는 결정 (사용자 결정 2026-08-20 — 리뷰어는 결함으로 잡지 말 것)

이 슬라이스의 표시 로직(등수 정렬·이름 붙이기·증감 포맷)에 **단위 테스트를 붙이지 않는다.** 클라 앱
코드가 전부 `Assembly-CSharp`라 테스트 어셈블리가 참조할 수 없기 때문이다.

**테스트를 붙이려고 별도 asmdef를 파지 않는다.** 이 로직은 개념적으로 ViewModel이 하는 일 그 자체이고
(아키텍처 문서: *"ViewModel은 순수 C#. 상태/로직"*), 떼어낼 설계상의 이유가 없다. 경계를 만들려면 와이어
타입을 베낀 입력 타입과 매핑 층이 따라붙는데, 그것들의 존재 이유가 오직 어셈블리 경계가 된다 —
테스트가 설계를 끌고 가는 모양이다.

> 참고: `Assets/Scripts/FlappyRaceSlice/Logic/`의 asmdef는 **이 경우와 다르다.** 거기 있는 건 충돌 시
> 속도 교환·추격자 곡선 같은 **결정론 시뮬 커널**이라, 엔진 비의존 순수 C#으로 두는 것이 원래 이
> 프로젝트의 규약이다. 테스트가 붙는 건 그 분리의 결과지 이유가 아니다. 그 선례를 표시 로직에
> 적용하면 안 된다.

**진짜 막고 있는 것은 이 기능이 아니라 구조**이며, 이미 로드맵에 있다 — "인증 트랙 이월 후속" 2번
*"Unity 앱 asmdef 도입 — 두 앱 프로젝트에 asmdef가 없어 앱 코드에 유닛 테스트를 못 붙인다."*
그게 들어오면 이 로직도 자연히 테스트가 붙는다. 그때까지 검증은 **실플레이(Task 4)**로 한다.

### 스펙에서 의도적으로 벗어나는 것

| 스펙 문장 | 이 계획 | 왜 |
|---|---|---|
| "View는 VM의 R3만 구독하는 얇은 바인더" | **ViewModel에 R3가 없다.** 평범한 읽기 전용 프로퍼티 | 결과 화면은 열릴 때 한 번 읽고 끝나는 값만 있다 — 시간에 따라 여러 번 바뀌는 라이브 상태가 없다. `architecture-guidelines.md`가 명시한다: *"바인딩할 라이브 상태가 없는 화면에는 R3가 등장하지 않는 게 정상이다."* |
| D = "결과 화면 + 프로필" | **결과 화면만** | 사용자 결정. 와이어 변경이 3레포에 걸쳐 회귀 위험이 여기 몰려 있고, 프로필은 조회 한 번이라 성격이 다르다 — 섞으면 문제 생겼을 때 원인이 안 갈린다. 프로필은 D2 |
| §9 "결과 화면에 참가자 등수표" | 남의 **점수 변화는 안 보낸다** | 사용자 결정. UI에서만 가리면 와이어엔 남의 MMR이 흐른다 |

---

## Task 1: 와이어 — `MatchEndedToC`에 결과를 싣는다

**레포:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`

**Files:**
- Create: `Protos/MatchPlacementInfo.proto`
- Modify: `Protos/MatchEndedToC.proto`
- Regenerate (스크립트가 씀): `Runtime.Generated/Scripts/Protobuf/*`,
  `Runtime.Generated/Scripts/MessageInitializer.cs`

**Interfaces:**
- Produces: 생성된 C# 타입 `LOP.MatchEndedToC` — 프로퍼티 `Placements`(repeated,
  `RepeatedField<MatchPlacementInfo>`, `.Add(...)`로 추가), `HasRatingChange`(bool),
  `MyMmrBefore`(int), `MyMmrAfter`(int). 그리고 `LOP.MatchPlacementInfo` — `UserId`(string),
  `Placement`(int). Task 2·3이 이 이름들을 그대로 쓴다.

> **왜 별도 파일인가:** `@auto_generate` 주석이 붙은 proto만 MessageId와 `IMessage` 구현을 받는다
> (`EntitySnap.proto`처럼 **원소 타입은 마커 없이 별도 파일**로 두는 것이 이 레포의 확립된 관례다).
> `MatchPlacementInfo`는 와이어 메시지가 아니라 원소 타입이므로 마커를 붙이면 안 된다.

- [ ] **Step 1: 원소 타입 proto 작성**

`Protos/MatchPlacementInfo.proto`:

```proto
syntax = "proto3";

message MatchPlacementInfo
{
	string user_id = 1;
	int32 placement = 2;
}
```

- [ ] **Step 2: `MatchEndedToC.proto`에 필드 추가**

기존 파일 전체를 아래로 교체한다(`@auto_generate` 주석은 반드시 유지 — 지우면 MessageId를 잃는다):

```proto
syntax = "proto3";

import "MatchPlacementInfo.proto";

// @auto_generate
message MatchEndedToC
{
	repeated MatchPlacementInfo placements = 1;
	bool has_rating_change = 2;
	int32 my_mmr_before = 3;
	int32 my_mmr_after = 4;
}
```

- [ ] **Step 3: 재생성 전 MessageIds 스냅샷**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
cp Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
```

- [ ] **Step 4: 재생성**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```

기대: `All proto-related scripts executed successfully.`

- [ ] **Step 5: ID 불변 검증 (필수 게이트)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
diff /tmp/MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs && echo "ID 불변 OK"
```

기대: **차이 없음** + `ID 불변 OK`. 한 줄이라도 다르면 **멈추고 보고**한다 — 배포된 클·서와 wire가
어긋난다. 특히 `MatchEndedToC = 2`가 유지돼야 한다.

또한 `MatchPlacementInfo`가 MessageIds에 **들어가지 않았는지** 확인한다(원소 타입이므로):

```bash
grep -c "MatchPlacementInfo" Runtime.Generated/Scripts/MessageIds.cs
```

기대: `0`

- [ ] **Step 6: 생성된 타입 확인**

```bash
grep -n "public.*Placements\|HasRatingChange\|MyMmrBefore\|MyMmrAfter" Runtime.Generated/Scripts/Protobuf/MatchEndedToC.cs | head
```

기대: 네 멤버가 모두 보인다. 실제 프로퍼티 이름을 **그대로 기록해 보고서에 남긴다** — Task 2·3이 쓴다.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos/MatchPlacementInfo.proto Protos/MatchEndedToC.proto Runtime.Generated/
git commit -m "feat(match): 매치 종료 통보에 등수와 내 점수 변화를 싣는다"
```

`.meta` 파일이 생성됐다면 함께 스테이지한다(Unity 에디터가 안 떠 있으면 아직 없을 수 있고, 그건 정상 —
소비 레포에서 임포트될 때 생성된다).

---

## Task 2: 서버 — 수신자마다 다른 결과를 보낸다

**레포:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`

**Files:**
- Modify: `Assets/Scripts/Room/LOPRoom.cs`

**Interfaces:**
- Consumes: Task 1의 `MatchEndedToC`/`MatchPlacementInfo`. 기존 `ReportMatchResultResponse`
  (`participants` = `ConfirmedParticipantDto[]`, 각각 `userId`/`placement`/`mmrBefore`/`mmrAfter`).
  `GameFramework`의 `ISession.userId`(string).
- Produces: 없음(와이어로만 나간다).

**배경 — 지금 코드의 모양:** `CloseRoomAsync()`가 ① 하트비트 취소 → ② 결과 보고 → ③ 방 상태 Closed →
④ 전 세션에 `MatchEndedToC` 브로드캐스트 → ⑤ 배수 대기 순으로 흐른다. ②의 응답이 ④보다 먼저 손에
들어오는 이 순서가 이 태스크의 전제다. 지금은 응답을 `code` 검사에만 쓰고 버린다.

- [ ] **Step 1: 확정 결과를 브로드캐스트까지 들고 간다**

`CloseRoomAsync()` 안, 보고 블록의 **바깥**(즉 `if (!EnvironmentSettings.active.Standalone)` 보다 앞)에
지역 변수를 선언한다:

```csharp
            //  보고가 성공했을 때만 채워진다. 실패하면 null로 남아 아래 통보가 빈 결과로 나간다.
            ConfirmedParticipantDto[] confirmed = null;
```

그리고 보고 성공 경로에서 채운다. 기존:

```csharp
                        var response = await ReportMatchResultAsync(outcome);

                        //  거절(명단 불일치·매치 없음 등)은 HTTP 자체는 200으로 오고 실패를
                        //  body의 code로 알린다 — HTTP 상태만 보면 거절을 조용히 놓친다.
                        if (response.code != 200)
                        {
                            Debug.LogError($"Match result report rejected by backend. code={response.code}");
                        }
```

을 아래로 바꾼다:

```csharp
                        var response = await ReportMatchResultAsync(outcome);

                        //  거절(명단 불일치·매치 없음 등)은 HTTP 자체는 200으로 오고 실패를
                        //  body의 code로 알린다 — HTTP 상태만 보면 거절을 조용히 놓친다.
                        if (response.code != 200)
                        {
                            Debug.LogError($"Match result report rejected by backend. code={response.code}");
                        }
                        else
                        {
                            confirmed = response.participants;
                        }
```

- [ ] **Step 2: 세션별 메시지를 만드는 헬퍼 추가**

`LOPRoom` 클래스 안, `ReportMatchResultAsync` 바로 아래에 추가한다:

```csharp
        //  수신자마다 다른 메시지를 만든다. 등수는 전원 것을 담지만 점수 변화는 본인 것만 담는다 —
        //  UI에서 가리는 것으로는 부족하고, 남의 실력 점수가 애초에 그 사람 회선으로 나가면 안 된다.
        private static MatchEndedToC BuildMatchEndedMessage(ConfirmedParticipantDto[] confirmed, string userId)
        {
            var message = new MatchEndedToC();

            //  보고가 실패했으면 확정 결과가 없다. 빈 결과로 보내고 화면은 "매치 종료"로 물러선다.
            if (confirmed == null)
            {
                return message;
            }

            foreach (var participant in confirmed)
            {
                message.Placements.Add(new MatchPlacementInfo
                {
                    UserId = participant.userId,
                    Placement = participant.placement,
                });

                if (participant.userId == userId)
                {
                    message.HasRatingChange = true;
                    message.MyMmrBefore = participant.mmrBefore;
                    message.MyMmrAfter = participant.mmrAfter;
                }
            }

            return message;
        }
```

- [ ] **Step 3: 브로드캐스트를 세션별 전송으로 바꾼다**

기존:

```csharp
                    session.Send(new MatchEndedToC());
```

을:

```csharp
                    session.Send(BuildMatchEndedMessage(confirmed, session.userId));
```

로 바꾼다. 감싸고 있는 `try`/`catch`와 주석은 **그대로 둔다** — 한 세션 전송 실패가 나머지를 막으면 안
되는 성질은 변하지 않았다.

- [ ] **Step 4: 컴파일 검증**

에디터가 떠 있으면 `unity` CLI로 붙어 컴파일을 확인한다(`--project-path` 필수). 안 되면 Bee+Roslyn 우회.
기대: **에러 0**. 특히 protobuf 프로퍼티 이름(`Placements`/`HasRatingChange`/`MyMmrBefore`/`MyMmrAfter`)이
Task 1 보고서에 적힌 것과 일치하는지 여기서 드러난다.

- [ ] **Step 5: 손으로 훑는 검증**

단위 테스트를 붙일 수 없으므로(위 "테스트를 포기하는 결정") 아래 네 가지를 읽어 확인하고 보고서에 적는다:

1. `confirmed`가 `null`일 때 `BuildMatchEndedMessage`가 **빈 메시지를 반환**하고 예외를 안 던진다
2. 내 userId가 명단에 없으면 `HasRatingChange`가 `false`로 남는다
3. `Placements`에는 **전원**이 들어간다(본인 여부와 무관)
4. `MyMmrBefore`/`MyMmrAfter`는 **본인 항목에서만** 설정된다

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(match): 매치 종료 통보에 등수와 본인 점수 변화를 실어 보낸다"
```

⚠️ `git status --short`에 `DefaultVolumeProfile.asset`·`ConfigureRoomComponent.cs`·
`FlapWangRuleSystem.cs`가 **unstaged로 남아 있어야 한다.** 이 셋은 로컬 픽스처다.

---

## Task 3: 클라 — 결과를 받아 화면에 띄운다

**레포:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`

**Files:**
- Modify: `Assets/Scripts/Stores/MatchResult.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs`
- Create: `Assets/Scripts/UI/MatchResult/MatchResultViewModel.cs`
- Modify: `Assets/Scripts/UI/MatchResult/MatchResultView.cs`
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`
- Modify: `Assets/UI/MatchResult/MatchResultView.uxml`
- Modify: `Assets/UI/MatchResult/MatchResultView.uss`
- Modify: `Assets/Scripts/WebAPI/ResponseCode.cs`

**Interfaces:**
- Consumes: Task 1의 `MatchEndedToC`(`Placements`/`HasRatingChange`/`MyMmrBefore`/`MyMmrAfter`).
  기존 `IUserDataStore.user.id`(내 userId), `IMatchResultDataStore.result`.
- Produces: 없음(마지막 구현 태스크).

- [ ] **Step 1: 결과 모델 확장**

`Assets/Scripts/Stores/MatchResult.cs` 전체를 교체한다:

```csharp
namespace LOP
{
    /// <summary>직전 매치의 결과. 보고가 실패한 판은 participants가 비어 있다.</summary>
    public class MatchResult
    {
        public string matchId;
        public MatchParticipantResult[] participants;
        public bool hasRatingChange;
        public int myMmrBefore;
        public int myMmrAfter;
    }

    public class MatchParticipantResult
    {
        public string userId;
        public int placement;
    }
}
```

- [ ] **Step 2: 핸들러가 와이어 값을 모델로 옮긴다**

`MatchEndedMessageHandler.OnMatchEnded`를 아래로 바꾼다:

```csharp
        private void OnMatchEnded(MatchEndedToC message)
        {
            var participants = new MatchParticipantResult[message.Placements.Count];
            for (int i = 0; i < message.Placements.Count; i++)
            {
                participants[i] = new MatchParticipantResult
                {
                    userId = message.Placements[i].UserId,
                    placement = message.Placements[i].Placement,
                };
            }

            matchResultDataStore.result = new MatchResult
            {
                matchId = roomDataStore.room?.matchId,
                participants = participants,
                hasRatingChange = message.HasRatingChange,
                myMmrBefore = message.MyMmrBefore,
                myMmrAfter = message.MyMmrAfter,
            };

            runner.EndMatch();
        }
```

- [ ] **Step 3: ViewModel 신설**

`Assets/Scripts/UI/MatchResult/MatchResultViewModel.cs`:

```csharp
using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>결과 화면 등수표의 한 줄.</summary>
    public readonly struct MatchResultRow
    {
        public readonly int Placement;
        public readonly string DisplayName;
        public readonly bool IsMe;

        public MatchResultRow(int placement, string displayName, bool isMe)
        {
            Placement = placement;
            DisplayName = displayName;
            IsMe = isMe;
        }
    }

    /// <summary>
    /// 결과 화면 ViewModel. 스토어에 남은 직전 매치 결과를 표시용 줄 목록과 점수 문자열로 바꾼다.
    /// 화면이 열릴 때 한 번 읽고 끝나는 값이라 R3 스트림을 두지 않는다(라이브로 바뀌는 상태가 없다).
    /// </summary>
    public class MatchResultViewModel
    {
        private const string MyName = "나";

        public IReadOnlyList<MatchResultRow> Rows { get; }
        public bool HasRatingChange { get; }

        /// <summary>"1138 (+138)" 형태. 변화가 없으면 빈 문자열.</summary>
        public string RatingText { get; }

        public MatchResultViewModel(IMatchResultDataStore matchResultDataStore, IUserDataStore userDataStore)
        {
            var result = matchResultDataStore.result;

            Rows = BuildRows(result?.participants, userDataStore.user?.id);

            HasRatingChange = result?.hasRatingChange ?? false;
            RatingText = HasRatingChange
                ? $"{result.myMmrAfter} ({FormatDelta(result.myMmrBefore, result.myMmrAfter)})"
                : string.Empty;
        }

        /// <summary>
        /// 등수 오름차순으로 정렬해 줄을 만든다. 본인은 "나", 나머지는 정렬 순서대로 "플레이어 1·2…".
        /// 닉네임 개념이 아직 없어 userId를 그대로 띄우지 않기 위한 표기다.
        /// </summary>
        private static IReadOnlyList<MatchResultRow> BuildRows(MatchParticipantResult[] participants, string myUserId)
        {
            var rows = new List<MatchResultRow>();

            //  보고가 실패한 판은 등수가 없다. 화면이 빈 목록을 보고 "매치 종료"로 물러선다.
            if (participants == null || participants.Length == 0)
            {
                return rows;
            }

            var sorted = new List<MatchParticipantResult>(participants);

            //  동점끼리의 순서가 실행마다 흔들리지 않게 userId로 갈라 준다(서수 비교 = 바이트 순).
            sorted.Sort((left, right) =>
            {
                int byPlacement = left.placement.CompareTo(right.placement);
                return byPlacement != 0
                    ? byPlacement
                    : string.CompareOrdinal(left.userId, right.userId);
            });

            int otherNumber = 0;
            foreach (var participant in sorted)
            {
                bool isMe = participant.userId == myUserId;
                string displayName = isMe ? MyName : $"플레이어 {++otherNumber}";

                rows.Add(new MatchResultRow(participant.placement, displayName, isMe));
            }

            return rows;
        }

        private static string FormatDelta(int before, int after)
        {
            int delta = after - before;

            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return "±0";
        }
    }
}
```

- [ ] **Step 4: UXML에 등수표와 점수 영역 추가**

`Assets/UI/MatchResult/MatchResultView.uxml` 전체를 교체한다:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="matchresult-root" class="scrim matchresult-root">
        <ui:VisualElement name="matchresult-card" class="card matchresult-card">
            <ui:Label text="결과" class="title matchresult-title" />
            <ui:Label name="matchresult-message" text="매치 종료" class="card-text matchresult-message" />
            <ui:VisualElement name="matchresult-rows" class="matchresult-rows" />
            <ui:VisualElement name="matchresult-rating" class="matchresult-rating">
                <ui:Label text="점수" class="card-text matchresult-rating-label" />
                <ui:Label name="matchresult-rating-value" class="card-text matchresult-rating-value" />
            </ui:VisualElement>
            <ui:Button name="confirm-button" text="확인" class="btn btn--primary matchresult-confirm" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 5: USS에 등수표 스타일 추가**

`Assets/UI/MatchResult/MatchResultView.uss` **끝에 덧붙인다**(기존 규칙은 건드리지 않는다):

```css
.matchresult-rows {
    flex-direction: column;
    margin-top: 8px;
    margin-bottom: 8px;
}

.matchresult-row {
    flex-direction: row;
    justify-content: space-between;
    padding-top: 4px;
    padding-bottom: 4px;
}

.matchresult-row--me {
    -unity-font-style: bold;
}

.matchresult-rating {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: 8px;
}
```

- [ ] **Step 6: View가 ViewModel을 그린다**

`Assets/Scripts/UI/MatchResult/MatchResultView.cs` 전체를 교체한다:

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 매치 결과 화면. ViewModel이 만들어 둔 줄 목록과 점수를 그리고, [확인]으로 닫는다.
    /// 여는 쪽(FrontEndCoordinator)이 SetConfirmCallback으로 닫기 동작을 배선한다.
    /// </summary>
    public class MatchResultView : UIView
    {
        private readonly MatchResultViewModel _viewModel;

        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _confirmButton;
        private System.Action _onConfirm;

        public MatchResultView(MatchResultViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public void SetConfirmCallback(System.Action onConfirm) => _onConfirm = onConfirm;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;

            BuildRows();
            BuildRating();
        }

        public override void OnClose()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            base.OnClose();
        }

        private void BuildRows()
        {
            var container = Root.Q<VisualElement>("matchresult-rows");

            //  보고가 실패한 판은 등수가 없다. 그때는 예전처럼 "매치 종료"만 남긴다.
            if (_viewModel.Rows.Count == 0)
            {
                container.style.display = DisplayStyle.None;
                return;
            }

            Root.Q<Label>("matchresult-message").style.display = DisplayStyle.None;

            foreach (var row in _viewModel.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                if (row.IsMe) line.AddToClassList("matchresult-row--me");

                var placement = new Label($"{row.Placement}등");
                placement.AddToClassList("card-text");

                var name = new Label(row.DisplayName);
                name.AddToClassList("card-text");

                line.Add(placement);
                line.Add(name);
                container.Add(line);
            }
        }

        private void BuildRating()
        {
            var rating = Root.Q<VisualElement>("matchresult-rating");

            if (!_viewModel.HasRatingChange)
            {
                rating.style.display = DisplayStyle.None;
                return;
            }

            Root.Q<Label>("matchresult-rating-value").text = _viewModel.RatingText;
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
```

- [ ] **Step 7: DI 등록**

`Assets/Scripts/Lobby/LobbyLifetimeScope.cs`의 `builder.Register<MatchResultView>(Lifetime.Transient);`
**바로 위**에 한 줄 추가한다:

```csharp
            //  View와 함께 Transient — 결과 화면을 열 때마다 그 시점의 스토어 값을 읽어야 한다.
            builder.Register<MatchResultViewModel>(Lifetime.Transient);
```

> `IMatchResultDataStore`와 `IUserDataStore`는 Root 스코프 싱글턴이라 이 자식 스코프에서 그대로 주입된다.
> 등록 순서는 VContainer가 해결하므로 상관없다.

- [ ] **Step 8: ResponseCode 어휘 맞추기**

`Assets/Scripts/WebAPI/ResponseCode.cs`의 `MATCH_NOT_EXIST = 20000;` 바로 아래에 추가한다:

```csharp
        public const int INVALID_MATCH_RESULT = 20001;
```

> 백엔드(`packages/server-core`)에만 있던 상수다. 클라가 아직 이 코드를 받는 경로는 없지만, 한쪽에만
> 있는 번호를 남겨 두면 다음에 쓰는 사람이 다른 번호를 붙인다.

- [ ] **Step 9: 컴파일 + 기존 테스트 회귀**

`unity` CLI로 컴파일 확인(에러 0) + EditMode 전체 실행. 신규 테스트는 없지만 **기존
`FlappyRaceSlice.Tests.EditMode`가 여전히 통과**해야 한다. 총 건수를 보고서에 적는다.

- [ ] **Step 10: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Stores/MatchResult.cs \
        Assets/Scripts/Game/MessageHandler/MatchEndedMessageHandler.cs \
        Assets/Scripts/UI/MatchResult \
        Assets/Scripts/Lobby/LobbyLifetimeScope.cs \
        Assets/Scripts/WebAPI/ResponseCode.cs \
        Assets/UI/MatchResult
git commit -m "feat(match): 결과 화면에 등수표와 내 점수 변화를 띄운다"
```

⚠️ `.meta` 파일이 빠지지 않았는지 확인한다(새 `.cs`는 짝이 있어야 한다).
`Assets/Art`와 `Jua-Regular SDF.asset`은 **unstaged로 남겨 둔다.**

---

## Task 4: 배포 + 끝‑끝 검증 (사람 손 필요)

**Files:** 없음(운영 작업)

**Interfaces:**
- Consumes: Task 1~3의 커밋 전부.

> **왜 사람이 필요한가:** 에디터 2대로 실제 한 판을 돌려야 하고, 게임서버 이미지를 새로 구워야 한다.
> 단위 테스트를 포기했으므로 **이 태스크가 유일한 실증**이다 — 건너뛰면 검증된 것이 없다.

- [ ] **Step 1: 게임서버 이미지 재빌드 (필수)**

Task 2가 서버 코드를 바꿨다. 옛 이미지면 **빈 메시지가 그대로 나가 화면이 안 바뀐다.**

> **먼저 정할 것 — Shared는 검증 전에 main에 올린다.** 아래 이유로 게임서버 빌드가 Shared main을 보기
> 때문이다. proto3의 필드 추가는 **양방향 호환**이라 안전하다: 옛 서버가 보낸 빈 메시지는 새 클라가
> 빈 결과로 읽고, 새 서버가 보낸 필드는 옛 클라가 무시한다. 그래서 Shared만 먼저 머지해도 라이브가
> 깨지지 않는다. 서버·클라는 검증 후에 머지한다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git push origin feature/match-result-display
gh workflow run gameserver-deploy.yml --ref feature/match-result-display -f environment=local
```

> ⚠️ 이 워크플로는 형제 UPM 레포(`GameFramework`, `LeagueOfPhysical-Shared`,
> `LeagueOfPhysical-MasterData-Server`)를 **각자 기본 브랜치(main)로 `git reset --hard`** 한다.
> Task 1의 Shared 커밋이 피처 브랜치에만 있으면 빌드가 옛 proto로 컴파일돼 `Placements`가 없다고 터진다.
> 백엔드는 이번에 안 바뀌었으므로 배포 불필요.

- [ ] **Step 2: 이미지가 실제로 클러스터에 도달했는지 확인**

```bash
kubectl exec deploy/room-server -- printenv GAME_SERVER_IMAGE
```

기대: 방금 빌드한 짧은 sha. **파드 목록에는 안 보인다** — 게임서버는 room-server가 매치마다 띄우는
파드이고 태그는 ConfigMap 값이다. 옛 태그면 ArgoCD 동기화를 더 기다린다.

미리 당겨 두면 첫 매치가 이미지 받다 지체하지 않는다:

```bash
docker exec lop-control-plane crictl pull docker.io/re5nardo/game-server:<sha>
```

- [ ] **Step 3: 한 판 돌리기**

`local-k8s` 환경으로 클라 에디터 2개 → 매칭 → 한 판 종료.

- [ ] **Step 4: 판정**

| 봐야 할 것 | 기대 |
|---|---|
| 결과 화면 | "매치 종료" 대신 **등수표**가 뜬다. 본인 줄이 "나"로 굵게 |
| 점수 | `1138 (+138)` 형태로 **본인 것만** |
| 상대 화면 | 등수는 같고, 점수는 **그쪽 본인 값**(`927 (-73)`) |
| [확인] | 눌러 닫히고, 로비를 오갔을 때 **다시 뜨지 않는다** |

DB 대조로 화면 값이 진짜인지 확인한다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c \
  'SELECT p."userId", p.placement, p."mmrBefore", p."mmrAfter" FROM "MatchParticipant" p
   WHERE p."matchId" = (SELECT id FROM "Match" ORDER BY "createdAt" DESC LIMIT 1) ORDER BY p.placement;'
```

- [ ] **Step 5: 빈 결과 경로 확인 (권장)**

단위 테스트가 없으므로 이 경로도 눈으로 한 번 밟는다. 로비를 잠시 내리고
(`kubectl scale deploy/lobby-server --replicas=0`) 한 판 끝낸 뒤, **결과 화면이 예전처럼 "매치 종료"로
뜨고 [확인]이 동작하는지** 확인한다. 끝나면 복구: `kubectl scale deploy/lobby-server --replicas=1`.

> ⚠️ GitOps라 replicas를 손으로 줄이면 ArgoCD가 되돌릴 수 있다. 되돌아오면 로비를 못 내린 것이니
> 이 단계는 접고 넘어간다.

- [ ] **Step 6: 머지**

세 레포 전부 `CLAUDE.md`의 "푸시 규약"대로. **한 줄씩 결과를 확인하고 넘어간다.** Unity 두 레포는
리베이스 전에 로컬 픽스처를 `git stash push -u`로 빼고 끝나면 `pop`한다.

---

## 검증 요약 (전체가 끝났다는 기준)

1. `MessageIds.cs` diff 없음 — 기존 wire 계약 불변
2. 클·서 컴파일 에러 0, 기존 EditMode 회귀 0
3. 실플레이에서 등수표와 본인 점수 변화가 뜨고, **상대 화면엔 상대 본인 값이 뜬다**
4. 화면 값이 DB `MatchParticipant`와 일치
5. 빈 결과 경로에서 화면이 "매치 종료"로 물러서고 [확인]이 동작한다
