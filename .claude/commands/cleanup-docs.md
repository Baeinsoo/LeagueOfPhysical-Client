---
description: 문서 정기 점검 — md 인벤토리와 정리 제안 (실행은 확인 후)
---

`docs/README.md`의 규칙을 기준으로 문서를 점검한다. **이 단계에서는 파일을 바꾸지 않는다.**

## 점검 항목

1. **ROADMAP 길이**: `wc -l docs/ROADMAP.md`가 300줄을 넘으면, 끝난 항목이나 세션 서술이 섞였는지 본다.
2. **닫히지 않은 spec/plan**: `docs/superpowers/specs|plans/`의 각 파일에 대해 작업이 머지됐는지 확인한다.
   근거는 8개 레포의 `git log origin/main --merges`에서 브랜치 이름과 파일 슬러그가 맞는지, 그리고 ROADMAP이다.
   머지됐으면 닫기 체크리스트(`docs/README.md`)가 빠진 것이다.
3. **오래된 문서**: 추적 중인 md를 마지막 커밋 날짜순으로 나열한다. `docs/archive/`, `docs/journal/`은 뺀다.
   ```bash
   git ls-files '*.md' ':!:Assets/**' ':!:docs/archive/**' ':!:docs/journal/**' \
     | while read f; do echo "$(git log -1 --format=%cs -- "$f") $f"; done | sort
   ```
   90일 넘게 안 바뀐 아키텍처 문서는 지금 코드와 맞는지 표본으로 확인한다.
4. **끊긴 링크**: `docs/`, `CLAUDE.md`의 상대 링크와 `docs/...` 경로 언급이 실제 파일을 가리키는지 확인한다.
5. **엉뚱한 곳의 md**: `docs/` 밖에 새로 생긴 md(요약·보고서·TODO류)와 `Assets/` 안의 md를 찾는다. 서드파티는 뺀다.
6. **메모리 누수**: 이 프로젝트의 메모리(`~/.claude/projects/*LeagueOfPhysical-Client*/memory/`)에서
   설계 결정으로 보이는 항목을 찾는다. 두 머신이 공유해야 하므로 ADR 후보다.

## 출력

표 하나로 보고한다: 경로 | 요약 | 마지막 수정일 | 문제 | 추천 조치(유지/갱신/병합/아카이브/삭제/ADR로).
사용자가 표를 확인하고 승인한 항목만 실행한다.
