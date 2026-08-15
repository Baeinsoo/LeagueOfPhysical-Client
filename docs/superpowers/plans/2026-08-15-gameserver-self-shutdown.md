# 게임서버 자가 종료 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치가 끝나면 게임서버가 클라들을 배웅한 뒤 **스스로 프로세스를 종료**해, 파드와 hostPort 반납이 백엔드 생사와 무관해지게 한다.

**Architecture:** 세 조각이 한 덩어리다. ① 게임서버가 통보 후 세션이 다 빠질 때까지 기다렸다 `Application.Quit()` ② 파드에 `restartPolicy: Never`(없으면 kubelet이 되살려 방이 부활한다) ③ 백엔드 파드 GC가 "이미 끝난 파드"도 수거. ①만 넣으면 지금보다 나빠지므로 ②는 선택이 아니다.

**Tech Stack:** Unity C# / UniTask / Mirror (LeagueOfPhysical-Server) · TypeScript / Node / jest (lop-backend room-server)

**Spec:** `docs/superpowers/specs/2026-08-15-gameserver-self-shutdown-design.md` (클라 레포)

## Global Constraints

- **레포 2개 · 브랜치는 각 레포마다 따로.** 어떤 레포에서도 `main`에 직접 커밋하지 않는다.
  - `LeagueOfPhysical-Server` → 브랜치 `feature/gameserver-self-shutdown` (Task 1)
  - `lop-backend` → 브랜치 `feature/gameserver-self-shutdown` (Task 2, 3)
  - 클라 레포는 이미 `feature/gameserver-self-shutdown` 브랜치에 spec/plan이 있다(코드 변경 없음).
- **Unity 레포는 워크트리를 쓰지 않는다.** 연결된 에디터가 main 체크아웃을 보므로, 그 체크아웃에서 `git switch -c`로 작업한다.
- **⚠️ `LeagueOfPhysical-Server`에 커밋 금지 로컬 픽스처가 있다**: `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/Scripts/Game/GameRuleSystem.cs`, `Assets/DefaultVolumeProfile.asset`. **`git add -A`/`git add .` 금지**, 경로를 명시해 stage하고 커밋 후 `git show --stat HEAD`로 파일 수를 확인한다.
- **백엔드 테스트는 빌드가 타입검사하지 않는다** — `apps/*/tsconfig.json`이 `__tests__`를 exclude한다. `pnpm build` 통과는 테스트 통과가 아니다. 반드시 jest를 돌린다.
- **에디터에서 `Application.Quit()`은 no-op다.** 이는 의도된 동작이며 `EditorApplication.isPlaying = false`로 대체하지 않는다(개발자 에디터 세션을 예고 없이 끊는다).
- 주석은 한국어, **왜**만, 일상어로. 코드로 자명한 것은 주석 없이 둔다.
- 커밋 메시지·문서·답변은 한국어.

---

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs` (수정) | 매치 종료 시 배수 후 자가 종료 | 1 |
| `lop-backend/apps/room-server/src/services/gameServerPod.ts` (수정) | 파드 매니페스트에 `restartPolicy: Never` | 2 |
| `lop-backend/apps/room-server/src/services/__tests__/gameServerPod.test.ts` (수정) | 위 값을 고정 | 2 |
| `lop-backend/apps/room-server/src/services/room.service.ts` (수정) | 끝난 파드도 GC 대상에 포함 | 3 |
| `lop-backend/apps/room-server/src/services/__tests__/room.service.test.ts` (수정) | GC 조건 확장을 고정 | 3 |

---

### Task 1: 게임서버 — 배수 후 자가 종료

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs` — `CloseRoomAsync`(현재 파일 하단, `OnGameStateChanged` 바로 아래) + 클래스 상수

**Interfaces:**
- Consumes: `ISessionManager.GetAllSessions()` → `IEnumerable<ISession>`; `ISession.isConnected` → `bool` (GameFramework `Session/ISession.cs`). `LOPSession.isConnected`는 `networkConnection != null && networkConnection.isReady`.
- Produces: (없음 — 이 레포 내부에서 끝난다)

**작업 시작 전:** 서버 레포의 원래 체크아웃에서 브랜치를 만든다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git switch -c feature/gameserver-self-shutdown
```

> **테스트 없음(의도).** 이 레포의 앱 코드는 asmdef 없이 `Assembly-CSharp`에 있어 EditMode 유닛 테스트를 붙일 수 없다. 검증은 컴파일 게이트 + Task 4의 인게임 시나리오다.

- [ ] **Step 1: 배수 타임아웃 상수를 추가한다**

`LOPRoom.cs` 상단 상수 블록(현재 `CLOSE_TIMEOUT_SECONDS` 아래)에 한 줄 추가한다.

```csharp
        private const int HEARTBEAT_INTERVAL = 2;       //  sec
        private const double TICK_INTERVAL = 1 / 50d;   //  sec
        private const double CLOSE_TIMEOUT_SECONDS = 1.5;
        private const double DRAIN_TIMEOUT_SECONDS = 10;
```

- [ ] **Step 2: `CloseRoomAsync` 끝에 배수 + 종료를 붙인다**

현재 `CloseRoomAsync`의 마지막 `foreach` 블록은 그대로 두고, **그 아래에** 이어 붙인다.

```csharp
            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(new MatchEndedToC());
            }

            //  클라는 결과를 받으면 로비 씬을 로드하고, 그때 스스로 연결을 끊는다. 그러니
            //  "다 나갔다"가 곧 "다 받았다"는 뜻이라, 이걸 기다렸다 끄는 게 가장 안전하다.
            //  고정 시간만 기다렸다 끄면 아직 못 받은 클라의 소켓이 죽는데, 클라에는 끊김을
            //  처리하는 곳이 없어서(onStopClient 미사용) 그 사람은 끝난 방에 갇힌다.
            try
            {
                using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(DRAIN_TIMEOUT_SECONDS));
                await UniTask.WaitUntil(
                    () => sessionManager.GetAllSessions().All(session => session.isConnected == false),
                    cancellationToken: drainCts.Token);
            }
            catch (OperationCanceledException)
            {
                //  느린 클라 하나 때문에 파드가 영원히 안 죽는 것만 막는다. 남은 사람은
                //  백엔드의 위치 자가치유가 로비에서 풀어 준다.
                Debug.LogWarning($"Drain timed out after {DRAIN_TIMEOUT_SECONDS}s. Quitting anyway.");
            }

            //  스스로 빠진다 — 백엔드가 파드를 지워 주기를 기다리지 않는다. 백엔드가 죽어 있어도
            //  포트와 파드가 즉시 반납된다. (에디터에서는 no-op이라 플레이 모드가 안 꺼진다.)
            Application.Quit();
```

`System.Linq`·`System.Threading`·`Cysharp.Threading.Tasks`는 이미 이 파일에 import돼 있다.

- [ ] **Step 3: 컴파일을 확인한다**

Run:
```bash
bash "/c/Users/re5na/AppData/Local/Temp/claude/C--Users-re5na-workspace-LOP-LeagueOfPhysical-Client/68ae091f-9348-4e51-9f55-16391d7d11c2/scratchpad/compile-server.sh"
```

이 스크립트는 Unity가 `Library/Bee`에 남긴 응답 파일 + 번들 Roslyn으로 서버 `Assembly-CSharp`를 컴파일하고, 산출물은 스크래치로 빼 Unity 실제 artifact를 건드리지 않는다.

Expected: `csc exit: 0`, 에러 0. **경고는 아래 4건 외에 새로 생기면 안 된다** — `LOPRunner.cs`의 CS0618 `Physics.autoSyncTransforms` ×3, `LOPNetworkAuthenticator.cs(130,31)`의 CS1998.

> 스크립트가 없으면(스크래치 정리됨) 다시 만든다: Unity `6000.3.16f1`의 `NetCoreRuntime/dotnet.exe`로 `DotNetSdkRoslyn/csc.dll`을 실행하고 `@Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp`를 주되, `-out`/`-refout`을 스크래치 경로로 덮어쓴다.

- [ ] **Step 4: 커밋 — 1파일만**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(room): 매치 종료 후 클라를 배웅하고 스스로 종료한다

지금까지 게임서버는 스스로 끝나지 않아 백엔드가 파드를 지워 줘야만
내려갔다. 백엔드가 죽어 있으면 좀비 파드가 남고 노드의 hostPort를
계속 붙든다.

- MatchEndedToC 전송 후 세션이 다 빠질 때까지 대기(타임아웃 10초)
- 그 뒤 Application.Quit()
- 고정 지연이 아니라 배수인 이유: 못 받은 클라의 소켓이 죽으면
  클라에 끊김 처리가 없어 끝난 방에 갇힌다"
git show --stat HEAD
```

Expected: `git show --stat HEAD`가 **정확히 1파일**(`Assets/Scripts/Room/LOPRoom.cs`)을 보여준다. 로컬 픽스처 3종은 미커밋으로 남아 있어야 한다(`git status --short`로 확인).

---

### Task 2: 백엔드 — 파드 `restartPolicy: Never`

**Files:**
- Modify: `lop-backend/apps/room-server/src/services/gameServerPod.ts` — `spec` 객체(현재 `terminationGracePeriodSeconds: 30`이 있는 곳)
- Test: `lop-backend/apps/room-server/src/services/__tests__/gameServerPod.test.ts` (기존 파일에 `it` 추가)

**Interfaces:**
- Consumes: (없음)
- Produces: 매니페스트 `spec.restartPolicy === 'Never'` — Task 1의 자가 종료가 이 값에 의존한다

**작업 시작 전:** lop-backend에서 브랜치를 만든다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git switch -c feature/gameserver-self-shutdown
```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`gameServerPod.test.ts`의 `describe` 안, 마지막 `it` 뒤에 추가한다.

```ts
    //  게임서버는 매치가 끝나면 스스로 종료한다. 기본값(Always)이면 kubelet이 그걸 되살리고,
    //  되살아난 컨테이너가 다시 하트비트를 보내 방이 "진행 중"으로 부활한다.
    it('restartPolicy를 Never로 둔다', () => {
        const manifest = buildGameServerPodManifest({ roomId: 'room-1', port: 7100 }) as any;

        expect(manifest.spec.restartPolicy).toBe('Never');
    });
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- gameServerPod
```
Expected: FAIL — `Expected: "Never"` / `Received: undefined`(매니페스트에 아직 그 키가 없다).

- [ ] **Step 3: 매니페스트에 추가한다**

`gameServerPod.ts`의 `spec` 객체에서 `terminationGracePeriodSeconds` 위에 넣는다.

```ts
        spec: {
            containers: [{
                //  ... 기존 그대로 ...
            }],
            //  게임서버는 매치가 끝나면 스스로 종료한다. 기본값(Always)이면 kubelet이 그걸
            //  되살리고, 되살아난 컨테이너의 하트비트가 이미 끝난 방을 "진행 중"으로 되돌린다.
            //  게임서버 상태(엔티티·위치·진행)는 어차피 복구 불가라 재시작에 의미도 없다.
            restartPolicy: 'Never',
            terminationGracePeriodSeconds: 30,
        },
```

- [ ] **Step 4: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- gameServerPod
```
Expected: PASS (기존 4건 + 신규 1건 = 5건).

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/room-server/src/services/gameServerPod.ts apps/room-server/src/services/__tests__/gameServerPod.test.ts
git commit -m "feat(room-server): 게임서버 파드를 restartPolicy Never로 띄운다

게임서버가 매치 종료 후 스스로 끝나게 되면서 필요해졌다. 기본값
Always면 kubelet이 그걸 되살리고, 되살아난 컨테이너의 하트비트가
이미 끝난 방을 '진행 중'으로 되돌린다."
```

---

### Task 3: 백엔드 — 끝난 파드도 GC 대상에 넣는다

**Files:**
- Modify: `lop-backend/apps/room-server/src/services/room.service.ts` — `deleteRunnersOfTerminatedRooms`(파드 루프만)
- Test: `lop-backend/apps/room-server/src/services/__tests__/room.service.test.ts` — 기존 `describe('RoomService.checkAndCleanupRoomRunners')` 블록에 추가

**Interfaces:**
- Consumes: Task 2의 `restartPolicy: 'Never'`(그래서 파드가 `Succeeded`로 남는다), 기존 `private shouldTerminateRoomRunner(room: Room): boolean`
- Produces: `private isPodFinished(pod: any): boolean` — 파드 `status.phase`가 `Succeeded` 또는 `Failed`면 true

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`room.service.test.ts`의 `describe('RoomService.checkAndCleanupRoomRunners')` 블록 안, 마지막 `it` 뒤에 추가한다. 파일 상단의 mock과 `roomOf` 헬퍼는 기존 것을 그대로 쓴다.

```ts
    //  게임서버가 스스로 끝나면 파드는 Succeeded로 남는다(포트·메모리는 이미 반납).
    //  백엔드가 죽어 있던 동안 매치가 끝났다면 룸은 아직 종단이 아니어서, 룸 상태만 보는
    //  기존 조건으로는 이 껍데기를 영원히 못 치운다.
    it('룸이 종단이 아니어도 이미 끝난 파드는 지운다', async () => {
        findAll.mockResolvedValue([roomOf({ id: 'R1', status: RoomStatus.GameInProgress })]);
        listPods.mockResolvedValue({
            items: [{
                metadata: { name: 'room-pod-R1', namespace: 'default', labels: { roomId: 'R1' } },
                status: { phase: 'Succeeded' },
            }],
        });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(deletePod).toHaveBeenCalledWith('room-pod-R1', 'default');
    });

    it('크래시로 끝난(Failed) 파드도 지운다', async () => {
        findAll.mockResolvedValue([roomOf({ id: 'R1', status: RoomStatus.GameInProgress })]);
        listPods.mockResolvedValue({
            items: [{
                metadata: { name: 'room-pod-R1', namespace: 'default', labels: { roomId: 'R1' } },
                status: { phase: 'Failed' },
            }],
        });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(deletePod).toHaveBeenCalledWith('room-pod-R1', 'default');
    });

    //  살아 있는 룸의 도는 파드를 지우면 진행 중인 매치가 끊긴다.
    it('살아 있는 룸의 Running 파드는 지우지 않는다', async () => {
        findAll.mockResolvedValue([roomOf({ id: 'R1', status: RoomStatus.GameInProgress })]);
        listPods.mockResolvedValue({
            items: [{
                metadata: { name: 'room-pod-R1', namespace: 'default', labels: { roomId: 'R1' } },
                status: { phase: 'Running' },
            }],
        });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(deletePod).not.toHaveBeenCalled();
    });
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- room.service
```
Expected: 앞의 두 건(`Succeeded`/`Failed`)이 FAIL — `deletePod`이 호출되지 않았다고 나온다. 세 번째(`Running`)는 이미 통과한다.

- [ ] **Step 3: 구현한다**

`deleteRunnersOfTerminatedRooms`의 **파드 루프 조건만** 바꾸고, 헬퍼를 하나 추가한다. **서비스 루프는 건드리지 않는다**(서비스는 파드처럼 "끝난 상태"를 갖지 않는다).

```ts
            if (pod.metadata?.name && pod.metadata?.namespace && room
                && (this.shouldTerminateRoomRunner(room) || RoomService.isPodFinished(pod))) {
                await k8sUtils.deletePod(pod.metadata.name, pod.metadata.namespace);
            }
```

그리고 클래스에 헬퍼를 추가한다(`isHeartbeatExpired` 옆).

```ts
    //  게임서버가 스스로 종료하면 컨테이너는 끝났는데 파드 "객체"는 남는다. 룸이 아직 종단이
    //  아닐 수 있으므로(백엔드가 죽어 있던 동안 매치가 끝난 경우) 룸 상태만으로는 못 치운다.
    private static isPodFinished(pod: { status?: { phase?: string } }): boolean {
        return pod.status?.phase === 'Succeeded' || pod.status?.phase === 'Failed';
    }
```

- [ ] **Step 4: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test && pnpm build
```
Expected: room-server 테스트 전부 PASS(직전 41건 + Task 2의 1건 + 이번 3건 = 45건), 루트 빌드 5/5.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/room-server/src/services/room.service.ts apps/room-server/src/services/__tests__/room.service.test.ts
git commit -m "fix(room-server): 이미 끝난 파드는 룸 상태와 무관하게 수거한다

게임서버가 스스로 종료하면 파드가 Succeeded로 남는다. 백엔드가 죽어
있던 동안 매치가 끝났다면 룸은 아직 종단이 아니어서, 룸 상태만 보는
기존 조건으로는 그 껍데기를 영원히 못 치운다."
```

---

### Task 4: 배포 · 인게임 검증 · 문서

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md` (유저 위치 트랙의 "다음에 할 것" 2번 + 본문 절 추가)

**Interfaces:**
- Consumes: Task 1~3 전부

> ⚠️ 이 태스크는 **push · main 머지 · 클러스터 배포 · 사람이 하는 플레이**를 포함한다. 에이전트가 자율 실행하지 않고 사용자 승인을 받아 진행한다.

- [ ] **Step 1: 배포**

두 브랜치를 push한 뒤 워크플로를 돌린다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git push -u origin feature/gameserver-self-shutdown
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git push -u origin feature/gameserver-self-shutdown
```

- `backend-deploy` — app: **room-server**, environment: **local** (마이그레이션 없음)
- `gameserver-deploy` — environment: **local** (맥 셀프호스트 러너 필요 — `gh api repos/Baeinsoo/LeagueOfPhysical-Server/actions/runners`로 online 확인)

⚠️ **롤아웃은 태그로 확인한다.** 막힌 롤아웃은 서비스를 안 죽이고 옛 버전이 계속 응답한다.

```bash
kubectl get pod -l app=room-server -o jsonpath='{.items[0].spec.containers[0].image}'
kubectl get cm -o name | grep game-server   # ConfigMap의 GAME_SERVER_IMAGE도 새 sha인지
```

⚠️ **새 게임서버 태그는 노드에 캐시가 없어 첫 매치가 콜드 pull로 실패할 수 있다**(하트비트 임계값 60초). 미리 받아 두면 그 변수가 사라진다:

```bash
docker pull re5nardo/game-server:<새 sha>
docker exec lop-control-plane crictl images | grep game-server   # 노드에 있는지 확인
```

- [ ] **Step 2: 인게임 ① — 정상 종료 시 파드가 스스로 사라진다**

파드를 지켜보며 매치를 한 판 끝낸다.

```bash
kubectl get pods -w | grep room-pod
```

Expected:
- 결과 창이 뜨고 유지된다(앞 트랙의 회귀가 없다)
- 룸 파드가 `Running` → `Completed`(Succeeded)로 **스스로** 바뀐다 — 백엔드가 지우기 전에
- 이어서 GC가 그 객체를 지운다

- [ ] **Step 3: 인게임 ② — 백엔드가 죽은 채 종료해도 좀비가 안 남는다**

앞 트랙에서 쓴 방법으로 백엔드를 내려두고 매치를 끝낸다. **`root`와 `backend` 양쪽 auto-sync를 꺼야 하고**(app-of-apps라 root가 backend 설정을 되돌린다), 다운 판정은 파드 수가 아니라 서비스 엔드포인트로 하며, 파드 종료 유예 30초를 넘겨 내려야 실제로 끊긴다.

```bash
OFF='{"spec":{"syncPolicy":{"automated":null}}}'
ON='{"spec":{"syncPolicy":{"automated":{"prune":true,"selfHeal":true}}}}'
kubectl -n argocd patch app root    --type merge -p "$OFF"
kubectl -n argocd patch app backend --type merge -p "$OFF"
kubectl scale deploy/room-server --replicas=0
kubectl get endpoints room-server-service -o jsonpath='{.subsets[*].addresses[*].ip}'   # 비어야 함
#   ... 매치 종료 ...
kubectl scale deploy/room-server --replicas=1
kubectl -n argocd patch app backend --type merge -p "$ON"
kubectl -n argocd patch app root    --type merge -p "$ON"
```

Expected: **룸 파드가 백엔드 없이도 스스로 끝난다**(`Completed`). 오늘 관측된 `GAMESERVER_PODS=1` 좀비가 이번엔 안 나온다. 백엔드 복구 후 GC가 껍데기를 치운다.

- [ ] **Step 4: 인게임 ③ — 백투백**

매치 종료 직후 곧바로 새 매칭을 건다. Expected: 같은 hostPort를 다시 받아도 정상 접속(포트가 실제로 반납됐다는 증거).

- [ ] **Step 5: ROADMAP 갱신**

`docs/ROADMAP.md`에서:
1. 유저 위치 트랙 "다음에 할 것"의 **2번(게임서버 자가 종료)을 완료로** 옮기고 번호를 정리한다.
2. 본문에 `### ✅ 게임서버 자가 종료 (2026-08-15, 2레포 머지)` 절을 추가한다 — 원인(스스로 안 끝나서 백엔드 의존), 세 변경, 인게임 결과, Agones 대조.

- [ ] **Step 6: 머지 · push**

최신 main 위로 리베이스한 뒤 `--no-ff` 머지한다(사용자 지정 방식).

```bash
git fetch origin && git rebase origin/main <브랜치> && git switch main && git merge --no-ff <브랜치>
```

3레포(Server / lop-backend / Client) 전부 처리하고 push한다.

---

## 자체 리뷰 결과

- **spec 커버리지**: 변경 1 → Task 1 / 변경 2 → Task 2 / 변경 3 → Task 3 / 검증 절 → Task 2·3의 jest + Task 4의 인게임 3건 / 범위 밖 4항목은 손대지 않음. 빠진 요구사항 없음.
- **타입 일관성**: `isPodFinished`(Task 3 정의·소비), `shouldTerminateRoomRunner`(기존, Task 3이 OR로 확장), `ISession.isConnected`(GameFramework 실재 확인), `DRAIN_TIMEOUT_SECONDS`(Task 1 정의·소비). Task 2의 `restartPolicy: 'Never'`가 Task 1 자가 종료의 전제임을 양쪽에 명시.
- **알려진 함정 반영**: 서버 레포 커밋 금지 픽스처 · 백엔드 테스트가 빌드 타입검사 밖 · 에디터 `Application.Quit()` no-op · 콜드 pull 60초 · app-of-apps의 root까지 꺼야 함 · 다운 판정은 엔드포인트 · 막힌 롤아웃은 옛 버전이 응답.
