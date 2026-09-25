# 0010. dev 환경은 iwinv 단일 노드 k3s이고, 클러스터마다 자체 ArgoCD를 둔다

- 상태: Accepted
- 날짜: 2026-07-25 (구축) · 2026-08-10 (GitOps 전환)
- 관련: `infrastructure/docs/specs/2026-08-10-dev-env-gitops-design.md` · [0003](0003-gitops-argocd-sha-tags.md) · [0009](0009-environment-naming.md)

## 맥락

폰·다른 PC의 클라이언트가 붙을 수 있는 **원격 공유 개발 환경**이 필요했다. 2026-07-25 iwinv 클라우드
서버 한 대(2코어/8GB)에 k3s(가벼운 k8s 배포판)를 깔고 백엔드 전체를 올렸다.

처음에는 로컬 매니페스트를 서버로 복사하고, 공인 IP만 서버에서 고쳐 넣은 뒤 손으로 적용했다.
그 결과 iwinv 전용 값은 git에 없었고, 이미지는 `:latest`였으며, CI가 태그를 올려도 iwinv에는
아무것도 내려가지 않았다. 자동화가 로컬 클러스터에만 있었다.

매니페스트는 환경 구분 없이 한 벌이었고 로컬에 맞춰져 있어서, iwinv의 ArgoCD가 같은 경로를 보면
클라에게 `127.0.0.1`을 알려주게 된다. 그래서 필요한 것은 "ArgoCD 설치"보다 **환경 분리**였다.

## 결정

- 공유 dev 환경은 **iwinv 단일 노드 k3s**다.
- 매니페스트는 Kustomize **`k8s/base/`** + **`k8s/envs/{local,dev}/`** overlay로 나눈다. 환경별 차이는
  게임서버 공인 IP와 이미지 태그뿐이라 그 둘만 `envs/`에 둔다. platform(DB·Redis·Ingress)은 overlay 없이
  두 환경이 base를 그대로 쓴다.
- **클러스터마다 자기 ArgoCD**를 둔다. 각 ArgoCD는 같은 public 레포에서 자기 환경 경로만 본다.
  Application 이름(`root`/`platform`/`backend`)은 두 환경에서 같게 유지한다.
- 배포 버튼에 `environment` 입력(`local`/`dev`/`both`)을 두고 **기본값은 `local`** 로 한다.
- **ArgoCD UI는 인터넷에 노출하지 않는다.** SSH 터널로만 연다.
- 시크릿은 iwinv에서 한 번 손으로 만든다(레포가 public이라 커밋할 수 없다).

## 결과

- 맥이 꺼져 있어도 iwinv는 main을 따라 스스로 동기화한다.
- iwinv 전용 값이 git에 커밋돼 이력이 남고, `kustomize build`로 재현된다.
- 기본값이 `local`이라 버튼을 그냥 누르면 dev는 그대로다. dev에 반영하려면 `dev`나 `both`를 골라야 한다.
- 2코어/8GB에 ArgoCD 파드 여러 개와 게임서버 파드가 함께 떠 자원이 빠듯하다. 로그인에 쓰지 않는
  ArgoCD 부품은 줄일 수 있다.
- 단일 노드라 HA(고가용성)는 없다. dev라서 허용한다.
- 버린 대안
  - **허브 ArgoCD 하나가 두 클러스터를 관리(허브-스포크)**: 허브가 개인 맥이라, 맥이 꺼지면 dev가 멈춘다.
  - **수동 복사·적용 유지**: git과 실제 상태가 어긋나고 CI가 dev에 닿지 않는다.
  - **낮은 환경 자동 배포 + 승격(Kargo 등)**: 환경 둘, 1인 개발에는 과하다.
  - **ArgoCD 웹훅**: 공개 엔드포인트가 필요하다. 폴링(약 3분)으로 충분하다.
  - **ArgoCD UI를 공인 IP로 노출**: 인터넷에 관리자 로그인 화면을 띄우지 않는다.
  - **SealedSecrets 등 시크릿 암호화 도구**: 운영 환경 단계의 과제로 미뤘다.
- 다시 볼 조건: 환경이 셋 이상이 되거나, 실제 유저를 받는 prod가 생기거나, 호스팅을 옮길 때.
