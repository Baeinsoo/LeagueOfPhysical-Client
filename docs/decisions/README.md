# 설계 결정 기록 (ADR)

"왜 이렇게 했나"를 남기는 곳이다. 형식은 [Michael Nygard의 ADR](https://adr.github.io/)을 따른다.

## 규칙

- 결정 하나에 파일 하나: `NNNN-짧은-슬러그.md` (번호는 4자리, 순서대로)
- **Accepted가 된 ADR은 고치지 않는다.** 결정이 바뀌면 새 ADR을 쓰고, 옛 ADR의 상태를 `Superseded by NNNN`으로만 바꾼다.
- 코드를 보면 알 수 있는 내용은 쓰지 않는다. **코드에 드러나지 않는 이유와 버린 대안**을 쓴다.
- 1~2쪽 이내로 쓴다. 긴 조사 과정은 spec에 두고 링크만 건다.

## 템플릿

```markdown
# NNNN. 제목 (무엇을 정했나를 한 문장으로)

- 상태: Accepted | Superseded by NNNN
- 날짜: YYYY-MM-DD
- 관련: spec·커밋·다른 ADR 링크

## 맥락
무엇이 문제였고, 어떤 힘(제약·요구)이 부딪쳤나.

## 결정
무엇을 하기로 했나.

## 결과
그래서 무엇이 쉬워지고 무엇이 어려워지나. 버린 대안과 버린 이유.
다시 검토할 조건이 있으면 적는다.
```

## 목록

| # | 제목 | 상태 |
|---|---|---|
| [0001](0001-server-has-no-art-submodule.md) | 서버 프로젝트는 Art 서브모듈을 갖지 않는다. 맵은 어드레서블 콘텐츠로 받는다 | Accepted |
| [0002](0002-backend-monorepo-single-prisma-owner.md) | 백엔드는 lop-backend 모노레포 하나로 합치고, Prisma 스키마는 `@lop/database` 하나만 가진다 | Accepted |
| [0003](0003-gitops-argocd-sha-tags.md) | 배포는 GitOps로 한다: infrastructure 레포가 진실이고, ArgoCD가 자동으로 맞추며, 이미지 태그는 git SHA다 | Accepted |
| [0004](0004-ci-runner-placement.md) | CI 러너 배치: 백엔드는 GitHub 호스티드, Unity 빌드는 개발 맥의 셀프호스트 러너(서버용·클라용 분리) | Accepted |
| [0005](0005-addressables-independent-append-only.md) | 어드레서블 콘텐츠는 앱과 따로 배포하고, 설치된 앱이 쓰는 콘텐츠는 S3에 추가만 한다 | Accepted |
| [0006](0006-gameserver-il2cpp-multiarch.md) | 게임서버는 IL2CPP로 빌드하고, amd64·arm64 멀티아치 이미지로 배포한다 | Accepted |
| [0007](0007-nuget-restore-in-ci.md) | NuGet DLL은 커밋하지 않고, CI에서 NuGetForUnity CLI로 복원한다 | Accepted |
| [0008](0008-art-direction.md) | LOP 아트 방향: 치비 인간, 폴가이즈 파티 톤, 주성치 병맛 한 스푼 | Accepted |
| [0009](0009-environment-naming.md) | 환경 이름은 역할(tier)로 짓고, 소문자 kebab-case로 쓴다 | Accepted |
| [0010](0010-dev-env-iwinv-k3s-per-cluster-argocd.md) | dev 환경은 iwinv 단일 노드 k3s이고, 클러스터마다 자체 ArgoCD를 둔다 | Accepted |
| [0011](0011-backend-authorization-model.md) | 백엔드 인가 모델: 주체별 인가, `/internal`은 키 전용, 볼 수 없는 것은 "없음"으로 답한다 | Accepted |
| [0012](0012-unity-repos-no-worktrees.md) | Unity 레포에서는 워크트리 대신 그 자리에서 브랜치를 바꾼다. 플랫폼용 사본만 별도 클론으로 둔다 | Accepted |
| [0013](0013-flappy-race-naming.md) | 플래피 미니게임의 공개 이름은 "Flappy Race"로 하고, 스토어 메타데이터에는 Flappy를 쓰지 않는다 | Accepted |
| [0014](0014-ordered-message-broker-only.md) | 메시지 브로커는 구독 순서를 지키는 `OrderedMessageBroker`만 쓴다 | Accepted |
| [0015](0015-wire-duration-as-end-tick.md) | 지속시간은 와이어에 "끝나는 절대 틱"으로 싣고, 변환은 "끝났다"를 아는 한 곳에서만 한다 | Accepted |
| [0016](0016-flappy-map-proof-by-search.md) | Flappy 맵이 통과 가능한지는 전수 탐색이 증명하고, 봇 고도화는 보류한다 | Accepted |
