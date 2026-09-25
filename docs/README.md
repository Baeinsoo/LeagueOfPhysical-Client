# LOP 문서 지도

LOP는 8개 레포로 나뉘어 있지만, **프로젝트 전체에 걸친 문서는 이 레포(Client)의 `docs/`에 모은다.**
다른 레포의 README는 자기 레포 사용법만 다루고 이곳을 가리킨다.

## 무엇이 어디 사는가

| 알고 싶은 것 | 보는 곳 | 수명 |
|---|---|---|
| 지금 어디까지 왔나, 다음은 뭔가 | [`ROADMAP.md`](ROADMAP.md) | 늘 최신. **300줄 이하** |
| 구조·설계 계약·컨벤션 | 아키텍처 문서 5개 (아래) | 오래 간다. 구조가 바뀌면 고친다 |
| 왜 이렇게 정했나 | [`decisions/`](decisions/) (ADR) | **확정 후 고치지 않는다.** 바뀌면 새 ADR로 대체 |
| 지금 짓는 것의 설계·구현 계획 | `superpowers/specs/`, `superpowers/plans/` | 작업 중에만 |
| 끝난 슬라이스의 설계 | `archive/specs/` | 참고용. 요청 없이는 안 읽는다 |
| 슬라이스에서 한 일·배운 것 | **머지 커밋 본문** (`git log --merges`) | git이 보관 |
| 2026-09-25 이전 세션 기록 | `journal/YYYY-MM.md` | 동결. 갱신하지 않는다 |

git 밖 메모리(`~/.claude/.../memory`)에는 **개인 작업 습관·도구 gotcha**만 둔다.
메모리는 머신마다 따로라서, 두 대의 맥이 함께 알아야 하는 결정은 반드시 이 레포에 둔다.

## 아키텍처 문서 (CLAUDE.md가 매 세션 자동 로드)

- [`architecture-guidelines.md`](architecture-guidelines.md) — 레이어·의존 방향·주석 컨벤션
- [`entity-system-design.md`](entity-system-design.md) — 엔티티 시스템
- [`lop-repo-topology.md`](lop-repo-topology.md) — 8개 레포의 관계
- [`world-core-connection-architecture.md`](world-core-connection-architecture.md) — World Core 연결 구조
- [`netcode-redesign.md`](netcode-redesign.md) — 넷코드 모델

## 슬라이스 수명주기

```
brainstorm ─▶ spec (맨 앞에 "의도" 절) ─▶ plan ─▶ 구현 ─▶ 머지 ─▶ 닫기
                superpowers/specs/      superpowers/plans/
```

**spec의 "의도" 절** — 설계를 시작하기 전에 네 가지를 적는다.
문제(지금 무엇이 불편한가) / 원하는 결과 / 제약 / 열린 질문.

**닫기 체크리스트** (머지 직후, 같은 브랜치에서):

1. **머지 커밋 본문**에 한 일·배운 것을 적는다 (예전 ROADMAP 세션 서술이 하던 역할).
2. `ROADMAP.md`의 해당 줄을 갱신한다. 끝난 건 지우고, 새로 생긴 할 일·파킹을 더한다.
3. spec에 **앞으로도 지켜야 할 결정**이 있으면 ADR을 쓰거나 아키텍처 문서에 반영한다.
4. spec은 `archive/specs/`로 `git mv`, plan은 `git rm` (git history가 보관한다).
5. CLAUDE.md에 이 슬라이스용 `@` 줄을 넣었다면 뺀다.

## 정기 점검

한 달에 한 번쯤 `/cleanup-docs`를 돌린다. 오래된 문서와 끊긴 링크, ROADMAP 길이, 닫히지 않은 spec을 표로 보여 준다.
표를 확인한 뒤에만 정리를 실행한다.
