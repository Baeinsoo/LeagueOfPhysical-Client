# 인증 cutover 1c — 방 접속 인증 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임서버가 로비에 introspect로 토큰을 확인해야만 방 접속을 허용하고, 인게임 메시지의 신원을 클라 주장값이 아니라 연결에서 도출하도록 바꾼다.

**Architecture:** 로비에 RFC 7662 introspection 엔드포인트를 신설하고, 게임서버 파드에는 *조회 전용* 키(`INTERNAL_API_KEY`)만 별도 Secret으로 준다. 게임서버는 `명단 대조 → introspect → sub 일치` 순서로 판정하고, 확인에 실패하면 전부 거부한다. 접속 후에는 `conn.authenticationData`에 저장된 `sub`을 유일한 신원으로 삼는다.

**Tech Stack:** Express + class-validator + jest/supertest/testcontainers (백엔드) · Unity 2022 + Mirror + VContainer + MessagePipe + UniTask (클·서) · kustomize (인프라)

**Spec:** `LeagueOfPhysical-Client/docs/superpowers/specs/2026-08-09-auth-cutover-1c-room-connection-auth-design.md`

## Global Constraints

- 환경변수 이름은 **`INTERNAL_API_KEY`**, HTTP 헤더는 **`X-Internal-Api-Key`**, k8s Secret 이름은 **`internal-api-secret`** — 세 값 모두 이 표기 그대로.
- introspect 응답: 토큰이 가짜/만료여도 **HTTP 200 + `{ "active": false }`**. **401은 호출자 키가 틀렸을 때만.**
- `active: false` 응답에는 `sub`/`exp`를 **넣지 않는다**(토큰 상태 유출 방지).
- 게임서버 파드는 `envFrom`이 아니라 **`secretKeyRef`로 `INTERNAL_API_KEY` 하나만** 주입한다 — `auth-secret`(서명키)을 파드에 노출하면 안 된다.
- 게임서버 판정 순서 고정: **① 중복 요청 무시 → ② 명단 대조 → ③(에디터면 여기서 수락) → ④ 키 존재 확인 → ⑤ introspect → ⑥ `active && sub == 주장한 userId`**.
- introspect 실패·타임아웃·`active:false`·`sub` 불일치는 **전부 거부**(fail closed). 타임아웃 **3초**.
- 서버는 `conn.authenticationData`의 `userId`에 **introspect가 돌려준 `sub`** 을 저장한다(클라 주장값 아님). 에디터 경로에서만 주장값을 쓴다.
- 클라는 **`StartClient()` 호출 전에** 토큰 갱신을 끝낸다. `GetAccessTokenAsync(forceRefresh: **false**, ...)` — 강제 갱신을 쓰지 않는다.
- 필드명은 `CustomProperties.token` → **`accessToken`** (클·서 양쪽 동일).
- proto 재생성은 **`compile_protos.sh`만** 돌린다. `generate_protos.sh`는 출력 폴더를 `rm -rf`로 지워 `.meta`가 전부 새 GUID로 재생성되므로 쓰지 않는다.
- Unity 프로젝트(클·서)에는 **asmdef가 없어 유닛 테스트를 붙일 수 없다** — 모든 앱 코드가 `Assembly-CSharp`에 있고 테스트 asmdef는 이를 참조할 수 없다. Unity 측 검증은 **컴파일 클린 + 수동 검증**이다. 없는 테스트를 지어내지 말 것.
- 새 `.cs`/`.asset` 파일을 만들거나 지우면 Unity가 만든 **`.meta` 파일을 반드시 함께 커밋**한다.
- UnityMCP 호출은 **매 호출에 `unity_instance`를 명시**한다(`mcpforunity://instances`에서 이름으로 id 해석). 서버 인스턴스를 임의로 건드리지 않는다.
- 커밋은 `git add <명시 경로>`로만 한다. `git add -A`/디렉터리 통째 추가 금지 — 무관한 작업 중 파일이 딸려 들어간다.

---

## File Structure

**lop-backend**
- 신규 `packages/server-core/src/middlewares/internalApiKey.middleware.ts` — 조회 키 검사 미들웨어
- 신규 `packages/server-core/src/middlewares/__tests__/internalApiKey.middleware.test.ts`
- 수정 `packages/server-core/src/entries/express.ts` — 미들웨어 export
- 수정 `packages/server-core/src/auth/token.ts` — `AccessTokenPayload`에 `exp` 추가
- 수정 `packages/server-core/src/auth/__tests__/token.test.ts` — 위 변경 반영
- 수정 `apps/lobby-server/src/dtos/auth.dto.ts` — introspect 요청/응답 DTO
- 수정 `apps/lobby-server/src/services/auth/auth.service.ts` — `introspect`
- 수정 `apps/lobby-server/src/controllers/auth.controller.ts` — `introspect`
- 수정 `apps/lobby-server/src/routes/auth.route.ts` — 라우트 등록
- 수정 `apps/lobby-server/src/main.ts` — `validateEnv`에 `INTERNAL_API_KEY`
- 신규 `apps/lobby-server/test/integration/introspect.integration.test.ts`
- 신규 `apps/room-server/src/services/gameServerPod.ts` — 파드 매니페스트 생성(순수 함수)
- 신규 `apps/room-server/src/services/__tests__/gameServerPod.test.ts`
- 수정 `apps/room-server/src/services/room.service.ts` — 위 함수 사용

**infrastructure**
- 수정 `k8s/apps/backend/lobby-server/lobby-server-deployment.yaml`
- 수정 `README.md`

**LeagueOfPhysical-Server**
- 수정 `Assets/Scripts/Room/CustomProperties.cs` — 필드 rename
- 신규 `Assets/Scripts/WebAPI/Dto/Response/IntrospectResponse.cs`
- 수정 `Assets/Scripts/WebAPI/WebAPI.cs` — `Introspect`
- 수정 `Assets/Scripts/Room/LOPNetworkAuthenticator.cs` — 비동기 판정
- 신규 `Assets/Scripts/Network/ClientMessage.cs` — 신원 + 메시지 봉투
- 수정 `Assets/Scripts/Network/NetworkMessageDispatcher.cs`
- 수정 `Assets/Scripts/Room/LOPRoom.cs` — 수신부에서 신원 전달
- 수정 `Assets/Scripts/RootLifetimeScope.cs` — 브로커 등록 교체
- 수정 `Assets/Scripts/Game/MessageHandler/{GameInfo,GameInput,GameEntity}MessageHandler.cs`

**LeagueOfPhysical-Client**
- 수정 `Assets/Scripts/Room/CustomProperties.cs` — 필드 rename
- 수정 `Assets/Scripts/Room/LOPNetworkAuthenticator.cs` — 사전 갱신 + 진짜 토큰
- 수정 `Assets/Scripts/Room/LOPRoom.cs` — `StartClient` 전 await
- 수정 `Assets/Scripts/RootLifetimeScope.cs` — `IAccessTokenProvider` 등록
- 수정 `Assets/Scripts/Game/PlayerInputManager.cs`, `Assets/Scripts/UI/Stats/StatsViewModel.cs`

**LeagueOfPhysical-Shared**
- 수정 `Protos/{GameInfoToS,InputCommandToS,StatAllocationToS}.proto` — 주석만(재생성 없음)

**GameFramework**
- 삭제 `Runtime/Scripts/Auth/Jwt.cs`(+`.meta`), `Tests/Runtime/Auth/JwtTests.cs`(+`.meta`)

---

## Task 1: 조회 키 미들웨어 (server-core)

**Repo:** `lop-backend`

**Files:**
- Create: `packages/server-core/src/middlewares/internalApiKey.middleware.ts`
- Create: `packages/server-core/src/middlewares/__tests__/internalApiKey.middleware.test.ts`
- Modify: `packages/server-core/src/entries/express.ts`

**Interfaces:**
- Produces: `export function internalApiKeyMiddleware(req: Request, res: Response, next: NextFunction): void` — `@lop/server-core/express`에서 export. `X-Internal-Api-Key` 헤더를 `process.env.INTERNAL_API_KEY`와 상수시간 비교. 불일치 401, env 미설정 500.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`packages/server-core/src/middlewares/__tests__/internalApiKey.middleware.test.ts`:

```ts
import type { NextFunction, Request, Response } from 'express';
import { internalApiKeyMiddleware } from '../internalApiKey.middleware';

const KEY = 'internal-key-0123456789';

//  express 응답을 흉내 내는 최소 더미. status()가 자기 자신을 돌려줘야 .json() 체이닝이 된다.
function createResponse() {
    const res = {
        statusCode: 0,
        body: undefined as unknown,
        status(code: number) { res.statusCode = code; return res; },
        json(payload: unknown) { res.body = payload; return res; },
    };
    return res as unknown as Response & { statusCode: number; body: unknown };
}

function run(headers: Record<string, string>) {
    const req = { headers } as unknown as Request;
    const res = createResponse();
    const next = jest.fn() as unknown as NextFunction;
    internalApiKeyMiddleware(req, res, next);
    return { res, next };
}

describe('internalApiKeyMiddleware', () => {
    beforeEach(() => { process.env.INTERNAL_API_KEY = KEY; });

    it('키가 맞으면 통과시킨다', () => {
        const { next } = run({ 'x-internal-api-key': KEY });
        expect(next).toHaveBeenCalled();
    });

    it('헤더가 없으면 401', () => {
        const { res, next } = run({});
        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    //  KEY와 "같은 길이, 다른 내용"이어야 한다 — 길이가 다르면 timingSafeEqual에 닿기 전에
    //  길이 가드에서 걸러져, 상수시간 비교가 통째로 빠져도 이 테스트가 통과해 버린다.
    it('키가 틀리면 401', () => {
        const { res, next } = run({ 'x-internal-api-key': 'internal-key-9999999999' });
        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    //  길이가 다르면 timingSafeEqual이 던진다 — 던지지 않고 401로 닫혀야 한다.
    it('키 길이가 달라도 던지지 않고 401', () => {
        const { res, next } = run({ 'x-internal-api-key': 'short' });
        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    //  설정 오류를 "호출자 잘못(401)"으로 돌려주면 운영 중 오진한다.
    it('INTERNAL_API_KEY 미설정이면 500', () => {
        delete process.env.INTERNAL_API_KEY;
        const { res, next } = run({ 'x-internal-api-key': KEY });
        expect(res.statusCode).toBe(500);
        expect(next).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend/packages/server-core && npx jest src/middlewares/__tests__/internalApiKey.middleware.test.ts
```
기대: `Cannot find module '../internalApiKey.middleware'`로 실패.

- [ ] **Step 3: 미들웨어를 구현한다**

`packages/server-core/src/middlewares/internalApiKey.middleware.ts`:

```ts
import { timingSafeEqual } from 'crypto';
import { NextFunction, Request, Response } from 'express';
import { logger } from '../utils/logger';

const HEADER_NAME = 'x-internal-api-key';

//  일반 문자열 비교(===)는 처음 다른 글자에서 바로 멈춘다 — 그 응답 시간 차이로 키를 한 글자씩
//  알아낼 수 있다. 길이만 먼저 보고, 내용은 끝까지 훑어 비교한다.
function isSameKey(provided: string, expected: string): boolean {
    const a = Buffer.from(provided);
    const b = Buffer.from(expected);

    if (a.length !== b.length) {
        return false;
    }

    return timingSafeEqual(a, b);
}

export function internalApiKeyMiddleware(req: Request, res: Response, next: NextFunction): void {
    const expected = process.env.INTERNAL_API_KEY;

    if (!expected) {
        //  호출자 잘못이 아니라 서버 설정 오류다. 401로 돌려주면 운영 중 "게임서버 키가 틀렸다"로
        //  오진하게 된다.
        logger.error('[AUTH] INTERNAL_API_KEY is not set.');
        res.status(500).json({ message: 'Internal server error.' });
        return;
    }

    const provided = req.headers[HEADER_NAME];

    if (typeof provided !== 'string' || isSameKey(provided, expected) === false) {
        res.status(401).json({ message: 'Invalid internal API key.' });
        return;
    }

    next();
}
```

- [ ] **Step 4: export를 추가한다**

`packages/server-core/src/entries/express.ts`에 한 줄 추가:

```ts
export { internalApiKeyMiddleware } from '../middlewares/internalApiKey.middleware';
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
cd lop-backend/packages/server-core && npx jest src/middlewares/__tests__/internalApiKey.middleware.test.ts
```
기대: 5개 PASS.

- [ ] **Step 6: 전체 빌드가 깨지지 않는지 확인한다**

```bash
cd lop-backend && npx turbo run build
```
기대: 성공. (CI가 테스트보다 빌드를 먼저 돌린다 — 빌드 확인을 생략하지 말 것.)

- [ ] **Step 7: 커밋**

```bash
cd lop-backend
git add packages/server-core/src/middlewares/internalApiKey.middleware.ts \
        packages/server-core/src/middlewares/__tests__/internalApiKey.middleware.test.ts \
        packages/server-core/src/entries/express.ts
git commit -m "feat(server-core): 내부 서비스 조회 키 미들웨어 추가"
```

---

## Task 2: `POST /auth/introspect` (lobby-server)

**Repo:** `lop-backend`

**Files:**
- Modify: `packages/server-core/src/auth/token.ts`
- Modify: `packages/server-core/src/auth/__tests__/token.test.ts`
- Modify: `apps/lobby-server/src/dtos/auth.dto.ts`
- Modify: `apps/lobby-server/src/services/auth/auth.service.ts`
- Modify: `apps/lobby-server/src/controllers/auth.controller.ts`
- Modify: `apps/lobby-server/src/routes/auth.route.ts`
- Modify: `apps/lobby-server/src/main.ts`
- Create: `apps/lobby-server/test/integration/introspect.integration.test.ts`

**Interfaces:**
- Consumes: `internalApiKeyMiddleware` (Task 1), `@lop/server-core/express`에서 import.
- Produces: `POST /auth/introspect` — 요청 `{ token: string }`, 응답 200 `{ active: true, sub, exp }` 또는 200 `{ active: false }`, 조회 키 실패 401, 본문 오류 400.
- Produces: `AccessTokenPayload`에 `exp: number` 추가.

- [ ] **Step 1: 통합 테스트를 쓴다**

`apps/lobby-server/test/integration/introspect.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import { signAccessToken } from '@lop/server-core/auth';
import AuthRoute from '@routes/auth.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new AuthRoute()]).getServer();

const KEY = 'introspect-integration-key';

//  /auth/anonymous는 IP당 레이트리밋이 걸려 있다. 테스트마다 새 IP를 배정해 429를 피한다.
let testIp = 0;

async function 계정을_만든다(): Promise<{ userId: string; accessToken: string }> {
    const response = await request(app)
        .post('/auth/anonymous')
        .set('X-Forwarded-For', `203.0.113.${testIp}`)
        .send();
    return { userId: response.body.userId, accessToken: response.body.accessToken };
}

describe('POST /auth/introspect', () => {
    beforeEach(async () => {
        testIp += 1;
        process.env.INTERNAL_API_KEY = KEY;
        await resetTables();
    });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('조회 키가 없으면 401', async () => {
        const { accessToken } = await 계정을_만든다();

        const response = await request(app).post('/auth/introspect').send({ token: accessToken });

        expect(response.status).toBe(401);
    });

    it('조회 키가 틀리면 401', async () => {
        const { accessToken } = await 계정을_만든다();

        const response = await request(app)
            .post('/auth/introspect')
            .set('X-Internal-Api-Key', 'wrong-key')
            .send({ token: accessToken });

        expect(response.status).toBe(401);
    });

    it('진짜 토큰이면 active:true와 sub을 준다', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app)
            .post('/auth/introspect')
            .set('X-Internal-Api-Key', KEY)
            .send({ token: accessToken });

        expect(response.status).toBe(200);
        expect(response.body.active).toBe(true);
        expect(response.body.sub).toBe(userId);
        expect(typeof response.body.exp).toBe('number');
    });

    //  RFC 7662 — 토큰이 나빠도 401이 아니라 200 + active:false다. 401은 호출자 자격 실패 전용.
    it('위조 토큰이면 200 + active:false', async () => {
        const response = await request(app)
            .post('/auth/introspect')
            .set('X-Internal-Api-Key', KEY)
            .send({ token: 'not.a.real.token' });

        expect(response.status).toBe(200);
        expect(response.body.active).toBe(false);
    });

    //  만료 케이스는 여기서 다루지 않는다 — 만료 판정은 verifyAccessToken의 책임이고
    //  packages/server-core/src/auth/__tests__/token.test.ts:36이 이미 덮는다. 여기서 재현하려면
    //  가짜 타이머가 필요한데, supertest의 실제 HTTP 왕복과 섞으면 요청이 멈춰 설 수 있다.
    //  introspect가 "검증 실패 → active:false"로 변환하는 부분은 바로 위 위조 토큰 케이스가 덮는다.

    //  만료인지 위조인지 알려주면 밖에서 토큰 상태를 떠볼 수 있다.
    it('active:false 응답에는 sub/exp가 없다', async () => {
        const response = await request(app)
            .post('/auth/introspect')
            .set('X-Internal-Api-Key', KEY)
            .send({ token: 'not.a.real.token' });

        expect(response.body.sub).toBeUndefined();
        expect(response.body.exp).toBeUndefined();
    });

    it('token 필드가 없으면 400', async () => {
        const response = await request(app)
            .post('/auth/introspect')
            .set('X-Internal-Api-Key', KEY)
            .send({});

        expect(response.status).toBe(400);
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend/apps/lobby-server && npx jest -c jest.integration.config.js introspect
```
기대: 404(라우트 없음) 때문에 실패.

- [ ] **Step 3: `verifyAccessToken`이 `exp`도 돌려주게 한다**

`packages/server-core/src/auth/token.ts`:

```ts
export interface AccessTokenPayload {
    userId: string;
    exp: number;
}
```

`verifyAccessToken` 본문의 검사와 반환을 바꾼다:

```ts
        if (typeof decoded.sub !== 'string' || typeof decoded.exp !== 'number') {
            return null;
        }

        return { userId: decoded.sub, exp: decoded.exp };
```

- [ ] **Step 4: 기존 토큰 테스트를 맞춘다**

`packages/server-core/src/auth/__tests__/token.test.ts:10`이 `toEqual({ userId: 'user-1' })`로 정확 일치를 본다. `exp`가 늘었으므로 고친다:

```ts
        expect(verifyAccessToken(token, SECRET)).toEqual({ userId: 'user-1', exp: expect.any(Number) });
```

- [ ] **Step 5: DTO를 추가한다**

`apps/lobby-server/src/dtos/auth.dto.ts` 끝에 추가:

```ts
export class IntrospectRequestDto {
    @IsString()
    public token: string;
}

//  RFC 7662 — 토큰이 유효하지 않으면 active만 담고 나머지는 비운다.
export class IntrospectResponseDto {
    public active: boolean;
    public sub?: string;
    public exp?: number;
}
```

- [ ] **Step 6: 서비스에 introspect를 넣는다**

`apps/lobby-server/src/services/auth/auth.service.ts` 상단 import에 `verifyAccessToken`, DTO를 더하고 클래스에 메서드를 추가한다:

```ts
    //  RFC 7662. 토큰이 나쁜 것은 "호출자 잘못"이 아니므로 예외를 던지지 않고 active:false를 돌려준다.
    public introspect(dto: IntrospectRequestDto): IntrospectResponseDto {
        const payload = verifyAccessToken(dto.token);

        if (payload === null) {
            return { active: false };
        }

        return { active: true, sub: payload.userId, exp: payload.exp };
    }
```

- [ ] **Step 7: 컨트롤러에 추가한다**

`apps/lobby-server/src/controllers/auth.controller.ts`의 클래스 안에:

```ts
    public introspect = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const response = this.authService.introspect(req.body as IntrospectRequestDto);
            res.status(200).json(response);
        } catch (error) {
            next(error);
        }
    };
```

`IntrospectRequestDto`를 `@dtos/auth.dto` import에 더한다.

- [ ] **Step 8: 라우트를 등록한다**

`apps/lobby-server/src/routes/auth.route.ts`의 `initializeRoutes`에 추가하고, import에 `internalApiKeyMiddleware`와 `IntrospectRequestDto`를 더한다:

```ts
        //  게임서버 전용 — 조회 키를 요구한다. 열어 두면 훔친 토큰의 유효성을 밖에서 확인할 수 있다.
        //  레이트리밋은 걸지 않는다: 호출자가 키로 이미 제한되고, 방 하나의 접속 폭주가 자기 입장을 막게 된다.
        this.router.post(
            `${this.path}/introspect`,
            internalApiKeyMiddleware,
            validationMiddleware(IntrospectRequestDto, 'body'),
            this.authController.introspect,
        );
```

- [ ] **Step 9: 기동 시 키를 요구한다**

`apps/lobby-server/src/main.ts`:

```ts
        //  이 앱은 JWT를 직접 발급/검증하고, 게임서버의 introspect 호출을 인증한다.
        validateEnv({ AUTH_JWT_SECRET: str(), INTERNAL_API_KEY: str() });
```

- [ ] **Step 10: 로컬 env에 값을 넣는다**

`apps/lobby-server/.env.development.local`의 `# AUTH` 절, `AUTH_JWT_SECRET` 줄 바로 아래에 추가한다.
이 파일은 `키 = 값`(등호 양옆 공백) 형식을 쓴다:

```
INTERNAL_API_KEY = local-dev-only-CHANGE-ME-not-a-real-key
```

`apps/lobby-server/.env.development.local-k8s`에는 **값을 넣지 않는다** — 이 파일은 이미
`AUTH_JWT_SECRET`을 일부러 비워 두고 k8s Secret이 주입하게 되어 있다. 같은 절의 주석만 한 줄 늘린다:

```
# k8s Secret(auth-secret)이 AUTH_JWT_SECRET을, internal-api-secret이 INTERNAL_API_KEY를 주입한다 —
# 이미지에 값을 굽지 않기 위해 여기서는 뺐다. dotenv는 override: false라 Secret이 이긴다.
```

- [ ] **Step 11: 테스트가 통과하는지 확인한다**

```bash
cd lop-backend && npx turbo run build
cd apps/lobby-server && npx jest -c jest.integration.config.js introspect
cd ../../packages/server-core && npx jest
```
기대: 통합 6건 PASS, server-core 유닛 전부 PASS.

- [ ] **Step 12: 커밋**

```bash
cd lop-backend
git add packages/server-core/src/auth/token.ts \
        packages/server-core/src/auth/__tests__/token.test.ts \
        apps/lobby-server/src/dtos/auth.dto.ts \
        apps/lobby-server/src/services/auth/auth.service.ts \
        apps/lobby-server/src/controllers/auth.controller.ts \
        apps/lobby-server/src/routes/auth.route.ts \
        apps/lobby-server/src/main.ts \
        apps/lobby-server/.env.development.local apps/lobby-server/.env.development.local-k8s \
        apps/lobby-server/test/integration/introspect.integration.test.ts
git commit -m "feat(lobby): RFC 7662 토큰 introspection 엔드포인트"
```

---

## Task 3: 게임서버 파드에 조회 키 주입 (room-server)

**Repo:** `lop-backend`

**Files:**
- Create: `apps/room-server/src/services/gameServerPod.ts`
- Create: `apps/room-server/src/services/__tests__/gameServerPod.test.ts`
- Modify: `apps/room-server/src/services/room.service.ts:143-168`

**Interfaces:**
- Produces: `export function buildGameServerPodManifest(params: { roomId: string; port: number }): object`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`apps/room-server/src/services/__tests__/gameServerPod.test.ts`:

```ts
import { buildGameServerPodManifest } from '../gameServerPod';

describe('buildGameServerPodManifest', () => {
    it('ROOM_ID와 PORT를 값으로 넣는다', () => {
        const manifest = buildGameServerPodManifest({ roomId: 'room-1', port: 7100 }) as any;
        const env = manifest.spec.containers[0].env;

        expect(env).toContainEqual({ name: 'ROOM_ID', value: 'room-1' });
        expect(env).toContainEqual({ name: 'PORT', value: '7100' });
    });

    //  키 "값"이 아니라 "참조"여야 한다 — 룸 서버 프로세스가 키를 만지지 않게.
    it('INTERNAL_API_KEY는 secretKeyRef로 넣는다', () => {
        const manifest = buildGameServerPodManifest({ roomId: 'room-1', port: 7100 }) as any;
        const env = manifest.spec.containers[0].env;

        expect(env).toContainEqual({
            name: 'INTERNAL_API_KEY',
            valueFrom: { secretKeyRef: { name: 'internal-api-secret', key: 'INTERNAL_API_KEY' } },
        });
    });

    //  envFrom으로 Secret을 통째로 붙이면 서명키(auth-secret)까지 파드에 들어갈 위험이 생긴다.
    it('envFrom을 쓰지 않는다', () => {
        const manifest = buildGameServerPodManifest({ roomId: 'room-1', port: 7100 }) as any;

        expect(manifest.spec.containers[0].envFrom).toBeUndefined();
    });

    it('포트를 containerPort와 hostPort 양쪽에 같게 쓴다', () => {
        const manifest = buildGameServerPodManifest({ roomId: 'room-1', port: 7100 }) as any;

        expect(manifest.spec.containers[0].ports).toEqual([{ containerPort: 7100, hostPort: 7100, protocol: 'UDP' }]);
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend/apps/room-server && npx jest src/services/__tests__/gameServerPod.test.ts
```
기대: `Cannot find module '../gameServerPod'`.

- [ ] **Step 3: 함수를 만든다**

`apps/room-server/src/services/gameServerPod.ts`:

```ts
export function buildGameServerPodManifest(params: { roomId: string; port: number }): object {
    const { roomId, port } = params;

    return {
        apiVersion: 'v1',
        kind: 'Pod',
        metadata: {
            name: `room-pod-${roomId}`,
            labels: {
                app: 'room-pod',
                roomId,
            },
        },
        spec: {
            containers: [{
                name: 'game-server',
                image: process.env.GAME_SERVER_IMAGE || 're5nardo/game-server:latest',
                imagePullPolicy: 'Always',
                //  hostPort = containerPort = PORT env (Agones의 Passthrough 정책에 대응).
                //  게임서버가 PORT를 읽어 바인딩하므로 세 값을 같게 두면 변환이 없다.
                ports: [{ containerPort: port, hostPort: port, protocol: 'UDP' }],
                env: [
                    { name: 'ROOM_ID', value: roomId },
                    { name: 'PORT', value: String(port) },
                    //  값이 아니라 참조를 적는다 — 룸 서버 프로세스는 키 값을 만지지 않고, 쿠버네티스가
                    //  파드 기동 시 직접 꽂는다. envFrom으로 Secret을 통째로 붙이면 안 된다(서명키 노출).
                    {
                        name: 'INTERNAL_API_KEY',
                        valueFrom: { secretKeyRef: { name: 'internal-api-secret', key: 'INTERNAL_API_KEY' } },
                    },
                ],
            }],
            terminationGracePeriodSeconds: 30,
        },
    };
}
```

- [ ] **Step 4: room.service.ts가 이 함수를 쓰게 한다**

`apps/room-server/src/services/room.service.ts`에서 인라인 `const podManifest = {...}` 블록(약 143~168행)을 지우고 다음으로 바꾼다. 위쪽의 `//  Pod` 주석과 `await k8sUtils.createPod(podManifest);` 호출은 유지한다.

```ts
            //  Pod
            const podManifest = buildGameServerPodManifest({ roomId: room.id, port });
```

파일 상단 import에 추가:

```ts
import { buildGameServerPodManifest } from './gameServerPod';
```

- [ ] **Step 5: 테스트와 빌드를 확인한다**

```bash
cd lop-backend && npx turbo run build
cd apps/room-server && npx jest
```
기대: 새 4건 포함 전부 PASS, 빌드 성공.

- [ ] **Step 6: 커밋**

```bash
cd lop-backend
git add apps/room-server/src/services/gameServerPod.ts \
        apps/room-server/src/services/__tests__/gameServerPod.test.ts \
        apps/room-server/src/services/room.service.ts
git commit -m "feat(room-server): 게임서버 파드에 조회 키를 secretKeyRef로 주입"
```

---

## Task 4: 조회 키 Secret 배선 (infrastructure)

**Repo:** `infrastructure`

**Files:**
- Modify: `k8s/apps/backend/lobby-server/lobby-server-deployment.yaml`
- Modify: `README.md`

**Interfaces:**
- Consumes: Secret 이름 `internal-api-secret`, 키 `INTERNAL_API_KEY` (Task 3이 파드에서 참조).

- [ ] **Step 1: 로비 Deployment에 Secret을 붙인다**

`k8s/apps/backend/lobby-server/lobby-server-deployment.yaml`의 `envFrom` 목록 끝에 추가한다:

```yaml
        - secretRef:
            name: internal-api-secret
```

> 로비는 `envFrom`으로 받아도 된다 — 이 Secret에는 조회 키 하나뿐이다. 게임서버 파드는 `envFrom`을 쓰지 않는다(Task 3).

**매칭 서버에는 붙이지 않는다** — introspect를 제공하지도 호출하지도 않는다.

- [ ] **Step 2: README 부트스트랩에 항목을 추가한다**

`README.md`의 `auth-secret` 항목(98~107행) 바로 뒤에 5번 항목으로 추가한다:

```markdown
5. `internal-api-secret` 수기 생성 (lobby-server가 부팅 시 요구 / 게임서버 파드가 introspect 호출에 사용)
   ```bash
   kubectl create secret generic internal-api-secret \
     --from-literal=INTERNAL_API_KEY='local-dev-only-CHANGE-ME-not-a-real-key'
   ```
   **`auth-secret`과 반드시 다른 Secret으로 둔다.** 게임서버 파드는 이 Secret의 키 하나만
   `secretKeyRef`로 받는데, 서명키와 같은 Secret에 넣으면 `envFrom` 실수 한 번으로 토큰을 위조할 수 있는
   키가 방마다 뜨는 파드에 퍼진다. 조회 키는 유출돼도 토큰 발급은 불가능하다.
```

- [ ] **Step 3: 매니페스트가 유효한지 확인한다**

```bash
cd infrastructure && kubectl kustomize k8s/apps/backend | grep -A 3 "internal-api-secret"
```
기대: lobby-server Deployment의 `envFrom`에 `secretRef: internal-api-secret`이 보인다.

- [ ] **Step 4: 커밋**

```bash
cd infrastructure
git add k8s/apps/backend/lobby-server/lobby-server-deployment.yaml README.md
git commit -m "feat(k8s): 조회 키 Secret(internal-api-secret)을 로비에 배선"
```

---

## Task 5: 게임서버 introspect 호출부

**Repo:** `LeagueOfPhysical-Server`

**Files:**
- Modify: `Assets/Scripts/Room/CustomProperties.cs`
- Create: `Assets/Scripts/WebAPI/Dto/Response/IntrospectResponse.cs` (+ `.meta`)
- Modify: `Assets/Scripts/WebAPI/WebAPI.cs`

**Interfaces:**
- Produces: `WebAPI.Introspect(string accessToken, CancellationToken)` → `UniTask<IntrospectResponse>`
- Produces: `IntrospectResponse { bool active; string sub; long exp; }`
- Produces: `CustomProperties { string userId; string accessToken; int characterId; }`

- [ ] **Step 1: `CustomProperties` 필드를 rename한다**

`Assets/Scripts/Room/CustomProperties.cs`:

```csharp
        public string userId;
        public string accessToken;
        public int characterId;
```

- [ ] **Step 2: 응답 DTO를 만든다**

`Assets/Scripts/WebAPI/Dto/Response/IntrospectResponse.cs`:

```csharp
using System;

namespace LOP
{
    //  이 엔드포인트는 다른 API와 달리 code 봉투를 쓰지 않는다(RFC 7662 형식) — HttpResponse를 상속하지 않는다.
    [Serializable]
    public class IntrospectResponse
    {
        public bool active;
        public string sub;
        public long exp;
    }
}
```

- [ ] **Step 3: 요청 DTO를 만든다**

`Assets/Scripts/WebAPI/Dto/Request/IntrospectRequest.cs`:

```csharp
using System;

namespace LOP
{
    [Serializable]
    public class IntrospectRequest
    {
        public string token;
    }
}
```

- [ ] **Step 4: `WebAPI.Introspect`를 추가한다**

`Assets/Scripts/WebAPI/WebAPI.cs`의 `#region Match` 아래에 새 region으로 추가한다:

```csharp
        #region Auth
        //  전역 발행(SendAsync<T>)을 쓰지 않는다 — 구독자가 없는데 GlobalMessagePipe.GetPublisher<T>를
        //  도는 것은 IL2CPP에서 open generic 미지원으로 터질 수 있고, 브로커를 등록할 이유도 없다.
        public static UniTask<IntrospectResponse> Introspect(string accessToken, CancellationToken cancellationToken = default)
        {
            var request = HttpRequestMessage.Post(
                $"{EnvironmentSettings.active.lobbyBaseURL}/auth/introspect",
                new IntrospectRequest { token = accessToken });

            request.Headers["X-Internal-Api-Key"] = System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY");

            return httpClient.SendAsync<IntrospectResponse>(request, cancellationToken);
        }
        #endregion
```

> `httpClient.SendAsync<T>`(`HttpClientJsonExtensions`)는 4xx·5xx에서 `HttpRequestException`을 던진다.
> 조회 키가 틀려 401이 와도 예외로 올라오므로, Task 6의 `catch`가 그대로 **거부**로 처리한다(fail closed).

- [ ] **Step 5: 컴파일을 확인한다**

UnityMCP `read_console`로 서버 인스턴스의 컴파일 에러가 0인지 확인한다(`unity_instance`에 서버 인스턴스 id를 명시).

기대: `CustomProperties.token`을 참조하던 곳이 없으므로(서버는 `msg.customProperties.userId`만 읽는다) 에러 0.

- [ ] **Step 6: 커밋**

```bash
cd LeagueOfPhysical-Server
git add Assets/Scripts/Room/CustomProperties.cs \
        Assets/Scripts/WebAPI/Dto/Response/IntrospectResponse.cs Assets/Scripts/WebAPI/Dto/Response/IntrospectResponse.cs.meta \
        Assets/Scripts/WebAPI/Dto/Request/IntrospectRequest.cs Assets/Scripts/WebAPI/Dto/Request/IntrospectRequest.cs.meta \
        Assets/Scripts/WebAPI/WebAPI.cs
git commit -m "feat(server): introspect 호출부 + CustomProperties.accessToken 리네임"
```

---

## Task 6: 게임서버 접속 판정

**Repo:** `LeagueOfPhysical-Server`

**Files:**
- Modify: `Assets/Scripts/Room/LOPNetworkAuthenticator.cs`

**Interfaces:**
- Consumes: `WebAPI.Introspect(string, CancellationToken)` → `UniTask<IntrospectResponse>` (Task 5), `CustomProperties.accessToken` (Task 5).
- Produces: `conn.authenticationData`에 `CustomProperties`가 들어가며, 그 `userId`는 introspect의 `sub`(에디터에서는 클라 주장값).

- [ ] **Step 1: 판정 로직을 비동기로 바꾼다**

`Assets/Scripts/Room/LOPNetworkAuthenticator.cs`의 `#region Server` 안을 아래로 교체한다. `OnStartServer`/`OnStopServer`/`OnServerAuthenticate`는 그대로 두되 `OnStopServer`에 `handledConnectionIds.Clear();`를 더한다.

```csharp
        //  로비가 죽었을 때 30초(HttpClient 기본 타임아웃)를 기다리지 않는다 — 접속은 사람이 기다리는 경로다.
        private const int IntrospectTimeoutSeconds = 3;

        //  같은 연결이 인증 요청을 반복해 보내면 그때마다 로비를 부르게 된다(소켓 1회 → HTTP N회 증폭).
        //  첫 요청만 처리한다. 방 수명이 짧고 연결 수는 참가자 수로 묶이므로 OnStopServer에서 통째로 비운다.
        private readonly HashSet<int> handledConnectionIds = new HashSet<int>();

        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (handledConnectionIds.Add(conn.connectionId) == false)
            {
                return;
            }

            AuthenticateAsync(conn, msg).Forget();
        }

        private async UniTaskVoid AuthenticateAsync(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            string claimedUserId = msg.customProperties?.userId;

            if (roomDataStore.match.playerList.Contains(claimedUserId) == false)
            {
                Reject(conn, $"명단에 없는 userId: {claimedUserId}");
                return;
            }

#if UNITY_EDITOR
            //  에디터의 게임서버는 가짜 방·가짜 명단으로 돈다(ConfigureRoomComponent). 조회 키를 git에
            //  커밋하지 않으려고 introspect도 같은 경계 안에 둔다. 실환경에서는 아래 경로를 반드시 탄다.
            Debug.LogWarning("[Auth] 에디터라 introspect를 건너뜁니다. 신원은 클라가 주장한 값을 씁니다.");
            Accept(conn, msg.customProperties, claimedUserId);
            return;
#else
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")))
            {
                Debug.LogError("[Auth] INTERNAL_API_KEY가 없습니다. 접속을 허용할 수 없습니다.");
                Reject(conn, "server misconfigured");
                return;
            }

            IntrospectResponse introspect;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(IntrospectTimeoutSeconds));
                introspect = await WebAPI.Introspect(msg.customProperties.accessToken, timeout.Token);
            }
            catch (Exception exception)
            {
                //  확인하지 못한 것은 통과시키지 않는다(fail closed).
                Reject(conn, $"introspect 실패: {exception.Message}");
                return;
            }

            if (introspect.active == false)
            {
                Reject(conn, "토큰이 유효하지 않음");
                return;
            }

            if (introspect.sub != claimedUserId)
            {
                //  명단은 A로 통과했는데 토큰 주인은 B인 경우 — 사칭이다.
                Reject(conn, $"토큰 주인과 주장한 userId가 다름: {introspect.sub} != {claimedUserId}");
                return;
            }

            Accept(conn, msg.customProperties, introspect.sub);
#endif
        }

        private void Accept(NetworkConnectionToClient conn, CustomProperties customProperties, string authenticatedUserId)
        {
            if (NetworkServer.connections.ContainsKey(conn.connectionId) == false)
            {
                //  로비에 물어보는 동안 끊긴 연결이다. 아무것도 하지 않는다.
                //  (isReady는 씬 준비 여부라 여기서 볼 값이 아니다 — 미인증 연결은 늘 false다.)
                return;
            }

            //  클라가 주장한 값이 아니라 확인된 신원을 저장한다 — 이후 모든 서버 로직이 이 값을 신원으로 쓴다.
            customProperties.userId = authenticatedUserId;
            conn.authenticationData = customProperties;

            conn.Send(new AuthResponseMessage { code = 200, message = "success" });
            ServerAccept(conn);
        }

        private void Reject(NetworkConnectionToClient conn, string reason)
        {
            //  클라에는 사유를 나누지 않는다 — 왜 거부됐는지 알려주면 밖에서 상태를 떠볼 수 있다.
            Debug.LogWarning($"[Auth] 접속 거부: {reason}");

            conn.Send(new AuthResponseMessage { code = 401, message = "Invalid Credentials" });
            conn.isAuthenticated = false;
            ServerReject(conn);
        }
```

기존 `IsAuthenticated` 메서드는 삭제한다.

- [ ] **Step 2: using을 맞춘다**

파일 상단에 다음이 있어야 한다(없는 것만 추가):

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
```

- [ ] **Step 3: 컴파일을 확인한다**

UnityMCP `read_console`(서버 인스턴스 명시)로 에러 0을 확인한다.

- [ ] **Step 4: 커밋**

```bash
cd LeagueOfPhysical-Server
git add Assets/Scripts/Room/LOPNetworkAuthenticator.cs
git commit -m "feat(server): 방 접속 시 로비 introspect로 토큰 확인"
```

---

## Task 7: 인게임 메시지 신원 귀속

**Repo:** `LeagueOfPhysical-Server`

**Files:**
- Create: `Assets/Scripts/Network/ClientMessage.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Network/NetworkMessageDispatcher.cs`
- Modify: `Assets/Scripts/Room/LOPRoom.cs:97-102`
- Modify: `Assets/Scripts/RootLifetimeScope.cs:28-30`
- Modify: `Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `conn.authenticationData`의 `CustomProperties.userId` = 확인된 신원 (Task 6).
- Produces: `ClientMessage<T> { string UserId; T Message; }`, `NetworkMessageDispatcher.Dispatch(string userId, IMessage message)`

- [ ] **Step 1: 봉투 타입을 만든다**

`Assets/Scripts/Network/ClientMessage.cs`:

```csharp
using GameFramework;

namespace LOP
{
    /// <summary>클라가 보낸 메시지와, 그 연결에서 서버가 확인한 신원을 함께 나른다.
    /// 메시지 안에 적힌 신원은 클라가 쓴 것이라 믿을 수 없다.</summary>
    public readonly struct ClientMessage<T> where T : IMessage
    {
        public string UserId { get; }
        public T Message { get; }

        public ClientMessage(string userId, T message)
        {
            UserId = userId;
            Message = message;
        }
    }
}
```

- [ ] **Step 2: 디스패처가 신원을 함께 발행하게 한다**

`Assets/Scripts/Network/NetworkMessageDispatcher.cs`를 아래로 바꾼다(주석 헤더는 유지하되 마지막 괄호 문장만 그대로 둔다):

```csharp
    public class NetworkMessageDispatcher
    {
        private readonly Dictionary<Type, Action<string, IMessage>> routes = new();

        [Inject]
        public NetworkMessageDispatcher(
            IPublisher<ClientMessage<GameInfoToS>> gameInfo,
            IPublisher<ClientMessage<InputCommandToS>> inputCommand,
            IPublisher<ClientMessage<StatAllocationToS>> statAllocation)
        {
            Register(gameInfo);
            Register(inputCommand);
            Register(statAllocation);
        }

        private void Register<T>(IPublisher<ClientMessage<T>> publisher) where T : IMessage
        {
            routes[typeof(T)] = (userId, message) => publisher.Publish(new ClientMessage<T>(userId, (T)message));
        }

        public void Dispatch(string userId, IMessage message)
        {
            if (routes.TryGetValue(message.GetType(), out var route))
            {
                route(userId, message);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[NetworkMessageDispatcher] 미등록 메시지 타입: {message.GetType()}");
            }
        }
    }
```

- [ ] **Step 3: 수신부가 신원을 넘기게 한다**

`Assets/Scripts/Room/LOPRoom.cs`의 `StartRoomServerAsync` 안 핸들러 등록을 바꾼다:

```csharp
            NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
            {
                //  RegisterHandler의 requireAuthentication 기본값이 true라 미인증 연결은 여기 오지 않는다.
                //  즉 authenticationData는 인증기가 채워 둔 값이 반드시 들어 있다.
                var customProperties = (CustomProperties)conn.authenticationData;
                dispatcher.Dispatch(customProperties.userId, message.payload);
            });
```

- [ ] **Step 4: 브로커 등록을 교체한다**

`Assets/Scripts/RootLifetimeScope.cs`의 28~30행을 바꾼다:

```csharp
            builder.RegisterMessageBroker<ClientMessage<GameInfoToS>>(options);
            builder.RegisterMessageBroker<ClientMessage<InputCommandToS>>(options);
            builder.RegisterMessageBroker<ClientMessage<StatAllocationToS>>(options);
```

- [ ] **Step 5: `GameInfoMessageHandler`를 고친다**

구독 타입과 버퍼 타입, 그리고 `Tick` 안의 신원 사용처를 바꾼다.

```csharp
        private readonly ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber;

        private List<ClientMessage<GameInfoToS>> gameInfoToSList = new List<ClientMessage<GameInfoToS>>();
```

생성자 파라미터 타입도 `ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber`로 바꾼다.

```csharp
        private void OnGameInfoToS(ClientMessage<GameInfoToS> received)
        {
            gameInfoToSList.Add(received);
        }
```

`Tick` 안의 루프에서 `gameInfoToS.UserId` 두 곳을 `received.UserId`로 바꾼다:

```csharp
            foreach (var received in gameInfoToSList)
            {
                var session = sessionManager.GetSessionByUserId(received.UserId);
                string entityId = entitySpawner.GetEntityIdByUserId(received.UserId);
```

- [ ] **Step 6: `GameInputMessageHandler`를 고친다**

```csharp
        private readonly ISubscriber<ClientMessage<InputCommandToS>> inputCommandSubscriber;
```

생성자 파라미터 타입도 같이 바꾼 뒤:

```csharp
        private void OnInputCommandToS(ClientMessage<InputCommandToS> received)
        {
            //  세션은 메시지에 적힌 값이 아니라 연결에서 확인된 신원으로 찾는다.
            ISession session = sessionManager.GetSessionByUserId(received.UserId);
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
            var buffer = entityRegistry.Get(entityId).Get<InputBuffer>();
            if (buffer == null)
            {
                return;
            }

            foreach (var entry in received.Message.RecentInputs)
            {
                if (inputBufferSystem.Enqueue(buffer, entry.Tick, ToInputCommand(entry.InputCommand)))
                {
                    buffer.TimingTracker.RecordArrival((int)(tickUpdater.tick - entry.Tick));
                }
            }
        }
```

(`RecentInputs` 위의 sliding-window 설명 주석은 그대로 둔다.)

- [ ] **Step 7: `GameEntityMessageHandler`를 고친다**

```csharp
        private readonly ISubscriber<ClientMessage<StatAllocationToS>> statAllocationSubscriber;
```

생성자 파라미터 타입도 같이 바꾼 뒤, 핸들러 앞부분을 바꾼다. 아래쪽 `switch`와 `Allocate` 호출은 `statAllocationToS.Stat` → `received.Message.Stat`으로만 바꾼다.

```csharp
        private void OnStatAllocationToS(ClientMessage<StatAllocationToS> received)
        {
            ISession session = sessionManager.GetSessionByUserId(received.UserId);
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
```

- [ ] **Step 8: 컴파일을 확인한다**

UnityMCP `read_console`(서버 인스턴스 명시)로 에러 0을 확인한다. `GetSessionById` 호출이 남아 있지 않은지 함께 확인한다:

```bash
cd LeagueOfPhysical-Server && grep -rn "GetSessionById" Assets/Scripts
```
기대: 출력 없음.

- [ ] **Step 9: 커밋**

```bash
cd LeagueOfPhysical-Server
git add Assets/Scripts/Network/ClientMessage.cs Assets/Scripts/Network/ClientMessage.cs.meta \
        Assets/Scripts/Network/NetworkMessageDispatcher.cs \
        Assets/Scripts/Room/LOPRoom.cs \
        Assets/Scripts/RootLifetimeScope.cs \
        Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs \
        Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs \
        Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(server): 인게임 메시지 신원을 연결에서 도출"
```

---

## Task 8: 클라 — 접속 전 갱신 + 진짜 토큰

**Repo:** `LeagueOfPhysical-Client`

**Files:**
- Modify: `Assets/Scripts/Room/CustomProperties.cs`
- Modify: `Assets/Scripts/Room/LOPNetworkAuthenticator.cs`
- Modify: `Assets/Scripts/Room/LOPRoom.cs:80-108`
- Modify: `Assets/Scripts/RootLifetimeScope.cs:58`

**Interfaces:**
- Produces: `LOPNetworkAuthenticator.PrepareCredentialAsync(CancellationToken)` → `UniTask`
- Consumes: `GameFramework.Http.IAccessTokenProvider.GetAccessTokenAsync(bool, CancellationToken)` (1a에서 도입)

- [ ] **Step 1: `CustomProperties` 필드를 rename한다**

`Assets/Scripts/Room/CustomProperties.cs`:

```csharp
        public string userId;
        public string accessToken;
        public int characterId;
```

- [ ] **Step 2: `AuthenticationService`를 인터페이스로도 등록한다**

`Assets/Scripts/RootLifetimeScope.cs:58`을 바꾼다. 지금은 구체 타입으로만 등록돼 있어 `IAccessTokenProvider` 주입이 실패한다.

```csharp
            builder.Register<AuthenticationService>(Lifetime.Singleton)
                .As<GameFramework.Http.IAccessTokenProvider>()
                .AsSelf();
```

(`RegisterBuildCallback`의 `container.Resolve<AuthenticationService>()`는 `.AsSelf()` 덕에 그대로 동작한다.)

- [ ] **Step 3: 인증기에 사전 갱신을 넣는다**

`Assets/Scripts/Room/LOPNetworkAuthenticator.cs`의 `#region Client` 안 `OnClientAuthenticate`를 바꾸고 위에 필드·메서드를 추가한다:

```csharp
        [Inject]
        private GameFramework.Http.IAccessTokenProvider accessTokenProvider;

        private string preparedAccessToken;

        /// <summary>접속 직전에 토큰을 준비한다. OnClientAuthenticate는 동기 Mirror 콜백이라
        /// 그 안에서 갱신을 기다릴 수 없으므로, StartClient() 전에 반드시 이것을 await해야 한다.</summary>
        public async UniTask PrepareCredentialAsync(CancellationToken cancellationToken)
        {
            //  강제 갱신(true)을 쓰지 않는다 — 게임서버는 접속 시점에 한 번만 검사하므로 남은 수명이
            //  짧아도 문제가 없고, 강제로 부르면 1a의 30초 스로틀과 얽혀 접속만 늦어진다.
            preparedAccessToken = await accessTokenProvider.GetAccessTokenAsync(false, cancellationToken);
        }

        public override void OnClientAuthenticate()
        {
            var customProperties = new CustomProperties
            {
                userId = userDataStore.user.id,
                accessToken = preparedAccessToken,
                characterId = 0,
            };

            NetworkClient.Send(new AuthRequestMessage { customProperties = customProperties });
        }
```

파일 상단 using에 `Cysharp.Threading.Tasks;`와 `System.Threading;`을 더한다.

- [ ] **Step 4: `LOPRoom`이 접속 전에 기다리게 한다**

`Assets/Scripts/Room/LOPRoom.cs`의 `ConnectRoomServerAsync`에서 `networkManager.StartClient();` 바로 앞에 넣는다:

```csharp
            //  토큰 갱신을 여기서 끝낸다 — 접속 인증 콜백은 동기라 그 안에서는 기다릴 수 없다.
            await ((LOPNetworkAuthenticator)networkManager.authenticator).PrepareCredentialAsync(destroyCancellationToken);

            networkManager.StartClient();
```

- [ ] **Step 5: 컴파일을 확인한다**

UnityMCP `read_console`(클라 인스턴스 명시 — `LeagueOfPhysical-Client@<hash>`)로 에러 0을 확인한다.

- [ ] **Step 6: 클라 EditMode 전체를 돌린다**

UnityMCP `run_tests`(EditMode, 클라 인스턴스 명시).
기대: 434건 전부 PASS(이 태스크는 패키지 코드를 건드리지 않는다).

- [ ] **Step 7: 커밋**

```bash
cd LeagueOfPhysical-Client
git add Assets/Scripts/Room/CustomProperties.cs \
        Assets/Scripts/Room/LOPNetworkAuthenticator.cs \
        Assets/Scripts/Room/LOPRoom.cs \
        Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(client): 방 접속 전 토큰 갱신 + 진짜 토큰 전송"
```

---

## Task 9: 클라가 신원 필드를 보내지 않게 한다

**Repos:** `LeagueOfPhysical-Client`, `LeagueOfPhysical-Shared`

**Files:**
- Create: `LeagueOfPhysical-Shared/Tools/Protobuf/protoc-28.2-osx-universal/` (내려받은 바이너리)
- Modify: `LeagueOfPhysical-Shared/Scripts/compile_protos.sh:3-8`
- Modify: `LeagueOfPhysical-Shared/Protos/{GameInfoToS,InputCommandToS,StatAllocationToS}.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/{GameInfoToS,InputCommandToS,StatAllocationToS}.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Room/LOPRoom.cs:113-120`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs:117`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/Stats/StatsViewModel.cs:72`

**Interfaces:**
- Consumes: Task 7이 이미 서버에서 이 필드들을 읽지 않게 만들었다. 순서를 지켜야 한다(9가 7보다 먼저 배포되면 서버가 신원을 못 찾는다).

- [ ] **Step 1: macOS용 protoc을 넣는다**

이 저장소는 win64 바이너리만 갖고 있다. **같은 버전(28.2)** 의 macOS universal 바이너리를 받아 나란히 둔다.

```bash
cd LeagueOfPhysical-Shared/Tools/Protobuf
curl -sL -o protoc-osx.zip https://github.com/protocolbuffers/protobuf/releases/download/v28.2/protoc-28.2-osx-universal_binary.zip
unzip -q protoc-osx.zip -d protoc-28.2-osx-universal
rm protoc-osx.zip
chmod +x protoc-28.2-osx-universal/bin/protoc
./protoc-28.2-osx-universal/bin/protoc --version
```
기대: `libprotoc 28.2`

- [ ] **Step 2: `compile_protos.sh`가 플랫폼을 분기하게 한다**

`Scripts/compile_protos.sh`의 3~6행(변수 설정)을 바꾼다. 나머지는 손대지 않는다.

```bash
# 변수 설정
# protoc은 플랫폼별 바이너리다 — 같은 버전(28.2)이면 어느 쪽으로 만들어도 출력이 바이트 단위로 같다.
case "$(uname -s)" in
    Darwin) PROTOC_HOME="../Tools/Protobuf/protoc-28.2-osx-universal" ;;
    *)      PROTOC_HOME="../Tools/Protobuf/protoc-28.2-win64" ;;
esac

PROTOC="$PROTOC_HOME/bin/protoc"
PROTO_PATH="../Protos"
INCLUDE_PATH="$PROTOC_HOME/include"
OUT_PATH="../Runtime.Generated/Scripts/Protobuf"
FILE_COUNT=0
```

- [ ] **Step 3: 아무것도 안 고친 상태로 재생성해 출력이 같은지 확인한다**

`.proto`를 건드리기 **전에** 돌려서, 도구 교체 자체로는 아무 변화가 없음을 확인한다.

```bash
cd LeagueOfPhysical-Shared/Scripts && ./compile_protos.sh
cd .. && git status --short Runtime.Generated/
```
기대: **출력 없음**(변경된 파일 0개). 여기서 파일이 바뀌면 도구가 다른 것이므로 멈추고 보고할 것.

- [ ] **Step 4: proto에서 신원 필드를 지운다**

지운 자리에 `reserved`를 남긴다 — 나중에 다른 필드가 같은 번호를 재사용하면, 배포 시점이 어긋난 구버전이 옛 값을 새 필드로 읽는다.

`Protos/GameInfoToS.proto`:

```protobuf
// @auto_generate
message GameInfoToS {
	// 신원은 연결에서 도출한다(1c) — 클라가 적어 보내지 않는다.
	reserved 1;
}
```

`Protos/InputCommandToS.proto`의 `message InputCommandToS`에서 `string session_id = 1;`을 지우고 같은 주석 + `reserved 1;`을 넣는다. 나머지 필드 번호(2~5)는 **바꾸지 않는다**.

`Protos/StatAllocationToS.proto`에서도 `string session_id = 1;`을 지우고 같은 주석 + `reserved 1;`을 넣는다. `stat = 2`는 그대로 둔다.

- [ ] **Step 5: 재생성하고 diff를 확인한다**

```bash
cd LeagueOfPhysical-Shared/Scripts && ./compile_protos.sh
cd .. && git status --short Runtime.Generated/
```
기대: `GameInfoToS.cs`, `InputCommandToS.cs`, `StatAllocationToS.cs` **딱 3개만** 수정됨. `.meta`가 함께 뜨면 `generate_protos.sh`를 잘못 돌린 것이다.

- [ ] **Step 6: 클라 송신부 세 곳에서 대입을 지운다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`에서 이 줄을 삭제한다:

```csharp
            inputCommandToS.SessionId = playerContext.session.sessionId;
```

`LeagueOfPhysical-Client/Assets/Scripts/UI/Stats/StatsViewModel.cs`에서 `SessionId` 대입 줄을 삭제해 이렇게 남긴다:

```csharp
            _playerContext.session.Send(new StatAllocationToS
            {
                Stat = statName,
            });
```

`LeagueOfPhysical-Client/Assets/Scripts/Room/LOPRoom.cs`의 `JoinRoomServerAsync`에서 `UserId` 대입을 지워 이렇게 남긴다:

```csharp
            CustomMirrorMessage message = new CustomMirrorMessage
            {
                payload = new GameInfoToS(),
            };
```

- [ ] **Step 7: 남은 참조가 없는지 확인한다**

```bash
cd LeagueOfPhysical-Client && grep -rn "inputCommandToS.SessionId\|SessionId = _playerContext\|UserId = userDataStore" Assets/Scripts
```
기대: 출력 없음. (필드가 사라졌으므로 남아 있으면 컴파일도 깨진다.)

- [ ] **Step 8: 컴파일과 테스트를 확인한다**

UnityMCP `read_console`(클라 인스턴스 명시)로 에러 0, `run_tests`(EditMode)로 434건 PASS.
서버 인스턴스도 `read_console`로 에러 0을 확인한다 — Task 7이 이미 이 필드들을 안 읽게 만들었으므로 깨질 곳이 없어야 한다.

- [ ] **Step 9: 커밋 (두 저장소)**

```bash
cd LeagueOfPhysical-Shared
git add Tools/Protobuf/protoc-28.2-osx-universal \
        Scripts/compile_protos.sh \
        Protos/GameInfoToS.proto Protos/InputCommandToS.proto Protos/StatAllocationToS.proto \
        Runtime.Generated/Scripts/Protobuf/GameInfoToS.cs \
        Runtime.Generated/Scripts/Protobuf/InputCommandToS.cs \
        Runtime.Generated/Scripts/Protobuf/StatAllocationToS.cs
git commit -m "feat(proto): ToS 메시지에서 신원 필드 제거 + macOS protoc 추가"

cd ../LeagueOfPhysical-Client
git add Assets/Scripts/Game/PlayerInputManager.cs \
        Assets/Scripts/UI/Stats/StatsViewModel.cs \
        Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(client): 인게임 메시지에서 신원 필드 전송 중단"
```

---

## Task 10: `GameFramework.Auth.Jwt` 삭제

**Repo:** `GameFramework`

**Files:**
- Delete: `Runtime/Scripts/Auth/Jwt.cs`, `Runtime/Scripts/Auth/Jwt.cs.meta`
- Delete: `Tests/Runtime/Auth/JwtTests.cs`, `Tests/Runtime/Auth/JwtTests.cs.meta`

**Interfaces:**
- 없음. 사용처가 없다(이 설계는 파드에서 로컬 서명 검증을 하지 않는다).

- [ ] **Step 1: 사용처가 정말 없는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "Jwt\." --include="*.cs" GameFramework/Runtime LeagueOfPhysical-Client/Assets/Scripts LeagueOfPhysical-Server/Assets/Scripts LeagueOfPhysical-Shared/Runtime
```
기대: 출력 없음(테스트 파일은 이 경로에 없다).

- [ ] **Step 2: 삭제한다**

```bash
cd GameFramework
git rm Runtime/Scripts/Auth/Jwt.cs Runtime/Scripts/Auth/Jwt.cs.meta \
       Tests/Runtime/Auth/JwtTests.cs Tests/Runtime/Auth/JwtTests.cs.meta
```

- [ ] **Step 3: 테스트를 돌린다**

UnityMCP `run_tests`(EditMode, 클라 인스턴스 명시). GameFramework은 클라 프로젝트의 `testables`에 들어 있어 클라에서 함께 돈다.

기대: `JwtTests` 10건이 빠진 수만큼 총계가 줄고, 실패 0.

- [ ] **Step 4: 커밋**

```bash
cd GameFramework
git commit -m "chore(auth): 사용처가 없어진 Jwt 검증기 삭제"
```

---

## 배포 순서 (전체 태스크 완료 후)

머지·배포는 **반드시 이 순서**다. `backend-deploy`의 `bump-tags` 잡이 infrastructure의 `main`을 체크아웃하므로 인프라가 먼저 가야 한다.

1. **클러스터에 Secret을 먼저 만든다** (README의 새 5번 항목). 없으면 새 로비 파드가 크래시 루프에 빠진다.
2. `infrastructure` 머지 → push
3. `lop-backend` 머지 → push → `gh workflow run backend-deploy.yml -f app=all`
4. `GameFramework`, `LeagueOfPhysical-Shared` 머지 → push
5. **게임서버 이미지와 클라를 함께** 내보낸다 — 게임서버만 나가면 아직 `"token"`을 보내는 구 클라가 전부 입장 거부된다.

## 수동 검증 (배포 후)

1. 정상 매칭 → 방 입장 성공. 로비 로그에 `POST /auth/introspect 200`
2. 게임서버 파드 env 확인: `kubectl exec <room-pod> -- printenv INTERNAL_API_KEY` → 값이 나오고, `printenv AUTH_JWT_SECRET` → **비어 있어야 한다**
3. 밖에서 키 없이 호출: `curl -X POST <ingress>/lobby/auth/introspect -d '{"token":"x"}' -H 'Content-Type: application/json'` → **401**
4. 클라에 임시 코드로 토큰을 훼손 → 방 입장 거부 확인 → 임시 코드 되돌림 (1b의 401 검증과 같은 방식)
