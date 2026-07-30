# 게임 룸 포트 노출 — Agones 표준(hostPort + 고정 포트 범위)으로 전환

클라가 게임 룸에 **한 번도 접속해 본 적이 없다.** 매칭·룸 생성·게임서버 기동은 모두 되는데,
마지막 한 걸음인 "클라 → 게임서버 UDP 접속"만 구조적으로 막혀 있었다. 이 문서는 그 원인과
업계 표준으로의 전환을 정한다.

## 1. 지금 어떻게 되어 있나

룸 서버가 룸마다 **파드 1개 + NodePort Service 1개**를 만들고, Service가 받은 `nodePort`를
룸 레코드에 적어 클라에게 알려 준다.

```
room.service.ts:176   type: 'NodePort'
room.service.ts:184   const nodePort = service.spec?.ports?.[0]?.nodePort;
room.service.ts:188   room.port = nodePort;          // 예: 30691
room.ip = process.env.GAME_SERVER_PUBLIC_IP          // 로컬: "localhost"
```

클라는 `roomDataStore.room.ip:port`로 Mirror(KCP/UDP) 접속한다.

## 2. 왜 안 되나

### 2-1. 로컬 — 노드가 내 PC가 아니다

로컬 클러스터는 **Docker Desktop 내장 쿠버네티스**(kind 기반, 노드 `desktop-control-plane`)다.
노드는 Docker Desktop이 관리하는 **숨은 컨테이너**이고 `docker ps`에도 안 보인다. 호스트로 공개된
포트는 `6443/tcp`(API 서버) **하나뿐**이다.

NodePort는 *노드의* 포트를 여는 것이므로, 그 포트는 내 PC에 나타나지 않는다. 그래서
`room.ip = "localhost"`가 **로컬에서는 거짓**이 된다 — 노드가 호스트가 아니기 때문이다.

**실측 (2026-07-30):** 같은 ingress 컨트롤러를 두 경로로 접근

| 서비스 타입 | 주소 | 결과 |
|---|---|---|
| LoadBalancer | `localhost:80` | **200** |
| NodePort | `localhost:31000` | **실패(000)** |

클라 콘솔도 같은 얘기를 한다 — `connect to localhost:30691` → *"the other end has closed the
connection"*(=아무도 안 듣는 UDP 포트 → ICMP port unreachable). 게임서버 로그에는 **접속 흔적 0건**
(패킷이 도달조차 못 함).

### 2-2. dev — 범위가 열려 있지 않다

dev(`115.68.178.46`)는 노드가 공인 IP를 가진 리눅스 서버라 NodePort가 원리적으로 닿는다. 실제로
ingress의 `31000`은 **인터넷에서 200으로 응답한다**(실측). 그러나 룸은 30000–32767 중 *임의* 번호를
받으므로, 그 대역이 방화벽에 열려 있지 않으면 똑같이 실패한다. `30691`은 무응답이었다(포트에 아무것도
없어서인지 방화벽인지는 미구분).

**즉 "룸이 쓸 포트를 작은 고정 범위로 정하는 일"은 로컬 우회가 아니라 어차피 필요한 일이다.**

### 2-3. 왜 여태 안 드러났나

`2026-07-12` spec이 *"실제 게임플레이 검증은 pod 기동·접속 확인까지가 이 작업 범위"* 라고 명시해
**클라 접속 경로를 검증 범위에서 제외**했다. 그리고 infrastructure에 **로컬 클러스터 생성 설정 파일이
아예 없다** — "어떤 포트를 호스트로 열지"를 정한 적이 없고 Docker Desktop이 준 클러스터를 그대로
써 왔다.

## 3. 업계 표준 — Agones

전용 게임서버를 쿠버네티스에 올리는 사실상 표준(Agones)은 세 방식을 명확히 가른다.

| 방식 | Agones의 판단 |
|---|---|
| **LoadBalancer** | ✗ *"UDP 패킷을 특정 게임서버 인스턴스로 라우팅할 수 없다"* + 홉 증가로 지연 |
| **NodePort** | ✗ kube-proxy를 한 번 더 거치는 불필요한 홉 — *"노드로 직접 가는 게 낫다"* |
| **`hostPort`** | ✅ 노드의 포트를 열어 iptables/ipvs로 컨테이너에 직접 라우팅 |

그리고:

- 포트는 **설치 시 정한 `MIN_PORT`~`MAX_PORT` 범위에서 동적 할당**한다. 기본값 **UDP 7000–8000**.
- **그 범위에 방화벽 규칙을 연다.**
- 클라는 오케스트레이터가 알려주는 **노드 IP + 그 포트**로 직접 접속한다.
- **게임서버마다 Service를 만들지 않는다.**
- 할당 정책 3종: `Static`(사용자 지정) / `Dynamic`(범위에서 자동) / `Passthrough`(컨테이너 포트 =
  hostPort).

## 4. 결정

### 4-1. `hostPort` + 고정 범위 + Service 제거

룸 파드가 `hostPort`로 노드 포트를 직접 점유하고, **룸마다 만들던 Service를 없앤다.**

```
파드 컨테이너:  containerPort = hostPort = <할당 포트>,  protocol: UDP
파드 env:      PORT = <할당 포트>          ← 게임서버가 이 값으로 바인딩(기존 그대로)
룸 레코드:      ip = GAME_SERVER_PUBLIC_IP,  port = <할당 포트>
```

**정책은 `Passthrough`에 대응한다** — LOP 게임서버는 이미 `PORT` 환경변수를 읽어 바인딩하므로
(`ConfigureRoomComponent`), 컨테이너 포트와 hostPort를 같은 값으로 두는 것이 자연스럽고 변환이 없다.
`7777` 고정값은 사라진다.

### 4-2. 포트 범위 — 방식은 환경 공통, 크기는 환경별

| 설정 | 로컬 | dev / 향후 prod | 어디에 |
|---|---|---|---|
| `ROOM_PORT_MIN` | `7000` | `7000` | 룸 서버 env (ConfigMap) |
| `ROOM_PORT_MAX` | `7009` (10개) | `7999` (1000개) | 룸 서버 env (ConfigMap) |

**무엇이 같아야 하고 무엇이 달라도 되는지 구분한다:**

- **방식은 환경 공통이어야 한다** — "hostPort + env로 받은 범위에서 빈 포트 할당". 이게 환경마다
  다르면 "로컬에서만 검증된 경로"가 생기고, 이 트랙에서 나온 문제들이 전부 그 종류였다.
- **범위 크기는 달라도 된다** — 레플리카 수·리소스 상한처럼 **용량 설정**이다. 같은 코드가 "주어진
  범위에서 고른다"를 수행하므로 10이든 1000이든 코드 경로가 동일하고, 검증 공백이 생기지 않는다.
  Agones도 `minPort`/`maxPort`를 **클러스터마다 설치 시 정하는 값**으로 둔다.

**왜 로컬만 좁은가:** kind는 `extraPortMappings`에 포트를 **한 줄씩 나열**해야 한다(범위 문법 없음).
반면 실제 클러스터는 노드 포트가 그냥 열려 있고 방화벽 규칙 **한 줄**로 범위를 통제하므로 넓혀도
비용이 없다.

시작 번호는 어디서나 `7000`으로 같다 — Agones 기본 범위의 시작값을 그대로 따른다(임의 선택 아님).
로컬 동시 룸 10개는 개발·검증에 충분하고, 늘리려면 kind 설정에 줄을 추가하고 `ROOM_PORT_MAX`를
올리면 된다.

### 4-3. 할당 방법 — DB의 사용 중 포트를 빼고 고른다

룸 레코드가 이미 `port`를 들고 있으므로 별도 저장소가 필요 없다.

```
사용중 = SELECT port FROM Room WHERE status NOT IN (Closed, Error)
후보  = [ROOM_PORT_MIN..ROOM_PORT_MAX] - 사용중
빈 후보 없음 → 명확한 에러로 거절 (룸 생성 실패)
```

- **동시 생성 경쟁**: 같은 포트를 두 룸에 줄 수 있다. `Room.port`에 **부분 유니크 제약**(살아 있는
  룸에 대해서만)을 걸고, 충돌 시 다음 후보로 재시도한다. Director가 단일 프로세스인 매칭 구조상
  실제 경쟁 확률은 낮지만, 제약이 없으면 조용히 깨지므로 DB가 막게 한다.
- **단일 노드 전제**: hostPort는 노드마다 독립이라 멀티 노드에서는 "노드별 범위"가 되어야 한다.
  로컬·dev 모두 단일 노드이므로 지금은 전역 풀로 둔다 — 멀티 노드는 §7.

### 4-4. 로컬 클러스터를 `kind`로 교체

Docker Desktop 내장 쿠버네티스는 **관리형이라 포트 매핑을 추가할 수 없다.** 자체 `kind` 클러스터로
바꾸고 범위를 호스트에 매핑한다. 이 설정 파일을 **infrastructure에 신설**한다(지금은 없다).

```yaml
# infrastructure/k8s/local/kind-cluster.yaml
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
  - role: control-plane
    extraPortMappings:
      - { containerPort: 80,   hostPort: 80,   protocol: TCP }   # ingress
      - { containerPort: 443,  hostPort: 443,  protocol: TCP }
      - { containerPort: 7000, hostPort: 7000, protocol: UDP }   # 룸 포트 풀
      # 7001 … 7009 동일
```

> `hostPort`(파드) → 노드 포트 → `extraPortMappings` → 내 PC. 세 단계가 같은 번호로 이어진다.

dev는 자기 범위(`7000`–`7999`)를 **방화벽에서 UDP로 개방**한다(사용자 작업). 방화벽은 범위 한 줄이라
로컬처럼 나열할 필요가 없다.

## 5. 무엇이 사라지나

- 룸마다 만들던 `room-service-*` **Service 오브젝트** — 생성·삭제 코드 모두 제거
- `nodePort` 조회 로직과 그 실패 처리
- 컨테이너 포트 하드코딩 `7777`

## 6. 검증

| 대상 | 방법 |
|---|---|
| 포트 할당 | 룸 서버 유닛 테스트 — 빈 풀에서 순차 할당 / 사용 중 제외 / 고갈 시 명확한 에러 / 충돌 재시도 |
| 파드 스펙 | 생성된 파드에 `hostPort == containerPort == PORT env` 인지 |
| Service 없음 | 룸 생성 후 `room-service-*`가 만들어지지 않는지 |
| 로컬 도달성 | 룸 생성 후 호스트에서 그 UDP 포트에 리스너가 보이는지(`netstat`) |
| **끝-끝** | 클라 2개 매칭 → 실제 입장 → 이동·전투 → 종료 |

## 7. 범위 밖

- **멀티 노드** — hostPort 풀은 노드별로 독립이므로 스케줄된 노드를 알아야 한다(Agones는 컨트롤러가
  노드별로 추적). 단일 노드를 벗어날 때.
- **Agones 도입 자체 — 지금은 안 하지만 *정식 도입을 지향*한다(방향 확정).** 이 슬라이스가 만든 것은
  Agones의 **부분집합**이며 짬뽕이 아니다: `hostPort`·포트 범위 동적 할당·GameServer당 Service 없음·
  오케스트레이터가 접속 주소 반환이 모두 1:1 대응한다. **빠진 핵심은 `Fleet`(사전 기동 서버 풀)** 이고,
  그래서 매치마다 이미지 pull + Unity 부팅 ~30초를 기다리며 하트비트 임계값을 60초로 올려야 했다 —
  Agones의 주된 가치가 바로 "준 서버를 즉시 배정"이다. 도입 시 순서: ① Agones 설치
  (`gameservers.minPort/maxPort` = 이 spec의 범위) ② 게임서버에 Agones SDK(`Ready`/`Health`/`Shutdown`)
  통합 ③ `Fleet`으로 사전 기동 ④ room-server가 파드 생성 대신 `GameServerAllocation` 호출.
  그때 **삭제**되는 것은 `roomPort.ts`·파드 생성 코드·HTTP 하트비트 배관이고, **유지**되는 것은
  hostPort 방식·포트 범위 개념·kind 포트 매핑(로컬)·방화벽(dev)이다.
- **dev 방화벽 자동화** — 수동 개방.
- **`GAME_SERVER_PUBLIC_IP` 자동 조회**(downward API/노드 IP) — 기존 spec에서 이미 범위 밖.

## 8. 산업 표준 매핑

| LOP | Agones | 근거 |
|---|---|---|
| 룸 파드 `hostPort` | GameServer `hostPort` | LB/NodePort를 배제한 동일 이유(UDP 인스턴스 라우팅 불가, 홉 제거) |
| `ROOM_PORT_MIN`/`MAX` | `gameservers.minPort`/`maxPort` | 설치 시 범위 지정 + 방화벽 개방 |
| 시작 번호 `7000` | 기본 범위 `7000`–`8000` | 표준값 차용 |
| 컨테이너 포트 = hostPort = `PORT` env | `portPolicy: Passthrough` | 게임서버가 주입된 포트로 바인딩 |
| 룸 레코드 `ip`+`port` | `status.address` + `status.ports[].port` | 오케스트레이터가 접속 주소를 알려 줌 |
| 룸당 Service 없음 | GameServer당 Service 없음 | |

**참고:** [Agones — GameServer Specification](https://agones.dev/site/docs/reference/gameserver/) ·
[Agones FAQ (LB/NodePort 대신 hostPort인 이유)](https://agones.dev/site/docs/faq/) ·
[Agones Series Part 2 — Address and Port of the Game Server](https://www.alibabacloud.com/blog/agones-series-part-2-address-and-port-of-the-game-server_599427) ·
[kind — extraPortMappings](https://kind.sigs.k8s.io/docs/user/configuration/#extra-port-mappings)

## 9. 실행 순서

1. 룸 서버: 포트 풀 할당 + `hostPort` + Service 제거 (+ 유닛 테스트)
2. infrastructure: `kind-cluster.yaml` 신설 + 룸 서버 ConfigMap에 범위 추가
3. 로컬: `kind` 설치 → 클러스터 교체 → ArgoCD 재부트스트랩(기존 절차)
4. dev: 방화벽 UDP `7000–7999` 개방 + 그 ConfigMap의 `ROOM_PORT_MAX=7999`
5. 끝-끝 검증

3·4는 사용자 머신·인프라 작업이다.
