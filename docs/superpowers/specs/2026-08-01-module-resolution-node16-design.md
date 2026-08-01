# `moduleResolution` node10 → node16 — 설계

백엔드 모노레포(`lop-backend`)의 TypeScript 모듈 해석을 `node`(=node10)에서 `node16`으로 올린다.

## 1. 왜

두 가지가 겹쳐 있다.

**① 하고 싶은 것을 막고 있다.** `@lop/server-core`의 배럴이 부수효과를 갖는 문제를 정석으로 풀려면
`package.json`의 `exports` 맵으로 서브패스를 제공해야 하는데, **TypeScript는 `moduleResolution: node10`에서
`exports` 필드를 아예 읽지 않는다.** Node 런타임은 읽으므로, 그대로 넣으면 타입 해석과 실행 해석이
갈라지는 더 나쁜 상태가 된다.

**② 어차피 해야 한다.** **node10은 deprecated이고 TypeScript 7.0에서 동작을 멈춘다.**

## 2. 무엇으로 가나 — `node16`

| 후보 | 판단 |
|---|---|
| **`node16`** ✅ | Node에서 도는 CJS 백엔드에 맞는 모드. `exports`/`imports`를 읽는다 |
| `nodenext` | node16과 같은 계열이나 TS 버전에 따라 의미가 움직인다. 지금은 고정값이 낫다 |
| `bundler` | **번들러용**이다. 우리는 번들러가 없다 — 오답 |

`module`도 함께 `node16`으로 간다(TS가 `moduleResolution: node16`을 `module: node16`과만 허용한다).

**산출물은 CJS 그대로다.** 어느 `package.json`에도 `"type": "module"`이 없어서 `.ts`는 CJS로 emit된다.
실측으로 확인했다(아래 §4).

## 3. 무엇이 걸리나 — `dotenv@10.0.0` 하나

실제로 바꿔서 재 본 결과 **막는 것은 하나뿐**이다:

```
src/config/index.ts(1,24): error TS7016: Could not find a declaration file for module 'dotenv'
```

`dotenv@10.0.0`(2021)은 node16 타입 해석 규약에 맞지 않는다. 세 앱이 모두 쓴다.

**`^16.5.0`으로 올린다.** 근거:
- **`packages/database`가 이미 `dotenv@16.5.0`을 쓴다** — 올리는 쪽이 저장소 안 버전을 통일하는 방향이다
- 우리가 쓰는 API는 `config({ path })` 하나뿐이고 10→16에서 그 시그니처는 바뀌지 않았다

## 4. 착수 전 실측 (전부 실제로 돌려 확인)

| 확인 | 결과 |
|---|---|
| 타입체크 5개 프로젝트 | 에러 **0** |
| 캐시 없는 실제 빌드 | `dotenv`만 실패 → 올린 뒤 **5/5 성공** |
| 테스트 | matchmaking 154 + room 11 = **165 통과** |
| 산출물 형식 | **CJS 유지**(`"use strict"` + `require(` 확인) |
| 런타임 | `dist/config` 로드 → env 값 채워짐 확인 |
| **`exports` 서브패스가 실제로 해석되나** | ✅ `@lop/server-core/config` 임시 맵으로 **확인** |

마지막 줄이 이 업그레이드의 **전제**다 — 목적을 실제로 풀어 주지 않으면 할 이유가 없다. 확인했다.

## 5. 범위

**이 슬라이스는 업그레이드만 한다. 동작 변화 0.**

- `tsconfig.base.json`: `module`/`moduleResolution` → `node16`
- 세 앱: `dotenv` `^10.0.0` → `^16.5.0`
- lockfile 갱신

**`exports` 맵과 배럴 분해는 다음 슬라이스다.** 둘을 한 번에 하면 나중에 뭔가 이상할 때 원인이
업그레이드인지 배럴 분해인지 가릴 수 없다 — 이 트랙에서 그 분리가 여러 번 값을 했다.

배럴을 **어디까지** 쪼갤지는 지금 정하지 않는다. 실제 `exports` 맵을 그려 보면서 정한다.

## 6. 검증

- 빌드 5/5(캐시 없이), 테스트 165
- **산출물이 CJS인지** 확인 — 이게 바뀌면 런타임 로딩이 통째로 달라진다
- **로컬 docker 이미지 3종** — 로컬 `pnpm build`는 워크스페이스 hoisting이 문제를 가린다
- 배포 후 4파드 기동 + 에러 0 + 2클라 E2E (동작 변화 0 확인이 목적)

## 7. 범위 밖

- `exports` 맵 / 배럴 분해 (다음 슬라이스)
- `verbatimModuleSyntax` 등 다른 엄격 옵션 — 이번에 곁들이지 않는다
- Unity 클라이언트·게임 서버 (무관)
