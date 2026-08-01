# 백엔드 공용 패키지 `@lop/server-core` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 백엔드 3개 앱이 복사해서 나눠 갖고 있는 26개 파일을 워크스페이스 패키지 `@lop/server-core` 하나로 모은다.

**Architecture:** `packages/database`(`@lop/database`)와 같은 구조의 워크스페이스 패키지를 신설하고, 앱들이 `workspace:*` 의존으로 가져다 쓴다. **동작 변화 0** — 코드는 이동만 하고 내용은 그대로다. 설정만 "인프라(공용) / 이웃 서비스 주소(앱별)"로 나눈다.

**Tech Stack:** TypeScript 5.7 / pnpm workspace + turbo / Express / Prisma / Docker

## Global Constraints

- **순수 이동이다. 동작을 바꾸지 마라.** 로직 개선·구조 변경·이름 변경을 곁들이지 마라. 옮기는 파일의 내용은 import 경로를 빼면 **한 글자도** 달라지면 안 된다.
- **패키지 내부에서는 경로 별칭을 쓰지 마라** — 상대 경로(`./`, `../`)만 쓴다. 별칭은 앱 쪽 `tsconfig`+`tsc-alias` 배선이고 패키지는 그것과 무관하게 서게 한다.
- **새 npm 의존성을 추가하지 마라** — 옮기는 파일이 이미 쓰는 것(express, winston, mongoose, redis 등)을 패키지 `package.json`으로 *옮겨 적는* 것은 추가가 아니다.
- 주석은 **한국어**로. 옮기는 파일의 기존 주석은 **그대로 둔다.**
- 검증: `pnpm --filter <app> run build` ×3, `pnpm --filter matchmaking-server test`(154), `pnpm --filter room-server test`(11).
- 작업 브랜치: `feature/backend-server-core`. **main에 직접 커밋 금지.**
- 작업 디렉터리는 **`C:/Users/re5na/workspace/LOP/lop-backend`** 뿐이다. 다른 저장소를 건드리지 마라.

---

## File Structure

```
packages/server-core/            ← 신설
  package.json                   @lop/server-core
  tsconfig.json                  declaration: true (앱이 .d.ts를 필요로 한다)
  src/
    index.ts                     배럴 — 앱은 여기서만 import 한다
    exceptions/HttpException.ts
    interfaces/…  daos/…  repositories/…  mappers/…  middlewares/…
    controllers/…  routes/…  utils/…
    config/…  caches/…  databases/…  loaders/…   (슬라이스 2)
  dist/                          빌드 산출물 (main/types가 가리킴)

apps/{lobby,matchmaking,room}-server/
  package.json                   "@lop/server-core": "workspace:*" 추가
  Dockerfile                     COPY + 패키지 빌드 단계 추가
  tsconfig.json                  옮긴 별칭 제거
  jest.config.js                 (matchmaking·room에만 존재) moduleNameMapper 정리
  src/…                          옮긴 파일 삭제 + import 교체
pnpm-lock.yaml                   갱신 커밋 필수
```

---

## Task 1: 패키지 신설 + 배선 증명 (파일 하나만 이전)

**Files:**
- Create: `packages/server-core/package.json`, `packages/server-core/tsconfig.json`, `packages/server-core/src/index.ts`
- Move: `apps/*/src/exceptions/HttpException.ts` → `packages/server-core/src/exceptions/HttpException.ts` (3앱에서 삭제, 패키지에 1개)
- Modify: `apps/{lobby,matchmaking,room}-server/package.json`, `.../Dockerfile`, `.../tsconfig.json`
- Modify: `apps/{matchmaking,room}-server/jest.config.js`
- Modify: `pnpm-lock.yaml` (pnpm이 갱신)
- Modify: `HttpException`을 import 하는 7개 파일

**Interfaces:**
- Consumes: (없음)
- Produces: `@lop/server-core` 패키지. 앱은 `import { HttpException } from '@lop/server-core';`로 쓴다.

> **이 태스크의 목적은 파일 하나를 옮기는 게 아니라 배선이 실제로 도는지 증명하는 것이다.**
> `HttpException`을 고른 이유는 소비처가 7곳(공통 미들웨어 + 앱 전용 서비스 하나)이라 import 교체가
> 두 종류 다 exercised 되기 때문이다. 나머지 15개는 Task 2에서 같은 배선을 타고 따라간다.

- [ ] **Step 1: 패키지 뼈대를 만든다**

`packages/server-core/package.json`:

```json
{
    "name": "@lop/server-core",
    "version": "0.0.0",
    "private": true,
    "main": "./dist/index.js",
    "types": "./dist/index.d.ts",
    "scripts": {
        "build": "tsc -p tsconfig.json",
        "clean": "tsc --build --clean"
    },
    "dependencies": {},
    "devDependencies": {
        "@types/node": "^22.20.1",
        "typescript": "^5.7.3"
    }
}
```

> `dependencies`는 Step 3에서 옮기는 파일이 실제로 쓰는 것만 채운다. 지금은 비워 둔다.

`packages/server-core/tsconfig.json`:

```json
{
    "extends": "../../tsconfig.base.json",
    "compilerOptions": {
        "declaration": true,
        "outDir": "dist",
        "rootDir": "src"
    },
    "include": ["src/**/*"]
}
```

> **`declaration: true`가 핵심이다.** 이게 없으면 `dist/index.d.ts`가 안 생기고 앱 `tsc`가
> `@lop/server-core`의 타입을 찾지 못한다. `tsconfig.base.json`에는 이 옵션이 없다.

`packages/server-core/src/index.ts`:

```typescript
export * from './exceptions/HttpException';
```

- [ ] **Step 2: 앱 3개가 이 패키지를 의존하게 한다**

`apps/{lobby,matchmaking,room}-server/package.json`의 `dependencies`에 추가(기존 `@lop/database` 옆):

```json
        "@lop/server-core": "workspace:*",
```

- [ ] **Step 3: 파일을 옮긴다**

```bash
git mv apps/lobby-server/src/exceptions/HttpException.ts packages/server-core/src/exceptions/HttpException.ts
git rm apps/matchmaking-server/src/exceptions/HttpException.ts apps/room-server/src/exceptions/HttpException.ts
```

옮긴 파일의 **내용은 그대로 둔다**(이 파일은 import가 없다). 세 앱의 `src/exceptions/` 폴더가 비면 폴더도 사라진다.

- [ ] **Step 4: 소비처 7곳의 import를 바꾼다**

`from '@exceptions/HttpException'` → `from '@lop/server-core'`. 대상:

```
apps/lobby-server/src/middlewares/error.middleware.ts
apps/lobby-server/src/middlewares/validation.middleware.ts
apps/matchmaking-server/src/middlewares/error.middleware.ts
apps/matchmaking-server/src/middlewares/validation.middleware.ts
apps/matchmaking-server/src/services/matchmakingTicket.service.ts
apps/room-server/src/middlewares/error.middleware.ts
apps/room-server/src/middlewares/validation.middleware.ts
```

- [ ] **Step 5: 앱 tsconfig에서 죽은 별칭을 지운다**

`apps/{lobby,matchmaking,room}-server/tsconfig.json`의 `paths`에서 `"@exceptions/*"` 줄을 삭제한다.

`apps/{matchmaking,room}-server/jest.config.js`의 `moduleNameMapper`에서도 `'^@exceptions/(.*)$'` 줄을 삭제하고, 대신 패키지를 소스로 매핑하는 줄을 추가한다(테스트는 빌드 산출물이 아니라 소스를 봐야 반복이 빠르다):

```javascript
        '^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
```

> lobby-server에는 jest 설정이 없다 — 건드릴 것이 없다.

- [ ] **Step 6: Dockerfile 3개를 고친다**

각 `apps/*/Dockerfile`에서 `COPY packages/database ./packages/database` **바로 다음 줄**에 추가:

```dockerfile
COPY packages/server-core ./packages/server-core
```

그리고 `RUN pnpm --filter @lop/database run generate` **다음 줄**(앱 build보다 **앞**)에 추가:

```dockerfile
RUN pnpm --filter @lop/server-core run build
```

> 앱의 `tsc`가 패키지의 `dist/index.d.ts`를 필요로 하므로 순서가 강제된다.

- [ ] **Step 7: lockfile을 갱신한다**

Run (repo 루트):
```bash
pnpm install
```
Expected: `pnpm-lock.yaml`이 갱신되고 `apps/*/node_modules/@lop/server-core` 심링크가 생긴다.

> Dockerfile이 `--frozen-lockfile`이라 **이 파일을 커밋하지 않으면 이미지 빌드가 설치 단계에서 실패한다.**

- [ ] **Step 8: 빌드와 테스트**

Run:
```bash
pnpm --filter @lop/server-core run build && pnpm --filter lobby-server run build && pnpm --filter matchmaking-server run build && pnpm --filter room-server run build && pnpm --filter matchmaking-server test && pnpm --filter room-server test
```
Expected: 전부 성공. matchmaking 154 tests, room 11 tests.

- [ ] **Step 9: 잔재 확인**

Run:
```bash
grep -rn "@exceptions" apps/ --include=*.ts --include=*.js --include=*.json | grep -v node_modules | grep -v dist
```
Expected: 출력 없음

- [ ] **Step 10: 커밋**

```bash
git add -A packages apps pnpm-lock.yaml
git commit -m "refactor(server-core): 공용 패키지 신설 + HttpException 이전 (배선 증명)"
```

---

## Task 2: 설정에 닿지 않는 나머지 15개 이전

**Files:**
- Move (각 3앱 → 패키지 1개): 아래 15개
- Modify: 세 앱의 import 소비처 전부, `tsconfig.json` paths, `jest.config.js`, `packages/server-core/src/index.ts`, `packages/server-core/package.json`(의존성)

**Interfaces:**
- Consumes: Task 1의 `@lop/server-core` 배선
- Produces: 아래 15개 타입·클래스가 배럴로 노출된다

옮길 15개 (전부 `@config`에 닿지 않는다 — spec §2의 계산 결과):

```
controllers/index.controller.ts          routes/index.route.ts
daos/dao.interface.ts                    repositories/repository.interface.ts
daos/dao.mongoose.base.ts                repositories/crudRepository.interface.ts
daos/dao.postgres.base.ts                repositories/cacheCrudRepository.ts
interfaces/responseBase.interface.ts     mappers/domain.entity.mapper.ts
interfaces/routes.interface.ts           middlewares/validation.middleware.ts
interfaces/user-location.interface.ts    utils/redis-json.utils.ts
utils/validateEnv.ts
```

- [ ] **Step 1: 옮기기 전에 세 앱이 정말 동일한지 다시 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
for f in controllers/index.controller.ts daos/dao.interface.ts daos/dao.mongoose.base.ts daos/dao.postgres.base.ts interfaces/responseBase.interface.ts interfaces/routes.interface.ts interfaces/user-location.interface.ts mappers/domain.entity.mapper.ts middlewares/validation.middleware.ts repositories/cacheCrudRepository.ts repositories/crudRepository.interface.ts repositories/repository.interface.ts routes/index.route.ts utils/redis-json.utils.ts utils/validateEnv.ts; do
  L=$(git hash-object apps/lobby-server/src/$f); M=$(git hash-object apps/matchmaking-server/src/$f); R=$(git hash-object apps/room-server/src/$f)
  [ "$L" = "$M" ] && [ "$M" = "$R" ] || echo "DIFFERS: $f"
done
```
Expected: 출력 없음. **하나라도 `DIFFERS`가 나오면 멈추고 보고하라** — 그 파일은 이미 갈라진 것이라 어느 쪽을 진실로 삼을지 사람이 정해야 한다.

- [ ] **Step 2: lobby-server 사본을 패키지로 옮기고 나머지 두 앱 사본을 지운다**

`git mv`로 lobby 사본을 `packages/server-core/src/<같은 경로>`에 옮기고, matchmaking·room 사본은 `git rm` 한다. 15개 전부.

- [ ] **Step 3: 패키지 내부 import를 상대 경로로 바꾼다**

옮긴 파일들이 서로를 별칭으로 참조하고 있다. 패키지 안에서는 **상대 경로만** 쓴다. 예:

```typescript
// packages/server-core/src/repositories/cacheCrudRepository.ts
import { CrudRepository } from './crudRepository.interface';
import { CrudDao } from '../daos/dao.interface';
import { DomainEntityMapper } from '../mappers/domain.entity.mapper';
```

`@lop/database`처럼 **다른 워크스페이스 패키지 import는 그대로 둔다**(`dao.postgres.base.ts`가 쓴다).

- [ ] **Step 4: 배럴을 채운다**

`packages/server-core/src/index.ts`에 15개를 `export * from './<경로>';`로 추가한다(Task 1의 `HttpException` 줄 유지). 경로 순서는 폴더별로 묶어 읽기 쉽게 둔다.

- [ ] **Step 5: 패키지 `package.json`의 dependencies를 채운다**

옮긴 파일이 실제로 import 하는 런타임 의존만 넣는다. 각 파일의 import 문을 보고 정하되, 최소한 이것들이 필요하다: `express`, `class-transformer`, `class-validator`, `envalid`, `mongoose`, `@lop/database`(`workspace:*`). `@types/*`는 devDependencies로.

> **직접 확인해서 넣어라.** 이 목록을 그대로 믿지 말고 `grep -h "^import" packages/server-core/src/**/*.ts`로 실제 사용을 확인하라. 안 쓰는 걸 넣으면 YAGNI 위반이고, 빠뜨리면 이미지에서 깨진다.

- [ ] **Step 6: 세 앱의 import를 교체한다**

옮긴 15개를 참조하던 모든 곳의 별칭 import를 `@lop/server-core`로 바꾼다. 한 파일에서 여러 개를 쓰면 한 줄로 합친다:

```typescript
import { CrudDao, DomainEntityMapper, CrudRepository } from '@lop/server-core';
```

- [ ] **Step 7: 죽은 별칭을 지운다**

세 앱의 `tsconfig.json` `paths`와 두 앱의 `jest.config.js` `moduleNameMapper`에서, **이제 앱에 남은 파일이 하나도 없는** 별칭만 지운다. 앱에 파일이 남아 있는 별칭(`@services/*`, `@dtos/*` 등)은 **그대로 둔다.**

> 예: `@daos/*`는 앱마다 앱 전용 DAO가 남아 있으므로 **유지**한다. `@mappers/*`도 마찬가지.
> 실제로 비었는지 `ls apps/<app>/src/<폴더>`로 확인하고 판단하라.

- [ ] **Step 8: 빌드·테스트·잔재 확인**

Run:
```bash
pnpm install && pnpm --filter @lop/server-core run build && pnpm --filter lobby-server run build && pnpm --filter matchmaking-server run build && pnpm --filter room-server run build && pnpm --filter matchmaking-server test && pnpm --filter room-server test
```
Expected: 전부 성공 (154 + 11 tests)

Run:
```bash
for f in controllers/index.controller.ts daos/dao.interface.ts interfaces/user-location.interface.ts repositories/cacheCrudRepository.ts utils/validateEnv.ts; do
  ls apps/*/src/$f 2>/dev/null
done
```
Expected: 출력 없음 (앱에서 사라졌다는 확인)

- [ ] **Step 9: 커밋**

```bash
git add -A packages apps pnpm-lock.yaml
git commit -m "refactor(server-core): 설정에 닿지 않는 15개 파일 이전"
```

---

## Task 3: 설정 분리

**Files:**
- Create: `packages/server-core/src/config/index.ts`
- Modify: `apps/{lobby,matchmaking,room}-server/src/config/index.ts` (이웃 주소만 남긴다)
- Modify: `packages/server-core/src/index.ts`, `packages/server-core/package.json`(dotenv)

**Interfaces:**
- Consumes: Task 2의 패키지
- Produces: `@lop/server-core`가 `NODE_ENV`/`PORT`/`LOG_FORMAT`/`LOG_DIR`/`CREDENTIALS`/`MONGODB_*`/`POSTGRES_*`/`REDIS_*`를 노출. 각 앱 `@config`는 이웃 서비스 주소만.

- [ ] **Step 1: 공용 설정을 만든다**

`packages/server-core/src/config/index.ts`:

```typescript
import { config } from 'dotenv';

//  dotenv는 여기서 한 번만 읽는다 — 앱 3개가 같은 규칙으로 같은 파일을 읽고 있었다.
config({ path: `.env.${process.env.NODE_ENV || 'development'}.${process.env.SPECIFIC_ENV || 'local'}` });

export const CREDENTIALS = process.env.CREDENTIALS === 'true';
export const { NODE_ENV, PORT, LOG_FORMAT, LOG_DIR } = process.env;
export const { MONGODB_HOST, MONGODB_PORT, MONGODB_DATABASE } = process.env;
export const { POSTGRES_HOST, POSTGRES_PORT, POSTGRES_DATABASE, POSTGRES_USER, POSTGRES_PASSWORD } = process.env;
export const { REDIS_HOST, REDIS_PORT } = process.env;
```

> 세 앱의 `config/index.ts`에서 **완전히 동일했던 부분을 그대로** 옮긴 것이다. 값을 바꾸거나 검증을 추가하지 마라.

배럴(`src/index.ts`)에 `export * from './config';`를 추가하고, `package.json` dependencies에 `dotenv`를 넣는다.

- [ ] **Step 2: 각 앱 설정을 이웃 주소만 남긴다**

`apps/lobby-server/src/config/index.ts`:

```typescript
//  이 앱이 부르는 이웃 서비스 주소. 인프라 설정(DB/Redis/로그/포트)은 @lop/server-core가 읽는다.
import '@lop/server-core';

export const { MATCH_MAKING_SERVER_HOST, MATCH_MAKING_SERVER_PORT } = process.env;
export const { ROOM_SERVER_HOST, ROOM_SERVER_PORT } = process.env;
```

matchmaking은 `LOBBY_SERVER_*` + `ROOM_SERVER_*`, room은 `LOBBY_SERVER_*` + `MATCH_MAKING_SERVER_*`.

> **`import '@lop/server-core';`가 중요하다** — 이 부수효과 import가 dotenv를 먼저 돌린다.
> 없으면 `process.env`가 아직 `.env` 파일을 안 읽은 상태라 이웃 주소가 `undefined`가 된다.
> 이 이유를 주석으로 남겨라(코드만 봐선 왜 쓰지도 않는 걸 import 하는지 알 수 없다).

- [ ] **Step 3: 인프라 설정을 쓰던 곳의 import를 바꾼다**

`from '@config'`로 인프라 값을 가져가던 곳을 `from '@lop/server-core'`로 바꾼다. **이웃 주소를 가져가던 곳은 `@config` 그대로 둔다.** 한 파일이 둘 다 쓰면 import가 두 줄이 된다.

Run으로 대상 확인:
```bash
grep -rn "from '@config'" apps/*/src --include=*.ts
```

- [ ] **Step 4: 빌드·테스트**

Run: Task 2 Step 8과 같은 명령
Expected: 전부 성공

- [ ] **Step 5: 커밋**

```bash
git add -A packages apps
git commit -m "refactor(server-core): 설정을 인프라(공용)와 이웃 주소(앱별)로 분리"
```

---

## Task 4: 설정에 닿는 나머지 10개 이전

**Files:**
- Move (각 3앱 → 패키지 1개): 아래 10개
- Modify: 세 앱의 import 소비처, `tsconfig.json`, `jest.config.js`, 배럴, 패키지 `package.json`

옮길 10개:

```
app.ts                      loaders/mongoose.loader.ts
caches/index.ts             loaders/postgres.loader.ts
caches/redis.cache.ts       middlewares/error.middleware.ts
daos/dao.redis.base.ts      utils/logger.ts
databases/mongodb/index.ts
databases/postgres/index.ts
```

**Interfaces:**
- Consumes: Task 3의 공용 설정
- Produces: `App`, `logger`/`stream`, `redisClient`/`redisConnection`, `prismaClient`, DAO redis base 등이 배럴로 노출

- [ ] **Step 1: `loaders/redis.loader.ts`를 함께 다룬다 (세미콜론 하나 차이)**

이 파일은 "동일 26개"에 없지만 **차이가 세미콜론 하나뿐**이고, 옮기는 10개 중 셋(`caches/redis.cache.ts`, `daos/dao.redis.base.ts`, `caches/index.ts`)이 이것을 참조한다. 함께 옮긴다.

Run으로 차이가 정말 그것뿐인지 먼저 확인:
```bash
diff apps/lobby-server/src/loaders/redis.loader.ts apps/matchmaking-server/src/loaders/redis.loader.ts
diff apps/lobby-server/src/loaders/redis.loader.ts apps/room-server/src/loaders/redis.loader.ts
```
Expected: 각각 `import` 줄의 세미콜론 유무 한 건. **그 외 차이가 나오면 멈추고 보고하라.**

- [ ] **Step 2: 11개 파일을 옮긴다**

Task 2 Step 2와 같은 방식(lobby 사본을 `git mv`, 나머지 둘 `git rm`). 패키지 내부 import는 상대 경로로, 설정은 `../config`에서 가져온다.

- [ ] **Step 3: `main.ts`·`loaders/index.ts`는 앱에 남긴다**

이 둘은 "3앱에 다 있지만 내용이 갈린" 파일이고 **각 앱의 조립부**다(어떤 라우트를 등록하는지, 어떤 로더를 어떤 순서로 부르는지). 옮기지 마라. import만 `@lop/server-core`로 바꾼다.

- [ ] **Step 4: 배럴·의존성·앱 import·별칭 정리**

Task 2의 Step 4~7과 같은 절차를 이 11개에 대해 반복한다. 이제 비는 별칭(`@caches/*`, `@databases/*`, `@loaders/*` 등)이 늘어나므로 **실제로 비었는지 확인하고** 지운다.

- [ ] **Step 5: `httpService.ts` 타임아웃 비대칭 — 손대지 말고 보고만 하라**

`services/httpServices/httpService.ts`는 매칭 서버에만 HTTP 타임아웃 5초가 있다(슬라이스 4b에서 추가). 이 파일은 **이번 이전 대상이 아니다**(3앱 내용이 다르다). 그대로 두고, 보고서에 현 상태만 적어라 — 세 앱에 같은 타임아웃이 옳은지는 사람이 정한다.

- [ ] **Step 6: 빌드·테스트·잔재 확인**

Run: Task 2 Step 8과 같은 명령
Expected: 전부 성공 (154 + 11 tests)

Run:
```bash
grep -rn "from '@config'" apps/*/src --include=*.ts | grep -v "SERVER_HOST\|SERVER_PORT" | head
```
Expected: 인프라 값을 `@config`에서 가져가는 곳이 남아 있지 않다

- [ ] **Step 7: 커밋**

```bash
git add -A packages apps
git commit -m "refactor(server-core): 설정에 닿는 11개 파일 이전"
```

---

## 검증·배포 (사람이 수행 — 서브에이전트 아님)

> 태스크가 아니다. 모든 태스크와 최종 리뷰가 끝난 뒤 **컨트롤러가 직접** 수행한다.

### 로컬 docker build 실증 (이 프로젝트의 진짜 관문)

Dockerfile 변경은 **로컬 `pnpm build`가 증거가 되지 못한다** — 워크스페이스 hoisting이 문제를 가린다.
과거 `db-migrate`가 정확히 이 방식으로 3주간 잠복해 깨져 있었다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
docker build -f apps/lobby-server/Dockerfile .
docker build -f apps/matchmaking-server/Dockerfile .
docker build -f apps/room-server/Dockerfile .
```

세 개 다 성공해야 한다. `--frozen-lockfile` 실패가 나오면 `pnpm-lock.yaml`이 커밋 안 된 것이다.

### 배포

1. 머지 후 push → `backend-deploy`를 **`app: all`** 로 실행
   (마이그레이션은 없지만 세 앱이 모두 바뀌었다)
2. ArgoCD 롤아웃 확인, 4개 파드 기동

### E2E

- [ ] 클라 2대 매칭 → 입장 → 게임 진행
- [ ] 매칭 취소 정상
- [ ] 네 앱 로그에 에러 0

> **동작 변화가 0임을 확인하는 것이 목적이다.** 뭔가 달라 보이면 그것이 곧 결함이다.
