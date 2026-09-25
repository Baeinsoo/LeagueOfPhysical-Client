# 0006. 게임서버는 IL2CPP로 빌드하고, amd64·arm64 멀티아치 이미지로 배포한다

- 상태: Accepted
- 날짜: 2026-07-12
- 관련: `infrastructure/docs/specs/2026-07-12-gameserver-il2cpp-multiarch-publicip-design.md` · [0003](0003-gitops-argocd-sha-tags.md) · [0004](0004-ci-runner-placement.md)

## 맥락

게임서버(Unity 데디케이티드 서버)는 처음에 Mono 백엔드, amd64 한 가지로만 구웠다. IL2CPP(C#을 C++로
바꿔 네이티브로 컴파일하는 방식)는 Linux 크로스 컴파일 도구(sysroot)를 못 찾아 실패했기 때문이다.

그런데 로컬 개발 클러스터는 맥 위에서 도는 **arm64** 노드였다. amd64 이미지는 거기서 에뮬레이션 없이
뜨지 않으므로, 게임서버 파드를 실제로 띄워 보는 검증을 할 수 없었다. 원격 서버는 amd64일 가능성이
높아 두 아키텍처가 다 필요했다.

Unity 6에는 **arm64 Linux 서버용 Mono가 없고 IL2CPP만 있다.** 그 사이 Unity의 Linux SDK 패키지로
IL2CPP 빌드가 되는 것이 확인됐다.

## 결정

- 게임서버 스크립팅 백엔드를 **IL2CPP**로 바꾼다. 필요한 Linux SDK·툴체인 패키지는 서버 프로젝트
  manifest에 커밋한다.
- Unity는 한 번에 한 아키텍처만 구우므로 **아키텍처별로 두 번 빌드**하고, 각각 이미지를 올린 뒤
  하나의 태그(`re5nardo/game-server:<sha>`)로 묶은 **멀티아치 매니페스트**를 만든다. 노드는 자기
  아키텍처에 맞는 이미지를 알아서 받는다.
- 매니페스트는 `docker buildx imagetools create`로 합친다.
- 런타임 베이스 이미지는 **Ubuntu 22.04**로 올린다.

## 결과

- 로컬 arm64 노드와 원격 amd64 노드에서 같은 태그로 게임서버가 뜬다(로컬 arm64 파드 기동, iwinv amd64
  기동 모두 실측).
- IL2CPP는 Mono보다 실행 성능이 낫고 서버 배포의 일반적인 선택이다.
- 대가: Unity 빌드가 두 번이라 CI 시간이 길어지고, IL2CPP 컴파일 자체도 Mono보다 느리다.
  IL2CPP 도구 준비가 첫 실행에 오래 걸려 CI는 Unity `Library/`를 지우지 않고 보존한다.
- Unity 6 IL2CPP 바이너리가 GLIBC 2.34 이상을 요구해서 Ubuntu 20.04(2.31)에서는 파드가 시작하자마자
  죽는다. 22.04로 올린 이유가 이것이다.
- 버린 대안
  - **Mono 유지 + amd64만**: 로컬 arm64 클러스터에서 게임서버를 못 띄운다.
  - **arm64도 Mono로**: Unity가 지원하지 않는다.
  - **QEMU 에뮬레이션으로 amd64 이미지를 arm64에서 실행**: 느리고 게임서버 틱 타이밍을 믿을 수 없다.
  - **`docker manifest create --amend`로 합치기**(spec 초안): 아키텍처별 이미지가 이미 OCI 인덱스로
    올라가면 실패한다. `buildx imagetools`가 이 경우를 견고하게 처리한다.
- 다시 볼 조건: 운영 노드 아키텍처가 하나로 정해지고 로컬도 그와 같아지면, 한쪽 빌드를 빼서 CI 시간을 줄일 수 있다.
