# 인증 cutover 1b — 서버 강제 + 인프라 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어 전용 변경 동작 3개(로비 입장·매칭 요청·매칭 취소)에 토큰 검사를 실제로 켜고, 문지기 자신을 지키는 장치(레이트리밋·`trust proxy`·서명키 분리)를 함께 넣는다.

**Architecture:** `server-core`에 이미 있는 `authMiddleware`/`requireSelf`를 라우트에 배선한다. 매칭 요청은 본문의 `userId`를 없애고 토큰의 신원을 쓴다. 취소는 대기표 주인과 대조해 403. `/auth/*`에 `express-rate-limit`을 걸고(`trust proxy` 세트), 서명키는 이미지 대신 k8s Secret에서 온다.

**Tech Stack:** TypeScript / Express / Prisma / jest + supertest + testcontainers / kubernetes / C#(Unity 클라 1줄)

**스펙:** `docs/superpowers/specs/2026-08-07-auth-cutover-1b-server-enforcement-design.md`

## Global Constraints

- **저장소 3개**, 전부 **본 체크아웃의 `feature/auth-cutover-1b-enforce` 브랜치**에 이미 있다.
  - `lop-backend` (`/Users/insoobae/workspace/LOP/lop-backend`, base `14e28bb`)
  - `infrastructure` (`/Users/insoobae/workspace/LOP/infrastructure`, base `6aca6be`)
  - 클라 (`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`, base `a757423`)
- **워크트리를 만들지 말 것.** `lop-backend`는 pnpm 워크스페이스라 워크트리에서 `node_modules`를 새로 깔아야 하고, 클라는 Unity가 본 체크아웃만 연다.
- **`git add`는 경로를 명시한다. `-A`나 `.` 금지.** 클라 본 체크아웃에 다른 작업의 미추적 파일이 많고(`Assets/Scripts/FlappyRaceSlice/` 등), `Assets/Art`·`ProjectSettings/*.asset`은 이미 수정 상태이니 **스테이징하지 않는다**. `infrastructure`에도 다른 작업(agones) 브랜치가 있으니 이 브랜치 파일만 만진다.
- **`ResponseCode`에 새 값을 추가하지 않는다.** Unity 클라의 C# enum과 값이 같아야 하는 와이어 계약이다. 권한 거부는 `HttpException(403)`으로 낸다.
- **`authMiddleware`는 반드시 라우트 핸들러보다 **먼저** 온다.** 뒤에 오면 부분 처리 후 401이 나가 대기표가 중복 생성된다.
- **레이트리밋 초과는 429다. 401이면 안 된다** — 401이면 다음 앱 시작 시 클라가 자격증명을 지운다(과거 계정 유실 버그).
- 주석은 **"왜"만, 쉬운 말로.** 코드로 자명한 것은 쓰지 않는다.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

## 테스트 실행

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter lobby-server test                 # 단위
pnpm --filter lobby-server test:integration     # 통합 (testcontainers로 postgres+redis 기동)
pnpm --filter matchmaking-server test
pnpm --filter matchmaking-server test:integration
```

통합 테스트는 도커가 떠 있어야 하고 컨테이너 기동에 수십 초 걸린다(`testTimeout: 60000`, `maxWorkers: 1`).

## ⚠️ 통합 테스트의 경계 — 매칭의 정상 경로는 여기서 못 본다

`matchmaking-server`는 유저·전적·위치를 **로비에 HTTP로 물어본다**(`LOBBY_SERVER_HOST`). 그래서
`POST /matchmaking`의 **성공 경로(200)** 는 로비가 실제로 떠 있어야 재현된다 — 통합 테스트에서는 다루지
않고 **수동 검증**으로 덮는다.

**거부 경로는 그 호출 전에 끊기므로 문제없다** — 401(미들웨어), 400(검증 미들웨어), 403(티켓 조회 직후
소유권 비교)은 전부 로비를 부르기 전이다. 이번 슬라이스가 검증해야 할 것이 정확히 그 세 가지다.

---

## 파일 구조

| 저장소 | 파일 | 작업 |
|---|---|---|
| backend | `packages/server-core/src/app.ts` | `trust proxy` 추가 |
| backend | `apps/lobby-server/src/middlewares/authRateLimit.ts` | 신규 — 리미터 |
| backend | `apps/lobby-server/src/routes/auth.route.ts` | 리미터 배선 |
| backend | `apps/lobby-server/src/routes/lobby.route.ts` | 인증 배선 + leave 삭제 |
| backend | `apps/lobby-server/src/controllers/lobby.controller.ts` | leave 삭제 |
| backend | `apps/lobby-server/src/services/lobby.service.ts` | leave 삭제 |
| backend | `apps/matchmaking-server/src/routes/matchmaking.route.ts` | 인증 배선 |
| backend | `apps/matchmaking-server/src/dtos/matchmaking.dto.ts` | `userId` 제거 |
| backend | `apps/matchmaking-server/src/controllers/matchmaking.controller.ts` | `req.userId` 전달 |
| backend | `apps/matchmaking-server/src/services/matchmaking.service.ts` | 시그니처 + 소유권 |
| backend | `apps/matchmaking-server/src/main.ts` | `AUTH_JWT_SECRET` 요구 |
| backend | 양쪽 `test/integration/` | 신규 테스트 |
| infra | `k8s/apps/backend/{lobby,matchmaking}-server/*-deployment.yaml` | `secretRef` 추가 |
| infra | `docs/` | `kubectl create secret` 명령 기록 |
| 클라 | `Assets/Scripts/WebAPI/Dto/Request/MatchmakingRequest.cs` | `userId` 제거 |

---

## Task 1: 레이트리밋 + `trust proxy`

**Files:**
- Modify: `packages/server-core/src/app.ts`
- Create: `apps/lobby-server/src/middlewares/authRateLimit.ts`
- Modify: `apps/lobby-server/src/routes/auth.route.ts`, `apps/lobby-server/package.json`
- Test: `apps/lobby-server/test/integration/authRateLimit.integration.test.ts`

**Interfaces:**
- Consumes: (없음)
- Produces: `authRateLimit` (Express `RequestHandler`)

**배경:** `POST /auth/anonymous`는 자격증명 없이 누구나 부를 수 있고 한 번에 bcrypt 해시를 쓴다. bcrypt는 일부러 느리고(~100ms) Node에서 **libuv 스레드풀(기본 4개)** 에서 돈다. 초당 40회면 풀이 포화돼 같은 풀을 쓰는 파일 I/O·DNS·gzip까지 밀린다. 공격이 아니라 클라 재시도 루프만으로도 도달한다.

- [ ] **Step 1: 의존성 추가**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter lobby-server add express-rate-limit
```

- [ ] **Step 2: 실패하는 테스트 작성**

`apps/lobby-server/test/integration/authRateLimit.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import AuthRoute from '@routes/auth.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new AuthRoute()]).getServer();

//  한도(15분 20회)를 넘기려면 21번을 불러야 한다. bcrypt가 붙어 있어 한 번에 ~100ms.
const LIMIT = 20;

describe('/auth/* 레이트리밋', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    //  401로 막으면 클라가 "이 자격증명은 거부됐다"로 읽고 저장된 자격증명을 지운다 — 계정 유실이다.
    it('한도를 넘으면 429를 준다 (401이 아니다)', async () => {
        let last;
        for (let i = 0; i < LIMIT + 1; i++) {
            last = await request(app)
                .post('/auth/anonymous')
                .set('X-Forwarded-For', '203.0.113.1')
                .send();
        }

        expect(last!.status).toBe(429);
        expect(last!.status).not.toBe(401);
    });

    //  trust proxy가 없으면 req.ip가 전부 ingress 파드 하나로 보여, 한 명이 한도를 다 쓰면
    //  나머지 전원이 로그인 불가가 된다 — 막으려던 것보다 나쁜 사고다.
    it('IP가 다르면 한도를 따로 센다', async () => {
        for (let i = 0; i < LIMIT + 1; i++) {
            await request(app).post('/auth/anonymous').set('X-Forwarded-For', '203.0.113.1').send();
        }

        const other = await request(app)
            .post('/auth/anonymous')
            .set('X-Forwarded-For', '203.0.113.2')
            .send();

        expect(other.status).toBe(201);
    });
});
```

- [ ] **Step 3: 실패 확인**

```bash
pnpm --filter lobby-server test:integration -- authRateLimit
```

Expected: 두 테스트 모두 실패 — 리미터가 없어 21번째도 201이 나온다.

- [ ] **Step 4: `trust proxy` 추가**

`packages/server-core/src/app.ts`의 `initializeMiddlewares` **맨 위**에:

```ts
        //  앞단 프록시는 nginx ingress 하나뿐이다. 이 설정이 없으면 req.ip가 전부 ingress 파드 IP로
        //  보여 IP당 제한이 전 세계 공용이 된다. true는 안 된다 — 클라가 X-Forwarded-For를 위조해
        //  제한을 빠져나간다.
        this.app.set('trust proxy', 1);
```

- [ ] **Step 5: 리미터 작성**

`apps/lobby-server/src/middlewares/authRateLimit.ts`:

```ts
import rateLimit from 'express-rate-limit';

//  bcrypt가 libuv 스레드풀(기본 4개)에서 도는 탓에, 로그인이 몰리면 같은 풀을 쓰는 파일 I/O·DNS·gzip이
//  전부 밀려 서버 전체가 느려진다. 저장소를 프로세스 메모리로 두는 이유도 같다 — 막으려는 대상이
//  "이 프로세스의 스레드풀"이라 범위가 정확히 일치하고, 새 의존성·실패 경로가 없다.
export const authRateLimit = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: 20,
    standardHeaders: 'draft-7',
    legacyHeaders: false,
});
```

> `express-rate-limit`의 기본 응답이 429다. **기본값을 바꾸지 않는다.**

- [ ] **Step 6: 배선**

`apps/lobby-server/src/routes/auth.route.ts`:

```ts
        this.router.post(`${this.path}/anonymous`, authRateLimit, this.authController.signInAnonymous);
        this.router.post(`${this.path}/login`, authRateLimit, validationMiddleware(LoginRequestDto, 'body'), this.authController.login);
```

- [ ] **Step 7: 통과 확인**

```bash
pnpm --filter lobby-server test:integration
pnpm --filter lobby-server test
```

Expected: 신규 2건 통과, 기존 통합·단위 테스트 전부 통과.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add packages/server-core/src/app.ts apps/lobby-server/src/middlewares/authRateLimit.ts \
        apps/lobby-server/src/routes/auth.route.ts apps/lobby-server/package.json pnpm-lock.yaml \
        apps/lobby-server/test/integration/authRateLimit.integration.test.ts
git commit -m "feat(auth): /auth/* 레이트리밋 + trust proxy

로그인은 bcrypt를 쓰고 그건 libuv 스레드풀에서 돈다. 초당 수십 회면 풀이 포화돼
같은 풀을 쓰는 파일 I/O·DNS·gzip까지 밀려 서버 전체가 느려진다 — 공격이 아니라
클라 재시도 루프만으로도 도달한다.

한도 초과는 429다(라이브러리 기본값). 401로 막으면 클라가 자격증명이 거부된 것으로
읽고 저장된 계정을 지운다.

trust proxy를 세트로 넣는다. 없으면 req.ip가 전부 ingress 파드 하나로 보여 IP당
제한이 전 세계 공용이 된다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 로비 입장 인증 + 고아 라우트 삭제

**Files:**
- Modify: `apps/lobby-server/src/routes/lobby.route.ts`, `src/controllers/lobby.controller.ts`, `src/services/lobby.service.ts`
- Modify: `apps/lobby-server/src/dtos/lobby.dto.ts` — `LeaveLobbyResponseDto`(8행) 삭제
- Test: `apps/lobby-server/test/integration/lobbyAuth.integration.test.ts`

**Interfaces:**
- Consumes: `authMiddleware`, `requireSelf` — `import { authMiddleware, requireSelf } from '@lop/server-core/auth'`
- Produces: (없음)

**배경:** `PUT /lobby/leave/:id`는 라우트·컨트롤러·서비스가 다 있는데 **부르는 곳이 어디에도 없다**(클라의 `LeaveLobby`는 슬라이스 0에서 삭제, 백엔드 내부 호출자도 0). 지금은 인터넷에서 아무나 남을 로비에서 뺄 수 있다.

- [ ] **Step 1: 실패하는 테스트 작성**

`apps/lobby-server/test/integration/lobbyAuth.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import { signAccessToken } from '@lop/server-core/auth';
import AuthRoute from '@routes/auth.route';
import LobbyRoute from '@routes/lobby.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new AuthRoute(), new LobbyRoute()]).getServer();

//  실제 계정을 만들어 토큰을 받는다 — 토큰만 위조하면 유저가 없어 다른 이유로 실패할 수 있다.
async function 계정을_만든다(): Promise<{ userId: string; accessToken: string }> {
    const response = await request(app).post('/auth/anonymous').send();
    return { userId: response.body.userId, accessToken: response.body.accessToken };
}

describe('PUT /lobby/join/:id', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('토큰이 없으면 401', async () => {
        const { userId } = await 계정을_만든다();

        const response = await request(app).put(`/lobby/join/${userId}`).send();

        expect(response.status).toBe(401);
    });

    it('Authorization 형식이 깨져 있으면 401', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app)
            .put(`/lobby/join/${userId}`)
            .set('Authorization', accessToken)   // "Bearer " 없음
            .send();

        expect(response.status).toBe(401);
    });

    it('위조된 토큰이면 401', async () => {
        const { userId } = await 계정을_만든다();

        const response = await request(app)
            .put(`/lobby/join/${userId}`)
            .set('Authorization', 'Bearer not.a.real.token')
            .send();

        expect(response.status).toBe(401);
    });

    //  이게 이 슬라이스가 막으려는 바로 그 사칭이다 — 같은 매치 사람들끼리 userId가 서로 보인다.
    it('남의 id로 입장하려 하면 403', async () => {
        const 나 = await 계정을_만든다();
        const 남 = await 계정을_만든다();

        const response = await request(app)
            .put(`/lobby/join/${남.userId}`)
            .set('Authorization', `Bearer ${나.accessToken}`)
            .send();

        expect(response.status).toBe(403);
    });

    it('자기 id로 입장하면 성공한다', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app)
            .put(`/lobby/join/${userId}`)
            .set('Authorization', `Bearer ${accessToken}`)
            .send();

        expect(response.status).toBe(200);
    });

    //  호출자가 0곳인 라우트를 열어두면 아무나 남을 로비에서 뺄 수 있다.
    it('leave 라우트는 존재하지 않는다', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app)
            .put(`/lobby/leave/${userId}`)
            .set('Authorization', `Bearer ${accessToken}`)
            .send();

        expect(response.status).toBe(404);
    });
});
```

- [ ] **Step 2: 실패 확인**

```bash
pnpm --filter lobby-server test:integration -- lobbyAuth
```

Expected: 401·403 기대 테스트는 200을 받아 실패, leave 테스트는 200을 받아 실패.

- [ ] **Step 3: 배선 + 삭제**

`apps/lobby-server/src/routes/lobby.route.ts`:

```ts
import { authMiddleware, requireSelf } from '@lop/server-core/auth';
...
    private initializeRoutes() {
        this.router.put(`${this.path}/join/:id`, authMiddleware, requireSelf('id'), this.lobbyController.joinLobby);
    }
```

`lobby.controller.ts`의 `leaveLobby`, `lobby.service.ts`의 `leaveLobby`, 그리고 그 반환 DTO
(`LeaveLobbyResponseDto`)를 지운다. 지운 뒤 `grep -rn "leaveLobby\|LeaveLobbyResponse"`로 잔재가 없는지 확인한다.

- [ ] **Step 4: 통과 확인**

```bash
pnpm --filter lobby-server test:integration
pnpm --filter lobby-server test
```

- [ ] **Step 5: 커밋** (경로 명시)

```
feat(lobby): 로비 입장에 토큰 검사 + 고아 leave 라우트 삭제

지금까지는 남의 userId만 알면 그 사람을 로비에 입장시킬 수 있었다. 같은 매치에 있는
사람들끼리 userId가 서로 보이므로 실제로 가능한 사칭이다.

leave는 클라·백엔드 어디에도 호출자가 없는데 인터넷에 열려 있어, 아무나 남을 로비에서
뺄 수 있었다. 필요해지면 인증을 갖춰 다시 만든다.
```

---

## Task 3: 매칭 요청을 토큰 신원으로 전환

**Files:**
- Modify: `apps/matchmaking-server/src/routes/matchmaking.route.ts`, `src/dtos/matchmaking.dto.ts`, `src/controllers/matchmaking.controller.ts`, `src/services/matchmaking.service.ts`, `src/main.ts`, `package.json`, `.env.development.local`, `test/integration/globalSetup.ts`
- Modify (호출부 수정): `src/services/__tests__/matchmaking.service.request.test.ts`
- Test: `apps/matchmaking-server/test/integration/matchmakingAuth.integration.test.ts`

**Interfaces:**
- Consumes: `authMiddleware` from `@lop/server-core/auth`
- Produces: `MatchmakingService.requestMatchmaking(userId: string, dto: RequestMatchmakingDto)`

**배경:** 클라가 본문에 `userId`를 실어 보내고 서버가 그걸 믿는다. 남기고 무시하면 API가 거짓말을 하게 되고(보내면 반영될 것처럼 보임), 남기고 비교하면 검사 지점이 둘로 늘어난다. **자기 이름을 스스로 신고하는 구조 자체를 없앤다.**

- [ ] **Step 1: 준비 — 의존성·환경변수**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter matchmaking-server add -D supertest @types/supertest
```

`apps/matchmaking-server/.env.development.local`에 한 줄 추가(로컬 개발용. **`.env.development.local-k8s`에는 추가하지 않는다** — 그쪽은 k8s Secret이 준다):

```
AUTH_JWT_SECRET = local-dev-only-CHANGE-ME-not-a-real-secret
```

`apps/matchmaking-server/test/integration/globalSetup.ts`에 로비와 같은 줄을 추가:

```ts
    process.env.AUTH_JWT_SECRET = 'integration-test-secret';
```

- [ ] **Step 2: 실패하는 테스트 작성**

`apps/matchmaking-server/test/integration/matchmakingAuth.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import { signAccessToken } from '@lop/server-core/auth';
import MatchmakingRoute from '@routes/matchmaking.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new MatchmakingRoute()]).getServer();

//  거부 경로만 다룬다 — 성공 경로는 매칭 서버가 유저·전적을 로비에 HTTP로 물어보므로
//  로비가 실제로 떠 있어야 재현된다(수동 검증에서 덮는다).
describe('POST /matchmaking — 거부 경로', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('토큰이 없으면 401', async () => {
        const response = await request(app)
            .post('/matchmaking')
            .send({ queueId: 1, gameModeId: 1, mapId: 1 });

        expect(response.status).toBe(401);
    });

    //  DTO에서 지운 필드를 보내면 400이다(validationMiddleware가 forbidNonWhitelisted).
    //  이 테스트가 "본문으로 신원을 신고하는 통로가 정말 없어졌는가"를 고정한다.
    it('본문에 userId를 실으면 400', async () => {
        const accessToken = signAccessToken('user-1');

        const response = await request(app)
            .post('/matchmaking')
            .set('Authorization', `Bearer ${accessToken}`)
            .send({ userId: 'user-2', queueId: 1, gameModeId: 1, mapId: 1 });

        expect(response.status).toBe(400);
    });
});

describe('DELETE /matchmaking/:ticketId — 거부 경로', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('토큰이 없으면 401', async () => {
        const response = await request(app).delete('/matchmaking/any-ticket-id').send();

        expect(response.status).toBe(401);
    });
});
```

- [ ] **Step 3: 실패 확인**

```bash
pnpm --filter matchmaking-server test:integration -- matchmakingAuth
```

Expected: 401 기대 두 건이 다른 코드를 받아 실패. `userId` 400 테스트도 아직 통과하지 않는다(현재 DTO가 그 필드를 허용).

- [ ] **Step 4: DTO에서 `userId` 제거**

```ts
export class RequestMatchmakingDto {
    @IsNumber() public queueId: number;
    @IsNumber() public gameModeId: number;
    @IsNumber() public mapId: number;
}
```

- [ ] **Step 5: 라우트 배선**

```ts
import { authMiddleware } from '@lop/server-core/auth';
...
        this.router.post(`${this.path}`, authMiddleware, validationMiddleware(RequestMatchmakingDto, 'body'), this.matchmakingController.requestMatchmaking);
        this.router.delete(`${this.path}/:ticketId`, authMiddleware, this.matchmakingController.cancelMatchmaking);
```

- [ ] **Step 6: 컨트롤러·서비스 시그니처**

컨트롤러:

```ts
            const response = await this.matchmakingService.requestMatchmaking(req.userId!, req.body);
```

서비스: `public async requestMatchmaking(userId: string, requestMatchmakingDto: RequestMatchmakingDto)`로 바꾸고
`requestMatchmakingDto.userId` 참조 **4곳**(`matchmaking.service.ts` 22·65·75·88행)을 `userId`로 교체한다.

- [ ] **Step 7: 기존 단위 테스트 호출부 수정**

`src/services/__tests__/matchmaking.service.request.test.ts`가 `service.requestMatchmaking(요청 as any)`로
부른다. `service.requestMatchmaking('<테스트 유저 id>', 요청 as any)` 형태로 바꾼다. **단언은 바꾸지 않는다** —
바꿔야 한다면 그건 동작이 변한 것이므로 보고할 것.

- [ ] **Step 8: `main.ts`에 환경변수 요구 추가**

```ts
        //  이 앱도 토큰을 직접 검증하므로 서명키가 필요하다. 공유 validateEnv에 넣지 않는 이유는
        //  그 값을 안 쓰는 room-server가 부팅 즉사하기 때문(과거에 낸 회귀).
        validateEnv({ AUTH_JWT_SECRET: str() });
```

`director.ts`는 HTTP 라우트를 서빙하지 않으므로 **건드리지 않는다.**

- [ ] **Step 9: 통과 확인**

```bash
pnpm --filter matchmaking-server test
pnpm --filter matchmaking-server test:integration
```

- [ ] **Step 10: 커밋** (경로 명시, `pnpm-lock.yaml` 포함)

```
feat(matchmaking): 매칭 요청·취소에 토큰 검사 + 본문 userId 제거

클라가 본문에 자기 신원을 실어 보내고 서버가 그걸 믿던 구조를 없앤다. 남겨두고
무시하면 API가 거짓말을 하게 되고, 남겨두고 비교하면 검사 지점이 둘로 늘어난다.

matchmaking-server도 토큰을 직접 검증하므로 AUTH_JWT_SECRET을 요구한다. 공유
validateEnv가 아니라 이 앱에서만 요구한다 — 공유에 넣으면 그 값을 안 쓰는
room-server가 부팅 즉사한다.
```

---

## Task 4: 대기표 소유권 확인

**Files:**
- Modify: `apps/matchmaking-server/src/controllers/matchmaking.controller.ts`, `src/services/matchmaking.service.ts`
- Modify (호출부 수정): `src/services/__tests__/matchmaking.service.cancel.test.ts`
- Modify: `apps/matchmaking-server/test/integration/matchmakingAuth.integration.test.ts`

**Interfaces:**
- Consumes: Task 3의 라우트 배선
- Produces: `MatchmakingService.cancelMatchmaking(ticketId: string, requesterId: string)`

**배경:** 취소는 `ticketId`만 받는다. uuid라 추측은 어렵지만, 매치가 잡히면 참가자들이 서로의 정보를 보게 되므로 **알아낼 수 있는 값**이다. 토큰만으로는 "유효한 내 토큰 + 남의 티켓"을 막지 못한다.

- [ ] **Step 1: 실패하는 테스트 추가**

`matchmakingAuth.integration.test.ts`의 `DELETE` describe에 추가한다. 티켓은 DB에 직접 넣는다
(발급 경로는 로비를 부르므로 여기서 못 쓴다). 스키마는 `packages/database/prisma/schema.prisma`의
`MatchmakingTicket`(125행) — `id`/`userIds`/`queueId`/`gameModeIds`/`mapIds`/`rating`이 필수이고
`matchId`는 선택이다:

```ts
    //  403을 주면서 일은 벌어지는 것이 이 슬라이스에서 가장 위험한 실패 모양이다
    //  (미들웨어를 핸들러 뒤에 배선했을 때 나타난다). 그래서 티켓이 남아 있는지까지 본다.
    it('남의 대기표는 취소되지 않는다 (403, 티켓 유지)', async () => {
        const 남의_티켓 = await rawPrisma.matchmakingTicket.create({
            data: {
                id: 'ticket-of-someone-else',
                userIds: ['someone-else'],
                queueId: 1,
                gameModeIds: [1],
                mapIds: [1],
                rating: 1000,
            },
        });
        const 내_토큰 = signAccessToken('me');

        const response = await request(app)
            .delete(`/matchmaking/${남의_티켓.id}`)
            .set('Authorization', `Bearer ${내_토큰}`)
            .send();

        expect(response.status).toBe(403);

        const 남아있는_티켓 = await rawPrisma.matchmakingTicket.findUnique({ where: { id: 남의_티켓.id } });
        expect(남아있는_티켓).not.toBeNull();
    });
```

- [ ] **Step 2: 실패 확인** — 403이 아니라 다른 코드가 나온다.

- [ ] **Step 3: 구현**

컨트롤러:

```ts
            const response = await this.matchmakingService.cancelMatchmaking(req.params.ticketId, req.userId!);
```

서비스 — 티켓 조회 **직후**, 다른 어떤 처리보다 먼저:

```ts
            //  남의 대기표는 "없는 것"이 아니라 "권한 없음"이다. ticketId는 uuid라 존재 여부를 숨겨
            //  얻을 실익이 없고, requireSelf와 같은 어휘(403)를 쓰는 편이 일관된다.
            if (matchmakingTicket.userIds.includes(requesterId) === false) {
                throw new HttpException(403, 'Forbidden.');
            }
```

`HttpException`은 `@lop/server-core`에서 가져온다(정확한 서브패스는 기존 import를 따를 것).
컨트롤러의 `catch (error) { next(error); }`가 `errorMiddleware`로 넘겨 403 `{ message }`가 나간다.

- [ ] **Step 4: 기존 단위 테스트 호출부 수정**

`matchmaking.service.cancel.test.ts`가 `cancelMatchmaking('t1')`로 부르는 곳이 **6군데**다. 두 번째 인자로
그 테스트의 티켓 소유자와 같은 id를 넘겨 기존 단언이 그대로 성립하게 한다. **단언은 바꾸지 않는다.**

- [ ] **Step 5: 통과 확인**

```bash
pnpm --filter matchmaking-server test
pnpm --filter matchmaking-server test:integration
```

- [ ] **Step 6: 커밋**

```
feat(matchmaking): 대기표 취소에 소유권 확인

토큰만으로는 "유효한 내 토큰 + 남의 티켓"을 막지 못한다. 티켓 조회 직후, 다른 처리보다
먼저 소유자와 대조한다. 새 ResponseCode를 만들지 않고 403으로 낸다 — ResponseCode는
Unity 클라의 C# enum과 값이 같아야 하는 와이어 계약이라 한쪽만 늘리면 조용히 어긋난다.
```

---

## Task 5: 클라 — 요청 본문에서 `userId` 제거

**Files:**
- Modify: `Assets/Scripts/WebAPI/Dto/Request/MatchmakingRequest.cs`
- Modify: `MatchmakingRequest`를 채우는 호출부 (`Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs` 추정 — `grep`으로 확인할 것)

**Interfaces:**
- Consumes: Task 3의 DTO 변경
- Produces: (없음)

**⚠️ 이 태스크는 Task 3과 함께 배포돼야 한다.** 백엔드가 먼저 올라가면 `userId`를 보내는 클라의 매칭이 전부 400으로 죽는다(`forbidNonWhitelisted`).

- [ ] **Step 1: 필드 삭제**

```csharp
namespace LOP
{
    public class MatchmakingRequest
    {
        public int queueId;
        public int gameModeId;
        public int mapId;
    }
}
```

- [ ] **Step 2: 채우던 호출부에서 그 줄 삭제**

`grep -rn "MatchmakingRequest" Assets/Scripts`로 찾아 `userId = ...` 줄을 지운다.

- [ ] **Step 3: 컴파일 확인**

UnityMCP로 `refresh_unity`(`compile="request"`, `wait_for_ready=true`) → `read_console`(`types=["error"]`)이 비어야 한다.
`unity_instance`를 **매 호출에 명시**한다(`mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 전체 id).

- [ ] **Step 4: 커밋** (경로 명시 — 다른 작업의 미추적 파일을 절대 담지 말 것)

```
refactor(matchmaking): 요청 본문에서 userId 제거

서버가 토큰의 신원을 쓰므로 클라가 자기 이름을 신고할 필요가 없어졌다.
```

---

## Task 6: 서명키를 k8s Secret으로

**Files:**
- Modify: `infrastructure/k8s/apps/backend/lobby-server/lobby-server-deployment.yaml`
- Modify: `infrastructure/k8s/apps/backend/matchmaking-server/matchmaking-server-deployment.yaml`
- Modify: `lop-backend/apps/lobby-server/.env.development.local-k8s`
- Create/Modify: `infrastructure` 문서 — `kubectl create secret` 명령 기록 (기존 로컬 k8s 문서를 찾아 거기에 붙일 것)

**Interfaces:**
- Consumes: Task 3(매칭이 `AUTH_JWT_SECRET`을 요구하게 됨)
- Produces: (없음)

**배경:** 지금 서명키는 커밋된 `.env`를 통해 **도커 이미지에 구워진다.** 값 자체는 자리표시자라 당장의 노출 피해는 없지만, 구조가 문제다 — 환경마다 다른 키를 쓰려면 이미지를 다시 빌드해야 하고, 이미지를 받을 수 있는 사람은 값을 꺼낼 수 있다.

- [ ] **Step 1: Secret 생성 (수기, 1회)**

```bash
kubectl create secret generic auth-secret \
  --from-literal=AUTH_JWT_SECRET='local-dev-only-CHANGE-ME-not-a-real-secret'
```

**매니페스트를 git에 만들지 않는다.** base64는 암호화가 아니다. ArgoCD는 `prune: true`지만 자기가 만든
리소스만 지우므로 이 Secret은 대상이 아니다.

- [ ] **Step 2: 두 deployment에 `secretRef` 추가**

```yaml
        envFrom:
        - configMapRef:
            name: lobby-server-config      # matchmaking은 matchmaking-server-config
        - secretRef:
            name: postgres-secret
        - secretRef:
            name: auth-secret
```

- [ ] **Step 3: 커밋된 `.env`에서 키 제거**

`lop-backend/apps/lobby-server/.env.development.local-k8s`에서 `AUTH_JWT_SECRET` 줄을 지운다.
**`.env.development.local`(로컬 개발용)은 그대로 둔다** — 그쪽은 k8s를 안 쓰므로 거기서 읽어야 한다.

`dotenv`는 기본이 `override: false`라 k8s가 준 환경변수가 이긴다.

- [ ] **Step 4: 문서화**

`infrastructure`의 로컬 k8s 안내 문서를 찾아(`docs/` 또는 `k8s/local-k8s/README`) Step 1의 명령을
**클러스터 재구축 시 필요한 수기 단계**로 기록한다. 왜 매니페스트를 git에 두지 않는지도 한 줄 남긴다
(base64는 암호화가 아님, 정석은 SealedSecrets/ESO/SOPS).

- [ ] **Step 5: 배포 확인**

```bash
kubectl rollout restart deploy/lobby-server deploy/matchmaking-server
kubectl get pods
kubectl logs deploy/matchmaking-server --tail=20
```

Expected: 두 파드 모두 `Running`. 매칭이 `AUTH_JWT_SECRET is not set`으로 죽으면 Secret이 안 붙은 것이다.

- [ ] **Step 6: 커밋** (두 저장소 각각)

```
chore(k8s): AUTH_JWT_SECRET을 이미지에서 Secret으로

값이 이미지에 구워져 있으면 환경마다 다른 키를 쓰려고 이미지를 다시 빌드해야 하고,
이미지를 받을 수 있는 사람은 값을 꺼낼 수 있다. 앱 코드는 process.env 그대로 —
검증 주체도 읽는 방식도 안 바뀌고 값의 출처만 옮긴다.

매니페스트는 git에 두지 않는다. base64는 암호화가 아니다. 정석(SealedSecrets/ESO/SOPS)
도입은 postgres-secret과 함께 별도 슬라이스.
```

---

## 수동 검증 (사람이 한다)

로컬 k8s와 Unity 에디터가 필요하다.

- [ ] **1. 정상 플레이** — 로그인 → 로비 진입 → 매칭 요청 → 취소가 이전과 동일. (매칭 성공 경로는
      통합 테스트가 못 덮는 부분이라 여기가 유일한 확인처다.)
- [ ] **2. 401 재시도 경로 — 1a의 핵심을 처음 실제로 밟는다.** 클라에서 토큰을 강제로 만료시켜
      (`AccessTokenInfo`의 만료 시각을 과거로) 요청을 보내면 401 → 갱신 → 재전송으로 **성공**해야 한다.
- [ ] **3. 레이트리밋** — `/auth/login`을 빠르게 반복 호출해 **429**가 나오는지 확인하고, 그 뒤 앱을
      재시작해도 **계정이 유지되는지**(401이었다면 지워졌을 것) 확인한다.
- [ ] **4. 서명키 일치** — 매칭 요청이 401 없이 통과하면 로비·매칭이 같은 키를 보고 있다는 뜻이다.

---

## 완료 조건

- [ ] `pnpm --filter lobby-server test` / `test:integration` 전부 통과
- [ ] `pnpm --filter matchmaking-server test` / `test:integration` 전부 통과
- [ ] 클라 컴파일 `error CS` 0
- [ ] 수동 검증 4건 통과
- [ ] 세 저장소 커밋에 다른 작업의 파일이 섞이지 않았다 (`git show --stat`으로 확인)

---

## Task 7: 고아 유저 변경 라우트 삭제 + 레이트리밋 분리 (최종 리뷰 후속)

**Files:**
- Modify: `apps/lobby-server/src/routes/user.route.ts`, `src/controllers/user.controller.ts`, `src/services/user.service.ts`, `src/dtos/user.dto.ts`
- Delete: `apps/lobby-server/src/routes/user-profile.route.ts` + 그 컨트롤러·서비스·DTO
- Modify: `apps/lobby-server/src/main.ts` (`UserProfileRoute` 등록 제거)
- Modify: `apps/lobby-server/src/middlewares/authRateLimit.ts`, `src/routes/auth.route.ts`
- Modify: `apps/matchmaking-server/src/dtos/matchmaking.dto.ts` (미사용 import)
- Test: `apps/lobby-server/test/integration/orphanRoutes.integration.test.ts` (신규)

**배경 — 최종 리뷰가 찾은 Critical**

경계 전체를 훑으니 `DELETE /user/:id`가 **인증 없이 인터넷에서 도달 가능**했다(리뷰어가 돌아가는
클러스터에서 없는 id로 500을 받아 핸들러 진입을 실증). `GET /user/all`이 전체 계정 목록을 그대로
주므로 userId를 미리 알 필요도 없다.

계정을 지우면 `UserIdentity`에 FK가 없어 깔끔히 지워지고, 피해자가 재실행하면 남은 신원으로 로그인이
401 → 클라가 "자격증명 거부"로 읽고 **저장된 계정을 지우고 새로 가입**한다. 인증 없는 루프 하나로
**DB의 모든 플레이어가 영구 계정 유실**이다.

`POST /user`(무제한 계정 생성, 새 레이트리밋 **우회**)와 `PUT /user/profile`(본문 userId로 남의
닉네임 변경)도 같은 부류다.

셋 다 **호출자가 0곳**이다 — 라우트 자신 말고는 참조가 없다(grep 확인). 우리가 §7에서 `leave`를
지운 논리가 그대로 적용된다: 쓰지 않는 변경 라우트를 인터넷에 열어둘 이유가 없다. **인증을 붙이는
대신 지운다** — 필요해지면 그때 인증을 갖춰 다시 만든다.

- [ ] **Step 1: 실패하는 테스트 작성**

`apps/lobby-server/test/integration/orphanRoutes.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import AuthRoute from '@routes/auth.route';
import UserRoute from '@routes/user.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new AuthRoute(), new UserRoute()]).getServer();

let testIp = 0;
function 다른_ip로() {
    testIp += 1;
    return `198.51.100.${testIp}`;
}

//  호출자가 0곳인 변경 라우트가 인터넷에 열려 있으면, 인증 없이 남의 계정을 지우거나 만들 수 있다.
//  실제로 DELETE /user/:id가 그런 상태였다.
describe('고아 유저 변경 라우트', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('DELETE /user/:id 는 존재하지 않는다', async () => {
        const 계정 = await request(app).post('/auth/anonymous').set('X-Forwarded-For', 다른_ip로()).send();

        const response = await request(app).delete(`/user/${계정.body.userId}`).send();

        expect(response.status).toBe(404);
        //  라우트가 사라졌어도 계정은 멀쩡해야 한다.
        expect(await rawPrisma.user.count()).toBe(1);
    });

    it('POST /user 는 존재하지 않는다', async () => {
        const response = await request(app).post('/user').send({ username: 'someone' });

        expect(response.status).toBe(404);
        expect(await rawPrisma.user.count()).toBe(0);
    });

    it('PUT /user/profile 은 존재하지 않는다', async () => {
        const response = await request(app).put('/user/profile').send({ userId: 'someone', nickname: '남의닉' });

        expect(response.status).toBe(404);
    });
});
```

> `PUT /user/profile`은 `UserProfileRoute`가 통째로 사라지므로 위 `App`에 등록하지 않아도 404가 맞다.

- [ ] **Step 2: 실패 확인**

```bash
pnpm --filter lobby-server test:integration -- orphanRoutes
```

Expected: `DELETE`는 200/500, `POST`는 201/400 등 404가 아닌 응답으로 실패.

- [ ] **Step 3: 삭제**

지울 것 — 각각 라우트 → 컨트롤러 메서드 → 서비스 메서드 → 전용 DTO 순으로:

| 라우트 | 컨트롤러 | 서비스 |
|---|---|---|
| `POST /user` | `UserController.createUser` | `UserService.createUser`(+ 쓰이지 않으면 `createUsers`) |
| `DELETE /user/:id` | `UserController.deleteUser` | `UserService.deleteUser`/`deleteUserById` |
| `PUT /user/profile` | `UserProfileController` 전체 | `UserProfileService`의 해당 메서드 |

`main.ts`에서 `UserProfileRoute` import·등록을 제거한다. `CreateUserDto`·`UpdateUserProfileDto`와
`UserMapper.CreateUserDto`도 참조가 사라지면 함께 지운다.

**남기는 것**: 조회 라우트는 전부 그대로 둔다(스펙 §1의 범위 밖 — 내부 서비스가 쓴다).
`GET /user/username/:username`도 호출자가 없지만 조회라 이번 범위에 넣지 않는다.

삭제 후 확인:

```bash
grep -rn "createUser\|deleteUser\|updateUserProfile\|CreateUserDto\|UpdateUserProfileDto" apps/lobby-server/src
```

- [ ] **Step 4: 레이트리밋 분리 + 재조정**

현재 리미터 **인스턴스 하나**를 `/auth/anonymous`와 `/auth/login`이 공유해, 스펙 표의 "각각 20회"와
달리 **합쳐서 20회**다. 그리고 갱신이 `/auth/login`을 반복 호출하므로 공유 NAT 뒤에서는 정상
사용자가 잠긴다.

`authRateLimit.ts`를 둘로 나눈다:

```ts
import rateLimit from 'express-rate-limit';

//  bcrypt가 libuv 스레드풀(기본 4개)에서 도는 탓에, 로그인이 몰리면 같은 풀을 쓰는 파일 I/O·DNS·gzip이
//  전부 밀려 서버 전체가 느려진다. 포화는 초당 40회쯤부터 — 15분으로 환산하면 36,000회다. 아래 한도는
//  거기서 한참 아래라 보호는 충분하고, 정상 사용자를 막지 않는 쪽에 여유를 뒀다.

//  계정 생성은 남용 통로(신규 계정 보상이 생기면 특히)라 더 조인다.
export const anonymousRateLimit = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: 30,
    standardHeaders: 'draft-7',
    legacyHeaders: false,
});

//  로그인은 앱 시작마다 + 토큰 갱신마다 불린다. 갱신 최소 간격이 30초라 클라 한 대만도 15분에
//  30회까지 나올 수 있고, 공유 NAT(모바일 캐리어·사무실) 뒤에는 그런 클라가 여럿이다.
export const loginRateLimit = rateLimit({
    windowMs: 15 * 60 * 1000,
    limit: 200,
    standardHeaders: 'draft-7',
    legacyHeaders: false,
});
```

`auth.route.ts`에서 각각 해당 라우트에 붙인다.

> **알려진 한계**: IP로 세는 한 CGNAT 뒤 대규모 공유는 근본적으로 못 가른다. 로그인은 본문에
> `provider`+`providerUserId`가 있으므로 **계정 단위 키**로 바꾸는 것이 정공법이다 — 별도 후속.

기존 레이트리밋 테스트의 `LIMIT` 상수와 대상 엔드포인트를 새 값에 맞춘다. **429가 나오는지와
401이 아닌지를 확인하는 단언은 그대로 둔다.**

- [ ] **Step 5: 미사용 import 정리**

`apps/matchmaking-server/src/dtos/matchmaking.dto.ts`의 첫 줄을 `import { IsNumber } from 'class-validator';`로.
(`IsString`은 이번 브랜치가 미사용으로 만들었고, `IsEnum`/`IsObject`는 그 전부터 미사용이었다.)

- [ ] **Step 6: 통과 확인**

```bash
pnpm --filter lobby-server test && pnpm --filter lobby-server test:integration
pnpm --filter matchmaking-server test && pnpm --filter matchmaking-server test:integration
```

- [ ] **Step 7: 커밋** (경로 명시)

```
fix(lobby): 인증 없이 계정을 지울 수 있던 고아 라우트 3종 삭제

DELETE /user/:id가 인증 없이 인터넷에서 도달 가능했다. GET /user/all이 전체 계정
목록을 주므로 userId를 미리 알 필요도 없고, 계정을 지우면 피해자는 재실행 시 로그인이
401 → 클라가 자격증명을 지우고 새로 가입한다. 인증 없는 루프 하나로 전 플레이어
영구 계정 유실이다.

POST /user(무제한 계정 생성, 레이트리밋 우회)와 PUT /user/profile(본문 userId로 남의
닉네임 변경)도 같은 부류다. 셋 다 호출자가 0곳이라 인증을 붙이는 대신 지운다.

레이트리밋도 분리했다 — 인스턴스 하나를 두 엔드포인트가 공유해 합쳐서 20회였고,
갱신이 로그인을 반복 호출하므로 공유 NAT 뒤 정상 사용자가 잠겼다.
```
