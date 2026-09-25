# 0009. 환경 이름은 역할(tier)로 짓고, 소문자 kebab-case로 쓴다

- 상태: Accepted
- 날짜: 2026-07-26
- 관련: Client `0a9d0f98` (chore(env): dev 환경을 iwinv로 갱신 + 환경 이름 lowercase 통일) · [2026-08-10 플레이어 빌드 환경 선택 spec](../archive/specs/2026-08-10-player-build-environment-selection-design.md) · [0010](0010-dev-env-iwinv-k3s-per-cluster-argocd.md)

## 맥락

원격 개발 서버를 iwinv에 새로 올리면서, 클라이언트의 `Dev` 환경은 이미 죽은 AWS 주소를 가리키고
있었다. 새 환경 이름을 `iwinv`로 지을지, 기존 `Dev`를 갱신할지 정해야 했다.

- 호스팅 업체 이름을 환경 이름에 쓰면, 다른 곳으로 옮기는 순간 이름이 거짓이 된다. 이전(Hetzner,
  GKE, EKS 등)은 이미 열린 과제였다.
- 환경 이름은 DNS 호스트명, 환경변수 값, k8s 네임스페이스(대문자 불가), 파일명, CI 입력값에 그대로
  들어간다. 백엔드·k8s·Docker 쪽은 이미 전부 소문자 kebab이었고, Unity 클라 환경(`Local`/`Dev`)만 예외였다.

## 결정

- 환경 이름은 **역할(tier)** 로 짓는다. 업체 이름(`iwinv`, `aws`)은 쓰지 않는다.
- 표기는 **소문자 kebab-case**로 통일한다.
- 지금 있는 환경은 셋이다.
  - `local`: 백엔드와 게임서버를 손으로 띄운다.
  - `local-k8s`: 백엔드를 로컬 k8s에 띄우고, 게임서버는 k8s가 매치마다 띄운다.
  - `dev`: 원격 공유 개발 환경(현재 iwinv).
- `staging`, `prod`는 **필요해질 때 만든다.** 실제 유저를 받을 때 `prod`, 그 사이 검증 단계가 필요할 때
  `staging`. 미리 만들지 않는다.
- 새 원격 환경이 필요해 보이면 먼저 기존 tier에 맞는 것이 있는지 보고 **갱신을 우선한다.**
- `NODE_ENV`와 배포 tier는 **다른 축**이다. `NODE_ENV`는 Node 관례라 `development`/`production` 둘뿐이고
  tier와 1:1이 아니다. 서버는 tier에 따른 차이를 `SPECIFIC_ENV`로 따로 받는다.

## 결과

- 호스팅을 옮겨도 이름은 그대로다. iwinv를 떠나도 `dev`는 `dev`다.
- 이름을 그대로 호스트명·네임스페이스·파일명에 써도 규칙 위반이 없다.
- `QA`는 전담 검증 조직·단계가 있을 때 쓰는 이름이라 쓰지 않았다. `인하우스`는 tier가 아니라 빌드 배포
  채널 개념이라 축이 다르다.
- 버린 대안
  - **업체 이름으로 환경 만들기(`iwinv`)**: 이전하면 거짓이 되고 레거시 이름이 남는다.
  - **`Dev` 옆에 새 환경을 추가**: 죽은 환경이 목록에 남는다.
  - **staging/prod 미리 만들기**: 쓰지 않는 설정이 낡아 간다.
- **후속(미완)**: 서버의 `SPECIFIC_ENV=local-k8s`가 원격 dev 클러스터에서도 그대로 쓰이고 있어 이름이
  사실과 다르다. 그 설정 파일의 내용은 클러스터 내부 DNS(`postgres-service` 등)라 어느 k8s에서나 맞으므로,
  **`k8s`로 이름만 바꾸면** 정확해진다. 범위는 lop-backend의 `apps/*/.env.development.local-k8s` 파일명 3개와
  각 Dockerfile의 `ENV SPECIFIC_ENV`, infrastructure의 서버 ConfigMap 3개다. 2026-09-25 기준 아직 바뀌지 않았다.
- 참고: infrastructure의 클러스터 overlay 이름 `envs/local`은 로컬 k8s 클러스터를 뜻하며, 클라이언트 쪽
  tier로는 `local-k8s`에 해당한다.
