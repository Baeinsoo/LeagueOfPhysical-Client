# 인증 cutover 1b — 서버 강제 + 인프라

> 슬라이스 1의 두 번째 조각. 결정 기록: `2026-08-06-auth-cutover-decisions.md`(특히 §1·§4·§5·§8)
> 선행: `2026-08-06-auth-cutover-1a-client-token-refresh-design.md`(1a — 클라 토큰 갱신, 완료)

## 0. 왜 지금인가

익명 로그인 슬라이스로 **토큰 발급·저장·전송**이 되고, 1a로 **갱신**이 붙었다. 남은 건 하나다 —
**아무도 토큰을 검사하지 않는다.**

그래서 지금은 남의 `userId`만 알면 그 사람으로 로비에 들어가고, 매칭을 걸고, 대기표를 취소할 수 있다.
같은 매치에 있는 사람들의 userId는 게임 중 서로 다 보인다.

1a가 먼저였던 이유가 여기서 갚아진다 — 검사를 켜도 **1시간 넘는 세션이 안 깨진다.**

## 1. 범위

**닫는다** — 플레이어 전용 *변경* 동작 3개

| 동작 | 검사 |
|---|---|
| `PUT /lobby/join/:id` | `authMiddleware` + `requireSelf('id')` |
| `POST /matchmaking` | `authMiddleware`. 본문의 `userId`를 **제거**하고 `req.userId`를 쓴다 |
| `DELETE /matchmaking/:ticketId` | `authMiddleware` + 대기표 주인 == `req.userId`, 아니면 403 |

**같이 한다** — 문지기 자체를 지키는 것

- `/auth/*` 레이트리밋 + `trust proxy` (§5)
- `AUTH_JWT_SECRET`을 이미지에서 k8s Secret으로 (§6)
- `matchmaking-server`에 `AUTH_JWT_SECRET` 요구 추가 (§4)
- 고아 라우트 `PUT /lobby/leave/:id` 삭제 (§7)

**안 닫는다**

- **조회 계열**(`GET /user/:id`, `/stats`, `/location`, `/match/:id`) — 내부 서비스가 같은 경로를
  쓴다. 닫으려면 서비스 간 토큰이 따라오고 범위가 2배 이상이 된다. 피해는 남의 닉네임·전적 열람 수준.
- **내부 전용 변경 라우트**(`PUT /user/location`, `PUT /room/status` 등) — ingress가 공개하고 있어
  인터넷에서 호출 가능하지만, 같은 이유로 범위 밖. **별도 슬라이스**(선택지: 서비스 간 토큰 /
  ingress 경로 축소 / 내부 전용 포트).
- **방 접속(Mirror)** — 1c.

### 왜 이 3개인가

세 동작은 **클라 전용 경로라 내부 호출자가 없다.** 그래서 서비스 간 인증 없이 깨끗하게 닫힌다.
그리고 사칭 피해가 가장 큰 것들이다 — 남을 이동시키기, 남을 매칭 걸기, 남의 대기표 없애기.

## 2. 이미 깔려 있는 것

이 슬라이스는 **대부분 배선**이다.

| 부품 | 상태 |
|---|---|
| `authMiddleware`, `requireSelf` | `server-core`에 구현·테스트 완비. **아무 라우트도 안 쓴다** |
| `validateEnv(extra)` 앱별 확장 | 이미 그 형태 |
| `HttpException` + `errorMiddleware` | 403을 던질 통로 |
| `envFrom` + `secretRef` | postgres로 이미 도는 패턴 |
| 통합 테스트 하네스 | 로비·매칭 양쪽에 `test/integration/`(globalSetup, db.ts) |

새로 들여오는 의존성은 **`express-rate-limit` 하나**다. `supertest`는 로비엔 있고 **매칭엔 없어 추가**한다.

## 3. 라우트 변경

### 3.1 로비

```ts
// apps/lobby-server/src/routes/lobby.route.ts
this.router.put(`${this.path}/join/:id`, authMiddleware, requireSelf('id'), this.lobbyController.joinLobby);
// leave 라우트는 삭제 (§7)
```

### 3.2 매칭

```ts
// apps/matchmaking-server/src/routes/matchmaking.route.ts
this.router.post(`${this.path}`, authMiddleware, validationMiddleware(RequestMatchmakingDto, 'body'), this.matchmakingController.requestMatchmaking);
this.router.delete(`${this.path}/:ticketId`, authMiddleware, this.matchmakingController.cancelMatchmaking);
```

> **순서가 계약이다.** `authMiddleware`가 **라우트 핸들러보다 먼저** 와야 한다. 뒤에 오면
> 핸들러가 부분 처리한 뒤 401이 나가 대기표가 중복 생성될 수 있다(401 재시도는 같은 요청을 본문째
> 다시 보낸다). 통합 테스트가 이 순서를 고정한다.

## 4. 요청 본문에서 `userId` 제거

`RequestMatchmakingDto`에서 `userId` 필드를 **지운다.** 서비스는 `req.userId`(토큰에서 온 신원)를 받는다.

```ts
// dto
export class RequestMatchmakingDto {
    @IsNumber() public queueId: number;
    @IsNumber() public gameModeId: number;
    @IsNumber() public mapId: number;
}

// controller
const response = await this.matchmakingService.requestMatchmaking(req.userId, req.body);
```

`requestMatchmaking(userId: string, dto: RequestMatchmakingDto)`로 시그니처를 바꾸고, 본문 안의
`requestMatchmakingDto.userId` 참조 **4곳**(`matchmaking.service.ts` 22·65·75·88행)을 인자로 교체한다.
미들웨어는 `import { authMiddleware, requireSelf } from '@lop/server-core/auth'`로 가져온다(서브패스
export 확인됨).

**남겨두고 무시하지 않는 이유**: 남기면 API가 거짓말을 하게 되고(보내면 반영될 것처럼 보임),
남겨두고 비교하면 검사 지점이 둘로 늘어난다. **클라가 자기 이름을 스스로 신고하는 구조를 없앤다.**

### ⚠️ 배포를 묶어야 한다

`validationMiddleware`가 **`forbidNonWhitelisted = true`** 로 돌아간다. 즉 DTO에 없는 필드를 보내면
**400**이다. 서버만 먼저 올리면 `userId`를 계속 보내는 구버전 클라의 매칭이 전부 400으로 죽는다.

- **지금(dev, 배포된 클라 없음)**: 클라·백엔드를 **함께** 올린다.
- **나중(실사용 클라가 존재할 때)**: 클라에서 필드를 먼저 빼 배포 → 충분히 퍼진 뒤 서버를 조인다.
  이 순서는 이 문서에 남겨 둔다.

클라 변경은 `MatchmakingRequest`에서 `public string userId;` 한 줄 삭제 + 채우던 호출부 한 줄.

## 5. 대기표 주인 확인

취소는 `ticketId`만 받는다. 티켓은 `userIds` 배열을 갖고 있으므로(현재 1인 파티라 `[0]`) 그걸 요청자와 비교한다.

```ts
// controller
const response = await this.matchmakingService.cancelMatchmaking(req.params.ticketId, req.userId);
```

서비스에서 티켓을 이미 조회하므로 **그 자리에서** 비교한다(중복 조회 없음).

```ts
const matchmakingTicket = await this.matchmakingTicketService.findMatchmakingTicketById(ticketId);
if (!matchmakingTicket) {
    return { code: ResponseCode.MATCH_MAKING_TICKET_NOT_EXIST };
}

//  남의 대기표는 "없는 것"이 아니라 "권한 없음"이다 — 존재 여부는 uuid를 이미 아는 사람만 알 수 있어
//  숨길 실익이 없고, requireSelf와 같은 어휘(403)를 쓰는 편이 일관된다.
if (matchmakingTicket.userIds.includes(requesterId) === false) {
    throw new HttpException(403, 'Forbidden.');
}
```

**새 `ResponseCode`를 만들지 않는다.** `ResponseCode`는 Unity 클라의 C# enum과 값이 같아야 하는
**와이어 계약**이라, 한쪽만 늘리면 조용히 어긋난다. 403은 `HttpException` → `errorMiddleware` 경로로
나가고, 이는 `requireSelf`가 이미 쓰는 모양이다.

정상 플레이에서는 발생하지 않는 응답이므로 클라 처리를 추가하지 않는다.

## 6. 검증 주체와 서명키

### 6.1 누가 검증하나 — 바뀌지 않는다

로비도 매칭도 **각자 HS256으로 로컬 검증**한다(`authMiddleware` → `verifyAccessToken`). 상시 떠 있는
백엔드이고 서로 이미 통신하는 사이라 서명키를 갖는 것이 자연스럽고, JWT를 쓰는 원래 이유(매번
발급자에게 묻지 않으려고)에도 맞는다.

→ `matchmaking-server`의 `main.ts`에 `validateEnv({ AUTH_JWT_SECRET: str() })`를 추가한다.
   **공유 `validateEnv`에 넣지 않는다** — 그 값을 안 쓰는 `room-server`가 부팅 즉사한다(과거에 낸 회귀).
   `director.ts`는 HTTP 라우트를 서빙하지 않으므로 **추가하지 않는다.**

> **키가 다르면 영구 401이 난다.** 서명하는 쪽(로비)과 검증하는 쪽(매칭)이 같은 값을 봐야 한다.
> 1a의 스로틀이 로그인 폭주는 막지만 사용자 경험은 그대로 깨진다.

### 6.2 서명키를 이미지 밖으로

**현재**: `apps/lobby-server/.env.development.local-k8s`가 git에 커밋돼 있고 도커 이미지에 통째로
구워진다. 지금 값은 `local-dev-only-CHANGE-ME-not-a-real-secret`(자리표시자)이라 당장의 노출 피해는
없지만, **구조가 문제다** — 환경마다 다른 키를 쓰려면 이미지를 다시 빌드해야 하고, 이미지를 받을 수
있는 사람은 값을 꺼낼 수 있다.

**결정**: k8s Secret으로 주입한다. 앱 코드는 `process.env.AUTH_JWT_SECRET` 그대로 — **읽는 방식도
검증 주체도 안 바뀌고, 값의 출처만 이미지 → 클러스터 설정으로 옮긴다.** 12-factor의 "config는 코드가
아니라 환경에" 원칙이고, 쿠버네티스에서 그 수단이 Secret이다.

- `kubectl create secret generic auth-secret --from-literal=AUTH_JWT_SECRET='<값>'` — **매니페스트를
  git에 넣지 않는다.** 명령은 `infrastructure` 문서에 남긴다.
- lobby·matchmaking deployment에 `- secretRef: { name: auth-secret }` 추가 (postgres와 같은 모양).
- 커밋된 `.env.development.local-k8s`에서 그 줄 **삭제**. 로컬 개발용 `.env.development.local`은
  **유지**(k8s를 안 쓰므로 거기서 읽어야 한다).
- `dotenv`는 기본이 `override: false`라 **k8s가 준 환경변수가 이긴다** — 순서 걱정 없음.
- **게임서버 파드에는 넣지 않는다** — 1c에서 introspection을 쓰므로 불필요.

**ArgoCD가 지우지 않는다**: `prune: true`지만 프루닝 대상은 **ArgoCD가 만든 리소스**뿐이다. 밖에서
만든 Secret은 추적되지 않아 대상이 아니다.

> **왜 매니페스트를 git에 안 넣나 — 업계 표준.** base64는 인코딩이지 암호화가 아니라 누구나 즉시
> 되돌린다. GitOps의 정석은 **Sealed Secrets / External Secrets Operator / SOPS**로 *암호화해서* 넣는
> 것이다. 지금 `postgres-secret.yaml`이 평문으로 git에 있는 것은 **따라야 할 관례가 아니라 기존
> 부채**다. 이번엔 손대지 않는다(배포가 도는 걸 흔든다). **후속**: SealedSecrets/ESO를 도입해
> auth·postgres를 함께 정리 — 컨트롤러 설치·키 관리가 딸린 별도 인프라 슬라이스.

## 7. 고아 라우트 삭제

`PUT /lobby/leave/:id`는 라우트·컨트롤러·서비스가 다 있는데 **부르는 곳이 어디에도 없다**(클라의
`LeaveLobby`는 슬라이스 0에서 삭제, 백엔드 내부 호출자도 0). 지금은 인터넷에서 아무나 남을 로비에서
뺄 수 있다.

라우트·컨트롤러 메서드·서비스 메서드·응답 DTO를 함께 지운다. 필요해지면 그때 인증을 갖춰 다시 만든다.

## 8. 레이트리밋

### 무엇을 막나

`POST /auth/anonymous`는 자격증명 없이 누구나 부를 수 있고, 한 번에 **DB 4행 + bcrypt 해시 1회**를
쓴다. bcrypt는 무차별 대입을 어렵게 하려고 **일부러 느리고**(~100ms), Node에서는 **libuv 스레드풀
(기본 4개)** 에서 돈다. 초당 40회면 풀이 포화되고, **같은 풀을 쓰는 파일 I/O·DNS·gzip이 전부 밀려
로비 서버 전체가 느려진다.** 공격이 아니라 **클라 재시도 루프**만으로도 도달한다. `/auth/login`도
같은 bcrypt 비용을 진다.

부차적으로, 계정을 무한히 만들 수 있는 것 자체가 신규 계정 보상이 생기면 어뷰징 통로다.

### 결정

`express-rate-limit`, **프로세스 메모리** 저장소.

**리미터 인스턴스를 엔드포인트마다 따로 둔다.** 하나를 공유하면 표의 숫자가 각각이 아니라 **합계**가 된다.

| 대상 | 한도 | 근거 |
|---|---|---|
| `/auth/anonymous` | IP당 15분 30회 | 계정 생성은 남용 통로라 더 조인다 |
| `/auth/login` | IP당 15분 200회 | 앱 시작마다 + 갱신마다 불린다. 갱신 최소 간격 30초라 클라 한 대만도 15분에 30회까지 나오고, 공유 NAT 뒤엔 그런 클라가 여럿이다. 200이면 한 IP에 100~200명까지 버틴다 |
| (1c의 `/auth/introspect`) | 1c에서 정한다 — bcrypt가 없어 훨씬 느슨하게 | |

둘을 합쳐도 15분 230회로, bcrypt 포화 임계(초당 40회 ≈ 15분 36,000회)의 **0.6%** 다 — 원래 보호 목적은
그대로이면서 정상 사용자를 막지 않는 쪽에 여유를 뒀다.

> **한계**: IP로 세는 한 CGNAT 뒤 대규모 공유는 못 가른다. 로그인은 본문에 `provider`+`providerUserId`가
> 있으니 **계정 단위 키**가 정공법이고, 익명 생성은 기기/어테스테이션 범위가 방향이다 — 단순히 IP
> 한도를 올리는 것은 잘못된 지렛대다. §13 후속 참조.

프로세스 메모리인 이유: **막으려는 대상(그 프로세스의 bcrypt 스레드풀)과 범위가 정확히 일치**하고,
새 의존성·실패 경로가 없다. 레플리카는 현재 1개. 늘릴 계획이 생기면 `rate-limit-redis`로 옮긴다
(Redis는 이미 로비가 물고 있다).

### 🔴 한도 초과는 **429**다. 401이면 안 된다

`express-rate-limit`의 기본이 429이므로 **기본값을 바꾸지 않으면 된다.** 그런데 이게 왜 차단
요구사항인지 명확히 해 둔다:

갱신은 이제 `POST /auth/login`을 **백그라운드에서 반복 호출**한다. 리미터나 프록시가 그걸 401로 막으면
갱신 자체는 안전하지만(1a가 옛 토큰 반환, 자격증명 유지) — **다음 앱 시작 시** `SignInAsync`가 같은
리미터에 걸려 401을 받고, 그걸 "이 자격증명은 거부됐다"로 읽어 **자격증명을 지우고 새 계정을 만든다.**
이 코드베이스가 이미 겪은 계정 유실 버그다.

통합 테스트로 429를 고정한다.

### 세트로 따라오는 `trust proxy`

`app.set('trust proxy', 1)` — `server-core`의 `App`에 넣는다.

**지금 이 설정이 없어서** nginx ingress를 통해 들어온 요청의 `req.ip`가 전부 ingress 파드 IP 하나로
보인다. 이 상태로 IP당 제한을 걸면 제한이 **전 세계 공용**이 되어, 한 명이 한도를 다 쓰면 나머지
전원이 로그인 불가가 된다 — 막으려던 것보다 나쁜 사고다.

`1`인 이유는 앞에 프록시가 nginx ingress 하나뿐이기 때문. **`true`는 안 된다** — 클라가
`X-Forwarded-For`를 위조해 제한을 빠져나간다. (`express-rate-limit` v7은 과허용 설정을 감지하면
`ERR_ERL_PERMISSIVE_TRUST_PROXY`로 막는다.)

## 9. 테스트

**통합 테스트 중심.** 이 슬라이스의 실수는 대부분 *배선 순서*와 *미들웨어 조합*이라 단위 테스트로는
안 잡힌다. 기존 하네스(`test/integration/` + `supertest` + `new App([...]).getServer()`)를 그대로 쓴다.

### 로비 (`apps/lobby-server/test/integration/`)

| 시나리오 | 기대 |
|---|---|
| 토큰 없이 `PUT /lobby/join/:id` | 401 |
| 형식이 깨진 Authorization 헤더 | 401 |
| 만료·위조 토큰 | 401 |
| **남의 id**로 입장 | 403 |
| 자기 id로 입장 | 200 |
| `/auth/login` 한도 초과 | **429** (401이 아님을 명시적으로 단언) |
| `PUT /lobby/leave/:id` | 404 (라우트 없음) |

### 매칭 (`apps/matchmaking-server/test/integration/`) — `supertest` 추가 필요

| 시나리오 | 기대 |
|---|---|
| 토큰 없이 `POST /matchmaking` | 401 |
| 본문에 `userId`를 넣어 보냄 | **400** (DTO에 없는 필드) |
| 정상 요청 | 토큰의 신원으로 티켓 생성 — 티켓의 `userIds[0] == 토큰의 sub` |
| 토큰 없이 `DELETE /matchmaking/:ticketId` | 401 |
| **남의** 대기표 취소 | 403 + **티켓이 그대로 남아 있음** |
| 자기 대기표 취소 | 200 |

마지막 항목에서 *티켓이 실제로 안 지워졌는지*까지 확인한다 — 403을 주면서 일은 벌어지는 것이
이 슬라이스에서 가장 위험한 실패 모양이다(§3의 미들웨어 순서와 같은 뿌리).

### 단위 테스트

`authMiddleware`/`requireSelf`는 이미 있다. 새로 추가하는 순수 로직이 없으므로 단위 테스트는 늘리지 않는다.

## 10. 배포 순서와 수동 검증

### 10.1 배포 순서 — 틀리면 조용히 헛돈다

클러스터는 **CI가 빌드한 핀된 이미지**를 돌고, `backend-deploy.yml`은 **`workflow_dispatch`(수동)** 다.
즉 머지만으로는 이 코드가 클러스터에 들어가지 않는다. 그리고 그 워크플로의 `bump-tags` 잡은
**infrastructure의 `main`을 체크아웃해** 새 이미지 태그를 밀어 넣는다.

```
1. infrastructure 머지  ← 반드시 먼저
2. lop-backend 머지
3. GitHub Actions에서 backend-deploy 수동 실행
4. ArgoCD 동기화 대기
5. 클라도 이 브랜치 빌드로 실행  ← §4 때문에 구버전 클라는 400
6. 아래 검증
```

**1을 건너뛰면**: `bump-tags`가 `secretRef: auth-secret`이 없는 `main`에 새 태그를 밀고, 새 파드가
`AUTH_JWT_SECRET` 없이 떠서 **로비·매칭 둘 다 크래시루프**한다(둘 다 `validateEnv`가 요구). replicas 1 +
RollingUpdate라 **옛 파드가 계속 서비스**하므로 ArgoCD는 Synced라고 보고하고, 검증하러 가면
"인증이 안 걸렸네?"가 된다 — 새 코드가 아예 안 떴기 때문이다.

### 10.2 검증

1. **정상 플레이** — 로그인 → 로비 → 매칭 요청 → 취소가 이전과 동일.
2. **401 재시도 경로** — **1a에서 확인하지 못한 것을 여기서 처음 밟는다.** 토큰을 강제로 만료시켜
   (예: 클라에서 `AccessTokenInfo`의 만료 시각을 과거로) 요청을 보내면, 401 → 갱신 → 재전송으로
   **성공**해야 한다. 1a의 핵심 경로가 실제로 도는지 보는 유일한 기회다.
3. **레이트리밋** — `/auth/login`을 반복 호출해 **429**가 나오는지 확인한다.

   > ⚠️ **초안의 "그 뒤 앱을 재시작해 계정 유지 확인"은 자기모순이라 삭제했다.** 창(15분)이 아직
   > 열려 있어 재시작 시 로그인도 429를 맞고, 검증자는 "리미터가 로그인을 망가뜨렸다"고 읽게 된다.
   > 계정 유지를 보려면 **다른 출발 IP에서** 확인하거나 창이 닫히길 기다린 뒤 재시작할 것.
   > (429가 자격증명을 지우지 않는다는 것 자체는 클라의 catch 필터가 `401`만 잡는다는 사실과
   > 통합 테스트로 이미 고정돼 있다.)
4. **서명키 일치** — 매칭 요청이 401 없이 통과하면 로비·매칭이 같은 키를 보고 있다는 뜻이다.

## 11. 산업 표준 매핑

| 우리 | 대응 |
|---|---|
| `Authorization: Bearer` + 로컬 HS256 검증 | RFC 6750, RFC 7519 |
| 소유권 불일치 → 403 | HTTP 의미 그대로(인증됐으나 권한 없음) |
| 본문 `userId` 제거, 토큰 신원 사용 | OAuth 계열 공통 — 클라가 주체를 자칭하지 않는다 |
| `trust proxy` 홉 수 명시 | Express 공식 권고(`true` 금지) |
| 한도 초과 429 | RFC 6585 |
| Secret을 환경변수로 주입 | 12-factor III(config), k8s Secret |
| (후속) 암호화 후 git | Sealed Secrets / External Secrets Operator / SOPS |

## 12. 위험

| 위험 | 대응 |
|---|---|
| `userId` 제거로 구버전 클라 400 | 클라·백엔드 **함께 배포**(§4). 실사용 클라가 생기면 순서 반대로 |
| 로비·매칭의 서명키 불일치 → 영구 401 | 같은 `auth-secret`을 양쪽 deployment가 참조. 수동 검증 1에서 드러남 |
| `trust proxy` 누락 → 전역 공용 한도 | `App`에 함께 넣는다. 순서상 리미터보다 먼저 |
| 미들웨어를 핸들러 뒤에 배선 | 통합 테스트가 "403인데 티켓이 남아 있다"로 고정 |
| Secret 미생성 상태로 배포 | `validateEnv`가 부팅 시 즉사시킨다(조용한 오작동보다 낫다) |
| 클러스터 재구축 시 Secret 유실 | `kubectl create secret` 명령을 infrastructure 문서에 남긴다 |

## 13. 후속 (최종 리뷰가 남긴 것)

- **`GET /user/all` 삭제** — 전체 계정 목록을 인터넷에 준다. **호출자가 어디에도 없다**(백엔드·클라·게임서버
  전부 확인) — 조회 라우트를 열어둔 명분("내부 서비스가 같은 경로를 쓴다")이 **이 라우트엔 적용되지 않는다.**
  받아들인 조회 라우트가 아니라 **아직 안 지운 고아**로 분류할 것. 남겨두면 userId 키 라우트들의 열거
  수단이 된다 — 예: `GET /user/all` + `PUT /user/location`(내부 전용이지만 열려 있음)으로 전 플레이어의
  매칭을 막을 수 있다.
- **내부 전용 변경 라우트 차단** — `PUT /user/location`, `PUT /room/status`, `PUT /room/heartbeat/:id`,
  `POST /room`, `DELETE /room/:id`. 서비스 간 인증이 필요해 이번 범위 밖(§1). 선택지: 서비스 간 토큰 /
  ingress 경로 축소 / 내부 전용 포트.
- **레이트리밋 키를 계정 단위로** — IP로 세는 한 CGNAT 뒤 대규모 공유는 못 가른다. 로그인은 본문에
  `provider`+`providerUserId`가 있으니 계정 단위 키가 정공법. 익명 생성은 기기/어테스테이션 범위 +
  Redis 저장소가 방향(단순히 IP 한도를 올리는 게 아니다).
- **커밋된 `.env`에서 키를 완전히 걷어내기** — `.env.development.local`(로비·매칭)에 아직 서명키가 있고
  이미지에 실린다. 값은 자리표시자라 무해하지만, §6.2의 "이미지 밖으로" 전제가 완전히 성립하진 않는다.
  `.dockerignore`에 `.env*` 추가 + 어떤 커밋된 `.env`에도 키를 쓰지 않는다는 규칙. 로비 Dockerfile의
  "`.env`는 gitignore되어 빌드 컨텍스트에 없다"는 주석도 사실과 다르다.
- **로컬 비-k8s 키 이중 관리** — 로비·매칭의 `.env.development.local`이 같은 값을 각자 들고 있다. 한쪽만
  고치면 매칭이 영구 401인데 원인을 알려주는 신호가 없다.
- **죽은 UserProfile 배관 정리** — `PUT /user/profile` 삭제로 `UserProfile`을 쓰는 곳이 사라졌다. 백엔드에
  6파일(repository/factory/entity-mapper/dao×2/interface), 클라에 `UpdateUserProfileRequest`/`Response`·
  `UserDataStore.HandleUpdateUserProfile`·브로커 등록이 남아 있다. 전부 컴파일되므로 급하지 않다.
- **통합 테스트 앱 조립을 `main.ts`와 공유** — 테스트가 라우트 클래스를 직접 나열해, 다른 클래스에 있던
  라우트의 부재는 증명하지 못한다. 라우트 목록을 공용 팩토리로 빼면 구조적으로 닫힌다.
- **`GET /user/username/:username`** — 호출자 0곳인 조회 고아. 위험은 낮지만 정리 대상.
