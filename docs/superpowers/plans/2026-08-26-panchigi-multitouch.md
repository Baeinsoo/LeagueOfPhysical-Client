# 판치기 손바닥 치기(멀티터치) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 손가락 하나가 아니라 손 전체로 친다 — 손가락 수와 벌린 간격이 결과를 바꾼다.

**Architecture:** 접촉점을 배열로 실어 보내고, 서버는 접촉점마다 기존 힘 커널을 돌려 임펄스를 **합산**한다. 힘 커널(`PanchigiStrike`)은 손대지 않는다. 클라는 손가락별 추적을 **순수 C# 수집기**로 빼서 MonoBehaviour를 얇게 둔다.

**Tech Stack:** Unity 6000.3.16f1, Mirror, protobuf(Google.Protobuf), Luban 마스터데이터, VContainer, New Input System

**Spec:** `docs/superpowers/specs/2026-08-26-panchigi-multitouch-strike-design.md`

## Global Constraints

- **접촉점 상한은 컨피그** — `TbPanchigiConfig.contact_max`, 값은 **4**. 코드에 4를 박지 않는다.
- **`contact_max`는 "동시에 눌린 손가락 수"가 아니라 "한 번의 치기가 모으는 접촉점 총 개수"** — 손가락을 떼도 자리가 나지 않는다.
- **클라가 먼저 자르고, 서버는 방어선** — 클라는 먼저 닿은 순서로 상한까지만 접수하고, 서버는 초과분이 오면 치기 전체를 거절한다.
- **판을 못 맞힌 손가락은 자리를 먹지 않는다.**
- **서버 거절은 전부 아니면 전무** — 접촉점 하나만 규칙을 어겨도 치기 전체를 버린다. 클램프하지 않는다.
- **힘 커널 `PanchigiStrike`는 수정 금지** — 접촉점마다 호출해 합산할 뿐이다.
- **레거시 `Input` 클래스 금지** — New Input System(`Touchscreen`/`Mouse`)만 쓴다. `#if UNITY_EDITOR / #elif UNITY_IOS` 분기도 쓰지 않는다.
- **World 타입은 풀 네임스페이스로 한정** — `GameFramework.World.Entity` 등. `using GameFramework.World;`를 추가하지 않는다(`UnityEngine.Component`와 충돌).
- **`git add -A` / `git commit -a` 금지** — 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 확인한다.
- **커밋 메시지는 한국어**, 무엇을 왜 바꿨는지 적는다.

---

### Task 1: 마스터데이터 — `contact_max` 컬럼

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#PanchigiConfig.xlsx`
- Regenerate(커밋 대상): `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`
- Regenerate(커밋 대상): `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`

**Interfaces:**
- Consumes: (없음)
- Produces: `LOP.MasterData.PanchigiConfig.ContactMax` (int, 값 4) — Task 3·4·5·6이 읽는다.

시트는 `A1:N5`이고 헤더가 4줄이다(`##var` / `##type` / `##group` / `##`), 데이터는 5행 하나뿐이다.
A열은 비어 있고 B열부터 값이다. **O열**에 컬럼을 더한다.

- [ ] **Step 1: 지금 값을 찍어 둔다 (되돌릴 때 기준)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb=openpyxl.load_workbook(r'Datas/#PanchigiConfig.xlsx')
ws=wb.active
print('dims:',ws.dimensions)
for r in ws.iter_rows(min_row=1,max_row=5): print([c.value for c in r])
"
```

Expected: `dims: A1:N5`, 마지막 데이터 행이 `[None, 1, 3, 0.05, 0.1, 10, 20, 60, 3, 8, 2, 4, 13, 1]`

- [ ] **Step 2: O열 추가**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
p=r'Datas/#PanchigiConfig.xlsx'
wb=openpyxl.load_workbook(p); ws=wb.active
ws['O1']='contact_max'
ws['O2']='int'
# O3(##group)은 비워 둔다 — 이 테이블은 클·서가 같은 컬럼을 본다
ws['O4']='contact_max'
ws['O5']=4
wb.save(p)
print('saved')
"
```

- [ ] **Step 3: 다시 찍어 확인**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb=openpyxl.load_workbook(r'Datas/#PanchigiConfig.xlsx'); ws=wb.active
print('dims:',ws.dimensions)
for r in ws.iter_rows(min_row=1,max_row=5): print([c.value for c in r])
"
```

Expected: `dims: A1:O5`, 데이터 행 끝이 `..., 13, 1, 4]`, 1행 끝이 `'contact_max'`, 2행 끝이 `'int'`, 3행 끝이 `None`

- [ ] **Step 4: Luban 재생성**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

Expected: 에러 없이 끝난다. 실패하면 멈추고 출력을 그대로 보고한다.

- [ ] **Step 5: 생성 결과에 `ContactMax`가 들어갔는지 확인**

```bash
grep -n "ContactMax" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/PanchigiConfig.cs
grep -n "ContactMax" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/PanchigiConfig.cs
```

Expected: 양쪽 모두 `ContactMax` 필드가 나온다. 한쪽이라도 없으면 멈추고 보고한다.

- [ ] **Step 6: 세 레포 각각 커밋 (바꾼 파일만)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git status --short
git add "table/Datas/#PanchigiConfig.xlsx"
git commit -m "feat(masterdata): 판치기 접촉점 상한(contact_max) 컬럼을 넣는다"
```

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git status --short
git add Runtime.Generated
git commit -m "chore(masterdata): contact_max 반영 재생성"
```

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git status --short
git add Runtime.Generated
git commit -m "chore(masterdata): contact_max 반영 재생성"
```

> `.meta` 파일이 "삭제됨"으로 남아 있으면 `gen.sh`의 `restore_deleted_meta`가 되돌렸어야 한다.
> 그래도 남아 있으면 **커밋하지 말고 보고**한다 — 지우면 씬·프리팹 참조가 끊긴다.

---

### Task 2: 와이어 — 접촉점 배열

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Protos/PanchigiStrikeContact.proto`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Protos/PanchigiStrikeToS.proto`
- Regenerate(커밋 대상): `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/**`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `PanchigiStrikeContact` — `StrikePoint`(ProtoVector3), `DragDelta`(ProtoVector3), `HoldTime`(float)
  - `PanchigiStrikeToS.Contacts` — `Google.Protobuf.Collections.RepeatedField<PanchigiStrikeContact>`
  - Task 3(서버)·Task 5(클라)가 쓴다.

`@auto_generate`가 **없으면** MessageId를 받지 않는 payload 타입이다(`EntitySnap`·`ProtoVector3`가 그렇다).
접촉점은 payload이므로 붙이지 않는다 — 이러면 **기존 MessageId가 하나도 안 밀린다.**

- [ ] **Step 1: 지금 MessageId 표를 떠 둔다 (뒤에서 대조할 기준)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
cp Runtime.Generated/Scripts/MessageIds.cs ../MessageIds.before.cs
grep -c "const ushort" Runtime.Generated/Scripts/MessageIds.cs
```

Expected: 개수가 찍힌다(현재 15). 이 파일을 반드시 남겨 둔다.

- [ ] **Step 2: 접촉점 proto 작성**

`LeagueOfPhysical-Shared/Protos/PanchigiStrikeContact.proto`:

```proto
syntax = "proto3";

import "ProtoVector3.proto";

message PanchigiStrikeContact
{
	ProtoVector3 strike_point = 1;   // 판 위 월드 좌표
	ProtoVector3 drag_delta   = 2;   // 판 평면 변위 (y = 0)
	float        hold_time    = 3;   // 초. 클라가 이미 상한을 적용해 보낸다
}
```

> `@auto_generate`를 **넣지 않는다.** 넣으면 MessageId를 받아 버려서 payload가 아니라 wire 메시지가 된다.

- [ ] **Step 3: `PanchigiStrikeToS.proto`를 통째로 교체**

```proto
syntax = "proto3";

import "PanchigiStrikeContact.proto";

// @auto_generate
message PanchigiStrikeToS
{
	// 신원은 연결에서 도출한다 — 클라가 적어 보내지 않는다.
	// 한 번의 치기가 모은 접촉점들. 손가락이 전부 떨어졌을 때 한 통으로 온다.
	repeated PanchigiStrikeContact contacts = 1;
}
```

- [ ] **Step 4: 생성 실행**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```

Expected: `All proto-related scripts executed successfully.`

- [ ] **Step 5: MessageId가 하나도 안 밀렸는지 대조 (필수)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
diff ../MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs && echo "MessageId 변화 없음 — OK"
```

Expected: **차이 없음.** 한 줄이라도 다르면 **멈추고 보고**한다 — ID가 밀리면 배포본과 wire desync가 난다.
(`PanchigiStrikeContact`가 목록에 새로 생겼다면 Step 2에서 `@auto_generate`를 실수로 넣은 것이다.)

확인이 끝나면 임시 파일을 지운다: `rm ../MessageIds.before.cs`

- [ ] **Step 6: 생성물 확인**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
ls Runtime.Generated/Scripts/Protobuf/PanchigiStrikeContact.cs
grep -n "Contacts" Runtime.Generated/Scripts/Protobuf/PanchigiStrikeToS.cs | head -3
```

Expected: 파일이 있고, `Contacts` 프로퍼티가 `RepeatedField<PanchigiStrikeContact>`로 나온다.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos Runtime.Generated
git commit -m "feat(panchigi): 타격을 접촉점 배열로 싣는다

손가락마다 접촉점이 하나씩 나오므로 점 하나로는 손 전체를 표현할 수 없다.
접촉점은 payload 타입이라 @auto_generate를 붙이지 않는다 — MessageId를
받지 않고, 따라서 기존 ID가 하나도 밀리지 않는다."
```

---

### Task 3: 서버 — 접촉점마다 검증하고 힘을 합산

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiStrikeValidation.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Editor/PanchigiVerification.cs`

**Interfaces:**
- Consumes: `PanchigiStrikeToS.Contacts`(Task 2), `PanchigiConfig.ContactMax`(Task 1)
- Produces: `PanchigiStrikeValidation.Validate(IReadOnlyList<Contact>, Bounds, float, float, int, out string)` → `bool`,
  `PanchigiStrikeValidation.Contact(Vector3 strikePoint, Vector3 dragDelta, float holdTime)`,
  `PanchigiStrikeValidation.ContainsXZ(Bounds, Vector3)` → `bool`,
  `PanchigiStrikeValidation.BoundEpsilon` (const float) — 검증 루틴이 부른다.

검증 규칙을 **순수 static 클래스로 뺀다.** 지금은 핸들러 안에 흩어져 있어 검증 루틴이 부를 수 없다.
뺀 뒤에는 "전부 아니면 전무"라는 규칙 자체를 표로 확인할 수 있다.

- [ ] **Step 1: 검증 규칙을 순수 클래스로 뺀다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiStrikeValidation.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 타격 메시지가 규칙에 맞나. 접촉점 하나만 어긋나도 <b>치기 전체</b>를 버린다 —
    /// 일부만 버리고 나머지를 적용하면 조작된 값이 조용히 섞여 들어간다.
    /// </summary>
    public static class PanchigiStrikeValidation
    {
        //  클라가 상한에 맞춰 자른 값이라도 성분에서 크기를 다시 재면 미세하게 커질 수 있다
        //  (ClampMagnitude는 성분을 다시 계산한다). 정직한 클라가 경계에서 거절당하지 않게 봐준다.
        public const float BoundEpsilon = 0.001f;

        /// <summary>한 접촉점. 와이어 타입을 이 레이어까지 끌고 오지 않으려고 따로 둔다.</summary>
        public readonly struct Contact
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public Contact(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>통과하면 true. 막히면 false와 함께 왜 막혔는지를 <paramref name="reason"/>에 담는다.</summary>
        public static bool Validate(IReadOnlyList<Contact> contacts, Bounds boardBounds,
            float holdTimeMax, float strikePowerMax, int contactMax, out string reason)
        {
            if (contacts == null || contacts.Count == 0)
            {
                reason = "접촉점이 없다";
                return false;
            }
            if (contacts.Count > contactMax)
            {
                reason = $"접촉점이 상한을 넘었다 {contacts.Count} > {contactMax}";
                return false;
            }

            for (int i = 0; i < contacts.Count; i++)
            {
                Contact c = contacts[i];
                if (ContainsXZ(boardBounds, c.StrikePoint) == false)
                {
                    reason = $"[{i}] 판 밖 타격점 {c.StrikePoint}";
                    return false;
                }
                if (c.HoldTime < -BoundEpsilon || c.HoldTime > holdTimeMax + BoundEpsilon)
                {
                    reason = $"[{i}] 누른 시간 범위 밖 {c.HoldTime}";
                    return false;
                }
                if (c.DragDelta.magnitude > strikePowerMax + BoundEpsilon)
                {
                    reason = $"[{i}] 세기 범위 밖 {c.DragDelta.magnitude}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        //  판은 평면이라 높이는 보지 않는다 — 위아래로 얼마나 떨어져 있든 "판 위"다.
        //  가장자리를 정확히 친 값도 반올림으로 밖에 떨어질 수 있어 BoundEpsilon만큼 넉넉히 본다.
        public static bool ContainsXZ(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x - BoundEpsilon && point.x <= bounds.max.x + BoundEpsilon
            && point.z >= bounds.min.z - BoundEpsilon && point.z <= bounds.max.z + BoundEpsilon;
    }
}
```

- [ ] **Step 2: 핸들러가 접촉점 배열을 다루게 고친다**

`PanchigiStrikeMessageHandler.cs`에서:

(a) 파일 맨 위 using에 `using System.Collections.Generic;`을 더한다.

(b) 클래스의 `private const float BoundEpsilon = 0.001f;` 줄과 그 위 주석 3줄을 **지운다**,
그리고 파일 맨 아래의 `private static bool ContainsXZ(Bounds bounds, Vector3 point)` 메서드와
그 위 주석 2줄을 **지운다** — 둘 다 `PanchigiStrikeValidation`으로 옮겼다.

(c) `OnStrike` 안에서 `PanchigiStrikeToS message = received.Message;`부터
`turnSystem.NotifyStruck(userId);`까지를 통째로 이 내용으로 바꾼다:

```csharp
            PanchigiStrikeToS message = received.Message;

            var contacts = new List<PanchigiStrikeValidation.Contact>(message.Contacts.Count);
            foreach (PanchigiStrikeContact wire in message.Contacts)
            {
                contacts.Add(new PanchigiStrikeValidation.Contact(
                    MapperConfig.mapper.Map<Vector3>(wire.StrikePoint),
                    MapperConfig.mapper.Map<Vector3>(wire.DragDelta),
                    wire.HoldTime));
            }

            //  클라가 이미 상한을 걸어 보내지만 믿지 않는다. 클램프가 아니라 거절이다 —
            //  클램프하면 조작된 값이 조용히 게임에 들어오고 로그도 안 남는다.
            if (PanchigiStrikeValidation.Validate(contacts, boardBounds,
                    config.HoldTimeMax, config.StrikePowerMax, config.ContactMax, out string reason) == false)
            {
                Debug.LogWarning($"[Panchigi] 타격 거절 — {reason} — {userId}");
                return;
            }
            if (config.CoverageSamples <= 0)
            {
                Debug.LogWarning($"[Panchigi] TbPanchigiConfig의 CoverageSamples가 {config.CoverageSamples}다 — 타격을 버린다.");
                return;
            }

            //  접촉점마다 같은 커널을 돌려 임펄스를 누적한다 — 손가락 수와 간격이 결과를 바꾸는 건
            //  힘 모델이 달라서가 아니라, 힘이 각자 자리에서 여러 번 들어가기 때문이다.
            for (int i = 0; i < contacts.Count; i++)
            {
                ApplyStrike(contacts[i].StrikePoint, contacts[i].DragDelta, contacts[i].HoldTime,
                    boardBounds, config);
            }
            turnSystem.NotifyStruck(userId);
```

(d) `ApplyStrike`는 **시그니처·본문 그대로 둔다.** 접촉점 하나를 처리하는 함수이고, 위에서 접촉점마다 부른다.
단 안에서 지운 심볼을 쓰는 두 곳을 고친다:
- `float reach = new Vector3(...).magnitude + BoundEpsilon;` → `... + PanchigiStrikeValidation.BoundEpsilon;`
- `if (ContainsXZ(boardBounds, sample) == false)` → `if (PanchigiStrikeValidation.ContainsXZ(boardBounds, sample) == false)`

> **빈 탭은 접촉점마다 그대로 건너뛴다** — `ApplyStrike` 첫머리의
> `if (dragDelta == Vector3.zero && holdTime == 0f) return;` 가드를 지우지 않는다.
> 접촉점 하나가 빈 탭이어도 나머지는 힘을 준다. 전부 빈 탭이어도 `NotifyStruck`은 불리므로
> 차례는 소모된다(슬라이스 4의 판정 유지 — 그러지 않으면 지연 전술이 된다).

- [ ] **Step 3: 검증 루틴에 항목을 더한다**

`Assets/Editor/PanchigiVerification.cs`의 `Run()`에서 `StrikeKernel(sb);` **바로 앞**에
`StrikeValidation(sb);`를 넣고, 아래 메서드를 클래스에 더한다:

```csharp
        private static void StrikeValidation(StringBuilder sb)
        {
            sb.AppendLine("[타격 검증] PanchigiStrikeValidation.Validate");

            var board = new Bounds(Vector3.zero, new Vector3(10f, 0.1f, 10f));
            const float HoldMax = 1f;
            const float PowerMax = 3f;
            const int ContactMax = 4;

            var good = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f), 0.5f);
            string reason;

            CheckBool(sb, "접촉점 1개 정상 → 통과",
                PanchigiStrikeValidation.Validate(new[] { good }, board, HoldMax, PowerMax, ContactMax, out reason), true);

            CheckBool(sb, "상한만큼(4개) → 통과",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, good }, board, HoldMax, PowerMax, ContactMax, out reason), true);

            CheckBool(sb, "빈 배열 → 거절",
                PanchigiStrikeValidation.Validate(new PanchigiStrikeValidation.Contact[0], board, HoldMax, PowerMax, ContactMax, out reason), false);

            CheckBool(sb, "null → 거절",
                PanchigiStrikeValidation.Validate(null, board, HoldMax, PowerMax, ContactMax, out reason), false);

            CheckBool(sb, "상한 초과(5개) → 거절",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, good, good }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            //  하나만 어긋나도 전체가 막힌다 — 이게 "전부 아니면 전무"의 핵심이다
            var farOut = new PanchigiStrikeValidation.Contact(new Vector3(99f, 0f, 0f), Vector3.zero, 0f);
            CheckBool(sb, "3개 정상 + 1개 판 밖 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, farOut }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            var tooLong = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), Vector3.zero, HoldMax + 1f);
            CheckBool(sb, "1개만 누른 시간 초과 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, tooLong }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            var tooStrong = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), new Vector3(PowerMax + 1f, 0f, 0f), 0f);
            CheckBool(sb, "1개만 세기 초과 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, tooStrong }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            //  경계에서 정직한 클라가 막히지 않아야 한다
            var atEdge = new PanchigiStrikeValidation.Contact(new Vector3(5f, 0f, 5f), new Vector3(PowerMax, 0f, 0f), HoldMax);
            CheckBool(sb, "판 모서리 + 상한 정확히 → 통과",
                PanchigiStrikeValidation.Validate(new[] { atEdge }, board, HoldMax, PowerMax, ContactMax, out reason), true);
        }
```

- [ ] **Step 4: 컴파일**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; S=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
"$U" cmd recompile --project-path "$S" --timeout 90
"$U" cmd recompile_status --project-path "$S" --timeout 90
```

Expected: `"status":"completed"`, `"failed":false`, `"errors":[]`

> ⚠️ `status`가 `up_to_date`면 **재컴파일을 안 한 것**이다 — 통과로 치지 말고,
> 콘솔 CS 에러를 **시각과 대조**해 확인한다:
> `"$U" cmd console --project-path "$S" -- --type error --limit 20`
> 에디터가 응답하지 않으면 Bee 응답파일 + Roslyn으로 직접 컴파일한다:
> ```bash
> cd "$S"
> sed -e 's#^-out:.*#-out:"../verify.dll"#' -e 's#^-refout:.*##' \
>   Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp > ../verify.rsp
> "C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Data/NetCoreRuntime/dotnet.exe" \
>   "C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Data/DotNetSdkRoslyn/csc.dll" "@../verify.rsp"
> ```

- [ ] **Step 5: 검증 루틴 실행**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; S=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
"$U" cmd menu --project-path "$S" -- --path "LOP/판치기 검증"
"$U" cmd console --project-path "$S" -- --limit 5
```

Expected: 출력에 `FAIL`이 **0개**. 새로 더한 `[타격 검증]` 9줄이 전부 OK.

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/PanchigiStrikeValidation.cs Assets/Scripts/Game/PanchigiStrikeValidation.cs.meta \
        Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs \
        Assets/Editor/PanchigiVerification.cs
git commit -m "feat(panchigi): 접촉점마다 검증하고 힘을 합산한다

검증 규칙을 순수 클래스로 빼서 검증 루틴이 직접 부를 수 있게 했다.
접촉점 하나만 어긋나도 치기 전체를 버린다 — 일부만 버리면 조작된 값이
조용히 섞인다. 힘 커널은 손대지 않았다: 접촉점마다 같은 커널을 돌려
임펄스를 누적할 뿐이다."
```

> `.meta`는 Unity가 만든 것만 커밋한다. 아직 없으면 에디터가 스캔할 때까지 기다렸다 추가한다.

---

### Task 4: 클라 — 접촉점 수집기 (순수 C#)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiContactCollector.cs`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Editor/PanchigiClientVerification.cs`

**Interfaces:**
- Consumes: (없음 — 순수 C#)
- Produces: `PanchigiContactCollector` — Task 5·6이 쓴다.
  - `PanchigiContactCollector(int contactMax)`
  - `bool Begin(int touchId, Vector3 boardPoint, float now)` — 접수했으면 true
  - `void Update(int touchId, Vector3 boardPoint)`
  - `void End(int touchId, Vector3 boardPoint, float now, float holdTimeMax, float strikePowerMax)`
  - `bool IsComplete { get; }`
  - `IReadOnlyList<Contact> Contacts { get; }` — `Contact { Vector3 StrikePoint; Vector3 DragDelta; float HoldTime; }`
  - `IReadOnlyList<Aim> Pressed { get; }` — `Aim { int TouchId; Vector3 Start; Vector3 Current; float PressTime; }`
  - `void Clear()`

손가락 추적을 MonoBehaviour 밖으로 빼서, 상한·순서 규칙을 Unity 입력 없이 확인할 수 있게 한다.

- [ ] **Step 1: 수집기 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 번의 치기가 모으는 접촉점. 손가락이 전부 떨어지면 완성된다.
    ///
    /// 상한(<c>contactMax</c>)은 <b>동시에 눌린 손가락 수가 아니라 한 번의 치기가 모으는 총 개수</b>다 —
    /// 손가락을 떼도 그 접촉점은 보관되므로 자리가 나지 않고, 앞서 넘쳐서 무시된 손가락이
    /// 승격되지도 않는다. 승격시키면 조준 중에 없던 선이 갑자기 생기고 "먼저 온 순서"가 흔들린다.
    /// </summary>
    public class PanchigiContactCollector
    {
        /// <summary>확정된 접촉점 하나.</summary>
        public readonly struct Contact
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public Contact(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>아직 눌려 있는 손가락 하나 — 조준선을 그리는 데 쓴다.</summary>
        public readonly struct Aim
        {
            public readonly int TouchId;
            public readonly Vector3 Start;
            public readonly Vector3 Current;
            public readonly float PressTime;

            public Aim(int touchId, Vector3 start, Vector3 current, float pressTime)
            {
                TouchId = touchId;
                Start = start;
                Current = current;
                PressTime = pressTime;
            }
        }

        private readonly int contactMax;

        //  List로 두는 건 순서가 곧 규칙이기 때문이다 — 먼저 닿은 순서로 접수한다.
        //  손가락은 많아야 상한(현재 4)이라 선형 탐색으로 충분하다.
        private readonly List<Aim> pressed = new();
        private readonly List<Contact> done = new();

        public PanchigiContactCollector(int contactMax)
        {
            this.contactMax = contactMax;
        }

        public IReadOnlyList<Contact> Contacts => done;
        public IReadOnlyList<Aim> Pressed => pressed;

        /// <summary>손가락이 전부 떨어졌고 모인 접촉점이 있다 = 한 번의 치기가 끝났다.</summary>
        public bool IsComplete => pressed.Count == 0 && done.Count > 0;

        /// <summary>손가락이 판에 닿았다. 상한을 넘었으면 접수하지 않고 false를 준다.</summary>
        public bool Begin(int touchId, Vector3 boardPoint, float now)
        {
            if (IndexOf(touchId) >= 0)
            {
                return false;   // 이미 추적 중인 손가락
            }
            if (done.Count + pressed.Count >= contactMax)
            {
                return false;   // 이번 치기가 모을 수 있는 만큼 다 찼다
            }

            pressed.Add(new Aim(touchId, boardPoint, boardPoint, now));
            return true;
        }

        /// <summary>손가락이 움직였다. 추적 중이 아니면 아무 일도 하지 않는다.</summary>
        public void Update(int touchId, Vector3 boardPoint)
        {
            int i = IndexOf(touchId);
            if (i < 0)
            {
                return;
            }
            Aim a = pressed[i];
            pressed[i] = new Aim(a.TouchId, a.Start, boardPoint, a.PressTime);
        }

        /// <summary>손가락이 떨어졌다. 그 손가락의 결과를 확정해 보관한다.</summary>
        public void End(int touchId, Vector3 boardPoint, float now, float holdTimeMax, float strikePowerMax)
        {
            int i = IndexOf(touchId);
            if (i < 0)
            {
                return;
            }
            Aim a = pressed[i];
            pressed.RemoveAt(i);

            //  누른 시간에 상한이 없으면 오래 누를수록 힘이 무한히 커진다(원본의 문제).
            float holdTime = Mathf.Min(now - a.PressTime, holdTimeMax);

            Vector3 drag = boardPoint - a.Start;
            drag.y = 0f;
            //  세기 상한도 여기서 자른다 — 서버는 넘으면 클램프가 아니라 거절한다.
            drag = Vector3.ClampMagnitude(drag, strikePowerMax);

            done.Add(new Contact(boardPoint, drag, holdTime));
        }

        public void Clear()
        {
            pressed.Clear();
            done.Clear();
        }

        private int IndexOf(int touchId)
        {
            for (int i = 0; i < pressed.Count; i++)
            {
                if (pressed[i].TouchId == touchId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
```

- [ ] **Step 2: 클라 검증 진입점 작성**

수집기는 **클라 레포**에 있고 기존 검증 루틴은 **서버 레포**에 있어 서로 못 본다.
클라 레포에 같은 방식의 진입점을 새로 만든다.

`LeagueOfPhysical-Client/Assets/Editor/PanchigiClientVerification.cs`:

```csharp
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 클라 접촉점 수집기를 표로 찍는다. 상한·순서 규칙은 눈으로 틀린 걸 알기 어려워 직접 대조한다.
    /// unity CLI의 menu 명령으로 헤드리스 재실행이 된다.
    /// </summary>
    public static class PanchigiClientVerification
    {
        [MenuItem("LOP/판치기 클라 검증")]
        public static void Run()
        {
            var sb = new StringBuilder();
            Collector(sb);
            Debug.Log(sb.ToString());
        }

        private static void CheckInt(StringBuilder sb, string label, int actual, int expected)
        {
            sb.AppendLine($"  {label}: 실제={actual} 기대={expected} → {(actual == expected ? "OK" : "FAIL")}");
        }

        private static void CheckBool(StringBuilder sb, string label, bool actual, bool expected)
        {
            sb.AppendLine($"  {label}: 실제={actual} 기대={expected} → {(actual == expected ? "OK" : "FAIL")}");
        }

        private static void CheckFloat(StringBuilder sb, string label, float actual, float expected, float eps = 1e-4f)
        {
            sb.AppendLine($"  {label}: 실제={actual:F4} 기대={expected:F4} → {(Mathf.Abs(actual - expected) <= eps ? "OK" : "FAIL")}");
        }

        private static void Collector(StringBuilder sb)
        {
            sb.AppendLine("[접촉점 수집기] PanchigiContactCollector");

            const float HoldMax = 1f;
            const float PowerMax = 3f;

            //  상한까지 접수하고, 넘긴 손가락은 거절한다
            var c = new PanchigiContactCollector(4);
            CheckBool(sb, "1번째 손가락 접수", c.Begin(1, Vector3.zero, 0f), true);
            CheckBool(sb, "2번째 접수", c.Begin(2, Vector3.zero, 0f), true);
            CheckBool(sb, "3번째 접수", c.Begin(3, Vector3.zero, 0f), true);
            CheckBool(sb, "4번째 접수", c.Begin(4, Vector3.zero, 0f), true);
            CheckBool(sb, "5번째는 거절(상한 4)", c.Begin(5, Vector3.zero, 0f), false);
            CheckInt(sb, "눌린 손가락 수", c.Pressed.Count, 4);

            //  떼도 자리는 나지 않는다 — 접촉점이 보관되기 때문
            c.End(1, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckInt(sb, "하나 떼면 눌린 수 3", c.Pressed.Count, 3);
            CheckInt(sb, "확정된 접촉점 1", c.Contacts.Count, 1);
            CheckBool(sb, "뗐어도 5번째는 여전히 거절", c.Begin(5, Vector3.zero, 0.5f), false);

            //  전부 떨어져야 완성이다
            CheckBool(sb, "아직 눌린 손가락 있음 → 미완성", c.IsComplete, false);
            c.End(2, Vector3.zero, 0.5f, HoldMax, PowerMax);
            c.End(3, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckBool(sb, "1개 남으면 아직 미완성", c.IsComplete, false);
            c.End(4, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckBool(sb, "전부 떨어지면 완성", c.IsComplete, true);
            CheckInt(sb, "모인 접촉점 4개", c.Contacts.Count, 4);

            //  Clear 뒤에는 다시 상한만큼 받는다
            c.Clear();
            CheckBool(sb, "Clear 뒤 미완성", c.IsComplete, false);
            CheckInt(sb, "Clear 뒤 접촉점 0", c.Contacts.Count, 0);
            CheckBool(sb, "Clear 뒤 다시 접수됨", c.Begin(9, Vector3.zero, 1f), true);

            //  누른 시간과 세기가 상한에서 잘린다
            var d = new PanchigiContactCollector(4);
            d.Begin(1, Vector3.zero, 0f);
            d.End(1, new Vector3(99f, 0f, 0f), 10f, HoldMax, PowerMax);
            CheckFloat(sb, "누른 시간이 상한으로 잘림", d.Contacts[0].HoldTime, HoldMax);
            CheckFloat(sb, "세기가 상한으로 잘림", d.Contacts[0].DragDelta.magnitude, PowerMax);

            //  드래그는 판 평면 위 변위다 — 높이는 빠진다
            var e = new PanchigiContactCollector(4);
            e.Begin(1, new Vector3(0f, 0f, 0f), 0f);
            e.End(1, new Vector3(1f, 5f, 0f), 0.2f, HoldMax, PowerMax);
            CheckFloat(sb, "드래그 y는 0", e.Contacts[0].DragDelta.y, 0f);
            CheckFloat(sb, "드래그 크기는 수평 성분만", e.Contacts[0].DragDelta.magnitude, 1f);

            //  같은 손가락을 두 번 접수하지 않는다
            var f = new PanchigiContactCollector(4);
            f.Begin(7, Vector3.zero, 0f);
            CheckBool(sb, "같은 touchId 재접수 거절", f.Begin(7, Vector3.zero, 0f), false);

            //  추적 중이 아닌 손가락의 Update/End는 아무 일도 하지 않는다
            var g = new PanchigiContactCollector(4);
            g.Update(42, Vector3.one);
            g.End(42, Vector3.one, 1f, HoldMax, PowerMax);
            CheckInt(sb, "모르는 손가락은 무시 — 접촉점 0", g.Contacts.Count, 0);
            CheckInt(sb, "모르는 손가락은 무시 — 눌린 수 0", g.Pressed.Count, 0);
        }
    }
}
```

- [ ] **Step 3: 컴파일**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; C=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
"$U" cmd recompile --project-path "$C" --timeout 90
"$U" cmd recompile_status --project-path "$C" --timeout 90
```

Expected: `"status":"completed"`, `"failed":false`, `"errors":[]` (Task 3 Step 4의 주의사항 동일 적용)

- [ ] **Step 4: 검증 루틴 실행**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; C=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
"$U" cmd menu --project-path "$C" -- --path "LOP/판치기 클라 검증"
"$U" cmd console --project-path "$C" -- --limit 5
```

Expected: `FAIL`이 **0개**.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/PanchigiContactCollector.cs Assets/Scripts/Game/PanchigiContactCollector.cs.meta \
        Assets/Editor/PanchigiClientVerification.cs Assets/Editor/PanchigiClientVerification.cs.meta
git commit -m "feat(panchigi): 손가락별 접촉점 수집기를 만든다

상한과 순서 규칙을 Unity 입력 없이 확인할 수 있게 순수 C#으로 뺐다.
상한은 동시에 눌린 손가락 수가 아니라 한 번의 치기가 모으는 총 개수다 —
떼도 자리가 나지 않고 무시된 손가락이 승격되지도 않는다."
```

> `Assets/Editor/` 폴더가 클라 레포에 처음 생기면 `Assets/Editor.meta`도 `git status`에 뜬다.
> 그때는 그 파일도 함께 add 한다.

---

### Task 5: 클라 — 입력을 수집기에 연결

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs`

**Interfaces:**
- Consumes: `PanchigiContactCollector`(Task 4), `PanchigiStrikeToS.Contacts`·`PanchigiStrikeContact`(Task 2), `PanchigiConfig.ContactMax`(Task 1)
- Produces: (없음 — 최종 소비자)

`Pointer.current` 하나를 **손가락별 순회**로 바꾼다. 터치스크린이 없으면 마우스를 **손가락 하나**로 취급한다 —
분기 없이 같은 경로를 탄다(원본의 `#if UNITY_EDITOR` 분기가 데스크톱 빌드에서 입력을 통째로 죽였다).

- [ ] **Step 1: using 추가**

파일 맨 위에 이 줄을 더한다(`using UnityEngine.InputSystem;`은 이미 있다):

```csharp
using UnityEngine.InputSystem.Controls;
```

- [ ] **Step 2: 필드를 수집기로 바꾼다**

아래 4개 필드를 지운다:

```csharp
        private bool aiming;
        private float pressTime;
        private Vector3 pressPoint;
        private Vector3 currentPoint;
```

대신 이 필드를 넣는다:

```csharp
        //  마우스는 손가락이 하나뿐이다. 터치 id와 겹치지 않는 값을 준다.
        private const int MouseTouchId = -1;

        private PanchigiContactCollector collector;
```

- [ ] **Step 3: `Update`를 손가락별 순회로 바꾼다**

`Update()` 전체를 이 내용으로 바꾸고, 이어지는 보조 메서드들을 클래스에 더한다:

```csharp
        private void Update()
        {
            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                return;
            }

            if (IsMyTurn() == false)
            {
                //  조준하는 중에 차례가 넘어갔다 — 모은 것도 조준선도 남으면 안 된다.
                if (collector != null && (collector.Pressed.Count > 0 || collector.Contacts.Count > 0))
                {
                    collector.Clear();
                    HideAllAimLines();
                }
                return;
            }

            collector ??= new PanchigiContactCollector(config.ContactMax);

            if (Touchscreen.current != null)
            {
                PollTouches(config);
            }
            else if (Mouse.current != null)
            {
                PollMouse(config);
            }

            DrawAimLines();

            //  손가락이 전부 떨어졌다 = 한 번의 치기가 끝났다.
            if (collector.IsComplete)
            {
                SendStrike();
                collector.Clear();
                HideAllAimLines();
            }
        }

        private bool IsMyTurn()
            => stateStore.Phase.CurrentValue == AimingPhase
            && stateStore.CurrentEntityId.CurrentValue == playerContext.entityId;

        private void PollTouches(LOP.MasterData.PanchigiConfig config)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                int touchId = touch.touchId.ReadValue();
                Vector2 screen = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    //  판을 못 맞힌 손가락은 접수하지 않고 자리도 먹지 않는다.
                    if (TryBoardPoint(screen, out Vector3 begin))
                    {
                        collector.Begin(touchId, begin, Time.time);
                    }
                    continue;
                }

                //  뗀 그 프레임엔 isPressed가 아직 true일 수 있다 — release를 먼저 본다.
                //  순서를 바꾸면 누르고 뗀 게 같은 프레임인 탭이 씹힌다.
                if (touch.press.wasReleasedThisFrame)
                {
                    EndTouch(touchId, screen, config);
                }
                else if (touch.press.isPressed)
                {
                    if (TryBoardPoint(screen, out Vector3 moved))
                    {
                        collector.Update(touchId, moved);
                    }
                }
            }
        }

        private void PollMouse(LOP.MasterData.PanchigiConfig config)
        {
            Vector2 screen = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (TryBoardPoint(screen, out Vector3 begin))
                {
                    collector.Begin(MouseTouchId, begin, Time.time);
                }
                return;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndTouch(MouseTouchId, screen, config);
            }
            else if (Mouse.current.leftButton.isPressed)
            {
                if (TryBoardPoint(screen, out Vector3 moved))
                {
                    collector.Update(MouseTouchId, moved);
                }
            }
        }

        //  뗀 자리가 판 밖이면 마지막으로 판 위에 있던 자리를 쓴다 — 손가락이 판을 벗어나며
        //  떨어졌다고 그 손가락의 힘이 사라지면 안 된다.
        private void EndTouch(int touchId, Vector2 screen, LOP.MasterData.PanchigiConfig config)
        {
            Vector3 endPoint;
            if (TryBoardPoint(screen, out Vector3 hit))
            {
                endPoint = hit;
            }
            else if (TryGetPressedCurrent(touchId, out Vector3 last))
            {
                endPoint = last;
            }
            else
            {
                return;   // 추적 중이 아닌 손가락이다
            }

            collector.End(touchId, endPoint, Time.time, config.HoldTimeMax, config.StrikePowerMax);
        }

        private bool TryGetPressedCurrent(int touchId, out Vector3 point)
        {
            foreach (PanchigiContactCollector.Aim aim in collector.Pressed)
            {
                if (aim.TouchId == touchId)
                {
                    point = aim.Current;
                    return true;
                }
            }
            point = default;
            return false;
        }

        private void SendStrike()
        {
            if (playerContext.session == null)
            {
                return;
            }

            var message = new PanchigiStrikeToS();
            foreach (PanchigiContactCollector.Contact contact in collector.Contacts)
            {
                message.Contacts.Add(new PanchigiStrikeContact
                {
                    StrikePoint = MapperConfig.mapper.Map<ProtoVector3>(contact.StrikePoint),
                    DragDelta = MapperConfig.mapper.Map<ProtoVector3>(contact.DragDelta),
                    HoldTime = contact.HoldTime,
                });
            }

            playerContext.session.Send(message);
        }
```

- [ ] **Step 4: 옛 메서드를 지우고 조준선 메서드를 임시로 맞춘다**

`BeginAim`, `UpdateAim`, `EndAim` 세 메서드를 **통째로 지운다.**
`TryBoardPoint`는 **그대로 둔다** — 위에서 계속 쓴다.

`DrawAimLine` / `SetAimLineVisible` 두 메서드를 아래 두 개로 바꾼다(Task 6이 다시 교체한다):

```csharp
        //  Task 6에서 손가락마다 그리도록 바뀐다. 지금은 첫 손가락만 그린다.
        private void DrawAimLines()
        {
            if (aimLine == null)
            {
                return;
            }
            if (collector.Pressed.Count == 0)
            {
                aimLine.enabled = false;
                return;
            }
            PanchigiContactCollector.Aim first = collector.Pressed[0];
            //  두 점은 판 윗면 바로 위에 찍힌다 — 그대로 그리면 깊이 테스트에 절반이 잘려 나간다.
            //  띄우는 건 그림뿐이고, 서버로 보내는 점은 건드리지 않는다.
            var lift = new Vector3(0f, 0.01f, 0f);
            aimLine.enabled = true;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, first.Start + lift);
            aimLine.SetPosition(1, first.Current + lift);
        }

        private void HideAllAimLines()
        {
            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
        }
```

`Awake()`의 `SetAimLineVisible(false);`를 `HideAllAimLines();`로 바꾼다.

`OnDisable()`을 이 내용으로 바꾼다:

```csharp
        private void OnDisable()
        {
            //  조준 중에 꺼지면 조준선이 화면에 남고, 다음 켜질 때 절반쯤 조준된 상태로
            //  시작한다 — 꺼질 때 확실히 리셋한다.
            collector?.Clear();
            HideAllAimLines();
        }
```

- [ ] **Step 5: 컴파일**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; C=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
"$U" cmd recompile --project-path "$C" --timeout 90
"$U" cmd recompile_status --project-path "$C" --timeout 90
```

Expected: `"status":"completed"`, `"failed":false`, `"errors":[]`

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/PanchigiStrikeInput.cs
git commit -m "feat(panchigi): 손가락마다 따로 추적해 한 번에 보낸다

Pointer.current 하나로는 손가락이 몇 개든 하나만 잡혔다. 이제 손가락별로
추적하고, 전부 떨어지면 접촉점들을 한 통으로 보낸다. 터치스크린이 없으면
마우스를 손가락 하나로 취급한다 — 플랫폼 분기를 두지 않는다."
```

---

### Task 6: 클라 — 조준선을 손가락마다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs`

**Interfaces:**
- Consumes: `PanchigiContactCollector.Pressed`(Task 4), `PanchigiConfig.ContactMax`(Task 1)
- Produces: (없음 — 최종 소비자)

씬에 배선된 `aimLine` 하나를 **틀(template)** 로 삼아 `contact_max`개를 복제해 두고 재사용한다.
치기마다 만들고 지우면 GC가 돈다.

- [ ] **Step 1: 풀 필드 추가**

`private PanchigiContactCollector collector;` 아래에 넣는다:

```csharp
        //  씬에 배선된 aimLine을 틀로 삼아 복제한다. 손가락마다 별도 LineRenderer를 써야 한다 —
        //  하나를 세그먼트로 나눠 쓰면 선들이 이어져 보인다.
        private LineRenderer[] aimLines;
```

- [ ] **Step 2: 풀을 만드는 메서드 추가**

```csharp
        private void EnsureAimLines(int count)
        {
            if (aimLine == null || aimLines != null)
            {
                return;
            }

            aimLines = new LineRenderer[count];
            aimLines[0] = aimLine;
            for (int i = 1; i < count; i++)
            {
                LineRenderer clone = Instantiate(aimLine, aimLine.transform.parent);
                clone.name = $"{aimLine.name}_{i}";
                aimLines[i] = clone;
            }
            HideAllAimLines();
        }
```

- [ ] **Step 3: `DrawAimLines` / `HideAllAimLines`를 교체**

Task 5에서 넣은 임시 두 메서드를 이 내용으로 바꾼다:

```csharp
        private void DrawAimLines()
        {
            if (aimLines == null)
            {
                return;
            }

            //  두 점은 판 윗면 바로 위에 찍힌다 — 그대로 그리면 깊이 테스트에 절반이 잘려 나간다.
            //  띄우는 건 그림뿐이고, 서버로 보내는 점은 건드리지 않는다.
            var lift = new Vector3(0f, 0.01f, 0f);

            int drawn = 0;
            foreach (PanchigiContactCollector.Aim aim in collector.Pressed)
            {
                if (drawn >= aimLines.Length)
                {
                    break;
                }
                LineRenderer line = aimLines[drawn++];
                line.enabled = true;
                line.positionCount = 2;
                line.SetPosition(0, aim.Start + lift);
                line.SetPosition(1, aim.Current + lift);
            }

            //  떨어진 손가락의 선은 즉시 숨긴다 — 남아 있으면 아직 조준 중인 것으로 읽힌다.
            for (int i = drawn; i < aimLines.Length; i++)
            {
                aimLines[i].enabled = false;
            }
        }

        private void HideAllAimLines()
        {
            if (aimLines == null)
            {
                return;
            }
            for (int i = 0; i < aimLines.Length; i++)
            {
                if (aimLines[i] != null)
                {
                    aimLines[i].enabled = false;
                }
            }
        }
```

- [ ] **Step 4: 풀을 만드는 시점을 잇고 `Awake`를 고친다**

`Update()`의 `collector ??= new PanchigiContactCollector(config.ContactMax);` **바로 아래**에 넣는다:

```csharp
            EnsureAimLines(config.ContactMax);
```

`Awake()`를 이 내용으로 바꾼다 — 풀이 아직 없어서 `HideAllAimLines()`가 아무 일도 안 하므로,
씬에 배선된 선 하나만 먼저 끈다:

```csharp
        private void Awake()
        {
            BoardLayerMask = LayerMask.GetMask("Default");
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
        }
```

- [ ] **Step 5: 컴파일**

```bash
U="$HOME/AppData/Local/Unity/bin/unity"; C=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
"$U" cmd recompile --project-path "$C" --timeout 90
"$U" cmd recompile_status --project-path "$C" --timeout 90
```

Expected: `"status":"completed"`, `"failed":false`, `"errors":[]`

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/PanchigiStrikeInput.cs
git commit -m "feat(panchigi): 조준선을 손가락마다 그린다

어느 손가락이 어디를 얼마나 끌고 있는지가 각각 보여야 한다. 씬에 배선된
선 하나를 틀로 삼아 상한만큼 복제해 재사용한다 — 치기마다 만들고 지우면
GC가 돈다. 상한을 넘어 무시된 손가락은 선도 그리지 않는다: 화면이 곧
'이 손가락은 안 센다'는 안내다."
```

---

### Task 7: 푸시·배포·검증

**Files:** (코드 변경 없음)

**Interfaces:**
- Consumes: Task 1~6의 커밋 전부
- Produces: (없음)

> ⚠️ **이 태스크는 바깥으로 나가는 작업이다.** 실행 전에 사용자 승인을 받는다.

- [ ] **Step 1: 6개 레포를 규약대로 푸시**

레포마다 **한 줄씩 결과를 확인하며** 아래 순서를 밟는다. `&&`로 이어 붙이지 않는다.
Unity 레포(클라·서버)는 리베이스 전에 로컬 픽스처를 `git stash push -u -m ...`로 빼두고 끝나면 `pop`한다.

순서: `infrastructure` → `MasterData-Client` → `MasterData-Server` → `LOP-Shared` → `LOP-Server` → `LOP-Client`

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature-branch>
git push origin main
```

**`git push --force` / `--force-with-lease` 금지.** 거절되면 다시 `fetch` → 리베이스 → 재시도.

- [ ] **Step 2: 게임서버 배포**

```bash
gh workflow run gameserver-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Server --ref main
gh run list --workflow gameserver-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Server --limit 1
gh run watch <run-id> --repo Baeinsoo/LeagueOfPhysical-Server --exit-status
```

- [ ] **Step 3: 클러스터가 새 이미지를 쓰는지 확인**

워크플로 성공만으로는 부족하다 — 옛 버전이 계속 응답하는 일이 실제로 있었다.

```bash
kubectl get cm -n default -o jsonpath='{range .items[*]}{.data.GAME_SERVER_IMAGE}{"\n"}{end}' | grep game-server
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rev-parse --short main
```

Expected: 두 값이 **일치**. 안 맞으면 ArgoCD를 새로고침한다:

```bash
kubectl patch application backend -n argocd --type merge \
  -p '{"metadata":{"annotations":{"argocd.argoproj.io/refresh":"hard"}}}'
```

- [ ] **Step 4: PC 회귀 — 마우스 한 손가락이 그대로 되는지**

에디터 2개(메인 + MPPM 클론 `Library/VP/mppm61234b75`)를 띄워 판치기로 매칭하고, **마우스로** 판을 끌어 친다.

Expected:
- 내 차례에만 조준선이 뜬다
- 치면 동전이 움직인다
- 차례가 상대에게 넘어간다
- 서버 로그에 `[Panchigi] 타격 거절` 이 **없다**

> 멀티터치는 PC에서 검증할 수 없다 — 에디터도 마우스도 손가락이 하나뿐이다.

- [ ] **Step 5: 실기기 — 손가락 수와 간격이 결과를 바꾸는지**

```bash
gh workflow run client-app-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Client --ref main
```

> ⚠️ `-development`를 빼면 평문 http가 막혀 빌드는 성공하고 실행이 전부 실패한다.

기기에서 확인:
- 손가락 **1개**로 치기 → 조준선 1줄, 동전이 조금 움직인다
- 손가락 **3개**를 모아 치기 → 조준선 3줄, 같은 자리 동전이 **더 많이** 움직인다
- 손가락 **3개**를 벌려 치기 → 조준선 3줄, **넓은 범위**의 동전이 움직인다
- 손가락 **5개** → 조준선이 **4줄만** 그려진다(5번째는 안 센다)
- 한 손가락을 먼저 떼도 **아직 안 보내진다**. 전부 떼야 한 번에 나간다

---

## Self-Review

**Spec 커버리지**

| spec 절 | 태스크 |
|---|---|
| §2.1 한 번의 치기 = 전부 떨어질 때까지 | Task 4 (`IsComplete`) · Task 5 (`SendStrike`) |
| §2.2 접촉점마다 힘 합산 | Task 3 (Step 2 루프) |
| §2.3 마우스는 손가락 하나 | Task 5 (`PollMouse`, `MouseTouchId`) |
| §3 와이어 | Task 2 |
| §4.1 `contact_max` | Task 1 |
| §4.2 전부 아니면 전무 | Task 3 (`Validate`) |
| §4.3 적용·빈 탭 | Task 3 (Step 2 주석) |
| §5 손가락 추적 | Task 4 · Task 5 |
| §5.1 조준 UI 손가락마다 | Task 6 |
| §5.2 상한·먼저 닿은 순서·자리 안 남 | Task 4 (`Begin`) · Task 4 Step 2 검증 |
| §5.3 차례가 아니면 | Task 5 (`IsMyTurn` 분기) |
| §6 검증 | Task 3 Step 3 · Task 4 Step 2 · Task 7 Step 4~5 |

**빠진 것 없음.** §7 범위 밖 항목(손 모양 모델링·동전 아트·전용 맵)은 의도적으로 태스크가 없다.

**타입 일관성**: `PanchigiContactCollector.Contact`(클라)와 `PanchigiStrikeValidation.Contact`(서버)는
**다른 타입**이다 — 레포가 달라 공유할 수 없고, 각자 자기 레이어의 값만 담는다. 와이어는
`PanchigiStrikeContact`(proto) 하나로 잇는다. 이름이 겹치지만 네임스페이스와 레포가 달라 충돌하지 않는다.

**메서드 이름 일관성**: Task 5가 `DrawAimLines`/`HideAllAimLines`를 도입하고 Task 6이 같은 이름으로
교체한다 — 이름이 바뀌지 않으므로 `Update`/`OnDisable`의 호출부를 두 번 고치지 않는다.
