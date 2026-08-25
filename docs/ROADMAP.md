# LOP 로드맵 — 한 일 / 할 일 / 파킹

이 문서는 **"지금 어디까지 왔고, 다음이 뭔지"의 단일 원천**이다. 슬라이스/워크스트림 단위 상태만 여기 둔다.

> 왜 이 파일이 필요했나: 상태가 메모리 status 파일(~11개) + 아키텍처 문서의 "상태/backlog" 절 + 58개 spec + 74개 plan에 **4겹으로 흩어져** 있어서, "다음에 뭐 하지?"를 한 곳에서 볼 수 없었다. (실제로 메모리에 "파킹"으로 적힌 항목이 이미 머지돼 있는 stale도 발생했다.)

## 경계 규칙 — 뭐가 어디 사는가 (필독)

| 종류 | 사는 곳 | 예 |
|---|---|---|
| **상태** (한 일/다음/파킹) | **이 파일** | "commit gate = 다음", "kinematic 이행 = 완료" |
| **왜 / gotcha / 결정 / 제약** (코드로 안 드러나는 durable 지식) | **메모리** (`~/.claude/.../memory/`) | "스폰 flush 통과→Depenetrate", "Mirror transport=메인스레드라 ping 정확도 이득 미미" |
| **구조 / 설계 계약 / 컨벤션** (선언적, 시간 비의존) | **아키텍처 문서** (`docs/*.md`) | connection-architecture, netcode-redesign(모델), topology |
| **태스크 상세** (단계·근거·체크리스트) | **spec / plan** (`docs/superpowers/`) | 각 슬라이스의 설계·구현 계획 |

**따라서:**
- 메모리 status 파일은 status 역할을 벗고 **durable 지식만** 남긴다 → 손댈 때 기회 있을 때 얇게 정리(지금 일괄 이관 X — YAGNI).
- 아키텍처 문서의 "상태" 절은 세부 진행을 적지 말고 **이 파일을 가리킨다**.
- 완료 원장의 "한 줄"은 spec/plan 링크만 걸고, 상세는 그 문서에 둔다.

---

## 📍 지금 어디 (2026-08-26 세션 끝)

**판치기가 게임이 됐다.** 차례가 돌고, 20초 안에 못 치면 넘어가고, 동전을 다 뒤집으면 이긴다
(슬라이스 4). 그 위에 **손바닥 치기**를 얹었다 — 손가락 하나가 아니라 여러 개로 치고, **손가락 수와
벌린 간격이 결과를 바꾼다**. 원본 프로젝트가 이미 그랬는데 슬라이스 3이 옮기면서 조용히 떨어뜨린 것을
되살린 것이다.

**손가락 수·간격이 결과를 바꾸는 것을 수치로 확인했다** (FourInLine 대형, 실제 노브 값):

| 접촉점 | x=-1.05 | x=-0.35 | x=0.35 | x=1.05 | 합 |
|---|---|---|---|---|---|
| 1개(중앙) | 1.095 | 3.905 | 3.986 | 1.114 | 10.100 |
| 3개 모아서 | 3.285 | 11.718 | 11.959 | 3.344 | 30.306 |
| 3개 벌려서 | **5.447** | 8.986 | 9.005 | **5.553** | 28.991 |

수를 늘리면 합이 정확히 3배(선형 누적), **벌리면 합은 비슷한데 분포가 뒤집힌다** —
바깥 동전이 65% 더 받고 가운데는 덜 받는다(집중도 0.395 → 0.311). 검증 루틴에 영구 추가.

**dev 환경도 최신화했다** — 게임서버 `c483292`(08-10) → `48cdce7`, 백엔드 4종 `3599617`(08-11)
→ `1876494`(마이그레이션 5개 포함). 실기기 APK는 dev로 굽는다(로컬은 `GAME_SERVER_PUBLIC_IP`가
127.0.0.1이라 폰이 못 온다).

타격의 토대(슬라이스 3)와 에셋 배달 경로(`content-deploy`)는 아래 원장에 남겼다.

**메시지 버스가 구독 순서를 지키게** 됐다 — "내 캐릭터를 못 알아본다"의 진범이 pub/sub 호출
순서였고, 버스를 GameFramework 공통으로 교체해 클·서가 같은 클래스를 쓴다. 유저 위치 트랙은 사실상
닫혔다 — 남은 건 서버 push 하나뿐이고 그건 인프라 신설(별도 트랙 크기)이다.

### 이번 세션(2026-08-26)에 닫힌 것
| | 항목 |
|---|---|
| ✅ | **판치기 슬라이스 4 — 턴 루프** — 차례 교대·20초 타임아웃·승패 판정. 7레포 머지 + 배포 + 두 클라 실플레이 |
| ✅ | **판치기 손바닥 치기(멀티터치)** — 6레포 머지 + 로컬·dev 배포 + PC 회귀 + 실기기 검증 |

### 2026-08-25에 닫힌 것
| | 항목 |
|---|---|
| ✅ | **판치기 슬라이스 3** — 타격 입력 + 힘 커널 + 물리 포트 정리. 8레포 머지 + 배포 2종 + 두 클라 실플레이 |
| ✅ | **동전 에셋이 서버에 안 보이던 문제** — 로컬 그룹 → 원격 그룹 `Panchigi` + `content-deploy` |

### 2026-08-24에 닫힌 것
| | 항목 |
|---|---|
| ✅ | **판치기 게임 모드 슬라이스 1~2** — 8레포 머지 + 백엔드/게임서버 배포 + 두 클라 입장 검증 |
| ✅ | **메시지 버스 순서 보장** — MessagePipe 기본 브로커가 해제된 자리를 재사용해 3회차부터 순서가 뒤집혔다. 3레포 머지 + local 배포 + 실플레이 검증 |
| ✅ | **Flappy Race B2-d2** — 몸통 캡슐 통일 + 클라가 자기 새를 예측 + 날갯짓 UI |
| ✅ | **엔티티 동기화 모드 선택 구조** — 게임마다 보간/예측을 DI로 고른다 |
| 🟡 | **Flappy 유령정지 + 원격 외삽** — 코드·리뷰 완료, **머지 안 함**(눈 검증 미완). 아래 참조 |

### 🟡 Flappy 유령정지 + 원격 외삽 — 코드 완료, 머지 대기 (2026-08-24)

브랜치 `feature/flappy-ghost-extrapolation`(7레포). 실플레이 제보 3증상(원격 순간이동 / 끼이면
카메라 진동 / 스폰 갇힘)의 두 뿌리를 고쳤다: 맵 충돌을 **막기 → 유령정지**(통과 + 0.8초 정지 +
0.6초 무적)로, 원격을 **시뮬 → 외삽**(마지막 스냅에서 로컬 시각까지, 상한 0.25초)으로.
몸싸움은 내 새만 원격의 외삽 위치에 부딪히는 한쪽 판정으로 남겼다.

- 태스크 10/11 완료(11=스무딩 임계 실측은 **미착수**, 측정 불가라 값을 지어내지 않음)
- 테스트: 클라 575/575, 서버 546/546. 리뷰 루프가 **제 계획서 결함 3건 + 실제 결함 5건** 적발
- **최종 리뷰가 Critical 적발**: `GhostAppearance`가 새 메시 생성 전에 렌더러를 잡아 **유령 연출이
  런타임에 통째로 무효**였다(예외·로그 없음, 테스트 초록). 지연 해석으로 수정
- **머지 안 하는 이유**: 세 증상이 사라졌는지 **눈으로 확인하지 못했다.** G1 뼈대만 서버 시뮬
  실측으로 확인(유령 진입→좌표 동결→통과, 정체 없음)

**남은 것 (다음 일감)**
1. **로컬 2인 검증 리그 정비** — 이번에 5번 시도해 5번 다 다른 이유로 어긋났다(서버 환경이
   standalone 아님 / 매치메이킹 50초 왕복 / 같은 userId 중복 접속 / 리슨 전 접속 / 클라 부팅 45초).
   근본은 **레이스 시작 게이트가 없어** 사람이 붙을 창이 없다는 것. `[[local-two-client-test-rig]]`
2. 그 위에서 **G2·G3 눈 검증** + Task 11 임계 실측 → 그다음 머지
3. `ExtrapolatedEntityInterpolator`/`GhostAppearance` **PlayMode 테스트** — C1이 이 공백의 청구서였다
4. 알려진 한계 2건(스펙에 기록됨): 내 새의 유령 상태가 보정되지 않음 / 롤백 재생이 저장 안 된
   원격 위치를 읽음


### 2026-08-23에 닫힌 것
| | 항목 |
|---|---|
| ✅ | 매칭 종료 사유별 안내 — 확정 실패를 말없이 넘기던 구멍 |
| ✅ | `locationDetail` 계약 강화 — 판별 유니온을 경계에서 강제 |
| ✅ | 갈라진 계약 정리 — DTO 3벌 → `@lop/server-core/dtos` 한 벌 |
| ✅ | `PhysicsFollower` 접기 + 줍기 판정을 규칙으로 |
| ✅ | EventSystem 하나로(`UIRoot` 프리팹) |
| ✅ | `targetMmr` 조용한 기본값 제거 |
| ✅ | **디스폰 플러시 NRE** — 세션 0개면 서버 틱이 통째로 멈추던 것 (08-06 부수 발견) |
| ✅ | `OrEmpty` 확장 제거 — 4레포 14곳을 표준 C#으로 |
| 🔵 | 매치 생성 원자성 · 클라 해석 일원화 → **강등**(서술이 낡았음) |

### ▶ 다음에 할 것 (값어치 순)
| | 항목 | 크기 | 유저가 겪나 |
|---|---|---|---|
| 🟢 | **판치기 — 판을 사람이 읽을 수 있게** — 동전 앞뒤 아트 + 뒤집힌 개수 표시. 지금은 결과 화면에 가서야 승패를 안다 | 중간 | 예 |
| 🟢 | **판치기 전용 맵** — 판이 10m×10m인데 동전 대형은 2.1m. 판 밖 복귀가 실전에서 안 걸리고, 2턴 만에 승부가 난다 | 중간 | 예 |
| 🟢 | **판치기 슬라이스 5 — 낙(落) 횟수 → 탈락** | 중간 | 예 |
| 🟠 | **장시간 플레이 성능 저하** — 1차 측정 완료, **에디터에선 누수 없음**. 다음은 **실기기 dev 빌드에 같은 방법** 또는 **초반 스폰 스파이크** | 큼 | 예(실기기) |
| ⏸ | **서버 뷰 NRE** — `LOPEntityView.LateUpdate`가 `entityRegistry.Get()` 결과에 null 가드가 없다 | **작음** | 아니오(콘솔만) |
| ⏸ | MasterData `file:` → git URL + tag | 작음 | 아니오 |
| ⬜ | 서버 push — 1초 폴링 제거(WebSocket/SSE 신설) | 큼 | 아니오 |

### 이 세션에서 새로 생긴 능력
**두 클라(메인 + MPPM 클론)를 `unity` CLI로 직접 몰 수 있다** — 매칭·UI 클릭·성능 샘플링을 사람 손 없이
끝까지 검증했다. `[[driving-both-clients-via-unity-cli]]`

### ⚠️ 이 세션의 교훈

**배포는 두 갈래다 — 백엔드를 올려도 게임서버는 안 올라간다.** 판치기를 머지·푸시하고
`backend-deploy`만 돌렸더니 매칭서버는 게임모드 7을 아는데 **게임서버 이미지는 판치기 직전
커밋에 멈춰** 있었다. 증상이 헷갈렸다 — 큐도 잡히고 매칭도 성사되는데 방 파드가 4초 만에
`Error`로 죽었다. 서버 코드는 이미 main에 있었으니 *코드를 아무리 읽어도 안 보인다.*
`[[deploy-has-two-pipelines]]`

**안전 모드는 `recompile_status`로 안 잡힌다.** 에디터가 안전 모드로 갇혀 있는데도 CLI는
`failed:false`를 보고했다. 컴파일 상태를 판정할 땐 *테스트가 실제로 도는지*까지 볼 것.
`[[client-compile-gate-without-editor]]`

백로그 항목 **여섯 개를 확인했는데 정확했던 건 하나뿐**이었다. 착수 전에 코드로 확인할 것.
`[[verify-backlog-claims-before-working]]`

---

## ✅ 한 일 (Done ledger)

최근 활성 워크스트림(넷코드 / 이동 / Stage④) 중심. 오래된 완료 워크스트림은 맨 아래 요약 + 메모리 링크.

### ✅ 판치기 손바닥 치기(멀티터치) — 2026-08-27, 6레포 머지 + 로컬·dev 배포 + 실기기 검증

**무엇**: 손가락 하나가 아니라 **손 전체**로 친다. 손가락마다 접촉점(누른 자리 + 끈 방향 + 누른 시간)을
모아 **전부 떨어지면** 한 통으로 보내고, 서버가 접촉점마다 **기존 힘 커널을 그대로** 돌려 임펄스를
누적한다. 손가락 수와 간격이 결과를 바꾸는 건 힘 모델이 달라서가 아니라 **힘이 각자 자리에서 여러 번
들어가기 때문**이다 — 새 물리를 만들지 않았다.

**왜 지금**: 원본 프로젝트(`workspace/panchigi`)의 `InputController`가 `Dictionary<int, InputData>`로
**손가락마다 따로 추적**하고 있었는데, 슬라이스 3이 옮기면서 `Pointer.current`(주 포인터 하나)로
줄여 버렸다. spec에 "원본 조작을 유지한다"고 적었지만 실제로는 *손가락 하나의 제스처*만 옮긴 것이다.
사용자가 "샘플 프로젝트는 이미 멀티터치였는데"라고 지적해 드러났다.

**잠근 결정**
- 한 번의 치기 = **손가락이 전부 떨어질 때까지** (턴과 어긋나지 않게)
- 상한은 **컨피그** `TbPanchigiConfig.contact_max` = **4**. "동시에 눌린 손가락 수"가 아니라
  **한 번의 치기가 모으는 총 개수** — 떼도 자리가 안 나고 무시된 손가락이 승격되지 않는다
- **클라가 먼저 자르고**(먼저 닿은 순서) **서버는 방어선**(초과면 치기 전체 거절).
  닿는 순간 결정돼야 조준선이 "이 손가락은 안 센다"를 즉시 보여준다
- **판을 못 맞힌 손가락은 자리를 먹지 않는다**
- 서버 거절은 **전부 아니면 전무** — 하나만 어겨도 치기 전체를 버린다(클램프하면 조작값이 조용히 섞인다)
- 와이어: 접촉점을 **`@auto_generate` 없는 payload 타입**으로 빼서 **MessageId가 한 줄도 안 밀렸다**

**최종 리뷰가 잡은 Critical — 짧은 탭 한 번이 그 차례를 죽였다**
계획이 `PollTouches`의 누름 분기 끝에 `continue;`를 넣어 **같은 프레임의 뗌을 못 보게** 했다.
Input System은 짧은 탭에서 `wasPressedThisFrame`과 `wasReleasedThisFrame`이 **동시에 true**가 된다.
바로 아래 주석이 "누르고 뗀 게 같은 프레임이면 탭이 씹힌다"고 경고하는데 코드가 그 사고를 저질렀고,
교체 전 코드는 `if`를 각각 두어 막고 있었다.
- 폰: 그 손가락이 영영 눌린 상태로 남아 그 차례 내내 아무것도 안 나감 → 20초 뒤 넘어감
- PC: 씹힌 뒤 다음 클릭이 무시되고 낡은 앵커로 확정 → 의도치 않은 **풀파워 타격**

**계획의 순서 결함**: 와이어(Task 2)를 바꾸고 소비처(Task 5)를 세 태스크 뒤에 고쳐서
**그 사이 클라가 안 빌드**됐다. 다음에 와이어를 바꿀 땐 소비처를 같은 태스크에서 따라가야 한다.

**아무것도 검증하지 않는 테스트를 지웠다**: `f(a)+f(a)==f(a)*2`는 커널이 뭘 계산하든 항상 참이다.
스펙 §6이 요구한 "합산"·"간격" 성질은 **핸들러 루프 + PhysX 누적**에서 일어나 물리 씬 없이는 단위
검증이 불가능하다 — 왜 여기 없는지를 주석으로 남기고 실기기 플레이테스트에 맡겼다.

**배포 실패는 우리 문제가 아니었다**: `startup_failure` + "workflow file issue" 메시지를 곧이곧대로
받아 워크플로 파일과 맥 러너를 의심했다(사용자에게 러너 재시작까지 요청 — 헛수고). 파일이 직전 성공
커밋과 **blob 해시까지 동일**한데도 계속 파일을 팠고, 늦게야 상태 페이지를 봤다:
**GitHub Actions major outage**(15:09~), "database primary 문제로 리플리카 페일오버 중".
**CI가 코드 변경과 무관하게 갑자기, 여러 번 같은 방식으로 깨지면 공급자 상태를 먼저 본다.**

**파킹 (되살아나는 조건 포함)**
- `BoardLayerMask`가 Default 전체라 레이가 **동전에도 맞는다.** 판 끝에 걸친 동전을 짚으면 그 접촉점이
  판 밖이 되어 all-or-nothing으로 치기 전체가 거절된다. 지금은 판이 10m라 도달 불가(최대 세기로 몰아도
  3.6m가 한계). ⚠️ **전용 맵에서 판이 작아지면 곧바로 도달 가능**해진다 — 전용 `Board` 레이어 필요
- 거절·미송신에 **클라 피드백이 없다**(차례가 20초 묶임). ⚠️ 위가 도달 가능해지면 같이
- `Touchscreen.current != null`이면 **마우스 경로가 통째로 죽는다**(하이브리드 기기). ⚠️ 그 기기를 지원할 때
- `contacts`가 옛 `strike_point`의 **field 번호 1을 재사용**. ⚠️ 무중단 배포를 하게 되면

spec `2026-08-26-panchigi-multitouch-strike-design.md`, plan `2026-08-26-panchigi-multitouch.md`.

### ✅ 판치기 슬라이스 4 — 턴 루프 (2026-08-26, 7레포 머지 + 배포 + 두 클라 실플레이 검증)

**무엇**: 판치기가 게임이 됐다. 차례가 돌고(`Settling` → `Aiming`), **20초** 안에 못 치면 넘어가고,
동전을 **다 뒤집으면** 그 사람이 이긴다. 판 밖으로 나간 동전은 제자리로 돌아온다.
스폰 자리는 코드가 아니라 **씬의 `PanchigiBoard`** 가 대형별로 들고 있다.

**최종 리뷰가 잡은 Critical — 턴 상태가 클라에 영영 안 닿았다**
`LOPRoom.StartGameAsync`가 플레이어를 안 기다리고 `runner.Run()`을 부르는데 턴 상태는 **바뀔 때만**
방송했다. 그래서 `Settling`·`Aiming(P1)` 두 방송이 **세션 0개**에게 나가고, 나중에 접속한 클라는
현재 상태를 영영 못 받았다 — HUD 미표시 + P1이 자기 첫 차례에 못 침. **매 판 100% 재현.**
엔티티 스냅샷은 매 틱 방송이라 자동으로 따라잡히는데 턴 상태만 send-on-change라 생긴 비대칭이다.
고침: "이번 상태를 못 받은 연결 세션에게 매 틱" + **끊긴 세션은 집합에서 지운다**(재접속은 같은
sessionId를 그대로 쓰므로 — 이건 재리뷰가 잡았다).

**DI 순환으로 게임서버가 부팅 실패** — `LOPRunner → PanchigiRuleSystem → PanchigiTurnSystem → LOPRunner`.
방 파드가 3초 만에 죽었다. **규칙이 이미 코드베이스 두 곳에 적혀 있었다**(`FlapWangRuleSystem`:
"룰은 호스트를 역참조하지 않는다", `GameLifetimeScope`: "호스트 역참조를 피하기 위해 직접 등록").
계획이 그걸 안 읽고 `IRunner`를 턴 시스템에 넣었다 — **있는 해법을 놔두고 새로 지은 것.**
고침: `ITickUpdater` 주입 + 등록을 조립 지점으로.

**실플레이에서 드러난 것**
- 동전이 **앞뒤 구분이 안 된다**(임시 실린더) → 사람이 판을 못 읽는다. 결과 화면에 가서야 승패를 안다
- 판이 **10m×10m**인데 동전 대형은 2.1m → 판 밖 복귀가 실전에서 안 걸리고 2턴 만에 승부가 난다

spec `2026-08-26-panchigi-slice4-turn-loop-design.md`, plan `2026-08-26-panchigi-slice4.md`.

### ✅ 판치기 슬라이스 3 — 타격 입력 + 힘 커널 (2026-08-25, 8레포 머지 + 배포 2종 + 두 클라 실플레이 검증)

**무엇**: 판을 끌어 치면 동전이 튀는 첫 상호작용. 클라는 *조준선만* 그리고, **힘은 서버가 정한다**.

**힘을 정하는 방식 — "덮임(coverage)"**. 원래 설계는 "친 지점에서 멀수록 약하게"였는데, 그건
**동전이 포개지거나 판 끄트머리에 반쯤 걸친 경우를 못 다룬다**(사용자 지적). 그래서 거리가 아니라
*판에 실제로 닿아 있는 면적*으로 세기를 정한다:

- 친 지점 주위에 **고정 개수(13개)의 샘플점**을 원판 위에 골고루 뿌린다(Vogel 나선 — 개수가 몇이든 고르다).
- 샘플마다 **아래로 레이를 쏘아 "처음 맞는 게 판인가"** 를 본다. 판이면 그 샘플은 살아 있다.
- `덮임 = 살아있는 샘플 / 전체 샘플`. 포개진 위쪽 동전은 아래에 동전이 먼저 맞아 전부 탈락 →
  **직접 힘을 안 받는다**(밑 동전이 튀며 밀어낼 뿐). 반쯤 걸친 동전은 절반만 살아 절반의 힘을 받는다.

> **레이 사거리는 상수가 아니라 동전 몸에서 뽑는다** — 판에 닿아 있다면 중심이 몸의 대각 절반
> (`|(r, t/2, r)|`)보다 높이 뜰 수 없다. 이보다 짧으면 **모로 선 동전이 영영 안 맞고**, 길면 얹힌
> 동전도 아래 판까지 닿아 통과한다.

**같이 정리한 부채(3-0)**: 물리 포트가 *엔티티 이름을 아는* 게 층 위반이었다 —
`IOverlapQuery`(엔티티 id를 돌려줌)를 없애고 `ICollisionQuery`에 `Raycast`/`OverlapSphere`를 합쳤다.
포트는 **콜라이더만** 돌려주고, 그 위에 얇은 확장(`CollisionHit.GetEntityId()`)이 엔티티를 되짚는다.
되짚기는 표를 만들지 않고 **매번 `GetComponentInParent<EntityActor>()`** — 관리 포인트를 늘리지 않는 쪽.

**최종 리뷰가 잡은 것(태스크별 리뷰 12번이 다 통과시킨 뒤)**:
- **Critical — 겹침 판정이 정확히 뒤집혀 있었다.** 사전 판정에서 레이 방향을 위→아래로 바꾸며
  *"왜 위로 쐈는지"*(= 제일 아래 = 판에 닿은 것을 고르려고)를 같이 버렸다. 결과는 반대 —
  *제일 위에 얹힌* 동전만 맞는 것. 고친 뒤 **에디터에서 실측**해 증명: 평평한 동전 64/64 생존,
  모로 선 동전 64/64(전엔 0/64), 공중 0.5m 0/64, 포갠 쌍은 아래 64/64 · 위 0/64.
- **Critical — 클라가 최대로 끌면 서버가 거절**했다(부동소수점 반올림). 서버 경계에만 `BoundEpsilon` 부여.

**배포**: `gameserver-deploy`(local) → `re5nardo/game-server:270bc5b`. **`backend-deploy`는 건너뜀** —
판치기 노브는 Luban group `c`/`s`에만 있고 매칭서버가 보는 group `m`(tbgamemode/tbmap/tbqueue)엔 안 닿는다
(`lop-backend` 워킹트리·미푸시 0으로 재확인).

**검증(두 클라 실플레이, 08-25)**: 조준선 ✅ · 동전 튐 ✅ · 양쪽 클라 소수점까지 일치 ✅ ·
0.7초 타격에 3개가 180° 뒤집힘 ✅ · 거리 감쇠 순서 ✅ · 서버 예외 0 ✅ · 클라 게임플레이 에러 0 ✅.
**FlapWang 근접 회귀 없음** — 다만 *내가 세운 검증 신호가 틀렸다*: "아이템 근처에서 경고가 사라진다"고
적었는데 근접 판정은 이미 `LayerMask.GetMask("Character")`로 걸러 아이템이 원래 안 들어온다. 대신
8개 지점에서 트리거 포함/제외 오버랩을 각각 재어 **전부 `all == solid`, Character 레이어의 트리거 0개**를
확인했다 — 바꾼 코드가 지나가는 집합이 동일하다.

**튜닝**: `force_multiplier=8` / `horizontal_force_multiplier=2`. 처음 "너무 약하다"고 판단했다가 뒤집었다 —
0.15초짜리 비현실적인 탭으로만 쟀던 탓이다. 누른 시간을 쓸어 보니 0.4초에 1개, 0.7초에 2개가 더 뒤집히고
이동은 0.2~0.6m로 판(10×10) 안에 머문다.

**어드레서블에서 세 번 헛짚었다 (기록)**: 배포된 서버가 동전 프리팹마다
`InvalidKeyException: No Location found`를 뱉었다. 원인은 처음부터 하나 — **동전을 클라의 *로컬* 그룹
`Vfx`에 넣어 S3에 배포되지 않았던 것**. 그런데 세 번을 엉뚱하게 고쳤다:
① "빌드가 카탈로그를 안 굽는다" → 플레이어 빌드가 이미 굽고 있었다.
② 원격 카탈로그 끄기가 무효 → `finally`가 `BuildPlayer`의 *두 번째* 굽기 전에 되돌렸다
(빌드 로그에 `Post Processing Catalog Entries`가 두 번 찍힌다).
③ 원격 끄기가 작동하자 **아트 씬이 깨졌다**(`FlapWangMap.unity` 못 찾음) — 원격이 실제 배달 경로였다. 되돌림.
**이 프로젝트는 모바일·서버가 S3에서 어드레서블을 받는 게 기본이고, 구운 결과물을 올려야 반영된다.**
해결: 새 원격 그룹 `Panchigi`(Item 스키마 복제 — build `ServerData/[BuildTarget]`, load `s3://lop-assets/dev/[BuildTarget]`)로
동전을 옮기고 **클라 레포의 `content-deploy`(target=gameserver)** 실행 → `panchigi_assets_all_*.bundle` 업로드,
서버 예외 0. 진단 중 서버 레포에 넣었던 동전 사본·그룹 등록·빌드 훅은 되돌렸다(`366f597`).
`[[deploy-has-two-pipelines]]` `[[measure-before-theorizing-netcode]]`

**배포**: `gameserver-deploy`(local) + **`content-deploy`(gameserver)**. 후자가 빠져 있던 단계다 —
서버 레포만 배포해서는 새 에셋이 반영되지 않는다. `backend-deploy`는 불필요(판치기 노브가 group `m`에 안 닿음).

머지: GF `18bfe9e` / Shared `3e9aa2a` / Client `d2117c7` / Server `366f597` /
MD-Client `9f7a786` / MD-Server `fb7c221` / infra.
spec `2026-08-24-panchigi-slice3-strike-design.md`, plan `2026-08-24-panchigi-slice3.md`.

### ✅ 판치기 게임 모드 — 슬라이스 1~2 (2026-08-24, 8레포 머지 + 배포·두 클라 입장 검증)

**무엇**: 별도 프로젝트였던 판치기를 FlapWang·Flappy Race에 이어 **세 번째 게임 모드**로 흡수했다.
슬라이스 1~2의 목표는 *연출이 아니라 "입장이 된다"* — 판이 차려지고 클·서 월드가 같은 모양이 되는 것까지다.

**룰(잠긴 결정)**: 모든 동전이 뒤집히면 종료, **마지막에 친 사람이 승자**. 뒤집힌 동전은 *제자리(초기
세팅)* 로 되돌린다. 동전 개수는 **참가 인원에 따라 달라지게 배선**만 해두고 실제 수치는 나중에 튜닝한다
(2인 → 4개). 낙(落) n회면 탈락이고 n은 컨피그에 있다. 탈락자 순위는 **승자 1등 / 나머지 공동 꼴등**.

**구조에서 바뀐 것 — 몸(物理) 설정을 데이터로 뺐다.** 이게 이 슬라이스의 진짜 무게다.
동전은 Unity 물리가 굴리는 **다이나믹 바디**인데, 기존 바디 생성은 "캐릭터는 캡슐 + 키네마틱"이 코드에
박혀 있었다. 새로 만든 것:

- `PhysicsConfig`(World Core 컴포넌트) — `BodyKind {Static, Kinematic, Dynamic}` + `FreezeRotation` + `IsTrigger`.
  **필수다**(없으면 팩토리가 던진다) — 없을 때 조용히 예전 동작을 하면 빠뜨린 걸 아무도 모른다.
- `DiscShape` — 반지름·두께. 모양은 `PhysicsConfig`와 **따로** 둔다. 순수 코어의 sweep이 모양을 읽기 때문.
- `PhysicsBodyFactory`가 `Create(root, worldEntity)` 하나로 줄고, 컴포넌트를 보고 분기한다.

> ⚠️ **`Simulated`와 헷갈리지 말 것.** `Simulated`는 *우리 시뮬이 이 엔티티의 틱을 소유하는가*이고
> `PhysicsConfig.Kind`는 *Unity 물리 엔진이 이 몸을 어떻게 취급하는가*다. 동전은 **`Simulated`가 아니다** —
> 우리 코어가 굴리는 게 아니라 PhysX가 굴리고, 우리는 결과를 읽어 온다.

**배포에서 걸린 것**: `backend-deploy`만 돌리고 **게임서버를 빠뜨렸다.** 매칭은 성사되는데 방 파드가
4초 만에 `Error`로 죽었다 — 게임서버 이미지가 판치기 직전 커밋(`b7113ea`)이라 모드 7을 몰랐다.
`gameserver-deploy`(local)를 돌려 `7ea507c`로 올리고서야 방이 살았다. `[[deploy-has-two-pipelines]]`

**검증**: 두 클라(메인 + MPPM 클론)를 `unity` CLI `eval`로 몰아 판치기를 골라 PLAY.
- 서버 방 파드 `Running` 유지 — `Registered panchigi player 1·2`, `Registered coin 3·4·5·6`, 예외 0
- 클라 **양쪽 모두 엔티티 6개** — 플레이어 2(`PhysicsConfig`) + 동전 4(`PhysicsConfig`+`DiscShape`), 서버와 일치
- 입장 시각 이후 클라 에러 0

머지: GF `5be12cc` / Shared `fffaee3` / Client `f68f975` / Server `7ea507c` /
MD-Client `2ba9da8` / MD-Server `203d8fa` / infra `308d4be` / backend `1876494`.
배포: `matchmaking-server:1876494`, `game-server:7ea507c`.
spec `docs/superpowers/specs/2026-08-24-panchigi-game-mode-design.md`,
plan `docs/superpowers/plans/2026-08-24-panchigi-slices-1-2.md`.

#### 남은 것 — 슬라이스 3~ (보이게 만들기)
지금은 **입장만 되고 화면은 비어 있다.** 아래는 슬라이스 1~2에서 *의도적으로* 미룬 것들이다.

| | 항목 |
|---|---|
| 🟢 | 전용 `Assets/Art/Scenes/PanchigiMap.unity` + Addressables 등록 (지금은 FlappyRace 패턴을 베껴 없는 씬을 가리킴 — 그 패턴 자체가 이미 깨져 있었다) |
| 🟢 | 동전 아트 + `formation`(초기 배치) 해석 |
| 🟢 | 플레이어 스폰 좌표 — 지금은 판 아래 `(0,-10,0)` 임시값 |
| 🟡 | `DiscShape.Thickness`가 콜라이더에 안 닿는다 — Unity가 구로 뭉갠다 |
| 🟡 | `PhysicsBodyFactory`의 레이어가 아직 `Character` 하드코딩 |

작은 정리(급하지 않음): `PhysicsBodyFactory`/`UnityPhysicsBody`의 낡은 클래스 주석,
서버 `EntityBinder`의 무방비 `PhysicsConfig` 읽기, Item 분기에만 없는 중복 엔티티 가드,
아무도 안 쓰는 `PhysicsConfig.PhysicsOwnsMotion`(틱 시스템은 `PhysicsBody.IsKinematic`으로 분기한다).

### ✅ 메시지 버스 순서 보장 — `OrderedMessageBroker` (2026-08-24, 3레포 머지 + local 배포·실플레이 검증)

**증상**: 매치에 들어가도 내 캐릭터를 못 알아봐 조작이 안 된다. 한 세션에서 매치를 반복하면 나오고,
새로 켜면 재현이 안 돼 "가끔 난다"로 보였다.

**진범은 pub/sub 호출 순서였다.** `GameInfoToC` 한 통에 "네 캐릭터는 누구"(`EntityId`)와 "이 엔티티들을
만들어라"(`EntityCreationDatas`)가 같이 오는데, 둘을 **서로 다른 구독자**가 나눠 먹는다 —
`GameDataStore`가 id를 칠판에 적고, `GameInfoMessageHandler`가 스폰하며 `EntityBinder`를 통해 그
칠판을 읽는다. 스포너가 먼저 불리면 칠판이 비어 있어 예측 대상 지정과 `playerContext.actor`가 둘 다 실패한다.

**MessagePipe 기본 브로커는 호출 순서를 보장하지 않는다.** `MessageBrokerCore.Publish`는 핸들러 배열을
**인덱스 순서**로 돌고, `FreeList.Add`는 해제된 자리를 큐에서 꺼내 **재사용**한다. `freeIndex`는 FIFO 큐가
맞지만 그 FIFO는 *구독자*가 아니라 **반환된 빈 자리 번호**에 대한 것이다. 패키지의 `FreeList.cs`를 그대로
컴파일해 매치 구독/해제 사이클을 돌리면 **3회차부터 뒤집힌다** — 실제 증상도 세 번째 판에서 났다.
브로커가 `RootLifetimeScope` 등록이라 빈 자리 큐가 앱 수명 내내 살아 있어, 반복할수록 어긋난다.

**고침**: `GameFramework`에 `OrderedMessageBroker<T>`(+키 버전)를 만들고 등록만
`RegisterMessageBroker` → `RegisterOrderedMessageBroker`로 바꿨다. 자리를 재사용하지 않고 항상 뒤에
붙이므로 배열 순서 = 구독 순서다. 해제한 자리는 비워만 두고 절반 넘게 비면 앞으로 당기되(앞뒤 유지),
**발행 중에는 당기지 않는다** — 순회 중 칸이 밀리면 건너뛰거나 두 번 부른다.
`IPublisher`/`ISubscriber`는 MessagePipe 것을 그대로 구현해 **호출부는 한 줄도 안 바뀌었다.**

**왜 GameFramework인가**: 버스는 게임 무관 인프라고(결정 트리 #1), 목적지 폴더에 이미
`MessageHandlerBase`가 살고 있었다. 서버도 같은 것을 쓴다 — 서버는 지금 메시지당 구독자가 하나라 증상이
없었지만 둘째를 붙이는 순간 조용히 깨지는 잠복 상태였다. LOP-Shared는 오답(도메인 아님).

**잠긴 결정**: 안 쓰는 변형(Async/Buffered)은 등록하지 않는다. 필터는 미구현이라 넘기면
`NotSupportedException` — 조용히 무시하지 않는다. `RegisterMessagePipe` 자체는 남긴다
(`GlobalMessagePipe`가 쓰는 `IServiceProvider` 등록이 거기 있다).

**검증**: 브로커 EditMode 11개(클 548 / 서 522 green) — 그중 "구독·해제 10사이클 순서 유지"는 옛 구현으로
돌리면 3회차에서 깨지는 테스트다. 런타임은 ① 등록된 메시지 11/11 + 키 브로커가 새 클래스로 해석됨,
② **실행 중인 앱의 진짜 브로커로 6사이클 순서 유지**(3·4회차 포함), ③ 실매치 2판 + 배포 후 1판
(`userEntityId`/`actor`/`simulated=1`, Recon 0.00m, 새 가드 미발동).

머지: GF `fce77b5` / Server `b7113ea` / Client `c8e0cb2`. 배포: `re5nardo/game-server:b7113ea`,
infra `993b9c0`, 매치 pod이 그 이미지로 Running 확인. `[[messagepipe-handler-order-not-fifo]]`

> ⚠️ **검증 중 관찰(미확정)**: 매치 종료가 두 번째부터 안 먹는 것처럼 보였다. 단 서버 대신
> `MatchEndedToC`를 로컬에서 흘려 넣은 **비정상 경로**라 진짜 버그인지는 확인하지 못했다.
> 실제 종료 상황에서 로비 복귀가 이상하면 그때 파볼 것.

### ✅ Flappy Race B2-d2 — 새가 난다 (2026-08-24, 4레포 머지)

몸통 캡슐 치수를 엔티티 컴포넌트로 통일(`CapsuleShape`, GF) + 공유 `BodySizes`(Shared), 클라가 자기 새를
`Simulated`로 예측, 전체화면 탭 = 날갯짓 UI(`FlapPadView`). 카메라 드래그는 이 게임에 불필요해 제거.
검증: 날갯짓으로 떠오르고 파이프 틈을 통과 — B2-d1이 채점 못 했던 그 항목. 머지: GF `24f6d11` /
Shared `abfd08c` / Client `1dd49e5` / Server `3ae33c9`.

### ✅ 엔티티 동기화 모드를 게임이 고른다 (2026-08-24, 2레포 머지)

원격 엔티티를 **보간할지 예측할지**를 게임별 정책으로 뺐다. `IEntitySyncPolicy` + `EntitySyncMode`
{Interpolated, Predicted} (클라 `LOP.EntitySync` asmdef). FlapWang = `OwnerPredictedSyncPolicy`(내 것만
예측 — 남을 밀어내는 게 게임성이 아니다), Flappy Race = `CharactersPredictedSyncPolicy`(새끼리 몸싸움이
게임성이라 전부 예측). `Simulated` 마커는 유지하되 **정책에서 파생**되고, `EntityBinder`가 유일한 부착
지점이다. 내 캐릭터 자체의 모드 선택은 **자리만 열어둠**(미구현).

**리서치 근거**: 원격 예측은 업계 표준(Rocket League는 모든 차·공을 예측, Photon Fusion은 Forecast
Physics로 로컬 시각까지 외삽). "최종 속도로 위치만 외삽"하는 dead reckoning(IEEE 1278)은 **채택 안 함** —
파이프 충돌이 있는 우리 게임엔 물리 없는 외삽이 벽을 뚫는다.

**검증**: 클라 측 몸싸움 실측 — bird2가 (364.73, −49.80)에 서 있고 접촉 순간 메인 (365.09, −50.24) /
클론 (367.74, −49.88)로 **각자 독립 예측**한 뒤 둘 다 (369.53, −49.92)로 수렴. FlapWang은 무변
(`simulated=1`). 머지: Shared `ceb4013` / Client `dc3c3c8`.

### Stage④ + 넷코드 + 이동 (2026-07, 시간순)

| 날짜 | 슬라이스 | spec/plan |
|---|---|---|
| 07-01 | **Motion 권위 → World.Entity** (Slice 4 마무리 문서 포함) | `specs/2026-07-01-stage4-motion-world-authority` |
| 07-02 | **input-as-data** — `InputCommand` + `InputBuffer` World 컴포넌트, 이동을 `LOPWorld.Tick`으로 | `specs/2026-07-02-input-as-data`, `-4e-velocity-apply-to-world`, `-4e-dash-world-direct-velocity-bridge` |
| 07-04 | **Stage④ slice 2 — SnapshotHistory** 기록 + DebugHud | `specs/2026-07-04-stage4-snapshot-history` |
| 07-04 | **Stage④ slice 3 — 하드 롤백 reconcile** 복원+재생 | `specs/2026-07-04-stage4-rollback-reconcile` |
| 07-04 | **Stage④ — 원격 엔티티 kinematic** + 가드 | `specs/2026-07-04-stage4-remote-kinematic` |
| 07-05 | **Stage④ — 어빌리티/상태이상 예측 replay** (풀 상태 스냅샷 + 풀 틱 재조정) | `specs/2026-07-05-stage4-ability-replay` |
| 07-05 | **velocity 단일 권위 + 기여 모델 slice 1** (MovementSystem = velocity 유일 writer) | `specs/2026-07-05-velocity-motor-contribution-slice1` |
| 07-05 | **넉백 slice 2** — 스냅 수신 + Reconciler 스냅 복원 (Additive 기여 첫 실사용) | `specs/2026-07-05-velocity-knockback-slice2` |
| 07-06 | **넉백 MasterData 승격** — AbilityDataProvider 매핑 | `specs/2026-07-06-knockback-masterdata-promotion` |
| 07-06 | **멀티틱 입력 기아 수정** (걷기 정상화) | `specs/2026-07-06-multi-tick-input-starvation` |
| 07-06 | **렌더 보정 offset-decay 이관** (`RenderCorrectionSmoother`) | `specs/2026-07-06-render-correction-smoothing` |
| 07-07 | **원격 엔티티 표준 스냅샷 보간** (receive-anchored 재생시계 + Hermite + 적응형 쿠션) | `specs/2026-07-07-remote-entity-interpolation` |
| 07-09 | **공유 키네마틱 캐릭터 컨트롤러 이행** (slice 1~3, 4레포 main) — velocity·위치 권위 = `World.Entity`, 예측=권위로 지면 recon 소멸 | `specs/2026-07-09-shared-kinematic-character-controller-design`, `plans/2026-07-09-kinematic-*` |
| 07-09 | **Depenetrate 공유 헬퍼 추출** (`KinematicDepenetration`, 클·서 중복 제거) | — |
| 07-10 | **확정 게이트 — 재생 억제 (방식 1)** — `WorldEventBuffer.Suppress()`(GameFramework) + `Reconciler` cue 손-회피 제거(라이브/재생 발동 경로 통일). 재생이 만든 연출을 억제 스코프가 버림 | `specs/2026-07-09-commit-gate-replay-suppression-design`, `plans/2026-07-09-commit-gate-replay-suppression` |
| 07-10 | **A1 — `DeterministicRandom`** (SplitMix64 결정론 난수 struct, GameFramework, 엔진 비의존) — 클라 예측 전투(A)의 첫 조각. 씨앗 유도·서버 배선·IRandom 교체는 A2 | `specs/2026-07-10-deterministic-random-primitive-design`, `plans/2026-07-10-deterministic-random-primitive` |
| 07-12 | **A2.1 — 서버 전투 키 RNG + 매치시드** (4저장소) — `LOPCombatSystem`이 키 `hash(matchSeed,tick,attacker,target,effectIndex)`로 크리/회피, `Hashing` 헬퍼, `AbilityEffectContext.EffectIndex`, `GameInfo.match_seed` 서버→클라 동기(클라 보관, A2.3 흡수). combat만 | `specs/2026-07-12-a2-1-server-combat-keyed-rng-design`, `plans/2026-07-12-a2-1-server-combat-keyed-rng` |
| 07-12 | **A2.2a — 전투 해소 LOP-Shared 공유화** — `LOPCombatSystem`을 서버→LOP-Shared 공유 concrete(`World.Entity`+씨앗 param), `ICombatSystem` 제거, `DamageEffectHandler` 배선. 이동으로 전투 해소 EditMode 테스트 가능. (+별도 밸런스: 데미지 3배) | `specs/2026-07-12-a2-2a-combat-resolution-shared-design`, `plans/2026-07-12-a2-2a-combat-resolution-shared` |
| 07-12 | **A2.2b — 히트 판정 LOP-Shared 공유화** (4저장소) — `IOverlapQuery`(GameFramework 포트, `ICollisionQuery` 짝) + 사이드별 `LOPOverlapQuery`(엔진 broad-phase). 부채꼴 필터·자기제외·Attack 루프를 공유 `DamageEffectHandler`(LOP-Shared)로, `World.Transform`(numerics 진실원본) 기준. 씨앗은 `IMatchSeed`. EditMode 7테스트. 서버 판정 EditMode 테스트 가능화 + 이중타격 dedup 교정. | `specs/2026-07-12-a2-2b-hit-detection-shared-design`, `plans/2026-07-12-a2-2b-hit-detection-shared` |
| 07-13 | **reconciler tick-guard 근본 수정** — 로컬 지연 렌더(`LocalEntityInterpolator`)의 "절대 틱키 dict 조회 + `[임시]` skip 가드"를 `GameFramework.Netcode.SnapshotInterpolation.Solve`(연속 renderTime 브래킷 탐색, 범위 밖 hold → 미스 불가) + EditMode 7테스트로 교체. 시간 기준(Fiedler alpha) 유지. 원격은 07-07에 이미 해소. | `netcode-redesign.md` §8 |
| 07-14 | **공격 어빌리티 이동 정책** (6레포) — 어빌리티가 Startup/Active/Recovery **페이즈별 이동배율(0~1)** + `BlockJump`를 데이터로 선언(`TbAbility`), 공유 `MovementSystem.Tick`이 수평속도에 곱(플레이어=모터결과·AI=잔류속도 공통), 회전은 자유. 업계표준=격투 프레임데이터 + GAS 이속 modifier. Tasks 1-6 TDD(122 EditMode), 플레이 검증. 캔슬(벌처 킁)은 v2 | `specs/2026-07-14-attack-movement-policy-design`, `plans/2026-07-14-attack-movement-policy` |
| 07-15 | **키네마틱 지면 캐칭 수정** — `KinematicMover`가 수평+중력을 한 sweep으로 합쳐, 발이 바닥 flush일 때 그 sweep이 바닥을 dist≈0로 맞아 `moveDist=0` → **수평 이동까지 취소**(발-바닥 접촉 종이한장 차이로 간헐 발현). 표준대로 **수평/수직 스텝 분리 + step offset**(수평 sweep을 0.1 띄워 발밑 바닥 회피). TDD(`GroundPlaneQuery` 재현) 122/122. **기본형** — 경사 따라가기·명시 step-up·ground snap은 경사/계단 콘텐츠 시 후속 | — |

### 그 밖의 완료 워크스트림 (요약 — 상세는 메모리)

- **Slice 4 (Runner→World 추출)** 리네임 + 4a~4c + I/O 어댑터 — `[[world-core-runner-world-naming]]`
- **어빌리티/상태이상 World Core** (레거시 Action/Status → Ability/StatusEffect, behavior 조합 B0, B1 attack=DamageEffect) — `[[ability-statuseffect-world-core]]`
- **World Core 뷰 이행** (Health/Mana/Level/Stats/Ownership 단일 진실원본, 클·서 패리티) — `[[world-core-view-migration-status]]`
- **넷코드 Phase 0~4** (clock sync + server input buffer + timing feedback) — `[[netcode-migration-status]]`
- **NetworkTime 추상화** (`GameEngine.NetworkTime` facade, 클·서) — `[[netcode-migration-status]]`
- **UI Toolkit 마이그레이션 M1~M5a** — `[[uitoolkit-migration-status]]`
- **MasterData Luban 전환** (α/β/γ) — `[[masterdata-slice-2b-2c-roadmap]]`, `[[masterdata-key-convention]]`
- **`OrEmpty` 확장 제거** (2026-08-23, 3레포 머지: GF `c3c9c83` · Server `8e81123` · Client `97048c3`) — `self ?? Enumerable.Empty<T>()`를 감싼 자체 확장을 4레포 14곳에서 걷어내고 정의도 삭제. 확인해보니 **소스가 null이 될 수 있는 곳이 하나도 없었다**(LINQ 체인 · protobuf `RepeatedField` · VContainer 주입 · 우리 자신의 컬렉션 반환). 표준 지침이 *"컬렉션 반환 멤버는 null 대신 빈 컬렉션을 반환하라"* 이므로 방향이 "호출부마다 가드"가 아니라 "반환하는 쪽이 null을 안 냄"이어야 했다

### 매치메이킹 표준화 트랙 (2026-07-27~)

개념 어휘를 업계 표준으로 바로잡고 매칭을 풀 기반 표준 배치로 전환하는 트랙.
spec `docs/superpowers/specs/2026-07-27-matchmaking-standardization-design.md`
(§7에 `WaitingRoom` 폐기 대상 5레포 41파일 체크리스트).

- ✅ **슬라이스 1 — Luban 테이블 신설** (07-27) — `TbGameMode`/`TbMap`/`TbQueue` 신설 +
  매칭 서버 전용 비기본 그룹 `m` + `matchmaking` 타깃(`typescript-json` + `json`) 추가.
  클·서 MasterData 패키지에 생성물 반영 + `TableFiles` 등록 + 참조 무결성 EditMode 테스트.
  기존 서브게임 5종을 값 그대로 이관해 **동작 무변화**.
  plan `2026-07-27-matchmaking-slice1-luban-tables`.
- ⚠️ **슬라이스 1의 매칭 서버 절반은 죽은 저장소에 적용됐다 → 1b로 재작업**
  (07-28 배포 시도 중 발견). 로더 교체·jest·XML 제거를 `re5nardo/LeagueOfPhysical-MatchmakingServer`에
  적용했는데 그 저장소는 **2025-08-31 아카이브**됐다. 실제 배포 소스는 **`Baeinsoo/lop-backend`
  모노레포**의 `apps/matchmaking-server`다(infrastructure README가 명시; 배포 이미지 태그
  `matchmaking-server:e08245e`의 sha가 아카이브 저장소에 없음으로 실증). 코드·설계는 유효하고
  모노레포 소스가 줄바꿈 빼고 0줄 차이라 이식은 기계적.
  **교훈: 계획 수립 전 대상 저장소가 살아있는지 확인할 것** — 태스크 리뷰 5회와 최종 whole-branch
  리뷰도 "이 저장소가 맞는가"는 묻지 않았다.
- ✅ **슬라이스 1b — 매칭 서버를 모노레포로 이식 + 배포 (07-28~29)** — Task 4·5를 `lop-backend`
  (`apps/matchmaking-server`)에 재적용. 모노레포 소스가 아카이브본과 줄바꿈 빼고 0줄 차이라 이식은
  기계적이었고, 도구 체계만 pnpm/turbo로 맞췄다. 빌드 4/4, 테스트 6/6.
  **중간 교정**: Luban TS 출력을 `src/loaders/generated/` → **`src/masterdata/`** 로 이동 — 루트
  `.dockerignore`의 `**/generated`가 도커 컨텍스트에서 제외해 **CI 이미지 빌드가 깨졌을** 것이고,
  이 레포에서 `generated/`는 "빌드 때 생성, 커밋 안 함"(Prisma) 규약이라 의미가 반대였다.
  **CI에 테스트 단계 추가**(빌드 뒤·이미지 푸시 앞) — 그전엔 새 스위트를 아무도 안 돌렸다.
  plan `2026-07-28-matchmaking-slice1b-monorepo-port`.
- ✅ **클러스터 재구축 + 실제 배포 (07-29)** — Docker Desktop이 클러스터를 재생성해 전부 소멸 →
  문서대로 ingress-nginx → ArgoCD v2.13.2 → `root-app` → platform(wave0) → backend(wave1) 복구.
  `backend-deploy` 워크플로 실행 → 이미지 `e5bd5b6` → infrastructure 태그 자동 bump → ArgoCD 롤아웃.
  **실측 확인**: 파드가 새 이미지로 기동, 컨테이너 안 `master_data`에 Luban json 3개만(XML 없음),
  `MasterData loaded!` 로그, 엔드포인트 200. **매칭도 실제로 성사** — Luban 테이블 조회→정원→매치까지
  동작 확인. GitOps 고리 전체가 한 바퀴 돌았다.
- ✅ **슬라이스 2 — 필드 어휘 리네임 + `Match` 라운드화 (07-30, 3레포 머지·배포·E2E 통과)** —
  `matchType`/`subGameId`/`mapId`가 `queueId`/`gameModeId`/`mapId`(전부 Luban 테이블의 **정수 기본키**)로
  바뀌고 `enum GameMode`가 5곳에서 사라졌다(큐는 이제 코드가 아니라 `TbQueue` 행). `Match`는 게임·맵을
  직접 들지 않고 `MatchRound`(원소 1개)로 든다 — 읽기·쓰기는 `MatchRepository`(애그리게잇 루트)가 감춘다.
  **동작 변화는 하나뿐**: 게임 서버 맵이 하드코딩 상수 → `rounds[0].mapId` → `TbMap.scenePath`.
  `TbMap` 행이 하나뿐이라 결과 씬은 같다(= 변화가 안 보이는 게 성공).
  plan `2026-07-30-matchmaking-slice2-vocabulary-rounds`, spec §8 "슬라이스 2 확정 사항".

  **착수 뒤 뒤집은 결정**: "트랜잭션 안 건다" → **매치 행 + 그 라운드는 한 트랜잭션**. 리뷰가
  `라운드 삭제 → 삽입` 사이가 끊기면 매치가 라운드 0개로 영구 저장되는 유실 경로를 짚었다.
  애그리게잇 하나를 한 DB 안에서 묶는 것뿐이라 값이 싸다(실제 postgres로 롤백까지 실증).
  넓은 경로(룸 생성·유저 위치·티켓 삭제 = HTTP)는 여전히 슬라이스 4.

  **배포 실측**: 마이그레이션이 실DB에 적용되고 **유저 전적이 보존**됐다(`Normal→1`/`Ranked→2` 이관,
  전적 행은 유저 생성 시에만 만들어져 지우면 기존 게스트 로그인이 깨진다). 2클라 매칭→입장→게임 진행 정상.
- ✅ **슬라이스 3 — 티켓 모델 확장 (07-31, 백엔드 전용, 배포·E2E 통과)** — 티켓이
  `creator`/`gameModeId`/`mapId` 대신 `userIds[]`/`gameModeIds[]`/`mapIds[]`를 든다.
  **클라와 게임 서버는 한 줄도 안 바뀌었고 재배포도 안 했다** — 클라는 티켓의 `ticketId`만 쓰고
  로비 서버는 존재만 확인하기 때문(전수 확인). 요청은 여전히 단수로 오고 `matchmaking.service`가
  `[값]`으로 감싼다. **눈에 보이는 변화 0** — 슬라이스 4의 Director가 필요로 하는 저장 모양을
  미리 갖추는 작업이다. 대기방은 후보의 첫 원소를 쓰고 후보가 비면 던진다.
  E2E 증거: 매치가 플레이어 2명 + 라운드 1개로 생성(목록 경로가 끝까지 돎), 취소 흐름 정상.
  plan `2026-07-30-matchmaking-slice3-ticket-model`, spec §8 "슬라이스 3 확정 사항".
- ✅ **슬라이스 4a — 매칭 알고리즘 순수 함수 (07-31, 백엔드 전용, 배포 없음)** — 풀 기반 매칭의 판단
  로직을 `apps/matchmaking-server/src/director/`의 순수 함수로 신설했다: 레이팅 폭 확장, **요구 인원
  선형 감소**, 제안 생성, 제안 선택, 맵 선택. 전부 `now`/`random`을 인자로 받아 DB도 시계도 없이 테스트된다
  (73 tests). **부르는 곳이 없어 동작 변화 0**이고 그래서 배포도 안 했다 — 4b가 Director를 세울 때 나간다.
  slice 4는 규모(삭제만 17파일 731줄) 때문에 4a/4b로 쪼갰다.
  plan `2026-07-31-matchmaking-slice4a-algorithm`, spec §8 "슬라이스 4 분할"·"§6-2 정정"·"§6-2 재정정".

  **최종 리뷰가 잡은 것 — 태스크 단위 리뷰가 구조적으로 볼 수 없던 결함.** 다섯 함수가 각각은 맞는데
  **합치면 큐가 영구히 막혔다**: 아무와도 실력이 안 맞는 티켓이 시간이 갈수록 최고참이 되어 묶음의
  기준점을 영구 점유하고, 기준점을 바꿔 재시도하는 단계가 없었다(폭 완화엔 상한이 있어 큰 격차는 영원히
  안 좁혀진다). 랭크전은 후보 목록이 비어 있어 **큐의 모든 게임이 동시에 막혔고**, 이는 대체하려던 대기방
  방식보다 나빴다. 원인은 내가 spec에 쓴 "게임당 제안 1개"였다 — Open Match 문서 확인 결과 MatchFunction은
  **매치들(복수)** 을 내고 충돌은 정상이며 Evaluator가 정리하는 것이 표준이고, 좁게 쓴 탓에 Evaluator가
  고를 대안이 아예 없었다. 제안을 반복 생성하도록 고치고 spec §6-2를 재정정했다.
  **합성 테스트가 없어서 못 봤다** — 그래서 함께 신설했다.
- ✅ **슬라이스 4b — Director 전환 (07-31, 백엔드 전용, 배포 완료)** — 매칭이 실제로 바뀌었다.
  요청은 **티켓만** 만들고, 별도 **Director** 프로세스(매칭 서버와 **같은 이미지의 두 번째 진입점**
  `dist/director.js`, k8s replica 1 + `strategy: Recreate`)가 1초마다 티켓 풀 전체를 보고 매치를 만든다.
  `WaitingRoom` 16파일 701줄 + `Updater` 삭제, 테이블 DROP. 로비 자가치유는 티켓 존재만 본다.
  티켓 요청 검증(큐/게임/맵/정원)으로 "조용히 영원히 대기"를 막고, Casual 최대 대기는 30초→**10초**
  (지금까지 코드에 `5`가 하드코딩돼 이 값이 쓰인 적이 없다). 154 tests / 19 suites.
  plan `2026-07-31-matchmaking-slice4b-director-transition`, spec §8 "슬라이스 4b 확정 사항".

  **⭐ 최종 리뷰가 잡은 Critical — 태스크 리뷰 9개가 전부 통과한 뒤에.** 매치가 확정되면
  `트랜잭션(매치 생성 + 티켓 삭제) → 룸 생성 → 유저 위치 갱신` 순인데, **룸 생성은 게임서버 Pod까지
  띄우느라 수백 ms**가 걸린다. 그 구간 내내 유저는 "위치=대기 중인데 티켓은 없음"이고,
  대기 화면 클라는 **1초마다** 위치를 조회하며 그 조회 경로가 로비 자가치유를 돌려 위치를
  `None`으로 **영구 저장**한다. 결과: **클라는 로비 화면인데 매치·게임서버는 그 사람을 기다린다.**
  매치당 기대 손실 ≈ 인원 × 창(초) — 예외가 아니라 일상 경로다.
  **원인은 내 설계 판단**이었다. 옛 대기방 코드는 티켓 삭제를 *룸 생성 뒤에* 해서 창이 수십 ms였는데,
  "한 명이 두 매치에 들어가는 것"을 막으려고 삭제를 트랜잭션 안으로 옮기면서 창이 열 배 넓어졌다.
  하나를 막고 다른 하나를 열었다.

  **⭐⭐ 그 수정이 다시 Critical을 낳았고, 재리뷰가 잡았다.** 티켓을 지우지 않고 `matchId`로
  **소비 표시**하도록 고쳤더니(창 = 0), 이번엔 **취소와 확정의 경합**이 열렸다. 취소가 티켓을 읽고
  HTTP 두 번을 거친 뒤 **낡은 스냅샷으로 판정해 무조건 삭제**했기 때문이다. 세 갈래 중 최악은
  *취소 → 위치 None → 즉시 재큐잉 → 새 티켓이 풀에 남은 채 Director가 룸A로 보냄 → 다음 틱에 룸B* =
  **한 명이 두 매치**. 근본 원인은 배열형 `$transaction`이 **조건부 중단을 표현할 수 없다**는 것이었다
  (선점 개수를 확인할 방법이 없다). 인터랙티브 트랜잭션 + **CAS**로 바꿔 닫았다:
  제안이 지목한 티켓을 전부 선점하지 못하면 롤백하고 그 제안만 버린다.

  **CAS 검증이 이 슬라이스에서 가장 중요한 확인이었다** — 개수 비교를 잘못하면 *정상 매칭이 전부
  롤백되어 매칭이 통째로 멈춘다*(고치려던 버그보다 나쁘다). 리뷰가 실제 코드 + 인메모리 테이블
  하네스로 6개 정상 케이스를 실증하고, 의존 불변식(유저당 1티켓 필터가 큐 분할 *전* 전역 적용되어
  선택된 제안들이 유저 단위로도 서로소)까지 증명했다.

  배포 실측: 마이그레이션 2건 적용(테이블 DROP + 컬럼 ADD), **유저 전적 보존**, Director 기동 확인.
  배포 순서가 강제된다 — **인프라(Director Deployment)를 먼저 push**해야 한다. 반대로 하면
  새 매칭 서버가 티켓만 만들고 Director가 없어 **조용한 전면 장애**가 된다.

- ✅ **슬라이스 5 — 개명 (07-31, 3레포, 배포·E2E 통과)** — 대기방 시절의 *이름*을 걷어냈다.
  `Location.WaitingRoom` → `Matchmaking`(**정수값 1 유지 → 와이어 불변**), `WaitingRoomLocationDetail` →
  `MatchmakingLocationDetail`(죽은 `waitingRoomId` 제거), FSM 상태 `InWaitingRoom` → `InMatchmaking`,
  `MatchEvent.LocationIsWaitingRoom` → `LocationIsMatchmaking`, 죽은 응답 코드 삭제(클라·게임서버).
  DB는 `ALTER TYPE "Location" RENAME VALUE`로 라벨만 바꿔 **행 재작성 0**.
  plan `2026-07-31-matchmaking-slice5-rename`, spec §8 "슬라이스 5 확정 사항".

  **spec 자기모순을 정정하고 시작했다.** §7은 `MatchmakingViewModel` 하드코딩 제거를 이 슬라이스에
  넣어 뒀는데, 같은 spec §11-E가 큐·게임·맵 **선택 UI를 별도 프로젝트**로 빼 뒀다. 하드코딩을 없앤다는
  건 곧 그 UI를 만든다는 뜻이라 개명 슬라이스에 들어올 수 없다 — 순수 개명으로 한정했다.
  상태 이름도 §7의 `Matchmaking` 대신 **`InMatchmaking`**(형제 `InGameRoom`과 `In*` 관용).

  **Unity 컴파일 검증이 이 슬라이스의 진짜 관문이었다** — 에디터가 워크트리가 아니라 main 체크아웃을
  보기 때문에 작업 중에는 컴파일러가 없고 grep이 유일한 안전망이다. 머지 후 UnityMCP로 클·서 각각
  force refresh + compile → **양쪽 에러 0** 확인.

  **최종 교차 저장소 리뷰가 내 인식을 하나 정정했다**: 같은 개념이 "5곳에 복제"라고 봤으나 게임 서버는
  `Location`을 **아예 정의하지 않는다**(가진 건 죽은 응답 코드뿐). 실제로는 4곳 + DB다. 그리고 백엔드
  3앱의 `user-location.interface.ts`는 **blob 해시가 동일**(byte-identical)이라 drift가 *없어 보이는* 게
  아니라 **없음이 증명**됐다.

  배포는 **`app: all` 필수** — `db-migrate`가 빠지면 DB는 옛 라벨인데 새 코드가 새 라벨을 써서
  유저 위치 갱신이 전부 실패한다(4b의 "인프라 먼저"와 같은, 순서 하나로 전면 장애가 되는 지점).

---

## 🏁 매치메이킹 표준화 트랙 종결 (슬라이스 1~5, 2026-07-27 ~ 07-31)

먼저 온 사람이 조건을 정하던 **대기방 방식**에서 전체 풀을 보고 결정하는 **Director 방식**으로 옮겼고,
어휘·데이터 모델·마스터데이터 진실원본까지 함께 정리했다. spec `2026-07-27-matchmaking-standardization-design.md`.

**이 트랙이 남긴 방법론적 교훈 — 세 번 반복됐다.** 슬라이스 4a·4b(두 번)에서, **태스크 단위 리뷰가 전부
통과한 뒤 최종 whole-branch 리뷰가 Critical을 잡았다.** 세 번 다 개별 파일은 옳은데 *합쳐 놓으면* 깨지는
종류였다(큐 영구 봉쇄 / 룸 생성 창에 유저 이탈 / 취소·확정 경합으로 이중 배치). 그리고 그중 두 번은
**앞선 수정이 만든 결함**이었다 — 하나를 막으면서 다른 하나를 열었다. 이 프로젝트에서 최종 리뷰 단계는
형식이 아니라 실제로 값을 한다.

**트랙이 남긴 후속 (슬라이스 4b 표 + 아래):**

| | 항목 | 왜 |
|---|---|---|
| ✅ | ~~**DB 통합 테스트가 하나도 없다**~~ | **해소(08-03)** — 아래 "확정·취소 경합 DB 통합 테스트" 항목 |
| ✅ | ~~**`user-location.interface.ts` 3중 복제**~~ | **해소(08-01)** — 아래 `@lop/server-core` 항목 |
| ✅ | ~~**버려진 티켓이 영원히 남는다**~~ | **해소(08-06)** — 아래 "버려진 대기표 만료" 항목 |
| ✅ | ~~**MongoDB를 연결만 하고 안 쓴다**~~ | **해소(08-05)** — 아래 "MongoDB 제거" 항목 |
| ✅ | ~~**`ResponseCode` 5중 복제**~~ | **해소(08-05)** — 아래 "ResponseCode 통합" 항목. 5 → 2(언어당 1) |
| 🟡 | **로비 선택 UI** | 큐·게임·맵 선택 화면(spec §11-E). `MatchmakingViewModel` 하드코딩은 이때 사라진다 |
| 🟠 | **유저 위치 전반 재정비 (백엔드+클라)** | 아래 별도 항목 — 사용자 지시로 추가(08-04) |

### ✅ 캐시 계층(`CacheCrudRepository` + `DaoRedisBase`) 정리 — 백엔드 공용 (2026-08-05, 배포 통과)

B 슬라이스(로비 자가치유) 최종 리뷰가 짚은 둘. **이번 브랜치가 만든 게 아니라 공용 패키지의 기존
동작**이고 고치면 세 앱 전부에 영향이 가서 별건으로 뺐던 것(2026-08-04, 사용자 결정) — 해소.
백엔드 머지 `8eef2ad`.

| 항목 | 무엇이 틀렸나 | 고친 것 |
|---|---|---|
| **쓰기 순서** | `save()`가 캐시를 DB 쓰기 *전에* 지웠다. 그 사이 조회가 **아직 안 바뀐 DB 값을 캐시에 되살린다.** 삭제 경로도 같은 순서(지우는 도중 조회가 아직 살아 있는 행을 되살린다) | **DB 먼저, 캐시 무효화는 그다음** — save/saveAll/delete 4종 전부 |
| **슬라이딩 TTL** | `findById`/`findAll`/`findAllById`가 읽을 때마다 `expire`를 다시 걸어, 1초 폴링 키는 **영원히 만료되지 않는다** | **TTL은 쓸 때만.** 읽기에서 연장 제거 |

**둘은 하나만 고치면 안 되는 짝이었다.** 순서만 고치면 창이 좁아질 뿐 여전히 남고(cache-aside의
원리적 잔여 창), TTL만 고치면 5분마다 자정될 뿐이다. **순서가 창을 줄이고 TTL이 그 잔여를 받아
주는 것**이 표준 cache-aside다. 그래서 두 파일 주석이 서로를 가리킨다.

**왜 지금 아팠나:** B 이전엔 조회가 끝날 때마다 `save()`가 캐시를 지워 낡은 항목이 한 폴링 이상
살아남지 못했다(대신 그 쓰기가 lost-update를 만들었다 — 그게 B가 고친 것). B가 읽기 쓰기를 없앤
뒤로 **낡은 항목이 무기한 남을 수 있게 됐다.** 결과가 오래간다: 캐시에 `None`이 박히면 자가치유가
"멀쩡하다"고 판단해 아무것도 안 고치고, 그 유저는 방이 닫힐 때까지 로비에 남는다.

**함께 지운 것:** `RoomDaoRedis.expire` — 호출자가 0인 죽은 메서드였고, 슬라이딩을 되살릴 수 있는
남은 통로였다.

**⚠️ 되돌리다 드러난 것 — "무조건 무효화"는 회귀였다.** id 없는 생성(create)은 원래 캐시를 아예
안 건드렸는데 무조건 호출로 바꿨더니, **Redis에 연결조차 하지 않는 인증 통합 테스트 11개가
`The client is closed`로 깨졌다.** 새 id로 캐시된 것이 있을 수 없으니 원래 동작이 옳다 — 기존
테스트가 잡아 준 것. (`packages/server-core`엔 Redis 하니스가 없어 새 테스트는 lobby-server
통합에 뒀다. root-is-light 가드를 matchmaking에 둔 것과 같은 이유.)

**검증:** 빌드 5/5 · 유닛 202 · 통합 lobby 23 + matchmaking 14 · CI · 배포 후 파드 4개 정상.
**두 테스트 모두 일부러 되돌려 실제로 실패하는 것을 확인**했다 — 순서를 되돌리니 옛 값
`Matchmaking(1)`이 새 값 `GameRoom(2)`을 가렸고, 슬라이딩을 되살리니 TTL이 `300`에서 꿈쩍 않았다.

**후속(같은 날 처리):** TTL이 *실제* staleness 상한이 되자 "그 상한을 감수할 값인가"를 대상별로
따져 보게 됐고, 그 결과가 아래 항목이다.

### ✅ 캐시 기준 정립 + 위치·티켓 캐시 제거 (2026-08-05, 배포 통과)

위 수정으로 "최악 5분 낡을 수 있다"가 눈에 보이게 되자 **애초에 캐시할 값인지**를 되물었고,
사용자 제안("신선도가 중요하면 차라리 캐시를 안 쓰는 건 어떤가")에서 출발해 전 대상을 재검토했다.
백엔드 머지 `6f3ab76`.

**기준을 세웠다:**

> 캐시는 **① 한 키를 여러 곳이 동시에 읽고 ② 그 값의 *모든* 쓰기가 저장소를 거칠 때**만 쓴다.

| 대상 | ① 팬아웃 | ② 쓰기가 전부 저장소 경유 | 결정 |
|---|---|---|---|
| 유저 위치 | ❌ 유저당 자기 것 1개 | ✅ | **제거** |
| 매칭 티켓 | ❌ | ❌ **우회 쓰기 있음** | **제거** |
| 방(Room) | ✅ 한 방에 N명이 같은 키를 1초마다 | ✅ 직접 쓰는 코드 0 | 유지 (TTL 10초) |
| 유저·프로필·스탯 | ❌ | ✅ | 유지 (폴링 대상 아님, 위험·이득 모두 작음) |

**🔴 티켓 캐시는 이미 오염돼 있었다.** 티켓의 소비 표시(`matchId`)는 **매치 저장 트랜잭션 안에서
티켓 테이블에 직접** 찍힌다(`tx.matchmakingTicket.updateMany`) — 저장소를 안 거치므로 캐시가
찢기지 않는다. 그래서 정작 중요한 조회들이 **전부 캐시를 우회하도록** 짜여 있었다
(`findByIdBypassingCache`, 주석까지 그 이유를 적어 둔 채로). **모두가 피해 다니는 캐시는
이득 없이 위험만 남긴다.**

**위치는 캐시가 아끼는 게 거의 없었다.** 읽기 경로(`isStale`)가 위치 근거를 확인하려고
**매 폴링마다 이웃 서버에 HTTP를 부른다** — 그것도 캐시 뒤가 아니라 앞에서. 즉 아끼는 건
"기본키로 행 하나 읽기" 하나뿐인데 그 옆에 서버 간 왕복이 통째로 붙어 있었다.

**우회하려고 만든 코드가 함께 사라졌다 — 326줄 삭제 / 89줄 추가.**
`findByIdBypassingCache` 2개, 직접 캐시 비우기 3곳, Redis DAO 2개. 겸사겸사 두 앱에 바이트
동일하게 복제돼 있던 `CrudRepositoryBase`를 공용 패키지로 올렸다(room-server 사본은 **아무도
안 쓰던 죽은 코드**였다).

**검증:** 빌드 5/5 · 유닛 200 · 통합 lobby 23 + matchmaking 15 · CI · 배포 후 파드 4개 정상.
새 통합 테스트(소비 표시가 곧바로 보이는가)를 넣고 **캐시를 되살려 실제로 실패하는 것을 확인**했다.

**지금 상태:** 위치·티켓 = 낡음 **0** / 방 = 최대 10초 / 유저·프로필·스탯 = 최대 5분.

### 🟠 유저 위치(UserLocation) 전반 재정비 — 백엔드 + Unity 클라 (**1·2순위 완료, 2026-08-15**)

**왜 트랙으로 묶나:** 유저 위치는 사실상 **세션 상태**(이 사람이 지금 어디 있나)인데, 그 진실을
**아무도 소유하지 않고 여러 곳이 폴링해서 각자 해석**하고 있었다. 경합·재접속 루프·쓰기 부하가 전부
같은 뿌리에서 나온다.

> **진행 상황 (2026-08-15):** 클라의 "조회 주인 없음"이 닫혔고(아래 `f6a27f0`), 이어서
> **1순위였던 "매치 종료 시 위치 정리"도 닫혔다** — 아래
> [매치 종료 시 유저 위치 정리](#-매치-종료-시-유저-위치-정리-2026-08-15-3레포-머지) 참조.
> **남은 것 = push 하나뿐이다.** `locationDetail` 타입 강화는 08-23 완료, 해석 일원화는 같은 날 강등
> (조사해보니 "같은 판단의 사본"이 아니라 서로 다른 질문 넷이었다 — 아래 표 참조).
> ⚠️ **위치 TTL(액티브 정리기)은 남은 일감이 아니다** — 같은 날 "지금은 안 짓는다"로 판단이 바뀌었다.
> 근거와 착수 트리거 4개는 아래 [lazy 보정 절](#-유저-위치는-왜-lazy-보정으로-충분한가--액티브-정리기의-착수-조건).

#### 백엔드 쪽

| | 상태 |
|---|---|
| ~~**정리 책임이 없다**~~ — 매치가 끝나도 위치가 `GameRoom`으로 남아 로비 진입 즉시 같은 게임에 자동 재접속 | ✅ **해소(08-15)** — 원인은 "누가 지우나"가 아니라 **"끝났다는 사실이 느린 파드 삭제 뒤에야 DB에 박히는 것"** 이었다. 아래 절 참조 |
| **만료(액티브 정리)가 없다** — 위치가 *읽힐 때만* 고쳐진다(lazy read-repair). 그 전까지 DB에는 낡은 값이 남는다 | 🔵 **의도적 보류 — 조건부**. 읽기 경로가 하나뿐이라 틀린 값을 관찰할 방법이 없고, Redis식 "액티브"의 동기(자원 누적)가 우리에겐 없다. 근거·착수 트리거 4개는 아래 [lazy 보정 절](#-유저-위치는-왜-lazy-보정으로-충분한가--액티브-정리기의-착수-조건). 티켓 쪽은 08-06 버려진 대기표 만료로 별도 해소 |
| **타입이 약하다** — `Location` enum + `locationDetail` **JSON**. 어느 detail이 오는지는 코드 규약일 뿐이고 클·서가 각자 정의한다 | ⬜ 남음 (계약 변경이라 클·서 동시) |
| ~~폴링마다 쓴다~~ — 조회할 때마다 행 전체를 갱신 | ✅ **해소(08-04, 로비 자가치유 lost-update 슬라이스)** — 바뀐 게 없으면 아무것도 안 쓴다 |

**이 트랙에 속하는 흩어진 항목들** (다른 절에 적혀 있던 것) — 아래 "다음에 할 것"에 값어치 순으로 편입됨.

#### Unity 클라 쪽

| | 상태 |
|---|---|
| ~~폴링이 흩어져 있다~~ | ✅ **닫힘(08-15)** — `UserLocationService` 하나가 조회·재시도·폴링을 소유. `WebAPI.GetUserLocation` 호출 지점 **1곳** |
| ~~**해석이 흩어져 있다**~~ — 위치→다음 행동 `switch`가 상태마다 따로 | 🔵 **강등(08-23).** 흩어진 게 아니라 **각자 다른 질문**이었다(어느 상태여야 하나 / 벗어났나 / 폴링하나 / 게임방인가). 근거는 아래 "다음에 할 것" 표 |
| **밀어주는 경로가 없다** — 전부 pull. 서버가 "너 이제 게임방이야"를 알려줄 수단이 없어 1초 폴링으로 때운다 | ⬜ 백엔드가 push를 줘야 가능. 생기면 `UserLocationService` **내부만** 바뀌고 소비자는 무변경 |

> ⚠️ 옛 서술에 있던 `CheckUserComponent`는 **존재하지 않는다**(Slice A에서 삭제된 `CheckLocationComponent`를
> 가리킨 것). 실제 조회 지점은 부팅 1회 + `CheckMatch` + `InMatchmaking` 셋이었고 지금은 서비스 1곳이다.

#### 다음에 할 것 (값어치 순)

> 판단 기준은 **"지금 유저가 실제로 겪는가"** 다. 🟠는 겪는 것, 🟡·⬜는 아직 아무도 안 아픈 것.

**완료:** ~~매치 종료 시 위치 정리~~ ✅(08-15) · ~~게임서버 자가 종료~~ ✅(08-15) — 각각 아래 절.

| | 항목 | 실제로 벌어지는 장면 |
|---|---|---|
| ✅ | ~~**"매칭 실패" 알림**~~ | **완료(08-16, 2레포 배포·인게임 검증)** — 아래 [매칭 실패 안내](#-매칭-실패-안내-2026-08-16-2레포-배포인게임-검증) 절 참조 |
| ✅ | ~~**위치 일괄 정리가 무조건 쓰기**~~ | **완료(08-16, 배포·인게임 회귀 확인)** — 아래 [매치 종료 정리를 방 조건부로](#-매치-종료-정리를-방-조건부로-2026-08-16-배포인게임-확인) 절 참조 |
| 🔵 | ~~**매치 생성 경로의 원자성**~~ | **강등(2026-08-23)** — *"그 사람은 매칭도 게임도 못 한다"* 는 서술이 **낡았다.** 코드를 따라가 보니 `assignProposal`의 `finally`가 어느 경로로든 티켓을 지우고, 로비 자가치유(`isStale`)가 *"티켓이 없으면 낡음"* 으로 위치를 풀어준다 → 로비 복귀. 위치가 `GameRoom`으로 넘어간 뒤 실패해도 방이 자가 종료(08-15)돼 같은 경로로 풀린다. **아무도 갇히지 않는다** — 남는 대가는 "드물게 매치 한 판 무산"뿐이고, 그건 분산 트랜잭션 없이 받아들이기로 한 값이다. 이 줄은 자가치유(08-04)·방 자가 종료(08-15)·`assignProposal` 순서 재설계보다 **먼저 적힌 문장**이었다 `[[deferred-item-motivation-decay]]` |
| ✅ | ~~**확정 실패를 말없이 넘긴다**~~ | **완료(2026-08-23, 배포·실패 유도 검증)** — 위 조사 중 발견. 아래 [매칭 종료 사유별 안내](#-매칭-종료-사유별-안내-2026-08-23-배포실패-유도-검증) 절 참조 |
| 🔵 | ~~**클라 해석 일원화**~~ | **강등(2026-08-23, 코드 확인)** — *"같은 판단이 흩어져 있다"* 가 **사실이 아니었다.** 네 곳이 각자 **다른 질문**을 한다: `CheckMatch`="지금 어느 상태여야 하나"(3종 전부 매핑) · `InMatchmaking`="대기를 벗어났나"(`Matchmaking`은 *머문다*는 세 번째 결과가 필요) · `UserLocationService`="폴링 돌려야 하나" · `MatchLoadingViewModel`="게임방인가"(bool 한 줄). **겹치는 곳에선 어긋나지도 않는다** — `GameRoom`은 넷 다 같고, `None`·모르는 값은 전부 "매칭 아님"이다. 공용 해석기를 만들면 넷 중 하나만 쓰고(`CheckMatch`), 하나는 결과 종류가 모자라 못 쓰고, 둘은 한 줄이 함수 호출로 길어진다 — **억지 추상화**다. **재개 조건: 한 위치를 여러 곳이 *같은* 방식으로 해석해야 하는 규칙이 실제로 생길 때** |
| ✅ | ~~**`locationDetail` 타입 강화**~~ | **완료(2026-08-23, 배포·실플레이 검증)** — 아래 [locationDetail 계약 강화](#-locationdetail-계약-강화-2026-08-23-배포실플레이-검증) 절 참조 |
| ⬜ | **서버 push** | 전부 pull이라 대기 내내 1초 폴링. 백엔드가 push를 주면 `UserLocationService` **내부만** 바뀌고 소비자는 무변경(08-14 정리의 이득) |
| 🔵 | **위치 정리기(active reconciler)** | **안 짓는다** — 근거·착수 트리거 4개는 아래 [lazy 보정 절](#-유저-위치는-왜-lazy-보정으로-충분한가--액티브-정리기의-착수-조건) |
| 🔵 | **대기표 생존신호를 Redis+TTL로** | **안 짓는다** — 지금은 대기자당 초당 1회를 DB 컬럼(`lastHeartbeat`)에 쓴다. 옮기면 티켓(DB)과 생사(Redis)가 갈라져 **Redis가 잠깐 죽을 때 대기자 전원 사망 판정**이 나고 그 안전장치를 또 설계해야 한다. **재개 조건: 동접이 커져 초당 쓰기가 실제 부담이 될 때.** spec `2026-08-05-abandoned-ticket-expiry-design.md` §3(대안 B)·§13 |

---

### ✅ locationDetail 계약 강화 (2026-08-23, 배포·실플레이 검증)

머지: Backend `fba6a7c` · Client `42058f4`.

**한 문장:** 위치에 딸린 값은 **위치마다 모양이 다른 판별 유니온**인데, 그 규칙을 강제하는 곳이
어디에도 없었다 — 경계에서 막고, 읽을 때 조용히 강등되지 않게 했다.

#### 로드맵 서술 정정

*"자유형식 JSON"* 은 과장이었다. 타입 3종은 클·서 양쪽에 **이미 정의돼 있었다.** 진짜 문제는
**그 정의를 아무도 강제하지 않는 것**이었다:

| 어디 | 무엇 |
|---|---|
| `UpdateUserLocationDto` | `@ValidateNested`가 **주석 처리** + `UserLocationDto`에 데코레이터 0개 → 배열 안이 무검증 |
| 매퍼 `toDomain` | **어느 필드가 있는지**로 타입을 골랐다 → `location=GameRoom` 행에 매칭 필드가 섞이면 안 맞는 객체가 생긴다 |
| `isStale` | `as MatchmakingLocationDetail` 무검사 캐스팅 → `undefined`로 티켓 조회 → **조용히 "낡음"** → 멀쩡한 유저가 로비로 |
| 클라 `Deserialize` | switch 빗나가거나 `catch`가 삼키면 베이스 타입 → `is NoneLocationDetail`이 **조용히 실패**(팝업 안 뜸) |

**왜 `@ValidateNested`가 꺼져 있었나:** 미들웨어가 `whitelist + forbidNonWhitelisted`로 돈다 —
**데코레이터 없는 속성은 "허용 안 된 필드"라 400**이다. 데코레이터가 하나도 없으니 켜는 순간
모든 위치 갱신이 죽는다. 그래서 켜지 못하고 주석 처리한 것이다. `[[commented-out-code-is-a-question]]`

#### 잠긴 결정

| 결정 | 근거 |
|---|---|
| **태그 + 변형별 데이터(판별 유니온) 유지** | Kubernetes API 컨벤션이 union/oneof에 discriminator를 처방한다. 대안(평평하게 + nullable)은 *설정/미설정/null* 3상태를 만들어 더 나쁘다 |
| **비정형 딕셔너리로 안 간다** | 자유형식이 맞는 조건 셋(변형이 열려 있다 / 양끝을 내가 소유하지 않는다 / 내용으로 판단하지 않는다)을 **모두 어긋난다** — 특히 `isStale`이 그 값으로 유저를 로비로 끌어낼지 **결정**한다 |
| **안쪽 태그를 없애지 않는다** | 표준은 태그가 body와 함께 있는 쪽이다. **바깥 `location`이 오히려 사본**(쿼리용 DB 컬럼)이다. 대신 **둘이 같은지 강제**한다 |
| **Zod로 안 갈아탄다** | 세 앱의 모든 DTO가 class-validator다. 한 필드 때문에 검증 체계를 둘로 만들지 않는다 |
| **DB 컬럼은 JSON 유지** | 계약은 경계에서 지키면 된다. 컬럼 변경은 마이그레이션 비용만 늘린다 |

#### ⚠️ 도구의 함정 — 하마터면 "검증한다"고 착각할 뻔했다

`discriminator`의 `subTypes[].name`은 태그 값과 **엄격 비교(`===`)** 된다.
`Location`이 숫자 enum이라 `String(Location.Matchmaking)`으로 감싸면 `"1" === 1`이 거짓이 되어
**조용히 베이스 타입으로 떨어지고 검증이 통째로 무력해진다.** 처음에 그렇게 짰고 **테스트가 잡았다** —
안 그랬으면 "검증을 켰다"고 배포해놓고 실제로는 아무것도 안 막고 있었을 것이다.

모르는 태그 값도 discriminator가 스스로 거절하지 못한다([class-validator#1033](https://github.com/typestack/class-validator/issues/1033)) —
베이스의 `@IsEnum(Location)`이 그 구멍을 메운다.

#### 검증

- DTO 단위 테스트 10개: **실제로 오는 페이로드 3종이 통과**(이게 깨지면 매칭이 400으로 멈춘다) + 어긋난 5종 거절
- 유닛 293 + 통합 122 전부 통과
- 배포 전 기존 데이터 스캔: **계약 위반 0건**
- 배포 후 실매칭: `match created ... players: 2` → 게임 → 로비 복귀. **400·매퍼 경고 0건**

> 첫 시도에서 로딩창이 로비로 되돌아왔는데 **이 변경과 무관**했다 — 게임서버 이미지(`fb81eca`,
> 665MB)가 이 노드에 처음이라 **pull에 1분**이 걸렸고 그동안 클라가 `joinable`만 폴링하다 포기했다.
> 이미지가 캐시된 뒤 재시도해 정상 통과. **k8s 이벤트(`Successfully pulled ... in 1m0.5s`)가
> 결정타였다** — 로그만 봤으면 우리 코드를 의심했을 것이다.

### ✅ 매칭 종료 사유별 안내 (2026-08-23, 배포·실패 유도 검증)

머지: Backend `be41159` · Client `9b54914`.

**한 문장:** 매치 확정이 실패하면 유저가 **아무 설명 없이 로비로 튕기고 있었다** — 사유를 달아 안내가 뜨게 했다.

#### 어떻게 발견했나

위 "원자성" 항목이 아직 유효한지 코드로 확인하다 나왔다. 원래 걱정("갇힌다")은 이미 해소돼 있었는데,
**그 옆에 아무도 안 본 구멍**이 있었다:

| 매칭이 끝난 이유 | 전에 유저가 보던 것 |
|---|---|
| 시간 초과 | "상대를 찾지 못했습니다" ✅ |
| **방 생성 실패** | **아무 말 없이 로비** ❌ |

버려진 대기표 경로(`tick.ts`)는 `notifyMatchmakingEnded`를 부르는데, 확정 실패 경로는 `failures`에
로그만 남기고 끝났다. 방금까지 "찾는 중"이던 화면이 말없이 로비로 바뀌니 유저는 자기가 뭘 잘못
눌렀나 싶다.

#### 잠긴 결정

| 결정 | 근거 |
|---|---|
| **사유 이름은 `Internal`** | 기존 `User`/`Timeout`이 PlayFab `CancellationReason`을 따른다. 그 표의 세 번째가 *Requested / Internal / Timeout* 중 **Internal** = "시스템이 내부 사유로 취소" — 정확히 이 경우다 (문서 대조 확인) |
| **`createMatch`가 롤백되면 알리지 않는다** | 티켓 선점 실패 = 그 사이 유저가 취소했다는 뜻이고 티켓이 풀에 남아 **아직 대기 중**이다. 여기서 "끝났다"고 알리면 멀쩡히 줄 선 사람을 끌어낸다. 테스트로 고정 |
| **알림 실패가 원래 예외를 덮지 않는다** | `finally` 안에서 던지면 진짜 원인(룸 생성 실패)이 사라져 원인 추적이 끊긴다 |
| **문구는 사유마다 다르다** | 확정 실패에 "상대를 찾지 못했습니다"는 **거짓말**이다 — 상대는 찾았고 방을 못 만든 것이다 |
| **유저 취소·사유 없음은 안내 안 함** | 취소는 자기가 아는 일이고, 사유 없는 자가치유는 서버도 왜인지 모른다 |

#### 검증 — 실패를 실제로 유도했다

이 경로는 정상 운영에서 절대 안 나온다. **디렉터 파드 안에서만** `room-server-service`를 루프백으로
돌려(`/etc/hosts` 한 줄) 방 생성을 실패시켰다. 룸 서버는 멀쩡히 살아 있고, ArgoCD가 관여하지 않는
컨테이너 내부 파일이라 되돌려지지도 않는다 — **파드 재시작 한 번으로 원복**된다.

- 디렉터 로그: `failed to assign ... connect ECONNREFUSED 127.0.0.1:80`
- DB: 두 유저 모두 `{"location":0,"cancellationReason":3}`, 타임스탬프 `.762`/`.766` (같은 틱)
- `failed to notify` 로그 **없음** = 알림 자체도 성공
- 클라: **"게임 준비에 실패했습니다. 다시 시도해 주세요."** (옛 문구 아님)
- 원복 후 정상 회귀 통과

> ⚠️ **`kubectl scale --replicas=0`은 ArgoCD가 몇 초 만에 되돌린다**(selfHeal). 파드를 지우는 것도
> 이 앱은 부팅이 5초라 창이 너무 짧았다. **의존 대상을 못 찾게 만드는 쪽**이 확실하고 덜 파괴적이다.

### ✅ 매치 종료 정리를 방 조건부로 (2026-08-16, 배포·인게임 확인)

매치가 끝나면 룸서버가 참가자 전원의 위치를 **조건 없이** `None`으로 덮고 있었다. 그 정리가 늦어지는
사이 결과 화면을 지나 **새 매칭을 건 사람**이 있으면, 뒤늦게 도착한 쓰기가 그를 **대기 화면에서 로비로
튕긴다.** 위 "매칭 실패 안내"에서 만든 티켓 조건부 해제의 **쌍둥이**를 방 id로 만들어 닫았다.

| 변경 | 무엇 |
|---|---|
| **로비** | `releaseGameRoomIfRoomMatches` — "`GameRoom`이고 **그 방**일 때만" raw SQL 한 문장 |
| **로비** | `PUT /internal/user/location/game-room-ended` (사유 없음 — 매치 종료는 결과 화면이 따로 있다) |
| **룸서버** | `releasePlayers`가 방 id를 실어 조건부 해제를 부른다. 무조건 덮어쓰기 제거 |
| **클라** | 변경 없음 |

**⚠️ 창이 열리는 조건 — 08-15 기록이 이걸 안 적어놔서 내가 등급을 잘못 매겼다.** 정상 경로에서는
게임서버가 `UpdateRoomStatus(Closed)`를 **await한 뒤에** 클라에 통보하므로(08-15 순서 뒤집기), 정리가
끝난 다음에야 유저가 로비에 도착한다 — 즉 경합이 없다. 창은 **게임서버의 1.5초 타임아웃을 넘길 때만**
열린다: 정리가 느림 → 게임서버가 포기하고 통보 강행 → 로비 자가치유가 위치를 비움 → 유저가 Play →
그제서야 느린 정리가 도착해 덮음. 정상 실측은 44ms였다.
→ 로드맵 표에 🟠(유저가 겪는 것)로 적어뒀던 것은 **과대평가**였다. 실제로는 🟡이 맞다. 사용자가
"일괄 정리가 언제 되길래 이슈가 되냐"고 물어 되짚다가 드러났다. **미룬 항목을 적을 때 "어떤 조건에서
터지는지"를 같이 적지 않으면, 나중에 그 문장만 읽고 과대·과소평가하게 된다.**

**받아들인 판단:** 그래도 고쳤다 — 조건이 좁을 뿐 구조적으로 열려 있고, 배관(조건부 해제 패턴)이
바로 옆에 있어 지금이 가장 쌌다. 클라 변경 0, 백엔드 1레포.

**검증**

| | |
|---|---|
| 통합(실 DB) | 4/4 — 그 방이면 해제 / **새 매칭 중이면 안 덮음** / 다른 방이면 안 덮음 / 방 id 비면 안 함 |
| 룸서버 단위 | 47/47 (신규 2: "옛 무조건 경로 안 씀", "방 id가 반드시 실림") |
| 전체 | 유닛 290 · 통합 11스위트 66 · 빌드 5/5 |
| 인게임 | 매치 종료 → `PUT …/game-room-ended` 200 → **4초 뒤 새 매칭 정상 성립**. 사유가 붙은 행 **0**(팝업 안 뜸) |

통합 테스트는 앞 트랙에서 배운 대로 **`repository.findById()`로 읽기 경로까지 왕복**시켜, 매퍼가 값을
버리는 종류의 실패를 처음부터 막았다.

**부수 발견 — `bump-tags`가 all-or-nothing이다.** 배포 중 우리와 무관한 `db-migrate` 이미지 빌드가
arm64 크로스빌드 중 TLS 끊김(`ERR_ASSERTION`)으로 실패하자, **세 앱 이미지는 레지스트리에 올라갔는데
태그 bump 잡이 통째로 skip**돼 클러스터만 옛 버전으로 남았다. 실패한 잡만 rerun해 해소.
→ 후속 후보: 앱별로 태그를 bump하거나, 무관한 앱의 실패가 나머지를 막지 않게.

---

### ✅ 매칭 실패 안내 (2026-08-16, 2레포 배포·인게임 검증)

대기 화면이 **아무 설명 없이** 닫혀 유저가 *내가 취소한 것*과 *사람이 안 모여 실패한 것*을 구분하지
못하던 문제. spec `2026-08-15-matchmaking-failure-notice-design.md`,
plan `2026-08-15-matchmaking-failure-notice.md`.

**사유를 어디에 남길지가 이 트랙의 전부였다.** 세 후보를 비교해 **출석부 메모(`locationDetail`)** 를
골랐다 — 저장소가 안 늘고(유저당 1행이 이미 있다), 클라가 이미 1초마다 그걸 보고 있어 **추가 조회가
0**이며, 취소 경로는 **이미 위치를 쓰고 있어서** 그 자리에 한 칸만 채우면 됐다.
표준(PlayFab/GameLift)은 *티켓*에 사유를 달지만, 그건 티켓을 안 지우고 남기는 걸 전제하고 그러면
**유일성 잠금**(유저당 티켓 1개, DB 기본키)과 **로비 자가치유** 두 곳이 조용히 깨진다. 값 이름
(`CancellationReason`/`User`/`Timeout`)만 표준에서 그대로 가져왔다.

| 변경 | 무엇 |
|---|---|
| **백엔드** 조건부 해제 | "아직 **이 티켓 때문에** 매칭 중일 때만" 위치를 비우며 사유 기록. **raw SQL 한 문장** |
| **백엔드** 취소 경로 | 무조건 덮어쓰기 → 조건부 해제(`User`). 기존 위험 하나가 덤으로 사라짐 |
| **백엔드** Director | 시간초과로 쓸어담은 티켓의 주인들을 `Timeout` 사유로 **즉시** 해제(전엔 로비가 조회할 때까지 대기) |
| **클라** | VM은 도메인 신호만, 팝업은 코디네이터. **취소·매치 성사엔 안 뜬다** — 서버가 사실을 주므로 클라는 추론하지 않는다 |

**⭐ "테스트가 진짜 경로를 안 지나가면 전부 초록인 채 기능이 죽는다"를 한 브랜치에서 두 번 겪었다.**

1. **Prisma jsonb `path` 필터가 항상 0건.** 단위 테스트 3/3·전체 13/13·타입검사 전부 통과 상태였다.
   원인은 `locationDetail`이 **이중 인코딩**(jsonb 안에 JSON 문자열)이라 top-level을 객체로 오인한 것.
   → `$executeRaw` 한 문장으로 교체. **미검증을 통과시키지 않고 실 DB 통합 테스트를 요구해서** 잡혔다.
2. **읽기 경로 매퍼가 사유를 통째로 버렸다.** `toDomain`이 알려진 필드만 골라 객체를 새로 짓는
   **화이트리스트**라 `cancellationReason`이 소멸 — DB엔 적혔는데 응답 만들기 전에 사라져 팝업이
   영영 안 뜰 상태였다. **태스크별 리뷰 8번이 전부 놓쳤다**: 그 매퍼가 계획의 어느 태스크 파일
   목록에도 없었고(쓰기 경로만 따라갔다), 통합 테스트가 `rawPrisma`로 **행을 직접 읽어 매퍼를
   우회**했기 때문. 최종 교차 리뷰가 잡았고, 회귀 테스트를 **`repository.findById()` 왕복**으로 바꿨다.

**부수 회귀 하나를 우리가 만들었고 CI가 잡았다.** 새 DTO에 `@Type`/`@ValidateNested`를 켰는데,
그 데코레이터는 모듈을 읽는 순간 `reflect-metadata`를 찾는다. 그 전제가 **앱 시작점(`main.ts`)에만**
있고 어디에도 적혀 있지 않아, `main.ts`를 안 거치는 통합 테스트 2개가 import 단계에서 죽었다.
바로 옆 기존 DTO에 같은 데코레이터가 **주석 처리**돼 있었던 것이 그 흔적이었는데(이유 미기록),
리뷰에서 그걸 보고도 *"우리 건 켜져 있으니 더 낫다"* 로만 읽고 **왜 껐는지 묻지 않았다.**
→ jest `setupFiles`로 세 앱에 선언해 순서를 강제하고 이유를 주석으로 남겼다(`fb1173a`).

**인게임 검증 (kind 로컬, 클라 2개)**

| 시나리오 | 결과 |
|---|---|
| ① 시간초과 | ✅ 끝-끝. 쓸어담기 → 해제(`PUT …/matchmaking-ended` 200) → 사유 `Timeout(2)` → **팝업**. 3초 내 |
| ② 취소 | ✅ 같은 자리에 `User(1)`, **팝업 없음**. 대기표·잠금 0으로 정리 |
| ③ 매치 성사 | ✅ 두 유저 `GameRoom`, 사유 남은 행 **0**, 팝업 없음 |
| ④ 늦게 온 해제 | ✅ 옛 티켓 id로 해제 시도 → **`UPDATE 0`**, 위치 그대로. 대조군(현 티켓)은 `1` — 조건이 과하지도 느슨하지도 않음 |

**미룬 것:** 기존 `UpdateUserLocationDto`의 꺼둔 중첩 검증 되살리기 — 이제 판독기가 켜졌으니 가능하지만,
*"지금까지 통과하던 요청이 400으로 거절될 수 있다"* 는 동작 변화라 이 배포에 섞지 않았다. 별도 항목.

---

### ✅ 게임서버 자가 종료 (2026-08-15, 3레포 머지)

게임서버가 **스스로 끝나지 않아** 백엔드가 파드를 지워줘야만 내려가던 문제. 앞 트랙(위 "매치 종료 시
유저 위치 정리") 검증 중 **좀비 파드를 실물로 관측**하면서 나왔다 — 백엔드를 46초 내려둔 사이 매치가
끝났는데 룸 파드가 살아 있었고(`GAMESERVER_PODS=1`), 백엔드가 돌아온 뒤에야 정리됐다. 남는 건 파드
객체만이 아니라 **노드의 hostPort를 실제로 붙든 컨테이너**이고, 풀은 10개뿐이다.
spec `2026-08-15-gameserver-self-shutdown-design.md`, plan `2026-08-15-gameserver-self-shutdown.md`.

**업계 표준 대조에서 나온 항목이다.** Agones는 게임서버가 `SDK.Shutdown()`을 부르고 **스스로 빠진다** —
오케스트레이터가 밖에서 지워주길 기다리지 않는다. 우리는 "밖에서 지워주기"만 있고 "스스로 빠지기"가
없었다.

| 변경 | 무엇 |
|---|---|
| **게임서버** `LOPRoom.CloseRoomAsync` | 통보 뒤 **세션이 다 빠질 때까지 대기**(타임아웃 10초) → `Application.Quit()`. 통지 실패·예상 못 한 예외에도 종료에 도달한다 |
| **백엔드** `gameServerPod.ts` | `restartPolicy: 'Never'`. **없으면 위 변경이 오히려 나쁘다** — kubelet이 되살리고, 되살아난 하트비트가 이미 끝난 방을 "진행 중"으로 되돌린다(`heartbeat()`에 종단 가드가 없다) |
| **백엔드** 파드 GC | 자가 종료로 남은 `Succeeded` 껍데기를 2초 스윕이 바로 수거. **`Failed`(크래시)는 일부러 제외** — 로그 보존이 60초 단축보다 값지다 |

**왜 고정 지연이 아니라 배수인가.** 클라는 결과를 받으면 로비 씬을 로드하며 스스로 나간다 — 즉
"다 나갔다 = 다 받았다"라서 전달을 확인하는 가장 정확한 신호다. 고정 지연으로 끄면 못 받은 클라의
소켓이 죽는데, **클라에는 끊김 처리가 없어**(`onStopClient` 주석 처리) 그 사람은 끝난 방에 갇힌다.

**인게임 검증 (kind 로컬, 클라 2개)**

| 시나리오 | 결과 |
|---|---|
| ① 정상 종료 | ✅ 결과 창 표시·유지. 단 **파드는 백엔드 GC가 1초 만에 수거**(실측 09:52:48→49) — 배수보다 빨라서 자가 종료가 발동하지 않는다. **실패가 아니라 경합**이고, 최종 리뷰가 미리 예측했다 |
| ② 백엔드 죽은 채 종료 | ✅ **`Running`→`Completed` 자가 종료 실증**(09:02:12 Running → 09:02:17 Completed, 5초 간격 8회 샘플). 그 구간엔 파드를 지울 주체가 없으므로 다른 해석이 불가능하다. 결과 창도 정상 표시 |
| ③ 백투백 | ✅ 새 매치 정상 성립 — 직전 매치의 hostPort가 실제로 반납됐다는 증거 |

> ⚠️ **이 기능의 검증은 백엔드를 내린 상태에서만 결정적이다.** 정상 경로에서는 백엔드 GC가 항상 먼저
> 이기므로 `Completed`를 볼 수 없다. 다음에 이 코드를 건드리는 사람이 "자가 종료가 안 도는데?"로
> 오해하기 쉬운 자리다.

**부수 확인**: 하트비트 정지 → 만료 감지(`marked as Error: heartbeat expired`) → 백엔드 복구 후 GC가
껍데기 수거까지 전 사슬 관측. `DeinitializeAsync`는 종료로 건너뛰어지지만 **전부 프로세스 내부 정리라
무해**(외부 부작용 0)함을 최종 리뷰가 확인.

**미룬 것**: 배수 중 신규 접속 미차단(룸이 이미 `Closed`라 매칭이 그리로 보내지 않아 실경로 없음).

---

### 📌 유저 위치는 왜 lazy 보정으로 충분한가 — 액티브 정리기의 착수 조건

**질문**: 유저 위치가 *읽힐 때만* 고쳐진다면, 그 전까지 DB에는 틀린 값이 남는다. 이게 표준인가?
따로 도는 정리기가 있어야 하지 않나? (2026-08-15 검증 중 실제로 관측 — 매치가 끝났는데 위치가
`GameRoom`으로 남아 있었다.)

**답: 지금은 필요 없다. 단, 조건이 명확하다.**

**우리 경우 lazy로 충분한 이유 두 가지:**

1. **틀린 값을 관찰할 방법이 없다.** `UserLocation`을 읽는 경로가 **정확히 하나**
   (`getOrCreateUserLocationById` — `healIfStale` 포함)다. `findAllById`는 쓰기 경로 내부에서만 쓰이고,
   집계·리포팅 소비자가 없다. **읽는 순간 고쳐지므로 틀린 값을 받아가는 코드가 존재하지 않는다.**
2. **실증됨** — DB에 `GameRoom`이 남은 상태에서 매칭을 걸었고 정상 성립했다(08-15). 그 값에 걸려
   막히는 로직이 없다.

**업계 표준은 사실 "둘 다"인데, 액티브를 두는 동기가 우리에겐 없다.** Redis가 이 문제의 교과서다 —
lazy(접근 시 삭제) + active(백그라운드 샘플링)를 함께 쓴다. 그런데 **액티브의 동기는 정확성이 아니라
자원 회수**다: 만료됐는데 다시 접근 안 되는 키가 *메모리를 계속 점유*하기 때문이다.

| | Redis 키 | 우리 UserLocation |
|---|---|---|
| 낡은 값이 쌓이나 | **쌓인다**(만료 키 누적) | **안 쌓인다**(유저당 1행, 제자리 갱신) |
| 자원 비용 | 메모리 증가 | 0 |
| 액티브가 | **필요** | 동기 없음 |

**착수 트리거 — 하나라도 생기면 그때 짓는다:**

1. **치유를 안 거치는 읽기 소비자**가 생길 때 — 어드민 대시보드, "접속 중 N명" 집계, 친구 목록 온라인
   표시. **집계 읽기는 행 단위 치유가 안 먹는다**(전체를 훑는데 각각을 고칠 수 없다). presence 시스템이
   TTL을 쓰는 이유가 정확히 이것이다.
2. **위치 값으로 무언가를 거부**하기 시작할 때 — 예: "게임 중인 유저는 큐에 못 들어감".
3. **방과 무관한 위치 종류**가 생길 때 — 지금 치유는 "방이 죽었나"로 판정한다. 방에 기대지 않는
   상태(관전·튜토리얼 등)가 생기면 판정 근거가 사라진다.
4. **사람이 반복해서 헷갈릴 때** — DB를 직접 보면 틀린 값이 보인다. 정확성 비용이 아니라 **관측·디버깅
   비용**이고, 지금은 감수하지만 이게 반복되면 그 자체가 이유다.

> 출처: [Redis Key Expiration Algorithm (Lazy + Active)](https://oneuptime.com/blog/post/2026-03-31-redis-how-redis-key-expiration-algorithm-works-lazy-active/view) ·
> [Redis expired keys still in memory](https://oneuptime.com/blog/post/2026-03-31-redis-how-redis-handles-expired-keys-still-in-memory/view)

---

### ✅ 매치 종료 시 유저 위치 정리 (2026-08-15, 3레포 머지)

매치가 끝나고 로비로 가면 **방금 끝난 게임으로 도로 끌려가** 결과 창이 부서지던 버그. 유저 위치 트랙의
1순위였고, Slice D 때 임시 스캐폴드로 우회했다가 원복해 둔 자리다.
spec `2026-08-15-match-end-location-release-design.md`, plan `2026-08-15-match-end-location-release.md`.

**원인은 로드맵에 적혀 있던 것과 달랐다.** 로비의 위치 조회 경로엔 **이미 자가치유가 있었다**
(`getOrCreateUserLocationById` → `healIfStale` → 방이 없거나 `Closed`/`Error`면 조건부 쓰기로 위치를 비움).
그러니 재접속이 나려면 *조회 시점에 방이 아직 종단이 아니어야* 한다 — 그리고 `updateRoomStatus`가
**느린 k8s 파드 삭제를 먼저 await한 뒤에야** `room.save(Closed)`를 하고 있었다. 그 창이 버그였다.
즉 고칠 것은 "누가 위치를 지우나"가 아니라 **"끝났다는 사실이 언제 DB에 박히나"** 였다.

| 변경 | 무엇 |
|---|---|
| **백엔드** `room.service.updateRoomStatus` | 상태 저장을 **맨 앞**으로 · 재진입 가드(종단 룸엔 부작용 0) · 위치 일괄 정리는 그 뒤(실패해도 요청 성공) · **파드 삭제를 요청 경로에서 제거**(2초 스윕이 이미 함) |
| **백엔드** 스윕 | 두 관심사로 분리 — **사실 박기**(룸 목록, 하트비트 만료를 `Error`로 전이 저장, 룸당 1회) / **파드 GC**(파드 목록 기준 — 룸 목록으로 돌면 DB에 쌓인 과거 종단 룸 전부를 2초마다 두드린다). 룸별 try/catch로 한 룸의 실패가 GC 패스를 막지 못하게 |
| **게임서버** `LOPRoom` | 통보 순서 뒤집기 — `UpdateRoomStatus(Closed)`를 **await한 뒤** `MatchEndedToC`. 실패·타임아웃이어도 **통보는 강행**. + **매치 종료 시 하트비트 정지**(아래) · 타임아웃 1.5초(스윕 주기 2초 아래) |
| **클라** `RoomConnector` | 응답의 `room.status`가 `Closed`/`Error`면 **즉시 포기**(이전엔 60회×1초 재시도). 부팅 중 거절과 구분해야 해서 응답 코드가 아니라 방 상태로 가른다 |

**최종 리뷰(opus, 3레포 교차)가 실재하는 구멍을 하나 더 잡았다** — `LOPRoom`의 하트비트가 매치 종료 후에도
계속 돌아, `Closed` 쓰기가 실패했는데 **프로세스는 살아 있는** 경우(백엔드 일시 장애 = 크래시보다 흔함)
룸이 영원히 `GameInProgress`로 남고 만료 스윕이 절대 잡지 못했다. **리컨실러는 있는데 그 입력이
거짓말을 하고 있었던 것.** `CancelInvoke("SendHeartbeat")` 한 줄로 닫았다.

**인게임 검증 (kind 로컬, 클라 2개)** — 로드맵의 "인게임 검증 ⑥"을 못 돌린 자리를 이걸로 닫았다.

| 시나리오 | 결과 |
|---|---|
| ① 정상 종료 ×3 | ✅ 결과 창 유지 · 종료 후 `/joinable` 재조회 **0건**(재접속 시도 자체 없음) · 위치 정리(44ms)가 룸 상태 요청(191ms) **안에 중첩** = 통보 전에 이미 풀림 |
| ② 백엔드 쓰기 실패 | ✅ 백엔드가 완전히 죽은 상태에서도 결과 창 표시 · 복구 후 **하트비트 0건**(수정 동작) · 좀비 파드 정리됨 · **20~30초 만에 해소** |
| 클라 가드 | ✅ 2회 실증 — `Room ... already closed (status: Error). Stop retrying.` |
| 스윕 마킹 · 재진입 가드 | ✅ 첫 매치(콜드 pull 실패)에서 실증 |

**부수 관측 (durable):**
- **룸을 `Error`로 박은 건 스윕이 아니라 조회 경로**(`isRoomJoinable`)였다. 리컨실러가 두 겹이고
  더 빠른 쪽이 먼저 잡는다 — 둘 다 정상.
- **클라는 위치 조회에 반복 실패하면 안전하게 Idle로 떨어진다**(`CheckMatch` 설계된 폴백). 백엔드가
  죽어 있어도 로비에 갇히지 않는다.
- **좀비 게임서버 파드** — 백엔드가 죽어 있는 동안 게임서버가 그대로 살아 있었다(위 "다음에 할 것" 2번의 근거).

**미룬 것(각각 근거 있음):** ~~위치 일괄 정리가 무조건 쓰기라 새 위치를 덮을 수 있음~~ **✅ 해소(08-16)** —
위 [매치 종료 정리를 방 조건부로](#-매치-종료-정리를-방-조건부로-2026-08-16-배포인게임-확인) 절.
⚠️ 이때 **"어떤 조건에서 터지는지"를 안 적어둔 탓에** 나중에 이 문장만 읽고 등급을 과대평가했다
(실제 조건: 정리가 게임서버의 1.5초 타임아웃을 넘길 때만) ·
종단 상태가 편도문이라 느린 콜드 스타트 룸이 복구 불가(콜드 pull 첫 매치 실패는
이전부터 알려진 동작) · 재확인 루프에 페이싱 없음 · 포트 재사용 창이 ~2초 넓어짐(기존 문제).

---

## ✅ 백엔드 공용 패키지 `@lop/server-core` (2026-08-01, 배포·E2E 통과)

백엔드 3앱이 **바이트 단위로 복제**하던 26개 파일을 워크스페이스 패키지 하나로 모았다. 목표는
동작 변화 0이고, 값은 "세 벌이 한 벌이 된 것"이다. spec `2026-07-31-backend-server-core-package-design.md`,
plan `2026-07-31-backend-server-core-package.md`.

**착수 근거(실측):** 26개 파일이 3앱에서 완전 동일했고, 이미 갈라지기 시작한 증거가 둘 있었다 —
`redis.loader.ts`가 **세미콜론 하나** 차이, `UserLocationResponseDto`의 `timestamp`가 **받는 쪽에만**
선언돼 있었다(보내는 쪽은 안 보냄 = 타입이 거짓말). 슬라이스 5에서 enum 하나 개명에 손으로 세 번
고치고 git 해시로 일치를 증명해야 했던 것이 직접적 계기다.

**경계는 손으로 정하지 않았다** — 의존 그래프를 계산해 `@config`에 닿는지로 16/10을 갈랐고, 설정은
"인프라(공용, dotenv 포함) / 이웃 서비스 주소(앱별 2줄)"로 나눴다. 앱은 "내가 누구를 부르는가"만 안다.

**리뷰가 잡은 것 넷 — 전부 순수 이동이 *부작용으로* 만든 것들:**

| | 무엇 | 왜 위험했나 |
|---|---|---|
| 🔴 | **CI 타입체크가 의존 패키지를 안 빌드한다** | `pnpm --filter <app> run build`는 워크스페이스 의존을 빌드하지 않는다. 깨끗한 CI 체크아웃엔 `packages/server-core/dist`가 없어 앱 컴파일이 죽고 **이미지 빌드에 도달조차 못 한다.** 로컬 빌드(dist 있음)도 docker build(Dockerfile에 빌드 단계 있음)도 **구조적으로 못 보는 자리**였다 |
| 🟠 | **mongoose가 두 물리 실체로 갈라짐** | 패키지가 `mongoose.connect()`를 런타임에 부르게 되면서, lobby에서 **연결이 열리는 인스턴스와 모델이 등록되는 인스턴스가 달라졌다.** 그 DAO 소비처가 0곳이라 안 터졌을 뿐인 지뢰. 루트 `pnpm.overrides`로 워크스페이스 단일화 |
| 🟠 | `redisClient`가 `any`로 노출 | 공유 패키지의 공개 API라 3앱 전부가 타입 체크를 잃는다. 제네릭 3번째 인자를 채워 실타입 부여 |
| 🟠 | 로그 파일 위치 이동 | `__dirname` 기준이라 패키지로 옮기며 `node_modules` 안쪽으로 갔다. `process.cwd()` 기준으로 |

**⭐ 한 번 되돌린 판단이 다시 옳아진 사례.** T2에서 구현자가 mongoose 버전을 올려 타입 충돌을 고쳤고,
"타입 문제를 런타임 의존 변경으로 고쳤다"는 이유로 **되돌렸다**(그땐 패키지가 타입으로만 썼다).
T4에서 패키지가 mongoose를 **런타임에** 쓰게 되자 같은 조치가 필수가 됐다 — 판단을 뒤집은 게 아니라
**전제가 바뀐 것**이다. 이번엔 `overrides`로 재발까지 막았다.

**검증:** 빌드 5/5, 테스트 165, **로컬 docker 이미지 3종**(로컬 `pnpm build`는 워크스페이스 hoisting이
문제를 가리므로 이미지 빌드가 유일한 증거), 리뷰가 **배포 산출물을 실제로 실행**해 모듈 그래프 해소와
**물리 모듈 중복 0**까지 확인. 배포 후 4개 파드 에러 0.

**남긴 것:**

| | 항목 | 왜 |
|---|---|---|
| ✅ | ~~**배럴에 부수효과가 생겼다**~~ | **대부분 해소(08-01)** — 아래 "자기완결화" 항목 |
| ✅ | ~~`packages/server-core`에 `lua/`가 없다~~ | **해소(08-01)** — lua가 패키지 안으로 들어왔다(덤으로 3앱 복제도 한 벌이 됐다) |
| 🟡 | `skipLibCheck`는 이제 불필요 | 버전 통일 후 꺼도 빌드가 통과함을 확인했다. 일반 안전장치로만 남겨 뒀고 주석에 그렇게 적혀 있다 |
| ✅ | ~~갈라진 계약 정리~~ | **완료(2026-08-23, 배포·실매칭 검증)** — 응답 코드 3앱 통합은 **이미 끝나 있었다**(`ResponseCode`가 앱별 `interfaces/`에 하나도 없다 — 이 줄이 낡았던 것). `timestamp` 거짓말은 **필드만 지운 게 아니라 DTO 자체를 한 벌로 합쳐** 해소했다 — 3벌로 두면 내일 또 갈라지고, 이 유령 필드가 정확히 그렇게 생긴 것이다. `user-location.dto`가 `@lop/server-core/dtos`로 이사(Backend `8cbc95c`). **⚠️ 루트 배럴에 넣었다가 가드 테스트(`server-core-root-is-light`)가 잡았다**: class-transformer의 `@Type`이 데코레이션 시점에 `Reflect.getMetadata`를 불러, 루트에 두면 `@lop/server-core` import만으로 `reflect-metadata`가 강제된다 → **서브패스를 가르는 기준은 "자원을 잡는가"가 아니라 "import가 공짜인가"**. `[[server-core-subpath-exports]]` · 남은 DTO 가족(`user`·`user-rating`·`match`)은 같은 방식이 통함이 증명됐으니 필요할 때 |

### ✅ 자기완결화 후속 (2026-08-01, 배포·E2E 통과)

위 배럴 부수효과를 정석(`exports` 맵으로 분해)으로 못 고치는 상황에서 **접근을 바꿔 해소**했다.
spec §8 "후속 정정", plan `2026-08-01-server-core-self-contained`.

**착수 계기는 사용자 질문이었다** — "이거 우리 프로젝트만 발생하는 이슈야? 업계 표준 확인해줘".
조사해 보니 **네 문제 다 교과서에 있는 것**이고, 우리가 표준을 벗어난 지점은 **하나뿐**이었다:

| 우리가 겪은 것 | 업계 이름 | 표준 |
|---|---|---|
| 배럴 부수효과 | *barrel file* 문제 | `exports` 맵 서브패스. `sideEffects: false`는 **번들러 힌트라 Node CJS엔 안 먹는다** |
| 같은 라이브러리 두 실체 | React "두 사본이 보인다" | `peerDependencies`(단 pnpm에선 알려진 함정) |
| **공용 패키지가 dotenv를 부른다** | **안티패턴** | **dotenv는 앱 진입점에서 한 번. 라이브러리는 읽기만** |
| `exports`가 안 먹는다 | `moduleResolution: node10` 제약 | `node16`/`bundler`. **node10은 TS 7.0에서 제거** |

**핵심 통찰:** 정석은 막혀 있었지만(`moduleResolution`), 다시 보니 실제로 아팠던 것은
"부수효과가 있다"가 아니라 **"부수효과가 자기 발로 못 선다"** 였다 — lua를 *남의* 작업 디렉터리에서
찾고, `LOG_DIR`이 없으면 터졌다. **그것만 고치니 임시 봉합을 걷어내고도 테스트가 통과했다.**

- dotenv를 앱 진입점으로 되돌렸다(진입점은 **넷** — `main.ts` ×3 + `director.ts`). 부수효과 import 소멸
- lua가 패키지 안으로(모듈 기준 읽기). **3앱 복제도 한 벌**이 됐다
- `logger` 재귀 mkdir + `LOG_DIR`/redis 기본값 — **env가 있으면 언제나 env가 이긴다**
- `jest.setup.js` 임시 봉합 제거

**검증:** 빌드 5/5, 154+11, docker 이미지 3종, **이미지 안에 lua가 실제로 실렸는지 실측**,
**환경변수를 통째로 비운 상태(`env -i`)에서 패키지 단독 로드**, 컴파일된 `dist` 4개의 첫 require 순서 확인.

**남은 것:** postgres/mongo 접속 URL은 여전히 로드 시점에 env 없이 조립된다(Prisma가 지연 연결이라
터지진 않는다). redis·logger와 대칭이 안 맞을 뿐 고장은 아니다.

### ✅ `moduleResolution` node10 → node16 (2026-08-01, 배포·E2E 통과)

배럴 정식 분해(`exports` 맵)의 **전제**를 깔았다. spec `2026-08-01-module-resolution-node16-design.md`.

**위험해 보였는데 실제로는 거의 공짜였다.** 착수 전에 그냥 바꿔서 재 봤더니 타입체크는 5개 프로젝트
전부 통과했고 **막는 것은 `dotenv@10`(2021) 하나**뿐이었다 — 그 버전의 `exports` 맵에 `types` 조건이
없어 node16이 타입을 못 찾는다. `^16.5.0`으로 올렸다(`packages/database`가 이미 16.5.0이라 통일 방향).
산출물은 CJS 그대로다(`"type": "module"`이 어디에도 없다).

**리뷰가 확인 수준을 한 단계 올렸다:**
- **산출물을 양쪽 설정으로 각각 컴파일해 바이트 비교** — 5개 프로젝트 전부 동일. `__importDefault`/
  `__importStar`/`exports.default` 같은 interop 표면까지 같다. "CJS 모양이더라"가 아니라 같음의 증명
- **모듈 해석 4600여 쌍 전수 대조** — 차이는 `csv-parse` 하나이고, 그것도 node10이 *타입은 ESM 선언,
  런타임은 CJS*를 보던 불일치를 **교정한** 것이다(두 선언 파일 내용은 동일)
- **`dotenv` v10 파서를 재현해 커밋된 `.env` 6개를 v16과 대조** — 전부 동일하게 파싱

**함께 닫은 함정:** turbo가 `tsconfig.base.json`을 추적하지 않아 **컴파일러 옵션만 바꾸면 캐시가
무효화되지 않고 낡은 산출물이 조용히 재생**됐다(이번에도 첫 빌드에서 패키지 둘이 옛 캐시를 재생했고,
락파일이 같이 바뀐 덕에 우연히 리빌드됐다). `globalDependencies`에 등록하고 실증했다.

## 🏁 매치메이킹 경합 트랙 종결 (B·C, 2026-08-03 ~ 08-04)

spec `2026-08-04-matchmaking-race-fixes-design.md`. **C(대기표 유일성)·B(로비 자가치유) 둘 다 배포·E2E 통과.**
앞선 "확정·취소 경합 DB 통합 테스트"가 이 트랙의 검증 수단이었다.

**세 건 다 같은 모양이었다** — *확인하고 → 그 사이에 세상이 바뀌고 → 옛 판단으로 쓴다.* 그리고 셋 다
**트랜잭션으로 감쌀 수 없었다**(읽기와 쓰기 사이에 HTTP가 낀다 — 잠금을 네트워크 대기 동안 쥐는 건
안티패턴). 조건부 쓰기와 DB 제약이 우회가 아니라 **이 모양의 정석**이라는 게 이 트랙의 결론이다.

### ✅ 로비 자가치유 lost-update (2026-08-04, 배포·E2E 통과) — 경합 트랙 B

게임에 들어가야 할 사람이 로비로 튕기던 문제. 위치 자가치유가 이웃 서비스에 HTTP로 물어보는 사이
남이 쓴 값을 옛 스냅샷으로 덮어썼다. plan `2026-08-04-lobby-selfheal-lost-update.md`, 머지 `5b6b7a8`.

**착수해 보니 범위가 넓었다.** 치유할 게 없어도 **매 조회마다 행 전체를 다시 쓰고 있었다**(아무도 안
읽는 `timestamp` 갱신 때문). 즉 같은 덮어쓰기가 **평범한 경로에서 1초마다** 벌어졌다.

| | 바꾼 것 |
|---|---|
| **쓰기 경로** | 자가치유 제거. 호출자 셋(매칭 요청·취소·Director 확정)이 **전부 자기가 방금 만든 것**을 기록하는데, 되묻는 낭비이고 **이웃이 삐끗하면 확정을 무효화**했다 — 쓰기가 자기를 되돌린다 |
| **읽기 경로** | 바뀐 게 없으면 **아무것도 쓰지 않는다** |
| **치유** | 판본(`timestamp`)이 그대로일 때만 |

부수 효과: 확정 경로에서 HTTP 왕복이 하나 사라져 다른 경합의 창도 줄었고, `timestamp`가
"마지막 조회 시각"(무의미)에서 **"마지막으로 위치가 바뀐 시각"** 이 되어 판본으로 쓸 수 있게 됐다.

**⭐ 최종 리뷰가 "고치려다 더 나쁘게 만든 것"을 잡았다.** 처음 구현은 `locationDetail` **문자열을 통째로**
비교했다. 그 값은 jsonb 안의 JSON 문자열이라 정규화가 없어, **키 순서나 옛 필드 하나만 달라도** 조건이
영영 안 맞는다. 그리고 그 실패는 "남이 썼다"와 **구분되지 않아** 치유를 영구히 포기한다 →
그 유저는 **취소도(티켓 없음) 재매칭도(위치가 Matchmaking) 못 하는 상태에 갇힌다.** DB를 손대야만 풀린다.
리뷰어가 **동시 쓰기 없이도** 재현했다. → 판본 비교로 교체(인코딩 모양과 무관).

함께: 판본이 없으면 조건이 통째로 사라져(Prisma가 `undefined` 필드를 버린다) **무조건 덮어쓰기**가 되던
구멍 차단 · 동시 쓰기를 흉내 내는 테스트가 `timestamp`를 안 올려 **진짜 쓰기를 흉내 내지 못하던** 것 교정.

**lobby-server에 없던 테스트 기반도 신설**했다(Postgres + Redis 컨테이너). 그전까지 테스트가 0개였다.

**다른 머신과 겹쳤다** — auth 트랙이 하필 같은 자리(lobby 하니스·CI 단계)를 만들어 충돌 7건. 합치면서
`globalSetup`은 양쪽 env + Redis, `db.ts`는 양쪽 헬퍼, CI는 한 단계로 이유를 합쳤다. 그리고 auth
마이그레이션이 **이름이 더 앞서는데 나중에 적용**되는 상황이라 컨테이너로 재현해 정상 적용을 확인한 뒤
배포했다(운영에서도 그대로 통과).

**남은 것:** ~~캐시 계층 둘~~ — **해소(08-05, 위 캐시 계층 항목)**.

### ✅ 대기표 유일성 (2026-08-04, 배포·E2E 통과) — 경합 트랙 C

한 유저가 열린 대기표를 두 장 갖는 것을 **DB가 원천 차단**한다.
spec `2026-08-04-matchmaking-race-fixes-design.md`(§4), plan `2026-08-04-matchmaking-ticket-uniqueness.md`,
백엔드 머지 `f59abf7`. **같은 spec의 B(로비 자가치유)는 아직 안 했다.**

**증상이 예상보다 나빴다.** 대기표 두 장이 매칭 함수에서 **서로 다른 사람으로 세어져**
(`playerCount += userIds.length`, 중복 제거는 티켓 id로만) **혼자서 2인 매치가 성립**하고, 상대가
영영 안 오는 게임방이 만들어진다. 실행으로 재현했다 — `제안 수: 1, 티켓: [T1,T2], playerCount: 2`.
> ⚠️ ROADMAP에 있던 *"확정 트랜잭션이 참가자의 다른 티켓까지 소비해 막는다"* 는 **두 장이 서로 다른
> 매치로 갈 때만** 통한다. 한 매치 안에 같이 들어가는 경우는 **아무것도 막지 않았다.**

**해법: 규칙을 기본키로.** `MatchmakingTicketUser.userId`가 기본키인 것 자체가 "한 유저는 열린 티켓
하나"다. **파티 인원 겹침까지 덮는다**(실험 확인). 티켓이 지워지면 cascade로 따라 사라져 **쓰는 곳은
발급 한 군데뿐**이다.

| 왜 이 모양인가 | |
|---|---|
| 조회로 확인 ❌ | 확인과 생성 사이의 틈에 다른 요청이 끼면 **둘 다 정직하게 "없음"을 받는다.** 조회를 더 정확히·자주 해도 안 없어진다 |
| 트랜잭션 ❌ | 확인이 **다른 서비스(HTTP)** 에 있다. 그 상태로 잠금을 쥐는 건 안티패턴 |
| 배열 제약 ❌ | `text[]`엔 gist 연산자 클래스가 없어 겹침 제외 제약을 못 만든다(`btree_gist`도 무효) — **실험 확인** |
| DB 트리거 ❌ | 우회 불가지만 **TypeScript를 읽어도 안 보인다.** 발급 경로가 하나뿐이라 명시적 트랜잭션 채택 |

**중복 요청은 기존 티켓으로 답한다(멱등)** — 버튼을 두 번 눌러도 유저에겐 정상으로 보인다. 반면
**한 티켓 안 중복·빈 인원은 조용히 고치지 않고 에러로 터뜨린다**(사용자 결정) — `null`은 "이미
대기 중"이라는 뜻이라 섞으면 안 된다. 인원 등록 없이 티켓만 만들던 **죽은 메서드 2개는 삭제**했다
(소비처 0, 남기면 보장을 무력화하는 우회로).

**⭐ 최종 리뷰가 배포 정지 위험을 잡았다.** 마이그레이션은 **PreSync로 도는 동안 보호 장치 없는
구버전 앱이 계속 티켓을 만든다.** 정리와 백필이 문마다 다른 스냅샷을 보므로 그 사이 커밋된 티켓이
백필의 기본키를 위반할 수 있고, 그러면 `_prisma_migrations`에 미완료로 남아 **이후 배포가 전부 막힌다**
(사람이 `resolve`를 쳐야 풀림). → 맨 앞에 `LOCK TABLE ... IN SHARE ROW EXCLUSIVE MODE`. 잠금이
INSERT를 대기시키는 것 실측(1.93초).

함께 고친 셋: **소비된 티켓을 먼저 보존**(안 그러면 이미 배정된 사람이 대기 풀에 다시 나타난다;
`createdAt`이 DB 시각이 아니라 앱 시각이라 파드 간 시계 오차로 순서가 뒤집힐 수 있어 시각보다 우선) ·
**빈 인원 거부**(등록이 없으면 유일성 규칙 적용 대상 밖) · **P2002를 전부 `null`로 바꾸지 않기**
(티켓 id 충돌 같은 진짜 오류가 로그 없이 정상 거절처럼 나가던 것).

**마이그레이션 SQL 버그도 하나 나왔다** — 한 티켓의 `userIds` 안에 같은 유저가 두 번 있으면 순번
매기기가 그걸 "티켓 두 장"으로 착각해 **그 유저의 유일한 티켓을 지웠다.** 5티켓 픽스처로 재현·수정.

**남은 것:** 겹침이 사슬로 이어질 때 정리가 필요 이상 지운다(파티 없이는 발생 불가, 주석 있음) ·
`CreateMatchmakingTicketDto`와 그 매퍼가 서로만 참조하는 죽은 코드로 남음 · 통합 테스트 헬퍼
`createTicket()`이 인원 등록을 우회한다(레거시 안전망 검증용이라 의도적).

### ✅ 확정·취소 경합 DB 통합 테스트 (2026-08-03, 배포·CI 통과)

매치 확정과 취소가 같은 순간에 부딪히는 상황을 **진짜 Postgres로 자동 재현**하는 테스트를 넣었다.
spec/plan `2026-08-03-matchmaking-db-integration-tests*`, 백엔드 머지 `5c33e7e`(+ CI `635de55`).
**런타임 코드 0줄 변경.**

**이건 고치는 작업이 아니라 고정하는 작업이었다.** 착수 전에 손으로 재현해 보니 주장이 **성립했다** —
확정이 선점한 채 커밋 전이면 취소가 **1.1초 실제로 블록됐다가** 조건을 다시 보고 0건이 되고, 반대로
취소가 먼저 커밋되면 확정이 매치·라운드까지 통째로 롤백하고 애꿎은 티켓은 풀에 남았다. 문제는 그게
**주석에 적힌 논증일 뿐 실행된 적이 없었다**는 것이다.

**하니스:** `@testcontainers/postgresql`(일회용 DB, 기동 ~8초) + `prisma migrate deploy`(운영과 같은
경로) + 모듈 로드 전에 env를 맞춰 **실제 DAO 코드**가 그 컨테이너를 보게 함. 유닛과 섞이지 않게
jest 설정·스크립트 분리(`test:integration`).

| 금기 | 왜 |
|---|---|
| "테스트를 트랜잭션으로 감싸 롤백"식 격리(`jest-prisma` 등) | 널리 쓰이지만 **여기선 검증 대상을 가린다** — 커넥션 둘이 서로의 커밋을 봐야 하는 게 핵심 |
| 로직 재현 | SQL을 손으로 다시 짜면 배포되는 코드를 못 지킨다. 실제 DAO를 부른다 |

**시나리오 6종** — 선점 중 취소는 기다렸다 0건 / 붙들던 확정이 **롤백**되면 취소가 1건(깨지면 확정
실패한 유저가 취소도 못 하는 상태에 갇힌다) / 취소가 먼저면 매치·라운드까지 롤백 / 여분 티켓이 있어도
정상 매치는 성공(넓히기는 개수를 안 본다) / 이미 소비된 티켓은 취소 불가.

**⭐ 최종 리뷰가 false-pass 통로를 실증해 잡았다.** 시나리오 ①이 **잠금 경합이 전혀 없어도 통과**했다 —
취소를 커밋 이후에 발행하면 3ms 만에 끝나는데도 단언 셋이 다 통과. 완료 시각만 보면 *"늦게 와서 이미
소비된 걸 봤다"* 와 *"잠금에 막혔다"* 가 구분되지 않는다. **취소가 커밋을 가로질렀음**(시작<커밋<완료)을
요구하도록 고쳤고, 경합 없는 상황을 재현해 이제 실패함을 확인했다.

**리뷰가 실행으로 검증:** 돌연변이 4종으로 각 시나리오가 겨냥한 회귀를 실제로 잡는지 확인(잠금 제거→①,
개수 가드 제거→②, 넓히기에 개수 비교 추가→③, `matchId: null` 제거→④). ①의 시뮬레이션이 프로덕션과
술어·페이로드 동일하고 다른 커넥션임을 확인. 격리(단독·무작위 순서)·안정성(6회 + CPU 포화)·컨테이너
정리·docker 빌드 영향 없음까지 실측.

**함께 넣은 것 — 푸시 시점 CI (`backend-ci.yml`).** 이 저장소엔 배포 워크플로(수동 실행 전용)뿐이라
가드가 **머지를 막지 못했다** — 회귀가 그냥 머지되고 *다음에 뭔가 배포하려는 순간에야* 처음 실패한다.
이제 푸시마다 빌드·유닛·통합이 돈다(**71초**). 동시성 정책은 배포와 **반대**로 뒀다(CI=앞 실행 취소 /
배포=취소 안 함 — 끊기면 이미지와 태그가 어긋난다).

**남은 것:** 이 트랙의 목적 절반은 다음 단계(B 로비 lost-update, C 티켓 유일성)의 검증 수단이었다.
B는 lobby-server 쪽이고 위치 저장소가 Postgres+Redis라 **Redis 컨테이너**가 추가로 필요하며,
**lobby-server에는 jest 설정 자체가 없다.**

### ✅ 배럴 분해 — 서브패스 `exports` (2026-08-03, 배포·E2E 통과)

`@lop/server-core`의 진입점을 배럴 하나에서 서브패스 다섯(`/logger` `/postgres` `/redis` `/mongoose`
`/express`)으로 나누고, **루트에는 외부 자원을 잡지 않는 순수 계약만** 남겼다.
spec `2026-08-01-server-core-subpath-exports-design.md`, plan 동명, 백엔드 머지 `f97ba7e`.

**성적표 — 루트 import: 1975ms / 1517모듈 → 40ms / 24모듈.** 남은 24개는 `envalid`(순수 검증
라이브러리, `validateEnv`가 씀)와 `tslib`뿐이다.

**방향을 뒤집은 게 핵심이었다.** 처음 구상은 "무거운 걸 루트에 두고 가벼운 걸 서브패스로"였는데,
세어 보니 **소비 파일 82개 중 52개(63%)가 가벼운 것만** 쓰고 있었다 — 5ms어치 받으려고 1.65초를
내던 셈. 루트를 가벼운 쪽으로 두니 **52개는 무변경, 30개만** 고치면 됐다. `firebase`(`./app` vs
`./firestore`)·`@sentry/node`(`./init` 분리)가 쓰는 표준 모양이기도 하다.

**가른 기준은 잰 무게가 아니라 "외부 자원을 잡는가"다.** `prismaClient`는 36ms로 싸지만 살아 있는
DB 클라이언트를 만들어 서브패스로 보냈다. 무게는 의존성 업그레이드 한 번에 바뀌지만 자원을 잡느냐는
안 바뀐다.

**함정 셋 — 전부 "그냥 넘어갔으면 조용히 나빠졌을" 종류:**

| | 함정 | 실측 |
|---|---|---|
| 1 | **`exports` 안에 `"//"` 주석 키** | `ERR_INVALID_PACKAGE_CONFIG`로 **패키지 자체가 로드 불가**. 키가 하나라도 `.`로 시작하면 전부 그래야 한다는 Node 검증. `turbo.json`의 주석 관습을 잘못 옮긴 것 → 최상위 형제 키 `"//exports"`로 뺀다 |
| 2 | **ts-jest는 `exports`를 안 읽는다** | `exports["."].types`를 없는 파일로 바꿔도 테스트가 통과 = 최상위 `types`로 폴백(node10 방식). 루트는 그 필드 덕에 살지만 서브패스는 대응물이 없어 `TS2307` → **`typesVersions`** 로 해결 |
| 3 | ⭐ **`isolatedModules: true`는 해법이 아니다** | ts-jest가 `TS151002`로 권하고 실제로 에러가 사라지지만, **타입 검사를 통째로 끄는 것**이다(`const x: number = "문자열"`이 통과함을 실측). 앱 tsconfig가 `__tests__`를 exclude하므로 켜면 테스트는 어디서도 타입 검사를 못 받는다 |

③은 서브에이전트가 "회귀 0"이라며 정식 해법으로 제안한 것이다. **받았으면 조용히 검사가 사라졌다.**

**해석기가 셋이 됐다 (이후 작업 시 반드시 함께 볼 것):** Node 런타임·`tsc` → `exports` /
ts-jest 타입검사 → `typesVersions` / jest 런타임 → `moduleNameMapper`(→ `src`). 즉 테스트는
`dist`의 타입으로 검사받으며 `src`를 실행한다. **새 서브패스를 추가하면 세 곳 모두** 갱신해야 한다.

**최종 리뷰(차단 결함 0)가 실행으로 검증한 것:** 캐시 전삭제 후 빌드 5/5 · `docker build` 3종 ·
**이미지 내부**에서 서브패스 해석·진입점 4개 기동·루트 24모듈 재현 · `pnpm deploy --prod` 산출물에
`dist/entries`·`lua` 포함 및 deep import 차단 · `typesVersions`를 지워 보고 실제로 load-bearing임
확인 · `requireActual` 제거 안전성(두 테스트 그래프에 `/postgres`의 다른 소비자 없음, 오히려
**예전엔 진짜 `PrismaClient`를 만들고 있었다**).

**리뷰 지적 하나를 조치했다** — 이 작업의 존재 이유인 성질("루트를 import해도 자원이 안 생긴다")을
지켜 주는 테스트가 없었다. 그 성질은 `dao.postgres.base`·`dao.mongoose.base`가 prisma/mongoose를
*타입으로만* 쓰는 데 걸려 있어 값으로 한 번만 써도 조용히 깨진다. 회귀 가드를 넣고 **일부러 깨뜨려
실제로 잡는지 확인**했다(`apps/matchmaking-server/src/__tests__/server-core-root-is-light.test.ts`).

**남은 주의사항(결함 아님):** jest 매퍼가 "모든 서브패스는 `src/entries/` 아래"를 가정한다 ·
lobby-server엔 테스트가 없어 `typesVersions` 누락이 테스트로 안 잡힐 수 있다(단 빌드·런타임은
정상이라 fail-safe) · 로컬 jest transform 캐시가 stale `dist`를 가릴 수 있다(CI는 무관).

**후속:** `DaoRedisBase`가 모듈 최상단에서 `redisClient` 싱글턴을 잡는 결합 — 주입식으로 바꾸면
루트로 올라올 수 있다. 이번엔 **서브패스가 그 결합을 드러내게** 두었다.

**슬라이스 4b가 남긴 것 (다음 사람이 알아야 할 것):**

| | 항목 | 왜 |
|---|---|---|
| ✅ | ~~**로비 자가치유의 lost-update**~~ | **해소(08-04)** — 아래 "로비 자가치유" 항목. 실제로는 **치유할 게 없는 평범한 조회에서도** 벌어지고 있었다(timestamp 갱신 때문에 매번 행 전체를 다시 씀) |
| ✅ | ~~**티켓 발급이 유저당 유일하지 않다**~~ | **해소(08-04)** — 아래 "대기표 유일성" 항목. ⚠️ 이 표에 적혀 있던 *"확정 트랜잭션이 막아 준다"* 는 **틀렸다** — 두 장이 서로 다른 매치로 갈 때만 통하고, 한 매치에 같이 들어가면 아무것도 안 막았다 |
| 🟡 | **DB 통합 테스트가 하나도 없다** | CAS의 정확성은 Postgres READ COMMITTED 의미론에 기대는데 그것을 실행으로 확인한 적이 없다. 두 커넥션(확정 vs 취소)을 동시에 돌리는 통합 테스트 하나면 이 트랙 최고 위험 주장이 논증에서 증거가 된다 |
| 🟡 | **워치독 30초는 실측값이 아니다** | 진척 신호 기반으로 바꿔 "바쁨"과 "굳음"은 구별하게 됐지만 숫자 자체는 경험값. 실제 `createRoom` 지연을 재고 조정할 것 |
| 🟡 | **룸 없는 고아 `Match` 행이 쌓인다** | 확정이 룸 생성에서 실패하면 매치 행만 남는다. 무해하지만 회수 로직이 없다 |
| 🟡 | `matchId` **인덱스 없음** | 풀 조회(`matchId IS NULL`)와 `userIds hasSome`이 초당 1회 스캔. 현 규모엔 무의미하나 스키마 부채 |
| 🟡 | Director **다중화 불가** | 틱 시작의 잔재 청소가 "지금 확정 중인 티켓은 없다"를 전제하는데 프로세스가 하나일 때만 참이다. 스케일아웃하려면 리더 선출이나 "묵은 것만 청소"로 |
| 🟡 | `maxPlayers < minPlayers` 같은 마스터데이터 오류는 조용히 영구 무매칭 — MasterData 무결성 테스트가 잡을 자리 (4a에서 넘어온 항목, 여전히 열려 있음) |
| 🟡 | 규모가 커지면 Director를 **별도 앱·별도 이미지**로 (Open Match가 그렇게 배포한다). 동기는 Director를 늘리는 게 아니라 매칭 로직만 고쳤을 때 API 서버까지 재시작되는 것을 피하고 장애를 격리하기 위해서다 |

**(참고) 슬라이스 4b가 할 일이었던 목록 — 전부 소진됨:**

| | 항목 |
|---|---|
| 🔴 | Director를 **같은 이미지의 두 번째 진입점**(`dist/director.js`)으로 만들고 k8s Deployment **replica 1**로. 매칭 루프가 둘 돌면 같은 사람을 두 매치에 넣는다 |
| 🔴 | 요청 경로에서 대기방 제거 — `requestMatchmaking`은 티켓만 만들고 끝 |
| 🔴 | `WaitingRoom` 17파일 731줄 + `Updater`/`Updatable` 삭제, 로비 자가치유를 티켓 기준으로. 로비의 `locationDetail` 판별자가 `waitingRoomId` 존재 여부라 함께 바꿔야 한다(클라는 `location` enum으로 판별하고 그 필드를 안 읽어 **무영향**) |
| 🟠 | `TbQueue`의 Casual `max_wait_seconds` 30 → **10**. 지금은 `5`가 하드코딩돼 있어 엑셀을 바꿔도 효과가 없다 (인프라 + MasterData 클·서 3개 저장소) |
| 🔵 | ~~매치 생성 경로의 원자성~~ — **강등(2026-08-23)**. 자가치유가 이미 받아준다(위 유저 위치 트랙의 강등 근거 참조) |
| 🟠 | 티켓 생성 시 검증: `queueId` 실존(슬2에서 미룸), 큐가 허락한 게임인지, **파티 인원 > 정원**이면 거부 (지금은 셋 다 조용히 영원히 대기한다) |
| 🟡 | 관측 — 어떤 제안에도 못 들어가는 티켓을 알릴 신호가 없다. 풀 크기 지표도 (게임당 O(N²), 200장 0ms라 현 규모는 무문제) |
| 🟡 | `maxPlayers < minPlayers` 같은 마스터데이터 오류는 조용히 영구 무매칭이 된다 — MasterData 무결성 테스트가 잡을 자리 |
| 🟡 | 규모가 커지면 Director를 **별도 앱·별도 이미지**로 (Open Match가 그렇게 배포한다). 동기는 Director를 늘리는 게 아니라 매칭 로직만 고쳤을 때 API 서버까지 재시작되는 것을 피하고 장애를 격리하기 위해서다 |

**슬라이스 3이 남긴 것 (슬라이스 4가 알아야 할 것):**

| | 항목 | 왜 |
|---|---|---|
| 🟠 | **후보 목록을 아무도 안 본다** | 티켓이 후보를 들지만 대기방 *합류* 경로는 레이팅·정원만 보고 큐·게임·맵을 비교하지 않는다(spec §1-2의 1번 문제 그대로). 목록화로 그 괴리가 커졌으므로 코드에 한계를 명시해 뒀다 — Director가 이걸 대체한다 |
| 🟠 | **빈 목록 가드가 방 생성 분기에만 있다** | 적당한 방이 이미 있으면 후보가 빈 티켓도 그 방에 들어간다. 요청 경로가 항상 원소 1개를 넣어 현재는 도달 불가 |
| 🟡 | `push(...userIds)`라 "티켓 1개 = 플레이어 1명" 불변식이 타입상 깨졌다 | 빈 배열이면 정원 계산에 0, 중복이면 `playerList`에 중복. 파티 착수 시 처리 |
| 🟡 | **마이그레이션 롤백 불가** | `DROP COLUMN`에 down이 없다. 앱만 되돌리면 옛 코드가 없는 컬럼을 조회해 전 요청 500 — 앞으로만 롤 |
| 🟡 | 배포 전 `WaitingRoom` 0행 + Redis 티켓 캐시 0개 확인 | 티켓만 지우고 방은 남기므로 좀비 방이 생길 수 있고, Redis(TTL 300초)는 재시작되지 않아 옛 모양 캐시가 남을 수 있다 |

**이 슬라이스가 남긴 것 (다음 사람이 알아야 할 것):**

| | 항목 | 왜 |
|---|---|---|
| 🟠 | **클라 맵은 여전히 하드코딩** — 게임 서버만 데이터 기반이 됐다 | `TbMap` 행이 하나라 지금은 우연히 같은 씬이다. **두 번째 맵이 생기는 순간** 클·서가 다른 씬을 로드한다. 클라가 `RoomDataStore.match`를 채우고도 안 읽어서 조용하다 → 슬라이스 5 |
| 🟠 | **`mapId` 검증은 넣었지만 `queueId`는 아직** | 잘못된 `mapId` 하나가 그 매치 전원에게 죽은 룸을 주는 경로가 이번에 생겨서 매칭 서버에 검증을 넣었다(맵 존재 + 그 맵이 해당 게임모드 소속). `queueId` 존재 검증은 슬라이스 4 |
| 🟡 | 배포는 **`backend-deploy` 1회 · `app: all`** | 마이그레이션이 db-migrate 이미지 안에 있고 PreSync 훅이다. 앱별로 따로 돌리면 db-migrate 태그가 안 올라가 옛 마이그레이션이 돌아 전 요청 500 |
| 🟡 | 세 앱의 `@types/node@^16`이 `node:22` 런타임과 어긋남 | 이번에 `packages/database`만 `^22`로 고쳤다. 나머지는 기존 부채 |

### ✅ 로컬 E2E(게임 진입) — 뚫렸다 (07-29 발견 → 07-30 해결)

**클라 2개로 매칭 → 실제 입장 → 게임 진행 성공(07-30).** 서버 로그 증거:
`Server listening on port 7000` → `KcpServerConnection:OnAuthenticated()` →
`[KCP] Server: OnConnected(...)`, 룸 `ip=127.0.0.1 port=7000 status=4`.

원인이 **다섯 겹**이었고 하나씩 벗겨야 다음 것이 보였다. 공통 성격: **"에디터에선 되고 빌드/로컬
클러스터에서만 깨지는" 미검증 경로에 쌓여 있었다.**

| # | 원인 | 성격 |
|---|---|---|
| 1 | 게임서버 빌드에 마스터데이터가 안 실림 | 패키지 StreamingAssets는 빌드에 복사되지 않음 |
| 2 | 게임서버 CI가 07-12부터 깨져 있었음(배포본은 손 빌드) | Library 삭제 → Linux IL2CPP sysroot 등록 불가 |
| 3 | 플레이어 빌드에서 평문 http 차단 | `insecureHttpOption=DevelopmentOnly` |
| 4 | NodePort가 로컬 호스트에서 안 닿음 | Docker Desktop 내장 k8s → **kind + hostPort 포트 풀**로 전환 |
| 5 | 클라에게 준 주소가 `localhost` | Windows는 `::1` 우선, kind 공개는 IPv4뿐 → `127.0.0.1`로 교정 |

**마무리 정리(07-30, 위 표 이후 추가 작업):**
- **2번을 증상 회피에서 근본 수정으로** — `clean:false`(Library 보존)만으로는 러너를 새로 깔거나
  워크스페이스가 지워지면 첫 실행이 또 죽는다. 그래서 **빌드 앞에 임포트만 하는 Unity 세션**을 한 번
  두는 단계를 추가했다(`-batchmode -quit`만, `-executeMethod` 없이). 그 세션이 sysroot 패키지 임포트를
  끝내고 다음 세션이 그것을 찾는다. 서버 레포 `9e0b73b`. *콜드 상태 실측은 러너 초기화 때 확인 예정.*
- **3번을 빌드 스크립트 → 프로젝트 세팅으로** — `insecureHttpOption`은 "이 앱이 평문 http로 통신한다"는
  **프로젝트 속성**이지 빌드 절차가 아니다. `ProjectSettings.asset`에 `1`→`2`(`AlwaysAllowed`),
  BuildScript의 그 줄 제거. 값은 `PlayerSettings` API로 Unity가 직접 쓰게 해 숫자를 추측하지 않았다.
  **클라는 `DevelopmentOnly` 유지**(유저 트래픽이므로 정답은 HTTPS — 클라 플레이어 빌드 시 결정 필요).
- **4번(Agones) 방향 확정** — 지금 것은 "Agones의 애플리케이션 측"이 아니라 **자체 오케스트레이터**다.
  Agones SDK는 Agones가 주입하는 **사이드카와 통신**하므로 "앱 측만 먼저 표준으로"는 원리적으로 불가능.
  **다음에 Agones를 도입할 때 이번 작업을 표준 대비 재감사해 지울 건 지우고 맞지 않는 건 고친다**(사용자
  지시). 재감사 대상: `roomPort.ts` · room-server의 파드 직접 생성 · **HTTP 하트비트(→ SDK `Health()`)** ·
  단일 노드 전제 DB 전역 풀. 빠진 핵심은 `Fleet`(사전 기동 풀) — 입장 지연 30초가 거기서 사라진다.

**후속(미착수) — 다음 세션이 알아야 할 것:**
| | 항목 | 왜 |
|---|---|---|
| 🟠 | **ConfigMap 자동 롤아웃** (kustomize `configMapGenerator`) | env 주입은 파드 시작 시점 스냅샷이라, 값만 바꾸면 **돌고 있는 파드는 옛 값을 쓴다.** 07-30에 두 번 밟아 사이클을 날렸다. 당장은 `kubectl rollout restart deploy/room-server` |
| 🟠 | **환경별 overlay 분리** | `k8s/apps/backend` 한 벌에 **로컬 전용 값이 하드코딩**돼 있다(`127.0.0.1`, `ROOM_PORT_MAX: 7009`). dev/prod를 같은 GitOps에 넣는 순간 충돌 |
| 🟠 | **kind 노드 이미지 사전 로드 자동화** | 3GB라 콜드 pull이 하트비트 60초를 넘겨 파드가 삭제된다. 지금은 수동(`docker save \| ctr images import`, 약 100초) |
| 🟡 | **Agones 정식 도입** (`Fleet` 포함) | 위 4번 |
| 🟡 | dev 방화벽 UDP `7000-7999` + 그쪽 `ROOM_PORT_MAX=7999` | dev는 아직 미검증 |
| 🟡 | 하트비트 상수 분리 · URL 인코딩 · playerList 픽스처 | 아래 원문 3·4·6번 |

#### (원문) 발견 당시 기록 — 07-29

매칭 검증 중 **매치 성사 이후 경로가 한 번도 끝까지 동작한 적이 없음**이 드러났다. infrastructure
README도 *"매치 오케스트레이션 E2E는 실제 매칭 필요 — 별도"* 라고 적어 미검증임을 예고하고 있었다.
아래는 그날 실측으로 확인한 것들이며, **1번이 프로덕션 차단 요인**이다.

1. ✅ **게임서버 빌드에 마스터데이터가 안 실린다 → 코드 수정 완료 (07-30). 이미지 재빌드 대기.**
   이미지 `re5nardo/game-server:be8203d` 안 `StreamingAssets`에 Addressables(`aa/`)만 있고
   **`MasterData/` 폴더가 없었다**(docker로 직접 확인). 부팅이 마스터데이터 로딩에서 죽어 하트비트를
   못 보내고 룸이 Error가 된다. 원인: Unity가 빌드로 자동 복사하는 StreamingAssets는
   **`Assets/StreamingAssets` 하나뿐**이고 패키지 안의 것은 복사되지 않는다(초기 진단의 "패키지 루트도
   복사된다"는 서술은 오류였다). 에디터에서는 `Path.GetFullPath("Packages/<pkg>/…")`를 Unity가 **실제
   패키지 폴더로 되돌려주기 때문에** 되던 것이라, 이 누락이 *플레이어 빌드에서만* 드러났다.
   `LOPMasterData.cs`의 *"플레이어 빌드에선 Unity가 복사한다"* 주석은 **거짓이었고 교정됐다.**
   **수정**: 클·서 MasterData 패키지 각각에 `Editor/Scripts/MasterDataPlayerBuildProcessor.cs` —
   Unity가 이 용도로 문서화한 `BuildPlayerProcessor.PrepareForBuild` +
   `AddAdditionalPathToStreamingAssets`(Unity 자신의 `AddressablesPlayerBuildProcessor`와 동일 방식).
   기존 이미지의 `aa/`가 바로 그 API로 실린 것이라 **Dedicated Server IL2CPP 빌드에서 동작함이
   실증돼 있다**(소스 `aa/Linux/catalog.bin` → 이미지 `aa/catalog.bin`, 즉 소스 폴더의 *내용*이
   목적지에 놓임). 원본 폴더가 없으면 **빌드를 실패시킨다** — 조용히 넘어가면 원래 증상(빌드 성공 →
   실행 중 사망)이 재발한다. EditMode 테스트 3개(원본 존재 / 로더 목록 전 테이블 존재 / **에디터가
   읽는 경로 == 빌드가 싣는 경로**), 서버 패키지 5/5·클라 10/10 green.
   양 패키지 main 머지·push 완료. **이미지 검증 완료(07-30)**: 새 이미지
   `re5nardo/game-server:ba13f8e`의 `StreamingAssets/MasterData/`에 `.bytes` **10개 전부** 존재하고,
   빌드 로그의 `CopyFiles …/StreamingAssets/MasterData/*.bytes`가 x86_64·arm64 양쪽에서 10개씩
   확인됐다(경로 중첩 없음). ArgoCD Synced, 클러스터 configmap `GAME_SERVER_IMAGE`도 새 태그.
   **아직 안 된 것 = 런타임 경로 확인** — 로컬 `docker run`은 엔트런스 1단계
   `ConfigureRoomComponent`(룸 env 필요)에서 죽어 `LoadMasterDataComponent`까지 가지 않으므로
   마스터데이터 로딩 자체는 실제 룸 스폰(=아래 E2E)에서만 확인된다. 바이너리 실행 자체
   (glibc·PhysX·IL2CPP)는 정상 확인됨. `[[masterdata-build-ship-path]]`

   **덤으로 드러난 것 — 게임서버 CI가 IL2CPP 전환 후 한 번도 성공한 적이 없었다.** 마지막 CI 성공은
   07-06(당시 Mono)이고 `847a18b` IL2CPP 전환(07-12) 이후 6회 연속 실패였다. 즉 **배포되던 이미지는
   CI 산물이 아니라 맥에서 손으로 빌드한 것**이었다. 원인이 로그에 안 보인 이유는 워크플로가
   `tail -80`만 찍는데 Unity는 요약을 마지막에 써서 *원인 줄*이 잘려 나갔기 때문. 세 가지를 고쳐
   처음으로 끝까지 초록이 됐다(서버 레포 `3c5c24e`, `e60c541`):
   ① **`checkout`에 `clean: false`** — 기본 clean이 무시 파일까지 지워 `Library/`가 매번 사라지는데,
   Linux sysroot·툴체인은 스텁 패키지의 에디터 코드가 실물을 내려받아 등록하는 구조라 콜드 실행에서는
   `Unable to find an Linux Sysroot`로 반드시 실패한다(그 대가로 남는 직전 산출물은 빌드 전에 지움 —
   `test -f` 가드가 낡은 바이너리로 통과해 실패를 성공으로 오인할 수 있다).
   ② **임시 `DOCKER_CONFIG`가 buildx를 가린다** — CLI 플러그인은 `$DOCKER_CONFIG/cli-plugins`에서
   찾으므로 keychain 우회용 빈 디렉터리로 갈아타면 `unknown command: docker buildx`가 난다.
   ③ **레거시 도커 빌더는 크로스 아치 불가** — `--platform`을 무시해 호스트(arm64) 베이스를 당긴다.
   `docker buildx build --push`로 전환. 진단 장비도 추가(에러 줄 추출 + `unity-*.log` 아티팩트).
   `[[gameserver-ci-pipeline-gotchas]]`
1-b. ✅ **게임서버가 룸 정보를 못 받아 엔트런스 1단계에서 죽었다 (07-30 수정)** — 마스터데이터를
   고친 뒤에도 매칭이 여전히 "로딩만 하다 끝남"이었다. 파드 로그를 잡아 보니
   `ConfigureRoomComponent` → `UnityWebRequest` → **`InvalidOperationException: Insecure connection
   not allowed`**. Unity는 **플레이어 빌드에서 평문 http를 막는다**(`insecureHttpOption` 기본값
   `DevelopmentOnly` = 개발빌드만 허용)이고 CI 빌드는 릴리스다. 게임서버는 클러스터 안 형제 서비스를
   `http://room-server-service` 등으로 부르므로 `BuildScript`에서 `AlwaysAllowed`로 명시했다.
   **마스터데이터와 정확히 같은 유형**("에디터에선 되고 플레이어 빌드에서만 깨짐")이며, 이 때문에
   마스터데이터 로딩은 실행조차 되지 않고 있었다(엔트런스 순서가 룸 설정 → 마스터데이터).
   함께 고침: `catch`의 Error 보고 조건이 **반대**였다 — `roomId`를 아는데 실패한 경우엔 보고를
   건너뛰고, 모를 때 빈 `roomId`로 요청했다. 그래서 실패가 룸 서버에 전달되지 않고 하트비트
   타임아웃으로만 정리됐다. 서버 레포 `428d4c8`.

   **✅ 서버 사이드 E2E 검증 완료(07-30)** — 룸을 직접 생성해(`POST /room {matchId}`) 파드 로그를
   끝까지 캡처: 에러 0건, `[World] Registered entity … Health=100/100`·`GameRuleSystem.SpawnEnemy`·
   틱 루프 정상, **하트비트가 2초마다 200**, `Game Over` 후 `PUT /room/status`로 룸이
   **`9 = Closed`** 로 종료(직전까지는 전부 `10 = Error`). 즉 부팅 → 룸/매치 조회 → 마스터데이터 →
   게임 진행 → 정상 종료가 처음으로 끝까지 돌았다. **남은 것 = 클라 2개로 실제 입장 확인.**

   **운영 함정 2건(반복 재발하니 배포 때마다 확인할 것):**
   - **ConfigMap을 env로 주입하면 파드 시작 시점 스냅샷이다.** 게임서버 이미지 태그를 bump해도
     **룸 서버를 재시작하지 않으면 옛 이미지로 계속 파드를 띄운다**(ArgoCD는 ConfigMap만 갱신하고
     Deployment 매니페스트가 안 바뀌니 롤아웃하지 않는다). 실제로 이것 때문에 한 사이클을 날렸다.
     정석 해결 = kustomize `configMapGenerator`(이름에 내용 해시가 붙어 자동 롤아웃) 또는 파드
     템플릿 체크섬 애노테이션. **미착수 — 다음 배포에서 또 밟는다.**
   - **kind 노드에 이미지를 미리 넣어야 한다.** 이미지가 ~3GB(압축 663MB)라 노드가 콜드 pull하면
     룸 서버의 하트비트 임계값 60초를 넘겨 **다운로드 도중 파드가 삭제된다.**
     `docker save | ctr -n k8s.io images import`로 미리 밀어넣으면 즉시 뜬다.

1-c. ✅ **클라가 룸에 접속할 수 없었다 — 로컬 클러스터가 노드 포트를 호스트에 열지 않았다 (07-30 해결)**
   `1-b`를 고쳐 서버가 정상 기동한 뒤에도 클라는 접속 즉시 끊겼다. 클라 콘솔이
   `connect to localhost:7777` → *"the other end has closed the connection"* 이었는데, 두 가지가
   겹쳐 있었다. ① **클라 환경이 `local`** 이라 `useLocalRoomInstance: 1` → 에디터 호스팅 서버(7777)로
   접속(에디터 기본 환경이 `local`이다. `local-k8s`로 전환해 해결). ② 전환 후 올바른 NodePort로 갔지만
   **NodePort가 호스트에서 아예 닿지 않았다** — Docker Desktop 내장 k8s의 노드는 숨은 컨테이너이고
   호스트로 공개된 포트가 `6443` 하나뿐이었다(같은 ingress를 LB `localhost:80`→200 /
   NodePort `localhost:31000`→실패로 실증).
   **해결 = Agones 표준으로 전환** — spec `2026-07-30-room-port-exposure-design`.
   룸마다 만들던 NodePort Service를 없애고 파드 `hostPort` + 고정 포트 풀로 바꿨다
   (`lop-backend b36657c`: `roomPort.ts` 순수 할당 로직 + 테스트 11개, 파드
   `containerPort=hostPort=PORT env` = Agones `Passthrough`). 로컬 클러스터를 **Docker Desktop 내장
   k8s → 자체 `kind` 클러스터**로 교체하고 그 포트 풀을 호스트에 매핑했다
   (`infrastructure c3325fc`: `k8s/local-k8s/kind-cluster.yaml` 신설 — 로컬 클러스터 생성 설정이 여태
   **아예 없었다**. HTTP는 기존 ingress NodePort 31000/32000을 호스트 80/443으로 넘겨 ingress 매니페스트
   무변경).
   **실측 검증(07-30)**: 룸이 풀에서 `7000` 배정 → 파드 `container=7000 host=7000 UDP`,
   `PORT=7000` → **호스트에 `0.0.0.0:7000` UDP 리스너 생성**(여태 없던 경로) → `room-service-*` 미생성 →
   **하트비트 15건 수신**. ArgoCD `root/platform/backend` 전부 Synced/Healthy, `localhost/lobby` 200.
   `[[gameserver-ci-pipeline-gotchas]]`

2. 🟠 **`useLocalRoomInstance`가 반만 우회한다** — 이 플래그는 `LOPRoom.cs:87`에서 Mirror 접속
   주소만 바꿀 뿐, 그 앞의 `RoomConnector`→`CheckRoomJoinable`(room-server) 게이트는 그대로 탄다.
   그래서 에디터 룸으로 테스트하려 해도 k8s 룸이 건강해야 한다(=1번에 막힘). 단순히 게이트를 건너뛰면
   `LOPRoom.cs:52`가 쓰는 `roomDataStore.room`이 비어 NRE — 룸 정보를 따로 채우는 경로가 필요하다.
3. 🟡 **하트비트/폴링 60초는 임시값** — 10초로는 콜드 스타트가 매번 실패해 양쪽 다 60초로 올렸다
   (room-server `HEARTBEAT_THRESHOLD`, 클라 `RoomConnector.DEFAULT_RETRY_COUNT`). 정석은 **기동 유예**와
   **생존 판정**을 분리하는 것 — 지금은 같은 상수를 둘 다에 쓴다(`room.service.ts:74`, `:322`).
4. 🟡 **`GetUserByUsername`이 username을 URL 인코딩하지 않는다**(`WebAPI.cs:73`, 경로 세그먼트에 직접
   삽입). `#`을 넣었다가 조회가 조용히 잘려 "없음 → 생성 → unique 충돌 500"을 겪었다. 닉네임 조회 등이
   생기면 공백·`/`·`?`에서 같은 종류로 깨진다.
5. 🟡 **MPPM 인스턴스 이름에 공백이 있다**(`Player 2`) — `DeviceIdentifier`가 그대로 붙여 username에
   공백이 들어간다. 지금은 통과하지만 접미사에서 걸러내는 게 안전하다.
6. 🟡 **서버 에디터 룸의 `playerList` uuid 하드코딩** — DB가 초기화될 때마다(클러스터 재구축 등) 무효가
   되어 매번 손으로 갱신해야 한다(`ConfigureRoomComponent`, 커밋 금지 픽스처). 구조적 해결 필요.

**⏸ 마스터데이터 값 핫업데이트 — 보류 결정 (07-30).** 1번을 고치면서 "데이터를 패키지에 싣지 말고
동적으로 받으면 어떠냐"를 검토했고, **현 StreamingAssets 구조 유지로 결정**했다. 재개할 때 다시
도출하지 않도록 확정된 사실만 박아 둔다: ① **스키마(`.cs`)는 컴파일 대상이라 동적화 불가** — 유연해질
수 있는 건 값뿐이고, 새 컬럼은 그것을 *읽는 코드*가 있어야 의미를 가진다(그래서 유연성의 답은 "스키마를
동적으로"가 아니라 "스키마를 조립 언어로 설계" — LOP는 `TbAbility`의 `AbilityEffect` 조합으로 이미
그렇게 하고 있다). ② UPM 패키지는 **에디터·빌드 시점 의존성**이라 "패키지를 런타임에 받는다"는 개념이
없다 — 이미지 안에 패키지 폴더·소스·데이터가 존재하지 않고 DLL만 있다. ③ 지으려면 **파일 출처(CDN/S3)
와 버전 권위(백엔드가 응답에 버전을 박아 내림)를 분리**하고, 빌드에 베이스라인을 계속 실은 뒤 원격이
새로우면 덮는다(네트워크 전용 금지 — 콜드 스타트가 이미 아픈 지점). ④ **클라↔게임서버 버전 일치는 하드
요구**(같은 시뮬 코드라 값이 다르면 예측 발산), 매칭서버는 정원만 보므로 느슨해도 된다. ⑤ 통일하려면
`.bytes`를 Addressables TextAsset으로 — 클라는 이미 Character·Item·Scene을 S3(dev 프로필)에서 받으므로
새 배관이 아니다. `[[masterdata-build-ship-path]]`

**후속(슬라이스 2/4/5에서 챙길 것):**
- **큐 대기시간 배선 시 동작 변경 주의** — `waitingRoom.service`는 아직 최대 대기 `5`초를 하드코딩하고,
  신설 `tbqueue.json`은 `max_wait_seconds` 30/60을 들고 있다. 이 30/60은 **spec이 새로 설계한 값이지
  기존 XML에서 이관한 값이 아니다.** 배선하는 사람은 대기창이 6~12배 길어지는데, 이는 의도된 것이지
  회귀가 아니니 버그로 되돌리지 말고 수동 확인할 것.
- **마스터데이터 그룹 좁히기** — 신설 3테이블이 전부 `c,s,m`으로 뭉뚱그려져 있어 클라가 절대 안 읽는
  매칭 전용 컬럼(`TbQueue.rating_range_start`/`rating_range_max`/`rating_relax_per_sec`/
  `max_wait_seconds`/`allowed_game_mode_ids`, `TbMap.scene_path`)까지 클라 빌드에 실린다. spec은 친선전
  실력 폭을 유저에게 **숨은** 값으로 설계했으니 클라 노출은 그 의도에 반한다. 슬라이스 4/5가 누가
  무엇을 읽는지 확정하면 컬럼별 `##group`으로 좁힐 것.
- **gen 스크립트가 Unity `.meta`를 지운다** — `gen.sh`/`gen.bat`이 출력 폴더를 `rm -rf`/`rmdir /s /q`로
  통째로 지우는데, 그 안의 Unity `.meta`는 Luban이 다시 만들어 주지 않는다. 지금 이 머신은 Unity
  Library 캐시가 기존 GUID를 복원해 문제가 안 보이지만, 클린 체크아웃·CI·다른 개발자 PC는 GUID가
  새로 발급돼 `.meta` 약 78개가 흔들린다. 폴더째 지우지 말고 `*.cs`/`*.bytes`만 지우도록 고칠 것 —
  기존부터 있던 조건이라 파이프라인 위생 정리 슬라이스에서 함께 처리.

---

## ✅ MongoDB 제거 (2026-08-05, 배포·프루닝 완료)

세 앱이 **시작할 때 접속만 하고 아무도 안 쓰던** MongoDB를 코드와 클러스터에서 통째로 걷어냈다.
매치메이킹 표준화 트랙이 남긴 후속 항목. 백엔드 머지 `fb10048`, 인프라 머지 `f176699`.

**32개 파일 / 583줄 삭제, 동작 변화 0.** 지운 것: 앱의 mongoose DAO 5 + 모델 5,
`@lop/server-core`의 `DaoMongooseBase`·접속 설정·`/mongoose` 서브패스·로더,
앱 로더 호출 3곳, `mongoose`/`mongodb` 의존(3앱 + 패키지), 루트 `pnpm.overrides`,
`.env` 6개와 패키지 `config`의 `MONGODB_*`. 인프라에선 deployment·service·**PVC**.

**순서가 유일한 위험이었다 — 백엔드를 먼저 배포해 의존을 끊고, 그다음 파드를 내렸다.**
반대로 했으면 아직 접속을 시도하는 파드가 재시작할 때 깨진다. 끊긴 것을 두 가지로 확인했다:
새 파드 기동 로그에서 `✌️ mongoose loaded and connected!` **줄이 사라진 것**과,
런타임 도커 이미지 안에 **mongoose 흔적 0**(코드가 물리적으로 접속할 수 없는 상태).

**검증:** 빌드 5/5 · 유닛 22+11+159+10 · 통합 lobby 21 + matchmaking 14 · `docker build` ·
배포 후 에러 로그 0 · ArgoCD 프루닝 후 최종 파드 목록에 mongodb 없음.

> 위 "배럴 분해" 항목이 서브패스 다섯(`/mongoose` 포함)과 `dao.mongoose.base`를 언급하는데,
> **그건 그 시점의 기록**이다. 현재 서브패스는 넷(`/logger` `/postgres` `/redis` `/express`)이고
> 루트 경량 가드 테스트가 기대하는 목록도 `['redis', 'express', 'winston']`로 줄었다.

---

## ✅ ResponseCode 통합 — 5 → 2 (2026-08-05)

**"5중 복제"의 실체는 4개가 살아 있고 1개는 죽어 있으며, 이미 어긋난 상태였다.**

| 사본 | 내용 | 조치 |
|---|---|---|
| 백엔드 lobby / room | 바이트 동일, 기본 12개 | **삭제** → 공용 패키지 |
| 백엔드 matchmaking | 기본 + 매칭 전용 4개(`INVALID_QUEUE`·`INVALID_GAME_MODE`·`INVALID_MAP`·`PARTY_TOO_LARGE`, 10102~10105) | **삭제** → 공용 패키지(합집합) |
| Unity 클라 | 기본 12개 — **그 4개를 모른다** | 4개 추가 + 계약 출처 주석 |
| Unity 게임서버 | 클라와 바이트 동일, **참조 0** | **삭제**(죽은 코드) |

**드리프트가 이미 일어나 있었다.** 매칭서버가 새 코드를 넣었는데 클라가 못 따라왔다 — 지금 클라는
`!= SUCCESS`만 보고 숫자를 그대로 로그에 찍으므로 **고장은 아니었지만**, 복제가 조용히 벌린 틈이다.
이제 두 파일의 코드 목록이 **16개로 정확히 일치**한다.

**남은 2개는 못 합친다(언어가 다르다).** 백엔드 TS 1 + Unity C# 1. 두 파일이 서로를 가리키는 주석을
달아 두는 것이 지금 할 수 있는 전부다 — **자동 드리프트 검사는 서로 다른 저장소라 CI가 상대를 볼 수
없어서 불가능**하고, 그걸 하려면 한쪽에서 생성하는 파이프라인이 필요하다(지금은 과설계).

**함께 지운 것 — 죽은 `CreateMatchmakingTicketDto`.** DTO는 매퍼의 중첩 클래스 하나만 참조하고
그 중첩 클래스는 아무도 참조하지 않는, **서로만 붙들고 있던 짝**이었다(리뷰가 짚었던 항목).

**호출부는 그대로다.** `class ResponseCode` + static 형태를 유지해 `ResponseCode.SUCCESS` 표기가
안 바뀌었고, 값도 안 바뀌어 동작 변화 0. 임포트 17곳만 `@lop/server-core`로 갈아끼웠다.
루트 배럴에 둔 이유는 **외부 자원을 하나도 잡지 않는 순수 계약**이라 `responseBase.interface`와
같은 자리이기 때문(배럴 분해 때 정한 기준 그대로).

**검증:** 빌드 5/5 · 유닛 202(22+159+10+11) · 통합 lobby 21 + matchmaking 14 ·
`docker build` 후 **이미지 안에서 실제 값 확인**(`200 / 10102 / 10105 / 5000000`) ·
TS↔C# 코드 목록 diff 일치. 백엔드 머지 `ea92c02`, 게임서버 머지 `9358310`.

---

## ✅ 버려진 매칭 대기표 만료 (2026-08-06, 배포 통과) — **하트비트는 같은 날 철회**

클라가 죽으면 대기표가 **영원히** 남아 유령 플레이어가 낀 매치를 만들던 것을 Director 틱이 스스로
정리한다. spec `2026-08-05-abandoned-ticket-expiry-design.md`, plan `2026-08-06-abandoned-ticket-expiry.md`,
백엔드 머지 `4051ea1`, 인프라 머지 `6b05c2e`.

**버리는 이유 둘, 서로 독립:**

| | 무엇을 잡나 | 값 |
|---|---|---|
| **신호 끊김** | 죽은 클라 | 60초 (코드 상수, Room의 `HEARTBEAT_THRESHOLD`와 같은 값) |
| **상한 초과** | 살아 있지만 너무 오래 못 잡힌 사람 | 큐별 `TbQueue.ticket_ttl_seconds` = 600초 |

**신호를 새로 만들지 않았다 — 이미 오고 있었다.** 로비가 대기자 한 명당 **초당 한 번**
`GET /matchmaking-ticket/:id`를 매칭서버에 던지고 있는데 아무도 기록하지 않았다. 그 자리에서
`lastHeartbeat`을 찍는다(갱신+조회 한 쿼리). **클라 변경 0** — 삭제 후 유저를 로비로 되돌리는 일은
직전 슬라이스에서 고친 로비 자가치유가 이미 한다.

**가장 위험한 지점에 가드를 뒀다.** 로비가 죽거나 로비↔매칭서버가 끊기면 아무도 신호를 못 보낸다 —
그걸 "여러 명이 동시에 죽었다"로 읽으면 **대기자 전원의 티켓이 날아간다.** 풀에 60초 이내 신호를 준
티켓이 하나도 없으면 신호 기반 삭제를 건너뛰고 `warn`을 남긴다. 상한 기반 삭제는 신호와 무관하므로
가드와 상관없이 계속한다.

**⭐ 계획의 되돌리기 검증이 두 번 빗나갔고, 두 번 다 실행이 잡았다.** 이 트랙의 가장 큰 교훈이다.

| | 계획이 지목한 것 | 실제 |
|---|---|---|
| Task 3 | 컨트롤러 한 줄을 되돌리면 깨진다 | **안 깨졌다** — 테스트가 서비스를 직접 불러 컨트롤러를 안 거쳤다. **그 한 줄이 이 기능을 프로덕션에서 살아있게 하는 유일한 연결인데 아무도 안 지키고 있었다.** HTTP 수준 테스트(supertest+App)를 추가해 해소 |
| Task 5 | 순수 함수의 `matchId` 필터를 지우면 통합 테스트가 깨진다 | **안 깨졌다** — 보호가 두 겹(DB 쿼리 + 순수 함수)인데 통합 경로는 첫 겹만 지난다. 두 겹 모두 각자 테스트로 지켜지고 있음이 확인돼 기능 결함은 아니었다 |

교훈: **"되돌려서 확인한다"를 계획에 쓸 때 *어느 층을 되돌리면 어느 테스트가 깨지는지*를 정확히
따져야 한다.** 층을 잘못 짚으면 검증이 헛돈다.

**⭐ 최종 브랜치 리뷰가 또 Critical급을 잡았다(이 프로젝트 네 번째).** 태스크 리뷰 5개가 전부 통과한 뒤였다.

- **청소부가 큐를 막았다** — 삭제가 던지면 그 틱의 매칭이 통째로 멈춘다. 루프 러너가 예외를 잡아
  로그만 남기므로 **파드는 멀쩡한 채 매칭만 0건인 상태가 지속**된다. 같은 코드베이스가 소비 티켓
  삭제에는 이미 정반대 결론(로그만 남기고 확정은 진행)을 내려 뒀는데 우선순위가 뒤집혀 있었다.
  *컨트롤러(나)가 "기존과 같은 패턴"으로 분류했던 것을 리뷰어가 반박했고 그쪽이 옳았다*
- **취소 응답이 유저를 없는 방으로 밀었다** — 취소 경로는 "삭제 0건 = 매치가 이겼다"에 의존하는데,
  **이 브랜치가 0건의 두 번째 원인(쓸어담기가 지움)을 만들었다.** `ALREADY_IN_GAME`을 받은 클라는
  게임룸으로 전이한다. 0건이면 재조회해 둘을 가르도록 고치고, 계약이 바뀐 네 계층의 주석을 갱신했다

**검증:** 빌드 5/5 · 유닛 217 · 통합 51 · CI · 배포 후 마이그레이션 적용 확인
(`lastHeartbeat` 컬럼 + `CURRENT_TIMESTAMP` 기본값 실측) · 파드 4개 정상 · 에러 0 ·
API↔Director 시계 차이 0초. 되돌리기 확인은 각 수정마다 실제로 실패를 관찰했다.

### ⚠️ 같은 날 하트비트를 철회했다 — 사용자가 구멍을 잡았다

배포 후 사용자가 물었다: *"매칭 대기 시간보다 60초가 길어서, 두 명이 매칭 걸고 한 명이 꺼도
그 전에 매칭되는 거 아냐?"* **맞았다.**

```
t=0초    A·B 매칭 시작 → 요구 인원 8명
t=2초    A 종료 (신호 끊김)
t=10초   요구 인원이 2명까지 내려감 → ★ A·B 매치 성사 (유령 매치)
t=62초   그제서야 A가 "죽었다"고 판정 — 이미 늦음
```

Casual은 최소 2명 · `max_wait_seconds` 10 · 틱 1초라 **매치가 10초에 만들어지는데 죽음 판정은
60초**였다. **50초짜리 구멍이 그대로 있었다.**

**왜 60초였나 — 값의 출처가 틀렸다.** 방(Room)의 `HEARTBEAT_THRESHOLD`를 그대로 가져왔는데, 방은
*"게임 서버가 죽었나"* 를 보는 것이고 여기서 대조했어야 할 기준은 **"매치가 만들어지는 데 걸리는
시간"** 이었다. 그 대조를 안 했고 **리뷰어 셋도 못 잡았다.** 값을 빌려올 때는 *그 값이 무엇과
비교되는지*까지 함께 봐야 한다.

**결정(사용자): 하트비트를 통째로 뺀다.** 유령 매치는 막지 않고 **이탈한 클라가 패널티를 진다**
(업계 통용). 남는 것은 **큐별 상한(600초)뿐** — 값은 "대기표가 무한히 쌓이지 않는다"로 줄어든다.
Open Match `assignedDeleteTimeout`과 같은 성격.

**가드도 함께 사라졌다.** 가드는 *"신호가 끊긴 게 사람이 죽어서인가 통로가 끊겨서인가"* 를 가르려고
있었는데, 상한은 `createdAt`만 보고 신호를 안 쓰므로 **잘못된 사망 판정 자체가 없다.** 지킬 게 없으면
가드도 없다. 판정 함수가 69줄 → 44줄, 전체 **+58 / −364줄**.

**배포는 expand-contract 두 단계로 나눴다.** `db-migrate`가 **PreSync 훅**이라 마이그레이션이
*구버전 파드가 아직 살아 있을 때* 돈다. 컬럼을 먼저 지우면 그 창에서 구버전 Prisma가 없는 컬럼을
select 해 티켓 조회가 전부 실패한다 — 매칭이 잠깐 죽고 대기 중이던 유저가 튕긴다.
① 컬럼을 안 쓰는 코드 배포(`d3136ae`) → ② 컬럼 삭제(`e53ca4e`). 리뷰어가 이 창을 짚었다.

> 남아 있던 컬럼이 새 코드에 무해했던 이유: `NOT NULL DEFAULT CURRENT_TIMESTAMP`라 Prisma가
> insert 에서 빼도 기본값이 채워지고, select 는 스키마에 없으니 안 읽는다.

**검증:** 빌드 5/5 · 유닛 211 · 통합 41 · CI · 2단계 배포 후 컬럼 소멸 실측(8컬럼) · 에러 0.

**실플레이 검증(08-06):**

| 무엇 | 결과 |
|---|---|
| **매칭 회귀** (클라 2개) | ✅ 매치 성사 `players: 2` + 룸 생성. **쓸어담기 0건** — 판정을 틱 한가운데 끼웠는데도 살아있는 대기자를 안 건드렸다 |
| **상한 만료** (클라 1개) | ✅ 끝에서 끝까지. `createdAt`을 11분 전으로 밀자 **다음 틱에 즉시** `abandoned tickets swept count: 1` → **같은 초에** 유저 위치가 `None`으로 치유 → 클라가 매칭 해제 |

> 상한 테스트는 **클라 하나로** 해야 한다 — 둘이면 10초 만에 매치가 성사돼 상한에 도달하지 않는다.
> 그리고 10분을 기다리거나 값을 바꿔 배포할 필요가 없다: `UPDATE "MatchmakingTicket" SET "createdAt"
> = now() - interval '11 minutes'` 한 줄이면 실제 코드 경로가 그대로 돈다.

**서버만 고쳤는데 클라가 알아서 돌아왔다** — 삭제 → 로비 자가치유 → 위치 `None` → 클라 복귀 사슬이
이미 있었고, 그게 "클라 변경 0"이 가능했던 이유다.

**취소 경로도 실플레이 확인됨** — 고친 곳이다(삭제 0건의 원인이 둘이 되어 재조회로 가른다).

**남은 후속:**

| | |
|---|---|
| ~~**"매칭 실패" 알림**~~ | ✅ **완료(08-16)** — 위 [매칭 실패 안내](#-매칭-실패-안내-2026-08-16-2레포-배포인게임-검증) 절 |
| **매치 수락 팝업** | 유령 플레이어의 AAA 표준 방어선(LoL·도타·오버워치). 큐 청소와 보완 관계 |
| **클라의 명시적 종료 감지 → 매칭 취소 요청** | 사용자 제안. `OnApplicationQuit`/`OnApplicationPause` 에서 취소를 쏘면, 하트비트 없이도 **가장 흔한 이탈(알트+F4·게임 종료)** 을 잡는다. 강제 종료·네트워크 끊김은 못 잡지만 그건 이제 이탈자 패널티 영역 |
| **클라 `CancelMatchmaking`의 에러 로그** | 쓸어담기가 이긴 경우는 이제 *예상된* 결과인데 여전히 `LogError` |
| **`director.ts` 합성 루트가 테스트 없음** | 배선을 no-op으로 바꿔도 통과한다. `purgeConsumedTickets`도 같은 처지라 이 브랜치가 만든 후퇴는 아님 |
| **파티 티켓** | 현재 파티는 미구현. 생기면 `lastHeartbeat`이 티켓당 하나라 **한 명만 살아 있어도 전원 생존으로 취급**된다 — per-member 신호가 필요 |
| **모바일** | OS 서스펜드로 폴링이 멈추면 60초 만에 큐에서 빠진다. 모바일 착수 시 재검토 |

> **유령 매치는 이 기능이 막지 않는다** — 하트비트를 철회했으므로 상한(10분) 전까지는 이탈자도
> 그대로 매칭된다. 그 대가는 이탈자가 진다는 것이 현재 결정이다.

---

## ✅ Recon 러버밴딩 원인 규명 (2026-08-06) — 06-24 가설은 틀렸고, 진짜 원인을 찾았다

계측기를 깔고 통제 실험을 돌려 원인을 확정한 트랙. **대응은 하지 않았다** — 별도 슬라이스다.
spec `2026-08-06-recon-entity-load-diagnostics-design.md`, plan `2026-08-06-recon-entity-load-diagnostics.md`.

### 06-24의 "엔티티가 많아서" 가설은 반증됐다

같은 세션 안에서 엔티티만 2 ↔ 52로 바꿔 가며 3조건(기준선 → 부하 → 되돌리기)을 측정했다.
Knight 고정(점프력이 곧 측정 감도), 자동 스폰 off(부하 드리프트 제거), 지연 150ms.

| | 기준선(2) | 부하(52) | 되돌리기(2) |
|---|---|---|---|
| 클라 FPS | 59 | 57 | 60 |
| Recon max | 0.000 | 0.000 | 0.000 |
| Snap gap avg/max | 20.3 / 101.3 | 19.9 / **348.4** | 20.0 / 68.2 |
| 서버 lag | −1 | **−1** | −1 |

**원인 A(서버 틱 밀림)·B(클라 프레임 저하) 모두 탈락.** 부하가 실제로 움직인 유일한 값은
`Snap gap max`(101 → 348 → 68)인데 recon으로 번지지 않았고, 되돌리기에서 제자리로 왔다.

### 진짜 원인 — 입력 한 틱 누락이 만든 오차가 문턱 아래라 영원히 안 고쳐진다

부하와 무관하게 **걷기에서만** 어긋난다는 사용자 관찰을 좇아 스파이크 정황 로그를 붙였고,
28건 연속 관측으로 사슬이 확정됐다:

```
입력이 한 틱 비어 서버가 1틱 제동 → 위치 4cm 차 → 문턱(0.06) 미만 → 보정 스킵 → 영구 잔류
```

**산술이 정확히 맞는다:** 속도 차 `2.00 m/s` = `maxAcceleration × dt` (100 × 0.02),
위치 차 `0.040 m` = `2.00 × 0.02`. 즉 서버가 정확히 한 틱 동안 입력 없이 돌았다.

**영구 잔류는 관측으로 증명됐다:** `delta=(0.039, 0, -0.008)`이 45틱 동안 **완전히 고정**됐고,
플레이어가 **가만히 서 있는 동안에도**(양쪽 속도 0) 그대로였으며, 네트워크가 정상으로
돌아온 뒤(`prune=0 seqGap=0`)에도 사라지지 않았다. `Reconciler`는 문턱 미만 오차를
"예측 정확"으로 보고 롤백을 건너뛰는데, **그 아래 오차를 서서히 줄이는 경로가 없다.**

> **눈에 보이는 튐은 2차 효과다.** 4cm 자체는 안 보인다. 이런 사건이 쌓여 0.06을 넘는 순간
> 스냅 보정이 걸리고 그게 러버밴딩으로 보인다(관측 중 `err=0.063` 건이 실제로 있었다).
>
> **더 큰 함의:** 서버는 플레이어가 보는 곳과 다른 자리에 그가 있다고 믿는다. 히트 판정·충돌·
> 어빌리티 범위가 전부 그 어긋난 위치로 해소된다. 이동에서는 안 보여도 전투에서 드러날 수 있다.

**한 틱이 빈 이유는 정황까지만 확정됐다.** 같은 창에 `prune=1 dMax=+1 seqGap=1`이 잡혔다 —
유실이 아니라 **지각**이다. 중복 전송(`RedundancyWindow=3`)은 유실을 막지만 복구본은 1틱 늦게
오고, 그때 여유가 `dAvg=-1.8`로 얇아져 있었다(평소 -3.0). **적응형 lead가 복구본이 쓰려던
쿠션을 미리 깎았을 가능성**이 있다. 이 인과는 창 단위 요약이라 특정 틱과 1:1 대응은 미확정.

> `netcode-redesign.md` Phase 3의 **"고손실 강건성(옵션 A = miss 시 마지막 인풋 반복) — 드롭.
> 실환경 prune율이 유의미해지면 재개"** 조건이 충족됐다.

### ✅ 대응 후보 — 셋 다 완료 (정산 2026-08-23)

| 후보 | 어디서 닫혔나 |
|---|---|
| 입력 miss 시 마지막 입력 반복 (옵션 A) | ✅ 08-09 **슬라이스 2 / 3층** — 유실 틱을 마지막 이동 입력으로 |
| 문턱 미만 잔류 오차 처리 (서서히 수렴) | ✅ 08-07 **슬라이스 1** — 재조정 문턱 `0.06` → `0.01`(점진은 렌더가 이미 맡음) |
| lead 마진 하한을 복구본 지연(1틱) 이상으로 보장 | ✅ 08-09 **슬라이스 2 / 1층** — 마진 바닥 0 → 1틱 + 평형점 `[-3,-1]` |

**따라서 "Recon 잔류 오차 대응"은 더 이상 할 일이 아니다.** 08-11 MPPM 클론 실측에서
`reconMax=0.004`(4mm) · `corrections=0` — 규명 당시 4cm의 1/10이고 하드 롤백이 한 번도 없었다.
남은 4mm는 **상수 오프셋일 수 있으나 쫓지 않기로 한 것**이고(아래 08-11 절), 이 표의 미착수 항목이
아니다. *(로드맵 "다음에 할 것"에 이 줄이 08-23까지 남아 있었다 — 완료 절이 이미 아래에 있는데도.
`[[verify-backlog-claims-before-working]]`)*

### 남긴 계측기 (상시)

- 클라 HUD: FPS·엔티티 수·`Snap lag`·`Snap gap`·`Cushion` + **`Reset stats`** + **`Dump`**(전 값을 한 줄 로그로)
- 클라 `[ReconSpike]` 로그 — 오차 0.02m 초과 시 예측/권위 위치·속도, 그 틱 입력, 접지,
  입력 타이밍, `snapAge`를 함께 기록. **오차가 지속되면 매 틱 찍혀 시끄럽다**(그 반복이
  영구 잔류를 증명한 근거이기도 하다). 임계값은 `Reconciler.SpikeLogThreshold` 상수
- 서버 `[TickHealth]` 로그(기본 꺼짐) + `DebugEnemySpawner`(부하 즉시 생성·제거)
- GameFramework `SnapshotArrivalStats`(EditMode 8 테스트)

### 실험 프로토콜에서 배운 것 (다음에 또 쓴다)

- **`Recon avg`로 판정하면 안 된다** — 1.2초 이동평균이라 자극이 끝나면 0으로 수렴한다. **`Recon max`**(리셋 이후 누적)로 읽을 것
- **측정 전 유효성 확인 필수** — `Snap gap avg`가 20ms 근처인지, `Entities`가 서버와 같은지.
  둘 중 하나라도 틀리면 그 측정은 버린다. 1회차에서 클라가 끊긴 줄 모르고 무효 데이터를 냈다
- **서버 `lag`의 건강한 기준선은 0이 아니라 −1**이고, `frameMaxMs > budgetMs`는 원인의 증거가 아니다(캐치업 여유가 `8 × interval`)
- 자극 선택이 결정적이다 — 점프는 값을 덮어쓰는 이벤트라 어긋나지 않는다. **걷기(적분)** 여야 드러난다

### 부수 발견 (전부 별건 — 1건 해소, 3건 미수정)

| | |
|---|---|
| ~~`EntitySpawner.FlushDespawns`가 **세션 0개일 때 NRE**~~ | ✅ **해소(2026-08-23, Server `8e81123`).** `GetAllSessions().DefaultIfEmpty()`가 null 하나를 냈다 — 예외가 틱 코루틴(`TickUpdaterBase.TickUpdateLoop`) 안에서 터지고 `RunnerBase.RunPhase`에 try/catch가 없어 **서버 틱이 영구히 멈춘다.** 디스폰 경로 전체(아이템·사망) 해당. **원인은 오타로 보인다** — 형제 호출부는 전부 자체 확장 `OrEmpty()`(=null 컬렉션 가드)를 쓰는데 여기만 이름이 비슷한 LINQ `DefaultIfEmpty()`(=빈 컬렉션에 **null 원소 하나를 채워 넣는다**)였다. **정반대 동작.** 고친 방식은 헬퍼 교체가 아니라 **제거** — `GetAllSessions()`는 `sessionsById.Values`를 그대로 돌려주므로 null이 불가능하고, 빈 컬렉션은 `foreach`가 0번 도는 게 정상이라 애초에 가드가 필요 없었다 |
| 인증 거절이 **NRE로 번진다** | `LOPRoom.OnPlayerDisconnect`가 인증 통과 전 연결을 가정하지 않음. 깨끗한 거절이 예외로 보여 원인 파악을 방해했다 |
| 클라가 **끊겨도 조용히 혼자 돌아간다** | 재접속도 알림도 없다. 서버와 완전히 분리된 채 게임이 멀쩡해 보인다 — 이번 진단에서 실제로 무효 데이터를 만들었다 |
| **50마리 동시 스폰이 클라를 끊는다** | 10초 무통신 → KCP 타임아웃. 서버도 그 순간 `frameMaxMs=2109ms`. 10마리씩 나눠 넣으면 버틴다 |

---

## ✅ 문턱 아래 잔류 오차 제거 (2026-08-07) — 대응 슬라이스 1

08-06 진단이 찾은 원인에 대한 첫 대응. **본체는 상수 하나**(재조정 문턱 `0.06` → `0.01`).
spec `2026-08-07-reconcile-threshold-residual-error-design.md`,
plan `2026-08-07-reconcile-threshold-residual-error.md`. 클라 단독.

### 무엇이 잘못돼 있었나

문턱은 **"스냅이냐 점진이냐"를 가르는 선**인데 우리는 **"고치느냐 마느냐"** 로 쓰고 있었다.
6cm 아래는 보정이 스킵되고 **줄이는 경로가 아예 없어서**, 입력 한 틱 누락이 만든 4cm가
정지 중에도 네트워크 회복 뒤에도 영구히 남았다(45틱 연속 동일 delta 관측).

[표준](https://en.wikipedia.org/wiki/Client-side_prediction)에는 방치 구간이 없다 —
작으면 점진 보간, 크면 스냅이다. **우리 구조에선 "점진"을 렌더가 이미 맡고 있다**
(`LocalEntityInterpolator` + `RenderCorrectionSmoother` = 언리얼 CMC의 캡슐/메시 분리).
그래서 시뮬에 드리프트를 새로 넣지 않고 **시뮬은 즉시 정확히 보정**하게 두는 것이 맞다.

> 시뮬 드리프트를 넣지 않은 이유: 같은 책임이 두 곳으로 갈리고, 시뮬이 영원히 "수렴 중"이라
> 서버와 정확히 일치하는 순간이 없어진다 — 히트 판정이 동기였는데 반만 해결된다.
> 언리얼식 client-trust(서버가 클라 보고 위치를 채택)는 `netcode-redesign.md` §6.4가 금지.

### 측정 — 같은 절차로 두 번

조건: 환경 `local`, `latency: 150` / `unreliableLoss: 2`(커밋 기본값), 같은 빈 장소에서 **60초 걷기**.
`Snap gap avg` 20.0ms로 두 번 다 유효성 확인.

| | 기준선 (6cm) | 수정 후 (1cm) |
|---|---|---|
| **`[ReconSpike]` 반복** | **12건 연속** 동일 delta | **4건, 서로 떨어짐**(tick 3588/4410/4419/5811) |
| **`reconMax`** | **0.400 m** | **0.040 m** |
| `corrections` | 5 | 4 |
| `fps` | 60 | 60 |
| `snapGapMax` | 333.3ms | 90.4ms |

**판정: 성공.** 단일 기준이었던 "같은 delta가 계속 찍히는 현상"이 사라졌다. 스파이크 사이 구간이
비어 있다는 것은 오차가 1cm 아래로 내려갔다는 뜻이다.

**예상 못 한 소득 — 최악 오차가 10배 줄었다.** 기준선에서는 작은 오차가 안 고쳐진 채 쌓이다
6cm를 넘는 순간 큰 보정이 터졌다(최대 0.40m). 지금은 생기는 족족 정리돼 **누적해 커질 기회가 없다** —
4cm는 단일 미스 하나가 만드는 크기 그대로다.

**비용은 늘지 않았다.** `corrections` 5 → 4. 늘어날 것으로 예상했으나 그렇지 않았다.
다만 60초 1회씩의 측정이라 4와 5의 차이는 잡음 범위이고, 정직하게 말할 수 있는 것은
**"치솟지 않았다"** 까지다. fps 60 유지.

**부작용 점검(사용자 확인): 없음** — 조작감 변화 없음, 화면 톡톡 튐 없음, 연출 중복·누락 없음.
따라서 렌더 `minCorrection`(2.5cm)은 이번에 건드리지 않았다.

### 함께 남긴 계측

`ReconciliationStats.CorrectionCount` — 보정(롤백+재생)이 실제로 일어난 횟수. HUD의 `Recon max` 줄에
`(corr N)`으로, `Dump`에 `corrections=`로 나온다. **문턱을 바꾸기 전에 넣어 기준선을 잡았다** —
비교 대상 없이 값을 바꾸면 판정이 불가능하다.

### 후속

| | |
|---|---|
| ~~**원인 자체를 줄이기**~~ | ✅ **완료(08-09)** — 마진 바닥 1틱 + 밴드 이동 + 입력 예측. 아래 "입력 파이프라인 3층 완성" |
| 렌더 `minCorrection` | 지금은 불필요. 작은 보정이 화면에 보이면 그때 |
| 실환경 재측정 | 이 판단은 **로컬 조건 기준**이다. 08-09에 로컬 잡음 바닥이 5틱임이 확인돼 **로컬에선 더 못 잰다** |

> **작업 중 배운 것:** 코드를 고치기 전에 **플레이를 먼저 정지**할 것. 플레이 중 리컴파일은 도메인
> 리로드로 런타임 DI 참조를 전부 날려(`entityRegistry` null) `LOPEntityView.Update`가 매 프레임
> NRE를 뱉는다. 코드 결함처럼 보이지만 아니다 — 플레이 정지 후 재컴파일하면 사라진다.

---

## ✅ 입력 파이프라인 3층 완성 (2026-08-09, 4레포 머지) — 대응 슬라이스 2

08-07이 *결과*(잔류 오차)를 자가 회복시켰다면, 이번엔 *원인*(입력 미스 시 제동)을 없앴다.
spec `2026-08-09-lead-margin-floor-design.md` · `2026-08-09-explicit-idle-input-frames-design.md` ·
`2026-08-09-input-prediction-repeat-last-design.md`.

### 표준 입력 파이프라인은 세 겹이다

| 층 | 막는 것 | 우리 (전 → 후) |
|---|---|---|
| 1. 버퍼(lead) | 지터 | 바닥 0 → **1틱**, 평형점을 꼬리가 이른 쪽으로 |
| 2. 중복 전송 | 유실 | 윈도우 3 (유지) + **매 틱 커맨드 프레임** |
| 3. 입력 예측 | 그래도 빈 칸 | **없음 → 마지막 이동 이어 쓰기** |

### 1층 — lead 평형점 (⚠️ 미검증)

`LeadController` 기본 밴드가 **"가장 늦게 온 입력이 1틱 지각"을 평형으로 용인**하고 있었다
(`tightBand=1, looseBand=-1`). 그 자리에선 지터 한 번에 곧바로 폐기이고, 유실 복구 사본(원본보다
1~2틱 늦다)은 아예 못 쓴다. 평형점을 `maxD ∈ [-3,-1]`(꼬리가 1~3틱 이르게)로 옮겼다 —
DOTS `TargetCommandSlack=2`와 같은 자리. 마진 바닥도 0 → 1틱(런타임 틱 간격에서 환산, 상수 아님).

**효과는 증명 못 했다** — 아래 "로컬 측정 한계" 참조. 표준 정합과 무해함만 확인.

### 2층 — 무입력도 명시적으로 보낸다

클라가 **입력 있을 때만** 스트림에 커맨드를 넣어서, 서버가 보는 "빈칸"이 *안 눌렀다*와 *유실됐다*
두 뜻이었다. 가만히 서 있으면 서버가 **매 틱 미스 경로**로 떨어지고 있었다.

무입력도 자기 틱 번호를 단 `{0,0}` 프레임으로 보낸다(표준 command-frame). **서버 동작은 그대로**
— 값이 같아 이동 결과가 동일하다. 바뀐 건 `input == null`의 뜻이 "유실" 하나가 된 것뿐이고,
그게 3층의 전제다. (②만으로는 값이 없다. ①과 한 덩어리.)

곁가지: 죽은 `InputSequenceToC` 송신 제거(클라 핸들러가 빈 몸통이었고 ② 이후 초당 50개가 될 뻔),
서버가 안 읽는 `input_command`·`entity_transform` 필드 채우기 중단(후자는 클라 보고 위치라
§6.4가 사용을 금지하는 값).

### 3층 — 유실 틱을 마지막 입력으로 메운다

서버는 빈 칸을 비워둘 수 없다. 0으로 메우는 건 "제동하라"는 **능동적으로 틀린** 지시다 —
한 틱은 20ms고, 그 사이 손을 뗐을 확률보다 계속 누르고 있을 확률이 압도적이다.

- 이동축만 이어 쓴다 (점프·어빌리티는 반복하면 두 번 발동)
- 예측값에 시퀀스를 안 물려준다 (dedup·seqGap 기준을 흐린다)
- 연속 8틱(160ms) 상한 → 넘으면 중립 (끊긴 캐릭터가 영영 달리면 안 된다)

LOP-Shared EditMode **10/10**(신규 5).

> **근거를 과장하지 말 것.** "마지막 입력 반복"은 롤백 넷코드(GGPO)의 확립된 기법이지만 그건
> *P2P에서 상대 입력*을 예측하는 맥락이다. **서버 권위 엔진이 서버 측에서 같은 처리를 한다는
> 근거는 확인하지 못했다** — Unity Netcode for Entities 문서는 `GetDataAtTick`의 미스 동작을
> 안 적어 놨고, Source 자료의 "repeat last known command"는 클라 쪽 서술이었다. 근거는
> ① 빈 칸은 메워야 하고 0은 흔한 경우에 틀린 값이라는 논리 ② `netcode-redesign.md` Phase 3이
> 이미 "옵션 A"로 지목하고 단 재개 조건을 08-06 진단이 충족시킨 것, 둘이다.

### 검증 — 서명으로만

**성공:** 걷는 중 스파이크에서 `predVel` 4.00 → `srvVel` 2.00(정확히 반토막 = 한 틱 제동)이던
서명이 사라지고, `predVel` 2.00 → `srvVel` 2.00(**크기 유지, 방향만 한 틱 낡음**)으로 바뀌었다.
오차도 0.040 → 0.024. 조작감·정지 이상 없음.

### ⚠️ 로컬 측정 한계 — 여기선 더 못 잰다 (중요)

네트워크 시뮬을 **전부 끄고도**(지연·지터·유실·순서뒤섞기 0) `snapGapMax`가 **96.9ms**(평균 20ms),
입력이 여전히 1틱 지각했다. **한 기계에서 에디터 둘을 돌리는 데서 오는 ~5틱 잡음 바닥**이고,
우리가 튜닝하려는 신호(1~2틱)보다 크다. 세 번 재는 동안 매번 9초짜리 멈춤(`stalls=1`,
`behindMax=463`)이 끼어 누적 수치가 오염됐다.

- **lead 정책(1층) 판정은 실환경으로 미룬다.** 마진이 천장(100ms)에 붙어도 `worstD`가 안 내려갔다
- 3층은 *누적 수치가 아니라 서명*으로 판정했으므로 유효
- 다음에 측정할 땐 **서버를 스탠드얼론 빌드**로 띄워 잡음 바닥부터 낮출 것

> ⚠️ **이 진단은 절반이 틀렸다 (2026-08-11).** 잡음의 정체는 "한 기계에 에디터 둘"이 아니라
> **메인 에디터 한쪽에만 걸리는 부하**였다. 빌드 없이 **MPPM 클론에서** 재니 `stalls=0` ·
> `snapGapMax` 96.9 → 54.6ms로 떨어져 네 항목 모두 판정 가능해졌다. 처방은 "서버를 빌드로"가 아니라
> **"클론에서 재라"** 다. 상세는 아래 "미검증 4항목 전부 통과".

### ✅ 미검증 4항목 전부 통과 (2026-08-11) — 빌드 없이 **MPPM 클론**에서 닫았다

08-09에 "빌드에서 재보자"고 유예한 네 항목을 **빌드를 만들지 않고** 검증했다. 열쇠는 환경이 아니라
**어느 에디터에서 재느냐**였다 — 잡음의 정체가 "한 기계에 에디터 둘"이 아니라 **메인 에디터 한쪽에만
걸리는 부하**였기 때문이다(08-09에 *"MPPM 2번 클라는 로딩도 빠르고 부드럽다"* 고 이미 적어둔 그것).

**조건:** 클라 환경 `local-k8s`(= 게임 서버가 kind 파드 안 IL2CPP 빌드, 에디터 호스팅 아님) +
클라 씬 `LatencySimulation` 활성(latency 150 / jitter 20ms / **unreliableLoss 2%** / scramble 2).
서버 씬엔 시뮬 없음. 측정은 **MPPM 클론**에서, 두 회차(정지 / 걷기).

| 볼 것 | 통과 기준 | 1회차(정지) | 2회차(걷기) | |
|---|---|---|---|---|
| **lead 평형점** `dMax` | `[-3,-1]`에 눌러앉는다 | -1 (avg -2.5) | **-1** (avg -2.2) | ✅ |
| **prune / seqGap** | 정상 구간 0 유지 | 0 / 0 | **0 / 0** | ✅ |
| **마진 수렴** | 천장(100ms)에 안 붙는다 | 32ms | **60ms** | ✅ |
| **클라 시작 멈춤** | ≤ 1초 (50틱) | `behindMax=13` | **`behindMax=8`** | ✅ |
| (잡음 바닥) `snapGapMax` | — | 64.8ms | **54.6ms** | 이전 96.9ms |

> ⚠️ **시작 멈춤은 `stalls`가 아니라 `behindMax`로 판정한다.** `ResetStats()`가 `stalls`의 기준선을
> 다시 잡으므로(`catchUpBaseline`), Reset을 누른 회차의 `stalls=0`은 *Reset 이후*만 뜻한다.
> `behindMax`는 리셋으로 안 지워져 시작 구간을 덮는다(코드 주석에 명시). 실제로 Reset 없이 잰
> 3회차에서는 `stalls=1`이 나왔다 — 클론도 한 번은 멈추되 **9틱(180ms)** 이라 기준의 1/5이다.

RTT 197ms + 지터 + **2% 손실**에서 66초 동안 입력이 한 틱도 안 버려졌다(엔티티 62개 동시). 3층
입력 파이프라인(08-09)이 실제 손실 환경에서 값을 한다는 직접 증거다. `margin`이 천장에서 내려와
**자기 평형점을 찾은 것**이 1층(lead 정책) 판정의 핵심 — 로컬에선 계속 천장이라 못 보던 것이다.

**reconciliation (덤)** — 66초 걷기에서 `reconMax=0.004`(4mm), `corrections=0`. 하드 롤백이 한 번도
없었다. 08-06에 규명한 잔류 오차(4cm)의 **1/10**이고, 08-07 수정이 실제 RTT 조건에서 확인된 건 처음이다.
⚠️ 단 `reconMax == reconAvg == reconLast == 0.004`로 **세 값이 같다** — 튀었다 사라지는 오차가 아니라
**상수 오프셋**일 수 있다(옛 버그와 *모양*이 같고 크기만 1/10). 소수점 3자리 출력이라 미세 변동과
구분이 안 된다. 4mm는 문턱(0.06) 근처도 아니고 시각적으로 무의미하니 **쫓지 않되, "작아졌다"와
"사라졌다"는 다르므로** 기록만 남긴다.

#### 방법이 자산이다 — 다음에도 이렇게 잰다

- **메인 에디터에서 재지 말 것.** 메인만 멈춤(`stalls`)을 겪어 누적 수치가 오염된다. 클론은 `stalls=0`.
- **`[HudDump]`는 클론 로그 파일에 그대로 쌓인다** — `Library/VP/<clone>/Logs/Editor.log`.
- **UnityMCP는 클론에 못 붙는다.** 클론이 떠 있어도 `mcpforunity://instances`가 메인 하나만 보고한다
  (`instance_count: 1`). 클론은 `-editor-mode com.unity.mppm.clone -library-redirect ../..`로 떠서
  프로젝트 경로를 공유하는데, 등록이 겹치는지 브릿지가 아예 안 뜨는지는 **미확인**. 로그 파일로 우회.
- **`Reset()`은 누적 카운터(`TotalPruneCount`/`TotalSeqGapCount`)까지 지운다**
  (`Assets/Scripts/Netcode/InputTimingStats.cs`). 그래서 위 두 회차는 **매치 시작 구간을 덮지 않는다** —
  시작 구간을 보려면 **Reset을 누르지 않고** Dump해야 한다(아래 "매치 시작 구간 입력 대량 폐기").

**남은 것(별건):** `LeadController`의 나머지 경계값(`maxMargin` 0.1s, step 10/2ms, `DefaultMargin`)이
아직 하드코딩이다. 바닥값만 틱에서 유도했으니 나머지도 틱 배수로 맞춘다(동작 무변경).

---

## ✅ 매치 시작 틱 정렬 (2026-08-09) — 서버는 해결, 클라는 넷코드 문제가 아니었다

spec `2026-08-09-tick-origin-alignment-design.md`, plan `2026-08-09-tick-origin-alignment.md`.

### 서버 — ✅ 해결

`Run(0, TICK_INTERVAL, 0)`으로 시드했는데 다음 프레임에 `elapsedTime`이 **프로세스 가동 시간**으로
덮여, tick만 뒤처진 채 몇 초를 8배속 질주했다. 그 상태의 `tick`과 `elapsedTime`을 같이 읽어
`gameInfo`에 담으니 **자기모순인 쌍**(Tick=200인데 ElapsedTime=33.6초=1680틱)이 클라로 복사됐다.

시드를 자기 시계에서 유도하도록 고쳤다.

| | 전 | 후 |
|---|---|---|
| `catch-up capped (behind by N)` | **1680** | **12** |

### 클라 — ⏸ 넷코드 문제가 아니다

세 번 시도하고 세 번 다 진단이 틀렸다. **계측이 뒤집었다:**

```
출발:   localTime=272.382   rtt=4.616   (설정 지연은 150ms)
3초후:  localTime=281.696
→ Delay(3000)을 걸었는데 9.31초가 흘렀다 = 클라가 6.3초 멈춰 있었다
```

시계가 뒤늦게 움직인 게 아니라 **클라 에디터가 시작할 때 멈춰 있었고**, 그동안 실제 시간이 흘러
`processibleTick`이 300틱 앞서간 것이다. `rtt=4.6초`도 pong을 처리 못 하고 있었다는 같은 증거.

- **메인 에디터만 겪는다** — MPPM 2번 클라는 로딩도 빠르고 부드럽다
- 빌드에서 멈춤이 1초 이하면 50틱이라 7프레임(≈0.1초)에 소화돼 **대책 없이 해결**된다
- **빌드에서 재보기 전에는 넷코드를 더 손대지 않는다**

> ✅ **닫힘 (2026-08-11)** — 빌드가 아니라 **MPPM 클론**에서 재서 확인했다. 판정 근거는 **`behindMax`**
> 다(세션 전체 최대 뒤처짐, 리셋으로 안 지워져 시작 구간을 덮는다):
>
> | | 메인 에디터 | 클론 (3회차) | 기준 |
> |---|---|---|---|
> | `behindMax` | **463틱 (9.3초)** | **13 / 8 / 9틱 (160~260ms)** | 50틱(1초) 이하면 대책 불필요 |
>
> 클론도 시작할 때 **한 번은 멈춘다**(Reset 없이 잰 회차에서 `stalls=1`). 다만 그 크기가 기준의
> 1/5이라 7프레임 안에 소화된다. "메인 에디터만 겪는 증상"은 유지 — 갈리는 건 멈춤의 **유무가 아니라
> 크기**다. 넷코드 대책은 필요 없다.
>
> ⚠️ **`stalls`를 판정에 쓰지 말 것** — `ResetStats()`가 기준선을 다시 잡아(`catchUpBaseline`)
> Reset 이후만 센다. 시작 구간을 보려면 `behindMax`를 보거나 Reset을 누르지 않는다.

### 만들었다가 되돌린 것 (다시 짓지 말 것)

| | 왜 되돌렸나 |
|---|---|
| **시계 수렴 대기** (2종) | 옛 기준(안정화, 2.8초)은 통했지만 **시계가 아니라 로딩 부하를 흘려보낸 것**이라 우연이었다. 새 기준(첫 pong)은 0ms라 아무것도 못 막았다 |
| **snap-forward** (밀리면 틱만 점프) | 동작은 확인했으나(313틱) **표준이 아니다** — Unity Netcode for Entities 1.8.0이 오히려 skip을 걷어내고 catch-up으로 바꿨다. 표준은 배칭(두 틱 → 한 틱 2×dt). `ClockDilator`가 점진 구간을 이미 덮는다 |

> 더 정교하게 다룰 일이 생기면 **`ClockDilator` 쪽을 고도화**한다 — 정책을 시계와 틱 두 층에
> 나눠 두지 않는다(그 불일치가 원래 문제였다).

### ⚠️ 이 트랙에서 배운 것 — 추론으로 세 번 단언하고 세 번 틀렸다

| 단언 | 실제 |
|---|---|
| "Mirror EWMA 수렴에 3.1초" (계산까지 붙임) | 첫 샘플을 그대로 대입한다 — pong 하나면 됨 |
| "서버 권위 구조는 틱을 건너뛸 수 있다" | 출처 없는 추론. 표준은 배칭/따라잡기 |
| "첫 pong이면 시계가 맞는다" | pong은 왔는데도 300틱 밀렸다 |

**코드를 읽고 계산을 맞춰봐도 계속 빗나갔고, 계측 한 번이 전부 갈랐다.** 시작할 때 그 로그부터
붙였으면 세 번의 왕복이 없었다.

---

### 새로 드러난 것 — 매치 시작 구간 입력 대량 폐기

접속 직후 `prune=162`, 입력이 평균 **142틱(≈2.8초)** 늦게 도착, 그동안 서버 캐릭터는 원점에 정지
(`srvPos=(0,0,0)`)인데 클라는 이미 걷고 있었다. 클라·서버 틱 시계가 정렬되기 전의 **시작 레이스**로
보인다. 그때 서버가 중립이었던 건 3층 규칙대로 옳다(받은 입력이 없으면 예측 근거도 없음).

**게임 시작 직후 몇 초간 조작이 안 먹는 증상**으로 나타날 수 있어 로컬 아티팩트로 단정하지 않는다.
다음 후보.

> **❌ 오진이었다 — 입력 폐기가 아니다 (2026-08-11).** Reset 없이 매치 시작부터 덮은 회차 3번 모두
> **`pruneTot=0` / `seqGapTot=0`**. 입력은 시작 구간에서도 한 개도 안 버려진다. `prune=162`는
> **같은 날 고친 서버 틱 시드 버그**(`behind by 1680`) 상태의 관측이었다. 방향도 정반대다 — 그때는
> 입력이 **늦게** 왔고(`+142틱`), 지금은 **일찍** 온다(`dAvg=-43.9`). 이른 도착은 폐기되지 않는다.
>
> **진짜 원인은 아래 "시작 구간 클럭 정렬" 트랙으로 옮긴다.**

---

## ✅ 시작 구간 클럭 정렬 — 해결 (2026-08-11 규명 → 2026-08-12 수정)

**증상:** 매치 입장 직후 몇 초간 **눈에 보이는 "드르륵"**(러버밴딩). 그동안 *"시작 직후 조작이 안
먹는다"* 로 표현하던 것의 실체다. 입력 폐기(옛 진단)가 **아니다** — `pruneTot=0`.

### 격리 — 자극과 오염을 갈랐다

적이 오염원이다(넉백은 외력이라 `reconMax`를 오염시킨다). 파드 이미지는 **10초마다 10마리**를
스폰하므로 **첫 10초는 적이 0마리**다. `entities=2`로 그 구간임을 매 덤프가 증명한다.

| 첫 ~9초 (`entities=2`) | 가만히 | **걷기** |
|---|---|---|
| `reconMax` | 0.000 | **0.640 (64cm)** |
| `corrections` | 20 | 28 |
| 체감 | — | **드르륵** |

**클럭이 어긋나 있어도 가만히 있으면 안 드러난다.** 이동이 있어야 어긋난 *시간*이 *거리*로 변환된다
— `[[recon-entity-load-parked]]`의 "점프로는 못 잡고 걷기라야 드러난다"와 같은 원리.

**시작 구간에 갇혀 있다:** 9.7초 `0.640` → 17.0초 `0.640`(증가 0, 7초를 더 걸었는데도).
`corrections`도 28 → 28. 클럭이 수렴하면 완전히 사라진다.

### 기전 — 같은 틱 번호가 서로 다른 순간을 가리킨다

`[ReconSpike]`가 그 순간을 통째로 남겼다:

```
tick=212  cur=264  err=0.640  snapAge=52          ← 정상 10~12
predPos=(5.00,0.00,-0.04)   srvPos=(5.00,0.00,-0.68)
predVel=(0.00,0.00,-2.00)   srvVel=(0.00,0.00,-4.00)     ← 정확히 반
input[h=0.00 v=-1.00]  timing[dAvg=-43.9 dMax=-43 prune=0]   ← 정상 -2.5
```

| tick 212 | 클라 기록 | 서버 |
|---|---|---|
| 위치 z | -0.04 (막 출발) | **-0.68** (0.68m 진행) |
| 속도 z | **-2.00** = 가속 **1틱**분(`MaxAcceleration 100 × dt 0.02`) | **-4.00** = 최고속 |

**클라의 틱 212는 "걷기 시작 직후", 서버의 틱 212는 "이미 0.68m 달린 뒤"** 다. 두 지표가 나란히
비정상이다 — 입력이 **43틱(860ms) 일찍** 도착(`dAvg=-43.9`), 스냅 앵커가 **52틱 낡음**(`snapAge=52`).
64cm는 롤백 문턱(0.06)의 **10배**라 하드 롤백이 걸리고 그게 러버밴딩으로 보인다.

로그 전체에서 `snapAge>20`은 이 시작 구간 스파이크 하나뿐이다(나머지는 `snapAge 10~12` /
`err 0.08~0.18`로 별개 양상).

### 수렴 곡선 (같은 회차)

| | ~9.7초 | ~17초 | 안정 |
|---|---|---|---|
| `dAvg` / `dMax` | -5.6 / -5 | -2.2 / -2 | `[-3,-1]` |
| `lead` | 12틱 | 8틱 | ~7 |
| `rtt` | **304ms** | 197ms | ~192 |

(가만히 회차는 더 컸다 — 9초 시점에 `dAvg=-15.4` / `lead=20` / `rtt=260`.)

### 원인 — 퐁 첫 표본 하나가 오염된 채 정답으로 굳는다

Mirror의 `OnClientPong`에 원본 표본 계측을 넣어 확정했다.

| | rawRtt | rawOffset |
|---|---|---|
| **n=1** | **1052ms** | **−191.692** (0.86초 오차) |
| n=2 | 192ms | −192.540 |
| n=3 | 179ms | −192.563 |

**첫 표본 하나만 오염돼 있다.** Mirror는 접속 즉시 첫 핑을 보내는데(`OnTransportConnected` →
`SendPing()`) 그 순간이 인증·씬·스폰으로 가장 바쁘고, Mirror 문서도 RTT가 처리 지연을 포함한
값임을 명시한다. 그리고 `ExponentialMovingAverage`는 **첫 표본을 평균 없이 그대로 채택**한 뒤
표본당 9.5%씩만 교정한다.

그 상태에서 우리는 **퐁 3개째에 출발선을 확정**했다(`[ClockSeed] pongs=3`) — 시드 오차
**0.70초 = 35틱**. 이후 `ClockDilator`가 초당 5%만 교정하므로 복구에 13초가 걸렸다.

**Mirror의 결함이 아니다.** `predictedTime`은 계속 읽으면 수렴하는 값이고 실제로 4~5초면 오차
10ms 이하다. 우리가 그것을 *한 번 읽어 출발선을 긋는 용도*로 쓴 것이고, 고정 틱 시뮬레이션은
우리가 얹은 것이므로 "언제 읽을지"도 우리 몫이다. 그 층이 비어 있었다.

### 수정 — 추정이 자리를 잡은 뒤에 시드 (2026-08-12)

`LOPRoom.WaitForClockSettleAsync()` — `drift`(예측시간 − 실시간)가 0.5초 창에서 진폭 5ms 미만이
될 때까지 기다렸다 시드한다. 임계 5ms는 Mirror의 평균 계수에서 유도했다(잔여 오차 1틱 미만).
대기는 접속 직후 시작해 `gameInfo` 왕복과 겹쳐 돌리고, 이미 떠 있는 로딩 화면 뒤에 숨는다.
7초 타임아웃 시 최선값으로 진행한다(폴백이 종전 동작보다 항상 같거나 낫다).

**측정 결과 (실측, 2회차 · MPPM 클론):**

| | 1회차 | 2회차 |
|---|---|---|
| 안정까지 걸린 시간 | 4.79초 | 5.26초 |
| 시드 시점 진폭 | 0.0049 | 0.0046 |
| 시드 시점 퐁 개수 | 42개 | 47개 |
| 시드 정확도(시드 시각 − `gameInfo` 시각 ≈ 대기 시간) | 40ms 이내 일치 | 40ms 이내 일치 |

같은 `entities=2` 구간, 걷기 기준으로: `reconMax` `0.48~0.64` → **`0.040~0.080`** ·
`corrections` `20~28` → **`8~9`** · `[ClockTrace]` 오차 최대 `420ms` → **`48~57ms`**(이후
`±5~10ms`로 안정) · `stalls`(잡음으로 멈춘 횟수) `1` → **`0`**. 입장 직후 "드르륵"은 소멸했다.

**영구 계측:** `[ClockSettle] settled elapsed=.. amplitude=.. drift=.. window=..`.
다른 환경(느린 폰, 실제 네트워크)에서 대기가 길어지면 추측이 아니라 이 로그로 판단한다.

spec `docs/superpowers/specs/2026-08-12-start-window-clock-sync-design.md`,
plan `docs/superpowers/plans/2026-08-12-start-window-clock-sync.md`.

### 남은 것 (별건)

- ~~**첫 틱 스파이크 (시계 아님).**~~ ✅ **해결 (2026-08-13)** — 아래 별도 절 참고. 당시 추정했던
  "입력 파이프라인이 막 가동돼 처음 몇 개가 늦거나 버려진다"는 **반증됐다**(`pruneTot=0`인 회차에도
  재현).
- **비용: 로딩이 4.8~5.3초 길어졌다**(실측). 대기는 기존 로딩 화면 뒤에 숨지만 총 로딩 시간에는
  그대로 더해진다. `[ClockSettle]`의 `elapsed`를 계속 남겨두므로, 다른 환경(느린 폰·실제
  네트워크)에서 더 길어지면 그 로그로 고도화 여부를 판단한다.

### 재는 법

**Reset 금지**(누적 카운터가 매치 시작부터 쌓여야 한다) · **클론에서** · 입장하자마자 **바로 걷기** ·
**~8초에 Dump**(`entities=2`면 적 오염 없음이 증명된다) · `[ReconSpike]`는 클론
`Library/VP/<clone>/Logs/Editor.log`에 쌓인다.

---

## ✅ 참가 전 틱 스냅으로 되돌아가던 문제 — 해결 (2026-08-13)

위 트랙에서 별건으로 떨어져 나온 "첫 틱 4~8cm 스파이크"의 원인이다. 시계와 무관했다.

### 원인 — "비교할 예측이 없다"가 "예측이 틀렸다"로 처리됐다

매치 시드 직후엔 스냅이 `snapAge`(≈12틱)만큼 **과거**를 가리키며 계속 도착한다. 클라는 그 틱들을
살지 않았으므로 예측 기록이 없다. 그런데 `Reconciler`가 이렇게 생겼었다:

```csharp
if (snapshotHistory.TryGet(anchorTick, out var predicted)) { ... 가까우면 return }
reconciliationStats.RecordCorrection();      // ← 기록이 없으면 비교 없이 여기로
SetPosition(worldEntity, snap.position);     // 옛 서버 상태(스폰 지점)로 하드 복원
...
if (!predictedAbilityStateHistory.TryGet(anchorTick, out var abilityState)) return;  // ← 재생까지 생략
```

**되돌리기만 하고 되감지 않는다.** 클라가 예측한 진행이 통째로 버려지고, 스냅이 다 빠질 때까지
반복된다. (재생만 돌았다면 표준대로 복구됐을 것이다 — 아래 참고.)

### 계측이 가른 것

| 단계 | 관측 | 결론 |
|---|---|---|
| `[FirstTicks]` (첫 15회 비교를 크기 무관 기록) | 첫 틱 `err=0.000`, 이후 한 틱만 어긋나고 다시 0 | 스냅샷 기록 시점 불일치 **반증** |
| `[WorldTick]` (`world.Tick` 앞뒤) | 위치가 **스폰 지점 0.000으로 반복 리셋**, 이동 자체는 매 틱 정상 | 이동이 아니라 **틱 사이에 누가 되돌린다** |
| `pruneTot=0`인 회차에도 재현 | | 입력 폐기설 **반증** |
| `[ReplaySkip]` | 5건 전부 `어빌리티 기록 없음`, 앵커 432~437 < 클라 첫 틱 440 | **확정** |

### 수정 — 흐름을 하나로 유지한다

보정은 **"서버 상태로 맞춘다 + 내 입력을 재생한다"** 하나여야 한다. 참가 전 틱도 그 흐름으로
처리된다 — 그 틱엔 내 입력이 없어 재생이 아무 일도 안 하고, 내 첫 틱부터는 재생이 제자리로
되돌려 놓는다.

그래서 분기를 **"보정할까 말까"에서 "재생 시작 상태를 어디서 가져올까"로 내렸다.** 어빌리티
기록이 없을 때 이유가 둘이고 대응이 다르므로, `SequenceBuffer.FirstRecordedTick`(GameFramework
신설)으로 가른다:

| 기록 없음의 이유 | 대응 |
|---|---|
| **앵커 < 내 첫 기록 틱** = 참가 전. 그 뒤로 내가 굴린 틱이 없으니 지금 상태가 곧 그때 상태 | 복원할 게 없을 뿐, **재생은 정상 수행** |
| 그 외 = 살았는데 링 밖으로 밀려남 | 그때 상태를 몰라 낡은 어빌리티로 재생하면 대시 등이 잘못 재현되므로 **종전대로 생략** |

> **처음엔 "참가 전 틱 스냅을 아예 무시"로 좁게 고쳤다가 되돌렸다.** 증상은 사라지지만 표준
> 흐름 밖에 분기를 하나 만드는 것이고, "복원하고 재생 안 하기"라는 구멍 자체는 남기 때문이다.

**측정 결과:** `reconMax` `0.040~0.080` → **`0.000`** · `[ReconSpike]` 첫 틱 1건 → **0건** ·
넉백 정상 처리(상태이상 게이트로 `corrections=2`, 위치 오차 0) · 체감 이상 없음.
EditMode 454/454(신규 5).

**남은 4cm 스파이크는 이 수정과 무관하다** — 같은 코드로 `pruneTot=8`인 회차엔 3건 나고
`pruneTot=0`인 회차엔 0건이다. **초반 입력 폐기**가 원인이며 아래 별건.

### 표준 대비

Gambetta의 정석은 **서버가 마지막으로 처리한 입력 시퀀스**를 기준으로 미처리 입력을 재생한다.
우리는 클·서가 틱 시간선을 공유하는 오버워치형이라 **틱 자체가 확인응답**이고, 앵커 이후 입력을
재생하는 것이 같은 일이다 — 구조는 표준과 다르지 않았다. 표준에 **없던 것은 "복원하고 재생을
건너뛰는" 경로**이며, 그 경로가 클라 진행을 조용히 버렸다.

> 첫 참가 시 "처리된 입력이 없는" 상태는 표준에서 예외가 아니라 **가장 쉬운 경우**다 —
> 미처리 입력이 전부라 전부 재생하면 된다. 핵심 성질은 *클라가 자기 입력을 잃지 않는 것*이다.

### ✅ 입력 폐기 — 원인 규명 완료 (2026-08-13). 넷코드 문제가 아니라 프레임 히칭의 증상

`pruneTot`이 회차마다 **0 · 3 · 8 · 11**로 들쭉날쭉하던 것의 정체. **넷코드는 정상이었다** —
시계·서버 시차 추정치·쿠션 어느 것도 원인이 아니다.

**인과 사슬 (프레임 단위 계측으로 확정):**

```
클라 프레임 하나가 90ms 걸림   (틱 간격 20ms → 4~5칸이 밀림)
  → 다음 프레임에 밀린 칸을 몰아서 처리 + 입력도 같은 순간에 몰아서 발송
  → 오래된 칸 번호로 찍힌 입력이 서버 도착 시 이미 지난 칸  → PruneBefore가 폐기
  → 서버는 그 칸들을 마지막 입력 반복(PredictMissing)으로 메움 → 클라와 어긋남 → 4cm 스냅백
```

`maxD`(가장 늦게 온 입력)의 크기가 매번 **그 창의 최장 프레임 ÷ 20ms**와 일치했다. 폐기가 터진
창은 예외 없이 `frameMax`가 40ms를 넘었고, 같은 창에서 서버 시차 추정치(`drift`)는 **1ms도 안
움직였다**. 틱을 몰아 도는 것(`TickUpdaterBase`의 캐치업) 자체는 표준이고 정상이다 — 문제는 그때
나가는 입력이 *지난 칸 번호로 찍힌다*는 것뿐이다.

**반증된 가설 (다시 세우지 말 것):**

| 가설 | 반증 근거 |
|---|---|
| 시작 쿠션(30ms=1.5틱)이 얇아서 | 깨끗한 판들도 같은 쿠션인데 폐기 0 |
| 서버 시차 추정치가 시작에 튄다 | 폐기 창에서 `drift` 평평(1ms 이내) |
| 시계가 목표를 못 따라간다 | `clockGap`은 *그 프레임의 길이*를 재고 있었다(아래 함정) |
| 매치 시작 한정 현상 | 긴 판 중간에도 `frameMax` 58ms 창에서 재현 |

> **계측 함정 (박제):** `TargetTime − elapsedTime`을 **elapsedTime을 이번 프레임분만큼 전진시키기
> 전에** 재면, 그 값은 "시계가 뒤처진 양"이 아니라 **거의 그 프레임의 길이**다. 나쁜 판의
> `clockGap` 105·126ms를 "시계가 5틱 뒤처졌다"로 읽었다가 프레임 추적으로 뒤집혔다.

**간헐 스파이크는 남긴다.** 실기기에선 예산을 지켜도 스파이크가 나며, 그때 현 구조는 "입력 몇 개
폐기 + 서버가 마지막 입력 이어 씀"으로 완만히 열화한다 — 표준적인 대응이고 대가는 관측된 4cm다.

### ✅ 실기기 실측으로 종결 (2026-08-13, Galaxy Z Flip3 / dev 개발 빌드 + adb logcat)

**에디터 착시가 맞았다.** 개발 APK(`client-app-deploy`, `environment=dev`, `development=true`)를
폰에 올려 같은 계측을 돌린 결과, **정상 구간에서 문제 자체가 없다.**

| | 에디터(MPPM 클론) | **실기기** |
|---|---|---|
| 프레임 | 10~45ms 오르내림 | **17ms 고정**(120프레임 내리 흔들림 0 = 정확히 60fps) |
| 우리 시뮬(`sim`) | 0.3~1.0ms | **0.6~1.0ms** (예산 16.7ms의 6%) |
| 폐기 | 창마다 0~17 | **0** (`maxD`가 늘 −2 = 2틱 일찍 도착) |
| **첫 틱 `sim`** | **25.4ms**(정상의 50배) | **8.1ms**(10배) — **그 프레임도 17ms 안에 들었다** |

- **① 프레임 예산 — 해소.** 에디터의 긴 프레임은 전부 에디터 오버헤드였다. 폰은 vsync 60Hz에
  물려 정확히 17ms로 돈다.
- **② 시뮬 첫 틱 워밍업 — 삭제.** 8.1ms는 예산 안이라 폐기를 만들지 않는다. 에디터의 25ms는
  상당 부분이 JIT이었고 IL2CPP 빌드엔 없다(예상대로).

> **재는 법(다음에도):** adb는 Unity 안드로이드 모듈에 들어 있다
> (`.../PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`). 앱 켜기 **전에**
> `logcat -G 5M`(이 폰의 상한) → `-c` → `logcat -s Unity:V > file`. 개발 빌드는 일반 로그에도
> 스택 트레이스를 붙이므로 `Application.SetStackTraceLogType(LogType.Log, None)`을 켜두지 않으면
> 버퍼가 시작 구간을 밀어낸다.

### 🟠 새로 드러남 — 장시간 플레이 성능 저하 (미착수, 별도 트랙)

같은 실기기 측정에서 **2분쯤 지나자 성능이 무너졌다.** 원래 찾던 것보다 큰 문제라 따로 둔다.

| 구간(tick) | 프레임 | 폐기/창 | GC(gen0) |
|---|---|---|---|
| ~1500 (첫 25초) | 17ms 고정 | 0 | **1** |
| 4000~5500 | 33~100ms | 1~5 | 32 |
| **5500~** | **117~333ms**(3~5fps) | **10~28** | **193** |

마지막 구간의 `maxD=100`은 **클라가 서버보다 2초 뒤처졌다**는 뜻이다(프레임당 캐치업 상한 8틱을
넘겨 계속 밀림). 넷코드가 아니라 **프레임/메모리 문제**다.

> ⚠️ **이 구간 수치는 계측이 부풀렸을 수 있다.** 진단 로그가 *40ms 넘는 프레임마다* 찍히고 그때마다
> 문자열을 할당한다 → 느려질수록 더 찍고 → 더 할당 → 더 느려지는 **되먹임**. 국면 1에선 로그가
> 0줄이었다가 국면 2에서 폭증했으므로, 붕괴의 얼마가 게임 탓인지 지금 데이터로는 못 가른다.
> **다시 잴 땐 초당 한 줄 요약**(그 1초의 최장 프레임·GC·최대 sim)으로 바꿔 로그를 60분의 1로
> 줄일 것. 사용자 판단으로 **지금은 착수하지 않는다**(짚이는 후보가 있음, 2026-08-13).

#### 📊 1차 측정 — 에디터에선 재현되지 않았다 (2026-08-23)

**방법이 바뀌었다.** 위 처방("로그를 초당 한 줄로")을 따르는 대신 **밖에서 CLI로 찔러 읽었다** —
`unity command get_performance_stats`(Unity 공식 `com.unity.pipeline`)가 `monoUsed`/`monoHeap`/
`cpuFrameTime`/`drawCalls`를 구조화해 준다. **게임 안에서 할당이 0이라 계측 되먹임이 구조적으로 없다.**
게임 코드는 한 줄도 안 고쳤다. `[[driving-both-clients-via-unity-cli]]`

두 클라를 CLI로 몰아 매칭 → 게임 진입 후 **30초 간격 5분** 샘플링:

| 초 | monoUsedMB | monoHeapMB | cpuFrameMs | drawCalls |
|---|---|---|---|---|
| 0 | 1255.7 | **1711.7** | 10.0 | 218 |
| 30 | 1254.8 | **1711.7** | 68.8 | 298 |
| 60 | 1341.2 | **1711.7** | 95.3 | 291 |
| 90 | 1284.8 | **1711.7** | 62.1 | 242 |
| 120 | 1280.6 | **1711.7** | 20.2 | 187 |
| 180 | 1379.2 | **1711.7** | 13.8 | 161 |
| 240 | 1356.0 | **1711.7** | 13.3 | 158 |
| 270~300 | — | — | — | 24 *(매치 종료, 로비 복귀)* |

**읽으면:**
- **누수 없음** — `monoHeap`이 1711.7MB로 **완전히 고정**(한 번도 안 늘었다). `monoUsed`는 1255~1379를
  오르내리는 정상 GC 톱니이고 **우상향이 없다.**
- **프레임이 나빠지지 않았다 — 오히려 좋아졌다.** 30~90초에 68~95ms로 튀었다가 13~26ms로 안정.
  로드맵이 말한 "2분 뒤 붕괴"와 **반대**다.
- **초반 스파이크가 지금 데이터가 가리키는 유일한 실체** — 그 구간 `drawCalls` 298 → 이후 157로 감소.
  **적 스폰 구간의 부하**로 보이며, 위 "동시 스폰 히칭"과 같은 건일 가능성이 높다.

> ⚠️ **이 측정은 "에디터에선 문제없다"만 말한다.** 원래 붕괴 데이터는 **실기기 dev 빌드**에서 나왔고,
> 그때는 진단 로그 되먹임이 섞여 있었다. 실기기 문제가 없다는 뜻이 **아니다** — 오히려 "붕괴의 얼마가
> 계측 탓인지 모른다"던 당시 의심을 키운다.

**다음 후보(값어치 순):** ① **실기기 dev 빌드에 같은 방법 적용**(진짜 답, 빌드·기기 필요) ·
② 에디터에서 **15~30분 장시간**(느린 누수 확인) · ③ **초반 스폰 스파이크** 파고들기(지금 바로 가능).

### 같이 확인된 별건 — 동시 스폰 히칭 → **위 ①과 같은 건**

적 10마리 동시 스폰 시 `fps` 60→53, `frameMs` 18.7, `snapGapMax` 121ms(체감 "살짝 끊김").
**`corrections`는 0** — 입력 파이프라인 3층이 받아내 위치는 안 어긋난다. 별건인 줄 알았으나
**프레임 히칭이라는 같은 뿌리**이며, 위 ①의 프레임 예산 트랙에서 함께 다룬다.
`[[recon-entity-load-parked]]`의 "동시 스폰은 나눠 넣을 것"과 같은 건이며 미수정.

---

**같은 창에서 스폰 위치도 어긋난다** — 반대 방향으로도 관측됐다(`predPos=(0,0,0)` vs 서버 5.5m).
클라 로컬 엔티티가 **스폰 위치를 받기 전 원점에 놓여 있는 동안** 오차 계측이 5.5m를 찍는 것으로,
정렬되면 사라진다. 다만 이건 시계 문제가 아니라 **초기 스폰 경로가 둘인 문제**일 가능성이 있다 —
초기 엔티티는 `GameInfoToC`에 실려 오고 이후는 `EntitySpawnToC`로 들어온다(같은 일을 두 경로로 한다).
**추정이고 코드로 확인한 적 없다.** 두 경로를 하나로 합치는 정리를 할 때 같이 본다 — 별도 트랙 아님.

---

## ✅ 플레이어 빌드 환경 선택 (2026-08-10, main 머지)

CI가 만든 APK가 **항상 `local-k8s`(= `http://localhost`)를 봤다.** 폰에서는 자기 자신이라 아무것도
안 됐고, 고를 방법 자체가 없었다(`#else` 분기가 상수 하나로 고정).

**두 겹이 막고 있었다.** 환경 고정이 하나, 그리고 `insecureHttpOption=DevelopmentOnly` — 릴리스
APK는 dev의 평문 http를 통째로 차단한다. **빌드는 성공하고 실행하면 죽는** 그 실패 모양이다
(07-30 게임서버와 동일). 그래서 개발 빌드로 뽑기로 했다. 프로젝트 세팅을 안 건드리니
`DevelopmentOnly`가 뜻 그대로 남고, 로그·프로파일러가 살아 **빌드에서 넷코드를 재보려던 미검증
항목**도 이 빌드로 처리할 수 있다.

**방식**: 선택한 `EnvironmentSettings.<env>.asset`을 빌드 직전 `.active`로 복사하고 후에 지운다.
검토한 셋 중 **커밋된 파일을 하나도 안 건드리는 유일한 길**이라 골랐다 — Preloaded Assets는 유니티가
이 용도로 둔 공식 API지만 `ProjectSettings.asset`에 저장돼, 실수로 커밋되면 모든 빌드가 조용히 그
환경으로 나간다(실제로 이 프로젝트는 **이미 그 항목을 쓰고 있어** 남의 항목과 섞일 뻔했다).
⚠️ "커밋된 SO를 메모리에서 수정"은 **동작 자체를 안 한다** — 빌드 중 도메인 리로드가 디스크 상태로
되돌린다.

- `-buildEnv <이름>` 필수 + `-development` 플래그. 누락 시 **빌드 실패**
- 굽기/치우기는 `EnvironmentBaker` 하나가 소유. CLI는 `BuildScript`가 `BuildPlayer` **호출 전**에,
  GUI 빌드는 훅이 부른다. 훅을 CI에 안 맡긴 이유 = `OnPreprocessBuild`에서 만든 Resources 자산이
  빌드에 포함된다는 보장을 유니티가 하지 않는다("timing may be tight")
- S3 경로를 환경별로 갈랐다 — 안 가르면 dev 아닌 APK 한 번에 **콘텐츠 baseline이 조용히 덮인다**
- 빌드 결과를 **QR로 잡 요약에 게시**(폰으로 찍어 설치). `data:` URI는 GitHub이 걷어내므로 PNG를
  S3에 올려 참조하고, 이미지가 안 떠도 되도록 링크를 함께 남긴다

**최종 리뷰가 잡은 진짜 버그**: 훅이 "이미 구워져 있으면 건너뛴다"를 **파일 존재**로 판단했다.
GUI 빌드가 실패하면 후처리가 안 돌아 `.active`가 남고, 그 다음 빌드가 환경을 바꿔도 낡은 것을
그대로 쓴다(gitignore돼 `git status`에도 안 보인다). 판단 기준을 **CLI 인자 유무**로 교체.
이 브랜치가 막으려던 실패 모양이 브랜치 자신에게 있었다.

**검증(실측)**: 인자 누락 → exit 2 · 없는 환경 → exit 2 + 잔여물 0 · `-buildEnv dev -development`
→ `구움 → APK OK(env=dev, development=True) → 치움`, exit 0, APK 69MB. 머지 후 main에서 컴파일
클린을 **리플렉션으로 실물 확인**(콘솔 0건은 도메인 리로드가 비운 것일 수 있어 신뢰 안 함).
프로세스를 강제 종료했을 때 `.active`가 남는 것도 확인 — 설계대로이고 다음 `Bake()`가 먼저 지운다.

**끝-끝 확인 완료(같은 날)**: CI 실행 → 잡 요약의 QR 렌더링 → 폰으로 스캔·다운로드·설치 →
앱에 dev 로비 주소 표시 → `POST /lobby/auth/anonymous` **404**. 그 404가 성공의 증거다 —
요청이 서버까지 가서 "그런 주소 없다"를 받았다는 뜻이고, `local-k8s`(=`localhost`)를 보고 있었다면
폰에서 자기 자신을 찔러 연결 자체가 안 됐을 것이다. dev 백엔드가 인증 cutover(08-04) 이전이라
익명 로그인 라우트가 아직 없다(`/lobby/auth/anonymous`·`/lobby/auth/login` 둘 다 404 실측,
`GET /lobby/`는 200). **클라 쪽은 더 손댈 게 없고, dev 최신화만 남았다.**

### ⚠️ 함께 터진 것 — 프리사인 URL이 시크릿 마스킹에 잘려 전부 깨졌다

첫 CI 실행에서 **QR도 링크도 접근 거부**였다. 원인은 S3도 권한도 아니었다 —
**SigV4 프리사인 URL은 구조상 액세스 키 ID를 `X-Amz-Credential`에 담는데**, 그 값이 GitHub
시크릿으로 등록돼 있어 **잡 요약에서 `***`로 마스킹**됐다. 서명이 깨져 AccessDenied.

진단이 세 번 빗나갔고 **매번 측정이 갈랐다**:
1. "카멜(이미지 프록시)이 쿼리스트링을 못 받는다" → 프로브로 쿼리스트링 이미지가 **정상 렌더링**됨을 확인, 기각
2. "요약이 안 써졌다" → 러너 로그에 write 실행 확인. 실제로는 **사용자가 로그아웃 상태**여서 안 보였던 것
   (공개 저장소도 로그·잡 요약은 로그인 필요. 화면의 "Sign in to view logs"가 단서였다)
3. "마크다운이 `&`를 `&amp;`로 망친다" → 사용자가 복사해준 URL에 `&`는 멀쩡, 대신 `***`가 보임 → **확정**

프로브로 S3 쪽 결백도 실측했다: HeadObject 성공 · Content-Type `image/png` · 프리사인 URL을
**러너에서 curl하면 HTTP 200**. 즉 URL은 멀쩡하고 *게시된 사본만* 손상돼 있었다.

**해소**: `AWS_ACCESS_KEY_ID`를 Secrets → **Variables**로 이전(`AWS_SECRET_ACCESS_KEY`는 시크릿 유지).
⚠️ **시크릿을 지워야 한다** — 워크플로가 `vars`를 써도 *등록된 시크릿 값*이 출력에 나타나면 계속
마스킹된다. 되돌리지 말라는 경고를 `client-app-deploy.yml` 헤더에 박았다.

> **교훈**: 프리사인 URL을 CI에서 게시하려는 것과, 그 URL에 든 키를 시크릿으로 등록하는 것은
> 서로 모순이다. "권한 문제"처럼 보이지만 `aws s3 presign`은 **권한을 검사하지 않는다** —
> 서명만 계산한다. 업로드가 됐다고 읽기 권한이 있다는 뜻도 아니고, 반대로 URL이 안 먹는다고
> 권한 문제인 것도 아니다.

**범위 밖(의도)**: Addressables 프로파일은 `dev` 고정 · 환경별 번들 ID/앱 이름 · 나머지 환경 자산을
`Resources/` 밖으로 · iOS(맥 러너는 이미 있고 `-buildEnv` 배관도 재사용되지만, Apple Developer
가입·서명·TestFlight가 별건이고 QR 대신 `itms-services://` 매니페스트가 필요).

spec `2026-08-10-player-build-environment-selection-design.md`,
plan `2026-08-10-player-build-environment-selection.md`.

---

## ✅ 유저 위치 조회를 서비스 하나로 (2026-08-15, main 머지 `f6a27f0`)

매치메이킹 FSM과 UI가 **각자** 유저 위치를 조회·해석하던 것을 `UserLocationService` 하나가 조회하고
나머지는 구독하는 형태로 정리했다. **위치 컨셉·FSM 구조·상태 이름은 그대로** — 배선만 바꿨다.

| 닫은 것 | 그 전 |
|---|---|
| 스토어가 값 변화를 안 알림 | `UserDataStore.userLocation`이 평범한 필드 → 소비자가 스토어를 못 쓰고 우회 |
| UI가 전송 객체(DTO)를 구독 | `MatchLoadingViewModel(ISubscriber<GetUserLocationResponse>)` |
| 폴링 주인이 없음 | `CheckMatch`(3회 재시도)·`InMatchmaking`(1초 루프+5회 포기)이 각자 HTTP·각자 정책 |
| 티켓 id를 받아놓고 버림 | 취소가 마지막 폴링 결과에 의존 → 요청 직후 취소 시 헛바퀴 |

**하지 않은 것(검토 후 명시적 제외)**: FSM 제거·티켓 축 재설계(Unity 공식 샘플 Boss Room의
`ConnectionManager`가 같은 구조[상태 6개]이고, 우리 위치 축은 PlayFab
`ListMatchmakingTicketsForPlayer`[복구 경로]를 상시 경로로 쓰는 변형이라 표준에서 벗어난 게 아니다) /
`locationDetail` 타입 강화·push·TTL·매치 종료 시 위치 정리(백엔드 몫).

spec `2026-08-14-user-location-service-design.md`(§8에 구현하며 드러난 것),
plan `2026-08-14-user-location-service.md`. 6태스크 subagent-driven + 태스크별 리뷰 + 최종
whole-branch 리뷰(opus).

### ⚠️ 최종 리뷰가 지목했는데 "ship"으로 넘겼다가 인게임에서 터진 회귀

`InMatchmaking`이 구독 즉시 받는 **캐시된 위치**로 판단해 대기 화면에서 곧바로 이탈했다. 그 캐시는
**요청 직전에 읽은 `None`** 이다 — 매칭 요청 응답은 위치를 갱신하지 않고, 서버 반영은 *다음* 조회에서야
보인다. 사용자에겐 **매칭이 스스로 취소되는 것**처럼 보였고, 다시 누르면 서버가 중복 티켓으로 거절
(`INVALID_TO_MATCH_MAKING` 10000)했다. 옛 코드는 캐시를 안 보고 1초 뒤 **새로 조회**해서 무사했다.

**수정**: 구독 즉시 replay되는 첫 값을 건너뛴다(`bdc438c`). **최종 리뷰가 이 위험을 M4로 정확히
지목했는데 "ship, 인게임에서 실증"으로 넘긴 것** — 그 판정이 틀렸고 인게임 게이트가 잡았다.
**교훈: "백엔드 타이밍에 걸려 있다"는 리뷰 지적은 ship 판정 대상이 아니다.**

### 검증 (서버 로그 실측)

| | |
|---|---|
| 매치 성사 | `[director] match created … players: 2` ✅ |
| 대기 중 취소 | ✅ |
| **게임 진입 후 폴링 0회** | ✅ 응답이 Matchmaking→GameRoom으로 바뀐 직후 조회 멎음. spec 위험 항목 종결 |
| **요청 직후 즉시 취소**(의도된 유일한 동작 변화) | ✅ 클라 로그에 `"User is not in matchmaking."` **0건** + 서버에서 POST와 DELETE가 **같은 초**에 짝지어 끝남(옛 코드였다면 취소 실패 후 위치 재확인 한 바퀴) |
| 매치 종료→로비 복귀 | ✅ **확인됨(08-15)** — 후속 트랙에서 정상 종료 ×3, 결과 창 유지 + 종료 후 `/joinable` 재조회 0건. 위 [매치 종료 시 유저 위치 정리](#-매치-종료-시-유저-위치-정리-2026-08-15-3레포-머지) 참조 |

### 이 트랙에서 얻은 도구·함정 (durable)

- **클라 컴파일 게이트를 에디터 없이 돌리는 법** — UnityMCP가 안 붙는 환경에서, Unity가 `Library/Bee`에
  남긴 응답 파일(defines·참조·소스 목록)을 Unity 번들 Roslyn(`DotNetSdkRoslyn/csc.dll`)에 그대로
  먹이면 된다. 6태스크 내내 이 게이트의 판정이 **Unity 실제 컴파일과 일치**했다. `dotnet build`는
  SDK 미설치라 불가. ⚠️ "응답 파일에 없는 `.cs`를 덧붙이는" 로직을 `Assets` 전체에 걸면 **자기 asmdef를
  가진 다른 어셈블리(Mirror 등)** 까지 끌어와 컴파일이 통째로 깨진다 — `Assets/Scripts`로 한정할 것.
- **MPPM 가상 플레이어는 메인 에디터와 다른 환경을 들고 있을 수 있다.** 메인만 `local-k8s`로 바꾸자
  클론은 `dev`에 남아 두 클라가 **다른 백엔드**에 붙었고 매칭이 성립하지 않았다(director `players: 1`).
  EditorPrefs는 이미 바뀌어 있었는데 클론이 못 집어갔다 — **클론 재시작으로 해소.**
  판별법: 각 인스턴스 콘솔의 `[LOP] environment=… lobby=…`.

---

## 🏁 매치 기록 통합 + 전적 목록 트랙 종결 (슬라이스 1~3, 2026-08-21)

**한 문장:** 판 하나를 `Match` **한 행**에 자기완결적으로 담아 전적 조회를 5쿼리에서 1쿼리로 만들고,
그 위에 LoL 전적 보기식 목록을 올렸다. **끝났다.**

spec `docs/superpowers/specs/2026-08-21-match-record-consolidation-design.md`,
plan(1) `docs/superpowers/plans/2026-08-21-match-record-consolidation-slice-1.md`.

### 왜 하게 됐나

전적 화면을 만들려고 데이터를 찾아보니 **"매치 결과"라고 부를 표가 없었다.** 판 하나가 `Match`(판당 1)
· `MatchRound`(라운드당 1) · `MatchParticipant`(**사람당 1**)에 흩어져, 20판을 읽는 데 5쿼리가 들었다.
이 표들은 전적을 보여주려고 만든 게 아니라 매치를 *진행하기 위해* 만든 것이고(확정 자물쇠·명단 게이트),
읽기용으로 설계된 적이 없었다.

**더 중요한 건 "그때 이름"이 없던 것이다.** 조회 시점에 `User.username`을 끌어오면 누가 개명하는 순간
과거 전적이 소급해서 바뀐다. 그리고 **이건 나중에 못 고친다** — 안 박아둔 과거의 이름은 복원 불가다.

### 잠긴 결정 (다시 논의하지 말 것)

| 결정 | 근거 |
|---|---|
| **`Match` 한 행 = 자기완결 기록** (`playerList` / `rounds` / `result`) | **Riot Match-V5**가 정본 — 한 매치 = 문서 하나, `info.participants[]`에 성적 **+ `riotIdGameName`**. 이름을 매치 안에 박는다 |
| **불변 기록은 비정규화한다** | 가변 상태는 정규화가 맞지만, 확정된 매치는 다시 안 바뀌므로 한 행에 담아도 어긋날 곳이 없고 "그 시점의 사실"이 통째로 보존된다 |
| **명단은 매치 생성 때 확정, 이후 불변** | 이 성질이 게임서버의 명단 위조를 막는다. **표가 아니라 쓰기 시점이 만드는 성질**이라 표를 합쳐도 유지된다 |
| **슬라이스 A가 표를 나눈 이유는 소멸** | 근거가 *"문자열 배열엔 참가자별 결과를 못 붙인다"* 였다. 결과를 객체 목록으로 담으면 그 제약이 없다 — 번복이 아니라 전제가 바뀐 것 |

### ✅ 슬라이스 1 — 스키마 + 쓰기 경로 (완료, 2026-08-21)

머지: backend `ab03437` (18파일, 표 2개 삭제) / Client `5924520` (문서).

**응답 DTO 무변경** — 도메인 모델이 이미 `playerList: string[]` + `rounds[]` 모양이라, 리포지토리가
세 표에서 읽어 도로 조립하던 코드가 사라지는 것이 변경의 대부분이었다. 덤으로 로드맵에 있던
"`MatchParticipant`에 FK 없어 고아 행" 부채가 **표와 함께 소멸**했다.

**실검증(local-k8s):** 마이그레이션 84건 이전(확정 3건만 `result`) · 표 2개 소멸 · 실플레이 한 판에서
`mmrBefore`가 직전 판 `mmrAfter`와 일치(**스키마를 통째로 바꿨는데 고리가 안 끊겼다**) · 등수를 뒤집어
재보고해도 저장값 불변(`gamesPlayed`·`updatedAt` 정지).

> ⚠️ **최종 리뷰가 내 주장을 반증했다.** 계획에 "게임서버·클라 변경 없음"이라고 썼는데, 응답 DTO는
> 그대로여도 **명단의 순서**가 바뀌었다. 옛 `findById`는 참가자 표를 `userId ASC`로 읽었는데 표를
> 합치며 저장 순서를 주게 됐고, **게임서버가 그 인덱스로 스폰 자리를 배정**한다. 매퍼에서 정렬해
> 복원. `[[plan-claims-become-unexamined-premises]]`
>
> 함께 고친 둘(에러 없이 나중에 터지는 부류): schema의 `rounds @default("[]")`가 마이그레이션의
> `DROP DEFAULT`와 어긋나 `create`에서 필드를 빠뜨려도 컴파일이 통과하던 것 · 상속받은 `saveAll`이
> 엔티티를 통째로 upsert해 `playerList`·`result`를 덮을 수 있던 뒷문(호출처 0이지만 다음 사람이 연다).

> ⚠️ **파괴적 마이그레이션이 구버전 파드와 겹친다.** `db-migrate`는 ArgoCD `PreSync` 훅이라 **새 파드가
> 뜨기 전에** 돈다. 이번엔 표를 DROP했으므로 그 창에 플레이했으면 매칭 생성·결과 확정이 전부 실패했다.
> 로컬 2인이라 "배포 중 안 하기"로 넘겼지만 **라이브에선 실제 다운타임**이다 →
> 파괴적 변경은 **비파괴 → 배포 → 파괴** 2단계로. `[[backend-deploy-ordering-traps]]`

### ✅ 슬라이스 2 — 전적 조회 라우트 (완료)

머지: backend `a3bc892`. `GET /user/{userId}/matches?limit=20` — 본인이 낀 끝난 판을 최신순으로,
**한 쿼리로**(`playerList: { has: userId }`). 인가는 레이팅 조회와 같다.

`limit`은 상한 50으로 자르되 **범위를 벗어나면 거절하지 않고 기본값으로 되돌린다** — 전적은 읽기라
400을 주면 화면만 빈다. 단위 6건(clamp 경계) + 통합 7건.

통합 테스트는 확정을 **진짜 경로**(`MatchResultService.confirm`)로 통과시킨 뒤 조회한다 — `result`를
손으로 넣으면 실제로 쓰이는 모양을 안 지나 `[[tests-must-traverse-the-real-path]]`. 그중 하나가 이
트랙의 존재 이유를 고정한다: **계정 이름을 바꿔도 전적의 이름은 안 바뀐다.**

### ✅ 슬라이스 3 — 클라 전적 목록 (완료)

머지: Client `c124ea3`. 프로필의 큐별 요약 아래에 판 카드를 `ScrollView`로 보여준다.

```
플랩왕                    08/21 20:23
캐주얼 · 플랩왕 맵
2등  -57
1등  Guest-05a6f594
2등  나
```

- 이름은 전적에 박힌 **확정 시점 값**을 쓴다(결과 화면이 "플레이어 N"으로 매기는 건 그 자리엔 이름이
  없어서고, 여기는 있다). 게스트 이름이 `Guest-<uuid>`라 앞 12자만
- **큐·맵을 덧붙인 것은 사용자 피드백**이다 — 모드 이름("플랩왕")만으로는 "어느 판이었나"가 안
  드러난다. 응답의 `queueId`/`rounds[].mapId` + 클라 마스터데이터로 해석하므로 백엔드 무변경
- **전역 발행(`WebAPI.SendAsync`)을 안 탄다** — 구독할 스토어가 없는 타입을 발행하면 조회는 성공한 뒤에
  예외가 난다(슬라이스 D1에서 물린 그 지점)
- 전적 조회 실패가 위의 요약을 죽이지 않게 따로 감쌌다

> **드래그 스크롤은 버그가 아니다.** `ScrollView`의 속성 이름 자체가 `touchScrollBehavior`이고 기본값이
> `Clamped` + `mode=Vertical`이라 **터치에서는 밀린다.** 에디터에서 마우스로는 원래 안 되고,
> 확인하려면 `Window > General > Device Simulator`를 켠다.

---

## 🏁 매치 결과 + 레이팅 트랙 종결 (A~D2, 2026-08-17 ~ 08-21)

**한 문장:** 한 판이 끝나면 등수를 남기고 그 결과로 실력 점수를 갱신해, 다음 매칭이 그 점수로 사람을
붙이는 고리를 닫는다. **트랙이 끝났다.** 고리가 닫혔고(C) 결과 화면(D1)과 프로필(D2)에서 보인다.
실플레이 3판이 끊김 없이 이어졌다 — 각 판의 `mmrBefore`가 직전 판 `mmrAfter`와 정확히 일치.

spec `docs/superpowers/specs/2026-08-17-match-result-rating-design.md`,
plan(A) `docs/superpowers/plans/2026-08-17-match-result-rating-slice-a.md`.

### 잠긴 결정 (다시 논의하지 말 것)

| 결정 | 근거 |
|---|---|
| **레이팅 엔진 = OpenSkill (Weng‑Lin)**, npm `openskill` MIT | 2~8명 FFA가 1급 시민(Elo·Glicko는 28쌍으로 쪼개야 함) · 실력을 `μ+σ` 두 값으로 봐 신규 수렴이 빠름 · TrueSkill은 같은 계열이지만 **MS 특허·비상업 라이선스**라 못 씀 |
| **결과 보고 = 게임서버 → lobby-server 직접** | PlayFab이 "통계는 서버 권위 경로로만"이라 못 박은 자리. Open Match는 **끝난 경기가 명시적으로 범위 밖** → 매치메이킹·룸서버는 결과 흐름에서 빠진다 |
| **3층 분리** `μ/σ`(엔진만) ↔ `mmr`(매칭이 읽는 정수) ↔ 표시값(티어, 범위 밖) | 디렉터가 정수 하나만 읽으므로 **매칭 코드 무변경** |
| **캐주얼도 점수를 갱신한다** | `has_visible_rank`는 *보여주느냐*의 플래그일 뿐 — 숨은 MMR을 굴려야 캐주얼 매칭 품질이 생긴다 |
| **결과 확정은 조건부 갱신(CAS)으로 정확히 한 번** | 조회-후-확인은 원리적으로 못 막는다(대기표 유일성에서 겪은 그것) → `[[invariant-as-primary-key]]` |

### ✅ 슬라이스 A — 스키마·어휘 재정비 (완료·배포·실플레이 검증)

머지: backend `0957efa` / Server `9d83ade` / Client `d033672`. 스트랭글러 7태스크(새 표 추가 → 소비처
이전 → 옛 것 삭제)로 **동작 무변화**를 유지하며 진행.

- `UserStats` → **`UserRating`**(`mu`/`sigma`/`mmr`/`gamesPlayed`/`firstPlaces`/`placementSum`).
  `eloRating`·`mmr` 중복과 안 쓰던 `tier` 제거, **FFA에 맞지 않던 승/무/패를 등수 지표로** 교체
- `Match`에 생애(`state`/`startedAt`/`endedAt`)와 `targetMmr`, 참가자별 **`MatchParticipant`** 신설
- **명단 진실원본이 `MatchParticipant`로 이전** — `Match.playerList` 컬럼 삭제, 응답 DTO의
  `playerList`는 참가자에서 파생(게임서버 방 접속 인증 계약 유지)
- 참가자 행은 매치 생성 시 `placement=null`로 **미리 깔린다** → 슬라이스 C의 결과 보고가
  *명단을 만드는 게 아니라 빈 칸을 채우는 일*이 되어, 게임서버가 남의 userId를 끼워 넣을 수 없다

**실검증(로컬 k8s, 클라 2인스턴스):** 로그인 → 매칭 → **룸 진입 성공.** 드리프트 점검
`prisma migrate diff` → "No difference detected". 레거시 백필 정확(유저 × 2 = 레이팅 행). 라이브 매치의
참가자 2행이 `placement` NULL로 생성됨(마이그레이션 백필이 아니라 새 코드 경로).

> ⚠️ **조용히 달라진 것 하나:** 명단 순서가 티켓 순서 → `userId` 오름차순으로 바뀌었다. 게임서버가
> 인덱스로 스폰 위치를 뽑으므로(`position = Vector3.right * i * 5`) **누가 어디서 시작하는지가 달라진다.**
> 실질 영향은 없지만 "무변화"라고 말할 때 빼놓으면 안 되는 항목.

### 남은 슬라이스

| | 무엇 | 비고 |
|---|---|---|
| ~~**B**~~ | ~~`@lop/rating` 순수 패키지~~ | ✅ **완료(2026-08-19, `030fb24`)** — 아래 절 참조 |
| ~~**C**~~ | ~~결과 보고 + 멱등 확정 + 점수 갱신~~ | ✅ **완료(2026-08-20)** — 아래 절 참조 |
| ~~**D1**~~ | ~~결과 화면 등수표 + 내 점수 변화~~ | ✅ **완료(2026-08-21)** — 아래 절 참조 |
| ~~**D2**~~ | ~~프로필 전적 (판수·1등·평균 등수·현재 MMR)~~ | ✅ **완료(2026-08-21)** — 아래 절 참조 |

### ✅ 슬라이스 B — `@lop/rating` (완료, 2026-08-19)

OpenSkill(Weng‑Lin)을 감싸 **`initialRating` / `rateMatch` / `toMmr`** 셋만 노출하는 순수 패키지.
DB도 HTTP도 모르므로 엔진을 갈아도 이 파일만 바뀐다. **아직 부르는 곳은 없다**(배선은 C).
단위 테스트 10건 — 1등↑/꼴등↓, 등수 순 정렬, 판수가 쌓이면 σ 감소, 동점=무승부, 1명 이하는 무변화,
입력 객체 불변, 그리고 **신규 유저 = 정확히 1000**(앵커).

- `mmr = round(40×(μ−3σ)) + 1000`. openskill `ordinal(r, {z:3, alpha:40, target:1000})`이 이 수식을
  **인자로 그대로** 표현한다 — 우리가 산수를 복제하지 않는다.
- 빌드 산출물(CJS)을 순수 node로 직접 밟아 확인했다: `mu 25 / sigma 8.333333333333334` → `toMmr 1000`,
  1승 후 1138 / 1패 후 927. **ts-jest는 TS 소스를 돌리므로 그것만으로는 배포되는 경로를 안 지난다.**

> ⚠️ **openskill 패키징 함정(박제).** CJS 산출물(`index.cjs`)과 CJS 선언(`index.d.cts`)을 둘 다 담고도
> `exports`가 `types`를 **조건 밖 맨 앞**에 둬서, `require`로 들어와도 타입은 ESM 선언으로 해석된다
> → `moduleResolution: node16`에서 **TS1479**. v4도 같아 다운그레이드는 답이 아니다. 런타임은 멀쩡하고
> 타입 라벨만 틀린 경우라 `tsconfig.paths`로 타입 해석만 `.d.cts`로 돌렸다(이유는 그 자리에 주석).
> **ts-jest는 이걸 통과시키고 `tsc`만 잡는다** — 테스트 초록을 빌드 통과로 읽으면 안 되는 사례.

**앵커 중복은 아직 남아 있다(의도).** `UserRatingFactory`(lobby-server)와 Prisma 기본값이 `mu 25 /
sigma 25/3 / mmr 1000`을 각자 들고 있다. 묶으려면 lobby-server가 `@lop/rating`을 의존해야 하는데,
**도커파일이 패키지를 선택적으로 복사**해서(`packages/{database,server-core}`만) 다음이 함께 필요하다:

```dockerfile
COPY packages/rating ./packages/rating          # lobby-server Dockerfile
RUN pnpm --filter @lop/rating run build         # lobby-server build 앞에
```

값이 이미 동일해 지금 옮겨도 기능 이득이 0이고, **C가 어차피 그 의존을 필요로 한다**(`rateMatch` 호출).
그래서 도커 빌드 검증과 함께 C에서 한 번에 한다. `[[unbuilt-image-hidden-breakage]]`

**⚠️ C 착수 전에 반드시 정리할 것 —** `MatchDaoPostgres.saveWithRounds`의 `tx.match.upsert({ update: match })`가
`state`/`startedAt`/`endedAt`을 **매번 덮는다.** 같은 매치를 다시 저장하면 `state`가 `Created`로 되돌아가는데,
**그 컬럼이 바로 C의 결과 확정 자물쇠(CAS)** 다. 슬라이스 A에선 재저장이 없어 무해해서 주석만 남겼다.

기타 C/D 메모: `MatchParticipant`엔 `Match` FK·`onDelete`가 없다(매치 삭제 시 고아 행) / 참가자 조회
인덱스는 `@@unique([matchId, userId])` 선두 컬럼으로 커버됨 / "명단에 없으면 거절"의 기준 명단은
`findParticipantUserIds`이며 이미 정렬돼 있다 / D 전에 `queueId` 누락 시 400으로 가를 것.

### ✅ 슬라이스 C — 결과 보고 + 멱등 확정 + 점수 갱신 (완료, 2026-08-20)

머지: backend `5e9fcd1` / Server `fd8f585`. plan `docs/superpowers/plans/2026-08-19-match-result-rating-slice-c.md`.

게임서버가 방을 닫기 직전에 등수를 로비로 보고하고, 로비가 **한 트랜잭션에서** 매치 확정 · 참가자
기록 · `UserRating` 갱신을 같이 한다. 확정은 정확히 한 번 — 재보고는 계산을 다시 하지 않고 저장된 결과를
그대로 돌려준다.

**실검증(local-k8s, 클라 2인스턴스 1:1 한 판):**

| userId | 등수 | mmr | μ | σ |
|---|---|---|---|---|
| 3e53c0c1… | 1 | 1000 → **1138** | 25 → 27.635 | 8.333 → 8.066 |
| 4d7ff8f3… | 2 | 1000 → **927** | 25 → 22.365 | 8.333 → 8.066 |

손으로 검산해 수식과 일치함을 확인했다(`(μ−3σ)×40+1000` → 1137.5→1138, 926.7→927). μ 이동이 ±2.635로
대칭이고 σ가 양쪽 똑같이 줄었다 — 동일 사전분포 1:1의 OpenSkill 정답 형태라 우연히 맞은 값이 아니다.

**멱등성은 등수를 뒤집어 재보고해 증명했다** — 패자를 1등으로 보냈는데도 응답은 저장된 원래 결과를
돌려줬고, `gamesPlayed`도 `updatedAt`도 안 움직였다. 고리가 닫혔는지는 **매칭이 실제로 부르는 그
엔드포인트**(`GET /user/{id}/rating?queueId=1`)를 매칭 파드 안에서 직접 쳤다 — 1138/927 반환, `μ`/`σ`는
응답에 없음(3층 분리 유지).

> ⚠️ **배포가 아니었으면 몰랐을 버그 하나.** 게임서버 CI 빌드가 `ConfigureRoomComponent.cs(49): 'Match' does
> not contain a definition for 'targetRating'`로 즉사했다. 슬라이스 A의 개명에서 이 호출부 하나가 빠졌는데,
> 그 파일이 **로컬 픽스처(테스트 uuid)와 같은 파일**이라 항상 unstaged였고, 내 워킹트리엔 고쳐진 상태가
> 섞여 에디터에선 멀쩡히 컴파일됐다. 게다가 A 이후 게임서버 이미지를 **한 번도 굽지 않아** CI가 볼 기회가
> 없었다. 수정 라인만 떼어 커밋(`c6c7e5f`)하고 uuid는 unstaged로 되돌렸다. 픽스처와 진짜 수정이 한 파일에
> 섞이면 이렇게 숨는다. `[[unbuilt-image-hidden-breakage]]` `[[deletion-slices-verify-backwards]]`

**배포 순서 주의(재발 방지).** 인프라 태그가 올라간 뒤에도 **클러스터는 잠시 옛 것을 그대로 들고 있다.**
이번에도 로비 파드가 `0957efa`로 돌고 있었고, 게임서버 ConfigMap은 `9418e2c`에 멈춰 있었다. 그 상태로
플레이하면 **옛 게임서버가 떠서 보고 자체가 안 나간다.** 판정은 `kubectl exec deploy/room-server -- printenv
GAME_SERVER_IMAGE`로 하고, 뒤에 kind 노드 프리풀(`crictl pull`)까지 해 두면 첫 매치가 이미지 받다 지체하지
않는다. `[[argocd-gitops-cluster-rebuild]]`

> ⚠️ **D 착수 전 필수:** `ResponseCode.INVALID_MATCH_RESULT = 20001`이 **백엔드에만** 있다. 클라의
> `ResponseCode.cs`에 같은 번호를 추가해야 양쪽 어휘가 맞는다.

### ✅ 슬라이스 D1 — 결과 화면 (완료, 2026-08-21)

머지: Shared `9e0d8dc` / Server `6bb8f40` / Client `dc443b2`.
plan `docs/superpowers/plans/2026-08-20-match-result-rating-slice-d1.md`.

"매치 종료" 한 줄이던 화면이 **참가자 등수표 + 본인 점수 변화**를 띄운다. 빈 메시지였던
`MatchEndedToC`에 필드를 얹고(기존 MessageId 불변), 게임서버가 **수신자마다 다른 메시지**를 만든다 —
등수는 전원, 점수는 본인 것만. 점수 필드가 참가자별이 아니라 **최상위 단수**라, 남의 실력 점수를
실으려면 proto를 고쳐야 한다: 프라이버시가 관례가 아니라 **구조로** 강제된다.

**실검증(local-k8s, 3판 연쇄):**

| 판 | 3e53c0c1 | 4d7ff8f3 |
|---|---|---|
| 1 | 1000 → 1138 (1등) | 1000 → 927 |
| 2 | 1138 → 1248 (1등) | 927 → 875 |
| 3 | 1248 → 1140 | 875 → 1033 (1등) |

각 판의 `mmrBefore`가 직전 판 `mmrAfter`와 정확히 일치 — 스토어가 제때 비워지고 새 결과로 채워진다는
것과 고리가 계속 돈다는 것이 함께 증명됐다. 3판에서 **875가 1248을 이겨 +158**, 진 쪽은 -108만 잃은
비대칭도 레이팅 엔진이 실제로 도는 신호다(고정값이면 안 나온다).

**표기 규칙(닉네임 없음):** 본인 "나", 나머지는 등수 순으로 "플레이어 1·2…". userId를 그대로 띄우지
않기 위한 것이며, 닉네임은 별개 기능이다.

> ⚠️ **최종 리뷰가 잡은 Important — 세 번째 같은 패턴.** `FrontEndCoordinator`가 결과 스토어를
> **[확인]을 눌렀을 때만** 비우고 있었다. 확인 전에 창이 닫히는 경로(로비 스코프 teardown 등)에서
> 결과가 남고, **다음 판 통보를 못 받은 클라**(서버는 세션 전송 실패를 catch하고 넘어가며
> `LOPSession.Send`는 `isConnected == false`면 조용히 no-op)가 로비로 오면 **지난 판 등수와 점수를
> 이번 판 것으로 믿고 본다**(`matchId`는 담아만 두고 아무도 대조하지 않는다).
> **원인은 이 브랜치 이전, 피해는 이 브랜치가 만들었다** — 화면이 "매치 종료" 한 줄일 땐 재등장이
> 무해했다. 수정: `Clear()`를 창 여는 직후로 이동(VM이 이미 값을 복사해 가므로 표시 무영향).
> `[[harmless-code-turns-harmful]]`
>
> 함께 넣은 잠복 가드: 백엔드 `alreadyConfirmed` 응답이 null을 0으로 메우므로, 등수가 1 미만이면
> 결과 전체를 버린다. 지금은 도달 불가(확정 트랜잭션만 `Finished`를 씀)지만 다른 경로가 Finished를
> 만들면 조용히 "0등"이 뜬다.

**⚠️ D2 착수 전 확인:** 클라 `ResponseCode.INVALID_MATCH_RESULT = 20001`은 D1에서 추가했다(해소됨).

**테스트 0건은 의도된 결정이다.** 표시 로직(정렬·이름·증감 포맷)은 개념적으로 ViewModel이 하는 일
그 자체라, 테스트를 붙이려고 별도 asmdef를 파지 않았다 — 경계를 만들면 와이어 타입을 베낀 입력
타입과 매핑 층이 따라붙고, 그것들의 존재 이유가 오직 어셈블리 경계가 된다. 진짜 병목은 "Unity 앱
asmdef 도입"(인증 트랙 이월 후속 2번)이며, **그 항목의 동기를 "테스트를 못 붙인다"가 아니라 "레이어
경계를 컴파일 타임에 강제한다"로 다시 써야 한다** — 지금 문장대로 착수하면 테스트가 필요한 자리마다
경계가 그어진다. `[[fixtures-hide-real-fixes]]`

### ✅ 슬라이스 D2 — 프로필 전적 (완료, 2026-08-21)

머지: Client `6e980b5`. 제목만 있던 프로필 셸이 **큐별 전적 점수·판수·1등 횟수·평균 등수**를 보여준다.
기록 없는 큐는 0이 아니라 "아직 기록이 없습니다" — 매칭이 아직 캐주얼(queueId=1)만 써서 랭크는
당분간 그 상태다.

**열 때마다 다시 받아온다.** 레이팅을 로그인 때 한 번만 받고 있어서(`LoadUserComponent`), 그대로
두면 판을 하고 프로필을 열어도 **로그인 시점의 낡은 값**이 떴다. D1이 결과 화면에서 올바른 값을 보여준
직후에 프로필이 옛 값을 보여주면 그게 더 이상하다.

**셸 UXML 분리:** 상점·설정·프로필이 `ShellView.uxml` 하나를 공유하고 있어, 프로필만 전용 UXML로 갈랐다
(`UIViewCatalog`의 ProfileView 항목이 그걸 가리킨다). 템플릿을 안 쓰는 프로젝트라 셸 크롬 6줄이
복제된다 — 나머지 셸에도 내용이 생기면 그때 템플릿이나 카탈로그 `bodyUxml` 필드가 값을 한다.

**D1과 달리 R3를 썼다.** "불러오는 중 → 도착"이라는 진짜 라이브 상태가 있어서다. 라이브 상태가 없던
결과 화면에 R3를 안 쓴 것과 같은 기준이다.

> ⚠️ **리뷰가 잡은 Critical — VContainer는 `Transient`를 Dispose하지 않는다.**
> `Container.ResolveCore`가 `Singleton`/`Scoped`만 추적하고 `Transient`는 `default:`로 빠진다.
> ViewModel(Transient)에 취소 토큰을 달아 "화면이 닫히면 HTTP를 끊는다"고 설계했는데 `Dispose`가
> 영영 안 불려 **취소 배선 전체가 죽은 코드**였다. 정리 주체는 DI가 아니라
> `WindowManager.Close → view.Dispose()`이고, View가 `Dispose()`를 오버라이드해 VM을 정리하는 것이
> 이 레포의 확립된 패턴이다(`StatsView`/`LoginView`). `[[di-cleanup-vcontainer]]`
>
> 함께 고친 것: `.shell-body`의 `align-items:center`가 `.profile-body`에 남아 라벨과 값이 붙어
> 보이던 것 · 평균 등수 소수점이 OS 로캘을 타 "3,5등"이 될 수 있던 것.

**남은 후속(작음):** 큐 목록을 `TbQueue`에서 읽기(지금은 id 하드코딩 — 로비 선택 UI 슬라이스 몫) ·
랭크 매칭이 생기면 그 슬라이스에서 함께 확인.

### 미뤄둔 Minor

~~`targetMmr`의 `DEFAULT 1000` 제거~~ ✅ **완료(2026-08-23)** — 기본값이 **두 겹**이었다(`MatchFactory`의 1000 + DB `@default(1000)`).
디렉터는 HTTP 라우트를 안 거치고 서비스를 직접 부르므로 `CreateMatchDto`의 `@IsNumber()`도 그 경로엔 안 걸려,
**막는 곳이 실질적으로 없었다.** 팩토리는 타입으로 강제(`Partial<Match> & Pick<Match,'targetMmr'>` — 빼먹으면 컴파일 에러),
DB는 default 제거. 기존 행은 값이 있어 데이터 변경 없음.

~~`saveWithRounds` 인자 묶기~~ · ~~`participantCreateMany` 목~~ — **소멸(확인 2026-08-23).** 둘 다
매치 기록 통합(08-21)이 그 함수·테이블을 없애면서 사라졌는데 이 줄만 남아 있었다.

**이 트랙에서 배운 것:** `[[build-gate-claims-need-cache-bypass]]` · `[[deletion-slices-verify-backwards]]`

---

## 🏁 플레이어 신원 트랙 종결 — 표시 이름 + 태그 (슬라이스 1~2, 2026-08-22)

**한 문장:** `Guest-<uuid>` 대신 사람이 읽는 이름을 주되, **이름 중복은 허용하고 신원은 태그가 가른다.**
**끝났다** — 가입하면 태그가 붙고, 프로필에서 이름을 바꾸면 전적에 새 이름이 박힌다.

머지: 슬라이스 1 = Backend `0530ddd` · Client `cefa827` / 슬라이스 2 = Client `ce53af1`.
spec `docs/superpowers/specs/2026-08-21-player-identity-design.md`,
plan `docs/superpowers/plans/2026-08-21-player-identity-slice-1.md`.

### 왜 하게 됐나

앞 트랙(매치 기록 통합)이 전적에 **확정 시점 이름**을 박게 만들었다. 그런데 박을 이름이
`Guest-3f2a…`였다 — 전적 화면을 만들어 놓고 정작 거기 뜨는 게 uuid 조각이었다.

### 잠긴 결정 (다시 논의하지 말 것)

| 결정 | 근거 |
|---|---|
| **이름은 유일하지 않다. 태그가 신원을 가른다** | Riot이 2023년에 소환사명(유일) → `이름#태그`로 옮긴 그 모델. 유일 이름은 선점(name squatting)을 만들고, 늦게 온 사람이 자기 이름을 못 쓴다 |
| **태그는 계정에 고정** — 가입 때 부여, 개명해도 안 바뀐다 | 이름이 바뀌어도 신원이 이어져야 친구 찾기·전적 대조가 성립한다. Riot도 태그는 계정 축 |
| **Crockford Base32 6자리** (`0-9A-Z`에서 `I`·`L`·`O`·`U` 제외) | `I/L/O`는 `1/0`과 눈으로 안 갈리고 `U`는 우연히 욕이 된다. 32⁶ ≈ 10억 — 판단 기준은 *수용량이 아니라 열거 저항*이었다(전수 조사로 남의 태그를 훑기 어려울 것) |
| **개명은 형식 검증만. "이미 사용 중" 실패가 존재하지 않는다** | 유일성을 안 거는 게 이 설계의 존재 이유다. 통합 테스트가 *같은 이름을 두 계정에 거는 것*을 명시적으로 단언한다 — 여기가 깨지면 어딘가에 유일성 검사가 들어간 것 |
| **전적에는 `이름#태그`를 통째로 박는다** | 이름만 박으면 동명이인이 구분되지 않는다. 전적은 "그때 누구였나"를 남기는 것이므로 태그까지 있어야 신원이 된다 |

### ✅ 슬라이스 1 — 스키마 + 태그 + 개명 API + 와이어 (완료)

- `User.username` → `displayName`(유일 아님) + `tag`(유일). 마이그레이션이 기존 11계정에 이름 `플레이어` + 각자 다른 태그를 채운다
- 가입 때 태그 부여, `P2002`(유니크 위반)면 최대 5회 재시도 — **조회-후-생성이 아니라 충돌-후-재시도**다(`[[invariant-as-primary-key]]`)
- `PUT /user/{userId}/display-name` — 본인 또는 서비스만. 실패 사유는 형식(`30001`)뿐
- 전적 쓰기 경로가 `displayName#tag`를 박는다. **개명해도 지난 전적은 안 바뀐다**(통합 테스트가 지킨다)
- 클라 DTO를 같은 이름으로 맞춤. 화면 변화는 없다 — 이 슬라이스는 *기존 흐름이 그대로인지*를 본다

> ⚠️ **마이그레이션에서 11계정 전부가 같은 태그를 받았다.** 상관 없는 서브쿼리는 Postgres가
> 한 번만 평가해(InitPlan) 재사용한다 — `random()`이 안에 있어도 소용없다. `WHERE "User"."id" = "User"."id"`로
> 바깥 행을 상관시켜 행마다 다시 돌게 했다. **계획이 이 함정을 예측하고 "인덱스를 약화시키지 말 것"까지
> 적어뒀는데도 구현이 밟았다.**

> ⚠️ **최종 리뷰가 두 건을 뚫었다.** ① `ShortName`이 12자에서 자르고 있어 새 19자 `이름#태그`가
> `김철수김철수#K7QM`으로 잘렸다 — **그럴듯하지만 틀린 신원**이 화면에 뜬다. ② 이름 검증이 `\s`만 봐서
> `U+3164`(한글 채움문자)·`U+200B`·BEL·`U+202E`(RTL 오버라이드)가 통과했다. 둘 다 *태스크 사이*에
> 앉아 있어 개별 리뷰가 못 봤다.

### 함께 고친 UI 결함 (같은 브랜치, 실플레이 로그에서 발견)

**`WindowManager.Close`가 같은 뷰에 두 번 불리고 있었다.** 로비 스코프가 내려갈 때 *뷰 팩토리를
해제하는 쪽*과 *코디네이터*가 서로 상대가 닫았는지 모른 채 각각 닫는다. 두 번째 호출이 이미 dispose된
`CancellationTokenSource`를 `Cancel`해서 터졌다. `Close`가 **아직 열려 있는 뷰만** 정리하도록 고쳤다 —
`OnClose`에서 이벤트를 쏘는 뷰가 있어(`MatchmakingFailedView.Closed`) 닫기 절차 자체가 한 번만 돌아야 한다.

**`UIView`의 Dispose를 문서화된 .NET 패턴으로 맞췄다.** `[[dispose-pattern-verify-against-doc]]`

### ✅ 슬라이스 2 — 개명 UI (완료, 2026-08-22)

머지: Client `ce53af1`. **백엔드 변경 0** — 슬라이스 1의 라우트에 클라 진입점이 생긴 것뿐이다.
프로필 상단에 `이름#태그`가 뜨고, [이름 바꾸기]로 모달을 띄운다.

**응답은 이 레포의 표준 경로를 탄다.** `WebAPI.SendAsync`가 전역 발행 → `UserDataStore`가 구독해
`user` 갱신 → 프로필이 모달이 닫힌 뒤 스토어를 다시 읽는다(`GetUser`와 같은 모양). 모달 VM은
스토어를 직접 안 만진다 — 진실이 둘이 되지 않게.

> 중간에 "스토어를 VM이 직접 갱신하는" 지름길을 제안했다가 **철회**했다. 한 엔드포인트만 다른
> 방식으로 하면 같은 일을 하는 길이 두 개가 된다. 브로커 등록 + 구독자 추가는 `RootLifetimeScope`와
> `UserDataStore`에 각 한 줄씩이다.

**`user`의 ReactiveProperty 승격은 미뤘다.** `IUserDataStore`는 `userLocation`만 RP고 나머지는
평범한 프로퍼티인데, 그 주석이 기준을 명시한다 — *"바뀌는 걸 알아야 하는 소비자가 있어"*.
지금 이름을 읽는 화면은 프로필 하나뿐이고 그 프로필이 변경을 일으킨 당사자다.
**승격 조건: 두 번째 화면이 이름을 표시하기 시작할 때**(로비 홈 상단 등). 그때 `user.id`를 읽는
7군데가 함께 바뀌고, `LoginComponent`처럼 **객체 안을 직접 고치는** 쓰기부터 정리해야 알림이 나간다.

**모달 규칙 두 가지** — ①백드롭 클릭으로 안 닫힌다(`AutoClose => false`): 입력하던 이름이 말없이
사라지는 것보다 [취소]를 누르게 하는 편이 낫다. ②실패해도 안 닫힌다: 결과를 확정하면 닫히므로
형식 위반은 안내만 띄우고 열어둔다(로그인 모달과 같은 규칙).

**클라 검증은 서버 규칙의 복제다.** 왕복을 아끼려는 것이고 진짜 게이트는 서버다 — 서버가 거절하면
클라가 뭘 통과시켰든 그 결과를 따른다. 길이는 서버와 같이 **코드포인트**로 센다.

**검증(실플레이):** 상단 신원 표시 · 개명 후 상단 즉시 갱신 · 형식 위반 차단 ·
**한 판 뒤 전적에 새 이름, 옛 판은 옛 이름** — 마지막 항목이 이 트랙 전체의 끝‑끝 확인이다.

### 남은 후속 (작음)

- `user` → `ReactiveProperty` 승격 (위 조건 충족 시)
- 결과 화면의 이름 — 와이어(`MatchEndedToC`)에 이름이 없어 지금도 "플레이어 N". proto를 고쳐야 하므로 spec §11대로 범위 밖
- 욕설 필터 · 태그로 친구 찾기 — 별개 트랙

---

## ▶ 다음 (Next — 순서 있음)

### 🏁 인증 트랙 종결 (2026-08-04 ~ 08-10) — 익명 로그인 → cutover 1a~2b 전부 완료

> **여기부터 읽으면 된다.** 트랙 전체가 끝났다. 슬라이스 0(HTTP 계층) → 1a(토큰 갱신) →
> 1b(유저 JWT 강제) → 1c(방 접속 인증) → 2a(세션 신원을 연결 기준으로) → 2b(내부 전용 라우트 차단).
> **지금 백엔드에 무인증으로 열린 라우트는 없다** — 익명 가입·로그인·헬스체크뿐이다. 상세는 아래
> 각 슬라이스 항목(시간순, 2b가 가장 아래에서 두 번째).
>
> **인증 트랙에서 이월된 후속** (값어치 순):
> 1. **서비스별 내부 키 분리 + 순환 + 감사 추적** — 지금은 키가 하나라 어느 서비스가 불렀는지 모르고,
>    새면 클러스터 안의 내부 동작이 전부 열린다(인그레스는 바깥만 막는다).
> 2. **Unity 앱 asmdef 도입** — 두 앱 프로젝트에 asmdef가 없어 앱 코드에 유닛 테스트를 못 붙인다.
>    2a·2b 두 슬라이스가 연속으로 이 한계를 안고 갔다(리뷰와 라이브 검증으로만 확인).
> 3. 커밋된 `.env`에서 서명키 제거 + `.dockerignore`
> 4. 세션 인수 시 옛 연결을 명시적으로 끊기(동시 접속 정책 = 게임 디자인 결정)
> 5. `characterId` 소유권 검증 / 토큰 폐기(revocation)
> 6. 잔챙이: 룸 라우트 테스트 양성 대조, `ApiKeyHandler`·`BearerTokenHandler`의 빈 키 테스트가
>    `""`를 안 봄, `GET /internal/user/findAll?ids=<단일>`이 빈 목록(실호출자는 axios가 `ids[]=`로
>    직렬화해 정상이라 머지 차단은 아니었음)
>
> **다음 트랙은 이 아래 다른 `###` 섹션에서 고른다** — 인증 때문에 막힌 건 더 없다.


**✅ 익명 로그인 + 세션 토큰 (2026-08-04, 3레포 머지)** — 게스트 계정을 서버가 발급하고
액세스 토큰(HS256, 1시간)을 내려준다. `User` 1:N `UserIdentity`(provider + providerUserId),
자격증명은 기기에 저장해 재로그인에 쓴다(리프레시 토큰 없음 — PlayFab 모델). 백엔드
`lop-backend@58d813e`, `GameFramework@1c8184d`(Jwt·AccessTokenInfo·자격증명 저장소, 테스트 29건),
클라 `@efdf140`(로그인 팝업·`AuthenticationService`). spec
`2026-08-04-anonymous-auth-session-design.md`.
**단 아직 아무도 토큰을 검사하지 않는다 — 인증이 절반만 켜진 상태다.**

**✅ 슬라이스 0 — HTTP 클라이언트 계층 표준화 (2026-08-06, 3레포 머지)**.
GameFramework `8c3661f` · 클라 `c4455f4` · 서버 `e30f2e1`. EditMode 412/0(신규 22), 양쪽 컴파일 0.
수동 검증: 로그인→로비 ✅ / 매치 경로 HTTP 전 구간 ✅ / **오프라인 계정 보존 ✅**(백엔드 차단 후
재기동 시 자격증명 보존, 복구 후 동일 userId 재로그인 확인 — 이 리팩터의 존재 이유).
**미검증 이월**: 게임 씬 진입(이 머신이 kind가 아니라 docker-desktop이라 룸 UDP 포트 미공개 —
Mirror transport는 이 브랜치가 한 줄도 안 건드림) / 서버 런타임 전반(배포 게임서버가 핀된 이미지라
새 이미지 빌드 전엔 실행 안 됨). **첫 이미지 빌드 시 스모크 3건 필수**: 서버 런타임, `#if UNITY_EDITOR`
의 `#else` 경로(배치 컴파일이 안 덮음), 하트비트 cadence·룸 상태 전이.
후속: 죽은 seam 정리(`UserDataStore.HandleCreateUser`·`CreateUserResponse` 등록·고아 DTO 3종) /
클라 브로커 5종 미등록(첫 모바일 IL2CPP 빌드 전) / `HttpJson`의 `TypeNameHandling.Auto` 결정.

> (원 계획) 아래는 착수 시점 기록. cutover의 토큰 갱신·401 재시도를 넣을
자리가 **구조적으로 없어서** 먼저 한다. `WebRequest<T>`가 생성자에서 전송하고 인터셉터가 동기라
`await`도 재전송도 불가능. 겸사겸사 같은 자리의 결함들을 정리한다 — 연결실패와 4xx가 한 덩어리로
뭉개진 것(**계정 유실 버그의 뿌리**), awaiter 3종이 `using GameFramework;` 유무로 갈리는 landmine,
취소·타임아웃·테스트 부재. **.NET `HttpClient` + `DelegatingHandler` 구조를 1:1로 옮긴다.**
3레포(GameFramework → 클라 → 서버), 호출부 22곳, GameFramework EditMode 테스트 9건.
spec `2026-08-06-http-client-layer-standardization-design.md`.

**슬라이스 1은 1a/1b/1c로 쪼갰다.** 갱신(1a)이 강제(1b)보다 먼저다 — 검사를 켜는 순간 1시간 넘는
세션이 전부 깨지기 때문. 결정 원본은 `2026-08-06-auth-cutover-decisions.md`(§8에 1a 구현에서
확정된 1b/1c 요구사항 추가).

**✅ 1a — 클라 토큰 갱신 (2026-08-06, 2레포 머지)**. GameFramework `4ea47f1` · 클라 `4a186d9`.
EditMode 426/0(신규 14). `IAccessTokenProvider` 포트 + `BearerTokenHandler`의 미리 갱신·401 재시도
1회·"토큰 그대로면 재전송 안 함" 가드 + `SingleFlight`(동시 갱신 접기) + `Throttle`(강제 갱신 최소
간격 30초). 갱신은 원래 **호출자가 0곳인 죽은 코드**였다.
수동 검증: 회귀 없음 ✅ / 실서버 갱신 13회 전부 200에 뒤따르는 요청도 전부 200, userId 동일 ✅.
**미검증 이월**: 401 재시도 경로(서버가 아직 401을 안 줌) → **1b 배포 시 필수 확인**.
spec `2026-08-06-auth-cutover-1a-client-token-refresh-design.md`.

**✅ 1b — 서버 강제 + 인프라 (2026-08-07, 3레포 머지)**. infrastructure `cd71127` · lop-backend `22be359`
· 클라 `44f7fdc`. 빌드 5/5, 로비 10단위/36통합, 매칭 168단위/22통합.
`로비 입장`·`매칭 요청`(본문 `userId` 제거→토큰 신원)·`매칭 취소`(대기표 주인 대조 403)를 닫았고,
`/auth/*` 레이트리밋(anonymous 30 / login 200, 엔드포인트별 분리) + `trust proxy 1` + 서명키를
k8s Secret으로 옮겼다. spec `2026-08-07-auth-cutover-1b-server-enforcement-design.md`.

**최종 리뷰가 Critical을 잡았다 — 대표 주장이 거짓이었다.** `DELETE /user/:id`가 **인증 없이** 열려
있었고, `GET /user/all`이 전체 계정 목록을 주므로 userId를 알 필요도 없었다. 지워진 유저는 재실행 시
로그인이 401 → 클라가 자격증명을 지우고 새로 가입 = **전 플레이어 영구 계정 유실**. 호출자 0곳인
`DELETE /user/:id`·`POST /user`·`PUT /user/profile`을 `PUT /lobby/leave/:id`와 함께 삭제해 닫았다.

**교훈(원장 박제): 코드를 지우는 작업은 테스트 통과로 컴파일이 보장되지 않는다.** 삭제된 DTO를
import하는 파일이 남아 `lobby-server`가 컴파일 안 됐는데 4개 스위트가 전부 통과했다 — 아무도 그
파일을 import하지 않아 ts-jest가 타입 검사를 건너뛰기 때문. CI는 테스트보다 **먼저** `turbo run build`를
돌린다. 검증 명령 맨 앞에 빌드를 넣을 것.

**✅ 배포·검증 완료 (2026-08-09)**. `gh workflow run backend-deploy.yml -f app=all`로 배포
(`workflow_dispatch`지만 `gh`로 실행 가능 — 이전 배포들도 전부 그렇게 했다) → 이미지
`re5nardo/*:22be359` → ArgoCD 동기화 → 파드 3개 Running. 8개 레포 전부 main 최신, EditMode 434/0.

검증 4건 전부 통과:
1. **정상 플레이** — 로그인→로비→매칭 요청→취소 전 구간 200, Unity 콘솔 에러 0.
2. **401 재시도** — **1a에서 만들고 한 번도 못 밟았던 경로를 여기서 처음 실증.** 임시로 서명이 깨진
   토큰을 실어 보내니 `PUT /lobby/join 401 → POST /auth/login 200 → PUT /lobby/join 200`이 **같은 1초
   안에** 돌았고 **사용자는 아무것도 못 느꼈다.** 임시 코드 제거·되돌림 확인.
   - *설계 함정*: 처음엔 "첫 인증 요청 1회만 망가뜨리기"로 했더니 `GET /user/{id}`(조회라 검사 안 함)가
     그 1회를 먹어치워 헛돌았다. 401이 실제로 날 때까지 망가뜨리도록 고쳐 잡음 — 스펙 §1의
     "조회는 안 닫는다"가 실물로 드러난 셈.
3. **레이트리밋** — 두 리미터가 각자 살아 있음(`RateLimit-Policy: 30;w=900` / `200;w=900`).
   **XFF 위조가 안 통한다**(다른 값으로 보내도 같은 버킷에서 카운터가 줄어듦 — nginx가 덮어씀) →
   `trust proxy 1`이 의도대로 동작. 한도 소진 429는 통합 테스트가 고정(라이브 소진은 계정 30개를
   만들어야 해 생략).
4. **서명키 일치** — `POST /matchmaking 200`이 곧 증거(로비가 서명한 걸 매칭이 검증).

**라이브 경계 확인**(배포 후 밖에서 직접 찔러봄): `DELETE /lobby/user/__probe__`·`POST /lobby/user`·
`PUT /lobby/user/profile` → **전부 404**(이전엔 500/400 = 핸들러 도달). 토큰 없는
`PUT /lobby/join`·`POST /matchmaking`·`DELETE /matchmaking/:id` → **전부 401**.

후속(스펙 §13): `GET /user/all` 삭제(**받아들인 조회 라우트가 아니라 아직 안 지운 고아** — 호출자 0곳) /
내부 전용 변경 라우트 차단(`PUT /user/location` 등) / 레이트리밋 키를 계정 단위로 / 커밋된 `.env`에서
키 완전 제거 + `.dockerignore` / 죽은 UserProfile 배관 정리 / 통합 테스트 앱 조립을 `main.ts`와 공유.

**✅ 2b — 내부 전용 라우트 차단 (08-10, 4레포 main 머지·배포·라이브 검증 완료)**. 인증이 걸린 라우트가
`PUT /lobby/join`·`POST/DELETE /matchmaking`(1b)·`POST /auth/introspect`(1c) **네 개뿐**이었다. 나머지는
전부 무인증으로 인터넷에 열려 있었고, 배포 전 실측이 `PUT /room/heartbeat/probe` → **200(성공)**,
`GET /lobby/user/all` → **200**, `PUT /lobby/user/location` → 500(= 인증 통과, 핸들러 도달)이었다.
아무나 남의 위치를 바꾸고 방을 만들고 지울 수 있었다(OWASP API #5 BFLA, #1 BOLA).

- **가른 축 = "동작이 다른가, 권한만 다른가"**. 처음엔 경로 문제로 보고 `/internal` 일괄 분리를 검토했는데,
  M2M 표준(Auth0·Cognito 등)은 **서비스도 유저와 같은 엔드포인트를 부르고 스코프가 범위를 정한다**고
  권고하고, BeyondProd는 **신뢰를 네트워크 위치가 아니라 서비스 신원에서** 판단하라 한다. 그래서
  *같은 동작, 권한만 넓음*(조회 4개)은 경로를 유지하고 주체별 인가만 붙였고, *유저가 부를 일이 없는 동작*
  (9개)만 `/internal`로 옮겼다. 후자는 클라가 안 부르니 **중복이 안 생기고**, 덤으로 엣지에서 한 번에 막힌다.
- **신원 확인과 인가 분리** — `authenticatePrincipal`이 주체를 `service`/`user`로 정하고,
  `requireSelfOrService`가 "서비스면 전체, 유저면 본인만"을 판단(server-core). 규칙 셋이 load-bearing:
  ① 키가 틀리면 유저 토큰으로 **강등하지 않는다**(강등하면 키를 떠보면서도 정상 응답을 받는다),
  ② 키 헤더가 없으면 `INTERNAL_API_KEY`를 **아예 읽지 않는다**(읽으면 그 값이 빠진 서비스에서 클라 조회가
  전부 500), ③ 둘 다 있으면 키가 이긴다.
- **`GET /match/:id`는 "볼 수 없음"과 "없음"의 응답이 바이트 단위로 같다** — 다르면 매치 id를 넣어보는
  것만으로 실재 여부를 알아낼 수 있다. 둘이 같은 `if` 블록이라 구조적으로 갈라질 수 없다.
- **`router.use`는 반드시 경로 지정형** — `App`이 모든 라우터를 `/`에 마운트하므로 `router.use(mw)`는
  **앱 전체**에 걸린다. 등록 순서 = 실행 순서라 `use`가 라우트보다 뒤면 아무것도 안 막고 **조용히** 통과한다.
  introspect만 `use`보다 앞에 둔다(레이트리밋이 "키가 틀린 시도"를 세야 하는데 `use`가 먼저면 못 센다).
- **인그레스에서 `/internal` 차단** — 내부 호출은 클러스터 DNS로 직행해 인그레스를 안 거치므로 공짜다.
  키가 새도 인터넷에서는 못 쓴다. 경로 정규식에 부정 전방탐색, **전방탐색 안쪽은 비캡처 `(?:...)`**
  (캡처면 그게 nginx의 `$2`가 되어 `rewrite-target`이 깨진다).
- **게임서버는 `ApiKeyHandler`(GameFramework)로 자동 부착** — `BearerTokenHandler`의 형제. introspect에만
  손으로 박아둔 헤더가 사라졌다. 키는 **보낼 때마다 다시 읽는다**(환경변수가 프로세스 시작 뒤 채워질 수 있음).
- **삭제**: `GET /user/all`·`/user/username/:x`·`/room/all` (호출자 0곳, 컨트롤러·서비스 메서드까지).

spec/plan `docs/superpowers/specs|plans/2026-08-10-auth-cutover-2b-internal-route-lockdown*`.

**라이브 검증**(배포 후 밖에서): `/internal/*` 4종 → **전부 404**(인그레스), 대소문자 우회
(`/Internal`·`/INTERNAL`) → **404**, 옛 무인증 쓰기 5종 → **전부 404**, `GET /user/<남>` (내 토큰) → **403**,
`GET /user/<나>` → 200, 토큰 없음 → 401.

**끝-끝 게임 루프도 확인 완료**(로그인 → 매칭 → 방 입장, 2인). 로그로 전 경로가 추적된다 —
`PUT /internal/user/location` 200 → **`POST /internal/room` 201**(디렉터) → `GET /internal/room/<id>` 200 →
`PUT /internal/room/status` 200 → **`POST /internal/auth/introspect` 200 ×2**(게임서버가 두 명 검증) →
`PUT /internal/room/heartbeat/<id>` 200 (2초 주기). 게임서버 파드 이미지 `game-server:6b4bb35` 확인.
**네 서비스 전부 비-2xx 응답 0건.** 응답 크기가 증거를 더 준다: introspect 77B(성공 shape,
실패는 16B) / `GET /match/<id>` 239B(거부였다면 `{code}`만 담긴 15B대). 클라 대면
`GET /user/<id>/location/`이 서로 다른 두 userId로 각각 200 = 두 클라가 각자 본인 것을 읽었다
(남의 것이면 403) → **주체별 인가가 실사용에서 동작 확인**.

> **최종 리뷰가 막은 것**: 계획의 구멍으로 **룸 서버가 부팅 불가**한 상태로 머지될 뻔했다. Task 5가
> `joinable`에 로그인 검사를 붙이며 `AUTH_JWT_SECRET` 요구를 추가했는데 Task 9는 `INTERNAL_API_KEY`만
> 전달했다. envalid는 던지지 않고 `process.exit(1)`이라 `try/catch`로도 못 막고 CrashLoopBackOff → 게임 전체
> 정지였다. **태스크별 리뷰가 구조적으로 볼 수 없는 종류**(요구는 A태스크, 공급은 B태스크)라 전체 리뷰가 잡았다.
> 유인은 `validateEnv.ts`의 낡은 주석("AUTH_JWT_SECRET은 lobby만 필요 — 넣으면 room/matchmaking이 부팅 즉사").
>
> **오탐이었던 것**: 인그레스 정규식이 대소문자를 구분해 `/Internal`로 우회된다는 지적. ingress-nginx가
> `use-regex`에서 `location ~*` + `rewrite "(?i)..."`를 **무조건** 생성해 전방탐색까지 대소문자 무시다.
> 파이썬 `re`의 기본값이 만든 착시였고, 배포 후 라이브 프로브로 확정했다.
>
> **검증 한계(정직하게)**: 무력한 테스트를 **두 번** 잡았다 — ① `/user/all`의 대체 단언이 옛 열린 라우트에
> 대해서도 통과(200도, `body.user` undefined도, `count()===0`도 전부 픽스처를 잰 것), ② 매치 비공개 속성이
> 정책 함수 단위 테스트만 있고 배선 레벨엔 없었다. 둘 다 실제 회귀 모양을 심어 빨개지는 걸 확인한 뒤 닫았다.
> Unity 앱 코드(`WebAPI.cs`)는 여전히 asmdef가 없어 유닛 테스트 불가 — 컴파일 클린 + 리뷰 + 위의
> 끝-끝 루프로만 확인했다.

(`validateEnv.ts`의 낡은 주석은 **정리 완료** — 이번 Critical을 유도한 breadcrumb이었다. 예시를 현재
사실[디렉터]로 바꾸고, envalid가 `process.exit(1)`이라는 점과 "요구를 늘리면 k8s·로컬 공급도 늘려야
한다"를 남겼다.)

후속: 서비스별 키 분리·순환·감사 추적(지금은 키 하나라 호출자 구분 불가·유출 시 내부 전부 열림) / `GET /internal/user/findAll?ids=<단일>`이
빈 목록(`Array.from(문자열)`이 글자 단위 — 실호출자는 axios가 `ids[]=`로 직렬화해 정상, 머지 차단 아님) /
룸 서버 라우트 테스트에 양성 대조 추가 / `ApiKeyHandler`·`BearerTokenHandler` 빈 키 테스트가 `""`를 안 봄 /
커밋된 `.env`에서 서명키 제거 + `.dockerignore` / **Unity 앱 asmdef 도입**(2a에서 이월).

**✅ 2a — 세션 신원을 연결 기준으로 (08-09, 2레포 main 머지·배포)**. 1c는 신원의 *출처*를 연결로 옮겼지만
그 값으로 다시 **계정 단위 세션 조회**를 했다. 그래서 재접속 중 실제 버그가 있었다 — connA가 죽은 걸
서버가 알아채기 전(kcp 타임아웃 10초) connB로 재접속하면 세션이 connB로 갈아타는데, 뒤늦게 도착한
connA의 해제가 **계정으로 같은 세션을 찾아 꺼버려** "다시 들어갔는데 아무 조작도 안 먹힘"이 됐다.

- **접속 메시지에서 주장한 userId 제거** — 자격증명(토큰)만 보낸다. Mirror 기본 인증기(`BasicAuthenticator`
  /`DeviceAuthenticator`)가 그 모양이고 주석이 OAuth면 accessToken을 넘기라 명시한다. 판정이
  `중복가드 → 명단 → introspect → sub 대조` 4단계에서 `중복가드 → introspect → sub가 명단에 있나` 3단계로.
  받아들인 비용: 명단 선검사가 사라져 소켓 하나가 로비 호출 하나를 유발(리미터는 실패만 세므로 안 걸림).
- **연결이 자기 세션을 가리킨다** — `ConnectionIdentity{UserId, SessionId}`를 `conn.authenticationData`에.
  `ISessionManager`(GameFramework)는 안 건드림 — Mirror 개념을 앱 비종속 계층에 넣지 않는다.
  부수효과로 액세스 토큰이 연결 수명 내내 남던 것도 사라짐.
- **`ClientMessage<T>`가 세션을 나른다** — 핸들러의 계정 기준 조회(던지는 인덱서) 제거.
- **해제·수신 양쪽에 `ReferenceEquals` 가드** — 수신 쪽은 최종 리뷰가 잡았다: 안 넣으면 좀비 연결의
  `GameInfoToS`에 대한 응답이 `session.Send`로 **산 연결에 배달**된다(엔티티 전체 덤프). 오귀속이 아니라 오배달.
- **인증 타임아웃 60초** — Mirror `TimeoutAuthenticator`와 같은 동작을 인증기 안에 구현(데코레이터를 끼우면
  씬 편집 + 클라의 `authenticator` 캐스트가 깨진다). **단 스톡 구현에 없는 참조 동일성 확인을 추가** —
  `Disconnect()`는 connectionId로 끊는데 kcp2k가 그 id를 재사용해, 그냥 두면 애먼 연결을 죽인다.
- **에디터 전용 클레임 리더** — 에디터는 introspect를 건너뛰므로(1c 결정) `sub`를 얻을 길이 없어졌다.
  서명 검증 없이 payload에서 `sub`만 읽고 파일 전체를 `#if UNITY_EDITOR`로 감쌌다. `#else`를 지우면
  조용한 우회가 아니라 **컴파일 에러**가 난다.

spec/plan `docs/superpowers/specs|plans/2026-08-09-session-identity-by-connection*`.

> **인과 정정(리뷰 지적)**: "조회 키를 계정→세션으로 바꿔서 경합이 구조적으로 불가능"은 **과장이었다.**
> 세션은 지워지지 않아(`RemoveSession` 호출부 0곳) 한 계정의 모든 연결이 **같은 세션 id**를 든다 — 늦게 온
> 옛 해제도 같은 세션을 찾아낸다. 실제로 막는 건 **연결 객체 비교 100%**. 조회 키 변경의 몫은 수신 경로에서
> 계정을 걷어낸 것과 항상-참이던 잘못된 캐스트 제거다. `connectionId`로 비교하면 kcp2k의 id 재사용 때문에
> 또 같아지므로 **객체 참조만이 유일하게 구분 가능한 축**이다.
>
> **검증 한계(정직하게)**: 두 Unity 앱 프로젝트는 asmdef가 없어 앱 코드에 유닛 테스트를 붙일 수 없다
> (EditMode 433건은 전부 패키지 것). 이 슬라이스의 검증은 **컴파일 클린 + 코드 리뷰 + 라이브 플레이**였고,
> 매달림·오배달·애먼 연결 끊기를 잡은 것은 전부 리뷰어의 눈이었다. 라이브로는 **정상 입장과 역순 재접속
> (정상 종료 → 즉시 감지)** 까지 확인했고, **문제의 정순(늦은 해제가 재접속보다 뒤에 도착)은 재현하지 않았다**
> — 재현하려면 프로세스 강제 종료 후 10초 안에 재접속해야 한다. 수정이 타이밍이 아니라 객체 동일성을
> 비교해 순서에 무관하고 리뷰가 세 순서를 모두 추적했으므로 **정적 검증으로 갈음**했다.

후속: **세션 인수 시 옛 연결을 명시적으로 끊지 않는다** — 같은 계정 두 연결이 동시에 열린 채 남고, 옛 쪽은
세션이 없어 아무것도 못 하지만 끊기지도 않는다(동시 접속 정책 = 게임 디자인 결정) / `InputTimingFeedbackSystem`
의 던지는 인덱서 + 죽은 널가드 / `EditorAccessTokenClaims`를 `JsonUtility`로 / **Unity 앱 asmdef 도입**.

**✅ 1c — 방 접속 인증 (08-09, 6레포 main 머지·배포·검증 완료)**. 이전엔 참가자 명단에 있는 userId만
주장하면 누구나 그 사람 자리로 들어갈 수 있었고(토큰 자리에 문자열 `"token"`이 들어가 검사 자체가 없었다),
접속 후에도 모든 인게임 메시지가 신원을 스스로 적어 보내 **한 참가자가 다른 참가자를 조종**할 수 있었다.

- **접속 인증**: 게임서버가 로비에 물어본다(RFC 7662 `POST /auth/introspect`). 판정 순서 **명단 → introspect
  → `sub` == 주장한 userId**. 명단이 먼저인 이유는 소켓 1회가 HTTP 1회로 증폭되는 걸 막기 위해서고,
  `sub` 비교가 사슬을 닫는다. 실패는 **전부 거부**(fail closed) — 로비 무응답·타임아웃(3초)·401·빈 본문·
  깨진 JSON·`active:false`·`sub` 불일치. `conn.authenticationData`에는 클라 주장값이 아니라 **`sub`** 을 저장.
- **열쇠 등급 분리**: 서명키(`auth-secret`)는 로비·매칭만, **조회 전용 키(`internal-api-secret`)** 만 게임서버
  파드에 `secretKeyRef`로 한 개. `envFrom` 금지를 **테스트로 강제**(`gameServerPod.test.ts`) — 서명키가
  방마다 뜨는 파드로 새는 걸 막는 자동 방어선.
- **인게임 메시지 신원**: 수신부가 이미 받아놓고 버리던 연결을 살려 `ClientMessage<T>{UserId, Message}` 봉투로
  핸들러에 전달. ToS proto 3종에서 신원 필드를 **물리 삭제**(+`reserved`) — 안 보내면 위조할 게 없다.
  Mirror 타입은 핸들러까지 번지지 않게 확인된 userId만 넘긴다.
- **`GameFramework.Auth.Jwt` 삭제** (파드에서 로컬 서명 검증을 하지 않으므로 사용처 소멸).
- **라이브 검증**: 정상 입장 → `introspect 200` 응답 **77B**(`{active,sub,exp}`) → `Accept`. 토큰을 일부러
  훼손 → 같은 엔드포인트가 **200 + 16B**(`{"active":false}`) → `[Auth] 접속 거부`. 이 16B/77B 대비가
  "가짜 토큰은 401이 아니라 200"과 "`active:false`엔 sub/exp 없음"을 동시에 증명한다. 외부에서 키 없이
  호출 → 401. IL2CPP AOT(`ClientMessage<T>` 브로커)도 실접속으로 해소.

spec/plan `docs/superpowers/specs|plans/2026-08-09-auth-cutover-1c-room-connection-auth*`.

> **계획이 틀렸던 지점들** (리뷰가 잡아 계획을 고친 것): ① 조회 키 오답 픽스처가 정답과 길이가 달라
> 상수시간 비교를 빼도 테스트가 통과 ② 검증을 `introspect`로 필터링해 돌려 `verifyAccessToken` 반환값에
> `exp`를 더한 게 옆 파일 테스트를 깨뜨린 걸 못 잡음 ③ `match`가 null이면 `try` 밖에서 NRE → `async
> UniTaskVoid`라 예외가 삼켜져 **연결이 수락도 거부도 아닌 채 매달림** ④ 빈 응답 본문이면 역직렬화가 null
> → 같은 매달림 ⑤ 중복 가드를 `connectionId`로 걸면 kcp2k가 id를 IP:포트 해시로 만들어 **재접속이
> 블랙홀에 빠짐**(키를 연결 객체로 교체).
>
> **배포에서 배운 것**: `room-server`가 `game-server-config`를 `envFrom`으로 읽는데 리로더가 없어
> ConfigMap 갱신으로 재시작되지 않는다. 실측 결과 재시작 전 값이 **7월 말 태그(`bbc4bc1`)** 였다 — 즉
> 그동안 태그 bump가 한 번도 반영된 적이 없었다. **실제 전환점은 `kubectl rollout restart
> deployment/room-server`** 이며, 워크플로 완료가 아니다. 계획서 배포 절차에 박제.
>
> **로컬 환경**: 이 맥은 Docker Desktop 내장 k8s를 쓰고 있어 방 포트(hostPort)가 맥으로 열리지 않아
> 접속이 불가능했다(웹 요청은 인그레스라 정상이라 "매칭은 되는데 로딩에서 멈춤"으로 보였다). 저장소에
> 이미 문서화돼 있던 대로 **kind `lop` 클러스터로 이행**(`k8s/local-k8s/kind-cluster.yaml`,
> `extraPortMappings` 7000~7009/UDP). ArgoCD·시크릿 재부트스트랩 포함 약 13분.

후속(스펙 §13): `characterId` 소유 검증 / 토큰 즉시 무효화 / 내부 전용 라우트 전반에
`internalApiKeyMiddleware` 확대(1b 후속과 합류) / 게임서버 HTTP 전반에 조회 키를 붙이는 `DelegatingHandler`
승격 / 에디터 introspect 예외 제거(로컬 시크릿 관리 도입 시) / Unity 앱 프로젝트 asmdef 도입(현재 앱 코드가
`Assembly-CSharp`에 있어 유닛 테스트를 붙일 수 없다) / introspect 엔드포인트의 인그레스 노출 차단.

### 프론트엔드 플로우 골격 (Slice A~D) — ✅ **트랙 종결(07-24)**
로그인 이후 화면 흐름(로비 홈 → 매칭 → 게임 → 결과)을 **3층 전환 모델**(씬=앱 FSM / 윈도우=코디네이터 / 화면 안 상태=VM)로 정리하는 트랙. spec `docs/superpowers/specs/2026-07-23-front-end-flow-skeleton-design.md`. **B·C·D·A 전부 완료·머지 — 트랙 종결.**

- ✅ **Slice B — 로비 홈 허브 (완료·머지 07-23)**: 로비 베이스 화면을 `LobbyHomeView`(Play + 하단 네비바 레이아웃)로 교체, `MatchmakingView` 은퇴(Play 역할 흡수), 매칭 대기 오버레이는 `MatchmakingCoordinator`가 담당. plan `2026-07-23-flow-slice-b-lobby-home.md`.
- ✅ **Slice C — 프론트엔드 네비(상점/설정/프로필 셸) (완료·머지 07-24)**: 네비바 버튼 배선. `LobbyHomeViewModel`이 네비 신호(`Observable<FrontEndDestination>`)만 노출 → 신규 `FrontEndCoordinator`가 구독해 셸 윈도우 push/pop(한 번에 하나). 셸 3종은 공유 `ShellView` 베이스 + 공유 UXML(제목만 다른 플레이스홀더). plan `2026-07-24-flow-slice-c-frontend-nav.md`. **셸 내용(상점 품목/설정 항목/프로필 데이터)은 화면별 후속 스펙.**
- ✅ **Slice D — 결과 화면 (완료·머지 07-24, 3레포)**: 매치 종료 통보 경로. 서버 `LOPRunner.EndMatch()` → `LOPRoom`이 전 세션에 신규 `MatchEndedToC`(빈 메시지) 브로드캐스트 → 클라 `MatchEndedMessageHandler`가 결과를 Root 스코프 `MatchResultDataStore`에 남기고 클라 `LOPRunner.EndMatch()` → 기존 `case GameOver`가 로비 씬 로드 → `FrontEndCoordinator`가 대기 결과를 보고 `MatchResultView`(플레이스홀더)를 한 번 띄우고 [확인] 시 Clear. 어휘 규약 확정(새 LOP 도메인 이름=match / 러너 상태 family=game 유지, 언리얼 `AGameMode`↔`EndMatch` 정합). spec `2026-07-24-flow-slice-d-match-result-design.md`, plan `2026-07-24-flow-slice-d-match-result.md`. **결과 내용(점수·순위)은 게임 모드 확정 후 후속.**
- ✅ **Slice A — 앱 FSM 씬 전환 일원화 (완료·머지 07-24)**: 흩어진 `LoadScene` 4곳을 Root 스코프 `AppStateMachine`(신규, GameFramework `StateMachine<AppEvent>` 위) 한 곳으로 일원화. 씬 페이즈 `Boot/FrontEnd/InMatch` + 신호 `BootCompleted`/`MatchFound`/`MatchEnded`, 씬 로드는 `ISceneLoader` 포트 뒤로(씬 이름 중앙화). 각 소스는 씬을 직접 로드하지 않고 신호만 Fire: `EntranceScene`→`BootCompleted`, 매칭 `InGameRoom`→`MatchFound`(+`RoomConnector` 로드 제거·이중 재시도 루프 정리), `LOPRoom` GameOver·에러→`MatchEnded`. 역할이 매칭 FSM에 흡수된 죽은 `CheckLocationComponent` 삭제. `AppStateMachine`은 `IStartable`(상속 `Start()`)로 앱 시작 시 기동+`AsSelf`로 자식 스코프 주입. spec `2026-07-24-flow-slice-a-app-fsm-design.md`, plan `2026-07-24-flow-slice-a-app-fsm.md`. **매치 자동 종료 트리거는 서버 `LOPRunner`의 경과 5분(`60*5`) 타이머(20초 아님).**

- ✅ **후속 — 매치 진입 로딩 커버리지 갭 (완료·머지 07-25)**: 트랙 종결 후 발견. 룸 연결(`InGameRoom`)~씬 로드~게임 준비 구간에 아무 오버레이도 안 뜨던 갭 해소. **MVVM-C**: Root `MatchLoadingViewModel`(유저 위치 `GetUserLocationResponse` 관찰 + 게임 라이브 사실 → `IsLoading = (위치==GameRoom) && !gameLive` 파생) + `MatchLoadingCoordinator`(`IsLoading` 구독 → `GameLoadingView` open/close, 씬 경계 넘어 뷰 소유) + `LOPGameSceneCoordinator`가 러너 `Playing`에 `NotifyGameLive()` **사실만 보고**(직접 open/close 제거). 새 매칭·재접속 두 경로 자동 커버, 매칭 측 무변경. spec `2026-07-25-match-loading-coverage-gap-design.md`, plan `2026-07-25-match-loading-coverage-gap.md`. `[[subagent-stray-edit-main-checkout]]`

> 화면 아트(타이틀/로비/로딩 배경)는 별도 `feature/ui-screen-art`로 들어옴 — 로비 배경은 은퇴한 `MatchMakingView.uss` 대신 `LobbyHomeView.uss`가 참조(07-24 머지 시 재배선).

### 엔티티 Unity 레이어 재구조화 — **트랙 종결(07-19)**, 파킹된 후속만 남음
World Core(순수 C# Entity/Component) 위 Unity 프레젠테이션을 **얇은 뷰/컴패니언 + Actor식 앵커**로 수렴하는 S1~S5 리팩터. **S1(설정 컴포넌트→World)·S2(PhysicsComponent→PhysicsFollower)·S3(레거시 substrate machinery 삭제)·③물리통합·S4(Unity 트리 표준화: LOPActor 루트 앵커+rb 루트+컴포넌트 co-location) 완료** → 엔티티=`Actor_{id}` 단일 루트(모든 behavior 컴포넌트, 모델=렌더 바디 자식), 문자열/구조 배선 소멸.

**S1~S5 완료 + 로직 분리 완료 + Actor 뷰 파사드 완료 + 엔티티 매니저 분리(ActorRegistry+EntitySpawner) 완료.** 엔티티 = 순수 C# `World.Entity`(데이터+시뮬), Unity = 얇은 프레젠테이션(Creator=데이터 직원 / 반응형 뷰 스포너=뷰 직원). 로직/시뮬도 `World.Entity`로 분리(LOPActor는 뷰 레이어 ~6곳만). 문자열/구조 배선·병렬 컴포넌트 시스템·파사드·레거시 과참조·뚱뚱한 매니저 소멸(아래 Done 원장).

- **Actor 뷰 파사드 ✅ 완료 (2026-07-19, main 머지 d9275bf)**: `LOPActor`를 엔티티의 **단일 대표(파사드)** 로 승격. 애초 아이디어("`LOPEntityView`를 Actor에 *통합*")는 brainstorm에서 **파사드로 선회** — 웹 리서치로 언리얼 Actor가 렌더러를 *소유·대표*(통합 아님)하고 유니티 `gameObject.transform`이 파사드-라이트 선례임을 확인 → `LOPEntityView`는 렌더링 전담으로 **유지**하되 `LOPActor`가 소유(`SetView`)+`visualGameObject` 위임 노출, 외부 소비처(playerContext/카메라/보간기2/월드스페이스 UI2)는 `entityView`→`actor` 단일 참조로 전환. spec/plan `2026-07-19-actor-view-facade*`. `[[entity-unity-layer-rearchitecture]]`.
- ~~**후속 후보 — `PhysicsFollower` 접기(공유 팩토리화)**~~: ✅ **완료(2026-08-23, 3레포 머지·실플레이 검증)** — 아래 파킹 표 참조.
- ~~**비차단 후속(기회 정리)**: register→PhysicsBody 순서 불변 주석 · S5b Task1 rationale 주석 복원.~~ ✅ **완료(07-19)** — 클라 `EntityBinder`에 (a) 뷰를 스포너로 분리해도 안전한 근거(동기 발행) 클래스 주석, (b) 등록→PhysicsBody 순서/동기 불변식 + `Add<PhysicsBody>` 제네릭 키 함정 주석 복원. 코드 동작 무변화(주석만). 서버 `EntityBinder`는 필요 시 별도. (~~`CharacterCreationDataCreator` param `lopEntity`→`actor`~~ ✅ 07-19 로직 분리 슬라이스에서 이미 `worldEntity`로 리네임됨 — stale 노트 제거.)

umbrella `docs/superpowers/specs/2026-07-18-entity-view-rearchitecture-umbrella-design.md`. 워크플로우: 각 슬라이스 brainstorming→spec→writing-plans→subagent-driven(컴파일=UnityMCP, 플레이=사용자). `[[entity-unity-layer-rearchitecture]]`.

### Stage④ 남은 트랙 (netcode-redesign.md §5 프론티어)

**A(클라 예측 전투)의 데미지 트랙은 닫혔다 (2026-07-12 결정).** 이동은 이미 예측(키네마틱+Reconciler), 어빌리티 발동도 예측(self-skip). **데미지는 예측하지 않고 서버권위 재생 유지** — 넷코드/예측 에픽은 자연 일단락. 아래 잔여는 대부분 **예측 콘텐츠 대기(B)** 또는 **독립 정리**다. 현재 진행 중인 항목 없음.

1. **클라 측 예측 전투 생성 (A)** — ⏸ **데미지 예측은 안 짓기로 결정(2026-07-12).** 데미지 숫자는 서버 `DamageEventToC` 재생 유지(남 캐릭터와 동일). 근거: HP는 스냅샷으로 항상 정확 → 틀릴 수 있는 건 떠오르는 숫자(연출)뿐이고, 이동(게임필의 큰 축)은 이미 예측됨 → 데미지 숫자 ~RTT 지연은 수용(빠른 슈터 표준, YAGNI). **재검토 조건:** 근접 타격감이 지연으로 답답해질 때.
   - 완료 잔재(헛되지 않음): A1 `DeterministicRandom`, A2.1 매치시드 클라 동기(**휴면·준비됨**), A2.2a/A2.2b 전투 공유화(**서버 EditMode 테스트 + 이중타격 dedup 버그교정 = 독립 이득**). 상세: 위 Done 원장.
   - 결정론 RNG(counter-based)·클라 combat RNG 소비는 데미지 예측과 함께 **휴면**. `[[deterministic-rng-counter-based]]`.

2. **B — 예측/확정 이벤트 machinery (해시 dedup)** — ⏸ **예측가치 있는 스킬이 올 때** 그 실제 사례 2~3종과 함께 짓는다. 근거: 재사용되는 "대조 원장"(틱도장+해시 dedup)은 예측 이벤트가 여럿일 때 값을 하고, 연출별 "취소"(예: 데미지 플로터 제자리 교체)는 종류마다 새로 만들어야 해 지금 하나로는 상각 안 됨. 방식 1(재생 억제, `WorldEventBuffer.Suppress()`)이 완료된 토대. 설계: `[[event-model-wire-decision]]`.

3. **`IInputSource` 표준 provider (4d)** — ⏸ A(예측 확장)에 묶여 함께 보류. 독립 wrap-only는 거부됨(2026-07-01, `specs/2026-06-30-slice4-input-source-port-design`).

4. ~~**통합 fan-out**(모든 World 상태 변경 → 이산 버퍼 → 한 곳에서 fan-out)~~ ✅ **종결 — 짓지 않는다 (2026-08-14).** 아래 참조.

> ~~독립 정리: reconciler-tick-guard `[임시]` 틱 가드 제거~~ ✅ **완료(07-13)** — 위 Done 원장 참조(브래킷 탐색 교체).

#### ✅ 통합 fan-out — 동기 소멸로 종결 (2026-08-14, 코드 확인)

**막던 위험이 후속 슬라이스들에 이미 닫혔다.** 이 항목은 2026-06-18 Mana 이행 spec의 durable 메모에서
태어났고, 걱정은 **"writer가 여럿인데 일부만 UI에 알린다"** 였다 — 당시 `World.Health` writer가
전투 / 이벤트 적용 / 스냅샷 셋인데 UI 신호(`EntityDamage`)는 전투 경로에서만 나가서, 서버 권위 보정으로
HP가 바뀌면 화면이 못 따라갔다.

그 전제가 두 결정으로 사라졌다:

| 없앤 것 | 언제 |
|---|---|
| 이벤트 적용(`WorldEventApplicator`) 삭제 + **HP 권위 = 스냅샷 단일화** | 06-22 (connection-arch backlog #1·#3) |
| **클라 데미지 예측 안 짓기로 결정** → 클라에 전투 writer 자체가 없음 | 07-12 `[[damage-prediction-dropped]]` |

**현재 실측**: 클라에서 `World.Health/Mana/Level/Stats`를 쓰는 곳은 **`GameEntityMessageHandler.cs`
한 파일 6곳이 전부**이고(`healthSystem.`/`manaSystem.`/`levelSystem.`/`statsSystem.` 전수 검색),
**전부 알림을 발행한다.** 누락 위험 0.

**남은 실체 = 그 한 파일 안의 국소 중복뿐** — `prev 저장 → Apply → 비교 → Publish` 4줄이 5~6번.
헬퍼 하나로 접으면 끝나는 정리이고, 아키텍처 일감이 아니다. 필요해지면 근처 손댈 때 같이 한다.

**같이 확인된 것 (구조는 이미 규칙대로다)**: 위치·회전·접지·시전 상태 = 매 프레임 pull /
HP·MP·레벨·스탯 = 스냅샷 적용 + UI엔 이산 알림 / 데미지 숫자·어빌리티 발동 = 이산 알림.
`EntityDamage`에는 **HP 값이 없다**(피격·크리·데미지량뿐) — connection-arch backlog #3의 "HP UI가
연출 이벤트에서 값을 읽는 잔여"도 이미 해소돼 있었다.

> **왜 이 항목이 오래 살아남았나 (박제):** spec에 *"정답은 X이며 Stage④의 일"* 이라고 적어두면, 그
> **동기가 후속 작업으로 사라져도 문장은 그대로 남는다.** 이번에 "다음 할 일"로 꺼낼 때 그 문장만 보고
> 유효하다고 단정했고, 없는 문제를 풀려고 *변경 감지 시스템 신설*(= Mana spec 결정 ③이 이미 YAGNI로
> 기각한 제네릭 옵저버와 사실상 같은 물건)까지 제안했다. 사용자가 **"그거 이미 다 되어 있잖아"** 로
> 잡았다. **미뤄둔 항목을 꺼낼 땐 결론이 아니라 그 항목이 막으려던 위험이 아직 있는지를 먼저 코드로
> 확인할 것.**

**업계 표준 대조(이번에 확인, 결론과 별개로 유효)**: 값 복제형(Unreal GAS `GetGameplayAttributeValueChangeDelegate`,
Mirror `SyncVar hook`)은 *값이 도착하는 단일 관문*에서 알리고, ECS형(Bevy `Changed<T>`, DOTS 청크 버전
필터, Photon Quantum)은 *알림 없이 훑는다(polling)*. Quantum은 **자주 바뀌는 시각 데이터엔 polling**을
권하는데(이벤트는 fire-and-forget이라 늦게 들어온 참가자가 못 받음) — 우리가 "스폰 때 1회 pull + 이후
알림" 두 겹을 쓰는 이유가 정확히 그것이다. 어느 진영도 *값 쓰는 자리마다 손으로 알림을 쏘지는* 않으므로,
위 국소 중복을 나중에 접을 때는 **단일 관문(GAS·Mirror식)** 모양이 맞다.

### 넷코드 잔여 (Stage④ 밖)

- **Phase 5 — 점프 임펄스 vy** ⏸ 보류(게임 디자인 콜). `[[netcode-migration-status]]`

### 구조 정리 백로그 (2026-07-13 전반 감사)

전반 구조/구현을 업계 표준 대비 5영역 병렬 감사. 코드는 대체로 건강 — 유의미한 것만. 소스 레벨 확인됨. **전체 findings 상세(file:line·심각도·노력): `docs/superpowers/audit-2026-07-13-structure.md`.**

- ✅ **#1 데미지 Amount 데이터 구동** (07-13) — `LOPCombatSystem.Attack`에 baseDamage 배선 → `DamageEffect.Amount` 소비(무동작; attack Amount=10=옛 하드코딩). 이제 Excel로 데미지 조정 가능.
- ✅ **#2 넉백 공유화 + `AttackSector` 추출** (07-13) — 넉백을 `IOverlapQuery`+`World.Transform`로 이관(마지막 World Core 우회 제거), 부채꼴 판정 공유 헬퍼화(Damage/Knockback 복사본 2벌 제거). 18 EditMode.
- ✅ **#4 스냅샷 채널 reliable→unreliable (서브셋 청킹)** (07-13, `e3d4496` — ROADMAP 상태만 stale이었음, 코드는 완료) — 통짜 flip 실패(Mirror unreliable 조각내기 불가, `EntitySnapsToC` >1184B 드롭) → **서브셋 청킹**(Quake/Source): 서버 `LOPRunner.EndUpdate`가 엔티티를 바이트 예산(`MaxEntityBytesPerMessage=1000`, `snap.CalculateSize()` 합)으로 나눠 **여러 `EntitySnapsToC`(같은 tick) `reliable:false`** 송신 — 각 청크 독립이라 손실 시 그 엔티티만 한 틱 놓침. 클라: 도착 기록 **틱당 1회 dedupe**(`GameEntityMessageHandler.lastRecordedArrivalTick` — 다중 메시지로 interval≈0→쿠션 폭증 방지), 소비자는 이미 tick-stale 무시(`RemoteEntityInterpolator`). 델타 압축(C)은 엔티티 많아질 때 후속. UserEntitySnap은 reliable 유지. 근거: `[[snapshot-mtu-chunking]]`.
- ✅ **#5 `generate_protos.sh` MessageId 보존** (07-13) — 부모 스크립트의 `rm MessageIds.cs` 제거. `generate_message_ids.sh`의 기존-ID-보존 로직이 이제 작동(파일이 있어야 읽어 보존). 검증: 서브스크립트 재실행 시 13개 ID 전부 불변. 메모리 gotcha 해소 → `[[proto-message-id-regen-gotcha]]`.
  - **UserEntitySnapToC → unreliable은 안 함(결정):** 한 엔티티(내 HP/MP/레벨) 소용량이라 reliable head-of-line-blocking 비용이 작음. 번호표(tick)+가드 들일 값어치 낮음 → **reliable 유지.** (스냅=unreliable 정석은 고빈도·대용량 스트림[위치]에서 값을 함.)
- ✅ **적(AI) 넉백 적용** (07-13) — 넉백 resolve가 `MovementSystem.Tick`의 입력 게이트 안에서만 돌아 AI(버퍼 없음)가 스킵되던 버그. 재사용 헬퍼 `MotionContributionSystem.ApplyToVelocity`(현 수평 velocity를 base로 외력 folding, y 보존·프루닝) 신설 + 서버 `MoveCharacters`에서 입력 비조종 캐릭에 호출(KinematicMove 통합 전). **공유 `MovementSystem.Tick`·클라 원격 경로 무변경** — 원격은 스냅 팔로워라 게이트 밖으로 빼면 스냅샷 권위 충돌 → 서버 host에서 AI만 folding. Shared 111 EditMode green + **인게임 육안 확인됨(몬스터 넉백 적용).** ⚠️ **임시 배치**(서버 분기) — 이상적 공통 루프 이전은 파킹(아래 "외력 처리 공통 루프 이전" 부채).
- ✅ **문서 stale 정합** (07-13, `docs/audit-stale-reconcile`) — `entity-system-design.md` 전면 재작성(코드 위치·타입명·enum값·컴포넌트/시스템 인벤토리 실제화) · `netcode-redesign.md §2.2`(input-as-데이터 축 + `InputBuffer` 실명; audit의 "예측 없음" 주장은 반박 — PlayerInputManager는 예측 트리거 유지) · connection-arch "괴리 #2" 해소 반영(`DeathCascadeSystem`, `LOPGame.HandleDeath` 삭제, death wire=`EntityDespawnToC` — `DamageDealtEvent.IsDead` 없음).
- ✅ **#3-WC `ctx.EntityManager` 탈출구 제거** (07-13) — 재검증 결과 완전한 죽은 pass-through(핸들러 전부 `EntityRegistry`+`IOverlapQuery`로 이전 완료). `AbilityEffectContext` 필드+ctor·`DriveActiveEntity` 파라미터·호출부 3곳·`Reconcile` 미사용 파라미터·테스트 5곳 정리. 동작 무변경.
- ✅ **#6 통합 World Tick (완료, 07-13)** — Reconciler 재생이 `LOPWorld.Mutation` 시퀀스를 수기 복제하던 desync 실패 클래스. **표준 정합**(클라 시뮬=예측 엔티티만 / 남·NPC=보간)으로 `Simulated` 마커 도입 → `world.Tick`이 그것만 순회 → 라이브==재생. spec `2026-07-13-unified-world-tick-client-sim-scope-design.md`, 3-슬라이스 분해(A/B/C). **✅ Sub-slice A 완료·머지**(07-13: `Simulated` 마커 + `Mutation` 순회 + driveeffects·외력 흡수, 넉백 부채 정산). **✅ Sub-slice B 완료·머지**(07-13: 클라 scope 축소[내 캐릭만] + `IMotionBridge` 포트 + 키네마틱 `world.Tick` 흡수 → **5페이즈 단일 진입점**). **✅ 후속 리팩터: 모션 브릿지 공유화**(07-13, 4 repo) — 처음엔 per-side `LOPMotionBridge` 2개(→클라 `IEntityManager` DI gotcha)였는데, 사용자 지적으로 **공유 concrete 1개(`MotionBridge`) + 공유 `PhysicsBody` 핸들 컴포넌트**로 통합(포트 유지, `UnityCollisionQuery`와 동형). 중복 + DI gotcha 동시 해소. **✅ Sub-slice C 완료·머지**(07-13: `Reconciler` 재생을 `world.Tick` 하나로 — 수기 5시스템 시퀀스 삭제). **→ 라이브·재생 둘 다 `world.Tick` = 두 벌 시퀀스 소멸 → `#6` 종결. `IWorld.Tick`이 단일 결정론 진입점 실현(클라=예측 엔티티만 시뮬 표준 정합 포함).**
- ✅ **죽은 코드 정리** (07-13) — #6-NC 레거시 `Status` 매틱 제거(구체 서브클래스 0, World Core StatusEffect가 대체) · #5-DM `MessageHandler<T>` 제거(4레포 사용처 0, 실 라우팅=`MessageFactory`+`EventBus`). 클·서 클린 컴파일.
- ✅ **#4-NC 링버퍼 3벌 → 공유 `SequenceBuffer<T>`** (07-16, feature 브랜치 `sequence-buffer-extract`) — `SnapshotHistory`/`InputHistory`/`PredictedAbilityStateHistory`가 각자 복제하던 `tick%capacity` 슬롯팅 + 병렬 tick 배열 stale 판별을 `GameFramework.Netcode.SequenceBuffer<T>` 하나로 흡수. 이름은 Fiedler "sequence buffer"(넷코드 표준, `RingBuffer`≠FIFO큐 구분). 순수 별칭이던 `InputHistory`/`PredictedAbilityStateHistory`는 삭제 → 호출처가 `SequenceBuffer<InputCommand>`/`<PredictedAbilityState>` 직접 사용, `SnapshotHistory`는 `Latest`/`Count`/tick-내장 `Record` 편의 있어 얇은 어댑터로 유지. GameFramework EditMode +10(269 green), 컴파일 클린.
- ✅ **#8 EventBus → MessagePipe (DI/R3 통일 + leak 구조적 해소)** (07-16, feature 브랜치 `eventbus-messagepipe-migration`, 클·서·GameFramework) — 전역 static 커스텀 버스(`GameFramework.EventBus`+앱 `EventBus.Default`/`EventTopic`)를 **Cysharp MessagePipe**(타입·keyed pub/sub + DI 스코프 브로커)로 이전. 웹 리서치 근거(패턴=표준/전역-static 형태=비표준/MessagePipe=R3 생태계 표준 답, `[[connection-arch]]`+spec). **①구독 IDisposable(AddTo) + Root 싱글턴 브로커**로 룸 재입장 leak 구조적 해소(②스코프 브로커는 교차스코프 복잡도 대비 redundant라 드롭). 5슬라이스: 0(도입) → 1(WebResponse/라이프사이클/ItemTouch) → 2(네트워크 수신=리플렉션 없는 `NetworkMessageDispatcher` 타입 라우팅) → 3(엔티티 keyed[키=entityId] + 죽은코드 트림[rotation/velocity 발행·컨트롤러 no-op]) → 4(버스 삭제). 정적/엔티티 컴포넌트=`GlobalMessagePipe`, DI 서비스/VM=`IPublisher`/`ISubscriber` 주입. IL2CPP 대비 `RegisterMessageBroker<T>` 명시 등록. 종합 플레이 검증 통과, EditMode 269 green, 커스텀 버스 전 레포 삭제. spec/plan `docs/superpowers/{specs,plans}/2026-07-16-eventbus-messagepipe-migration*`.
- ✅ **#7 WorldEventBatch 단일 envelope** (07-17, feature 브랜치 `world-event-batch-envelope`, Shared·Server·Client) — 개념별 top-level 패킷(`DamageEventToC`/`AbilityActivatedToC`)을 단일 폴리모픽 `WorldEventBatchToC`(oneof `WorldEventToC`)로 통합. 서버=버퍼를 배치 1개로 조립해 세션당 1회 송신, 클라=단일 `GameWorldEventMessageHandler`(oneof 순회 + `WorldEventWire.FromWire`, ability self-skip 보존), 변형 2개는 `@auto_generate` 제거로 nested-only 은퇴(MessageId/IMessage/creator 빠짐, 본문·필드번호 유지=바이너리 호환). 새 WorldEvent 타입이 MessageId/dispatcher/핸들러를 새로 안 요구. 공유 순수 매퍼 `WorldEventWire`(Generated 어셈블리) EditMode 6. 클라 EditMode 275/275, 최종 whole-branch 리뷰(3레포 교차) Critical/Important 0, 서버 컴파일 + 플레이 회귀 검증 통과. spec/plan `2026-07-17-world-event-batch-envelope*`.
- ✅ **전투 히트 해소 Part 1 — 닷지 on-hit 게이트** (07-17, feature 브랜치 `feature/combat-hit-resolution`, Shared·Server·Client) — 버그(닷지해도 넉백 당함)를 표준 구조로 수정. **데미지=히트 정의자**(per-attack 닷지 판정 + 명중 대상을 발동당 `AttackHitContext`에 기록), **넉백=on-hit 라이더**(명중 대상만 밀기, 자체 타게팅/닷지 제거). 닷지=per-attack seed(effectIndex 제거→모든 효과 동일 답), 크리=per-effect seed. 넉백 `Range/Angle` 제거(히트 형상=데미지). 업계표준=WoW attack table/GAS/LoL on-hit. EditMode(AttackHitContext 3+LOPCombatSystem 7+Knockback 3), 최종 whole-branch 리뷰 Critical/Important 0, 인게임 검증(닷지→안 밀림/명중→데미지+넉백). spec/plan `2026-07-17-combat-hit-resolution-*`. `[[combat-hit-resolution]]`
- ✅ **전투 히트 해소 Part 2 — 크리/회피 상수 MasterData 승격** (07-17, feature 브랜치 `feature/combat-config-masterdata`, infra·MasterData-Server·Shared·Server) — 하드코딩 회피/크리 확률·배수(0.05/0.95/0.05/0.50/1.25/1.75)를 새 전역 테이블 **`TbCombatConfig`**(단일 행 id=1, **서버 group `s`** — `LOPCombatSystem`이 서버 전용 등록이라 클라 불필요)로 승격. Luban 저작(python openpyxl `#CombatConfig.xlsx`+`__tables__` 행)→gen.sh 재생성→서버 `CombatConfigProvider`(AbilityData 패턴)→`CombatConfig` struct(Shared) 주입. 기본값=구 하드코딩이라 **동작 무변화**(밸런스만 Excel 조정 가능). `TableFiles` 배열 갱신 필수. EditMode 9, 최종 whole-branch 리뷰(4레포) Critical/Important 0(필드 swap 0), 서버 컴파일+플레이 무변화 검증. spec `2026-07-17-combat-hit-resolution-design`(Part 2 §), plan `2026-07-17-combat-hit-resolution-part2`. `[[combat-hit-resolution]]`
- ✅ **Tier-2/3 정리 (Item 2·3, 07-18)** — 넉백 Luban `Range/Angle` 잔재 제거(`__beans__.xlsx` KnockbackEffect bean + `#Ability.xlsx` 데이터 → gen.sh 재생성 → 클·서 생성 `KnockbackEffect.cs`가 `Strength/DurationTicks/DecayPerTick`만; 제거 필드는 C#·양 매퍼 미참조 죽은 바이트라 **동작 무변화**) · `CombatConfigProvider.Get()` fail-loud 가드(`Get(1)`의 애매한 `KeyNotFoundException` → `GetOrDefault(1)`+null 가드로 원인 짚는 `InvalidOperationException`). 클·서 editor 컴파일·MasterData 로드 클린 검증. 4레포 브랜치 `feature/combat-tier23-cleanup`.
  - **Item 1 (`ctx.Target` 항상 자기자신)은 그대로 유지** (사용자 결정, 07-18) — 제거 안 함. 향후 실 타게팅/on-hit 디버프는 넉백처럼 `HitContext` 라이더로 지을 축으로 남김.
- ✅ **엔티티 Unity 레이어 재구조화 S1 — 설정 컴포넌트 → World 데이터 컴포넌트** (07-18, feature 브랜치 `feature/entity-view-rearchitecture`, 클·서·Shared) — 레거시 Unity MonoBehaviour 엔티티 컴포넌트 4종(`Appearance`/`Character`/`Item`/`EntityType`Component)을 제거하고 LOP-Shared 순수 C# World 컴포넌트 3종(`Appearance{VisualId}`/`MasterDataRef{Code}`/`EntityKind{EntityType}`, `GameFramework.World.Component` 상속)으로 이관. enum `EntityType` shared로 dedupe. 소비자(크리에이터·뷰·바인더·서버 팩토리/생성데이터)는 `EntityRegistry.Get(id).Get<T>()`로 재배선, `EnemyBrain`은 `CharacterComponent.masterData.Speed`→`StatsSystem.GetValue(MoveSpeed)`로 전환해 `CharacterComponent` 통째 삭제. 서버 dead `LOPEntityView`도 동일 수정(후속 슬라이스에서 삭제 예정). EditMode +6(131 green), 최종 whole-branch 리뷰(DI 실파일 대조) Critical/Important 0, 클·서 컴파일 클린 + 인게임(모델 로드·AI 이동·넉백·마블) 검증. umbrella `2026-07-18-entity-view-rearchitecture-umbrella-design`, spec/plan `2026-07-18-s1-config-components-to-world*`. **큰 트랙 = 엔티티 Unity 레이어를 얇은 뷰/컴패니언 + Actor식 앵커로 수렴(S1~S5).**
- ✅ **엔티티 Unity 레이어 재구조화 S2 — PhysicsComponent → PhysicsFollower** (07-18, feature 브랜치 `feature/entity-s2-physics-follower`, 클·서) — 레거시 `PhysicsComponent : LOPComponent`(엔티티-컴포넌트)를 순수 MonoBehaviour `PhysicsFollower : MonoBehaviour, ICleanup`로 전환. `worldEntity` 보유(LOPEntity 의존 끊기), 죽은 `Depenetrate` 삭제, `physicsGameObject` reactive→필드. 창조자는 `AddEntityComponent`→`gameObject.AddComponent`+`Initialize(worldEntity,…)`, `LOPEntity` 브릿지는 `GetComponent<PhysicsFollower>()`로 최소 수정. 서버는 `TriggerDetector`/`ItemTouch` 유지. **→ `AddEntityComponent` 호출 0 = `entity.components` 병렬 리스트 완전히 빔**(마지막 LOPComponent 제거). 물리 루프·MotionBridge·LOPEntityController 무변경(경로 통합은 후속). 최종 whole-branch 리뷰 findings 0, 클·서 컴파일 클린 + 인게임(이동·충돌·아이템줍기·넉백) 검증. spec/plan `2026-07-18-s2-physics-component-to-follower*`.
- ✅ **엔티티 Unity 레이어 재구조화 S3 — 레거시 substrate machinery 삭제** (07-18, feature 브랜치 `feature/entity-s3-substrate-deletion`, GameFramework·클·서 3레포) — 레거시 엔티티/컴포넌트 substrate machinery 삭제: `IComponent`/`MonoComponent` + 클·서 `LOPComponent` + 서버 `Status` + `MonoEntity` + `Entity.Extensions`(컴포넌트 메서드 + `GetEntityTransform`). `LOPEntity : MonoEntity` → **순수 `LOPEntity : MonoBehaviour, IEntity`**(entityId+파사드 자체 선언), `IsGrounded`는 GF 확장 → `LOPEntity` 인스턴스 메서드(클라). `UpdateEntity`/`UpdateStatuses` 제거, `UpdateEntities()` 빈 본문. 전부 죽은 코드(S1·S2가 비운 뒤)라 **동작 무변화**. S3a(컴포넌트 dead)+S3b(엔티티) 2단계. GF·클·서 컴파일 클린 + GF EditMode 154/154 + 최종 리뷰 Critical/Important 0 + 인게임 스모크 검증. **`IEntity`는 4멤버 파사드 계약으로 축소해 잠정 유지**(서버 매니저가 파사드를 IEntity로 읽어 완전 삭제는 매니저 재작업=S5와 묶임). spec/plan `2026-07-18-s3-substrate-deletion*`. **→ S1·S2·S3로 레거시 MonoBehaviour machinery(컴포넌트 시스템 + MonoEntity) 소멸.**
- ✅ **물리 rb-follow 경로 통합 (③, 07-18, feature 브랜치 `feature/physics-follow-consolidation`, 클·서)** — rb가 World.Transform을 따라가는 3중복 경로(`MotionBridge.PushMotion` Simulated만 / `LOPEntity.SyncPhysics`·`PushMotionToPhysics`+`LOPEntityController` 전엔티티 / `PhysicsFollower.OnPropertyChange` reactive)를 **호스트 단일 패스**로 통합: `LOPRunner.SimulatePhysics`에서 `Physics.Simulate` 직전 `entityRegistry.All`의 `PhysicsBody` 엔티티에 `MotionBridge.PushMotion` 1회(파리티 위치=BeforePhysicsSimulation). 아이템에 `PhysicsBody` 추가(클·서 ItemCreator), Reconciler 롤백-후 push를 `MotionBridge.PushMotion`으로 스왑, `LOPEntityController`(클·서)·`SyncPhysics`/`PushMotionToPhysics`·reactive·죽은 `PropertyChange` 타입/브로커 삭제. strangler 2태스크(공존→삭제). 한-프레임 stale은 기존과 동일(파리티). 최종 리뷰 Critical/Important 0, 클·서 컴파일 클린 + 인게임(원격 위치·회전 따라오기·충돌·아이템·롤백) 검증. spec/plan `2026-07-18-physics-follow-consolidation*`. **→ S3에서 분리한 물리 통합 종결.**
- ✅ **엔티티 Unity 레이어 재구조화 S4 — Unity 트리 표준화 (07-18, feature 브랜치 `feature/entity-s4-tree`, 클·서)** — 빈 루트 → `Actor_{id}` **단일 앵커 겸 시뮬 바디**(kinematic rb+콜라이더 루트로). 모든 behavior(`LOPActor`·`PhysicsFollower`·interpolator·`LOPEntityView`·nameplate·floater·서버 AIController)를 **루트 컴포넌트**로 co-location, 모델 인스턴스=**루트 직속 렌더 바디 자식**(View가 `Instantiate(prefab, transform)`). `Visual`/`Physics` 빈 컨테이너 제거. 문자열/구조 배선(`Find("Visual")`/`Find("Physics")`/`parent?.parent?.GetComponentInChildren`) → `GetComponent`/`GetComponentInParent<LOPActor>()`(콜라이더→엔티티 매핑=trigger·`LOPOverlapQuery`). `LOPEntity`→`LOPActor` rename(whole-word, `IEntity` 인터페이스는 S5까지 유지). 매니저 destroy 루트 참조 교정. **스폰 fix**: 루트가 움직이는 시뮬 바디라 `PhysicsFollower`가 트랜스폼을 스폰 위치에 즉시 배치(안 하면 원점→스폰 점프에 자식 모델이 끌려가 첫 틱 순간이동). 업계표준 = Unity rb-on-root · Unreal Actor · Entitas View · NGO 두 바디 분리. 4태스크(rename→클 reshape→서 reshape→검증), 컴파일 클린(클·서 UnityMCP), 인게임 통과(스폰·충돌·아이템·넉백/데미지·롤백·원격보간·서버 테스트렌더), 최종 whole-branch 리뷰(opus) Critical/Important 0. 서버 프로덕션 렌더 제외(`#if !UNITY_SERVER`)는 후속. spec/plan `2026-07-18-s4-unity-tree-reconstruction*`.
- ✅ **엔티티 Unity 레이어 재구조화 S5a — IEntity 삭제 + 모션 접근자 통일 (07-18, 3레포 main 머지: GF 6945d0b / Client 0eb6e9c / Server b19d09d)** — 얇은 파사드 인터페이스 `IEntity`(4멤버) 완전 삭제. 모션(pos/rot/vel) 유니티↔순수숫자 변환+동등성가드를 **GameFramework `EntityMotionExtensions` 한 벌**(World.Entity 확장, 클·서 공유)로 통일 — 구 `LOPActor` 파사드가 6+ 사이트에 제공하던 것을 이전. `LOPActor` = **순수 신원 앵커**(`MonoBehaviour` + `entityId` + `Initialize`, `IsGrounded`는 클라 뷰로 이전). 엔티티 추상 제약(`IEntityManager`/`IEntityFactory`/`IEntityCreator`/`EntityFactory` + 서버 `IBrain`/`IEntityCreationDataCreator`) `where T : IEntity` → `MonoBehaviour` flip, 소비처는 콘크리트 `LOPActor`로 재타입(매니저는 `(LOPActor)(object)`/`(T)(object)` 캐스트). **World.Entity 단일 진실원본, 값 동치**(동작 무변화). 7태스크 subagent-driven(모션 익스텐션 additive+TDD → 클·서 모션 이전 → LOP 재타입+캐스트 → 3레포 제약 flip → LOPActor 슬림 → IEntity 삭제), 컴파일 클린(클·서 UnityMCP)·EditMode 290/290·인게임 스모크 통과·최종 whole-branch 리뷰(opus) Critical/Important 0. spec/plan `2026-07-18-s5a-ientity-deletion*`. **다음 = S5b(Creator→스포너 분해) + 어휘 rename 패스.**
- ✅ **엔티티 Unity 레이어 재구조화 S5b — Creator 데이터/뷰 분리 (07-19, 클·서 main 머지: Client b5acd77 / Server cf9c61c)** — 엔티티 생성을 **두 역할**로 분리: `CharacterCreator`/`ItemCreator` = **데이터 직원**(World.Entity 데이터 인라인 조립 + 앵커 GameObject+LOPActor + `EntityRegistry.Add` + 어빌리티 Grant; 클라 캐릭 isUser면 `playerContext.actor`), 반응형 **뷰 스포너 = 뷰 직원**(`EntityCreated` 동기 반응 → 모든 Unity 뷰 컴포넌트 + `PhysicsBody`). **클라 = `EntityBinder` 확장**(주요 뷰[물리/모델/보간] + 장식 뷰[nameplate/floater] 통일, Kind·isUser 분기), **서버 = `EntityViewSpawner` 신설**(물리+테스트렌더+비-플레이어 AIController, isPlayer=`Has<Ownership>`). MessagePipe 동기 발행이라 스포너가 `CreateEntity` 반환 전에 뷰+PhysicsBody 완결 → 반환 계약·물리 틱 공백 없음. **GF·LOP-Shared·물리 레이어·매니저·entityMap 무변, PhysicsBody 콘크리트 유지(X)** — 값 동치(동작 무변화). + `entity`→`actor` 어휘 rename(LOPActor 타입 식별자만, World.Entity 제외; 클 19파일 / 서 17파일 순수 rename). 3태스크 subagent-driven(클 뷰→스포너 / 서 뷰→스포너 신설 / rename), 컴파일 클린(클·서 UnityMCP)·인게임 스모크 통과·최종 whole-branch 리뷰(opus) Critical/Important 0(값 동치 컴포넌트 단위 검증). spec/plan `2026-07-18-s5b-creator-view-spawner*`. `[[physicsbody-port-purity-deferred]]`. **→ S1~S5 트랙 종결: World.Entity=단일 진실원본, Unity=얇은 뷰/스포너.**
- ✅ **로직/시뮬을 LOPActor에서 World.Entity로 분리 (07-19, 클·서 main 머지: Client cb2ae38 / Server edfc1af)** — S5 후속 구조 정리. 소비처 감사 결과 `LOPActor`(얇은 Unity 뷰) 참조 ~28이 **id만 쓰는 레거시 과참조**(뚱뚱한 `LOPEntity` 시절 잔재)임이 드러나, **순수 로직/시뮬을 `GameFramework.World.Entity`+`EntityRegistry`(데이터)로 전환**. `GetEntities<LOPActor>()`→`entityRegistry.All`, `actor.entityId`→`worldEntity.Id`, `GetEntity<LOPActor>(id)`→`entityRegistry.Get(id)`, `PlayerContext.actor`(LOPActor)→`entityId`(string), `IBrain<LOPActor>.Think(LOPActor)`→`IBrain.Think(World.Entity)`(제네릭 드롭), 서버 `IEntityCreationDataCreator.Create(LOPActor)`→`Create(World.Entity)`. `LOPActor`는 **뷰 레이어 ~6역할만** 보유(크리에이터/뷰 스포너/매니저 teardown/콜라이더→엔티티 브리지). 죽은 `RemoteEntityInterpolator.actor` 삭제. **값 동치**(id 동일 → 동작 무변화, EnemyBrain 중복 조회 제거는 부가 효율). 업계 표준 정합(Quantum/DOTS: 시뮬 로직=데이터 참조, GameObject=뷰만). **뷰/UI 컴포넌트는 범위 밖**(표준 뷰 패턴). 2슬라이스 B1(클)/B2(서) subagent-driven, 컴파일 클린·인게임 스모크 통과·최종 리뷰(opus) Critical/Important 0(값 동치 사이트 단위 검증). spec/plan `2026-07-19-logic-decouple-from-actor*`. `[[entity-unity-layer-rearchitecture]]`.
- ✅ **엔티티 매니저 → `ActorRegistry` + `EntitySpawner` 분리 (07-19, 3레포 main 머지: GF `5362b2b` / Client `8091eb3` / Server `3796864`)** — 뚱뚱한 `LOPEntityManager`(클·서)의 두 책임을 리액티브 뷰 리졸버 표준대로 분리. **데이터 축** = `EntitySpawner`(`Spawn`/`Despawn`/`FlushDespawns`, **`LOPActor`/`ActorRegistry` 미참조** 불변식) + `EntityRegistry`; **뷰 축** = `EntityBinder`(`EntityCreated`/`EntityDestroyed` 반응 → actor GameObject+뷰 생성/파괴) + `ActorRegistry`(신규 순수 id→LOPActor 인덱스). `EntityCreated` 페이로드 `LOPActor`→`string entityId` flip(발행 시점엔 actor 없음), 데이터 creator는 순수 데이터로 축소(actor 앵커 생성이 `EntityBinder`로 이동), 서버 `EntityViewSpawner`→`EntityBinder` 이름 통일. **GameFramework 옛 제네릭 3종(`IEntityManager`/`IEntityFactory`/`IEntityCreator`) + `EntityFactory` 삭제** + `IRunner`/`RunnerBase`에서 `entityManager` 제거(리플렉션 factory 폐기, `Spawn` 타입별 오버로드로 대체). 서버 부가(id 발급·`userEntityMap`·despawn 와이어)는 서버 `EntitySpawner`에, `GetAllEntitySnaps`/`GetAllEntityCreationDatas`는 유일 호출자(LOPRunner/GameInfoHandler)의 `entityRegistry.All` 순회 헬퍼로 이전, `GetUserIdByEntityId`→`Ownership` 파생. **값 동치**(동작 무변화) — 스폰/디스폰 wire·타이밍 무변, MessagePipe 동기 발행이 데이터↔뷰 co-location 불변식의 토대. 4태스크 subagent-driven(GF 삭제 → 클 마이그레이션 → 서 마이그레이션 → 통합), 컴파일 클린(클·서 UnityMCP, `.meta`·씬 컴포넌트 정리 포함)·인게임 스모크 통과·최종 whole-branch 리뷰(opus, 3레포 교차) Critical/Important 0. spec/plan `2026-07-19-entity-manager-actor-registry-split*`. `[[entity-unity-layer-rearchitecture]]`. **→ "의도적 보류"의 `entityMap`→스포너 / `IEntityManager` 해체 실현.**
- ✅ **PhysicsBody 순수 포트화 — 코어 추상 포트 + Unity 어댑터 (07-19, 4레포 main 머지: GF `aae51b8` / Shared `e08d464` / Client `19fb490` / Server `dffb3c3`)** — `World.Entity`에 얹혀 있던 **마지막 Unity 타입**(`PhysicsBody`가 든 `Rigidbody`/`Collider`)을 헥사고날 DIP로 제거해 엔티티 컴포넌트 집합을 100% 순수화. **코어에 순수 추상 포트 신설** `GameFramework.World.PhysicsBody`(System.Numerics — `IsKinematic`/`SetPosition`/`SetRotation`/`SetVelocity`/`ComputePushOut`), **Unity 어댑터** = 구 Shared 구체 `PhysicsBody`(Rigidbody 보유)를 `UnityPhysicsBody : World.PhysicsBody`로 rename+교체(LOP-Shared), `MotionBridge`는 **추상 포트만 호출**(구체 Rigidbody 미참조). 스폰은 **추상 키로** `worldEntity.Add<World.PhysicsBody>(new UnityPhysicsBody(rb,col))` — gotcha: `Entity.Add<T>`가 `typeof(T)` 키라 명시 제네릭 필수(아니면 `Get<PhysicsBody>()` 미스). **값 동치**(로직 1:1, 동작 무변화)·5파일·클·서 UnityMCP 컴파일 클린. 업계표준 확인(Unreal `FPhysicsActorHandle` 프록시 / DOTS companion / 헥사고날 "도메인 포트 정의·어댑터 구현"). "사이드테이블(외부 id→핸들 맵)" 대안은 폐기(엔티티 컴포넌트=죽으면 자동 정리, 누수 없음이 우월). `[[physicsbody-port-purity-deferred]]`.
- ✅ **클라 Assets/Scripts 폴더 재편 — 그루핑 + 업계 표준 네이밍 (07-19, 브랜치 `refactor/scripts-folder-reorg`)** — 개념 단위 그루핑 + 오해 소지 이름 정정. 엔티티 3폴더(`EntityCreator`/`Component`/`Game`의 `EntityBinder`)→`Entity/` 통합, 흩어진 넷코드(시간동기·보정·스냅샷 13파일)를 `Netcode/` 신설로 집결, `Data/`→`Stores/`(런타임 상태 캐시=store, `Model`과 혼동 해소)·`Model/`→`Domain/`(anemic 도메인 표현)·`WebAPI/Model/`→`WebAPI/Dto/`(전송 객체 정정) 개명, `Messaging/`→`Network/` 병합, 빈 폴더 3개(`Extensions`/`Popup`/`Game/UI`) 삭제. **평면 `namespace LOP`라 코드 편집 0줄**(순수 파일/폴더 이동, `.cs`+`.meta` 동반). 웹 리서치로 네이밍 표준 확인(Unity Netcode 용어, DTO vs Model vs Domain, Flux/Redux store). 최종 Entity 11/Netcode 13/Domain 11/Stores 8, 60cs·60meta 짝 검증·클라 UnityMCP 컴파일 클린(에러 0·meta 경고 0). subagent-driven 이동 + git 객관 검증. spec/plan `2026-07-19-scripts-folder-reorg*`. **서버도 대칭 적용 완료**(같은 날, 서버 `main` `cbd4dae`; 29 `.cs` 순수 이동·컴파일 클린·스모크 통과. 서버 특화: `DeathCascadeSystem`→`Game/`[`GameRuleSystem` 옆], `AI/`·`EntityCreationDataFactory/` 유지, `Netcode/`는 보간기/스냅샷 없어 4파일). `[[entity-unity-layer-rearchitecture]]`.
- ✅ **GF 넷코드 클래스 `GameFramework.Netcode` 수렴 (07-19→20, 4레포 main: GF `201a2a4` / Shared `2c0e6a7` / Client `402c7ac` / Server `a647c29`)** — GF `Game/` 잡탕에 흩어져 있던 넷코드 5클래스(`ClockDilator`·`INetworkTime`·`InputTimingTracker`·`InputTimingSummary`·`LeadController`)를 이미 있던 `Netcode/` 폴더 + `GameFramework.Netcode` 네임스페이스로 이동(기존 Netcode 파일들과 정합). **폴더만 옮긴 클·서와 달리 진짜 리팩터** — GF는 sub-namespace라 네임스페이스 변경 + 소비처 `using GameFramework.Netcode;` 추가(GF 5·클 6·서 2·**Shared 1**[초기 grep에서 누락→컴파일이 잡음, InputBuffer FQN 참조]). blast radius 실측 후 진행(위험=컴파일로 잡히는 로우, 수고=~20파일/4레포). C# enclosing-namespace 규칙으로 이동 파일→부모 GameFramework 참조는 using 불필요. RNG·물리포트·엔진은 별도 택소노미라 이번 범위 밖(넷코드만 딱 자름). + GF 소소 정리: 루트 흩어진 라이프사이클 인터페이스 3종→`Lifecycle/`, `TriggerDetector`→빈 `Component/` 재활용. 4레포 UnityMCP 컴파일 클린 + GF EditMode 75/75. 파킹 `[[netcode-namespace-consolidation]]` 해소.
- ✅ **GF `Game/` 잡탕 추가 분할 — 물리포트·RNG sub-namespace (07-20, 4레포 main: GF `cbd39f3` / Shared `8701379` / Client `8783ed5` / Server `87d0546`)** — 넷코드에 이어 GF `Game/` 나머지 22개를 마저 정돈: **물리 엔진 추상 7종**(`ICollisionQuery`·`IOverlapQuery`·`IPhysicsSimulator`·`CollisionHit`·`KinematicDepenetration`·`Unity{Collision,Physics}`)→`Physics/`+`GameFramework.Physics`, **RNG/결정론 4종**(`DeterministicRandom`·`Hashing`·`IRandom`·`UnityRandom`)→`Rng/`+`GameFramework.Rng`. `Game/`은 엔진 호스트 코어(Runner/Tick/Factory/Presenter 11개)로 응집. 소비처 갱신: Physics(GF 0[IMotionBridge는 주석뿐-오탐]·Shared 9·클 2·서 3)·Rng(GF 테스트 2·Shared 1·서 2). blast radius 실측 후 진행. **⚠️ gotcha: `GameFramework.Physics`가 `UnityEngine.Physics`와 충돌** — 어댑터 안 bare `Physics.Simulate/CapsuleCast` 등이 감싸는 네임스페이스로 해석돼 깨짐(컴파일이 잡음). 해결=어댑터 3파일 4곳 `UnityEngine.Physics.` 풀네임(DOTS 관용, 소비처는 영향 0). 서버 픽스처 `GameRuleSystem`(스폰 개수)은 사용자 승인 폐기 후 using만 커밋. 4레포 컴파일 클린 + EditMode 206/206(GF+Shared). `[[netcode-namespace-consolidation]]`.
- ✅ **메시지 핸들러 `MessageHandlerBase` 통합 (07-20, 3레포 main: GF `b0e5b49` / Client `b164066` / Server `c65c076`)** — 클·서에 **글자 하나 안 다른 빈 마커 인터페이스** `IGameMessageHandler`/`IRoomMessageHandler`(각 `: IInitializable, IDisposable`) + 핸들러마다 복붙된 구독/해제 배관을 **GF 공유 `MessageHandlerBase` 한 벌**로 흡수. 게임/룸 구분은 **DI 스코프가 이미** 하므로(GameLifetimeScope vs RoomLifetimeScope 등록) 타입 중복 제거 = 마커 둘 다 삭제. base=VContainer 엔트리포인트(`Initialize→Subscribe()`/`Dispose→Track한 IDisposable 일괄 해제`), MessagePipe 비의존(구독 결과 `IDisposable`만 `Track`, `ISubscriber`는 자식만). 파일명=클래스명 표준화(`Game.Info.MessageHandler.cs`→`GameInfoMessageHandler.cs` 등), 룸 `GameMessageHandler`→`RoomSessionMessageHandler`(하는 일=세션 세팅). **⚠️ gotcha: GF에 자체 `GameFramework.IInitializable`(`Lifecycle/`)이 있어** base가 `VContainer.Unity.IInitializable`을 **풀네임**으로 써야 함(bare는 GF 것으로 오해석). 서버 `GameInfoMessageHandler`는 구독 외 `runner.AddListener`도 해서 `Dispose` override로 `base.Dispose()`+`RemoveListener` 보존. 값 동치(핸들러 로직·`[Inject]`·등록 순서 불변). 클 8핸들러+서 4핸들러, GF EditMode 2 green·클·서 UnityMCP 컴파일 클린·스모크 통과·최종 whole-branch 리뷰(opus, 3레포) Critical/Important 0. spec/plan `2026-07-20-message-handler-base-consolidation*`. `[[messagepipe-migration]]`. + 후속 tidy(07-20, GF `1a4ea92`/클 `a255ecf`/서 `81a6a29`): defer됐던 Minor 정리 완료 — GF `Dispose()` XML doc 추가 + 미사용 `using System;` 3곳(클 EntityBinder/PlayerHudCoordinator·서 EntityBinder) 제거, 클·서 컴파일 클린.
- ✅ **매치메이킹 FSM 견고화 (07-20, 2레포 main: GF `71c9759` / Client `9b9e782`)** — 사용자 작성 브랜치(`feature/matchmaking-fsm-robustness`) 리뷰→표준 대비 타당 확인→테스트 보강 후 머지. **GF `State`/`StateMachine` 견고성 3축**: ① **return-based 전이**(`OnExecuteAsync→Task<TEvent?>`, 상태가 다음 이벤트를 *반환*하면 base가 발행 — 취소 판정이 base 한 곳으로 집중돼 "이미 나간 상태가 발행"하는 버그 클래스 제거, State 패턴 정석) ② **`OnError` 훅**(일반 예외를 회복 이벤트로 흡수 — 예전엔 fire-and-forget Task라 예외가 사라지고 FSM이 조용히 멈춤) ③ **재진입 큐**(전이 도중 Fire를 큐잉해 순서 보장 = UML statechart run-to-completion). `TEvent` 제약 `struct, Enum`. **클라 매치 상태들의 실제 hang 버그 수정**: `CheckMatch`(조회 실패 시 이벤트 없이 멈춤)·`InWaitingRoom`(폴링 예외 한 번에 대기실 갇힘)을 유한 재시도+반드시 종료 이벤트 반환으로. **테스트**: GF EditMode 7개(`StateMachineTests`, 재진입 큐 유효성=큐 무력화 시 그것만 실패 확인), GF 전체 84/84 green. **서버는 FSM 미사용**(GF 공유 타입이나 실사용=클라 매치메이킹뿐, GF 변경이 서버 컴파일 안 깸 확인). 표준 매핑: return-based=GoF State / 재진입 큐=UML RTC(XState·Stateless·Boost.SML) / OnError=폴백 복원력. 오래된 브랜치라 머지 전 양 레포에 최신 main 병합(충돌 0 — FSM 파일은 그간 정리와 안 겹침). `[[fsm-architecture]]`.
- ✅ **로컬 변경점 위생 정리 (07-21, 4레포)** — 여러 레포에 쌓여 있던 uncommitted 로컬 변경을 분류해 *진짜 커밋 필요한 것만* 반영. **① MasterData-Server `d7fdcd9`**: 누락된 CombatConfig `.meta` 3개(`CombatConfig`/`TbCombatConfig`/`tbcombatconfig.bytes` — `.cs`/`.bytes`는 07-17 `fd93f9d`에 커밋됐는데 짝 meta만 빠짐, 같은 폴더 다른 16개 meta는 정상. CLAUDE.md meta 필수 규칙). **② infrastructure `7219a42`**: `.gitignore`에 `__pycache__/`·`*.pyc`(table/scripts 실행 캐시). **③ Client `d0e936c` / Server `eeb1102`**: `GraphicsSettings.LightsUseLinearIntensity 0→1`(Linear color space 프로젝트에 Unity가 자동 교정, 클·서 동일 → 커밋해 재발 방지). **커밋 안 한 것(의도)**: 서버 로컬 픽스처(`GameRuleSystem` 스폰 에네미 10→3·`ConfigureRoomComponent` 에디터 부팅 — 커밋 금지 픽스처), Unity 자동 노이즈(`PackageManagerSettings` instanceId·`DefaultVolumeProfile` 재직렬화), Art submodule(.mat, 무시). 분류 기준: 누락된 커밋 필요물 vs 의도적 로컬 픽스처 vs 세션마다 바뀌는 자동 노이즈.
- ✅ **사용 안 하는 코드 제거 (07-22, GF `0023a01` / Client `50551e5`)** — 4레포 병렬 dead-code 감사(전 레포 참조 검색 + `.meta` GUID로 씬/프리팹/에셋 교차 확인 + 리플렉션/DI/attribute 축약형 필터) 후 **확실한 통째 삭제만** 제거. **GF 14파일**(BoundedDictionary·BoundedQueue·IBuilder·MathUtility / 레거시 팝업 클러스터 6종[Popup·PopupManager·IPopup·IPopupManager·PrefabReferences·ScriptableObjectSingleton = UI Toolkit WindowManager로 대체된 UGUI 시절] / 죽은 BinaryFormatter 직렬화 `Serialization/` 4파일[Extensions 포함]) + 빈 폴더 5. **Client 2파일**(LocalEntitySnap=옛 delta-replay 잔재·IndividualTextOutline=미부착 MonoBehaviour) + 빈 `PrefabReferences.asset`. **Shared 0**(이미 여러 정리 슬라이스로 깨끗). **감사 발견 = Shared 0 / Client A8·B1·C180 / Server A10·B0·C13 / GF A13·B3·C4** — Unity GUID 스캔이 `LOPTickUpdater`(코드참조 0이나 씬 배치)·팝업(문자열로딩 0 재확인) 등 오탐 방지. **보류(사용자 결정)**: 부분 트림 5건(IInitializable footgun·IDeinitializable sync·EntityDeath·LOPGameState 죽은 상태 4×2), 살려둔 것(Singleton·UpdateUserProfileRequest·서버 WebAPI DTO 6종=미래용), `GameOver` 배선 누락(=런타임 죽은 분기, 삭제 아닌 배선 이슈로 남김). 클·서 UnityMCP 컴파일 클린. `BoundedList`는 보간기가 실사용이라 오인 정정.

### Runner(호스트 계층) 표준화 리팩터링 — Slice 1~6 완료·트랙 종결 (07-24~25)

옛 `GameEngine`을 이름만 `Runner`로 바꾼 레거시 호스트 계층(`IRunner`/`RunnerBase`/static `Runner`/`TickUpdaterBase`/`IGameState`)을 업계 표준(DOTS SystemGroup / Quantum / Overwatch / Fiedler)에 맞춰 재구성하는 트랙. umbrella `docs/superpowers/specs/2026-07-24-runner-standardization-refactor-design.md`(감사 A~I + 목표 모양 + 잠금 결정 + 6-슬라이스 분해). 각 슬라이스 brainstorm→spec→plan→subagent-driven, 클·서 UnityMCP 컴파일 + 리뷰(opus) + 플레이 스모크. `[[runner-standardization-refactor]]`.

- ✅ **1 — 틱 루프 강화 (C, 07-24, GF)**: `TickCatchUp.ClampTarget`(프레임당 캐치업 상한=spiral of death 방지, Fiedler) 순수 커널+EditMode, `TickUpdaterBase` 문자열 코루틴→저장 참조, `ITickUpdater.deltaTime` 추가. plan `2026-07-24-runner-slice1-tick-loop-hardening`.
- ✅ **2 — 상태 enum화 (D, 07-24, 3레포)**: 빈 마커 인터페이스 `IGameState` + 마커 클래스 9개(×2) → `enum RunnerState`. 빈 마커로 상태 표현은 비표준(.NET `ConnectionState`/`TaskStatus`·Photon `ClientState`류가 표준), 행동 FSM(앱/매칭 `StateMachine<TEvent>`)과 다른 축=상태 플래그라 enum이 정답. 미사용 값(Preparing/Prepared/Error) 가지치기, None=기본값. plan `2026-07-24-runner-slice2-state-enum`.
- ✅ **3 — 라이프사이클 死코드 삭제 (G, 07-24, GF-only)**: sync `IInitializable` + 제네릭 `IInitializable<T1..3>` + sync `IDeinitializable` 제거(구현체·호출부 0). **`ICleanup`은 존치** — 조사로 판명: 스코프 teardown이 아니라 `EntityBinder.OnEntityDestroyed`의 엔티티-파괴 정리 마커(`GetComponentsInChildren<ICleanup>` 스윕)라 VContainer `IDisposable`로 못 옮김(over-match 위험). `IInitializableAsync`/`IDeinitializableAsync`만 남김. plan `2026-07-24-runner-slice3-lifecycle-deadcode`.
- ✅ **4 — 클럭 DI화 (B, 07-24, 3레포, 4a+4b)**: static `Runner` facade(`current`/`Time`/`NetworkTime` = Service Locator 안티패턴) 통째 삭제. **4a** `Runner.Time.*`→주입 `ITickUpdater`(계산값 `deltaTime` 흡수, 죽은 `tickTime` 폐기). **4b** `INetworkTime` DI 등록·주입 + `LOPTickUpdater`는 `[SceneInject]` 대신 **LOP측 hand-off**(LOPRunner가 sibling에 세팅 — 클라 `LeadState` 잠복 미주입을 안 깨워 넷코드 타이밍 불변), is-running(`Runner.current`)→`runner.gameState` enum 순서 판정(`< RunnerState.Playing`), `CreateNetworkTime` 훅·static `Runner` 삭제, 종료-창 NRE 가드(`tickUpdater != null`). **서버 debug HUD는 부모 Room 스코프라 자식 Game 스코프 `IRunner` 못 봄** → `DebugHud` GameObject를 `Room.unity`→`LOPGame.unity` 씬 이동(`MoveGameObjectToScene`, MCP)으로 co-scope. plan `2026-07-24-runner-slice4a-tick-facade`·`4b-networktime-delete-facade`.
- ✅ **5 — 시스템 리스트: god-object 해체 (A·I, 07-24~25, 5A+5B)**: **5A** 리플렉션 틱 이벤트버스(`[RunnerListen]`/`DispatchEvent<T>`/`AddListener`) → 타입드 `ITickSystem` 페이즈 registry(`RegisterSystem<TPhase>`/`RunPhase<TPhase>`, 런타임 add/remove 지원=동적 per-entity 리스너, dup 가드). 4 소비자 전환, 죽은 페이즈 4개 삭제. **5B** 인라인 파이프라인 스텝(reconcile/physics/이벤트드레인/스냅샷/브로드캐스트/death/입력…) → `ITickSystem` 추출(**서버 8 + 클라 5**), `UpdateRunner`=주입 시스템 순서대로 직접호출 + `RunPhase` 훅 + `world.Tick` 별도. **서버 `LOPRunner` 406→122줄, 클라 204→103줄.** 순서 불변식(deaths→drain·despawn 마지막·End→broadcast·input→world.Tick) 보존. plan `2026-07-24-runner-slice5a-tick-system-registry`·`2026-07-25-runner-slice5b-pipeline-systems`.
- ✅ **6 — 네이밍/네임스페이스 (E·F·H, 07-25, 3레포 main: GF `eaa704b` / Client `45e36e4` / Server `e0eb9ca`)**: **E** `UpdateRunner()`를 `IRunner`에서 제거 + `RunnerBase`에서 `protected abstract`로 내부화(외부 호출자 0=`OnTick` 내부뿐, 캡슐화 누수 제거). **F** GF `IGamePresenter`/`MonoGamePresenter`(UI/MVP 용어·클라 전용) 삭제 + 클라 `LOPGamePresenter`→**`LOPGameSceneCoordinator`**(게임씬 수명 신호를 듣고 로딩화면·카메라 조율=코디네이터, 자체 UI 문서·`PlayerHudCoordinator` 어휘와 정합. `git mv` cs+meta로 GUID 보존→씬 참조 무결). **H** 호스트 클러스터 9파일(`IRunner`/`RunnerBase`/`RunnerState`/`ITickUpdater`/`TickUpdaterBase`/`TickCatchUp`/`ITickSystem`/`IMapLoader`/`IGameFactory`)→**`GameFramework.Runner`** 네임스페이스(`.World`/`.Netcode` 형제와 정합). **⚠️ gotcha: 두 경로 다 처리** — 완전한정명 `GameFramework.ITickSystem`(TickSystems 13개)·`GameFramework.IMapLoader`(GameLifetimeScope 2개)는 **FQN 재작성**(`GameFramework.Runner.*`), 비한정 사용(`using GameFramework;` 의존)은 **`using GameFramework.Runner;` 추가**(use-side ~28파일 + GF 내부 `IRoom.cs`[자식 네임스페이스라 outer-walk 안 됨] + 테스트 1). C# enclosing-namespace 규칙으로 이동한 GF 9파일→부모 GameFramework 루트 타입은 using 불필요(namespace 줄만). 클·서 UnityMCP 컴파일 클린(에러 0·불필요 using 경고 0)·GF EditMode 90/90·씬 GUID 참조 검증. 서버 픽스처 `GameRuleSystem`은 using만 편집·커밋 제외. plan `2026-07-25-runner-slice6-naming`.
- ✅ **트랙 종결.** 옛 `GameEngine`→`Runner` 레거시 호스트 계층이 업계 표준(DOTS SystemGroup·Quantum·Overwatch·Fiedler)으로 재구성 완료: 틱 캡·enum 상태·라이프사이클 정리·DI 클럭(static locator 제거)·시스템 리스트(god-object 해체)·네임스페이스 그룹핑. 감사 A~I 전부 해소.

---

## ⏸ 파킹 (Parked — 미룬 것 + 재개 조건)

| 항목 | 왜 미뤘나 | 재개 조건 |
|---|---|---|
| ~~**매치 종료 시 유저 위치 백엔드 정리 (Slice D 후속)**~~ | ✅ **해소(2026-08-15)** — 위 "매치 종료 시 유저 위치 정리" 절 참조. ⚠️ **이 항목의 옛 서술은 두 군데가 틀렸다**: ① 원인이 "위치를 아무도 안 지운다"가 아니라 **"끝났다는 사실이 느린 파드 삭제 뒤에야 DB에 박힌다"** 였다(로비 조회 경로엔 자가치유가 이미 있었다). ② *"`if (!Standalone)` 가드로 로컬에선 스킵"* 은 07-30 kind 전환 **이전** 기준이다 — `standalone: 1`은 에디터 기본 환경(`local`)뿐이고, 로컬 E2E는 `local-k8s`(standalone=0)라 실제로는 호출된다. `[[flow-slice-d-match-result]]` | — |
| ~~**외력(넉백) 처리를 공통 엔티티 루프로 이전** (부채)~~ ✅ **정산(07-13)** — 통합 World Tick Sub-slice A에서 외력 resolve를 공유 `MovementSystem.Tick`(=`world.Tick` 이동 페이즈)로 흡수, 서버 `MoveCharacters` 임시분기 제거. 입력 없는 Simulated(서버 AI)도 resolve. `Simulated` 마커가 클라 원격 문제를 자연 해소(원격은 마킹 안 돼 클라가 안 틱). `[[velocity-motor-contribution-slice]]` | — |
| ~~**`PhysicsFollower` 접기 (공유 팩토리화)**~~ | ✅ **완료(2026-08-23, 3레포 머지·실플레이 검증)** — LOP-Shared `581f27e` · Server `1d41a7d` · Client `0dc2e4d`. 셋업을 공유 `PhysicsBodyFactory.Create(root, World.Entity, isKinematic, isTrigger)`로 접고 `PhysicsFollower` 2벌 삭제. **⚠️ 이 항목의 옛 서술은 틀렸다 — "런타임 behavior 0"은 *클라 기준*이었고 서버 것은 껍데기가 아니었다**: `[Inject] IPublisher<ItemTouch>` + `TriggerDetector` 배선으로 **아이템 줍기를 감지**하고 있었다(→ `FlapWangRuleSystem`이 디스폰 + 경험치 +10). 그래서 지우는 대신 **`ItemTouchDetector`로 갈라냈고**, 트리거인 것(=아이템)에만 붙인다 — 캐릭터에 붙은 감지기는 상대에 `Ownership`이 없어 늘 무시로 끝나고 있었다(동작 변화 없음). **몸 만들기(클·서 공통)와 접촉 판정(서버 규칙)은 상관없는 일**이라 갈라지는 게 맞았다. **후속(같은 날, Server `188daeb`)**: 갈라낸 감지기가 *"주운 게 플레이어인가"*(`Ownership` 검사)까지 들고 있어 **Unity 레이어가 World Core를 주입받고** 있었다 — 그건 게임 규칙이므로 `FlapWangRuleSystem`으로 옮겼다(디스폰·경험치가 이미 거기 있다). 감지기는 이제 *"이 아이템에 이 엔티티가 닿았다"* 는 **엔진 사실만 옮기고 도메인을 모른다**. 트리거 수신 자체는 MonoBehaviour일 수밖에 없고 가이드라인도 "Collider 반응"을 Controller 역할로 인정한다 — 어긋났던 건 그 안의 도메인 지식뿐이었다. `[[physicsbody-port-purity-deferred]]` | — |
| ~~**Recon 엔티티-로드 러버밴딩**~~ ✅ **원인 규명 완료(08-06)** — 엔티티 부하 가설은 **반증**됐고(2 vs 52 차이 없음), 진짜 원인은 **입력 한 틱 누락 → 1틱 제동 → 4cm → 문턱 미만이라 영구 잔류**. 위 "Recon 러버밴딩 원인 규명" 절 참조. **대응은 미착수** | — |
| ~~**캐릭터끼리 충돌 wedge** (몹 뭉침)~~ ✅ **결론(07-16) — "단단한 벽" 확정** | 소프트 분리(BOTW식)를 시도했다 폐기: 클라 예측(현재 틱) vs 원격 보간(과거 틱) **타임라인 불일치** 때문에 클라가 분리를 밀면 덜덜(recon 폭발) / 안 밀면 관통 → config로 "안 겹침+안 덜덜" 동시 불가(predict-all이나 통과만 가능, 범위 밖). **클·서 동일 벽 모델**(sweep에 Character 포함 + 디펜 full)로 확정 → 겹침·덜덜·recon 다 해소, 8마리 군집 정상. wedge는 실전 비문제(비스듬 접근=곡면 슬라이드). 동작상 원래와 사실상 동일 + 전용 레이어·명시 디펜·측정지식 추가. spec `2026-07-16-character-soft-separation-design`, `[[kinematic-controller-migration]]` | — |
| **클라 코드가 asmdef 레이어링을 안 쓴다** (`Assets/Scripts` 전체가 `Assembly-CSharp` 한 덩어리) | `architecture-guidelines.md`의 "Assembly Definition 전략"은 레이어별 asmdef를 규정하는데 클라는 적용된 적이 없다. 그래서 **단위 테스트를 붙이려면 코드를 패키지(Shared)로 밀어 넣게 되는 압력**이 반복된다 — 유니티 설계상 테스트 asmdef가 `Assembly-CSharp`를 참조할 수 없기 때문(공식 해법도 "프로덕션 코드를 asmdef로"뿐이다). 2026-08-23 동기화 정책이 실제로 이 이유로 Shared에 잘못 놓였다 재배치됐다(`LOP.EntitySync` asmdef 신설로 해소) | 같은 압력이 또 나올 때, 또는 컴파일 시간이 문제가 될 때. 한 번에 다 옮기지 말고 이번처럼 **테스트가 필요한 조각부터 asmdef로 떼어내는** 방식이 현실적이다 |
| **서버 뷰 NRE** (`LOPEntity.get_position`/`LOPEntityView.LateUpdate`) | 뷰 `LateUpdate`가 `worldTransform` 링크 전/해제 후 `position`을 읽는 수명 타이밍. 도메인 리로드(재시작) 시 발현. 이동 버그와 무관(07-15 확인) | 서버 뷰 수명 손댈 때 |
| ~~**EventSystem 2개** (additive 씬 중복)~~ | ✅ **완료(2026-08-23, Client `ff2246c`)** — 씬 4개(Entrance·Lobby·FlapWang·FlappyRace)가 각자 하나씩 갖고 있었고 Room엔 없었다. **`UIRoot` 프리팹**(`DontDestroyOnLoad` 싱글턴)에 하나 얹고 씬에서 전부 제거. **⚠️ "게임 씬에만 두기"는 오답이다** — 로그인·로비·게임 HUD가 전부 같은 전역 `UIRoot`에서 뜨므로 게임 씬에만 두면 로비 버튼이 안 눌린다. **기준: EventSystem 수명 = UI 수명.** `IngameDebugConsole` 프리팹의 EventSystem은 **비활성**이라 경고를 안 내므로 손대지 않았다. 표준: 하나만·전역, additive 씬에서는 제거([Unity Multi-Scene editing](https://docs.unity3d.com/2020.1/Documentation/Manual/MultiSceneEditing.html)) | — |
| **`LeadController` 나머지 경계값 하드코딩** | 08-09에 마진 *바닥*만 런타임 틱 간격에서 유도했고 `maxMargin`(0.1s)·step(10/2ms)·`DefaultMargin`은 아직 상수다. 틱 간격이 바뀌면 정책 의미가 조용히 달라진다. 동작 무변경 정리라 급하지 않음 | lead 정책을 손댈 때 (틱 배수로 유도) |
| **입력 포커스** (에디터 Play Mode Input Behavior) | **게임 버그 아님** — Game 뷰 포커스 잃으면 Input System이 키를 0으로 봄(`kbNull=False`인데 전 키 false). brake-to-desired 모터가 입력 0→즉시 정지시켜 "낀 것처럼" 드러남(옛 관성 모터는 덮여 안 보였음). 빌드 무관, 2에디터 테스트 artifact | 테스트 편의 시 InputSettings에 `All Device Input Always Goes To Game View` 설정, 또는 Game 뷰 포커스 유지 |
| **네이티브 clock sync (방향 B)** | Mirror transport=메인스레드라 ping/pong 정확도 이득 미미; 순수측정=전용소켓+스레드 큰 작업 | **Mirror 제거가 실제 안건이 될 때.** `[[netcode-migration-status]]` §9.8 |
| **M5b — LOP.UI 인프라 GameFramework 승격** | 단일 클라라 YAGNI | 서버도 같은 UI 인프라가 필요해질 때. `[[uitoolkit-migration-status]]` |
| ~~**넷코드 status 메모리류 `GameFramework.Netcode` 수렴**~~ ✅ **완료(07-20)** — GF `Game/`의 넷코드 5클래스를 `Netcode/`+`GameFramework.Netcode`로 이동(4레포). 위 Done 원장 참조. `[[netcode-namespace-consolidation]]` | — | — |
| **MasterData `file:` → git URL + tag 전환** | 안정화 후 결정 | 패키지 3종 함께 전환 시점. topology Open Decisions |
| **메시지 id 생성기가 버려진 번호를 다시 채운다** | 판치기 타격 메시지가 **id 3**을 받았다 — 은퇴한 `DamageEventToC`가 쓰던 번호다. 생성기가 빈 번호를 앞에서부터 메우기 때문인데, 지금은 **클라를 늘 새로 빌드**하므로 무해하다. 위험해지는 건 *강제 갱신이 불가능한 클라가 밖에 나간 뒤*다 — 옛 클라가 3번을 `DamageEventToC`로 읽어 **에러 없이 엉뚱하게 해석**한다. 번호를 늘 증가시키거나(단조 증가) 은퇴 번호를 예약 목록에 남기는 두 방법이 있다 | **APK가 스토어/외부 배포로 나가기 전.** 그 전까지는 매 배포가 클라·서버를 함께 갈아치우므로 안전 |
| ~~**게임 씬 스코프 분리** (GamePlay 씬)~~ ✅ **종결(07-17)** — 이미 구현돼 있었고(Root→Room→Game 스코프 + `EnqueueParent(roomScope)` + additive 로드: `LOPGameFactory`/`GameLifetimeScope`/`RoomLifetimeScope`), **문서 정합 완료**: spec 상단에 "구현 완료 + 실제와의 차이" 배너 추가(씬명 `GamePlay`→`LOPGame`, gameInfo=`runner.Run` 파라미터[Enqueue 아님], SceneManager 채택, 수명제어=`IGameFactory` 캡슐화, Runner/World 리네임) + CLAUDE.md 자동로드 `@` 줄 제거(구현됨). | — |

---

## 2026-07-26 — 애니메이션 동기화 트랙 (진행 중) + 캐릭터별 어빌리티 로드아웃 (완료)

### ✅ 접지를 World 상태로 승격 + 스냅샷 복제 (슬라이스 1)
GF `GroundState` 신설 — `KinematicMover`가 계산해 **버리던** 접지를 `KinematicMoveSystem`이 기록.
`EntitySnap.grounded`(8)로 복제, 클라 뷰가 발밑 콜라이더 **이름(`"Plane"`)을 뒤지던 임시 판정을 삭제**.
머지: GF `e32469a` / Shared `ed910dd` / Server `ebc0f0b` / Client `6b921e9`.
spec `2026-07-25-animation-state-sync-design.md`.

### ✅ 시전 상태 스냅샷 복제 (슬라이스 2, 완료)
스킬 시전 모션이 **일회성 이벤트**로만 전달돼 늦게 접속하거나 패킷을 놓친 클라에선 시전 중인
캐릭터가 서 있는 걸로 보이던 문제. 지속 상태로 복제해 해소.
- **배관**(선행 머지): 진행도 커널 `AbilityPlayback.Solve` + 연출용 `AbilityActivation.ForPresentation`,
  와이어 `active_ability_id`(9)/`ability_end_tick`(10), 클라 원격 복원(종료 틱에서 페이즈 경계 역산).
  Shared `2b9e578` / Server `e41791d` / Client `e645a9d`.
- **뷰 전환**(Task 10·11): `TbAbilityView`(클라 전용, 어빌리티 id → 애니 스테이트/레이어) 신설 +
  `TbAbility.cue` 제거, 클라 `EntityRenderClock`(내 캐릭=예측 틱 / 남=보간 재생 시계 — 위치와 애니가
  같은 시점을 보게), `LOPEntityView`가 트리거 대신 지속 상태에서 파생.
  infra `89ec221` / MD-C `8c150b6` / Client `327f7e6`+`4e5cb54`.
- **플랜 대비 교정 3건**: ① 애니 이름은 트리거 파라미터가 아니라 **스테이트 이름**
  (3=`Attack 01` / 5=`melee attack with wand` / 6=`Attack`, 셋 다 Base Layer 0 — 컨트롤러 파싱으로 확인).
  ② 진행도 드리프트 재동기 **삭제** — 클립 길이 ≠ 어빌리티 길이라 끝에서 되감겨 덜덜거림. 발동마다 한 번만.
  ③ 새 발동 판별 키에 **종료 틱** 포함 — `abilityId`만으로는 같은 스킬 연타 시 두 번째가 안 걸림.
- **재생 방식**(사용자 논의 후 확정): `Play`(하드컷) → **`CrossFadeInFixedTime(state, 0.1s, layer, 경과 초)`**.
  `CrossFade`(정규화)는 섞는 시간이 *출발 동작 길이의 비율*이라 Idle/Run에서 시작할 때 길이가 달라짐 →
  초 단위 쪽. 시작 지점도 진행도(0~1)가 아니라 **발동 후 경과 초** — 비율로 넣으면 클립 길이가
  어빌리티 길이와 다를 때 발동을 본 클라와 놓친 클라가 다른 포즈를 그린다.
  언리얼 몽타주 `InTimeToStartMontageAt`(초) + 애셋 Blend In과 같은 매핑. 블렌드 시간은 상수 1개라
  데이터 컬럼화는 보류(스킬별로 달라져야 할 때 `TbAbilityView`에 추가).
- **미포함**: `global_attack`(4, G키 테스트용)은 세 캐릭터가 공유해 애니 이름이 하나로 안 정해지고
  지속 1틱이라 상태 기반으로 못 그림 → 뷰 행 없음(모션 없음, 데미지·넉백은 그대로).
- 검증: 클·서 컴파일 클린, EditMode 332/332, 인게임(3캐릭터 모션 + 손실 20~30% 모션 누락 0 + 블렌딩).

### ✅ 캐릭터별 어빌리티 로드아웃 + 슬롯 장착 (트랙 B, 전량 완료)
슬라이스 2가 막힌 원인 해소 — 캐릭터 3종이 같은 `attack`(id 3)을 쓰는데 공격 애니 스테이트
이름이 각각 달라(`Attack 01`/`Melee Attack`/`Attack`) "어빌리티 id → 애니 이름" 매핑이 성립하지
않았다. **어빌리티를 캐릭터별로 갈라** id가 캐릭터를 구분하게 만들어 단일 키로 해소.

- **어휘 정정 3층**: `AbilityData`(정의) / **`GrantedAbility`**(부여 기록 + 슬롯 + 쿨다운, 구 `AbilitySlot`) /
  **`AbilityActivation`**(진행 중 발동, 구 `ActiveAbility`). GAS `FGameplayAbilitySpec`·인스턴스 대응.
  "슬롯"이 부여 기록을 잘못 가리키던 것을 **장착 자리**라는 표준 의미로 되찾음(GAS `InputID` 대응).
- **슬롯 도입**: `Grant(entity, abilityId, slot)` + 코어 순수 조회 `TryGetAbilityIdBySlot` /
  side-local `AbilityActivator.TryActivateSlot`. 버튼·AI가 id 대신 자리를 가리킨다.
- **`TbCharacterLoadout`**(캐릭터→슬롯→어빌리티, 클·서 공용) + 캐릭터별 공격 행(5=necro, 6=archer).
  `CharacterCreator`의 `Grant` 하드코딩 전멸.
- **함정 박제**: `TryActivate`의 쿨다운 갱신이 부여 기록을 통째로 덮어써 **발동할 때마다 슬롯이
  0으로 지워지는** 버그 — 컴파일·기존 테스트를 다 통과하는 종류. 보존 코드 + 회귀 테스트로 고정.
- 머지: infra `ff69782` / MD-C `e687273` / MD-S `bdb9626` / Shared `5ad33c9` / Server `e27e35d` / Client `668ae79`.
  EditMode 324/324. spec `2026-07-26-character-ability-loadout-design.md`.
- **미채택(범위 밖)**: 반응형 패시브(현 구조는 동시 발동 1개라 자리를 영구 점유하면 전부 막힘 —
  상시형은 `StatusEffects` + `DurationPolicy.Infinite`로 이미 가능), 동시 발동 규칙(확장 시 표준은
  GAS 태그 규칙), 보유 풀과 장착의 분리(현재 보유=장착이라 구분할 데이터 없음).

### ✅ 후속 정리 + 버그 4건 (같은 날)
- **어휘 마감**: `Abilities.Current` → **`.Activation`** (`mana.Current`와 한 화면 충돌 + 스냅샷
  `PredictedAbilityState.Activation`과 이름 불일치 해소). 자동로드 문서 `entity-system-design.md`가
  삭제된 타입명을 가리키던 것도 수정. Shared `71ec791` / Client `01261bd` / Server `1e13182`.
- **마스터데이터 로더 목록 누락** — `LOPMasterData.TableFiles`(손 유지 배열)에 새 테이블을 안 넣어
  게임이 Entrance에서 `KeyNotFoundException`. **EditMode 테스트로 봉인**(`TableFileManifestTests`,
  클·서 양방향). MD-C `02ad2c0` / MD-S `6fd2ce5`.
- **전역 공격(G키) 사망** — `character_001`을 "플레이어"로 착각. `GameRuleSystem`이 플레이어에게
  세 캐릭터를 무작위 배정하므로 Knight를 안 뽑으면 슬롯 4가 없었다. 세 코드 전부에 행 추가.
  infra `8377686`.
- **원격 걷기 애니 부재**(기존 구멍, 이번 회귀 아님) — 보간이 velocity를 World에 안 썼다.
  **위치 곡선의 미분**(`Hermite.Velocity`, GF 신규 + EditMode 6케이스)으로 산출해 반영.
  GF `7b26974` / Client `baad313`.

### ✅ 상태이상 — 명중자 부여 + 복제 + 몸에 붙는 VFX (슬라이스 3)
상태이상이 **자기 자신에게만** 걸렸고, 클라는 그 존재를 **아예 몰랐다**. 그래서 헤이스트는 걸려도
화면에 아무 표시가 없었고 슬로우는 만들 수단 자체가 없었다. 7태스크로 끝-끝 해소.

- **`TargetType { Self, HitTargets }`** — 효과를 누구에게 걸지 데이터로 선언. 명중 대상은 데미지가
  이미 기록하므로(`AttackHitContext`) 넉백과 같은 on-hit 라이더 규칙을 따른다. 마스터데이터는
  `target_type` string 컬럼(`duration_policy`/`stack_policy`와 같은 방식, 새 Luban enum 안 만듦).
- **슬로우**(id 2, MoveSpeed −30%, 60틱) + **캐릭터별 공격 3·5·6 전부**에 라이더 부착.
  효과 순서 = 데미지 뒤(명중자를 데미지가 정하므로).
- **와이어**: `EntitySnap.status_effects`(11) + `ProtoActiveEffect`. MessageId 무변경.
- **⭐ 소유자 동기화** — 클라는 *내 캐릭만* 시뮬하므로 **남이 나에게 건 효과를 예측할 수 없다.**
  모르면 서버만 나를 70%로 움직여 매 틱 위치가 어긋난다(러버밴딩) — 연출을 빼도 발생하는
  넷코드 문제. `StatusEffectSystem.ApplyAuthoritativeState`(HealthSystem과 같은 이름·역할) +
  `Reconciler`가 `RestoreTo` **직후** 호출. 넉백(`MotionContributions`)이 이미 쓰던 규칙과 같은 축.
  **재조정 게이트도 확장** — 위치만 보면 가만히 서서 맞은 슬로우는 오차 0이라 영영 안 들어오고,
  이동 무관 효과(스턴·도트)는 구조적으로 도달 불가였다. 비교는 **앵커 틱끼리**(시점 정합),
  클라가 해석 못 하는 id는 제외(안 그러면 불일치 영구 미해소 → 롤백 무한 반복).
- **연출**: `TbStatusEffectView`(클라 전용, id → VFX 주소) + `StatusEffectVfxView`(매 프레임 상태
  목록을 읽어 맞춤, 루트에 부착). 에셋은 **더미**(`Assets/Art_Placeholder/`, 유니티 기본 파티클 +
  URP 셰이더, 외부 의존 0). **교체 = Excel 주소 한 줄 + 폴더 삭제, 코드 0줄.**
- **머지 완료(6 저장소)**: infra `7fed22b` / MD-C `0461058` / MD-S `d9b80c1` / Shared `aac5be7` /
  Server `8153535` / Client `686c164`. 머지 후 main에서 클·서 컴파일 클린 + EditMode 353/353 재확인.
- **인게임 검증 통과**(사용자): 헤이스트·슬로우 이펙트, 명중자에게만 부여(내 몸엔 안 뜸),
  **몬스터에게 맞았을 때 내 몸에 이펙트 + 느려짐 + 러버밴딩 없음**(핵심), 동시 적용, 정리, 회귀.
- **검증 중 배운 것(오진 주의)**: recon이 걷기만 해도 0이 아니어서 회귀를 의심했으나, 원인은
  이전 테스트에서 켜둔 **패킷 손실 30%** 설정이 남아 있던 것. 넉백 피격 시 recon이 한 번 튀고
  0으로 복귀하는 것도 정상 — 재조정이 위치뿐 아니라 **넉백 레시피(방향·세기·시작/종료 틱·감쇠)**
  까지 스냅에서 복원해 클라가 남은 밀림을 같은 코드로 재현하기 때문. 첫 튐 크기가 들쭉날쭉한 것은
  "서버가 몇 틱 밀어낸 뒤 알았나"(스냅 유실·합쳐짐)에 비례하며, HUD가 소수점 2자리라 5mm 미만은
  `0.00`으로 보인다. `Recon max`는 게임 시작 후 최댓값이라 내려가지 않는다.
- EditMode **353/353**(332 → +21). spec `2026-07-26-status-effect-vfx-design.md`,
  plan `2026-07-26-status-effect-vfx.md`.
- **후속(머지 차단 아님)**: 마스터데이터 검증 EditMode 테스트(모든 `StatusEffectApplyEffect` id가
  `TbStatusEffect`에 존재 + `target_type`이 유효 `TargetType`) — 클·서 provider가 unknown id에
  다르게 반응하는 것(클=null·서=throw)의 정석 해법 / 와이어에서 만드는 죽은 `sourceId` 제거 또는
  `SourceIdFor` 단일화 / `AbilityDataProvider` 매핑 캐시(원격 시전 중 매 스냅 `Enum.Parse`) /
  게이트가 id 집합만 봐 스택·만료틱 차이 미감지(이동 시 자동 해소) / 원격은 모디파이어 미적용이라
  원격 `Stats`를 읽는 코드가 생기면 함정.
- **범위 밖 발견**: `LOPEntityView.UpdateVisual`은 `await` 중 `Cleanup`이 핸들을 해제하면 해제된
  결과로 `Instantiate`한다(이번에 만든 `StatusEffectVfxView`는 같은 함정을 `ownsHandle`로 막았다).

### ✅ Knight 피격 애니 정렬 (별건, 같은 날 발견)
상태이상 검증 중 발견 — Knight만 맞고 나면 한참 서 있었다. **Knight만 피격 경로가 두 개**였다:
다른 둘처럼 Hit Layer에도 있고, Base Layer에도 있었다. Base Layer 쪽은 `Idle`에서만 진입 가능하고
나오는 길이 `Hit → Idle`(전이 0.54초)뿐이라 걷기가 밀려났다(다른 둘은 별도 레이어라 아래층이 계속 걷는다).
Base Layer의 `Hit` 상태와 `Idle → Hit` 전이를 제거해 구조를 맞췄고, **Hit Layer가 Archer 클립을
가리키던 것**도 Knight 자기 클립으로 정정. 반복 피격을 위해 `Hit → Empty State` 복귀 전이 추가.
Art 저장소 `0e136e0`(푸시됨), 클라 서브모듈 포인터 갱신. 사용자 인게임 확인 완료.

> 남은 것(사용자 결정으로 보류): **넉백 중 리액션 모션 없음** — 밀려나는 동안 재생할 애니가 없어
> 선 자세로 미끄러진다. 콘텐츠 구멍이라 별건.
>
> ⚠️ Art 저장소는 **작업 사본이 두 벌**이다(`LOP/LeagueOfPhysical-Art` 별도 클론 + 클라
> `Assets/Art` 서브모듈). 한쪽에서 커밋하면 다른 쪽엔 안 보인다 — 원격을 거쳐야 만난다.

### ✅ 후속 정리 4건 (같은 날, 트랙 종결 직후)
- **`LOPEntityView.UpdateVisual` 비동기 로딩 레이스** (진짜 결함) — `await` 뒤에 아무 검사가 없어,
  모델 로딩 중 `Cleanup`이 돌면 **해제된 에셋으로 파괴된 부모 밑에** 인스턴스를 만들었다.
  요청 카운터로 소유권을 명시(`Cleanup`이 카운터부터 올려 진행 중 로딩이 자기 handle을 스스로 놓게)
  → handle 하나당 해제 정확히 1회. 같은 슬라이스에서 만든 `StatusEffectVfxView`의 검증된 패턴.
- **`"se:{id}"` 규약 단일화** — 와이어 변환이 `StatusEffectSystem.SourceIdFor`의 문자열 규약을
  손으로 복제하고 있었다. public static으로 노출해 정의를 한 곳으로.
- **`AbilityDataProvider` 매핑 캐시** (클·서 동일) — 호출마다 Luban 행을 재매핑 + `Enum.Parse`(리플렉션)
  하던 것을 메모이즈. 클라는 원격이 시전 중인 동안 매 스냅 호출한다.
- **`AbilityDataIntegrityTests`** (MD-Client EditMode) — 모든 `StatusEffectApplyEffect`의
  상태이상 id가 `TbStatusEffect`에 존재하고 `target_type`이 유효한지 검증. **클라가 런타임에
  모르는 id를 조용히 건너뛰어도 되는 근거**가 이 테스트다(예외가 시뮬 틱 한복판에서 터지면 크래시).
  ⚠️ 어셈블리 경계 때문에 `TargetType` 멤버 이름을 하드코딩 — enum이 바뀌면 같이 갱신해야 한다.
- 머지: Shared `49abdf8` / Client `43f170d` / Server `a2eabb7` / MD-C `8f8642d`. EditMode 354/354.

EditMode 354/354. **다음 = 새 트랙.** 애니 동기화 트랙(슬라이스 1·2·3) + 후속 정리까지 종결.

---

## 🔴 Flappy 몸싸움 정합성 — 남의 새를 추측으로 밀고 있다 (2026-08-25 발견, 미해결)

**증상이 아니라 구조 문제다.** 세 조각이 겹쳐서 생긴다:

1. `FlappyBodyCollisionSystem` — 새끼리 부딪히면 서로 밀어내고 세로 속도를 주고받는다.
   주석대로 **"새끼리 몸싸움이 게임성"** 이다.
2. `OwnerPredictedRemotesExtrapolatedSyncPolicy`(2026-08-24 도입) — 남의 새는 굴리지 않고
   마지막 속도로 **외삽**한다. 즉 내 새는 *추측된* 남의 위치에 부딪힌다.
3. `NoServerCorrection` — 스냅샷으로 위치를 보정하지 않는다. **어긋난 몸싸움 결과가 영영 안 고쳐진다.**

②는 ①을 위해 도입된 게 아니다. 그 반대다 — 원래 `CharactersPredictedSyncPolicy`(남도 같이 굴림)였는데,
**남의 플랩 입력이 클라에 오지 않아** 시뮬로 굴리면 "계속 추락"이 나와서 외삽으로 바꿨다.
그 교체가 몸싸움 판정을 추측 위에 올려놓았고, ③이 그걸 고칠 길을 막고 있다.

### 선택지

| | 남의 새 | 필요한 것 | 몸싸움 정합 |
|---|---|---|---|
| A (현재) | 외삽 | 없음 | ❌ 추측 위치로 판정 + 보정 없음 |
| B | 시뮬 | **남의 입력을 와이어로** | ✅ 같은 입력 → 같은 결과 |
| C | 지연 보간 | 없음 | 판정을 서버에만 맡김(반응성 손해) |

**B의 통로는 이미 있다** — `InputSequenceToC { entity_id, input_sequence }`가 proto에 정의돼 있으나
**어느 레포에서도 쓰이지 않는다**(죽은 와이어). 정확히 이 용도의 모양이다.
B의 진짜 비용은 와이어가 아니라 타이밍이다: 남의 입력은 그 틱을 굴리기 *전에* 와야 하는데 클라는
서버보다 앞서 달린다. → 입력 지연(락스텝)이거나, 도착 전 외삽 + 도착 시 롤백 재생(GGPO/Quantum).
후자의 뼈대는 `Reconciler`(스냅샷+재생)에 이미 있다.

### 상태

미해결. 시작 게이트 슬라이스의 눈 검증에서 **"남의 새가 실제로 얼마나 어색한가"** 를 보고 나서
브레인스토밍으로 연다 — A로 바꾼 이유가 그 어색함이었으므로, 개선 폭이 B의 필요성 판단에 직접 들어간다.
`NoServerCorrection`은 A를 유지하든 B로 가든 답이 필요하다.


## 상태

이 파일은 **2026-07-09 생성**, Stage④ 프론티어 + 최근 넷코드/이동 워크스트림을 시드했다. 나머지 워크스트림 상태는 각 메모리에 남아 있으며, **손댈 때 기회 있을 때** 이리로 점진 이관한다(일괄 이관 안 함).
