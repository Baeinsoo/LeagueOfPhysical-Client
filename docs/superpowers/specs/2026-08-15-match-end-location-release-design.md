# 매치 종료 시 유저 위치 정리 — 설계

> 트랙: 유저 위치(UserLocation) 전반 재정비 **1순위**. 로드맵 `docs/ROADMAP.md`의
> "다음에 할 것 (우선순위) 1. 매치 종료 시 위치 정리" + 파킹 표의 "매치 종료 시 유저 위치 백엔드
> 정리(Slice D 후속)"와 같은 건이다.
>
> 레포: `lop-backend`(room-server) · `LeagueOfPhysical-Server` · `LeagueOfPhysical-Client`.

## 1. 증상

매치가 끝나고 로비로 돌아오면 **방금 끝난 게임으로 도로 끌려간다.** 결과 창이 뜬 직후 Room 씬이
다시 로드되면서 결과 창이 부서진다.

사용자가 실제로 겪는 유일한 위치 버그이고, Slice D(매치 결과 화면) 때 임시 스캐폴드로 우회한 뒤
원복해 둔 자리다.

## 2. 지금 코드가 하는 일

```
게임서버  LOPRoom.OnGameStateChanged(GameOver)
   ① MatchEndedToC → 모든 클라          (먼저 보낸다)
   ② WebAPI.UpdateRoomStatus(Closed).Forget()   ← 결과를 안 기다린다

백엔드   room.service.updateRoomStatus(Closed)
   ① await deleteRoomRunnerById()        ← k8s 파드 삭제. 느리다
   ② match.playerList 전원 위치 → None
   ③ room.save(status = Closed)          ← "끝났다"가 DB에 박히는 건 여기, 맨 끝

클라     MatchEndedToC → 결과 저장 → 로비 씬
   로비 진입과 동시에 둘이 병렬로 돈다
     · FrontEndCoordinator  → 결과 창 (확인 버튼을 눌러야 닫힘)
     · MatchStateMachine    → CheckMatch → GetUserLocation
         아직 GameRoom이면 → InGameRoom → 재접속 → 결과 창 파괴
```

## 3. 원인 — 자가치유는 이미 있다. "끝났다는 사실"이 늦게 박힐 뿐이다

로비의 위치 **조회** 경로는 이미 스스로 고친다:

```
lobby-server  getOrCreateUserLocationById
   → healIfStale → isStale?
        위치가 GameRoom인데 그 방이 없거나 Closed/Error  →  None으로 비움
        (clearLocationIfUnchanged = 조건부 쓰기라 남이 쓴 값을 덮지 않는다)
```

그러니 위치가 `GameRoom`으로 남아 재접속이 나려면 **조회 시점에 DB의 방이 아직 `Closed`가 아니어야**
한다. 그리고 위 §2가 정확히 그 상태를 만든다 — `room.save(status = Closed)`가 **느린 k8s 파드
삭제 뒤에** 있어서, 그 사이 방은 DB에서 여전히 `GameInProgress`다.

> **결론: 고칠 것은 "누가 위치를 지우나"가 아니라 "룸이 끝났다는 사실이 언제 DB에 박히나"다.**
> 유저 위치는 룸 상태의 파생이고, 읽기 경로가 이미 그렇게 다루고 있다.

### 함께 드러난 두 번째 구멍 — 크래시하면 아무도 사실을 박지 않는다

`checkAndCleanupRoomRunners`(2초 주기 스윕)는 **파드만 지우고 룸 상태를 바꾸지 않는다.** 게임서버가
`Closed`를 못 보내고 죽으면 DB엔 `GameInProgress`로 남고 → 자가치유가 안 돌고 → **그 사람들은
영원히 `GameRoom`**이다. 게다가 스윕은 *파드 목록*을 순회하므로, 파드가 이미 사라진 룸은 아예 보이지도
않는다.

## 4. 설계

### 원칙

**진실원본은 룸 상태다. 유저 위치는 그로부터 파생되며, 읽기 경로가 자가치유한다.**
그래서 모든 변경은 "종료 사실을 *빨리, 반드시* DB에 박는다"로 수렴한다. 위치 일괄 정리는
*빠른 길*일 뿐 안전망이 아니다.

### 변경 1 — 백엔드(room-server): 사실을 먼저 박고, 파드 삭제를 요청 경로에서 뺀다

`apps/room-server/src/services/room.service.ts` `updateRoomStatus`:

| | 무엇 | 왜 |
|---|---|---|
| a | 이미 `Closed`/`Error`면 상태 전이 없이 현재 룸을 반환 | 재진입 가드. 위치 일괄 정리가 두 번 돌면 *그 사이 새로 매칭에 들어간* 사람의 위치를 `None`으로 덮는다 |
| b | **`room.save(status)`를 가장 먼저** | 응답이 나가는 시점에 "끝났다"가 이미 DB에 있다 → 이후 어떤 조회든 자가치유된다 |
| c | 그 다음 playerList 위치 일괄 `None` | 빠른 길(자가치유를 기다리지 않게). **실패해도 b가 받치므로 예외는 삼키고 로그만** |
| d | `deleteRoomRunnerById` 호출을 **뺀다** | 2초 스윕이 이미 `Closed`/`Error` 룸의 파드를 지운다. 느린 k8s 호출이 요청 경로에서 사라진다 |

`Error`도 `Closed`와 같은 처리다 — 룸이 터졌으면 플레이어도 풀어야 한다.

`deleteRoomById`(명시적 삭제 엔드포인트)의 `deleteRoomRunnerById` 호출은 **그대로 둔다** — 거긴
요청자가 파드 삭제를 기다리는 게 맞는 자리다.

### 변경 2 — 게임서버: 통보 순서를 뒤집는다 (인과 확정)

`Assets/Scripts/Room/LOPRoom.cs` `OnGameStateChanged(GameOver)`:

```
await UpdateRoomStatus(Closed)      // 타임아웃 3초
foreach session → MatchEndedToC
```

- 이벤트 핸들러라 `await`가 안 되므로 `async UniTaskVoid` 헬퍼로 분리한다(`Awake`와 같은 방식).
- **실패·타임아웃이어도 반드시 통보한다.** 클라가 룸에 갇히는 쪽이 더 나쁘고, 그 경우는 스윕(변경 3)과
  자가치유가 받는다.
- 기존 주석 *"룸을 닫으면 클라 연결이 끊겨 못 받는다 — 상태 갱신보다 반드시 먼저 보낸다"* 의 전제(파드
  삭제가 그 호출 안에 있음)가 **변경 1-d로 사라진다** → 주석을 새 이유로 갱신한다.
- `RunnerBase.gameState` 세터가 같은 값 재대입을 걸러내므로 `GameOver` 전이는 한 번만 발화한다
  (`LateUpdate`가 매 프레임 `EndMatch()`를 불러도 통보 폭주는 없다).

**타임아웃 3초의 근거**: 클라는 이 시간만큼 결과 화면을 늦게 본다. 백엔드가 정상이면 한 자릿수 ms고,
비정상이면 어차피 자가치유 경로로 넘어가므로 오래 기다릴 값이 없다.

### 변경 3 — 크래시 경로: 스윕이 사실을 박게 한다

`checkAndCleanupRoomRunners`를 **두 관심사로 가른다.** 지금은 파드 목록만 돌기 때문에, 파드가
이미 사라진 크래시 룸은 아예 보이지 않는다.

**A. 사실 박기 (신규)** — 룸 목록에서 *아직 종료 상태가 아닌데* 하트비트가 만료된 룸을 찾아
`status = Error`로 저장한다. 이게 박히면 자가치유가 알아서 사람들을 푼다.

**B. 파드 GC (기존 유지)** — 지금처럼 **파드/서비스 목록**을 돌며 대응 룸이 종료 대상이면 삭제한다.

> ⚠️ **A와 B의 순회 기준이 다른 게 핵심이다.** B까지 룸 목록으로 바꾸면, DB에 쌓인 과거 `Closed`
> 룸 전부에 대해 **2초마다 파드 삭제를 호출**하게 된다(룸은 DB에서 지워지지 않는다). 파드 목록을
> 도는 한 실재하는 파드 수만큼만 돈다. A는 종료 상태로 *전이시키는* 일이라 룸당 한 번만 일어나므로
> 룸 목록을 돌아도 반복 부작용이 없다.

- **위치 일괄 정리는 여기 넣지 않는다.** 스윕은 2초마다 반복되므로, 이미 종료된 룸에 대해 매번 돌면
  *그 사이 새 매칭에 들어간* 사람의 위치를 덮는다. 전이 1회성을 지키고 나머지는 자가치유에 맡긴다.

이 변경은 로드맵 2순위 "위치 TTL"이 막으려던 사유의 상당 부분을 함께 닫는다 — 위치의 *근거*(방)가
죽으면 위치도 죽는다.

### 변경 4 — 클라: 못 들어가는 방에 60초 매달리지 않기

`Assets/Scripts/Room/RoomConnector.cs` `TryToEnterRoomById`는 실패 시 **60회 × 1초** 재시도한다.
변경 2의 강행 경로(백엔드 호출 실패)에서 위치가 잠깐 `GameRoom`으로 남으면 로비가 60초 묶인다.

- 응답의 `room.status`가 `Closed`/`Error`면 **확정 거절 → 즉시 `false`**.
- 그 밖의 거절은 지금처럼 재시도한다. ⚠️ **`ROOM_NOT_JOINABLE` 자체를 확정 거절로 보면 안 된다** —
  파드가 부팅 중일 때(`RunnerCreated`/`Initializing`)도 같은 코드가 오고, 그 60초 여유는 여전히
  필요하다. 갈라내는 건 코드가 아니라 **응답에 실린 `room.status`** 다(`RoomJoinableResponse.room`에
  이미 있다).

## 5. 바꾸지 않는 것 (범위 밖 — 의도)

- `locationDetail` 타입 강화(JSON → 계약) · 서버 → 클라 **push** 경로 — 계약 변경이라 별건.
- **위치 자체의 TTL** — 변경 3이 방-근거 경로를 닫지만, 저장소 레벨 TTL은 별도 항목(로드맵 2순위).
- 클라 해석 일원화(`CheckMatch`/`InMatchmaking`/`MatchLoadingViewModel` 3곳의 `switch`) — 로드맵
  3순위. 위치 해석 *규칙*이 바뀔 때 함께 하는 게 값이 크다.
- **Standalone 경로**(`EnvironmentSettings.local`, standalone=1) — 플레이 검증은 kind 파드
  (`local-k8s`, standalone=0) 구성으로 한다. 로드맵의 *"`if (!Standalone)` 가드로 로컬에선 스킵"*
  서술은 07-30 kind 전환 이전 기준이라 **stale**이다. 이번에 ROADMAP도 정정한다.
- 매치 생성 경로의 원자성, "매칭 실패" 알림 — 같은 트랙의 다른 항목.

## 6. 검증

### 자동

| 대상 | 무엇 |
|---|---|
| `room.service.updateRoomStatus` | ① 상태 저장이 위치 정리보다 **먼저** 일어난다 ② 이미 `Closed`면 위치를 다시 안 건드린다(재진입 가드) ③ 위치 정리가 던져도 상태 저장은 남는다 ④ 파드 삭제를 부르지 않는다 |
| `checkAndCleanupRoomRunners` | ① 하트비트 만료 룸을 `Error`로 전이 저장 ② 이미 종료 상태인 룸은 **다시 저장하지 않는다**(1회성) ③ 파드가 없어도 룸 목록(A)으로 잡힌다 ④ 파드 삭제(B)는 여전히 **실재하는 파드 수**만큼만 호출된다 |
| 클라 `RoomConnector` | `room.status`가 `Closed`/`Error`면 재시도 없이 즉시 실패 |

⚠️ `apps/*/tsconfig.json`이 `__tests__`를 exclude하므로 **`pnpm build` 통과는 테스트 타입 안전을
뜻하지 않는다.** 공유 타입을 건드렸으면 전체 테스트를 돌린다.

### 인게임 (kind 파드 구성)

1. 매치 진입 → 5분 경과(또는 종료 조건) → **결과 창이 뜨고 유지된다. 재접속하지 않는다.**
   — 로드맵의 "인게임 검증 ⑥"을 못 돌린 자리다.
2. 결과 창 확인 → 로비 → 새 매칭 요청이 정상 동작한다(위치가 `None`이라 `Idle`에서 출발).
3. 게임서버 파드를 강제 종료(크래시 모사) → **60초 안에** 그 사람이 로비에서 풀려난다.

## 7. 산업 표준 매핑

| 우리 결정 | 대응하는 표준 |
|---|---|
| 게임서버가 "끝났다"를 백엔드에 확정시킨 뒤 클라에 통보 | 세션 서비스가 매치 종료를 소유하고, 클라 복귀는 그 뒤에 오는 일반형(Agones `Shutdown()` → allocation 해제 → 클라 반환) |
| 파드 삭제를 요청 경로에서 빼고 상태 전이 + 스윕(리컨실러)에 맡김 | k8s 컨트롤러의 **선언적 리컨실 루프** — 요청은 *원하는 상태*만 기록하고, 실제 정리는 별도 루프가 수렴시킨다 |
| 위치 = 룸 상태의 파생 + 읽기 경로 자가치유 | presence/세션 상태의 **lease + lazy expiration**. Redis의 TTL 만료가 조회 시점에 확정되는 것과 같은 모양 |
| 종료 *전이* 시 1회만 부작용 | 이벤트소싱의 상태 전이 훅 — 상태가 이미 종단이면 재발화하지 않는다 |

## 8. 리스크

| 리스크 | 대응 |
|---|---|
| 파드 삭제가 요청 경로에서 빠져 룸 파드가 최대 2초 더 산다 | hostPort 풀(10개)을 그만큼 더 오래 점유. 2초는 매치 간격에 비해 무시할 수준이고, 스윕이 이미 그 일을 하고 있었다 |
| 게임서버가 백엔드 응답을 최대 3초 기다린다 | 그 사이 게임은 이미 끝난 상태(`GameOver`)라 시뮬 영향 없음. 실패해도 통보는 강행 |
| 스윕 A가 룸 목록을 돈다 | `findAll()`은 이미 매 2초 부르고 있다(추가 조회 없음). 쓰기는 종료 상태로 *전이할 때만* 이라 룸당 한 번뿐 |
| 위치 일괄 정리 실패를 삼킨다 | 자가치유가 안전망. 삼키되 **반드시 로그**를 남겨 조용한 실패로 만들지 않는다 |
