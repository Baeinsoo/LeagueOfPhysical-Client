# 인증 cutover 2b — 내부 전용 라우트 차단 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 무인증으로 열려 있는 백엔드 라우트를 닫는다 — 서비스 전용 동작 9개는 `/internal/*`로 옮겨 내부 키를 요구하고, 클라도 부르는 조회 4개는 경로를 유지한 채 주체(유저/서비스)별로 인가하며, 호출자가 없는 3개는 지운다.

**Architecture:** 신원 확인(누구냐)과 인가(해도 되냐)를 분리한다. `authenticatePrincipal`이 내부 키 헤더 유무로 주체를 `service` 또는 `user`로 정하고, `requireSelfOrService`가 "서비스면 전체 / 유저면 본인만"을 판단한다. 서비스 전용 라우트는 별도 `/internal` 라우터에 모아 키 미들웨어를 경로 단위로 한 번 건다. 부르는 쪽은 백엔드가 공용 axios 인스턴스, 게임서버가 `ApiKeyHandler`로 헤더를 자동 부착한다.

**Tech Stack:** TypeScript / Express 4 / axios 0.26 / jest + ts-jest + supertest / pnpm + turbo 모노레포 / Unity C# (UniTask, NUnit) / k8s (ingress-nginx)

**Spec:** `docs/superpowers/specs/2026-08-10-auth-cutover-2b-internal-route-lockdown-design.md` (LeagueOfPhysical-Client 저장소)

## Global Constraints

- **브랜치**: 다섯 저장소 모두 `feature/internal-route-lockdown`에서 작업한다. main 직접 커밋 금지.
- **저장소 경로**: `/Users/insoobae/workspace/LOP/{lop-backend,infrastructure,GameFramework,LeagueOfPhysical-Server,LeagueOfPhysical-Client}`
- **server-core를 고치면 반드시 빌드한다**: `pnpm --filter @lop/server-core build`. 앱의 **빌드와 실행**은 `dist/`를 읽으므로, 빌드를 빼면 앱이 옛 코드를 본다. (앱 jest는 `moduleNameMapper`가 `@lop/server-core/*`를 `src`로 돌려 놓아서 테스트만은 최신 소스를 본다 — 그래서 "테스트는 통과하는데 빌드가 깨지는" 상태가 나올 수 있다.)
- **키가 틀렸을 때 유저 토큰으로 강등하지 않는다.** 키 헤더가 있는데 검증에 실패하면 401로 끝낸다.
- **키 헤더가 없으면 `INTERNAL_API_KEY` 환경변수를 읽지 않는다.** 읽으면 그 값이 빠졌을 때 키가 필요 없는 유저 요청까지 500이 된다.
- **키와 토큰이 둘 다 있으면 키가 이긴다.**
- 응답 코드: 자격증명 없음 `401` / 유저인데 남의 것 `403` / 키 틀림 `401`.
- **주석은 "왜"만 쓴다.** 코드로 자명한 내용은 주석을 달지 않는다. 전문용어를 던지지 말고 그 자리에서 풀어 쓴다.
- **`.meta` 파일을 반드시 함께 커밋한다** (Unity 저장소에 새 파일을 만들 때). Unity 에디터가 만든 것만 커밋하고 직접 만들지 않는다.
- 테스트 이름·주석은 한국어로 쓴다 (기존 코드 관례).
- `pnpm` 명령은 항상 `lop-backend` 루트에서, `npx jest`는 해당 패키지 디렉터리에서 실행한다.

## File Structure

**lop-backend**

| 파일 | 책임 |
|---|---|
| `packages/server-core/src/middlewares/principal.middleware.ts` (신규) | 주체 판별 + 주체별 인가 (`Principal`, `authenticatePrincipal`, `requireSelfOrService`) |
| `packages/server-core/src/http/internalHttpClient.ts` (신규) | 서비스 간 호출 전용 axios 인스턴스 — 키 헤더 자동 부착 |
| `packages/server-core/src/entries/{auth,http}.ts` | 서브패스 내보내기 |
| `apps/lobby-server/src/routes/internal.route.ts` (신규) | 로비의 서비스 전용 라우트 3개 |
| `apps/matchmaking-server/src/routes/internal.route.ts` (신규) | 매치메이킹의 서비스 전용 라우트 1개 |
| `apps/room-server/src/routes/internal.route.ts` (신규) | 룸의 서비스 전용 라우트 6개 |
| `apps/matchmaking-server/src/policies/matchAccess.ts` (신규) | "이 주체가 이 매치를 볼 수 있나" 순수 함수 |
| 각 앱 `routes/*.route.ts` | 공개 라우트에 인가 부착 + 고아 라우트 제거 |
| 각 앱 `services/httpServices/*.service.ts` | 공용 내부 클라이언트 + `/internal` URL로 전환 |

**GameFramework / LeagueOfPhysical-Server**

| 파일 | 책임 |
|---|---|
| `GameFramework/Runtime/Scripts/Http/ApiKeyHandler.cs` (신규) | 고정 헤더에 API 키를 붙이는 DelegatingHandler |
| `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs` | 핸들러 조립 + `/internal` URL |

**infrastructure**

| 파일 | 책임 |
|---|---|
| `k8s/apps/backend/{matchmaking-server/matchmaking-server,matchmaking-server/matchmaking-director,room-server/room-server}-deployment.yaml` | `internal-api-secret` 주입 |
| `k8s/platform/ingress/ingress.yaml` | 외부에서 `/internal` 접근 차단 |

---

### Task 1: 주체 판별 + 주체별 인가 미들웨어

**Files:**
- Create: `lop-backend/packages/server-core/src/middlewares/principal.middleware.ts`
- Create: `lop-backend/packages/server-core/src/middlewares/__tests__/principal.middleware.test.ts`
- Modify: `lop-backend/packages/server-core/src/entries/auth.ts`

**Interfaces:**
- Consumes: `authMiddleware`(`../auth.middleware`), `internalApiKeyMiddleware`(`../internalApiKey.middleware`), `signAccessToken(userId: string, secret: string): string`(`../auth/token`)
- Produces:
  - `type Principal = { kind: 'service' } | { kind: 'user'; userId: string }`
  - `authenticatePrincipal(req, res, next): void` — `req.principal`을 채운다. 유저 주체일 때 `req.userId`도 기존대로 채운다.
  - `requireSelfOrService(paramName?: string): RequestHandler` — 기본값 `'id'`
  - 셋 다 `@lop/server-core/auth`로 내보낸다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`lop-backend/packages/server-core/src/middlewares/__tests__/principal.middleware.test.ts`:

```ts
import type { NextFunction, Request, Response } from 'express';
import { authenticatePrincipal, requireSelfOrService } from '../principal.middleware';
import { signAccessToken } from '../../auth/token';

const SECRET = 'test-secret-0123456789';
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

function 인증한다(headers: Record<string, string>) {
    const req = { headers, params: {} } as unknown as Request;
    const res = createResponse();
    const next = jest.fn() as unknown as NextFunction;
    authenticatePrincipal(req, res, next);
    return { req, res, next };
}

describe('authenticatePrincipal', () => {
    beforeEach(() => {
        process.env.AUTH_JWT_SECRET = SECRET;
        process.env.INTERNAL_API_KEY = KEY;
    });

    it('키가 맞으면 서비스 주체로 통과시킨다', () => {
        const { req, next } = 인증한다({ 'x-internal-api-key': KEY });

        expect(req.principal).toEqual({ kind: 'service' });
        expect(next).toHaveBeenCalled();
    });

    it('토큰만 있으면 유저 주체로 통과시키고 req.userId도 채운다', () => {
        const { req, next } = 인증한다({ authorization: `Bearer ${signAccessToken('user-1', SECRET)}` });

        expect(req.principal).toEqual({ kind: 'user', userId: 'user-1' });
        expect(req.userId).toBe('user-1');
        expect(next).toHaveBeenCalled();
    });

    it('아무 자격증명도 없으면 401', () => {
        const { res, next } = 인증한다({});

        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    //  자격증명을 제시했는데 조용히 낮은 등급으로 강등하면, 공격자가 키를 한 글자씩 떠보면서도
    //  정상 응답을 받는다. KEY와 "같은 길이, 다른 내용"이어야 상수시간 비교까지 실제로 탄다.
    it('키가 틀리면 유효한 토큰이 같이 있어도 401', () => {
        const { res, next } = 인증한다({
            'x-internal-api-key': 'internal-key-9999999999',
            authorization: `Bearer ${signAccessToken('user-1', SECRET)}`,
        });

        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    //  키 헤더가 없는 요청까지 INTERNAL_API_KEY를 보게 만들면, 그 값이 빠진 서비스에서
    //  키가 필요 없는 클라 조회가 전부 500이 된다.
    it('키 헤더가 없으면 INTERNAL_API_KEY가 없어도 500이 아니다', () => {
        delete process.env.INTERNAL_API_KEY;

        const { req, res, next } = 인증한다({ authorization: `Bearer ${signAccessToken('user-1', SECRET)}` });

        expect(res.statusCode).not.toBe(500);
        expect(req.principal).toEqual({ kind: 'user', userId: 'user-1' });
        expect(next).toHaveBeenCalled();
    });
});

function 인가한다(principal: unknown, params: Record<string, string>) {
    const req = { headers: {}, params, principal } as unknown as Request;
    const res = createResponse();
    const next = jest.fn() as unknown as NextFunction;
    requireSelfOrService()(req, res, next);
    return { res, next };
}

describe('requireSelfOrService', () => {
    it('서비스 주체는 남의 id여도 통과한다', () => {
        const { next } = 인가한다({ kind: 'service' }, { id: 'someone-else' });

        expect(next).toHaveBeenCalled();
    });

    it('유저 주체는 자기 id면 통과한다', () => {
        const { next } = 인가한다({ kind: 'user', userId: 'user-1' }, { id: 'user-1' });

        expect(next).toHaveBeenCalled();
    });

    it('유저 주체가 남의 id를 보면 403', () => {
        const { res, next } = 인가한다({ kind: 'user', userId: 'user-1' }, { id: 'user-2' });

        expect(res.statusCode).toBe(403);
        expect(next).not.toHaveBeenCalled();
    });

    //  주체와 파라미터가 둘 다 비면 undefined === undefined로 우연히 통과할 수 있다.
    it('주체가 없으면 403', () => {
        const { res, next } = 인가한다(undefined, {});

        expect(res.statusCode).toBe(403);
        expect(next).not.toHaveBeenCalled();
    });

    it('파라미터 이름을 지정하면 그 값을 본다', () => {
        const req = { headers: {}, params: { userId: 'user-1' }, principal: { kind: 'user', userId: 'user-1' } } as unknown as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        requireSelfOrService('userId')(req, res, next);

        expect(next).toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/server-core && npx jest src/middlewares/__tests__/principal.middleware.test.ts
```

Expected: FAIL — `Cannot find module '../principal.middleware'`

- [ ] **Step 3: 미들웨어를 구현한다**

`lop-backend/packages/server-core/src/middlewares/principal.middleware.ts`:

```ts
import { NextFunction, Request, RequestHandler, Response } from 'express';
import { authMiddleware } from './auth.middleware';
import { internalApiKeyMiddleware } from './internalApiKey.middleware';

/** 요청을 보낸 것이 우리 서비스인지, 로그인한 유저인지. */
export type Principal =
    | { kind: 'service' }
    | { kind: 'user'; userId: string };

declare global {
    namespace Express {
        interface Request {
            principal?: Principal;
        }
    }
}

const INTERNAL_KEY_HEADER = 'x-internal-api-key';

/**
 * 자격증명을 보고 요청 주체를 정한다. 키 헤더가 있으면 서비스, 없으면 유저 토큰으로 본다.
 */
export function authenticatePrincipal(req: Request, res: Response, next: NextFunction): void {
    //  키 헤더가 붙어 있으면 서비스 호출로 확정한다 — 검증에 실패해도 유저 토큰으로 내려가지
    //  않는다. 강등해 주면 공격자가 키를 떠보면서도 정상 응답을 받고, 설정 오류도 조용히 숨는다.
    if (typeof req.headers[INTERNAL_KEY_HEADER] === 'string') {
        internalApiKeyMiddleware(req, res, () => {
            req.principal = { kind: 'service' };
            next();
        });
        return;
    }

    //  키 헤더가 없으면 INTERNAL_API_KEY를 아예 보지 않는다. 보면 그 값이 빠진 서비스에서
    //  키가 필요 없는 유저 조회까지 500이 된다.
    authMiddleware(req, res, () => {
        req.principal = { kind: 'user', userId: req.userId as string };
        next();
    });
}

/**
 * 서비스 주체는 전부, 유저 주체는 URL의 그 id가 자기 것일 때만 통과시킨다.
 */
export function requireSelfOrService(paramName: string = 'id'): RequestHandler {
    return (req: Request, res: Response, next: NextFunction): void => {
        if (req.principal?.kind === 'service') {
            next();
            return;
        }

        const paramValue = req.params[paramName];

        //  주체와 파라미터가 둘 다 비어 있으면(배선 누락 + 이름 오타) 우연히 같아져 통과해버릴 수
        //  있다 — 값이 실제로 있는지부터 확인해 안전한 쪽(거부)으로 닫는다.
        if (req.principal?.kind !== 'user' || !paramValue || paramValue !== req.principal.userId) {
            res.status(403).json({ message: 'Forbidden.' });
            return;
        }

        next();
    };
}
```

- [ ] **Step 4: 서브패스로 내보낸다**

`lop-backend/packages/server-core/src/entries/auth.ts` 를 아래로 바꾼다:

```ts
export * from '../auth/token';
export * from '../middlewares/auth.middleware';
export * from '../middlewares/principal.middleware';
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/server-core && npx jest
```

Expected: PASS — 새 파일 11개 포함, server-core 전체 통과

- [ ] **Step 6: 강등 방지가 실제로 동작하는지 사보타주로 확인한다**

`principal.middleware.ts`의 키 분기를 잠시 아래처럼 바꿔(키가 틀리면 유저 경로로 흘리도록) 테스트를 돌린다:

```ts
    if (typeof req.headers[INTERNAL_KEY_HEADER] === 'string' && req.headers[INTERNAL_KEY_HEADER] === process.env.INTERNAL_API_KEY) {
```

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/server-core && npx jest src/middlewares/__tests__/principal.middleware.test.ts
```

Expected: FAIL — "키가 틀리면 유효한 토큰이 같이 있어도 401" 1건 실패.
확인 후 Step 3의 원래 코드로 되돌리고 다시 돌려 전부 통과하는 것을 확인한다.

- [ ] **Step 7: server-core를 빌드한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm --filter @lop/server-core build
```

Expected: 에러 없이 종료. (앱은 `dist/`를 읽으므로 이 단계를 빼면 뒤 태스크가 옛 코드를 본다.)

- [ ] **Step 8: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add packages/server-core/src/middlewares/principal.middleware.ts \
        packages/server-core/src/middlewares/__tests__/principal.middleware.test.ts \
        packages/server-core/src/entries/auth.ts
git commit -m "feat(server-core): 주체 판별 + 주체별 인가 미들웨어

키 헤더가 있으면 서비스, 없으면 유저 토큰으로 주체를 정한다. 키가 틀리면
유저 토큰으로 강등하지 않고 401로 끝내고, 키 헤더가 없을 때는
INTERNAL_API_KEY를 아예 읽지 않는다(그 값이 빠져도 클라 조회가 500이 되지 않도록)."
```

---

### Task 2: 서비스 간 호출용 공용 axios 클라이언트

**Files:**
- Create: `lop-backend/packages/server-core/src/http/internalHttpClient.ts`
- Create: `lop-backend/packages/server-core/src/http/__tests__/internalHttpClient.test.ts`
- Create: `lop-backend/packages/server-core/src/entries/http.ts`
- Modify: `lop-backend/packages/server-core/package.json`
- Modify: `lop-backend/packages/server-core/jest.config.js`

**Interfaces:**
- Produces: `internalHttpClient` — axios 인스턴스. `@lop/server-core/http`로 내보낸다. 모든 요청에 `x-internal-api-key` 헤더를 붙인다.

> jest 설정은 건드리지 않는다. `testMatch: ['<rootDir>/src/**/__tests__/**/*.test.ts']`가 이미 `src/http/__tests__/`를 잡는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`lop-backend/packages/server-core/src/http/__tests__/internalHttpClient.test.ts`:

```ts
import { internalHttpClient } from '../internalHttpClient';

//  실제 요청을 보내지 않고, 인스턴스에 걸린 요청 인터셉터만 직접 돌려 헤더를 확인한다.
async function 인터셉터를_통과시킨다(config: Record<string, unknown> = {}) {
    const handlers = (internalHttpClient.interceptors.request as unknown as {
        handlers: Array<{ fulfilled: (c: unknown) => unknown } | null>;
    }).handlers;

    let result: unknown = config;
    for (const handler of handlers) {
        if (handler) {
            result = await handler.fulfilled(result);
        }
    }
    return result as { headers?: Record<string, string> };
}

describe('internalHttpClient', () => {
    afterEach(() => { delete process.env.INTERNAL_API_KEY; });

    it('요청에 내부 키 헤더를 붙인다', async () => {
        process.env.INTERNAL_API_KEY = 'key-abc';

        const config = await 인터셉터를_통과시킨다();

        expect(config.headers?.['x-internal-api-key']).toBe('key-abc');
    });

    //  모듈을 로드할 때 한 번만 읽으면, .env를 나중에 읽는 진입점에서 빈 키가 굳어버린다.
    it('키를 요청 시점에 읽는다 — 모듈 로드 후에 세팅해도 반영된다', async () => {
        process.env.INTERNAL_API_KEY = 'key-first';
        const first = await 인터셉터를_통과시킨다();

        process.env.INTERNAL_API_KEY = 'key-second';
        const second = await 인터셉터를_통과시킨다();

        expect(first.headers?.['x-internal-api-key']).toBe('key-first');
        expect(second.headers?.['x-internal-api-key']).toBe('key-second');
    });

    it('기존 헤더를 지우지 않는다', async () => {
        process.env.INTERNAL_API_KEY = 'key-abc';

        const config = await 인터셉터를_통과시킨다({ headers: { 'content-type': 'application/json' } });

        expect(config.headers?.['content-type']).toBe('application/json');
        expect(config.headers?.['x-internal-api-key']).toBe('key-abc');
    });

    it('키가 없으면 헤더를 붙이지 않는다', async () => {
        const config = await 인터셉터를_통과시킨다();

        expect(config.headers?.['x-internal-api-key']).toBeUndefined();
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/server-core && npx jest src/http
```

Expected: FAIL — `Cannot find module '../internalHttpClient'`

- [ ] **Step 3: axios를 server-core 의존성에 추가한다**

`lop-backend/packages/server-core/package.json`의 `dependencies`에 아래 한 줄을 추가한다 (알파벳 순서상 `@lop/database` 다음):

```json
        "axios": "^0.26.1",
```

그리고 설치한다:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm install
```

- [ ] **Step 4: 클라이언트를 구현한다**

`lop-backend/packages/server-core/src/http/internalHttpClient.ts`:

```ts
import axios from 'axios';

const INTERNAL_KEY_HEADER = 'x-internal-api-key';

/**
 * 우리 서비스끼리 부를 때 쓰는 HTTP 클라이언트. 모든 요청에 내부 키를 붙인다.
 *
 * 클라이언트가 보는 API에는 쓰지 않는다 — 유저 요청에 서비스 권한을 실어 보내게 된다.
 */
export const internalHttpClient = axios.create();

internalHttpClient.interceptors.request.use(config => {
    //  키를 모듈 로드 때가 아니라 요청 때 읽는다. 진입점이 .env를 읽기 전에 이 모듈이 먼저
    //  로드될 수 있고, 그러면 빈 키가 그대로 굳는다.
    const key = process.env.INTERNAL_API_KEY;

    if (key) {
        config.headers = { ...config.headers, [INTERNAL_KEY_HEADER]: key };
    }

    return config;
});
```

- [ ] **Step 5: 서브패스 진입점을 만든다**

`lop-backend/packages/server-core/src/entries/http.ts`:

```ts
export { internalHttpClient } from '../http/internalHttpClient';
```

`lop-backend/packages/server-core/package.json`의 `exports`에 `"./auth"` 다음으로 추가한다:

```json
        "./http": {
            "types": "./dist/entries/http.d.ts",
            "default": "./dist/entries/http.js"
        },
```

같은 파일의 `typesVersions."*"`에도 추가한다 (`"auth"` 다음):

```json
            "http": [
                "dist/entries/http.d.ts"
            ]
```

> 루트(`index.ts`)가 아니라 서브패스로 내보내는 이유: 루트는 "import해도 외부 자원을 만들지 않는" 순수 계약만 담는 규칙인데, 이 모듈은 로드 시점에 axios 인스턴스를 만든다.

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/server-core && npx jest
```

Expected: PASS — server-core 전체 통과

- [ ] **Step 7: server-core를 빌드한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm --filter @lop/server-core build
```

Expected: 에러 없이 종료

- [ ] **Step 8: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add packages/server-core/src/http/ packages/server-core/src/entries/http.ts \
        packages/server-core/package.json pnpm-lock.yaml
git commit -m "feat(server-core): 서비스 간 호출용 내부 HTTP 클라이언트

모든 요청에 x-internal-api-key를 붙이는 axios 인스턴스. 키를 모듈 로드가 아니라
요청 시점에 읽는다 — 진입점이 .env를 읽기 전에 로드되면 빈 키가 굳기 때문."
```

---

### Task 3: 로비 서버 라우트 재편

**Files:**
- Create: `lop-backend/apps/lobby-server/src/routes/internal.route.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/user.route.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/user-location.route.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/user-stats.route.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/auth.route.ts`
- Modify: `lop-backend/apps/lobby-server/src/controllers/user.controller.ts`
- Modify: `lop-backend/apps/lobby-server/src/services/user.service.ts`
- Modify: `lop-backend/apps/lobby-server/src/main.ts`
- Modify: `lop-backend/apps/lobby-server/test/integration/orphanRoutes.integration.test.ts`
- Create: `lop-backend/apps/lobby-server/test/integration/userAccess.integration.test.ts`

**Interfaces:**
- Consumes: `authenticatePrincipal`, `requireSelfOrService`(`@lop/server-core/auth`), `internalApiKeyMiddleware`(`@lop/server-core/express`)
- Produces: 라우트 계약 —
  - `GET /user/:id`, `GET /user/:userId/location`, `GET /user/:userId/stats` = 주체별 인가
  - `GET /internal/user/findAll`, `PUT /internal/user/location`, `POST /internal/auth/introspect` = 키 필요
  - `GET /user/all`, `GET /user/username/:username` = 없어짐

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`lop-backend/apps/lobby-server/test/integration/userAccess.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import AuthRoute from '@routes/auth.route';
import UserRoute from '@routes/user.route';
import UserLocationRoute from '@routes/user-location.route';
import UserStatsRoute from '@routes/user-stats.route';
import InternalRoute from '@routes/internal.route';
import { rawPrisma, resetTables, connectRedis, disconnectAll } from './db';

const app = new App([
    new AuthRoute(), new UserRoute(), new UserLocationRoute(), new UserStatsRoute(), new InternalRoute(),
]).getServer();

const KEY = 'test-internal-key';

let testIp = 0;

async function 계정을_만든다(): Promise<{ userId: string; accessToken: string }> {
    testIp += 1;
    const response = await request(app)
        .post('/auth/anonymous')
        .set('X-Forwarded-For', `198.51.100.${testIp}`)
        .send();
    return { userId: response.body.userId, accessToken: response.body.accessToken };
}

describe('유저 조회 접근 제어', () => {
    beforeAll(connectRedis);
    beforeEach(async () => {
        process.env.INTERNAL_API_KEY = KEY;
        await resetTables();
    });
    afterAll(disconnectAll);

    it('토큰이 없으면 401', async () => {
        const { userId } = await 계정을_만든다();

        const response = await request(app).get(`/user/${userId}`).send();

        expect(response.status).toBe(401);
    });

    it('자기 것은 200', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app).get(`/user/${userId}`).set('Authorization', `Bearer ${accessToken}`).send();

        expect(response.status).toBe(200);
    });

    it('남의 것은 403', async () => {
        const 나 = await 계정을_만든다();
        const 남 = await 계정을_만든다();

        const response = await request(app).get(`/user/${남.userId}`).set('Authorization', `Bearer ${나.accessToken}`).send();

        expect(response.status).toBe(403);
    });

    //  매치메이킹이 매칭 상대를 고르려면 남의 유저를 읽어야 한다.
    it('내부 키로는 남의 것도 200', async () => {
        const 남 = await 계정을_만든다();

        const response = await request(app).get(`/user/${남.userId}`).set('X-Internal-Api-Key', KEY).send();

        expect(response.status).toBe(200);
    });

    it('위치 조회도 남의 것은 403, 내부 키는 통과', async () => {
        const 나 = await 계정을_만든다();
        const 남 = await 계정을_만든다();

        const 거부 = await request(app).get(`/user/${남.userId}/location`).set('Authorization', `Bearer ${나.accessToken}`).send();
        const 허용 = await request(app).get(`/user/${남.userId}/location`).set('X-Internal-Api-Key', KEY).send();

        expect(거부.status).toBe(403);
        expect(허용.status).toBe(200);
    });

    it('전적 조회도 남의 것은 403', async () => {
        const 나 = await 계정을_만든다();
        const 남 = await 계정을_만든다();

        const response = await request(app)
            .get(`/user/${남.userId}/stats?queueId=1`)
            .set('Authorization', `Bearer ${나.accessToken}`)
            .send();

        expect(response.status).toBe(403);
    });
});

describe('서비스 전용 라우트', () => {
    beforeAll(connectRedis);
    beforeEach(async () => {
        process.env.INTERNAL_API_KEY = KEY;
        await resetTables();
    });
    afterAll(disconnectAll);

    it('PUT /internal/user/location 은 키가 없으면 401', async () => {
        const response = await request(app).put('/internal/user/location').send({ userId: 'someone', where: 0 });

        expect(response.status).toBe(401);
    });

    it('GET /internal/user/findAll 은 키가 없으면 401', async () => {
        const response = await request(app).get('/internal/user/findAll').send();

        expect(response.status).toBe(401);
    });

    //  유저 토큰으로는 서비스 전용 동작에 닿을 수 없어야 한다.
    it('유효한 유저 토큰만으로는 내부 라우트에 못 들어간다', async () => {
        const { accessToken } = await 계정을_만든다();

        const response = await request(app).get('/internal/user/findAll').set('Authorization', `Bearer ${accessToken}`).send();

        expect(response.status).toBe(401);
    });

    it('옛 경로 PUT /user/location 은 존재하지 않는다', async () => {
        const response = await request(app).put('/user/location').send({ userId: 'someone', where: 0 });

        expect(response.status).toBe(404);
    });

    it('옛 경로 GET /user/findAll 은 존재하지 않는다', async () => {
        const response = await request(app).get('/user/findAll').send();

        expect(response.status).toBe(404);
    });

    it('GET /user/all 은 존재하지 않는다', async () => {
        const response = await request(app).get('/user/all').set('X-Internal-Api-Key', KEY).send();

        expect(response.status).toBe(404);
        expect(await rawPrisma.user.count()).toBe(0);
    });

    it('GET /user/username/:username 은 존재하지 않는다', async () => {
        const response = await request(app).get('/user/username/someone').set('X-Internal-Api-Key', KEY).send();

        expect(response.status).toBe(404);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/lobby-server && npx jest test/integration/userAccess.integration.test.ts
```

Expected: FAIL — `Cannot find module '@routes/internal.route'`

- [ ] **Step 3: 서비스 전용 라우터를 만든다**

`lop-backend/apps/lobby-server/src/routes/internal.route.ts`:

```ts
import { Router } from 'express';
import { Routes } from '@lop/server-core';
import { internalApiKeyMiddleware, validationMiddleware } from '@lop/server-core/express';
import UserController from '@controllers/user.controller';
import UserLocationController from '@controllers/user-location.controller';
import AuthController from '@controllers/auth.controller';
import { UpdateUserLocationDto } from '@dtos/user-location.dto';
import { IntrospectRequestDto } from '@dtos/auth.dto';
import { introspectRateLimit } from '@middlewares/authRateLimit';

/** 우리 서비스만 부르는 동작들. 유저가 부를 이유가 없어 키만 요구한다. */
class InternalRoute implements Routes {
    public path = '/internal';
    public router = Router();
    public userController = new UserController();
    public userLocationController = new UserLocationController();
    public authController = new AuthController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        //  introspect만 아래 use보다 앞에 둔다. 레이트리밋이 "키가 틀린 시도"를 세야 하는데,
        //  use가 먼저 걸리면 그 요청이 리밋에 닿기 전에 401로 끊긴다(1c에서 정한 순서).
        this.router.post(
            `${this.path}/auth/introspect`,
            introspectRateLimit,
            internalApiKeyMiddleware,
            validationMiddleware(IntrospectRequestDto, 'body'),
            this.authController.introspect,
        );

        //  나머지는 접두사 단위로 한 번에 건다 — 나중에 내부 라우트를 추가할 때 미들웨어를
        //  빠뜨려도 구멍이 나지 않는다. 경로를 지정한 use라야 이 접두사에만 걸린다
        //  (App이 라우터를 '/'에 마운트하므로 경로 없는 use는 앱의 모든 요청에 걸린다).
        this.router.use(`${this.path}`, internalApiKeyMiddleware);

        this.router.get(`${this.path}/user/findAll`, this.userController.findAllUsers);
        this.router.put(
            `${this.path}/user/location`,
            validationMiddleware(UpdateUserLocationDto, 'body'),
            this.userLocationController.updateUserLocation,
        );
    }
}

export default InternalRoute;
```

> ⚠️ 등록 **순서가 곧 실행 순서**다. `router.use(path, mw)`는 그 뒤에 등록된 라우트에만 걸린다 — `findAll`/`location`보다 뒤로 내리면 아무것도 막지 못한다. 반대로 introspect를 `use` 뒤로 내리면 레이트리밋이 무력해진다.

- [ ] **Step 4: 공개 라우트에 인가를 붙이고 고아를 지운다**

`lop-backend/apps/lobby-server/src/routes/user.route.ts` 전체를 아래로 바꾼다:

```ts
import { Router } from 'express';
import UserController from '@controllers/user.controller';
import { Routes } from '@lop/server-core';
import { authenticatePrincipal, requireSelfOrService } from '@lop/server-core/auth';

class UserRoute implements Routes {
    public path = '/user';
    public router = Router();
    public userController = new UserController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        //  유저는 자기 것만, 매치메이킹 같은 서비스는 전부 읽는다.
        this.router.get(`${this.path}/:id`, authenticatePrincipal, requireSelfOrService('id'), this.userController.getUserById);
    }
}

export default UserRoute;
```

`lop-backend/apps/lobby-server/src/routes/user-location.route.ts` 전체를 아래로 바꾼다:

```ts
import { Router } from 'express';
import UserLocationController from '@controllers/user-location.controller';
import { Routes } from '@lop/server-core';
import { authenticatePrincipal, requireSelfOrService } from '@lop/server-core/auth';

class UserLocationRoute implements Routes {
    public path = '/user';
    public router = Router();
    public userLocationController = new UserLocationController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        this.router.get(
            `${this.path}/:userId/location`,
            authenticatePrincipal,
            requireSelfOrService('userId'),
            this.userLocationController.getOrCreateUserLocationById,
        );
    }
}

export default UserLocationRoute;
```

`lop-backend/apps/lobby-server/src/routes/user-stats.route.ts` 전체를 아래로 바꾼다:

```ts
import { Router } from 'express';
import UserStatsController from '@controllers/user-stats.controller';
import { Routes } from '@lop/server-core';
import { authenticatePrincipal, requireSelfOrService } from '@lop/server-core/auth';

class UserStatsRoute implements Routes {
    public path = '/user';
    public router = Router();
    public userStatsController = new UserStatsController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        this.router.get(
            `${this.path}/:userId/stats`,
            authenticatePrincipal,
            requireSelfOrService('userId'),
            this.userStatsController.getUserStatsById,
        );
    }
}

export default UserStatsRoute;
```

- [ ] **Step 5: introspect를 옛 경로에서 뗀다**

`lop-backend/apps/lobby-server/src/routes/auth.route.ts`에서 `POST /auth/introspect` 등록 블록(주석 포함)과 이제 안 쓰는 import(`internalApiKeyMiddleware`, `IntrospectRequestDto`, `introspectRateLimit`)를 지운다. 결과:

```ts
import { Router } from 'express';
import { Routes } from '@lop/server-core';
import { validationMiddleware } from '@lop/server-core/express';
import { LoginRequestDto } from '@dtos/auth.dto';
import AuthController from '@controllers/auth.controller';
import { anonymousRateLimit, loginRateLimit } from '@middlewares/authRateLimit';

class AuthRoute implements Routes {
    public path = '/auth';
    public router = Router();
    public authController = new AuthController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        this.router.post(`${this.path}/anonymous`, anonymousRateLimit, this.authController.signInAnonymous);
        this.router.post(`${this.path}/login`, loginRateLimit, validationMiddleware(LoginRequestDto, 'body'), this.authController.login);
    }
}

export default AuthRoute;
```

- [ ] **Step 6: 고아 컨트롤러·서비스 메서드를 지운다**

`lop-backend/apps/lobby-server/src/controllers/user.controller.ts`에서 `getUsers`와 `getUserByUsername` 메서드를 지운다. `getUserById`와 `findAllUsers`는 남긴다.

`lop-backend/apps/lobby-server/src/services/user.service.ts`에서 `findAllUsers()`와 `findUserByUsername(...)`를 지운다. `findAllUsersById`와 `findUserById`는 남긴다.

지운 뒤 남는 사용처가 없는지 확인한다:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && grep -rn "getUsers\|getUserByUsername\|findUserByUsername\|findAllUsers()" --include="*.ts" apps packages | grep -v node_modules | grep -v dist
```

Expected: 출력 없음 (`findAllUsersById`는 다른 이름이라 걸리지 않는다)

- [ ] **Step 7: main.ts에 새 라우터를 등록한다**

`lop-backend/apps/lobby-server/src/main.ts`에 import를 추가하고 App 배열에 넣는다:

```ts
import InternalRoute from '@routes/internal.route';
```

```ts
        const app = new App([new IndexRoute(), new AuthRoute(), new UserRoute(), new UserLocationRoute(), new UserStatsRoute(), new LobbyRoute(), new InternalRoute()]);
```

- [ ] **Step 8: 기존 고아 라우트 테스트의 대조군을 고친다**

`lop-backend/apps/lobby-server/test/integration/orphanRoutes.integration.test.ts`의 마지막 대조군 테스트가 `GET /user/:id`에 토큰 없이 200을 기대한다 — 이제 401이다. 그 테스트를 아래로 바꾼다:

```ts
    //  양성 대조군. 위 세 개의 404가 "라우트가 진짜 지워졌다"가 아니라 "UserRoute를 이 app에
    //  안 올렸다"에서 나온 착시일 수 있다 — 남아 있는 조회 라우트(GET /user/:id)가 토큰을
    //  요구하는 응답(401)을 주는지 확인해서, UserRoute가 실제로 마운트돼 있음을 증명한다.
    //  404가 아니라 401이라는 점이 곧 "라우트는 있다"는 증거다.
    it('(대조군) GET /user/:id 는 남아 있다', async () => {
        const 계정 = await request(app).post('/auth/anonymous').set('X-Forwarded-For', 다른_ip로()).send();

        const response = await request(app).get(`/user/${계정.body.userId}`).send();

        expect(response.status).toBe(401);
    });
```

- [ ] **Step 9: 로비 서버 전체 테스트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/lobby-server && npx jest
```

Expected: PASS — 전부 통과. 실패하면 그 파일을 고친다 (introspect 통합 테스트가 옛 경로 `/auth/introspect`를 쓰고 있으면 `/internal/auth/introspect`로 바꾼다).

- [ ] **Step 10: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add apps/lobby-server/
git commit -m "feat(lobby): 유저 조회는 주체별 인가, 서비스 전용은 /internal로

GET /user/:id, /location, /stats 는 경로를 유지하고 유저는 본인만·서비스는 전부
읽게 했다. findAll·location 갱신·introspect는 /internal로 옮겨 키만 요구한다.
호출자가 없던 /user/all, /user/username/:username 은 컨트롤러·서비스까지 지웠다."
```

---

### Task 4: 매치메이킹 서버 라우트 재편

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/policies/matchAccess.ts`
- Create: `lop-backend/apps/matchmaking-server/src/policies/__tests__/matchAccess.test.ts`
- Create: `lop-backend/apps/matchmaking-server/src/routes/internal.route.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/routes/match.route.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/controllers/match.controller.ts`
- Delete: `lop-backend/apps/matchmaking-server/src/routes/matchmakingTicket.route.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/main.ts`

**Interfaces:**
- Consumes: `authenticatePrincipal`(`@lop/server-core/auth`), `Principal` 타입, `internalApiKeyMiddleware`(`@lop/server-core/express`)
- Produces:
  - `canReadMatch(principal: Principal | undefined, playerList: string[] | undefined): boolean`
  - 라우트 계약 — `GET /match/:id` = 주체별 인가, `GET /internal/matchmaking-ticket/:id` = 키 필요, `GET /matchmaking-ticket/:id` = 없어짐

- [ ] **Step 1: 실패하는 정책 테스트를 쓴다**

`lop-backend/apps/matchmaking-server/src/policies/__tests__/matchAccess.test.ts`:

```ts
import { canReadMatch } from '../matchAccess';

describe('canReadMatch', () => {
    it('서비스 주체는 남의 매치도 읽는다', () => {
        expect(canReadMatch({ kind: 'service' }, ['user-1', 'user-2'])).toBe(true);
    });

    it('유저 주체는 자기가 낀 매치만 읽는다', () => {
        expect(canReadMatch({ kind: 'user', userId: 'user-1' }, ['user-1', 'user-2'])).toBe(true);
    });

    it('유저 주체가 참가자가 아니면 읽을 수 없다', () => {
        expect(canReadMatch({ kind: 'user', userId: 'user-9' }, ['user-1', 'user-2'])).toBe(false);
    });

    //  존재하지 않는 매치도 "볼 수 없다"로 통일한다 — 서비스는 매치가 없어도 응답을
    //  그대로 받아야 하므로 서비스 분기가 먼저 걸린다.
    it('매치가 없으면 유저는 읽을 수 없다', () => {
        expect(canReadMatch({ kind: 'user', userId: 'user-1' }, undefined)).toBe(false);
    });

    it('매치가 없어도 서비스는 통과한다', () => {
        expect(canReadMatch({ kind: 'service' }, undefined)).toBe(true);
    });

    it('주체가 없으면 읽을 수 없다', () => {
        expect(canReadMatch(undefined, ['user-1'])).toBe(false);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/matchmaking-server && npx jest src/policies
```

Expected: FAIL — `Cannot find module '../matchAccess'`

- [ ] **Step 3: 정책 함수를 구현한다**

`lop-backend/apps/matchmaking-server/src/policies/matchAccess.ts`:

```ts
import { Principal } from '@lop/server-core/auth';

/**
 * 이 주체가 이 매치를 읽어도 되는가.
 *
 * 매치메이킹·게임서버는 남의 매치를 읽어야 동작하므로 서비스 주체는 전부 통과한다.
 * 유저는 자기가 참가한 매치만 볼 수 있다.
 */
export function canReadMatch(principal: Principal | undefined, playerList: string[] | undefined): boolean {
    if (principal?.kind === 'service') {
        return true;
    }

    if (principal?.kind !== 'user' || !playerList) {
        return false;
    }

    return playerList.includes(principal.userId);
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/matchmaking-server && npx jest src/policies
```

Expected: PASS — 6개 통과

- [ ] **Step 5: 컨트롤러에서 정책을 적용한다**

`lop-backend/apps/matchmaking-server/src/controllers/match.controller.ts` 전체를 아래로 바꾼다:

```ts
import { NextFunction, Request, Response } from 'express';
import { ResponseCode } from '@lop/server-core';
import MatchService from '@services/match.service';
import { canReadMatch } from '@src/policies/matchAccess';

class MatchController {
    private matchService = new MatchService();

    public getMatchById = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const matchId: string = req.params.id;
            const response = await this.matchService.findMatchById(matchId);

            //  볼 수 없는 매치는 "없는 매치"와 똑같이 답한다. 403과 없음을 구분해 주면
            //  매치 id를 넣어보는 것만으로 그 매치의 존재 여부를 알아낼 수 있다.
            if (canReadMatch(req.principal, response.match?.playerList) === false) {
                res.status(200).json({ code: ResponseCode.MATCH_NOT_EXIST });
                return;
            }

            res.status(200).json(response);
        } catch (error) {
            next(error);
        }
    };
}

export default MatchController;
```

> `@src/*` 별칭은 `tsconfig.json`과 `jest.config.js` 양쪽에 이미 있다 — 그대로 쓰면 된다.

- [ ] **Step 6: 매치 라우트에 인가를 붙인다**

`lop-backend/apps/matchmaking-server/src/routes/match.route.ts`의 `initializeRoutes`를 아래로 바꾸고 import를 추가한다:

```ts
import { authenticatePrincipal } from '@lop/server-core/auth';
```

```ts
    private initializeRoutes() {
        //  참가자인지는 매치를 읽어봐야 알 수 있어 컨트롤러에서 판단한다 — 여기서는 주체만 정한다.
        this.router.get(`${this.path}/:id`, authenticatePrincipal, this.matchController.getMatchById);
    }
```

- [ ] **Step 7: 티켓 조회를 내부 라우터로 옮긴다**

`lop-backend/apps/matchmaking-server/src/routes/internal.route.ts`:

```ts
import { Router } from 'express';
import { Routes } from '@lop/server-core';
import { internalApiKeyMiddleware } from '@lop/server-core/express';
import MatchmakingTicketController from '@controllers/matchmakingTicket.controller';

/** 우리 서비스만 부르는 동작들. 유저가 부를 이유가 없어 키만 요구한다. */
class InternalRoute implements Routes {
    public path = '/internal';
    public router = Router();
    public matchmakingTicketController = new MatchmakingTicketController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        //  경로를 지정한 use라야 이 접두사에만 걸린다. App이 라우터를 '/'에 마운트하므로
        //  경로 없이 use하면 앱의 모든 요청에 키를 요구하게 된다. 라우트마다 붙이지 않는
        //  이유는 나중에 내부 라우트를 추가할 때 빠뜨려도 구멍이 나지 않게 하려는 것.
        this.router.use(`${this.path}`, internalApiKeyMiddleware);

        this.router.get(`${this.path}/matchmaking-ticket/:id`, this.matchmakingTicketController.getMatchmakingTicketById);
    }
}

export default InternalRoute;
```

그리고 `lop-backend/apps/matchmaking-server/src/routes/matchmakingTicket.route.ts`를 지운다:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && git rm apps/matchmaking-server/src/routes/matchmakingTicket.route.ts
```

- [ ] **Step 8: main.ts를 고친다**

`lop-backend/apps/matchmaking-server/src/main.ts`에서 `MatchmakingTicketRoute` import와 사용을 지우고 `InternalRoute`로 바꾼다. `validateEnv`에 `INTERNAL_API_KEY`를 추가한다:

```ts
import InternalRoute from '@routes/internal.route';
```

```ts
        //  이 앱은 토큰을 직접 검증하고(AUTH_JWT_SECRET), 서비스 간 호출을 보내고 받는다
        //  (INTERNAL_API_KEY). 없으면 런타임에 401로 조용히 새므로 부팅 때 죽인다.
        validateEnv({ AUTH_JWT_SECRET: str(), INTERNAL_API_KEY: str() });
```

```ts
        const app = new App([new IndexRoute(), new MatchRoute(), new MatchmakingRoute(), new InternalRoute()]);
```

- [ ] **Step 9: 디렉터도 키를 요구하게 한다**

`lop-backend/apps/matchmaking-server/src/director.ts`의 `validateEnv();`를 아래로 바꾸고 import를 추가한다:

```ts
import { str } from 'envalid';
```

```ts
        //  디렉터는 방 생성(POST /internal/room)과 위치 갱신(PUT /internal/user/location)을
        //  HTTP로 부른다. 키가 없으면 매칭이 통째로 멈추므로 부팅 때 죽인다.
        validateEnv({ INTERNAL_API_KEY: str() });
```

- [ ] **Step 10: 매치메이킹 전체 테스트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/matchmaking-server && npx jest
```

Expected: PASS — 전부 통과

- [ ] **Step 11: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add apps/matchmaking-server/
git commit -m "feat(matchmaking): 매치 조회 주체별 인가 + 티켓 조회를 /internal로

GET /match/:id 는 서비스는 전부, 유저는 자기가 낀 매치만 읽는다. 볼 수 없는
매치는 없는 매치와 같은 응답을 준다 — 구분해 주면 id를 넣어보는 것만으로
존재 여부를 알아낼 수 있다. 매치메이킹 서버와 디렉터가 부팅 때 키를 요구한다."
```

---

### Task 5: 룸 서버 라우트 재편

**Files:**
- Create: `lop-backend/apps/room-server/src/routes/internal.route.ts`
- Modify: `lop-backend/apps/room-server/src/routes/room.route.ts`
- Modify: `lop-backend/apps/room-server/src/controllers/room.controller.ts`
- Modify: `lop-backend/apps/room-server/src/services/room.service.ts`
- Modify: `lop-backend/apps/room-server/src/main.ts`
- Create: `lop-backend/apps/room-server/src/routes/__tests__/roomRoutes.test.ts`
- Modify: `lop-backend/apps/room-server/package.json` (supertest 추가)

**Interfaces:**
- Consumes: `authMiddleware`(`@lop/server-core/auth`), `internalApiKeyMiddleware`(`@lop/server-core/express`)
- Produces: 라우트 계약 —
  - `GET /room/:id/joinable` = 로그인 필요
  - `GET /internal/room/:id`, `PUT /internal/room/status`, `PUT /internal/room/heartbeat/:id`, `POST /internal/room`, `DELETE /internal/room/:id` = 키 필요
  - `GET /room/all` = 없어짐

- [ ] **Step 1: supertest를 devDependency로 추가한다**

room-server에는 통합 테스트 디렉터리(`test/`)도 supertest도 없다 — 테스트는 `src/**/__tests__/`에 있다. 라우트가 열려 있는지만 보면 되므로 DB 없이 401/404만 확인한다.

`lop-backend/apps/room-server/package.json`의 `devDependencies`에 추가한다 (다른 앱과 같은 버전 계열):

```json
        "@types/supertest": "^6.0.2",
        "supertest": "^7.0.0",
```

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm install
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`lop-backend/apps/room-server/src/routes/__tests__/roomRoutes.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import RoomRoute from '@routes/room.route';
import InternalRoute from '@routes/internal.route';

const app = new App([new RoomRoute(), new InternalRoute()]).getServer();

describe('룸 라우트 접근 제어', () => {
    beforeEach(() => {
        process.env.INTERNAL_API_KEY = 'test-internal-key';
        process.env.AUTH_JWT_SECRET = 'test-secret-0123456789';
    });

    it('PUT /internal/room/status 는 키가 없으면 401', async () => {
        const response = await request(app).put('/internal/room/status').send({ roomId: 'r1', status: 4 });

        expect(response.status).toBe(401);
    });

    it('PUT /internal/room/heartbeat/:id 는 키가 없으면 401', async () => {
        const response = await request(app).put('/internal/room/heartbeat/r1').send();

        expect(response.status).toBe(401);
    });

    it('POST /internal/room 은 키가 없으면 401', async () => {
        const response = await request(app).post('/internal/room').send({ matchId: 'm1' });

        expect(response.status).toBe(401);
    });

    it('DELETE /internal/room/:id 는 키가 없으면 401', async () => {
        const response = await request(app).delete('/internal/room/r1').send();

        expect(response.status).toBe(401);
    });

    it('GET /internal/room/:id 는 키가 없으면 401', async () => {
        const response = await request(app).get('/internal/room/r1').send();

        expect(response.status).toBe(401);
    });

    it('옛 경로 PUT /room/status 는 존재하지 않는다', async () => {
        const response = await request(app).put('/room/status').send({ roomId: 'r1', status: 4 });

        expect(response.status).toBe(404);
    });

    it('옛 경로 PUT /room/heartbeat/:id 는 존재하지 않는다', async () => {
        const response = await request(app).put('/room/heartbeat/r1').send();

        expect(response.status).toBe(404);
    });

    it('옛 경로 POST /room 은 존재하지 않는다', async () => {
        const response = await request(app).post('/room').send({ matchId: 'm1' });

        expect(response.status).toBe(404);
    });

    it('GET /room/all 은 존재하지 않는다', async () => {
        const response = await request(app).get('/room/all').send();

        expect(response.status).toBe(404);
    });

    it('GET /room/:id/joinable 은 토큰이 없으면 401', async () => {
        const response = await request(app).get('/room/r1/joinable').send();

        expect(response.status).toBe(401);
    });
});
```

- [ ] **Step 3: 테스트가 실패하는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/room-server && npx jest roomRoutes
```

Expected: FAIL — `Cannot find module '@routes/internal.route'`

- [ ] **Step 4: 서비스 전용 라우터를 만든다**

`lop-backend/apps/room-server/src/routes/internal.route.ts`:

```ts
import { Router } from 'express';
import { Routes } from '@lop/server-core';
import { internalApiKeyMiddleware, validationMiddleware } from '@lop/server-core/express';
import RoomController from '@controllers/room.controller';
import { CreateRoomDto, UpdateRoomStatusDto } from '@dtos/room.dto';

/** 게임서버와 매치메이킹 디렉터만 부르는 동작들. 유저가 부를 이유가 없어 키만 요구한다. */
class InternalRoute implements Routes {
    public path = '/internal';
    public router = Router();
    public roomController = new RoomController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        //  경로를 지정한 use라야 이 접두사에만 걸린다. App이 라우터를 '/'에 마운트하므로
        //  경로 없이 use하면 앱의 모든 요청에 키를 요구하게 된다. 라우트마다 붙이지 않는
        //  이유는 나중에 내부 라우트를 추가할 때 빠뜨려도 구멍이 나지 않게 하려는 것.
        this.router.use(`${this.path}`, internalApiKeyMiddleware);

        this.router.put(`${this.path}/room/status`, validationMiddleware(UpdateRoomStatusDto, 'body'), this.roomController.updateRoomStatus);
        this.router.put(`${this.path}/room/heartbeat/:id`, this.roomController.heartbeat);
        this.router.get(`${this.path}/room/:id`, this.roomController.getRoomById);
        this.router.post(`${this.path}/room`, validationMiddleware(CreateRoomDto, 'body'), this.roomController.createRoom);
        this.router.delete(`${this.path}/room/:id`, this.roomController.deleteRoom);
    }
}

export default InternalRoute;
```

- [ ] **Step 5: 공개 라우트를 줄인다**

`lop-backend/apps/room-server/src/routes/room.route.ts` 전체를 아래로 바꾼다 (파일 끝의 설명 주석 블록은 그대로 남긴다):

```ts
import { Router } from 'express';
import RoomController from '@controllers/room.controller';
import { Routes } from '@lop/server-core';
import { authMiddleware } from '@lop/server-core/auth';

class RoomRoute implements Routes {
    public path = '/room';
    public router = Router();
    public roomController = new RoomController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        //  방 주인이 누구인지 이 파라미터로는 알 수 없어 소유권까지는 못 본다 — 로그인만 확인한다.
        //  실제 방어는 방 접속 인증(게임서버가 토큰을 검사)이 한다.
        this.router.get(`${this.path}/:id/joinable`, authMiddleware, this.roomController.isRoomJoinable);
    }
}

export default RoomRoute;
```

- [ ] **Step 6: 고아 컨트롤러·서비스 메서드를 지운다**

`lop-backend/apps/room-server/src/controllers/room.controller.ts`에서 `getAllRooms` 메서드를 지운다.
`lop-backend/apps/room-server/src/services/room.service.ts`에서 그 컨트롤러가 부르던 `findAllRooms()`를 지운다.

지운 뒤 남는 사용처가 없는지 확인한다:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && grep -rn "getAllRooms\|findAllRooms\|GetAllRoomsResponseDto" --include="*.ts" apps packages | grep -v node_modules | grep -v dist
```

Expected: DTO 정의(`room.dto.ts`)만 남거나 출력 없음. DTO만 남으면 그 DTO도 지운다.

- [ ] **Step 7: main.ts를 고친다**

`lop-backend/apps/room-server/src/main.ts`에 import를 추가하고, App 배열에 넣고, `validateEnv`를 고친다:

```ts
import { str } from 'envalid';
import InternalRoute from '@routes/internal.route';
```

```ts
        //  이 앱은 서비스 간 호출을 보내고 받는다. 키가 없으면 런타임에 401로 조용히
        //  새므로 부팅 때 죽인다. 토큰 검증(joinable)도 하므로 서명키가 필요하다.
        validateEnv({ AUTH_JWT_SECRET: str(), INTERNAL_API_KEY: str() });
```

```ts
        const app = new App([new IndexRoute(), new RoomRoute(), new InternalRoute()]);
```

- [ ] **Step 8: 룸 서버 전체 테스트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/apps/room-server && npx jest
```

Expected: PASS — 전부 통과

- [ ] **Step 9: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add apps/room-server/
git commit -m "feat(room): 서비스 전용 동작을 /internal로, joinable은 로그인 요구

방 생성·삭제·상태·하트비트·조회를 /internal로 옮겨 키만 요구한다. joinable은
소유권을 볼 수 없어(파라미터가 roomId) 로그인까지만 확인한다 — 읽기처럼 생겼지만
끊긴 방을 Error로 저장하고 게임서버 주소를 준다. 호출자가 없던 /room/all은 지웠다."
```

---

### Task 6: 백엔드 호출부를 내부 클라이언트와 새 경로로 전환

**Files:**
- Modify: `lop-backend/apps/lobby-server/src/services/httpServices/matchmakingServer.service.ts`
- Modify: `lop-backend/apps/lobby-server/src/services/httpServices/roomServer.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/httpServices/roomServer.service.ts`
- Modify: `lop-backend/apps/room-server/src/services/httpServices/lobbyServer.service.ts`
- Modify: `lop-backend/apps/room-server/src/services/httpServices/matchmaking-server.service.ts`

**Interfaces:**
- Consumes: `internalHttpClient`(`@lop/server-core/http`)
- Produces: 없음 (내부 배선만)

- [ ] **Step 1: 바꿀 자리를 전부 뽑는다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && grep -rn "axios\.\|const url" apps/*/src/services/httpServices/*.service.ts
```

출력된 목록을 기준으로 아래 Step 2~3을 적용한다. 총 14곳의 `axios.*` 호출이 있어야 한다.

- [ ] **Step 2: 여섯 파일의 import와 호출을 바꾼다**

각 파일에서:

```ts
import axios from 'axios';
```

를

```ts
import { internalHttpClient } from '@lop/server-core/http';
```

로 바꾸고, 본문의 `axios.get(`/`axios.put(`/`axios.post(`/`axios.delete(`를 각각 `internalHttpClient.get(`/`internalHttpClient.put(`/`internalHttpClient.post(`/`internalHttpClient.delete(`로 바꾼다.

- [ ] **Step 3: URL을 새 경로로 바꾼다**

아래 표대로 정확히 바꾼다. 표에 없는 URL(`/user/{id}`, `/user/{id}/location`, `/user/{id}/stats`, `/match/{id}`)은 **경로가 그대로**다 — 키만 실리면 된다.

| 파일 | 옛 URL | 새 URL |
|---|---|---|
| `matchmaking-server/.../lobbyServer.service.ts` | `/user/findAll` | `/internal/user/findAll` |
| `matchmaking-server/.../lobbyServer.service.ts` | `/user/location` | `/internal/user/location` |
| `matchmaking-server/.../roomServer.service.ts` | `/room` | `/internal/room` |
| `room-server/.../lobbyServer.service.ts` | `/user/findAll` | `/internal/user/findAll` |
| `room-server/.../lobbyServer.service.ts` | `/user/location` | `/internal/user/location` |
| `lobby-server/.../roomServer.service.ts` | `/room/${roomId}` | `/internal/room/${roomId}` |
| `lobby-server/.../matchmakingServer.service.ts` | `/matchmaking-ticket/${matchmakingTicketId}` | `/internal/matchmaking-ticket/${matchmakingTicketId}` |

`room-server/.../matchmaking-server.service.ts`는 `/match/${id}` 하나만 부른다 — **URL을 바꾸지 않는다.** import와 호출만 Step 2대로 바꾼다.

- [ ] **Step 4: 옛 경로와 남은 axios 직접 호출이 없는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
echo "--- httpServices에 남은 axios 직접 호출 (0줄이어야 함) ---"
grep -rn "axios\." apps/lobby-server/src apps/matchmaking-server/src apps/room-server/src --include="*.ts" | grep -v node_modules
echo "--- 내부 클라이언트가 부르는 URL 전수 (표와 대조) ---"
grep -rn "const url" apps/lobby-server/src/services/httpServices apps/matchmaking-server/src/services/httpServices apps/room-server/src/services/httpServices
```

Expected: 첫 목록은 0줄. 두 번째 목록의 URL이 Step 3의 표와 정확히 일치하고, `findAll`·`user/location`·`/room`(생성)·`/room/{id}`·`matchmaking-ticket`이 전부 `/internal`로 시작한다.

- [ ] **Step 5: 세 앱을 빌드해 타입 오류가 없는지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm --filter lobby-server --filter matchmaking-server --filter room-server build
```

Expected: 에러 없이 종료. `@lop/server-core/http`를 못 찾는다는 오류가 나면 Task 2의 `exports`/`typesVersions` 추가가 빠졌거나 server-core를 빌드하지 않은 것이다 — `pnpm --filter @lop/server-core build`를 먼저 돌린다.

> 앱 이름이 `lobby-server`가 아닐 수 있다. 확인: `grep -h '"name"' apps/*/package.json`

- [ ] **Step 6: 세 앱 테스트를 돌린다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && pnpm test
```

Expected: PASS — turbo가 모든 패키지 테스트를 돌린다. 전부 통과.

- [ ] **Step 7: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git add apps/lobby-server/src/services apps/matchmaking-server/src/services apps/room-server/src/services
git commit -m "refactor(backend): 서비스 간 호출을 내부 클라이언트와 /internal 경로로

axios 직접 호출 14곳을 키를 자동으로 붙이는 공용 클라이언트로 바꾸고, 옮겨간
라우트의 URL을 /internal로 맞췄다. 조회 4개(user/:id, location, stats, match/:id)는
경로가 그대로고 키만 실린다."
```

---

### Task 7: 게임서버용 ApiKeyHandler (GameFramework)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/ApiKeyHandler.cs`
- Create: `GameFramework/Tests/Runtime/Http/ApiKeyHandlerTests.cs`

**Interfaces:**
- Consumes: `DelegatingHandler`, `HttpMessageHandler`, `HttpRequestMessage`(`GameFramework.Http`)
- Produces: `ApiKeyHandler(HttpMessageHandler innerHandler, string headerName, Func<string> keyProvider) : DelegatingHandler`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Http/ApiKeyHandlerTests.cs`:

```csharp
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameFramework.Tests.Http
{
    public class ApiKeyHandlerTests
    {
        [UnityTest]
        public IEnumerator 키가_있으면_지정한_헤더에_붙인다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(new ApiKeyHandler(fake, "X-Internal-Api-Key", () => "secret-key"));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests[0].Headers["X-Internal-Api-Key"], Is.EqualTo("secret-key"));
        });

        //  빈 키를 보내면 서버가 "틀린 키"로 읽어 401을 준다 — 아예 안 붙이는 편이 낫다.
        [UnityTest]
        public IEnumerator 키가_비어_있으면_아무것도_붙이지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(new ApiKeyHandler(fake, "X-Internal-Api-Key", () => null));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests[0].Headers.ContainsKey("X-Internal-Api-Key"), Is.False);
        });

        //  환경변수가 프로세스 시작 뒤에 채워질 수 있어, 만들 때 한 번 읽으면 빈 값이 굳는다.
        [UnityTest]
        public IEnumerator 키를_보낼_때마다_다시_읽는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            string key = "first";
            var client = new HttpClient(new ApiKeyHandler(fake, "X-Internal-Api-Key", () => key));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));
            key = "second";
            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests[0].Headers["X-Internal-Api-Key"], Is.EqualTo("first"));
            Assert.That(fake.Requests[1].Headers["X-Internal-Api-Key"], Is.EqualTo("second"));
        });

        [Test]
        public void 키_공급자가_없으면_생성에서_던진다()
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");

            Assert.Throws<ArgumentNullException>(() => new ApiKeyHandler(fake, "X-Internal-Api-Key", null));
        }

        [Test]
        public void 헤더_이름이_비면_생성에서_던진다()
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");

            Assert.Throws<ArgumentException>(() => new ApiKeyHandler(fake, "", () => "k"));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

Unity 에디터에서 GameFramework를 여는 대신, 이 태스크는 **컴파일 확인만 코드 리뷰로** 하고 실행은 Step 5에서 사용자가 돌린다. 여기서는 파일이 없어 컴파일이 깨지는 것이 곧 실패 상태다.

- [ ] **Step 3: 핸들러를 구현한다**

`GameFramework/Runtime/Scripts/Http/ApiKeyHandler.cs`:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>요청마다 지정한 헤더에 API 키를 붙인다. 서비스끼리 부르는 호출에 쓴다.</summary>
    public class ApiKeyHandler : DelegatingHandler
    {
        private readonly string headerName;
        private readonly Func<string> keyProvider;

        public ApiKeyHandler(HttpMessageHandler innerHandler, string headerName, Func<string> keyProvider) : base(innerHandler)
        {
            if (string.IsNullOrEmpty(headerName))
            {
                throw new ArgumentException("헤더 이름이 필요하다.", nameof(headerName));
            }

            this.headerName = headerName;
            this.keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        }

        public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //  보낼 때마다 다시 읽는다 — 환경변수가 프로세스 시작 뒤에 채워질 수 있어,
            //  만들 때 한 번 읽으면 빈 값이 그대로 굳는다.
            string key = keyProvider();

            //  키가 없으면 헤더를 붙이지 않는다. 빈 값을 보내면 서버가 "틀린 키"로 읽어
            //  401을 주고, 설정이 빠진 것인지 키가 틀린 것인지 구분하기 어려워진다.
            if (string.IsNullOrEmpty(key) == false)
            {
                request.Headers[headerName] = key;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Unity가 `.meta`를 만들게 하고 함께 커밋한다**

사용자에게 GameFramework를 포함한 Unity 에디터에 포커스를 주어 컴파일·`.meta` 생성을 요청한다. 그 뒤:

```bash
cd /Users/insoobae/workspace/LOP/GameFramework && git status --porcelain
```

Expected: `ApiKeyHandler.cs`, `ApiKeyHandler.cs.meta`, `ApiKeyHandlerTests.cs`, `ApiKeyHandlerTests.cs.meta` 네 개가 보인다. `.meta`가 없으면 Unity가 아직 임포트하지 않은 것이다 — 직접 만들지 말고 다시 요청한다.

- [ ] **Step 5: 테스트를 돌린다**

Unity 에디터 **Window > General > Test Runner**에서 EditMode(또는 PlayMode) 테스트를 돌려 `ApiKeyHandlerTests` 5개가 통과하는지 확인한다. 콘솔에 컴파일 에러가 없어야 한다.

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http/ApiKeyHandler.cs Runtime/Scripts/Http/ApiKeyHandler.cs.meta \
        Tests/Runtime/Http/ApiKeyHandlerTests.cs Tests/Runtime/Http/ApiKeyHandlerTests.cs.meta
git commit -m "feat(http): 지정한 헤더에 API 키를 붙이는 ApiKeyHandler

BearerTokenHandler와 같은 자리의 DelegatingHandler. 키를 보낼 때마다 다시 읽는다 —
환경변수가 프로세스 시작 뒤에 채워질 수 있어 만들 때 한 번 읽으면 빈 값이 굳는다."
```

---

### Task 8: 게임서버 HTTP 배선과 경로 전환

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs`

**Interfaces:**
- Consumes: `ApiKeyHandler(HttpMessageHandler, string, Func<string>)`(`GameFramework.Http`, Task 7)
- Produces: 없음 (내부 배선만)

- [ ] **Step 1: HttpClient 조립을 바꾼다**

`LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs` 10행:

```csharp
        private static readonly HttpClient httpClient = new HttpClient(new UnityWebRequestHandler());
```

를 아래로 바꾼다:

```csharp
        //  게임서버가 백엔드에 거는 모든 호출은 서비스 간 호출이다 — 키를 한 곳에서 붙인다.
        //  호출부마다 헤더를 손으로 넣으면 새 API를 추가할 때 빠뜨린다.
        private static readonly HttpClient httpClient = new HttpClient(
            new ApiKeyHandler(new UnityWebRequestHandler(), "X-Internal-Api-Key",
                () => System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")));
```

`using GameFramework.Http;`가 이미 있는지 확인하고 없으면 추가한다.

- [ ] **Step 2: Introspect의 손수 붙인 헤더를 지운다**

같은 파일의 `Introspect` 메서드에서 아래 줄을 지운다:

```csharp
            request.Headers["X-Internal-Api-Key"] = System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY");
```

`MessagePipe` 관련 주석("전역 발행(SendAsync<T>)을 쓰지 않는다 …")은 그대로 남긴다 — 여전히 유효한 이유다.

- [ ] **Step 3: URL 네 개를 새 경로로 바꾼다**

같은 파일에서 아래 표대로 바꾼다. `GetMatch`의 `/match/{matchId}`는 **바꾸지 않는다**.

| 메서드 | 옛 URL | 새 URL |
|---|---|---|
| `Heartbeat` | `{roomBaseURL}/room/heartbeat/{roomId}` | `{roomBaseURL}/internal/room/heartbeat/{roomId}` |
| `UpdateRoomStatus` | `{roomBaseURL}/room/status` | `{roomBaseURL}/internal/room/status` |
| `GetRoom` | `{roomBaseURL}/room/{roomId}` | `{roomBaseURL}/internal/room/{roomId}` |
| `Introspect` | `{lobbyBaseURL}/auth/introspect` | `{lobbyBaseURL}/internal/auth/introspect` |

- [ ] **Step 4: 옛 경로가 남아 있지 않은지 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server && grep -n "roomBaseURL\|lobbyBaseURL\|matchmakingBaseURL" Assets/Scripts/WebAPI/WebAPI.cs
```

Expected: `room/`·`auth/`로 시작하는 경로가 없고, 넷 다 `/internal/`로 시작한다. `/match/{matchId}` 하나만 `/internal` 없이 남는다.

- [ ] **Step 5: 컴파일을 확인한다**

사용자에게 LeagueOfPhysical-Server Unity 에디터에서 컴파일을 요청하고, 콘솔에 에러가 없는지 확인한다.

> UnityMCP를 쓸 경우 `mcpforunity://instances`에서 `LeagueOfPhysical-Server`의 전체 id를 읽어 **모든 호출에 `unity_instance`를 명시**한다. 클라이언트 인스턴스를 건드리지 않는다.

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/WebAPI/WebAPI.cs
git commit -m "feat(webapi): 모든 백엔드 호출에 내부 키를 자동 부착 + /internal 경로

ApiKeyHandler를 HttpClient에 끼워 introspect에만 손으로 붙이던 헤더를 없앴다.
룸 하트비트·상태·조회와 introspect를 /internal로 옮겼다. 매치 조회는 주체별
인가 라우트라 경로가 그대로다."
```

---

### Task 9: 인프라 — 시크릿 주입, 로컬 env, 인그레스 차단

**Files:**
- Modify: `infrastructure/k8s/apps/backend/matchmaking-server/matchmaking-server-deployment.yaml`
- Modify: `infrastructure/k8s/apps/backend/matchmaking-server/matchmaking-director-deployment.yaml`
- Modify: `infrastructure/k8s/apps/backend/room-server/room-server-deployment.yaml`
- Modify: `infrastructure/k8s/platform/ingress/ingress.yaml`
- Modify: `lop-backend/apps/matchmaking-server/.env.development.local`
- Modify: `lop-backend/apps/matchmaking-server/.env.development.local-k8s`
- Modify: `lop-backend/apps/room-server/.env.development.local`
- Modify: `lop-backend/apps/room-server/.env.development.local-k8s`

**Interfaces:**
- Consumes: `internal-api-secret` (1c에서 만든 k8s Secret, 키 이름 `INTERNAL_API_KEY`)
- Produces: 없음

- [ ] **Step 1: 세 배포에 시크릿을 붙인다**

세 파일의 `envFrom` 목록 **맨 끝**에 아래 두 줄을 추가한다 (들여쓰기는 같은 목록의 다른 `- secretRef:`와 맞춘다):

```yaml
        - secretRef:
            name: internal-api-secret
```

대상: `matchmaking-server-deployment.yaml`, `matchmaking-director-deployment.yaml`, `room-server-deployment.yaml`.

확인:

```bash
cd /Users/insoobae/workspace/LOP/infrastructure && grep -c "internal-api-secret" k8s/apps/backend/lobby-server/lobby-server-deployment.yaml k8s/apps/backend/matchmaking-server/matchmaking-server-deployment.yaml k8s/apps/backend/matchmaking-server/matchmaking-director-deployment.yaml k8s/apps/backend/room-server/room-server-deployment.yaml
```

Expected: 네 파일 모두 `1`

- [ ] **Step 2: 로컬 env에 키를 넣는다**

`lop-backend/apps/matchmaking-server/.env.development.local`과 `lop-backend/apps/room-server/.env.development.local`의 `# AUTH` 절(없으면 파일 끝)에 로비와 **같은 값**을 추가한다:

```
INTERNAL_API_KEY = local-dev-only-CHANGE-ME-not-a-real-key
```

값이 로비와 같은지 확인한다:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend && grep -h "INTERNAL_API_KEY" apps/lobby-server/.env.development.local apps/matchmaking-server/.env.development.local apps/room-server/.env.development.local
```

Expected: 세 줄이 모두 같은 값

- [ ] **Step 3: k8s용 env에는 주석만 남긴다**

`lop-backend/apps/matchmaking-server/.env.development.local-k8s`와 `lop-backend/apps/room-server/.env.development.local-k8s`의 `# AUTH` 절(없으면 파일 끝)에 아래 주석을 추가한다. **값은 넣지 않는다** — 이미지에 키를 굽지 않기 위해서다.

```
# AUTH
# k8s Secret(internal-api-secret)이 INTERNAL_API_KEY를 주입한다 — 이미지에 값을 굽지
# 않기 위해 여기서는 뺐다. dotenv는 override: false라 Secret이 이긴다.
```

`matchmaking-server`의 k8s env에 이미 `AUTH_JWT_SECRET` 관련 주석이 있으면 그 절에 문장만 덧붙인다.

- [ ] **Step 4: 인그레스에서 /internal을 막는다**

`infrastructure/k8s/platform/ingress/ingress.yaml`의 세 `path`를 아래로 바꾼다:

```yaml
      - path: /lobby(/|$)(?!internal(/|$))(.*)
```
```yaml
      - path: /matchmaking(/|$)(?!internal(/|$))(.*)
```
```yaml
      - path: /room(/|$)(?!internal(/|$))(.*)
```

그리고 `spec:` 위 `metadata.annotations` 아래에 이유를 남긴다:

```yaml
    # 내부 호출은 클러스터 DNS(http://lobby-server-service)로 직접 가고 인그레스를 안 거친다.
    # 그래서 /internal을 여기서 막아도 깨질 호출이 없고, 내부 키가 새도 인터넷에서는 쓸 수 없다.
    # (?!...)는 캡처하지 않으므로 rewrite-target의 $2가 그대로 동작한다.
```

- [ ] **Step 5: 인그레스 정규식이 유효한지 확인한다**

nginx는 PCRE를 쓰므로 전방탐색을 지원한다. 배포 전에 정규식 자체를 확인한다:

```bash
python3 -c "
import re
for prefix in ['lobby', 'matchmaking', 'room']:
    p = re.compile(r'/' + prefix + r'(/|\$)(?!internal(/|\$))(.*)')
    통과 = p.fullmatch('/' + prefix + '/user/abc')
    차단 = p.fullmatch('/' + prefix + '/internal/user/findAll')
    유사 = p.fullmatch('/' + prefix + '/internalize')
    print(prefix, '정상통과=', bool(통과), '내부차단=', 차단 is None, '유사경로통과=', bool(유사))
    if 통과: print('   rewrite \$2 =', 통과.group(3))
"
```

Expected: 세 줄 모두 `정상통과= True 내부차단= True 유사경로통과= True`, rewrite 값이 `user/abc`

> 캡처 그룹 번호에 주의한다. 전방탐색 안의 `(/|$)`가 그룹 2를 차지하므로 파이썬에서는 `group(3)`이지만, **nginx의 `$2`는 캡처 그룹 2**다. 전방탐색 안의 그룹이 nginx에서 `$2`가 되어 rewrite가 깨질 수 있으므로, Step 6에서 실제 배포로 확인하기 전까지 이 변경을 신뢰하지 않는다. 파이썬 확인에서 `group(2)`가 `internal(/|$)`의 캡처라면, 전방탐색 안쪽을 비캡처로 바꾼다: `(?!internal(?:/|$))`.

**위 주의사항을 반영해 최종 형태는 아래로 한다** (전방탐색 안쪽을 비캡처 `(?:...)`로):

```yaml
      - path: /lobby(/|$)(?!internal(?:/|$))(.*)
      - path: /matchmaking(/|$)(?!internal(?:/|$))(.*)
      - path: /room(/|$)(?!internal(?:/|$))(.*)
```

이 형태로 Step 5의 파이썬 확인을 다시 돌려 `group(2)`가 rewrite 대상(`user/abc`)인지 확인한다:

```bash
python3 -c "
import re
p = re.compile(r'/lobby(/|\$)(?!internal(?:/|\$))(.*)')
m = p.fullmatch('/lobby/user/abc')
print('group(2) =', m.group(2))
print('내부차단 =', p.fullmatch('/lobby/internal/user/findAll') is None)
"
```

Expected: `group(2) = user/abc`, `내부차단 = True`

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure
git add k8s/apps/backend/matchmaking-server/ k8s/apps/backend/room-server/ k8s/platform/ingress/ingress.yaml
git commit -m "feat(k8s): 세 서비스에 내부 키 주입 + 인그레스에서 /internal 차단

매치메이킹 서버·디렉터·룸 서버가 서비스 간 호출을 보내고 받으므로 키가 필요하다
(디렉터는 방 생성과 위치 갱신을 HTTP로 부른다). 내부 호출은 클러스터 DNS로
직접 가고 인그레스를 안 거치므로, /internal을 밖에서 막아도 깨질 호출이 없다."

cd /Users/insoobae/workspace/LOP/lop-backend
git add apps/matchmaking-server/.env.development.local apps/matchmaking-server/.env.development.local-k8s \
        apps/room-server/.env.development.local apps/room-server/.env.development.local-k8s
git commit -m "chore(env): 매치메이킹·룸 서버에 내부 API 키 설정 추가

로컬은 로비와 같은 개발용 값을 쓰고, k8s용 파일에는 값을 넣지 않는다 —
Secret이 주입하므로 이미지에 키를 굽지 않기 위해서다."
```

---

## 배포 (모든 태스크 완료 후, 사람이 확인하며 진행)

라우트를 옮기는 순간 옛 경로는 404다. 실 유저가 없고 클러스터가 하나라 한 번에 배포한다.

- [ ] **다섯 저장소를 main에 `--no-ff`로 머지한다** (리뷰 통과 후)
- [ ] 백엔드 3서비스 + 디렉터 이미지를 빌드·푸시하고 배포한다
- [ ] 게임서버 이미지 태그를 갱신하고 **`kubectl rollout restart deployment/room-server`** 를 반드시 실행한다 — ConfigMap의 태그만 바꾸면 재시작이 안 걸려 옛 이미지가 계속 뜬다 (1c에서 확인)
- [ ] 인그레스를 적용하고 **즉시 정상 경로를 확인한다** — 정규식이 틀리면 서비스 트래픽이 통째로 404가 된다
- [ ] 이미 돌고 있던 방은 버린다 (옛 게임서버가 옛 경로로 하트비트를 보내 404가 난다)

### 라이브 검증

| 확인 | 기대 |
|---|---|
| 밖에서 `PUT /room/internal/room/status` | 404 |
| 밖에서 `PUT /lobby/user/location` | 404 |
| 밖에서 `GET /lobby/user/all` | 404 |
| 밖에서 `GET /lobby/user/<남의id>` (내 토큰) | 403 |
| 밖에서 `GET /lobby/user/<내id>` (내 토큰) | 200 |
| 밖에서 `GET /lobby/user/<내id>` (토큰 없음) | 401 |
| 클라 로그인 → 매칭 → 방 입장 | 정상 |
| 룸 서버·게임서버 로그 | 404/401 없음 |
| 매치메이킹 디렉터 로그 | 방 생성 성공 |

---

## 알려진 한계 (정직하게 기록할 것)

- **키가 하나뿐이라 "어느 서비스인가"를 구분하지 못한다.** 주체는 `service` 하나로만 표현되고, 키가 새면 모든 내부 동작이 열린다.
- **유니티 앱 코드(`WebAPI.cs`)는 asmdef가 없어 단위 테스트가 불가능하다.** 경로 문자열과 핸들러 조립은 리뷰와 라이브 검증으로만 확인한다. (`ApiKeyHandler` 자체는 GameFramework에 있어 테스트가 붙는다.)
- **인그레스 차단은 정규식 하나에 달려 있다.** 배포 후 정상 경로 확인이 곧 유일한 검증이다.
