# 0003. 배포는 GitOps로 한다: infrastructure 레포가 진실이고, ArgoCD가 자동으로 맞추며, 이미지 태그는 git SHA다

- 상태: Accepted
- 날짜: 2026-07-05 (2026-08-10 ConfigMap 재시작 보강)
- 관련: `infrastructure/docs/specs/2026-07-05-deployment-system-design.md` · `infrastructure/docs/specs/2026-08-10-dev-env-gitops-design.md` §4 · [0002](0002-backend-monorepo-single-prisma-owner.md) · [0004](0004-ci-runner-placement.md) · [0010](0010-dev-env-iwinv-k3s-per-cluster-argocd.md)

## 맥락

배포 전에는 서버 레포마다 k8s 매니페스트(배포 설정 파일)가 흩어져 있었고, 이미지는 `:latest`에
손으로 올렸다. `:latest`는 올릴 때마다 내용이 바뀌므로 "지금 클러스터에 무엇이 떠 있나"를 git으로
알 수 없고, 되돌리려 해도 이전 내용이 레지스트리에 남아 있다는 보장이 없다.

사용자가 원한 것은 "젠킨스처럼 버튼을 누르면 돌아가는" 배포와, 배포 상태를 한눈에 보는 화면이었다.

## 결정

- 모든 k8s 매니페스트는 **infrastructure 레포**에 Kustomize로 모은다. 이 레포의 `main`이 곧
  "클러스터가 가져야 할 상태"다(GitOps: git을 유일한 진실원본으로 삼는 운영 방식).
- 클러스터에 설치한 **ArgoCD**가 이 레포를 지켜보며 자동으로 맞춘다. app-of-apps(루트 앱이 하위
  앱들을 만드는 구조)로 `root`/`platform`/`backend`를 두고, 모두 `automated` + `prune` + `selfHeal`이다.
- 빌드는 GitHub Actions의 수동 버튼(`workflow_dispatch`)이다. CI는 이미지를 **`:<git 커밋 SHA>`** 로
  굽고, infrastructure 레포의 태그 값을 바꾸는 커밋을 직접 push한다. 그러면 ArgoCD가 그 커밋을 받아
  배포한다. `:latest`는 쓰지 않는다.
- **롤백은 infrastructure 커밋을 revert한다.** 사람이 `kubectl apply`나 `kubectl rollout restart`로
  클러스터를 직접 바꾸지 않는다. selfHeal이 곧바로 git 상태로 되돌려 놓기 때문이다.
- DB 마이그레이션은 ArgoCD PreSync 훅(동기화 직전에 먼저 도는 Job)으로 서버보다 먼저 돌린다.
- **게임서버 설정 ConfigMap은 kustomize `configMapGenerator`로 만든다**(2026-08-10). 이름에 내용
  해시가 붙으므로 값(게임서버 이미지 태그 등)이 바뀌면 이름이 바뀌고, 그걸 참조하는 room-server가
  스스로 롤링 재시작한다.

## 결과

- 무엇이 언제 배포됐는지가 infrastructure 커밋 이력에 그대로 남는다. 되돌리기도 커밋 하나다.
- 빌드 상태·로그는 GitHub Actions 화면, 배포 상태·차이는 ArgoCD 화면으로 역할이 나뉜다.
- 클러스터에서 매니페스트를 "잠깐 시험"하는 일은 할 수 없다(수 초 안에 되돌려진다). 시험하려면
  브랜치가 아니라 main에 커밋해야 한다.
- configMapGenerator 이전에는 ConfigMap만 바뀌면 room-server가 재시작되지 않아 옛 이미지로 매치를
  띄웠다. 그래서 "게임서버 배포를 먼저, 백엔드 배포를 나중에" 돌리는 관행이 있었는데, 이 보강으로
  **그 순서는 더 이상 필요 없다.** 해시가 붙어 옛 ConfigMap이 남는 것은 ArgoCD의 prune이 치운다.
- **게임서버 이미지 태그는 서버 레포(LeagueOfPhysical-Server)의 커밋 SHA다.** CI가 함께 받아 굽는
  형제 패키지(GameFramework, Shared, MasterData)만 바꾸고 서버 레포는 그대로면 **태그가 움직이지 않는다.**
  그러면 같은 태그에 새 내용이 덮여 올라가고, 매니페스트에는 변화가 없어 ArgoCD도 아무것도 하지 않으며,
  노드는 이미 받아 둔 옛 이미지를 계속 쓴다. 패키지만 바꿨을 때는 서버 레포에도 커밋을 만들어 태그를
  움직여야 한다.
- 버린 대안
  - **CI가 `kubectl apply`로 직접 배포(push 방식)**: 클러스터 접속 권한을 CI에 줘야 하고, 클러스터의
    실제 상태가 git과 어긋나도 알 수 없다.
  - **`:latest` 유지 + 재시작으로 갱신**: 재현성과 롤백을 레지스트리에 맡기게 된다.
  - **ArgoCD에 수동 승인 단계 두기**: 사람이 누르는 단계는 이미 GitHub 버튼에 있어 두 번 누를 이유가 없다.
- 다시 볼 조건: 환경이 셋 이상이 되거나 릴리스 승인 절차가 필요해지면, 환경을 고르는 버튼 대신
  "낮은 환경 자동 배포 + 위로 승격" 방식을 검토한다.
