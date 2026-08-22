# 슬라이스 1 — 플레이어 신원 (스키마 + 태그 + 개명) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 가입하면 `플레이어#K7QM2X` 같은 신원이 붙고, 유저가 이름을 바꿀 수 있다. **화면은 안 바뀐다.**

**Architecture:** `User.username`(로그인에 안 쓰이는 표시 이름 자리)을 `displayName`으로 개명하고
`@unique`를 뗀다. 대신 **전역 유일한 Crockford Base32 6자리 `tag`** 를 가입 때 부여한다. 태그가
유일성을 지므로 이름은 자유이고 **개명이 구조적으로 거절되지 않는다.**

**Tech Stack:** Prisma 6 + PostgreSQL · pnpm/turbo 모노레포 · jest + testcontainers · Unity(DTO만)

**Spec:** `docs/superpowers/specs/2026-08-21-player-identity-design.md`
(클라 레포: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\docs\superpowers\specs\...`)

---

## Global Constraints

- **레포 2개**: `lop-backend`(브랜치 `feature/player-identity`) + `LeagueOfPhysical-Client`(DTO만).
  게임서버는 **안 건드린다** — `username`을 읽는 곳이 없다(확인됨).
- **⚠️ 이건 와이어 계약 변경이다.** `UserResponseDto.username` → `displayName`. 클라의
  `UserDto`/`User`/`CreateUserRequest`가 그 필드를 들고 있으므로 **같은 슬라이스에서 함께 고친다.**
  한쪽만 바꾸면 예외 없이 **조용히 null이 들어간다**(화면에 안 쓰여서 증상도 안 뜬다).
- **유일성은 DB 제약이 진다.** 태그 부여는 "무작위로 넣고 걸리면 다시 뽑기"다. 미리 조회해서 빈 걸
  찾는 방식(check-then-insert)은 **경합에서 뚫린다** — 두 요청이 같은 빈 태그를 동시에 본다.
- **이름에 유일성 검사를 넣지 않는다.** 넣는 순간 이 설계의 목적(개명이 절대 안 막힘)이 사라진다.
- **빌드가 테스트보다 먼저다.** 검증은 항상 `pnpm exec turbo run build --force`를 맨 앞에 —
  ts-jest는 import 안 되는 파일의 타입 오류를 건너뛴다. `--force` 없으면 캐시가 통과를 위조한다.
- **삭제·개명은 역방향으로 검증한다.** `username`을 없앤 뒤 **아직 그 이름을 부르는 곳**을 두 레포에서
  grep한다(정방향 "새 이름이 다 반영됐나"는 그 다음).
- **마이그레이션을 실DB에 psql로 직접 밀지 말 것.** Prisma가 `_prisma_migrations`로 이력을 추적해서,
  손으로 넣으면 배포가 같은 걸 또 돌리다 실패한다. 검증은 복제본에서:
  `pg_dump -U postgres postgres | psql -U postgres -d scratch` (배타 락 불필요).
- 주석은 **왜**만. 한국어.

---
## Task 1: 스키마 + 마이그레이션

**Files:**
- Modify: `packages/database/prisma/schema.prisma`
- Create: `packages/database/prisma/migrations/20260822000000_player_identity/migration.sql`

**Interfaces:**
- Produces: Prisma `User`가 `displayName: string`(유일 아님) + `tag: string`(`@unique`)을 갖는다.
  `username`은 **사라진다**. Task 2~4가 이걸 쓴다.

- [ ] **Step 1: 스키마 교체**

`model User`를 아래로 바꾼다:

```prisma
//  로그인은 UserIdentity(provider, providerUserId)가 한다 — 여기 이름은 표시용이다.
model User {
  id          String    @id @default(uuid())
  //  자유·중복 허용. 유일성은 tag가 진다.
  displayName String
  //  Crockford Base32 6자리. 가입 때 부여하고 이후 바꾸지 않는다 — 이 값이 유일해야
  //  이름을 아무렇게나 지어도 신원이 안 겹치고, 그래서 개명이 거절되지 않는다.
  tag         String    @unique
  email       String?   @unique
  createdAt   DateTime  @default(now())
  updatedAt   DateTime  @updatedAt
  lastLoginAt DateTime?
}
```

- [ ] **Step 2: 마이그레이션 작성**

기존 유저(게스트 몇 명)에게도 태그를 줘야 한다. **SQL 안에서 Crockford 알파벳으로 생성**한다 —
`I`·`L`·`O`·`U`를 뺀 32자다.

```sql
--  username은 로그인에 쓰이지 않는 표시 이름 자리였다(로그인은 UserIdentity가 한다).
--  이름을 제대로 붙이고 @unique를 뗀다 — 유일성은 아래 tag가 진다.
ALTER TABLE "User" RENAME COLUMN "username" TO "displayName";
DROP INDEX IF EXISTS "User_username_key";

--  Crockford Base32: 숫자 10 + 알파벳 22(I·L·O 제외 = 1·0과 혼동, U 제외 = 우연한 욕설).
--  기존 행에 줄 태그를 만든다. 6자리라 10억 가짓수이고 기존 유저는 한 자릿수라 충돌 확률은 무시 가능하지만,
--  아래 유니크 인덱스가 최종 판정을 한다 — 실패하면 마이그레이션이 멈추고 그게 맞다.
ALTER TABLE "User" ADD COLUMN "tag" TEXT;

UPDATE "User" SET "tag" = (
    SELECT string_agg(
        substr('0123456789ABCDEFGHJKMNPQRSTVWXYZ', (floor(random() * 32) + 1)::int, 1), ''
    )
    FROM generate_series(1, 6)
);

ALTER TABLE "User" ALTER COLUMN "tag" SET NOT NULL;
CREATE UNIQUE INDEX "User_tag_key" ON "User"("tag");

--  기존 게스트 이름(Guest-<uuid>)은 사람이 읽으라고 만든 게 아니다. 기본 이름으로 되돌리고
--  태그가 구분을 맡는다.
UPDATE "User" SET "displayName" = '플레이어' WHERE "displayName" LIKE 'Guest-%';
```

> ⚠️ `UPDATE`의 서브쿼리가 **행마다 다시 평가되는지** 확인해야 한다. Postgres에서 상관 없는
> 서브쿼리는 한 번만 평가돼 **모든 행이 같은 태그**를 받을 수 있고, 그러면 유니크 인덱스에서 멈춘다.
> Step 4의 검증이 이걸 잡는다 — 멈추면 `random()`이 행마다 돌도록 고친다(예:
> `WHERE id = "User".id` 같은 상관 조건을 넣거나, 행별 `md5(random()::text || id)` 기반으로 바꾼다).

- [ ] **Step 3: 스키마 검증**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database exec prisma validate
```

기대: valid.

- [ ] **Step 4: 복제본에서 마이그레이션 검증**

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'DROP DATABASE IF EXISTS scratch;'
kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'CREATE DATABASE scratch;'
kubectl exec deploy/postgres-deployment -- sh -c 'pg_dump -U postgres postgres | psql -U postgres -d scratch -q'
kubectl exec -i deploy/postgres-deployment -- psql -U postgres -d scratch -v ON_ERROR_STOP=1 \
  < packages/database/prisma/migrations/20260822000000_player_identity/migration.sql
```

기대: 무오류. 그리고 **태그가 행마다 다른지** 확인한다(위 ⚠️ 가 여기서 드러난다):

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d scratch -c \
  'SELECT count(*) AS 유저수, count(DISTINCT "tag") AS 태그종류, min(length("tag")) AS 최소길이, max(length("tag")) AS 최대길이 FROM "User";'
kubectl exec deploy/postgres-deployment -- psql -U postgres -d scratch -c \
  'SELECT "displayName", "tag" FROM "User" ORDER BY "createdAt" LIMIT 5;'
```

기대: **유저수 == 태그종류**(전부 다름), 길이 6, 이름은 `플레이어`.
`tag`에 `I`·`L`·`O`·`U`가 없는지도 눈으로 본다.

정리: `kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'DROP DATABASE scratch;'`

- [ ] **Step 5: 클라이언트 재생성 + 빌드**

```bash
pnpm --filter @lop/database run generate
pnpm exec turbo run build --force
```

기대: **빌드가 깨진다**(`username`이 사라져서). 깨진 파일 목록을 보고서에 적는다 — Task 2·3의 작업
목록이 된다.

- [ ] **Step 6: 커밋**

```bash
git add packages/database/prisma/schema.prisma packages/database/prisma/migrations
git commit -m "feat(user): 표시 이름과 태그로 신원을 나눈다"
```

`packages/database/generated/`는 빌드 산출물이다 — `git status --short`로 확인.

---

## Task 2: 태그 생성 + 가입 경로

**Files:**
- Create: `apps/lobby-server/src/utils/tag.ts`
- Create: `apps/lobby-server/src/utils/__tests__/tag.test.ts`
- Modify: `apps/lobby-server/src/services/user.service.ts`
- Modify: `apps/lobby-server/src/services/auth/auth.service.ts`
- Modify: `apps/lobby-server/src/dtos/user.dto.ts`
- Modify: `apps/lobby-server/src/interfaces/user.interface.ts`
- Modify: `apps/lobby-server/src/factories/user.factory.ts`
- Modify: `apps/lobby-server/src/mappers/controllers/user.mapper.ts`
- Modify: `apps/lobby-server/src/mappers/entities/user.mapper.ts`

**Interfaces:**
- Produces: `generateTag(): string` (Crockford Base32 6자리 대문자),
  `UserService.createUser({ displayName })` — 호출자가 태그를 안 준다(서비스가 뽑는다).
  `UserResponseDto { id, displayName, tag, email }`.

- [ ] **Step 1: 태그 생성기 — 실패하는 테스트 먼저**

`apps/lobby-server/src/utils/__tests__/tag.test.ts`:

```ts
import { CROCKFORD_ALPHABET, generateTag } from '@utils/tag';

describe('generateTag', () => {
    it('6자리다', () => {
        expect(generateTag()).toHaveLength(6);
    });

    it('Crockford 알파벳만 쓴다', () => {
        //  I·L·O는 1·0과 헷갈리고 U는 우연한 욕설이 될 수 있어 뺀다.
        //  사람이 불러주고 받아적는 코드라 이 제외가 핵심이다.
        for (let i = 0; i < 200; i += 1) {
            for (const ch of generateTag()) {
                expect(CROCKFORD_ALPHABET).toContain(ch);
            }
        }
    });

    it('I·L·O·U가 절대 안 나온다', () => {
        for (let i = 0; i < 200; i += 1) {
            expect(generateTag()).not.toMatch(/[ILOU]/);
        }
    });

    it('대문자다', () => {
        expect(generateTag()).toBe(generateTag().toUpperCase());
    });

    it('매번 같은 값이 나오지 않는다', () => {
        const tags = new Set(Array.from({ length: 50 }, () => generateTag()));
        //  6자리 10억 공간에서 50개가 전부 같을 확률은 사실상 0이다. 상수를 반환하는 구현을 잡는다.
        expect(tags.size).toBeGreaterThan(40);
    });
});
```

- [ ] **Step 2: 실패 확인**

```bash
pnpm --filter lobby-server exec jest --testPathPattern tag
```

기대: 모듈 없음으로 실패.

- [ ] **Step 3: 구현**

`apps/lobby-server/src/utils/tag.ts`:

```ts
import { randomInt } from 'crypto';

/**
 * Crockford Base32 — 숫자 10 + 알파벳 22.
 * I·L·O를 뺀 것은 1·0과 눈으로 구분이 안 되기 때문이고, U를 뺀 것은 무작위 조합이 우연히
 * 욕설이 되는 걸 줄이기 위해서다. 사람이 불러주고 받아적는 코드라 이 제외가 핵심이다.
 */
export const CROCKFORD_ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

const TAG_LENGTH = 6;

/**
 * 계정 태그 한 개. 6자리라 10억 가짓수 — 용량이 아니라 **찍어서 맞히기**를 막는 값이다
 * (계정 10만일 때 무작위 한 번에 맞힐 확률 1/10,737).
 *
 * 유일성은 이 함수가 아니라 DB의 unique 제약이 보장한다 — 여기서 미리 조회해 빈 값을 찾으면
 * 두 요청이 같은 값을 동시에 보고 둘 다 쓴다.
 */
export function generateTag(): string {
    let tag = '';
    for (let i = 0; i < TAG_LENGTH; i += 1) {
        tag += CROCKFORD_ALPHABET[randomInt(CROCKFORD_ALPHABET.length)];
    }

    return tag;
}
```

- [ ] **Step 4: 통과 확인**

기대: 5건 전부 PASS.

- [ ] **Step 5: 도메인·DTO·매퍼에서 username을 걷어낸다**

아래 파일에서 `username` → `displayName`으로 바꾸고 `tag`를 더한다. **각 파일의 실제 모양을 읽고**
그 자리에 맞춰 고칠 것(아래는 바뀐 뒤의 형태다):

`interfaces/user.interface.ts`:
```ts
export interface User {
    id: string;
    displayName: string;
    tag: string;
    email: string | null;
    ...
}
```

`dtos/user.dto.ts` — `CreateUserDto`는 **태그를 받지 않는다**(서비스가 뽑는다):
```ts
export class CreateUserDto {
    @IsString()
    public displayName: string;

    @IsOptional()
    @IsString()
    public email?: string;
}

export class UserResponseDto {
    public id: string;
    public displayName: string;
    public tag: string;
    public email: string | null;
}
```

`factories/user.factory.ts`: `username: ''` → `displayName: '', tag: ''`
(팩토리 기본값은 자리만 잡는다 — 실제 태그는 서비스가 넣는다).

두 매퍼(`mappers/controllers/user.mapper.ts`, `mappers/entities/user.mapper.ts`)도 같은 규칙으로.

- [ ] **Step 6: 가입이 태그를 부여하게 한다**

`UserService.createUser`에서 엔티티를 만든 **직후, 저장 전에** 태그를 넣고, 유니크 충돌이면 다시 뽑는다.

기존 `user = await this.userRepository.save(user);` 를 아래로 바꾼다:

```ts
            let user = UserMapper.CreateUserDto.toEntity(createUserDto);
            user = await this.saveWithFreshTag(user);
```

그리고 같은 클래스에 추가:

```ts
    //  태그는 무작위로 넣고 유니크 제약에 걸리면 다시 뽑는다. 미리 조회해 빈 값을 찾으면
    //  두 요청이 같은 값을 동시에 보고 둘 다 쓴다 — 유일성은 DB가 지고 여기는 실패를 되받을 뿐이다.
    //  10억 공간에 계정 10만이면 한 번에 걸릴 확률이 1/10,737이라 재시도는 사실상 안 일어난다.
    private async saveWithFreshTag(user: User): Promise<User> {
        for (let attempt = 0; attempt < TAG_ATTEMPTS; attempt += 1) {
            try {
                return await this.userRepository.save({ ...user, tag: generateTag() });
            } catch (error) {
                if (!isUniqueViolation(error, 'tag')) {
                    throw error;
                }
            }
        }

        //  여기까지 왔다면 태그 공간이 찼거나 생성기가 고장 난 것이다. 조용히 넘기지 않는다.
        throw new Error(`Failed to assign a unique tag after ${TAG_ATTEMPTS} attempts.`);
    }
```

파일 상단에 `const TAG_ATTEMPTS = 5;`와 `import { generateTag } from '@utils/tag';`.

> **`isUniqueViolation`을 지어내지 말 것.** 이 레포에 **이미 같은 판별을 하는 선례가 있다** —
> `apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts:86` 부근이
> `error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2002'`로 검사하고
> `error.meta.target`으로 어느 제약인지까지 가른다. **그 파일을 열어 같은 모양으로** 쓸 것.

- [ ] **Step 7: 게스트 가입이 기본 이름을 쓰게 한다**

`auth.service.ts`의 `createUser` 호출과 그 위 주석(“username은 @unique다…”)을 바꾼다:

```ts
        //  이름은 유일하지 않아도 된다 — 태그가 신원을 가른다. 그래서 모두 같은 기본 이름으로
        //  시작하고, 유저가 원할 때 바꾼다.
        const createUser = await this.userService.createUser({
            displayName: DEFAULT_DISPLAY_NAME,
        });
```

파일 상단에 `const DEFAULT_DISPLAY_NAME = '플레이어';`.

- [ ] **Step 8: 빌드 + 테스트**

```bash
pnpm exec turbo run build --force
pnpm --filter lobby-server test
```

기대: lobby-server 빌드 통과(다른 앱은 Task 3 몫일 수 있다 — 남은 에러 목록을 보고서에).
`Cached: 0 cached` 확인.

- [ ] **Step 9: 커밋**

```bash
git add apps/lobby-server
git commit -m "feat(user): 가입 때 태그를 부여한다"
```

---

## Task 3: 개명 라우트 + 전적에 신원 박기 + 잔여 정리

**Files:**
- Create: `apps/lobby-server/src/routes/display-name.route.ts`
- Create: `apps/lobby-server/src/controllers/display-name.controller.ts`
- Create: `apps/lobby-server/src/services/display-name.service.ts`
- Create: `apps/lobby-server/src/services/__tests__/display-name.service.test.ts`
- Create: `apps/lobby-server/src/dtos/display-name.dto.ts`
- Modify: `apps/lobby-server/src/main.ts` (라우트 등록)
- Modify: `packages/server-core/src/interfaces/responseCode.interface.ts`
- Modify: `apps/lobby-server/src/daos/match-result.dao.postgres.ts`
- Modify: `apps/matchmaking-server/src/dtos/user.dto.ts`, `apps/room-server/src/dtos/user.dto.ts`

**Interfaces:**
- Consumes: Task 1의 `displayName`/`tag`, Task 2의 도메인·DTO.
- Produces: `PUT /user/{userId}/display-name` (본인만). 응답은 `{ code, user? }`.

- [ ] **Step 1: 응답 코드 추가**

`packages/server-core/src/interfaces/responseCode.interface.ts`의 `USER_NOT_EXIST = 30000` 아래:

```ts
    public static readonly INVALID_DISPLAY_NAME = 30001;
```

- [ ] **Step 2: 이름 검증 — 실패하는 테스트 먼저**

`apps/lobby-server/src/services/__tests__/display-name.service.test.ts`:

```ts
import DisplayNameService from '@services/display-name.service';

describe('DisplayNameService.normalize', () => {
    it('앞뒤 공백을 떼고 돌려준다', () => {
        expect(DisplayNameService.normalize('  철수  ')).toBe('철수');
    });

    it('2자 미만은 거절', () => {
        expect(DisplayNameService.normalize('가')).toBeNull();
        expect(DisplayNameService.normalize('   ')).toBeNull();
    });

    it('12자 초과는 거절', () => {
        expect(DisplayNameService.normalize('가'.repeat(13))).toBeNull();
    });

    it('12자는 통과', () => {
        expect(DisplayNameService.normalize('가'.repeat(12))).toBe('가'.repeat(12));
    });

    it('중간 공백·제어문자는 거절', () => {
        //  공백을 허용하면 "철  수"와 "철 수"가 눈으로 구분이 안 된다.
        expect(DisplayNameService.normalize('철 수')).toBeNull();
        expect(DisplayNameService.normalize(String.fromCharCode(52384, 9, 49688))).toBeNull();
        expect(DisplayNameService.normalize(String.fromCharCode(52384, 10, 49688))).toBeNull();
    });

    it('빈 값·null은 거절', () => {
        expect(DisplayNameService.normalize('')).toBeNull();
        expect(DisplayNameService.normalize(undefined as any)).toBeNull();
    });
});
```

> 탭·개행을 테스트에 쓸 때 `String.fromCharCode`를 쓴 것은 **에디터나 도구가 그 문자를 삼키는 일을
> 막기 위해서**다. 소스에 리터럴로 박으면 나중에 누가 자동 포맷을 돌릴 때 조용히 사라진다.

- [ ] **Step 3: 실패 확인**

```bash
pnpm --filter lobby-server exec jest --testPathPattern display-name
```

기대: 모듈 없음으로 실패.

- [ ] **Step 4: 서비스 구현**

`apps/lobby-server/src/services/display-name.service.ts`:

```ts
import { ResponseCode } from '@lop/server-core';
import { UserRepository } from '@repositories/user.repository';
import { UserMapper } from '@mappers/controllers/user.mapper';
import { ChangeDisplayNameResponseDto } from '@dtos/display-name.dto';

const MIN_LENGTH = 2;
const MAX_LENGTH = 12;

class DisplayNameService {

    private userRepository = new UserRepository();

    public async change(userId: string, raw: string): Promise<ChangeDisplayNameResponseDto> {
        try {
            const displayName = DisplayNameService.normalize(raw);
            if (displayName === null) {
                return { code: ResponseCode.INVALID_DISPLAY_NAME };
            }

            const user = await this.userRepository.findById(userId);
            if (!user) {
                return { code: ResponseCode.USER_NOT_EXIST };
            }

            //  유일성 검사가 없다 — 이름은 겹쳐도 되고 신원은 tag가 가른다.
            //  그래서 이 경로에 "이미 사용 중" 실패가 존재하지 않는다.
            const saved = await this.userRepository.save({ ...user, displayName });

            return { code: ResponseCode.SUCCESS, user: UserMapper.toUserResponseDto(saved) };
        } catch (error) {
            return Promise.reject(error);
        }
    }

    /** 통과하면 다듬은 이름, 아니면 null. 거절 사유는 형식뿐이다. */
    public static normalize(raw: string): string | null {
        if (typeof raw !== 'string') {
            return null;
        }

        const trimmed = raw.trim();
        if (trimmed.length < MIN_LENGTH || trimmed.length > MAX_LENGTH) {
            return null;
        }

        //  중간 공백을 허용하면 "철  수"와 "철 수"가 눈으로 구분이 안 된다. 제어문자도 같은 이유.
        if (/\s/.test(trimmed)) {
            return null;
        }

        return trimmed;
    }
}

export default DisplayNameService;
```

> `UserRepository`는 `CacheCrudRepository<User, UserEntity>`를 상속한다 — `findById`/`save`는 베이스에
> 있다. **베이스 클래스를 열어 실제 시그니처를 확인하고 맞출 것**(캐시가 끼어 있으므로 저장 후 캐시
> 처리가 어떻게 되는지도 함께 본다). 이름이 다르면 그것을 쓴다.

- [ ] **Step 5: DTO·컨트롤러·라우트**

`dtos/display-name.dto.ts`:

```ts
import { IsString } from 'class-validator';
import { ResponseBase } from '@lop/server-core';
import { UserResponseDto } from '@dtos/user.dto';

export class ChangeDisplayNameDto {
    @IsString()
    public displayName: string;
}

export class ChangeDisplayNameResponseDto implements ResponseBase {
    public code: number;
    public user?: UserResponseDto;
}
```

컨트롤러·라우트는 **`user-rating.controller.ts`/`user-rating.route.ts`와 같은 모양**으로 만든다
(그 두 파일을 열어 그대로 따를 것). 라우트의 핵심은:

```ts
        this.router.put(
            `${this.path}/:userId/display-name`,
            authenticatePrincipal,
            requireSelfOrService('userId'),
            validationMiddleware(ChangeDisplayNameDto, 'body'),
            this.displayNameController.changeDisplayName,
        );
```

`main.ts`의 라우트 목록에 `new DisplayNameRoute()`를 더한다.

- [ ] **Step 6: 전적에 신원을 박는다**

`match-result.dao.postgres.ts`의 이름 조회를 바꾼다. 기존:

```ts
                const users = await tx.user.findMany({
                    where: { id: { in: 명단 } },
                    select: { id: true, username: true },
                });
                const 이름 = new Map(users.map(u => [u.id, u.username]));
```

교체:

```ts
                const users = await tx.user.findMany({
                    where: { id: { in: 명단 } },
                    select: { id: true, displayName: true, tag: true },
                });
                //  이름만 담으면 동명이인이 구분되지 않는다. 태그까지 붙여야 "그때 누구였나"가 남는다.
                const 이름 = new Map(users.map(u => [u.id, `${u.displayName}#${u.tag}`]));
```

- [ ] **Step 7: 다른 앱의 DTO 정리 + 역방향 검증**

`apps/matchmaking-server/src/dtos/user.dto.ts`와 `apps/room-server/src/dtos/user.dto.ts`의
`username` 필드를 `displayName`으로 바꾸고 `tag`를 더한다.

**없앤 이름을 아직 부르는 곳**을 찾는다(정방향이 아니라 역방향이다):

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
grep -rn "username" apps/ packages/ --include=*.ts | grep -v node_modules | grep -v generated
```

기대: **0건**. 테스트 파일에서 걸리면 그것도 고친다. 출력을 보고서에 그대로 붙인다.

- [ ] **Step 8: 통합 테스트**

`apps/lobby-server/test/integration/`에 개명 통합 테스트를 더한다. **기존 파일의 헬퍼 모양을 먼저
읽고** 맞출 것(지어내지 말 것).

```ts
    it('같은 이름을 여럿이 써도 성공한다', async () => {
        //  이 설계의 핵심이다 — 이름은 겹쳐도 되고 신원은 tag가 가른다.
        //  여기가 실패하면 어딘가에 유일성 검사가 들어간 것이다.
        const a = await 유저를_만든다();
        const b = await 유저를_만든다();

        expect((await service.change(a.id, '철수')).code).toBe(ResponseCode.SUCCESS);
        expect((await service.change(b.id, '철수')).code).toBe(ResponseCode.SUCCESS);
    });

    it('가입하면 서로 다른 태그를 받는다', async () => {
        const a = await 유저를_만든다();
        const b = await 유저를_만든다();

        expect(a.tag).not.toBe(b.tag);
        expect(a.tag).toHaveLength(6);
    });
```

기존 `matchResult` 통합의 **"개명해도 전적의 이름은 안 바뀐다"** 테스트가 여전히 통과해야 한다 —
이제 저장값이 `이름#태그`이므로 기대값을 그 모양으로 맞춘다(검증하는 성질은 그대로다).

- [ ] **Step 9: 빌드 + 전체 테스트**

```bash
pnpm exec turbo run build --force
pnpm --filter lobby-server test
pnpm --filter lobby-server run test:integration
pnpm --filter matchmaking-server test
pnpm --filter matchmaking-server run test:integration
```

기대: 빌드 6/6(`Cached: 0 cached`), 네 스위트 전부 통과. 요약을 보고서에 그대로 붙인다.

- [ ] **Step 10: 커밋**

```bash
git add apps packages
git commit -m "feat(user): 이름을 바꿀 수 있게 하고 전적에 신원을 박는다"
```

---

## Task 4: 클라 DTO 맞추기 (와이어 계약)

**레포:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` — **브랜치 `feature/player-identity`**
(이미 만들어져 있고 스펙 커밋이 올라가 있다)

**Files:**
- Modify: `Assets/Scripts/Domain/User.cs`
- Modify: `Assets/Scripts/WebAPI/Dto/UserDto.cs`
- Modify: `Assets/Scripts/WebAPI/Dto/Request/CreateUserRequest.cs`

**Interfaces:**
- Consumes: Task 2·3이 바꾼 응답 필드(`displayName`, `tag`).
- Produces: 없음(마지막 코드 태스크).

**왜 이 태스크가 있나:** 백엔드가 `username` → `displayName`으로 필드 이름을 바꿨다. 클라가 그 이름을
그대로 들고 있으면 **역직렬화가 조용히 null을 넣는다** — 예외도 안 나고 화면에 쓰는 곳도 없어서
증상이 아예 안 뜬다. 이 프로젝트는 이 부류에 한 번 물렸다.

- [ ] **Step 1: 도메인 모델**

`Assets/Scripts/Domain/User.cs`의 `username` 필드를 아래로 바꾼다:

```csharp
        //  표시용 이름. 유일하지 않다 — 신원은 tag가 가른다.
        public string displayName;
        //  Crockford Base32 6자리. 가입 때 서버가 부여하고 바뀌지 않는다.
        public string tag;
```

- [ ] **Step 2: 응답 DTO**

`Assets/Scripts/WebAPI/Dto/UserDto.cs`의 `username` → `displayName`, 그리고 `tag` 추가.
**JSON 필드명과 정확히 같아야 한다** — 다르면 조용히 null이 된다.

- [ ] **Step 3: 요청 DTO**

`Assets/Scripts/WebAPI/Dto/Request/CreateUserRequest.cs`의 `username` → `displayName`.

> 이 요청은 지금 호출처가 없을 수 있다(게스트 가입은 `/auth/anonymous`가 한다). 있는지 확인하고,
> 없으면 필드만 맞춘다 — 지우는 것은 이 태스크 범위가 아니다.

- [ ] **Step 4: 역방향 검증**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
grep -rn "username" Assets/Scripts --include=*.cs
```

기대: **0건**. 출력을 보고서에 그대로 붙인다.

- [ ] **Step 5: 컴파일**

```bash
unity command recompile --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```

그 다음 완료까지 폴링:

```bash
until unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" 2>&1 | grep -qE '"status":"(completed|up_to_date)"'; do sleep 5; done
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```

기대: `failed: false`, `errors: []`. 결과 줄을 보고서에 그대로 붙인다.

> ⛔ **EditMode 테스트를 `run_tests`로 띄우고 백그라운드에서 폴링하지 말 것.** 이 도구는 30초에
> 끊기는데 에디터 작업은 남아, 뒤따르는 모든 명령이 그 뒤에 쌓여 큐가 막힌다(취소 명령까지).
> 재시작 말고는 못 푼다. 필요하면 `unity command run_tests ... -- --mode EditMode`를 **동기로 한 번**
> 부르면 결과까지 한 번에 돌아온다. 이 태스크는 DTO 필드명만 바꾸므로 컴파일이면 충분하다.

- [ ] **Step 6: 커밋**

```bash
git status --short
git add Assets/Scripts/Domain/User.cs Assets/Scripts/WebAPI/Dto/UserDto.cs Assets/Scripts/WebAPI/Dto/Request/CreateUserRequest.cs
git commit -m "refactor(user): 표시 이름과 태그로 와이어 필드를 맞춘다"
```

⚠️ 워킹트리에 **커밋하면 안 되는 로컬 픽스처**가 있다: `Assets/Art`(서브모듈 포인터),
`Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, 그리고 에디터가 만드는 잡음
(`Assets/AddressableAssetsData/*`, `ProjectSettings/PackageManagerSettings.asset`).
**`git add -A` 금지** — 위 세 경로만 지정하고 커밋 전 `git status --short`로 확인한다.

---

## Task 5: 배포 + 끝‑끝 검증 (사람 손 필요)

**Files:** 없음(운영 작업)

> **왜 사람이 필요한가:** 마이그레이션이 실DB에 처음 적용되고, 실제로 가입·플레이해서 확인해야 한다.
> **이 슬라이스는 화면 변화가 없다** — 새로 보이는 것이 아니라 *기존 흐름이 그대로인지*를 본다.

- [ ] **Step 1: 배포 — `app=all`**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git push origin feature/player-identity
gh workflow run backend-deploy.yml --ref feature/player-identity -f app=all -f environment=local
```

> **`app=all`이어야 한다** — 마이그레이션이 있고 `packages/server-core`(응답 코드)와
> `packages/database`가 바뀌어 세 앱 모두 새 이미지가 필요하다.
> **게임서버는 재빌드 불필요**(Unity 서버 코드 무변경).
>
> ⚠️ **배포 중에는 플레이하지 말 것.** `db-migrate`가 ArgoCD `PreSync` 훅이라 새 파드보다 **먼저**
> 돈다. 이번엔 컬럼 개명이 있어 그 창에서 구버전 코드가 `username`을 찾다 실패한다.

- [ ] **Step 2: 마이그레이션 확인**

```bash
kubectl get jobs | grep db-migrate
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c '\d "User"'
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c \
  'SELECT "displayName", "tag" FROM "User" ORDER BY "createdAt" DESC LIMIT 5;'
```

기대: `displayName`·`tag` 컬럼 존재, `username` 없음, `User_tag_key` 유니크 인덱스 존재.
기존 유저가 **각자 다른 6자리 태그**를 갖고 이름은 `플레이어`.

- [ ] **Step 3: 파드 롤아웃 확인**

```bash
kubectl get pods -o custom-columns='NAME:.metadata.name,IMAGE:.spec.containers[0].image,STATUS:.status.phase'
```

기대: 세 앱 모두 새 태그로 Running. **옛 태그면 기다린다.**

- [ ] **Step 4: 개명이 되는지 (클러스터 안에서)**

```bash
KEY=$(kubectl get secret internal-api-secret -o jsonpath='{.data.INTERNAL_API_KEY}' | base64 -d)
POD=$(kubectl get pods -l app=lobby-server --field-selector=status.phase=Running -o jsonpath='{.items[0].metadata.name}')
kubectl exec $POD -- curl -s -X PUT -H "x-internal-api-key: $KEY" -H "Content-Type: application/json" \
  -d '{"displayName":"철수"}' "http://localhost:80/user/<userId>/display-name"
```

기대: `{"code":200,"user":{...,"displayName":"철수","tag":"..."}}`.

**같은 이름을 다른 유저에게도** 걸어본다 → **둘 다 200이어야 한다.** 하나라도 거절되면 어딘가에
유일성 검사가 들어간 것이다(이 설계의 핵심이 깨진 것).

형식 위반도 확인: `{"displayName":"가"}` → `code: 30001`.

- [ ] **Step 5: 회귀 — 한 판**

`local-k8s`로 클라 2대. **전부 이전과 같아야 한다**: 로그인 → 로비 → 매칭 → 방 진입 → 플레이 →
결과 화면 → 프로필.

그리고 전적에 **신원이 박혔는지** 확인한다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c \
  "SELECT jsonb_pretty(\"result\") FROM \"Match\" WHERE state='Finished' ORDER BY \"endedAt\" DESC LIMIT 1;"
```

기대: `displayName`이 `철수#K7QM2X` 형태(이름만이 아니라 태그까지).

- [ ] **Step 6: 그때 이름이 안 바뀌는지**

Step 4로 이름을 한 번 더 바꾼 뒤, 위 판의 `result`를 다시 읽는다.
기대: **안 바뀐다.** 바뀌면 어딘가가 조회 시점에 계정을 다시 읽고 있는 것이다.

- [ ] **Step 7: 머지**

두 레포 모두 `CLAUDE.md`의 "푸시 규약"대로. **한 줄씩 결과를 확인하고 넘어간다.**
클라는 리베이스 전에 로컬 픽스처를 `git stash push -u`로 빼고 끝나면 `pop`한다.

---

## 검증 요약 (전체가 끝났다는 기준)

1. 빌드 6/6(`Cached: 0 cached`), 백엔드 네 스위트 통과, 클라 컴파일 클린
2. 역방향 grep 클린 — 두 레포에 `username`을 부르는 곳이 없다
3. 마이그레이션 후 기존 유저가 **각자 다른** 6자리 태그를 갖는다
4. **같은 이름을 여러 유저가 쓸 수 있다** — 이 설계의 존재 이유
5. 전적의 `displayName`이 `이름#태그` 형태이고, 개명해도 안 바뀐다
6. 실플레이 회귀 0
