# 인증 cutover 2b — 내부 전용 라우트 차단

**날짜**: 2026-08-10
**범위**: lop-backend(3서비스+디렉터), infrastructure(k8s), GameFramework, LeagueOfPhysical-Server
**선행**: 1b(유저 JWT 강제), 1c(방 접속 인증 + 내부 API 키), 2a(세션 신원을 연결 기준으로)

---

## 1. 문제

인증이 걸린 라우트는 네 개뿐이다 — `PUT /lobby/join/:id`, `POST /matchmaking`,
`DELETE /matchmaking/:ticketId`(1b), `POST /auth/introspect`(1c). **나머지는 전부 무인증**이고,
셋 다 인그레스로 외부에 노출돼 있다. 라이브 프로브로 확인된 것:

```
PUT  /room/room/heartbeat/probe   → 200   (성공 — 죽은 방을 살려둘 수 있음)
PUT  /room/room/status            → 400   (본문 검증에만 걸림 — 인증은 통과)
PUT  /lobby/user/location         → 500   (인증은 통과)
GET  /lobby/user/all              → 200
```

특히 쓰기 여섯 개가 문제다: 남의 위치 변경(`PUT /user/location`), 방 상태·하트비트 조작,
**아무나 방 생성**(`POST /room`), **아무나 방 삭제**(`DELETE /room/:id`).

OWASP API Security Top 10 기준 **#5 Broken Function Level Authorization**(무인증 관리성
엔드포인트)과 **#1 BOLA**(남의 `userId`로 상태 변경)에 정확히 해당한다.

---

## 2. 설계 원칙 — 동작이 다른가, 권한만 다른가

핵심 난점은 **같은 라우트를 클라와 내부 서비스가 같이 부르는데 필요한 권한이 다르다**는 것이다.
클라는 자기 것만 읽으면 되지만, 매치메이킹은 매칭 상대를 골라야 하므로 **모든 유저**를 읽어야 한다.

이건 *경로* 문제가 아니라 *인가* 문제다. 표준 해법은 경로를 나누는 것이 아니라 **주체(caller)의
권한으로 판단**하는 것이다:

- **M2M 표준**: 서비스는 유저와 *같은* 엔드포인트를 부르고, 토큰 스코프가 범위를 정한다
  (OAuth 2.0 client_credentials + scope claims). Auth0·Cognito·Stytch 문서가 공통으로
  "별도 내부 엔드포인트 대신 단일 엔드포인트 + 스코프"를 권고한다.
- **Google BeyondProd**: 신뢰를 *네트워크 위치*가 아니라 *서비스 신원*에서 판단한다.
  "내부 경로니까 안전"이라는 전제 자체를 걷어낸다.

**경로 분리는 권한이 아니라 노출을 다루는 도구다.** GitLab internal API처럼 *애초에 다른 API*이거나,
엣지가 아예 라우팅하지 않게 할 때 쓴다.

그래서 라우트를 두 부류로 가른다:

| 부류 | 판단 | 처리 |
|---|---|---|
| **A. 같은 동작, 권한만 넓음** | 유저도 (자기 것은) 정당하게 부른다 | 경로 하나 + 주체별 인가 |
| **B. 유저가 부를 일이 없는 동작** | 어떤 유저도 부를 이유가 없다 | `/internal/*` + 키만 |

B는 "권한이 넓다"가 아니라 **동작 자체가 서비스 전용**이다. 클라가 안 부르니 경로를 옮겨도
중복이 생기지 않고, 인그레스에서 한 번에 막을 수 있는 표면이 된다.

---

## 3. 라우트 분류 (확정)

### A. 주체별 인가 — 경로 유지

| 라우트 | 유저 토큰 | 내부 키 |
|---|---|---|
| `GET /user/:id` | 본인만 | 전체 |
| `GET /user/:userId/location` | 본인만 | 전체 |
| `GET /user/:userId/stats` | 본인만 | 전체 |
| `GET /match/:id` | **playerList에 있을 때만** | 전체 |

`GET /match/:id`는 파라미터가 matchId라 `requireSelf`가 안 먹는다. `Match.playerList`가 유저 id
배열(`match.interface.ts`)이므로 **조회 후 참가자 여부**로 판단한다 — 미들웨어가 아니라
`MatchService` 안에서.

### B. `/internal/*` + 키

| 기존 | 변경 후 | 부르는 쪽 |
|---|---|---|
| `PUT /user/location` | `PUT /internal/user/location` | 매치메이킹, 디렉터, 룸 |
| `GET /user/findAll` | `GET /internal/user/findAll` | 매치메이킹, 룸 |
| `POST /auth/introspect` | `POST /internal/auth/introspect` | 게임서버 |
| `GET /matchmaking-ticket/:id` | `GET /internal/matchmaking-ticket/:id` | 로비 |
| `GET /room/:id` | `GET /internal/room/:id` | 로비, 게임서버 |
| `PUT /room/status` | `PUT /internal/room/status` | 게임서버 |
| `PUT /room/heartbeat/:id` | `PUT /internal/room/heartbeat/:id` | 게임서버 |
| `POST /room` | `POST /internal/room` | 디렉터 |
| `DELETE /room/:id` | `DELETE /internal/room/:id` | (현재 없음 — 보존) |

introspect도 옮긴다. 서비스 전용인데 혼자 밖에 남으면 인그레스 차단 규칙에서 빠져 구멍이 된다.
1c의 `introspectRateLimit`은 경로만 바뀌고 그대로 유지한다.

`DELETE /room/:id`는 호출자가 없지만 지우지 않고 잠근다 — 방 정리는 룸 서버 자신의 책임이고
언젠가 필요해진다.

### C. 로그인만 — `GET /room/:id/joinable`

파라미터가 roomId라 소유권을 볼 수 없다. `authMiddleware`만 붙인다.

**클라는 이미 이 호출에 토큰을 싣고 있다** — `CheckRoomJoinable`이 `authorized` HttpClient를
쓴다(`anonymous`를 쓰는 건 `/auth/anonymous`·`/auth/login` 둘뿐). 따라서 클라 변경 0.

public으로 두지 않는 이유: ① 읽기처럼 생겼지만 **쓴다** — heartbeat이 끊긴 방을 만나면
`status = Error`로 저장한다(`room.service.ts:77-79`), ② 응답 `Room`에 게임서버 `ip`·`port`가
들어 있다. 접속 자체는 1c가 막으므로 주소를 안다고 뚫리진 않지만, 무인증으로 뿌릴 이유가 없다.

참가자 검사(room → matchId → match.playerList)는 하지 않는다 — 룸 서버가 매치메이킹까지
왕복해야 하는데 접속은 이미 1c가 막고 있어 값을 못 한다.

### D. 삭제 — 호출자 없음

`GET /user/all`, `GET /user/username/:username`, `GET /room/all`. 다섯 저장소 전부 검색해
호출부가 없음을 확인했다. 라우트·컨트롤러·서비스 메서드까지 지운다.

---

## 4. 인가 미들웨어

**신원 확인(누구냐)과 인가(해도 되냐)를 분리**한다 — OAuth 리소스 서버의 토큰 → 클레임 → 정책과
같은 모양.

```
authenticatePrincipal
  ├ x-internal-api-key 있음 → 검증 → { kind: 'service' }
  └ 없음                    → Bearer 검증 → { kind: 'user', userId }

requireSelfOrService(param)   서비스면 통과 / 유저면 본인일 때만      [A그룹]
requireSelf(param)            유저 본인만                            [기존]
internalApiKeyMiddleware      서비스만                               [B그룹, 1c 것 그대로]
authMiddleware                로그인만                               [joinable]
```

주체 타입:

```ts
type Principal =
    | { kind: 'service' }
    | { kind: 'user'; userId: string };
```

`req.principal`에 담고, **`req.userId`는 유저 주체일 때 기존대로 채운다** — 컨트롤러가
`req.userId!`로 쓰고 있어서(예: `cancelMatchmaking`) 깨지 않기 위해서다.

### 규칙 셋 (구현 시 반드시 지킬 것)

1. **키 헤더가 있는데 틀리면 401로 거부한다. 유저 토큰으로 내려가지 않는다.** 자격증명을 제시했는데
   조용히 강등하면 공격자가 키를 떠보면서도 정상 응답을 받고, 설정 오류도 숨겨진다.
2. **키 헤더가 없으면 `INTERNAL_API_KEY` 환경변수를 보지 않는다.** 1c 미들웨어는 env가 없으면
   500을 내는데, A그룹에 그대로 쓰면 **env 하나 빠졌을 때 클라의 모든 조회가 500**이 된다.
   유저 경로는 키가 필요 없으므로 키 분기 안에서만 검사한다.
3. **둘 다 있으면 키가 이긴다.** 먼저 보므로. 실제로 그럴 호출자는 없지만 동작을 정의해 둔다.

응답 코드는 기존과 맞춘다 — 자격증명 없음 401, 유저인데 남의 것 403, 키 틀림 401.

위치는 `packages/server-core/src/middlewares/`, 내보내기는 `@lop/server-core/auth`.

### 알려진 한계

**키가 하나뿐이라 "어느 서비스인가"를 구분하지 못한다.** 주체는 `service` 하나로만 표현되고,
키가 새면 모든 내부 동작이 열린다. 서비스별 키 분리·순환(rotation)·감사 추적은 후속.

---

## 5. 부르는 쪽

### 키가 필요한 프로세스

| 프로세스 | 보냄 | 검증 |
|---|---|---|
| lobby-server | ✅ | ✅ |
| matchmaking-server | ✅ | ✅ |
| **matchmaking-director** (별도 배포) | ✅ | — |
| room-server | ✅ | ✅ |
| 게임서버 파드 | ✅ | — (1c에서 주입됨) |

**디렉터를 빠뜨리지 말 것.** `director.ts`가 `RoomService`(→ `POST /room`)와
`UserLocationService`(→ `PUT /user/location`)를 쓴다. 키가 없으면 매칭이 통째로 멈춘다.

현재 `internal-api-secret`이 붙은 건 lobby-server 하나뿐이다. 나머지 셋에 `envFrom secretRef`를
붙이고, 세 진입점(`matchmaking main.ts`, `director.ts`, `room main.ts`)의 `validateEnv`에
`INTERNAL_API_KEY: str()`를 추가해 **없으면 부팅 때 죽게** 한다. 런타임에 401로 조용히 새는 것보다
낫다. 로컬 `.env*` 파일에도 같은 키를 넣는다.

### 백엔드 — 공용 http 클라이언트

지금은 `*.service.ts` 6개 파일에서 `axios.get/put/post`를 직접 부른다(14곳).
`@lop/server-core/http`에 **키 헤더를 자동으로 붙이는 axios 인스턴스**를 두고 전부 그것을 쓴다.

키는 **모듈 로드 시점이 아니라 요청 시점에** `process.env`에서 읽는다 — 테스트에서 env를 나중에
세팅해도 동작하도록.

각 앱의 `httpServices/httpService.ts` 베이스 클래스(host/port만 보유, 3중 중복)는 이번에 손대지
않는다 — 범위 밖.

### 게임서버 — ApiKeyHandler

`BearerTokenHandler : DelegatingHandler`라는 선례가 이미 있다. **같은 모양으로
`ApiKeyHandler(inner, headerName, keyProvider)`** 를 GameFramework에 만들고 `WebAPI`의
`HttpClient` 조립에 끼운다:

```csharp
new HttpClient(new ApiKeyHandler(new UnityWebRequestHandler(), "X-Internal-Api-Key", keyProvider))
```

그러면 `Introspect`에 손으로 박아둔 헤더가 사라지고 모든 호출에 자동으로 실린다. 키는 provider
델리게이트로 받아 테스트 가능하게 한다(런타임 구현은
`() => Environment.GetEnvironmentVariable("INTERNAL_API_KEY")`).

호출 URL 4곳(`/room/heartbeat/{id}`, `/room/status`, `/room/{id}`, `/auth/introspect`)을
`/internal/...`로 바꾼다. `GET /match/{id}`는 A그룹이라 경로 그대로 — 키만 실리면 된다.

---

## 6. 인그레스 차단

내부 호출은 전부 클러스터 DNS(`http://lobby-server-service`)로 가고 **인그레스를 거치지 않는다**
(게임서버 `EnvironmentSettings.*.asset`, 백엔드 `*_SERVER_HOST` 확인). 따라서 인그레스에서
`/internal`을 막아도 깨질 내부 호출이 0이고, **키가 새도 인터넷에서는 쓸 수 없게** 된다.

방법은 경로 정규식에 부정 전방탐색을 넣는 것:

```yaml
- path: /lobby(/|$)(?!internal(/|$))(.*)
```

매칭이 안 되면 default backend가 404를 낸다. snippet 어노테이션(ingress-nginx 1.9+에서 기본
비활성)을 쓰지 않아도 되는 게 장점이다. 세 규칙 모두 같은 방식으로 바꾼다.

전방탐색 `(?!...)`은 **캡처하지 않으므로** 기존 `rewrite-target: /$2`가 그대로 동작한다
(그룹 1 = `(/|$)`, 그룹 2 = `(.*)`). 안쪽에 `(/|$)`를 한 번 더 둔 이유는 `internal`로 *시작만*
하는 다른 경로(`/lobby/internalize` 같은)까지 막지 않기 위해서다.

**위험**: 정규식을 틀리면 해당 서비스 트래픽이 통째로 404가 된다. 배포 직후 정상 경로 확인이 필수.

---

## 7. 배포

라우트를 옮기는 순간 옛 경로는 404다. 정석은 expand-then-contract(새 경로 추가 → 호출부 전환 →
옛 경로 제거, 배포 3회)이지만, **실 유저가 없고 클러스터가 하나라 flag-day(한 번에)** 로 간다.
순서는 지킨다:

1. 백엔드 3서비스 + 디렉터 이미지 동시 배포 (라우트·호출부·키가 한 커밋에)
2. 게임서버 이미지 태그 갱신 → **`kubectl rollout restart deployment/room-server`**
   (1c에서 확인 — ConfigMap 이미지 태그만 바꾸면 재시작이 안 걸려 옛 이미지가 계속 뜬다)
3. 인그레스 적용 후 정상 경로 즉시 확인

**이미 돌고 있는 방은 버린다** — 옛 게임서버 이미지가 옛 경로로 heartbeat을 보내 404가 된다.

### 라이브 검증

| 확인 | 기대 |
|---|---|
| 밖에서 `PUT /room/internal/room/status` | 404 (인그레스가 막음) |
| 밖에서 `PUT /lobby/user/location` | 404 (경로 자체가 사라짐) |
| 밖에서 `GET /lobby/user/all` | 404 (삭제됨) |
| 밖에서 `GET /lobby/user/<남의id>` (내 토큰) | 403 |
| 밖에서 `GET /lobby/user/<내id>` (내 토큰) | 200 |
| 밖에서 `GET /lobby/user/<내id>` (토큰 없음) | 401 |
| 클라 로그인 → 매칭 → 방 입장 | 정상 |
| 룸 서버 로그 | heartbeat 404 없음 |

---

## 8. 테스트

| 대상 | 방식 |
|---|---|
| `authenticatePrincipal` 4갈래 (키 정상/키 오류/토큰만/무자격증명) | server-core jest 단위 |
| `requireSelfOrService` (서비스 통과 / 본인 통과 / 남 403) | server-core jest 단위 |
| env 미설정 + 키 헤더 없음 → 500 아님 | server-core jest 단위 (규칙 2 회귀) |
| `GET /match/:id` 참가자 검사 | matchmaking-server 단위 |
| 라우트 배선 | lobby-server 기존 integration test 패턴 |
| `ApiKeyHandler` | GameFramework EditMode (`BearerTokenHandlerTests.cs` 옆) |

**한계 (정직하게)**: 유니티 앱 코드(`WebAPI.cs` 조립, 경로 문자열)는 여전히 asmdef가 없어
단위 테스트가 불가능하다 — 2a와 같은 한계다. 이 부분은 리뷰 + 라이브 검증으로만 확인한다.
asmdef 도입은 후속에 남아 있다.

---

## 9. 후속 (이번에 하지 않음)

- **서비스별 키 분리 + 순환** — 지금은 키 하나라 호출자 구분·감사 추적이 없다
- **커밋된 `.env`에서 서명키 제거 + `.dockerignore`** (1c 후속에서 이월)
- **유니티 앱 asmdef 도입** — 앱 코드가 테스트를 가질 수 있게 (2a 후속에서 이월)
- **`httpService.ts` 3중 중복 정리**
- 토큰 폐기(revocation)
- `characterId` 소유권 검증 (1c 후속)

---

## 10. 산업 표준 매핑

| 우리 | 대응 |
|---|---|
| 주체별 인가 (같은 경로, 서비스=전체 / 유저=본인) | OAuth 2.0 client_credentials + scope claims (Auth0/Cognito M2M) |
| 신원 확인 → 주체 → 인가 분리 | OAuth 리소스 서버 (토큰 → 클레임 → 정책) |
| 내부 API 키 | Steam publisher Web API key |
| `/internal/*` + 엣지 차단 | API 게이트웨이 패턴 (내부 서비스는 엣지가 라우팅하지 않음) |
| 신뢰를 경로가 아니라 신원에서 | Google BeyondProd |
| flag-day vs expand-contract | parallel change (Fowler) — 알고서 flag-day를 택함 |

**우리가 아직 안 하는 것**: mTLS·서비스 메시(사다리 2단계), SPIFFE 워크로드 신원(3단계).
지금은 1단계(공유 시크릿)이며, 라우트 코드를 그대로 두고 인프라만 바꾸면 올라갈 수 있는 모양으로
남긴다.

**출처**: [Auth0 M2M](https://auth0.com/features/machine-to-machine) ·
[Cognito M2M 스코프별 권한](https://repost.aws/articles/ARIf-MWgo8Sw2Bgk9Do0HObA/how-to-authenticate-and-authorize-applications-with-different-permissions-using-amazon-cognito-m2m) ·
[BeyondProd](https://docs.cloud.google.com/docs/security/beyondprod) ·
[API 게이트웨이 패턴](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/direct-client-to-microservice-communication-versus-the-api-gateway-pattern) ·
[GitLab internal API](https://docs.gitlab.com/development/internal_api/)
