# 익명 로그인 백엔드 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** lobby-server에 익명 계정 생성·로그인 엔드포인트와 JWT 세션 토큰을 추가한다. 기존 API의 동작은 하나도 바꾸지 않는다.

**Architecture:** 토큰 발급·검증과 인증 미들웨어는 `packages/server-core`의 auth entry에 두고, 계정/신원 데이터는 `User` 1:N `UserIdentity`로 나눈다. provider별 검증은 verifier 인터페이스 뒤에 두어 익명만 구현하고 구글/애플은 501을 반환하는 stub으로 자리만 잡는다.

**Tech Stack:** TypeScript, Express, Prisma(PostgreSQL), jsonwebtoken(HS256), bcrypt, jest + ts-jest, supertest, @testcontainers/postgresql

**설계 문서:** `docs/superpowers/specs/2026-08-04-anonymous-auth-session-design.md` (LeagueOfPhysical-Client 리포)

## Global Constraints

- **작업 리포는 `/Users/insoobae/workspace/LOP/lop-backend`다.** 이 계획의 모든 파일 경로는 그 디렉터리 기준이다. (계획 문서 자체만 LeagueOfPhysical-Client 리포에 있다.)
- 다른 머신에서 같은 리포를 병렬로 수정 중이다. **각 태스크 시작 전 `git pull --rebase`**, 특히 Task 3(스키마)은 반드시.
- 토큰 알고리즘은 **HS256**, 클레임은 `sub = userId` + `exp`. 수명은 **3600초**.
- 비밀키는 환경변수 **`AUTH_JWT_SECRET`**. 기본값을 코드에 두지 않는다 — 없으면 예외.
- 익명 secret은 **평문 저장 금지**. bcrypt 해시만 DB에 남긴다.
- provider 값은 Prisma enum `AuthProvider`의 `ANONYMOUS` / `GOOGLE_PLAY_GAMES` / `GAME_CENTER`.
- **기존 라우트에 인증 미들웨어를 붙이지 않는다.** 이 계획은 순수 추가만 한다 — 미들웨어 적용은 클라이언트가 준비된 뒤(다음 계획) 한다. 지금 붙이면 그때까지 모든 API가 401이 되어 아무것도 확인할 수 없다.
- 들여쓰기 4칸, 세미콜론 사용 — 기존 파일 스타일을 따른다.
- 주석은 *왜*만 쓴다. 코드로 자명한 것은 쓰지 않는다.

---

### Task 0: 작업 브랜치 생성

**Files:**
- 없음 (git 작업만)

- [ ] **Step 1: 최신 main에서 브랜치 생성**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
git checkout main
git pull --ff-only
git checkout -b feature/auth-anonymous-session
git status -sb
```

기대: `## feature/auth-anonymous-session...origin/main`, 변경 없음.

---

### Task 1: server-core — 토큰 발급·검증

**Files:**
- Create: `packages/server-core/src/auth/token.ts`
- Create: `packages/server-core/src/auth/__tests__/token.test.ts`
- Create: `packages/server-core/src/entries/auth.ts`
- Create: `packages/server-core/jest.config.js`
- Modify: `packages/server-core/package.json` (deps, exports, typesVersions, test script)

**Interfaces:**
- Produces:
  - `ACCESS_TOKEN_TTL_SECONDS: number` (= 3600)
  - `signAccessToken(userId: string, secret?: string): string`
  - `verifyAccessToken(token: string, secret?: string): AccessTokenPayload | null`
  - `interface AccessTokenPayload { userId: string }`
  - `getAuthSecret(): string` — `AUTH_JWT_SECRET`를 읽고 없으면 throw

- [ ] **Step 1: 의존성과 test 스크립트 추가**

`packages/server-core/package.json`의 `scripts`에 한 줄 추가:

```json
        "test": "jest",
```

`dependencies`에 추가:

```json
        "jsonwebtoken": "^9.0.2",
```

`devDependencies`에 추가:

```json
        "@types/jest": "^29.5.14",
        "@types/jsonwebtoken": "^9.0.7",
        "jest": "^29.7.0",
        "ts-jest": "^29.4.12",
```

`exports`에 `./auth` 항목 추가 (`./express` 항목 바로 뒤):

```json
        "./auth": {
            "types": "./dist/entries/auth.d.ts",
            "default": "./dist/entries/auth.js"
        },
```

`typesVersions`의 `"*"` 안에 추가:

```json
            "auth": ["dist/entries/auth.d.ts"],
```

설치:

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm install
```

- [ ] **Step 2: jest 설정 파일 생성**

`packages/server-core/jest.config.js` — 이 패키지는 path alias를 쓰지 않으므로 matchmaking-server 것보다 단순하다.

```js
/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    rootDir: '.',
    testMatch: ['<rootDir>/src/**/__tests__/**/*.test.ts'],
};
```

- [ ] **Step 3: 실패하는 테스트 작성**

`packages/server-core/src/auth/__tests__/token.test.ts`:

```ts
import { signAccessToken, verifyAccessToken, getAuthSecret, ACCESS_TOKEN_TTL_SECONDS } from '../token';

const SECRET = 'test-secret-0123456789';
const OTHER_SECRET = 'another-secret-9876543210';

describe('access token', () => {
    it('발급한 토큰을 검증하면 userId가 나온다', () => {
        const token = signAccessToken('user-1', SECRET);

        expect(verifyAccessToken(token, SECRET)).toEqual({ userId: 'user-1' });
    });

    it('다른 키로 검증하면 실패한다', () => {
        const token = signAccessToken('user-1', SECRET);

        expect(verifyAccessToken(token, OTHER_SECRET)).toBeNull();
    });

    //  서명이 깨진 토큰은 "값이 이상한 토큰"이 아니라 위조 시도다 — 절대 통과하면 안 된다.
    it('서명을 변조한 토큰은 거부한다', () => {
        const token = signAccessToken('user-1', SECRET);
        const tampered = token.slice(0, -1) + (token.endsWith('A') ? 'B' : 'A');

        expect(verifyAccessToken(tampered, SECRET)).toBeNull();
    });

    it('페이로드를 바꿔치기한 토큰은 거부한다', () => {
        const token = signAccessToken('user-1', SECRET);
        const [header, , signature] = token.split('.');
        const forgedPayload = Buffer.from(JSON.stringify({ sub: 'user-2' })).toString('base64url');

        expect(verifyAccessToken(`${header}.${forgedPayload}.${signature}`, SECRET)).toBeNull();
    });

    it('만료된 토큰은 거부한다', () => {
        jest.useFakeTimers().setSystemTime(new Date('2026-01-01T00:00:00Z'));
        const token = signAccessToken('user-1', SECRET);

        jest.setSystemTime(new Date('2026-01-01T00:00:00Z').getTime() + (ACCESS_TOKEN_TTL_SECONDS + 60) * 1000);
        expect(verifyAccessToken(token, SECRET)).toBeNull();

        jest.useRealTimers();
    });

    it('토큰 형식이 아니면 거부한다', () => {
        expect(verifyAccessToken('not-a-token', SECRET)).toBeNull();
        expect(verifyAccessToken('', SECRET)).toBeNull();
    });
});

describe('getAuthSecret', () => {
    const original = process.env.AUTH_JWT_SECRET;
    afterEach(() => {
        if (original === undefined) delete process.env.AUTH_JWT_SECRET;
        else process.env.AUTH_JWT_SECRET = original;
    });

    it('환경변수를 읽어 온다', () => {
        process.env.AUTH_JWT_SECRET = SECRET;

        expect(getAuthSecret()).toBe(SECRET);
    });

    //  비밀키가 없을 때 조용히 빈 문자열로 서명하면 누구나 토큰을 위조할 수 있다.
    //  기본값을 두지 않고 터뜨리는 것이 안전한 실패다.
    it('환경변수가 없으면 예외를 던진다', () => {
        delete process.env.AUTH_JWT_SECRET;

        expect(() => getAuthSecret()).toThrow('AUTH_JWT_SECRET');
    });
});
```

- [ ] **Step 4: 테스트가 실패하는지 확인**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter @lop/server-core run test
```

기대: FAIL — `Cannot find module '../token'`

- [ ] **Step 5: 구현**

`packages/server-core/src/auth/token.ts`:

```ts
import jwt from 'jsonwebtoken';

export const ACCESS_TOKEN_TTL_SECONDS = 3600;

const ALGORITHM = 'HS256';

export interface AccessTokenPayload {
    userId: string;
}

export function getAuthSecret(): string {
    const secret = process.env.AUTH_JWT_SECRET;

    if (!secret) {
        throw new Error('AUTH_JWT_SECRET is not set.');
    }

    return secret;
}

export function signAccessToken(userId: string, secret: string = getAuthSecret()): string {
    return jwt.sign({ sub: userId }, secret, { algorithm: ALGORITHM, expiresIn: ACCESS_TOKEN_TTL_SECONDS });
}

//  검증 실패를 예외가 아니라 null로 돌려준다 — 호출부(미들웨어)에서 만료·위조·형식오류를
//  전부 "인증 실패" 한 가지로 다루면 되기 때문이다.
export function verifyAccessToken(token: string, secret: string = getAuthSecret()): AccessTokenPayload | null {
    try {
        const decoded = jwt.verify(token, secret, { algorithms: [ALGORITHM] }) as jwt.JwtPayload;

        if (typeof decoded.sub !== 'string') {
            return null;
        }

        return { userId: decoded.sub };
    } catch {
        return null;
    }
}
```

`packages/server-core/src/entries/auth.ts`:

```ts
export * from '../auth/token';
```

- [ ] **Step 6: 테스트 통과 확인**

```bash
pnpm --filter @lop/server-core run test
```

기대: PASS (8 tests)

- [ ] **Step 7: 빌드가 깨지지 않는지 확인**

```bash
pnpm exec turbo run build
```

기대: 전체 성공

- [ ] **Step 8: 커밋**

```bash
git add packages/server-core pnpm-lock.yaml
git commit -m "feat(auth): HS256 세션 토큰 발급·검증 추가

비밀키는 AUTH_JWT_SECRET 환경변수에서만 읽는다 — 기본값을 두면
비밀키 누락이 조용한 위조 취약점이 되므로 없으면 예외를 던진다."
```

---

### Task 2: server-core — 인증 미들웨어

**Files:**
- Create: `packages/server-core/src/middlewares/auth.middleware.ts`
- Create: `packages/server-core/src/middlewares/__tests__/auth.middleware.test.ts`
- Modify: `packages/server-core/src/entries/auth.ts`

**Interfaces:**
- Consumes: `verifyAccessToken`, `AccessTokenPayload` (Task 1)
- Produces:
  - `authMiddleware(req, res, next)` — `Authorization: Bearer` 검증 후 `req.userId` 설정, 실패 시 401
  - `requireSelf(paramName?: string)` — 경로 파라미터와 `req.userId` 비교, 다르면 403 (기본 `'id'`)
  - `Express.Request.userId?: string` 타입 확장

- [ ] **Step 1: 실패하는 테스트 작성**

`packages/server-core/src/middlewares/__tests__/auth.middleware.test.ts`:

```ts
import type { NextFunction, Request, Response } from 'express';
import { authMiddleware, requireSelf } from '../auth.middleware';
import { signAccessToken } from '../../auth/token';

const SECRET = 'test-secret-0123456789';

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

describe('authMiddleware', () => {
    beforeEach(() => { process.env.AUTH_JWT_SECRET = SECRET; });

    it('유효한 토큰이면 userId를 심고 통과시킨다', () => {
        const req = { headers: { authorization: `Bearer ${signAccessToken('user-1', SECRET)}` } } as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        authMiddleware(req, res, next);

        expect(req.userId).toBe('user-1');
        expect(next).toHaveBeenCalled();
    });

    it('Authorization 헤더가 없으면 401', () => {
        const req = { headers: {} } as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        authMiddleware(req, res, next);

        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    it('Bearer 스킴이 아니면 401', () => {
        const req = { headers: { authorization: 'Basic abcdef' } } as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        authMiddleware(req, res, next);

        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });

    it('검증에 실패하는 토큰이면 401', () => {
        const req = { headers: { authorization: 'Bearer garbage' } } as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        authMiddleware(req, res, next);

        expect(res.statusCode).toBe(401);
        expect(next).not.toHaveBeenCalled();
    });
});

describe('requireSelf', () => {
    it('경로의 id가 토큰의 userId와 같으면 통과', () => {
        const req = { params: { id: 'user-1' }, userId: 'user-1' } as unknown as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        requireSelf()(req, res, next);

        expect(next).toHaveBeenCalled();
    });

    //  남의 userId를 경로에 넣어 남의 자원을 읽는 것을 막는 장치다.
    it('다르면 403', () => {
        const req = { params: { id: 'user-2' }, userId: 'user-1' } as unknown as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        requireSelf()(req, res, next);

        expect(res.statusCode).toBe(403);
        expect(next).not.toHaveBeenCalled();
    });

    it('파라미터 이름을 지정할 수 있다', () => {
        const req = { params: { userId: 'user-1' }, userId: 'user-1' } as unknown as Request;
        const res = createResponse();
        const next = jest.fn() as unknown as NextFunction;

        requireSelf('userId')(req, res, next);

        expect(next).toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
pnpm --filter @lop/server-core run test
```

기대: FAIL — `Cannot find module '../auth.middleware'`

- [ ] **Step 3: 구현**

`packages/server-core/src/middlewares/auth.middleware.ts`:

```ts
import { NextFunction, Request, RequestHandler, Response } from 'express';
import { verifyAccessToken } from '../auth/token';

declare global {
    namespace Express {
        interface Request {
            userId?: string;
        }
    }
}

const BEARER_PREFIX = 'Bearer ';

export function authMiddleware(req: Request, res: Response, next: NextFunction): void {
    const header = req.headers.authorization;

    if (!header || header.startsWith(BEARER_PREFIX) === false) {
        res.status(401).json({ message: 'Authorization header is missing or malformed.' });
        return;
    }

    const payload = verifyAccessToken(header.slice(BEARER_PREFIX.length));

    if (payload === null) {
        res.status(401).json({ message: 'Invalid or expired access token.' });
        return;
    }

    req.userId = payload.userId;
    next();
}

//  URL 모양(/user/:id/...)을 그대로 두고 소유권만 확인한다 — 경로를 /me로 바꾸면
//  클라이언트 호출부를 전부 고쳐야 한다.
export function requireSelf(paramName: string = 'id'): RequestHandler {
    return (req: Request, res: Response, next: NextFunction): void => {
        if (req.params[paramName] !== req.userId) {
            res.status(403).json({ message: 'Forbidden.' });
            return;
        }

        next();
    };
}
```

`packages/server-core/src/entries/auth.ts`를 다음으로 교체:

```ts
export * from '../auth/token';
export * from '../middlewares/auth.middleware';
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
pnpm --filter @lop/server-core run test
```

기대: PASS (15 tests)

- [ ] **Step 5: 빌드 확인 후 커밋**

```bash
pnpm exec turbo run build
git add packages/server-core
git commit -m "feat(auth): Bearer 토큰 인증 미들웨어와 소유권 확인 추가

경로를 /me로 바꾸지 않고 requireSelf로 소유권만 확인한다 —
URL 모양을 유지해 클라이언트 호출부 변경을 0으로 둔다."
```

---

### Task 3: 스키마 — UserIdentity 추가, User 정리

**Files:**
- Modify: `packages/database/prisma/schema.prisma`
- Create: `packages/database/prisma/migrations/<timestamp>_add_user_identity/migration.sql` (prisma가 생성)
- Modify: `apps/lobby-server/src/interfaces/user.interface.ts`
- Modify: `apps/lobby-server/src/factories/user.factory.ts`
- Modify: `apps/lobby-server/src/mappers/entities/user.mapper.ts`
- Modify: `apps/lobby-server/src/mappers/controllers/user.mapper.ts`
- Modify: `apps/lobby-server/src/dtos/user.dto.ts`

**Interfaces:**
- Produces:
  - Prisma enum `AuthProvider { ANONYMOUS, GOOGLE_PLAY_GAMES, GAME_CENTER }`
  - Prisma model `UserIdentity { id, userId, provider, providerUserId, secretHash?, createdAt }`, `@@unique([provider, providerUserId])`
  - `User` 도메인 인터페이스: `passwordHash` 제거, `email: string | null`

> ⚠️ **이 태스크 직전에 반드시 `git pull --rebase`.** 병렬 작업과 마이그레이션 순서가 꼬이는 것을 막는 유일한 지점이다.

- [ ] **Step 1: 스키마 수정**

`packages/database/prisma/schema.prisma`의 `model User`를 다음으로 교체:

```prisma
model User {
  id          String    @id @default(uuid())
  username    String    @unique
  email       String?   @unique
  createdAt   DateTime  @default(now())
  updatedAt   DateTime  @updatedAt
  lastLoginAt DateTime?
}

//  계정(User)과 "무엇으로 로그인했는가"(UserIdentity)를 나눈다.
//  나중에 구글/애플 연동은 이 표에 행 하나를 더하는 것으로 끝난다.
model UserIdentity {
  id             String       @id @default(uuid())
  userId         String
  provider       AuthProvider
  providerUserId String
  //  익명 계정만 secret을 갖는다. 플랫폼 신원은 구글/애플이 보증하므로 우리가 비밀을 들지 않는다.
  secretHash     String?
  createdAt      DateTime     @default(now())

  @@unique([provider, providerUserId])
  @@index([userId])
}

enum AuthProvider {
  ANONYMOUS
  GOOGLE_PLAY_GAMES
  GAME_CENTER
}
```

- [ ] **Step 2: 마이그레이션 생성**

`prisma migrate dev`는 shadow DB가 필요하다. 일회용 postgres를 띄워서 만든다.

```bash
cd /Users/insoobae/workspace/LOP/lop-backend/packages/database
docker run --rm -d -p 55432:5432 -e POSTGRES_PASSWORD=pw --name lop-migrate-tmp postgres:16-alpine
sleep 5
DATABASE_URL="postgresql://postgres:pw@localhost:55432/postgres" npx prisma migrate dev --name add_user_identity
docker rm -f lop-migrate-tmp
```

기대: `prisma/migrations/<timestamp>_add_user_identity/migration.sql` 생성. 내용에 `CREATE TABLE "UserIdentity"`, `DROP COLUMN "passwordHash"`, `ALTER COLUMN "email" DROP NOT NULL`가 포함된다.

- [ ] **Step 3: 마이그레이션 SQL 눈으로 확인**

```bash
cat prisma/migrations/*_add_user_identity/migration.sql
```

`DROP COLUMN "passwordHash"`가 있는지 확인한다. 파괴적 변경이지만 dev DB 데이터는 버려도 된다고 합의된 사항이다.

- [ ] **Step 4: 타입 재생성 후 빌드 — 여기서 컴파일이 깨진다 (의도됨)**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter @lop/database run generate
pnpm exec turbo run build
```

기대: FAIL — `passwordHash` / `email` 타입 불일치 4~6곳

- [ ] **Step 5: 도메인 인터페이스 수정**

`apps/lobby-server/src/interfaces/user.interface.ts`:

```ts

export interface User {
    id: string;
    username: string;
    email: string | null;
    lastLoginAt: Date | null;
}
```

- [ ] **Step 6: 팩토리 수정**

`apps/lobby-server/src/factories/user.factory.ts`의 `createDefault`를 다음으로 교체:

```ts
    private static createDefault(): User {
        return {
            id: '',
            username: '',
            email: null,
            lastLoginAt: null,
        };
    }
```

- [ ] **Step 7: 엔티티 매퍼 수정**

`apps/lobby-server/src/mappers/entities/user.mapper.ts`의 `toDomain`·`toEntity`에서 `passwordHash` 줄을 삭제한다.

```ts
    public toDomain(entity: UserEntity): User {
        return {
            id: entity.id,
            username: entity.username,
            email: entity.email,
            lastLoginAt: entity.lastLoginAt,
        };
    }

    public toEntity(domain: User): UserEntity {
        return {
            id: domain.id,
            username: domain.username,
            email: domain.email,
            lastLoginAt: domain.lastLoginAt,
        } as UserEntity;
    }
```

- [ ] **Step 8: DTO와 컨트롤러 매퍼 수정**

`apps/lobby-server/src/dtos/user.dto.ts`의 두 곳:

```ts
export class CreateUserDto {
    @IsString()
    public username: string;

    @IsOptional()
    @IsString()
    public email?: string;
}

export class UserResponseDto {
    public id: string;
    public username: string;
    public email: string | null;
}
```

`class-validator` import에 `IsOptional`을 추가한다:

```ts
import { IsNumber, IsString, IsEnum, IsObject, IsArray, ValidateNested, IsOptional } from 'class-validator';
```

`apps/lobby-server/src/mappers/controllers/user.mapper.ts`의 `toEntity`:

```ts
        public static toEntity(createUserDto: CreateUserDto): User {
            return UserFactory.create({
                username: createUserDto.username,
                email: createUserDto.email ?? null,
            });
        }
```

- [ ] **Step 9: 빌드 통과 확인**

```bash
pnpm exec turbo run build
```

기대: 전체 성공

- [ ] **Step 10: 커밋**

```bash
git add packages/database apps/lobby-server
git commit -m "feat(auth): User/UserIdentity로 계정과 신원 분리

기기 ID를 username에 넣어 계정 식별자로 쓰던 구조를 걷어내기 위한 스키마.
쓰이지 않던 passwordHash를 지우고, 익명 계정에는 이메일이 없는 것이
정상이므로 email을 nullable로 바꾼다."
```

---

### Task 4: lobby-server 테스트 하니스

**Files:**
- Create: `apps/lobby-server/jest.config.js`
- Create: `apps/lobby-server/jest.integration.config.js`
- Create: `apps/lobby-server/test/integration/globalSetup.ts`
- Create: `apps/lobby-server/test/integration/globalTeardown.ts`
- Create: `apps/lobby-server/test/integration/db.ts`
- Create: `apps/lobby-server/test/integration/harness.integration.test.ts`
- Modify: `apps/lobby-server/package.json`
- Modify: `packages/server-core/src/app.ts` (`getServer()` 추가)

**Interfaces:**
- Produces:
  - `rawPrisma: PrismaClient` — 테스트가 직접 심고 확인할 때 쓰는 클라이언트
  - `resetTables(): Promise<void>` — `UserIdentity` → `UserStats` → `User` 순 삭제
  - `App.getServer(): express.Application` — supertest에 넘길 express 인스턴스

- [ ] **Step 1: server-core App에 express 인스턴스 노출 추가**

supertest는 `listen()`한 서버가 아니라 express 앱 객체를 받는다. `packages/server-core/src/app.ts`의 `listen()` 메서드 바로 위에 추가:

```ts
    //  supertest가 포트를 열지 않고 앱에 직접 요청을 넣기 위해 필요하다.
    public getServer(): express.Application {
        return this.app;
    }
```

- [ ] **Step 2: 의존성과 스크립트 추가**

`apps/lobby-server/package.json`의 `scripts`에 추가:

```json
        "test": "jest",
        "test:integration": "jest -c jest.integration.config.js",
```

`devDependencies`에 추가:

```json
        "@testcontainers/postgresql": "^12.0.4",
        "@types/jest": "^29.5.14",
        "@types/supertest": "^6.0.2",
        "jest": "^29.7.0",
        "supertest": "^7.0.0",
        "ts-jest": "^29.4.12",
```

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm install
```

> `jest`/`ts-jest`/`@types/jest`/`@testcontainers/postgresql` 버전은 matchmaking-server가 쓰는 것과
> 같게 맞춘 값이다. `supertest`는 이 리포에 처음 들어오는 패키지라 맞출 대상이 없다.

- [ ] **Step 3: jest 설정 두 개 생성**

`apps/lobby-server/jest.config.js`:

```js
/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    rootDir: '.',
    testMatch: ['<rootDir>/src/**/__tests__/**/*.test.ts'],
    moduleNameMapper: {
        '^@src/(.*)$': '<rootDir>/src/$1',
        '^@controllers/(.*)$': '<rootDir>/src/controllers/$1',
        //  jest는 package.json의 exports 맵을 보지 않는다. 서브패스를 여기 적지 않으면 그것만 node 해석을
        //  타고 dist로 새어, 한 테스트 안에 src와 dist 두 벌이 뜬다.
        '^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
        '^@lop/server-core/(.*)$': '<rootDir>/../../packages/server-core/src/entries/$1',
        '^@interfaces/(.*)$': '<rootDir>/src/interfaces/$1',
        '^@models/(.*)$': '<rootDir>/src/models/$1',
        '^@routes/(.*)$': '<rootDir>/src/routes/$1',
        '^@services/(.*)$': '<rootDir>/src/services/$1',
        '^@utils/(.*)$': '<rootDir>/src/utils/$1',
        '^@dtos/(.*)$': '<rootDir>/src/dtos/$1',
        '^@daos/(.*)$': '<rootDir>/src/daos/$1',
        '^@repositories/(.*)$': '<rootDir>/src/repositories/$1',
        '^@loaders/(.*)$': '<rootDir>/src/loaders/$1',
        '^@factories/(.*)$': '<rootDir>/src/factories/$1',
        '^@mappers/(.*)$': '<rootDir>/src/mappers/$1',
        '^@config$': '<rootDir>/src/config',
    },
};
```

`apps/lobby-server/jest.integration.config.js`:

```js
const base = require('./jest.config');

/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    ...base,
    //  유닛과 파일 위치가 겹치지 않게 test/ 아래만 본다.
    testMatch: ['<rootDir>/test/integration/**/*.integration.test.ts'],
    globalSetup: '<rootDir>/test/integration/globalSetup.ts',
    globalTeardown: '<rootDir>/test/integration/globalTeardown.ts',
    //  DB 하나를 공유하므로 병렬로 돌리면 서로의 데이터를 지운다.
    maxWorkers: 1,
    //  컨테이너 기동(약 10초)이 있어 기본 5초로는 모자란다.
    testTimeout: 60000,
};
```

- [ ] **Step 4: testcontainers 셋업/티어다운 작성**

`apps/lobby-server/test/integration/globalSetup.ts`:

```ts
import { PostgreSqlContainer, StartedPostgreSqlContainer } from '@testcontainers/postgresql';
import { execFileSync } from 'child_process';
import { join } from 'path';

declare global {
    var __PG_CONTAINER__: StartedPostgreSqlContainer | undefined;
}

export default async function globalSetup(): Promise<void> {
    const container = await new PostgreSqlContainer('postgres:16-alpine').start();
    globalThis.__PG_CONTAINER__ = container;

    const databaseUrl = container.getConnectionUri();

    //  스키마는 앱이 아니라 마이그레이션이 만든다 — 운영과 같은 경로여야 의미가 있다.
    const databaseDir = join(__dirname, '..', '..', '..', '..', 'packages', 'database');
    execFileSync('npx', ['prisma', 'migrate', 'deploy'], {
        cwd: databaseDir,
        env: { ...process.env, DATABASE_URL: databaseUrl },
        stdio: 'inherit',
        shell: true,
    });

    process.env.POSTGRES_HOST = container.getHost();
    process.env.POSTGRES_PORT = String(container.getPort());
    process.env.POSTGRES_USER = container.getUsername();
    process.env.POSTGRES_PASSWORD = container.getPassword();
    process.env.POSTGRES_DATABASE = container.getDatabase();
    process.env.TEST_DATABASE_URL = databaseUrl;

    //  인증 테스트가 토큰을 발급·검증하려면 비밀키가 있어야 한다.
    process.env.AUTH_JWT_SECRET = 'integration-test-secret';
}
```

`apps/lobby-server/test/integration/globalTeardown.ts`:

```ts
export default async function globalTeardown(): Promise<void> {
    await globalThis.__PG_CONTAINER__?.stop();
}
```

- [ ] **Step 5: DB 헬퍼 작성**

`apps/lobby-server/test/integration/db.ts`:

```ts
import { PrismaClient } from '@lop/database';

//  테스트가 데이터를 심고 결과를 확인할 때 쓰는 클라이언트. 앱이 쓰는 것과 다른 커넥션이다.
export const rawPrisma = new PrismaClient({
    datasources: { db: { url: process.env.TEST_DATABASE_URL } },
});

export async function resetTables(): Promise<void> {
    await rawPrisma.userIdentity.deleteMany({});
    await rawPrisma.userStats.deleteMany({});
    await rawPrisma.user.deleteMany({});
}
```

- [ ] **Step 6: 하니스 점검 테스트 작성**

`apps/lobby-server/test/integration/harness.integration.test.ts`:

```ts
import { rawPrisma, resetTables } from './db';

describe('통합 테스트 하니스', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    //  마이그레이션이 실제로 적용된 컨테이너를 보고 있는지 먼저 확인한다.
    //  이게 깨지면 아래 인증 테스트의 실패는 전부 의미가 없다.
    it('마이그레이션된 스키마를 본다', async () => {
        const user = await rawPrisma.user.create({
            data: { username: 'harness-user', email: null },
        });
        await rawPrisma.userIdentity.create({
            data: { userId: user.id, provider: 'ANONYMOUS', providerUserId: 'p-1', secretHash: 'h' },
        });

        expect(await rawPrisma.userIdentity.count()).toBe(1);
    });

    it('같은 (provider, providerUserId)는 두 번 들어가지 않는다', async () => {
        const user = await rawPrisma.user.create({ data: { username: 'dup-user', email: null } });
        await rawPrisma.userIdentity.create({
            data: { userId: user.id, provider: 'ANONYMOUS', providerUserId: 'same', secretHash: 'h' },
        });

        await expect(
            rawPrisma.userIdentity.create({
                data: { userId: user.id, provider: 'ANONYMOUS', providerUserId: 'same', secretHash: 'h' },
            }),
        ).rejects.toThrow();
    });
});
```

- [ ] **Step 7: 통합 테스트 실행**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm --filter lobby-server run test:integration
```

기대: PASS (2 tests). 첫 실행은 postgres 이미지를 받느라 오래 걸릴 수 있다.

- [ ] **Step 8: 커밋**

```bash
git add apps/lobby-server packages/server-core pnpm-lock.yaml
git commit -m "test(lobby): 진짜 DB로 도는 통합 테스트 하니스 추가

스키마를 앱이 아니라 마이그레이션으로 만든다 — UNIQUE(provider,
providerUserId) 같은 제약이 실제로 계정 중복을 막는지 검증하려면
운영과 같은 경로로 만들어진 스키마여야 한다."
```

---

### Task 5: UserIdentity 저장소와 익명 verifier

**Files:**
- Create: `apps/lobby-server/src/interfaces/user-identity.interface.ts`
- Create: `apps/lobby-server/src/daos/user-identity.dao.postgres.ts`
- Create: `apps/lobby-server/src/repositories/user-identity.repository.ts`
- Create: `apps/lobby-server/src/services/auth/credential.ts`
- Create: `apps/lobby-server/src/services/auth/__tests__/credential.test.ts`

**Interfaces:**
- Consumes: `DaoPostgresBase`, `prismaClient` (기존 server-core)
- Produces:
  - `interface UserIdentity { id, userId, provider, providerUserId, secretHash, createdAt }`
  - `class UserIdentityRepository` — `findByProviderAndProviderUserId(provider, providerUserId)`, `save(identity)`
  - `generateSecret(): string` — 32바이트 랜덤 base64url
  - `hashSecret(secret): Promise<string>` / `verifySecret(secret, hash): Promise<boolean>` — bcrypt

> `UserIdentity`는 캐시가 필요 없다(로그인 때만 읽는다). 그래서 `CacheCrudRepository`가 아니라 postgres DAO를 직접 감싼다.

- [ ] **Step 1: 실패하는 테스트 작성**

`apps/lobby-server/src/services/auth/__tests__/credential.test.ts`:

```ts
import { generateSecret, hashSecret, verifySecret } from '../credential';

describe('익명 자격증명', () => {
    it('생성한 secret은 매번 다르다', () => {
        const secrets = new Set(Array.from({ length: 20 }, () => generateSecret()));

        expect(secrets.size).toBe(20);
    });

    //  URL/JSON에 그대로 실려 다니므로 특수문자가 섞이면 안 된다.
    it('secret은 URL에 안전한 문자만 쓴다', () => {
        expect(generateSecret()).toMatch(/^[A-Za-z0-9_-]+$/);
    });

    it('해시한 secret은 원문과 다르고, 검증하면 통과한다', async () => {
        const secret = generateSecret();
        const hash = await hashSecret(secret);

        expect(hash).not.toBe(secret);
        await expect(verifySecret(secret, hash)).resolves.toBe(true);
    });

    it('다른 secret은 검증에 실패한다', async () => {
        const hash = await hashSecret(generateSecret());

        await expect(verifySecret(generateSecret(), hash)).resolves.toBe(false);
    });

    //  플랫폼 신원(구글/애플)은 secretHash가 없다. 그 행에 아무 secret이나 넣어
    //  로그인되면 안 되므로 해시가 없으면 항상 실패여야 한다.
    it('해시가 없으면 항상 실패한다', async () => {
        await expect(verifySecret(generateSecret(), null)).resolves.toBe(false);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
pnpm --filter lobby-server run test
```

기대: FAIL — `Cannot find module '../credential'`

- [ ] **Step 3: credential 구현**

`apps/lobby-server/src/services/auth/credential.ts`:

```ts
import { randomBytes } from 'crypto';
import bcrypt from 'bcrypt';

const SECRET_BYTES = 32;
const SALT_ROUNDS = 10;

export function generateSecret(): string {
    return randomBytes(SECRET_BYTES).toString('base64url');
}

export async function hashSecret(secret: string): Promise<string> {
    return bcrypt.hash(secret, SALT_ROUNDS);
}

export async function verifySecret(secret: string, hash: string | null): Promise<boolean> {
    if (!hash) {
        return false;
    }

    return bcrypt.compare(secret, hash);
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
pnpm --filter lobby-server run test
```

기대: PASS (5 tests)

- [ ] **Step 5: 도메인 인터페이스 작성**

`apps/lobby-server/src/interfaces/user-identity.interface.ts`:

```ts
import { AuthProvider } from '@lop/database';

export interface UserIdentity {
    id: string;
    userId: string;
    provider: AuthProvider;
    providerUserId: string;
    secretHash: string | null;
    createdAt: Date;
}
```

- [ ] **Step 6: DAO와 저장소 작성**

`apps/lobby-server/src/daos/user-identity.dao.postgres.ts`:

```ts
import { PrismaClient, UserIdentity as UserIdentityEntity } from '@lop/database';
import { DaoPostgresBase } from '@lop/server-core';
import { prismaClient } from '@lop/server-core/postgres';

export class UserIdentityDaoPostgres extends DaoPostgresBase<UserIdentityEntity, PrismaClient['userIdentity']> {
    constructor() {
        super(prismaClient, prismaClient.userIdentity);
    }
}
```

`apps/lobby-server/src/repositories/user-identity.repository.ts`:

```ts
import { AuthProvider } from '@lop/database';
import { UserIdentity } from '@interfaces/user-identity.interface';
import { UserIdentityDaoPostgres } from '@daos/user-identity.dao.postgres';

export class UserIdentityRepository {
    private dao = new UserIdentityDaoPostgres();

    public async findByProvider(provider: AuthProvider, providerUserId: string): Promise<UserIdentity | null> {
        const found = await this.dao.findWhere([['provider', provider], ['providerUserId', providerUserId]]);

        return (found as UserIdentity) ?? null;
    }

    public async create(identity: Omit<UserIdentity, 'id' | 'createdAt'>): Promise<UserIdentity> {
        return await this.dao.save({ ...identity, id: '' } as UserIdentity) as UserIdentity;
    }
}
```

- [ ] **Step 7: 빌드 확인 후 커밋**

```bash
pnpm exec turbo run build
git add apps/lobby-server
git commit -m "feat(auth): 익명 자격증명 생성·검증과 UserIdentity 저장소

secret은 발급 응답에서 한 번만 평문으로 나가고 DB에는 bcrypt 해시만 남는다.
secretHash가 없는 플랫폼 신원은 secret 검증이 항상 실패하도록 한다."
```

---

### Task 6: POST /auth/anonymous

**Files:**
- Create: `apps/lobby-server/src/dtos/auth.dto.ts`
- Create: `apps/lobby-server/src/services/auth/auth.service.ts`
- Create: `apps/lobby-server/src/controllers/auth.controller.ts`
- Create: `apps/lobby-server/src/routes/auth.route.ts`
- Create: `apps/lobby-server/test/integration/auth.integration.test.ts`
- Modify: `apps/lobby-server/src/main.ts`

**Interfaces:**
- Consumes: `generateSecret`/`hashSecret` (Task 5), `UserIdentityRepository` (Task 5), `signAccessToken`/`ACCESS_TOKEN_TTL_SECONDS` (Task 1), 기존 `UserService.createUser`
- Produces:
  - `AuthService.signInAnonymous(): Promise<AnonymousSignInResponseDto>`
  - `POST /auth/anonymous` → 201 `{ userId, credential: { provider, providerUserId, secret }, accessToken, expiresIn }`

- [ ] **Step 1: 실패하는 통합 테스트 작성**

`apps/lobby-server/test/integration/auth.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import { verifyAccessToken } from '@lop/server-core/auth';
import AuthRoute from '@routes/auth.route';
import { rawPrisma, resetTables } from './db';

const app = new App([new AuthRoute()]).getServer();

describe('POST /auth/anonymous', () => {
    beforeEach(async () => { await resetTables(); });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('계정과 신원을 하나씩 만들고 토큰을 준다', async () => {
        const response = await request(app).post('/auth/anonymous').send();

        expect(response.status).toBe(201);
        expect(response.body.userId).toEqual(expect.any(String));
        expect(response.body.credential.provider).toBe('ANONYMOUS');
        expect(response.body.credential.providerUserId).toEqual(expect.any(String));
        expect(response.body.credential.secret).toEqual(expect.any(String));
        expect(response.body.expiresIn).toBe(3600);

        expect(await rawPrisma.user.count()).toBe(1);
        expect(await rawPrisma.userIdentity.count()).toBe(1);
    });

    it('발급한 토큰은 그 계정의 userId를 담고 있다', async () => {
        const response = await request(app).post('/auth/anonymous').send();

        expect(verifyAccessToken(response.body.accessToken)).toEqual({ userId: response.body.userId });
    });

    //  평문 secret이 DB에 남으면 DB를 읽을 수 있는 사람이 모든 계정으로 로그인할 수 있다.
    it('secret은 DB에 평문으로 저장하지 않는다', async () => {
        const response = await request(app).post('/auth/anonymous').send();

        const identity = await rawPrisma.userIdentity.findFirstOrThrow();
        expect(identity.secretHash).not.toBe(response.body.credential.secret);
        expect(identity.secretHash).toEqual(expect.any(String));
    });

    it('두 번 부르면 서로 다른 계정이 만들어진다', async () => {
        const first = await request(app).post('/auth/anonymous').send();
        const second = await request(app).post('/auth/anonymous').send();

        expect(first.body.userId).not.toBe(second.body.userId);
        expect(await rawPrisma.user.count()).toBe(2);
    });

    it('큐별 전적 행도 함께 생긴다', async () => {
        const response = await request(app).post('/auth/anonymous').send();

        const stats = await rawPrisma.userStats.findMany({ where: { userId: response.body.userId } });
        expect(stats.map(s => s.queueId).sort()).toEqual([1, 2]);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
pnpm --filter lobby-server run test:integration
```

기대: FAIL — `Cannot find module '@routes/auth.route'`

- [ ] **Step 3: DTO 작성**

`apps/lobby-server/src/dtos/auth.dto.ts`:

```ts
import { AuthProvider } from '@lop/database';

export class CredentialDto {
    public provider: AuthProvider;
    public providerUserId: string;
    public secret: string;
}

export class AnonymousSignInResponseDto {
    public userId: string;
    public credential: CredentialDto;
    public accessToken: string;
    public expiresIn: number;
}
```

- [ ] **Step 4: 서비스 작성**

`apps/lobby-server/src/services/auth/auth.service.ts`:

```ts
import { randomUUID } from 'crypto';
import { AuthProvider } from '@lop/database';
import { ACCESS_TOKEN_TTL_SECONDS, signAccessToken } from '@lop/server-core/auth';
import { AnonymousSignInResponseDto } from '@dtos/auth.dto';
import { UserIdentityRepository } from '@repositories/user-identity.repository';
import UserService from '@services/user.service';
import { generateSecret, hashSecret } from './credential';

class AuthService {
    private userService = new UserService();
    private userIdentityRepository = new UserIdentityRepository();

    public async signInAnonymous(): Promise<AnonymousSignInResponseDto> {
        const providerUserId = randomUUID();
        const secret = generateSecret();

        //  username은 @unique다. providerUserId(uuid)에서 따오면 충돌을 따로 재시도할 필요가 없다.
        const createUser = await this.userService.createUser({
            username: `Guest-${providerUserId.slice(0, 8)}`,
        });

        const userId = createUser.user!.id;

        await this.userIdentityRepository.create({
            userId,
            provider: AuthProvider.ANONYMOUS,
            providerUserId,
            secretHash: await hashSecret(secret),
        });

        return {
            userId,
            credential: { provider: AuthProvider.ANONYMOUS, providerUserId, secret },
            accessToken: signAccessToken(userId),
            expiresIn: ACCESS_TOKEN_TTL_SECONDS,
        };
    }
}

export default AuthService;
```

- [ ] **Step 5: 컨트롤러와 라우트 작성**

`apps/lobby-server/src/controllers/auth.controller.ts`:

```ts
import { NextFunction, Request, Response } from 'express';
import AuthService from '@services/auth/auth.service';

class AuthController {
    private authService = new AuthService();

    public signInAnonymous = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const response = await this.authService.signInAnonymous();
            res.status(201).json(response);
        } catch (error) {
            next(error);
        }
    };
}

export default AuthController;
```

`apps/lobby-server/src/routes/auth.route.ts`:

```ts
import { Router } from 'express';
import { Routes } from '@lop/server-core';
import AuthController from '@controllers/auth.controller';

class AuthRoute implements Routes {
    public path = '/auth';
    public router = Router();
    public authController = new AuthController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        this.router.post(`${this.path}/anonymous`, this.authController.signInAnonymous);
    }
}

export default AuthRoute;
```

- [ ] **Step 6: main.ts에 라우트 등록**

`apps/lobby-server/src/main.ts`에 import 추가:

```ts
import AuthRoute from '@routes/auth.route';
```

`new App([...])` 배열의 맨 앞(IndexRoute 다음)에 `new AuthRoute()` 추가:

```ts
        const app = new App([new IndexRoute(), new AuthRoute(), new UserRoute(), new UserLocationRoute(), new UserProfileRoute(), new UserStatsRoute(), new LobbyRoute()]);
```

- [ ] **Step 7: 테스트 통과 확인**

```bash
pnpm --filter lobby-server run test:integration
```

기대: PASS (7 tests — 하니스 2 + 익명 5)

- [ ] **Step 8: 커밋**

```bash
git add apps/lobby-server
git commit -m "feat(auth): POST /auth/anonymous — 익명 계정 생성과 토큰 발급

username은 @unique라 providerUserId(uuid)에서 따와 충돌 재시도를 없앤다."
```

---

### Task 7: POST /auth/login + provider verifier

**Files:**
- Create: `apps/lobby-server/src/services/auth/verifiers.ts`
- Modify: `apps/lobby-server/src/dtos/auth.dto.ts`
- Modify: `apps/lobby-server/src/services/auth/auth.service.ts`
- Modify: `apps/lobby-server/src/controllers/auth.controller.ts`
- Modify: `apps/lobby-server/src/routes/auth.route.ts`
- Modify: `apps/lobby-server/test/integration/auth.integration.test.ts`

**Interfaces:**
- Consumes: `verifySecret` (Task 5), `UserIdentityRepository` (Task 5), `signAccessToken` (Task 1)
- Produces:
  - `interface AuthProviderVerifier { verify(identity, credential): Promise<boolean> }`
  - `getVerifier(provider): AuthProviderVerifier | null` — 미구현 provider면 null
  - `AuthService.login(dto): Promise<LoginResult>` — `{ ok: true, response } | { ok: false, status: 401 | 501 }`
  - `POST /auth/login` → 200 / 401 / 501

- [ ] **Step 1: 실패하는 통합 테스트 추가**

`apps/lobby-server/test/integration/auth.integration.test.ts` 끝에 추가. 상단 import에 `AuthProvider`를 더한다:

```ts
import { AuthProvider } from '@lop/database';
```

```ts
describe('POST /auth/login', () => {
    beforeEach(async () => { await resetTables(); });

    async function 익명가입() {
        const response = await request(app).post('/auth/anonymous').send();
        return response.body;
    }

    it('발급받은 자격증명으로 로그인하면 같은 계정이다', async () => {
        const signUp = await 익명가입();

        const response = await request(app).post('/auth/login').send(signUp.credential);

        expect(response.status).toBe(200);
        expect(response.body.userId).toBe(signUp.userId);
        expect(verifyAccessToken(response.body.accessToken)).toEqual({ userId: signUp.userId });
    });

    it('로그인해도 계정이 새로 생기지 않는다', async () => {
        const signUp = await 익명가입();

        await request(app).post('/auth/login').send(signUp.credential);
        await request(app).post('/auth/login').send(signUp.credential);

        expect(await rawPrisma.user.count()).toBe(1);
        expect(await rawPrisma.userIdentity.count()).toBe(1);
    });

    it('secret이 틀리면 401이고 계정은 그대로다', async () => {
        const signUp = await 익명가입();

        const response = await request(app)
            .post('/auth/login')
            .send({ ...signUp.credential, secret: 'wrong-secret' });

        expect(response.status).toBe(401);
        expect(await rawPrisma.user.count()).toBe(1);
    });

    //  없는 신원으로 로그인하면 "만들어 주는" 동작이 되면 안 된다 — 계정 생성 경로는 /auth/anonymous 하나다.
    it('없는 신원이면 401이고 계정을 만들지 않는다', async () => {
        const response = await request(app).post('/auth/login').send({
            provider: AuthProvider.ANONYMOUS,
            providerUserId: 'never-issued',
            secret: 'whatever',
        });

        expect(response.status).toBe(401);
        expect(await rawPrisma.user.count()).toBe(0);
    });

    it('아직 구현하지 않은 provider는 501', async () => {
        const response = await request(app).post('/auth/login').send({
            provider: AuthProvider.GOOGLE_PLAY_GAMES,
            providerUserId: 'google-player-1',
            secret: 'token',
        });

        expect(response.status).toBe(501);
    });

    it('provider 값이 아예 잘못되면 400', async () => {
        const response = await request(app).post('/auth/login').send({
            provider: 'NOT_A_PROVIDER',
            providerUserId: 'x',
            secret: 'y',
        });

        expect(response.status).toBe(400);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
pnpm --filter lobby-server run test:integration
```

기대: FAIL — `/auth/login`이 404

- [ ] **Step 3: verifier 작성**

`apps/lobby-server/src/services/auth/verifiers.ts`:

```ts
import { AuthProvider } from '@lop/database';
import { UserIdentity } from '@interfaces/user-identity.interface';
import { verifySecret } from './credential';

export interface LoginCredential {
    provider: AuthProvider;
    providerUserId: string;
    secret?: string;
}

export interface AuthProviderVerifier {
    verify(identity: UserIdentity, credential: LoginCredential): Promise<boolean>;
}

const anonymousVerifier: AuthProviderVerifier = {
    async verify(identity, credential) {
        if (!credential.secret) {
            return false;
        }

        return verifySecret(credential.secret, identity.secretHash);
    },
};

//  구글/애플은 인터페이스만 잡아 둔다. 여기에 실제 검증(서버 auth code 교환 / 애플 공개키 서명 검증)이
//  들어오면 라우트·토큰·클라이언트는 그대로 두고 이 표에 한 줄 추가하는 것으로 끝난다.
const verifiers: Partial<Record<AuthProvider, AuthProviderVerifier>> = {
    [AuthProvider.ANONYMOUS]: anonymousVerifier,
};

export function getVerifier(provider: AuthProvider): AuthProviderVerifier | null {
    return verifiers[provider] ?? null;
}
```

- [ ] **Step 4: DTO 추가**

`apps/lobby-server/src/dtos/auth.dto.ts` 끝에 추가 (상단 import에 `class-validator` 추가):

```ts
import { IsEnum, IsOptional, IsString } from 'class-validator';
```

```ts
export class LoginRequestDto {
    @IsEnum(AuthProvider)
    public provider: AuthProvider;

    @IsString()
    public providerUserId: string;

    @IsOptional()
    @IsString()
    public secret?: string;
}

export class LoginResponseDto {
    public userId: string;
    public accessToken: string;
    public expiresIn: number;
}
```

- [ ] **Step 5: 서비스에 login 추가**

`apps/lobby-server/src/services/auth/auth.service.ts`의 import에 추가:

```ts
import { LoginRequestDto, LoginResponseDto } from '@dtos/auth.dto';
import { getVerifier } from './verifiers';
```

클래스 안에 메서드 추가:

```ts
    public async login(dto: LoginRequestDto): Promise<{ ok: true; response: LoginResponseDto } | { ok: false; status: 401 | 501 }> {
        const verifier = getVerifier(dto.provider);

        if (verifier === null) {
            return { ok: false, status: 501 };
        }

        const identity = await this.userIdentityRepository.findByProvider(dto.provider, dto.providerUserId);

        //  없는 신원과 secret이 틀린 경우를 같은 401로 돌려준다 — 응답을 구분하면
        //  "이 신원이 존재하는가"를 밖에서 떠볼 수 있게 된다.
        if (identity === null || (await verifier.verify(identity, dto)) === false) {
            return { ok: false, status: 401 };
        }

        return {
            ok: true,
            response: {
                userId: identity.userId,
                accessToken: signAccessToken(identity.userId),
                expiresIn: ACCESS_TOKEN_TTL_SECONDS,
            },
        };
    }
```

- [ ] **Step 6: 컨트롤러와 라우트 추가**

`apps/lobby-server/src/controllers/auth.controller.ts`에 메서드 추가 (import에 `LoginRequestDto` 추가):

```ts
    public login = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const result = await this.authService.login(req.body as LoginRequestDto);

            if (result.ok === false) {
                res.status(result.status).json({ message: result.status === 501 ? 'Provider not implemented.' : 'Invalid credential.' });
                return;
            }

            res.status(200).json(result.response);
        } catch (error) {
            next(error);
        }
    };
```

`apps/lobby-server/src/routes/auth.route.ts`의 `initializeRoutes`에 추가 (import에 `validationMiddleware`, `LoginRequestDto` 추가):

```ts
import { validationMiddleware } from '@lop/server-core/express';
import { LoginRequestDto } from '@dtos/auth.dto';
```

```ts
        this.router.post(`${this.path}/login`, validationMiddleware(LoginRequestDto, 'body'), this.authController.login);
```

- [ ] **Step 7: 테스트 통과 확인**

```bash
pnpm --filter lobby-server run test:integration
```

기대: PASS (13 tests)

400은 `validationMiddleware`가 `HttpException(400)`을 `next()`로 넘기고, `App`이 등록한
`errorMiddleware`가 그것을 응답으로 바꾸는 경로다. 테스트가 `App`을 통해 앱을 만들기 때문에 그
미들웨어가 이미 붙어 있다.

- [ ] **Step 8: 커밋**

```bash
git add apps/lobby-server
git commit -m "feat(auth): POST /auth/login — provider별 verifier로 검증

없는 신원과 secret 불일치를 같은 401로 돌려준다 — 구분하면 신원 존재
여부를 밖에서 떠볼 수 있다. 구글/애플은 verifier 자리만 두고 501."
```

---

### Task 8: CI에 lobby 통합 테스트 추가

**Files:**
- Modify: `.github/workflows/backend-ci.yml`

- [ ] **Step 1: 워크플로에 스텝 추가**

`.github/workflows/backend-ci.yml`의 마지막 스텝(`통합 테스트 (진짜 DB — matchmaking)`) 뒤에 추가:

```yaml
      - name: 통합 테스트 (진짜 DB — lobby)
        #  인증은 UNIQUE(provider, providerUserId)와 마이그레이션이 맞아야 성립한다 —
        #  목킹한 저장소로는 그 둘을 검증할 수 없어 진짜 DB로 돌린다.
        run: pnpm --filter lobby-server run test:integration
```

- [ ] **Step 2: 전체 검증 (CI가 하는 것과 같은 순서)**

```bash
cd /Users/insoobae/workspace/LOP/lop-backend
pnpm install --frozen-lockfile
pnpm --filter @lop/database run generate
pnpm exec turbo run build
pnpm exec turbo run test
pnpm --filter matchmaking-server run test:integration
pnpm --filter lobby-server run test:integration
```

기대: 전부 통과. `--frozen-lockfile`이 실패하면 lockfile 커밋이 빠진 것이므로 `pnpm install` 후 `pnpm-lock.yaml`을 커밋한다.

- [ ] **Step 3: 커밋 및 푸시**

```bash
git add .github/workflows/backend-ci.yml
git commit -m "ci: lobby 통합 테스트를 CI에 추가"
git push -u origin feature/auth-anonymous-session
```

- [ ] **Step 4: CI 결과 확인**

```bash
gh run list --branch feature/auth-anonymous-session --limit 3
```

기대: `backend-ci` 성공. 실패하면 로그를 보고 고친 뒤 다시 푸시한다.

---

## 완료 조건

이 계획이 끝나면 다음이 성립한다.

- `POST /auth/anonymous`로 계정이 만들어지고 세션 토큰이 나온다
- `POST /auth/login`으로 같은 계정에 다시 로그인된다
- 틀린 secret·없는 신원은 401, 구글/애플은 501
- 위 전부가 **진짜 postgres를 띄우는 통합 테스트**로 검증되고 CI에서 돈다
- **기존 API는 하나도 바뀌지 않았다** — 클라이언트는 지금 그대로 동작한다

설계 문서의 E2E 시나리오 9개 중 **무인증 요청 401 / 타인 자원 403 / 만료 토큰 401** 세 가지는 여기서
다루지 않는다. 셋 다 기존 라우트에 미들웨어를 붙여야 관측되는 것이라 다음 계획으로 넘어간다.
미들웨어 자체의 동작은 Task 2에서 단위 테스트로 검증된다.

## 다음 계획 (별도 문서)

`2026-08-04-anonymous-auth-client.md` — Unity 클라이언트 `AuthenticationService`, Entrance 흐름 교체, 프로필 분리, 그리고 **마지막에** 기존 라우트에 `authMiddleware` 적용 + 룸 접속 인증. 미들웨어 적용이 클·서 동시 전환 지점이라 클라이언트가 준비된 뒤에 켠다.
