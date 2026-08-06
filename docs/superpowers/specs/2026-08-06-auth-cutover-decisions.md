# 인증 cutover — 확정된 결정 기록

> **이것은 완성된 스펙이 아니라 결정 기록이다.** 2026-08-06 브레인스토밍에서 아래 결정을 모두
> 확정했으나, 클라이언트 토큰 갱신 부분이 **슬라이스 0(HTTP 계층 표준화)** 의 결과 모양에
> 의존하므로 전체 스펙은 슬라이스 0이 끝난 뒤에 쓴다.
> 슬라이스 0: `2026-08-06-http-client-layer-standardization-design.md`
> 선행 슬라이스: `2026-08-04-anonymous-auth-session-design.md`

## 0. 지금 상태 — 인증이 절반만 켜져 있다

익명 로그인 슬라이스(2026-08-04)로 **토큰 발급·저장·전송은 동작한다.** 그러나 **아무도 토큰을
검사하지 않는다.** 클라는 `Authorization: Bearer`를 붙여 보내고 서버는 그걸 읽지 않는다.

그래서 지금 이 상태다.

- 남의 `userId`만 알면 그 사람으로 로비에 입장하고, 매칭을 걸고, 남의 대기표를 취소할 수 있다.
- 방(게임서버) 접속은 `token = "token"` 리터럴을 보내고, 서버는 "이 userId가 매치 참가자 명단에
  있나"만 확인한다. **같은 매치 사람들의 userId는 게임 중 서로 다 보이므로**, 옆 사람 id로 접속해
  남의 캐릭터를 조종할 수 있다.
- 토큰 수명이 1시간인데 **갱신을 부르는 코드가 없다.** 검사를 켜는 순간 1시간 넘는 세션이 깨진다.

## 1. 신뢰 경계 — 플레이어 경로만 닫는다

### 발견: 백엔드 3개가 서로를 인증 없이 부른다

| 부르는 쪽 | 부르는 대상 |
|---|---|
| matchmaking → lobby | `GET /user/:id`, `GET /user/:id/stats`, `PUT /user/location` |
| room → lobby | `GET /user/:id`, `PUT /user/location` |
| lobby → matchmaking, lobby → room | 조회 |
| Unity 게임서버 → room | `PUT /room/heartbeat/:id`, `PUT /room/status`, `POST/DELETE /room` |

게다가 **ingress가 세 서비스의 모든 경로를 그대로 공개한다** (`/lobby/*`, `/matchmaking/*`,
`/room/*`). 내부 호출은 클러스터 내부 Service 주소로 가서 ingress를 안 거칠 뿐, 경로 자체는 열려
있다. 여기에 `authMiddleware`를 전 라우트에 붙이면 **서비스 간 호출이 전부 401로 죽는다.**

### 결정: 플레이어 전용 변경 동작 3개 + 방 접속만 닫는다

닫는 것:

| 동작 | 검사 |
|---|---|
| `PUT /lobby/join/:id` | `authMiddleware` + `requireSelf('id')` |
| `POST /matchmaking` | `authMiddleware`. 요청 본문의 `userId`를 **제거**하고 `req.userId`를 쓴다 |
| `DELETE /matchmaking/:ticketId` | `authMiddleware` + 대기표 주인 == `req.userId` (서비스에서 조회 후 비교, 아니면 403) |
| 방 접속 (Mirror) | 토큰의 신원 == 접속 요청 userId == 매치 참가자 명단 |

이 셋은 **클라 전용 경로라 내부 호출자가 없다.** 그래서 서비스 간 인증 없이 깨끗하게 닫힌다.
그리고 사칭 피해가 가장 큰 것들이기도 하다(남을 이동시키기·남을 매칭 걸기·남의 대기표 취소).

안 닫는 것 — **조회 계열** (`GET /user/:id`, `/stats`, `/location`, `/match/:id`). 내부 서비스가
같은 경로를 쓴다. 닫으려면 서비스 간 토큰이 따라오고 범위가 2배 이상이 된다. 피해는 남의
닉네임·전적 열람 수준이다. **범위 밖으로 두고 여기 기록한다.**

`userId`를 요청 본문에서 지우는 이유: 남겨두고 무시하면 API가 거짓말을 하게 되고, 남겨두고
비교하면 검사 지점이 둘로 늘어난다. **클라가 자기 이름을 스스로 신고하는 구조 자체를 없앤다.**
백엔드 `RequestMatchmakingDto`와 클라 `MatchmakingRequest` 양쪽 각각 한 줄.

### 이월: ingress가 내부 라우트를 공개하는 상태

조회를 안 닫기로 했으므로 `PUT /user/location`, `PUT /room/status` 같은 **내부 전용 변경 라우트가
인터넷에 열린 채로 남는다.** 아무나 남을 순간이동시키거나 방 상태를 바꿀 수 있다. 별도 슬라이스로
처리한다 — 선택지는 (a) 서비스 간 토큰, (b) ingress 경로 축소, (c) 내부 전용 포트 분리.

## 2. 토큰 검증 방식 — 서비스마다 다르다

### 백엔드 서비스(로비·매칭)는 직접 검증

`authMiddleware`가 HS256으로 로컬 검증한다. 상시 떠 있는 백엔드이고 서로 이미 통신하는 사이라
서명키를 갖는 것이 자연스럽다. JWT를 쓰는 원래 이유(매번 발급자에게 묻지 않으려고)에도 맞는다.

→ `matchmaking-server`에도 `AUTH_JWT_SECRET`과 `validateEnv` 항목을 추가해야 한다.
   (지금은 lobby만 요구한다. **공유 `validateEnv`에 넣으면 room-server·matchmaking이 기동
   즉사한다** — 인증 슬라이스에서 실제로 낸 회귀다. 앱별 추가 스펙으로 넣을 것.)

### 게임서버(Unity)는 로비에 물어본다 (introspection)

**결정 근거(사용자):** "게임 룸은 단순 게임 플레이 및 결과 전송 정도이지 굳이 저런 로직이 필요
없을 것 같아." 신원 확인은 신원의 주인이 한다.

부수 효과로 **게임서버 파드에 서명키를 안 넣어도 된다.** 방마다 뜨고 지는 임시 프로세스에 위조
능력을 뿌리지 않는다. (HS256은 대칭키라 검증 능력 = 위조 능력이다.)

호출 빈도는 **플레이어 1명이 방에 들어갈 때 1회**다. Mirror의 인증 훅은 연결 수립 시 한 번만 돌고
그 뒤로는 다시 확인하지 않는다. 10인 매치면 매치 시작 시 10회, 클러스터 내부 호출이라 1~5ms.

**대가:** 로비가 죽으면 방 입장이 불가능해진다. 감수하기로 했다. (로비가 죽으면 매칭도 로비를
부르므로 새 매치 자체가 안 만들어진다 — 실질 차이는 이미 만들어진 방뿐이다.)

**부수:** `GameFramework.Auth.Jwt`(HS256 검증, 테스트 12건)가 쓰이지 않게 된다. 삭제 여부는 스펙
작성 시 판단한다.

### 검증 API — RFC 7662 (OAuth 2.0 Token Introspection)

```
POST /auth/introspect
  요청:  { "token": "<액세스 토큰>" }
  응답:  { "active": true,  "sub": "abc-123" }
        { "active": false }
```

필드 이름 `token` / `active` / `sub`는 전부 **RFC 7662가 정한 이름**이다(`sub`는 JWT의 RFC 7519
에서도 같은 뜻). 토큰이 나쁠 때 401이 아니라 **200 + `active: false`** 를 주는 것도 규격대로다 —
401은 "부른 쪽의 자격이 나쁘다"는 뜻이라 의미가 다르다.

**규격에서 벗어나는 한 곳:** RFC는 요청 본문을 form-encoded로 규정하지만 **JSON을 쓴다.** 백엔드가
전부 `express.json` + class-validator DTO로 돌아가는데 이 엔드포인트 하나만 form 파싱을 붙이는
것은 이득 없는 예외다. 필드 이름은 규격대로, 인코딩만 우리 관례대로.

**인증 없이 연다.** 우리 토큰은 서명된 JWT라 찍어서 맞힐 수 없고, 유효한 토큰을 이미 가진 사람이
물어봐야 새로 얻는 정보가 없다. 대신 레이트리밋은 건다.

### 게임서버의 검사 순서 (순서가 중요하다)

```
1. 클라가 말한 userId가 매치 참가자 명단에 있나?   → 없으면 즉시 거절 (네트워크 호출 없음)
2. 로비에 POST /auth/introspect { token }          → active: false 면 거절
3. 응답의 sub 가 1번의 userId 와 같은가?            → 다르면 거절
4. 통과. 이후 이 연결의 신원은 sub 를 쓴다.
```

1번을 먼저 두는 이유: 아무나 연결을 열어 게임서버가 로비를 계속 두드리게 만드는 것을 막는다.
명단은 로컬 메모리라 공짜이고, 이걸 통과해야만 네트워크 호출이 나가므로 **호출 횟수가 매치
인원수 언저리로 묶인다.**

3번이 핵심이다. 1번만으로는 "명단에 있는 남의 이름"을 못 막고, 2번만으로는 "유효한 내 토큰 +
남의 userId"를 못 막는다. **둘을 이어붙여야** 사칭이 닫힌다.

배선은 이미 되어 있다 — Unity 게임서버의 `EnvironmentSettings`가 `lobbyServerBaseUrl:
http://lobby-server-service`(클러스터 내부 주소, ingress를 안 거침)를 이미 들고 있다.

### 명명 정리

Mirror 메시지의 `CustomProperties.token` → **`accessToken`** 으로 바꾼다. 클라가
`AccessTokenInfo`/`AuthSession.accessToken`, 백엔드도 `accessToken`으로 내려준다. 여기만 `token`
이면 같은 것에 두 어휘를 쓰게 된다. 클·서 struct 정의 한 줄씩.

## 3. 토큰 갱신 — 표준 JWT 갱신 패턴

### 우리는 이미 표준 위에 있다

리프레시 토큰을 안 만든 것은 표준에서 벗어난 것이 아니라 **게임 백엔드 쪽 표준**을 따른 것이다.
저장해 둔 익명 secret이 리프레시 토큰의 역할을 그대로 한다 — 길게 살고, 갱신에만 쓰이고,
비밀번호가 아니다. 다른 점은 갱신 창구가 `/token`이 아니라 `/auth/login`이라는 것뿐이다.
PlayFab이 정확히 이 모델이다(세션 티켓 + 저장된 device/custom ID로 재로그인).

| | OAuth 표준 | LOP |
|---|---|---|
| 짧은 토큰 | 액세스 토큰 | 액세스 토큰 (1시간) ✅ |
| 긴 자격 | 리프레시 토큰 | **저장된 익명 secret** (만료 없음) |
| 갱신 창구 | `POST /token` | `POST /auth/login` |
| 5분 미리 갱신 | ✅ | `AccessTokenInfo.NeedsRefresh(margin 5분)` **구현됨** |
| 401 재시도 | ✅ | 없음 — 이번에 넣는다 |
| single-flight | ✅ | 없음 — 이번에 넣는다 |

리프레시 토큰을 진짜로 도입하는 것은 지금 이득이 없다 — secret이 이미 비밀번호가 아니고, 토큰을
하나 더 늘리면 만료·회전·폐기 규칙이 따라붙는다. **플랫폼 로그인(구글/애플) 도입 시 재검토.**

### 결정: 미리 갱신 + 401 재시도 + single-flight

세 겹 전부 넣는다. MSAL·Google·AWS SDK·Axios/Retrofit 인터셉터가 전부 이 모양이다.

1. **미리 갱신** — 만료 5분 전이면 보내기 전에 먼저 재로그인. 5분은 업계 공통값이고 이미 구현돼 있다.
2. **401이면 갱신 후 1회 재시도** — 1번을 뚫고 실패한 경우의 안전망.
3. **single-flight** — 동시에 여러 요청이 갱신을 유발해도 **한 번만** 실행하고 나머지는 그 결과를
   기다린다. 이게 빠지면 앱 시작 시 요청 여러 개가 동시에 재로그인하고, 그중 하나가 401을 맞아
   **자격증명을 지워 계정이 유실된다.** 우리가 이미 겪은 부류의 버그(더블클릭 계정 2개 생성).

`SingleFlight`는 Go의 `singleflight` 패키지에서 온 확립된 용어다(CDN 쪽에선 request coalescing).

### 구현 위치 — 슬라이스 0에 의존

슬라이스 0 이후 이것은 **`AuthorizationHandler` 안에서 끝난다** — 보내기 전 갱신 확인, 보냄,
401이면 갱신 후 재전송. 핸들러 체인이 그걸 할 수 있게 만드는 것이 슬라이스 0의 목적이다.

**경계는 "정책은 프레임워크, 내용물은 앱"이다.** "401이면 갱신 후 1회 재시도"와 single-flight는
앱 비종속 정책(OkHttp `Authenticator`가 정확히 그것)이라 **GameFramework**에 둔다. "갱신 = 저장된
익명 secret으로 `/auth/login`을 친다"는 LOP 도메인이라 델리게이트로 주입한다.
(부수적으로 GameFramework 쪽은 EditMode 테스트가 가능하다 — 클라 본체엔 테스트 어셈블리가 없다.)

**방 접속은 401이 아니라 그냥 연결 거부**라 재시도 훅이 없다. 접속 직전에 미리 갱신 확인을 태운다.

## 4. 레이트리밋

### 막으려는 것

`POST /auth/anonymous`는 자격증명 없이 누구나 부를 수 있고, 한 번에 **DB 4행 + bcrypt 해시 1회**를
쓴다. bcrypt는 무차별 대입을 어렵게 하려고 **일부러 느리게**(~100ms) 설계됐고 Node에서는
**libuv 스레드풀(기본 4개)** 에서 돈다. 초당 40회면 풀이 포화되고, **같은 풀을 쓰는 파일 I/O·DNS
조회·`compression`(gzip)이 전부 밀려 로비 서버 전체가 느려진다.** 공격이 아니라 **클라 재시도
루프**만으로도 도달한다. `/auth/login`도 같은 bcrypt 비용을 진다.

부차적으로, 계정을 무한히 만들 수 있는 것 자체 — 나중에 신규 계정 보상이 생기면 어뷰징 통로다.

### 결정: `express-rate-limit`, 프로세스 메모리, 두 종류

| 대상 | 한도 | 이유 |
|---|---|---|
| `/auth/anonymous`, `/auth/login` | IP당 15분 20회 (튜닝 대상) | bcrypt가 비싸다 |
| `/auth/introspect` | 훨씬 느슨하게 | bcrypt 없음, 게임서버가 정상적으로 부른다 |

프로세스 메모리를 고른 이유: **막으려는 대상(그 프로세스의 bcrypt 스레드풀)과 범위가 정확히
일치**하고, 새 의존성·실패 경로가 없다. 레플리카는 현재 1개. 늘릴 계획이 생기면 `rate-limit-redis`
(Redis는 이미 로비가 물고 있다)로 옮긴다.

### 세트로 따라오는 것: `trust proxy`

`app.set('trust proxy', 1)`. **지금 이 설정이 없어서**, nginx ingress를 통해 들어온 요청의 `req.ip`가
전부 ingress 파드 IP 하나로 보인다. 이 상태로 IP당 제한을 걸면 제한이 **전 세계 공용**이 되어,
한 명이 한도를 다 쓰면 나머지 전원이 로그인 불가가 된다 — 막으려던 것보다 나쁜 사고다.

`1`인 이유는 앞에 프록시가 nginx ingress 하나뿐이기 때문이다. **`true`는 안 된다** — 클라가
`X-Forwarded-For`를 위조해 제한을 빠져나간다. (`express-rate-limit` v7은 과허용 설정을 감지하면
`ERR_ERL_PERMISSIVE_TRUST_PROXY`로 막는다.)

## 5. 서명키 위치

### 현재 상태 — git에 평문으로 있다

`AUTH_JWT_SECRET`은 **`apps/lobby-server/.env.development.local-k8s`에 커밋되어 Docker 이미지에
통째로 구워지고 있다.** 그래서 k8s 매니페스트에 아무 설정이 없는데도 lobby-server가 정상 기동한다.
(선행 슬라이스의 "배포 전 확인" 2건은 이걸로 둘 다 해소됐다 — 이미지가 실제로 돌고 있으므로
bcrypt 네이티브 바인딩도 검증됐다.)

**이건 해결이 아니라 빚이다.** 저장소를 읽을 수 있는 사람은 아무 유저로든 토큰을 위조할 수 있다.
지금은 검사하는 곳이 없어서 무해하지만, **토큰을 강제하는 순간 이 키가 곧 인증 그 자체가 된다.**

### 결정: k8s Secret으로 옮긴다

- `auth-secret` Secret을 만들고 lobby-server·matchmaking-server deployment가 `secretRef`로 참조
- 커밋된 `.env.development.local-k8s`에서 값 제거 (로컬 개발용 `.env.development.local`은 유지)
- `dotenv`는 기본이 `override: false`라 **k8s가 준 환경변수가 이긴다** — 순서 걱정 없음
- **게임서버 파드에는 넣지 않는다** (introspection을 쓰므로 불필요)

base64는 암호화가 아니므로 **Secret 매니페스트를 git에 넣지 않는다.** dev는 `kubectl create secret`
으로 클러스터에 직접 만들고 명령을 문서에 남긴다. ArgoCD는 git에 없는 리소스를 관리하지 않으므로
`selfHeal`에 지워지지 않는다(프루닝 설정은 확인 필요).

## 6. cutover가 깨지지 않는 이유

**클라는 이미 모든 요청에 토큰을 싣고 있다**(`LOPWebRequestInterceptor.Default`). 따라서 HTTP 쪽
검사는 **서버만 고치면** 현재 클라와 그대로 맞물린다. 조율이 필요한 것은 방 접속(클·서 Unity 동시
변경)뿐이고, 그 둘은 dev에서 함께 배포된다.

DB 스키마 변경은 **없다.**

## 7. 남은 결정 / 스펙 작성 시 판단할 것

- `GameFramework.Auth.Jwt` (테스트 12건) 삭제 여부 — introspection 선택으로 사용처가 사라졌다
- `/auth/introspect` 레이트리밋 구체 한도
- 대기표 주인 불일치 시 403 vs 404 (ticketId가 uuid라 열거 위험은 없다 → 403으로 충분해 보임)
- 게임서버가 introspect를 부를 때의 타임아웃·실패 시 재시도 여부
