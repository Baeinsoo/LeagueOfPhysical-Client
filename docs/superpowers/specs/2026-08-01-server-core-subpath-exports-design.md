# `@lop/server-core` 배럴 분해 — 서브패스 `exports` 설계

`@lop/server-core`의 진입점을 하나(배럴)에서 여럿(서브패스)으로 나눈다. **루트에는 순수 계약만 남기고,
외부 자원을 잡는 것은 전부 서브패스로 민다.**

## 1. 왜 — 실측부터

배럴을 한 번 `require` 하면 **1.65초 / 1518개 모듈**이 로드된다. 새 프로세스에서 모듈별로 재 보니
비용이 아주 깨끗하게 갈린다(캐시 오염을 피하려 각각 독립 프로세스로 측정):

| 모듈 | 시간 | 추가 모듈 |
|---|---|---|
| `exceptions/HttpException` · `interfaces/*` · `daos/dao.interface` | 3~5ms | **1** |
| `daos/dao.postgres.base` · `daos/dao.mongoose.base` | 4~5ms | **1** |
| `repositories/*` · `mappers/domain.entity.mapper` · `utils/redis-json.utils` | 3~4ms | **1** |
| `config` · `caches/index` · `databases/*` | 4~5ms | 1~2 |
| `utils/validateEnv` | 18ms | 10 |
| `loaders/postgres.loader` | 36ms | 5 |
| **`utils/logger`** | 164ms | **97** |
| **`middlewares/error.middleware`** | 116ms | **98** |
| **`middlewares/validation.middleware`** | 290ms | **322** |
| **`routes/index.route`** | 263ms | **125** |
| **`app`** | 360ms | **252** |
| **`loaders/mongoose.loader`** | 397ms | **416** |
| **`loaders/redis.loader`** | 466ms | **501** |
| **`caches/redis.cache`** | 443ms | **502** |
| **`daos/dao.redis.base`** | 763ms | **502** |

**비용의 99% 이상이 다섯 덩어리(redis / mongoose / express / 검증 / logger)에 있다. 타입은 사실상 공짜다.**

그리고 소비 측을 세어 보니 — 배럴을 쓰는 앱 파일 **82개 중 52개(63%)가 가벼운 것만** 쓴다.
5ms어치를 받으려고 1.65초를 내고 있다.

## 2. 방향을 뒤집는다 — 루트가 가벼운 쪽

처음 구상은 "무거운 걸 루트에 두고 가벼운 걸 서브패스로"였다. 그러면 **다수인 52개 파일**을 고쳐야 한다.
반대로 하면 **52개는 손도 안 대고 30개만** 고친다. 그리고 "기본은 싸고, 무게는 옵트인"이 표준 모양이다.

## 3. 무엇을 기준으로 가르나 — 잰 무게가 아니라 **자원**

두 후보가 있었다:

- **A — 잰 무게로 가른다.** `prismaClient`는 36ms라 싸니까 루트에 남는다. 고칠 파일 최소.
  대신 "루트를 import 하면 DB 클라이언트가 생긴다"가 남고, `DaoRedisBase`는 서브패스인데
  `DaoMongooseBase`는 루트인 비대칭이 생긴다.
- **B — 외부 자원을 잡는가로 가른다.** ✅ **채택.** 루트에는 **자원을 하나도 만들지 않는 것만** 남는다.

**B를 고른 이유:** 오래 가는 가치는 36ms가 아니라 **"루트를 가져와도 아무 자원도 안 생긴다"** 는 성질이다.
잰 무게는 의존성 업그레이드 한 번에 바뀌지만, 자원을 잡느냐는 안 바뀐다. 서브패스 이름도
`postgres`/`redis`/`mongoose`로 예측 가능해진다.

## 4. 산업 표준 매핑 (실물 확인)

Node 공식 문서: `"."`를 주 진입점으로 두고 서브패스를 함께 정의하는 것이 표준이며, **`exports`를 쓰면
나머지 내부 경로는 캡슐화**되어 `ERR_PACKAGE_PATH_NOT_EXPORTED`가 난다.

npm 레지스트리에서 실제 `exports` 맵을 열어 확인한 것:

| 패키지 | 모양 |
|---|---|
| **firebase** 12.17 | 33 서브패스. `./app`(가벼운 코어) vs `./firestore`·`./auth`·`./storage`(무거운 SDK) — 무게를 옵트인 |
| **@sentry/node** 10.69 | `./init`·`./preload` — **부수효과 진입점을 본체와 분리**. 우리 경우와 가장 가깝다 |
| **jotai** 2.20 | 코어 + `./utils` |
| **date-fns** 4.4 | 741 서브패스(함수 단위) |
| **zod** 4.4 | `.`의 조건 순서가 `types > import > require` |

우리 매핑: 루트 = firebase의 `./app`(계약), 서브패스 = `./firestore`류(인프라) + Sentry의 `./init`(부수효과).

## 5. 최종 배치

### 루트 `.` — 순수 계약 (자원 0)

| 대상 | 비고 |
|---|---|
| `config` | env 상수 읽기만 |
| `exceptions/HttpException` | |
| `interfaces/responseBase` · `routes` · `user-location` | `ResponseBase`(19곳)·`Location`(13곳)·`Routes`(9곳) |
| `daos/dao.interface` · `dao.postgres.base` · `dao.mongoose.base` | **둘 다 타입으로만** prisma/mongoose를 쓴다(런타임 0모듈) |
| `repositories/repository.interface` · `crudRepository.interface` · `cacheCrudRepository` | |
| `mappers/domain.entity.mapper` | |
| `utils/redis-json.utils` · `utils/validateEnv` | `validateEnv`는 18ms 순수 함수 — 자원을 안 잡는다 |

### 서브패스 — 자원을 잡는 것

| 서브패스 | 담기는 것 | 고칠 앱 파일 |
|---|---|---|
| `./logger` | `logger`, `stream` | 10 |
| `./postgres` | `postgresConnection`, `load`, `prismaClient` | 11 |
| `./redis` | `redisConnection`, `load`, `redisClient`, `RedisCache`, **`DaoRedisBase`** | 9 |
| `./mongoose` | `mongodbConnection`, `load` | 3 |
| `./express` | `App`, `IndexRoute`, `IndexController`, `errorMiddleware`, `validationMiddleware` | 9 |

중복을 빼면 **고칠 앱 파일 30개**(그중 6개는 서브패스를 둘 이상 함께 쓴다). 나머지 **52개는 그대로**다.

> **`DaoRedisBase`가 왜 `/redis`에 있나** — 이 클래스가 모듈 최상단에서 `redisClient` 싱글턴을 직접
> 붙잡고 있어서다(형제인 `DaoPostgresBase`·`DaoMongooseBase`는 안 그런다). 이번엔 **고치지 않고
> 서브패스가 그 결합을 드러내게 둔다.** 주입식으로 바꾸면 서브클래스 6개가 딸려 와 범위가 커진다 —
> §9 후속으로 남긴다.

## 6. `exports` 맵 — `types` 함정 둘

```jsonc
{
  "main":  "./dist/index.js",      // exports를 무시하는 낡은 도구용 폴백 (TS 문서 권고)
  "types": "./dist/index.d.ts",
  "exports": {
    ".":            { "types": "./dist/index.d.ts",             "default": "./dist/index.js" },
    "./logger":     { "types": "./dist/entries/logger.d.ts",    "default": "./dist/entries/logger.js" },
    "./postgres":   { "types": "./dist/entries/postgres.d.ts",  "default": "./dist/entries/postgres.js" },
    "./redis":      { "types": "./dist/entries/redis.d.ts",     "default": "./dist/entries/redis.js" },
    "./mongoose":   { "types": "./dist/entries/mongoose.d.ts",  "default": "./dist/entries/mongoose.js" },
    "./express":    { "types": "./dist/entries/express.d.ts",   "default": "./dist/entries/express.js" },
    "./package.json": "./package.json"
  }
}
```

**규칙 ①(필수) — `exports`가 생기면 최상위 `types` 필드는 그 경로에 더 이상 적용되지 않는다.**
그래서 **모든 항목이 자기 `types`를 들고 있어야 한다.** `dotenv@10`이 정확히 이것으로 깨졌다:
`exports`는 조건 없는 문자열(`{".": "./lib/main.js"}`)이고 타입은 최상위 `types/index.d.ts`에만 있어,
TS가 `lib/main.d.ts`를 찾다 실패해 TS7016을 냈다. v16이 맵 안으로 `types`를 넣어 고쳤다.

**규칙 ②(안전장치) — 조건 객체 안에서 `types`를 맨 앞에.** Node 문서가 "키 순서가 유의미하며 앞 항목이
우선"이라고 못박는다. 뒤에 두어도 도는 사례(firebase)가 있지만 그건 선언 파일이 JS 옆에 있어서다.
둘 다 지킨다.

> **deep import가 막힌다** — `@lop/server-core/dist/...`는 이제 에러가 난다. 실제 코드에 그런 import는
> 0곳이라 무해하고, 오히려 **캡슐화 이득**이다(공개면이 맵에 적힌 6개로 고정).

## 7. jest — `exports`를 안 본다 (반드시 함께 고칠 것)

세 앱의 `jest.config.js`가 `moduleNameMapper`로 패키지를 **소스 폴더에 직접** 꽂는다:

```js
'^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
```

이 패턴은 `$`로 끝나 **서브패스를 매칭하지 않는다.** 그대로 두면 서브패스만 node 해석을 타고 `dist`로
가서 **한 테스트 안에 src와 dist 두 벌**이 뜬다(모노레포에서 알려진 함정). 매퍼를 추가한다:

```js
'^@lop/server-core$':      '<rootDir>/../../packages/server-core/src',
'^@lop/server-core/(.*)$': '<rootDir>/../../packages/server-core/src/entries/$1',
```

대상은 `apps/matchmaking-server`(19스위트)·`apps/room-server`(1스위트). lobby-server는 jest 설정도
테스트도 없다.

**기존 mock도 옮겨야 한다.** `match.dao.postgres.test.ts`·`matchmakingTicket.dao.postgres.test.ts`가
`jest.mock('@lop/server-core', …)`로 `prismaClient`를 스텁하는데, 그게 `/postgres`로 가므로
`jest.mock('@lop/server-core/postgres', …)`가 된다.

## 8. 검증

- 타입체크·빌드 5/5 (캐시 없이), 테스트 154 + 11
- **효과 실측** — 새 프로세스에서 루트 배럴 import 비용: **1.65초/1518모듈 → 50ms 미만**을 기대. 재서 기록한다
- **모듈 두 벌 없음** — 서브패스로 두 번 가져온 `redisClient`가 동일 인스턴스인지, jest에서 `dist`가
  아니라 `src`를 탔는지 확인
- **로컬 `docker build` 3종** — 로컬 `pnpm build`는 워크스페이스 hoisting이 문제를 가린다(전례 있음)
- 배포 후 4파드 기동 + 에러 0 + 2클라 E2E

## 9. 범위 밖 / 후속

- **`DaoRedisBase`의 `redisClient` 직접 참조 해소** — 주입식으로 바꾸면 루트로 올라올 수 있다. 별건
- `exports`에 `require`/`import` 조건 분리 (지금은 CJS 단일 산출물이라 불필요)
- `attw`(are-the-types-wrong) 도입 — 지금은 private 워크스페이스 패키지라 선택
- Unity 클라이언트·게임 서버 (무관)
