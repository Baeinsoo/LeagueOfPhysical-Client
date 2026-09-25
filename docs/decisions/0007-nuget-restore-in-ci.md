# 0007. NuGet DLL은 커밋하지 않고, CI에서 NuGetForUnity CLI로 복원한다

- 상태: Accepted
- 날짜: 2026-07-12
- 관련: Client `bb54d43c`, Server `617a6c5` (refactor(ci): NuGet 패키지를 커밋 대신 CI 복원으로 전환) · [0004](0004-ci-runner-placement.md)

## 맥락

Client·Server Unity 프로젝트는 R3, AutoMapper 같은 .NET 라이브러리를 NuGetForUnity(Unity에서 NuGet
패키지를 쓰게 해 주는 플러그인)로 받는다. 받은 DLL이 들어가는 `Assets/NuGetForUnity/Packages/`는
`.gitignore`에 들어 있어서, CI가 새로 체크아웃하면 DLL이 없어 컴파일이 깨졌다.

처음 CI를 세울 때(Phase 3·4)는 급한 대로 그 DLL들을 강제로 커밋해서 넘겼다. 하지만 사용자는
**매니페스트(`packages.config`, 어떤 패키지를 어떤 버전으로 쓰는지 적은 목록)만 관리하고 바이너리는
레포에 넣지 않기**를 원했다.

## 결정

- `Assets/NuGetForUnity/Packages/`는 다시 gitignore 규칙대로 둔다(커밋했던 DLL은 추적 해제).
- 두 레포 모두 `.config/dotnet-tools.json`에 **NuGetForUnity CLI 4.5.0**을 버전 고정한다.
- CI 워크플로는 Unity 빌드 전에 `nugetforunity restore`로 `packages.config`에 적힌 패키지를 복원한다.
- 로컬 개발자도 새 클론에서는 같은 명령으로 복원한다.

## 결과

- 레포에는 목록만 남는다. 패키지 버전을 바꾸는 변경은 `packages.config` 한 줄 diff로 보인다.
- `packages.config`가 버전을 고정하므로 내용은 재현된다. 남는 위험은 nuget.org가 내려가 있는 경우뿐이다.
- 걱정했던 문제(batchmode에서 DLL의 `.meta` 설정이 어긋나는 것)는 생기지 않았다. NuGetForUnity가
  임포트할 때 설정을 복구한다.
- 새 클론·새 사본을 만들 때 복원 단계를 빼먹으면 컴파일이 깨진다(DLL이 따라오지 않는다).
- 버린 대안
  - **DLL 커밋 유지**: 동작은 하지만 바이너리가 레포에 쌓이고, 사용자 선호와 맞지 않는다.
  - **UnityNuGet 같은 UPM 레지스트리로 옮기기**: 패키지 관리 방식을 통째로 바꾸는 일이라 이번 범위를 넘는다.
- 다시 볼 조건: NuGetForUnity를 버리고 다른 패키지 경로로 옮길 때.
