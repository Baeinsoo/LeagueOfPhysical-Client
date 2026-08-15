# 매치 종료 시 유저 위치 정리 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치가 끝난 뒤 로비로 돌아간 플레이어가 방금 끝난 게임으로 다시 끌려가지 않게 한다.

**Architecture:** 유저 위치는 룸 상태의 파생이고, 로비의 조회 경로가 이미 자가치유(`healIfStale`)를 한다. 그래서 모든 변경은 **"룸이 끝났다는 사실을 빨리·반드시 DB에 박는다"** 로 수렴한다 — 백엔드는 상태 저장을 요청 경로 맨 앞으로 옮기고 느린 k8s 파드 삭제를 빼며, 게임서버는 그 확정을 기다린 뒤에 클라에 매치 종료를 알리고, 주기 스윕은 크래시로 하트비트가 끊긴 룸을 `Error`로 박는다.

**Tech Stack:** TypeScript / Node / Express / Prisma (lop-backend, jest + ts-jest) · Unity C# / UniTask / Mirror / VContainer (LeagueOfPhysical-Server, LeagueOfPhysical-Client)

**Spec:** `docs/superpowers/specs/2026-08-15-match-end-location-release-design.md` (클라 레포)

## Global Constraints

- **레포 3개 · 브랜치는 각 레포마다 따로 만든다.** 어떤 레포에서도 `main`에 직접 커밋하지 않는다.
  - `lop-backend` → 브랜치 `feature/match-end-location-release` (Task 1, 2)
  - `LeagueOfPhysical-Server` → 브랜치 `feature/match-end-location-release` (Task 3)
  - `LeagueOfPhysical-Client` → 이미 이 워크트리(`worktree-feature+match-end-location-release`) (Task 4, 5)
- **Unity 두 레포는 워크트리를 쓰지 않는다.** 연결된 Unity 에디터는 각 레포의 **main 체크아웃 디렉터리**를 보므로, 워크트리에 짠 `Assets/` 코드는 컴파일 검증이 안 된다. 그 레포의 원래 체크아웃에서 `git switch -c`로 피처 브랜치를 만들어 작업한다. (클라 Task 4는 이미 만들어진 이 워크트리에서 하고, 컴파일 검증은 Task 5에서 머지 후 에디터로 한다.)
- **`.meta` 파일**: 이 계획은 새 Unity 에셋/스크립트를 만들지 않는다. 파일 추가가 생기면 Unity가 만든 `.meta`를 함께 커밋한다.
- **백엔드 테스트는 빌드가 타입검사하지 않는다** — `apps/*/tsconfig.json`이 `__tests__`를 exclude한다. `pnpm build` 통과를 테스트 통과로 읽지 말 것. 반드시 `pnpm --filter room-server test`를 돌린다.
- **주석은 "왜"만, 일상어로** (`CLAUDE.md`). 코드로 자명한 것은 주석 없이 둔다.
- 답변·커밋 메시지·문서는 한국어.

---

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `lop-backend/apps/room-server/src/services/room.service.ts` (수정) | 룸 상태 전이의 진실원본. 종료 전이 시 ① 상태 저장 ② 플레이어 해제. 파드 수명은 더 이상 여기 없음 | 1, 2 |
| `lop-backend/apps/room-server/src/services/__tests__/room.service.test.ts` (신규) | 위 두 동작의 순서·1회성·실패 격리를 고정 | 1, 2 |
| `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs` (수정) | 매치 종료 통보 순서: 백엔드 확정 → 클라 통보 | 3 |
| `LeagueOfPhysical-Client/Assets/Scripts/Room/RoomConnector.cs` (수정) | 닫힌 방은 확정 거절로 보고 즉시 포기 | 4 |
| `LeagueOfPhysical-Client/docs/ROADMAP.md` (수정) | 트랙 상태 갱신 + stale 서술 정정 | 5 |

---

### Task 1: 백엔드 — 종료 사실을 먼저 박고, 파드 삭제를 요청 경로에서 뺀다

**Files:**
- Modify: `lop-backend/apps/room-server/src/services/room.service.ts:173-214` (`updateRoomStatus`)
- Test: `lop-backend/apps/room-server/src/services/__tests__/room.service.test.ts` (신규)

**Interfaces:**
- Consumes: 기존 `RoomRepository.findById/save`, `MatchService.findMatchById`, `UserLocationService.updateUserLocation`
- Produces: `private isTerminal(status: RoomStatus): boolean` — Task 2가 그대로 재사용한다. `private async releasePlayers(matchId: string): Promise<void>` — Task 2는 부르지 않는다(의도).

**작업 시작 전:** `lop-backend`에서 브랜치를 만든다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git switch -c feature/match-end-location-release
```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

새 파일 `apps/room-server/src/services/__tests__/room.service.test.ts`:

```ts
import { RoomStatus } from '@interfaces/room.interface';
import { Location, ResponseCode } from '@lop/server-core';

const findById = jest.fn();
const save = jest.fn();
const findAll = jest.fn();
const findMatchById = jest.fn();
const updateUserLocation = jest.fn();
const deletePod = jest.fn();
const deleteService = jest.fn();
const listPods = jest.fn();
const listServices = jest.fn();

jest.mock('@repositories/room.repository', () => ({
    RoomRepository: jest.fn(() => ({ findById, save, findAll })),
}));
jest.mock('@services/match.service', () => ({ __esModule: true, default: jest.fn(() => ({ findMatchById })) }));
jest.mock('@services/user-location.service', () => ({ __esModule: true, default: jest.fn(() => ({ updateUserLocation })) }));
//  k8sUtils는 import 되는 순간 kube config를 읽는다(static 초기화). 갈아 끼우지 않으면
//  테스트 실행 환경에 클러스터 설정이 없어 파일을 불러오는 것만으로 죽는다.
jest.mock('@utils/k8sUtils', () => ({
    k8sUtils: { deletePod, deleteService, listPods, listServices },
}));

import RoomService from '@services/room.service';

const roomOf = (over: Record<string, unknown> = {}) => ({
    id: 'R1',
    matchId: 'M1',
    status: RoomStatus.GameInProgress,
    ip: '127.0.0.1',
    port: 7000,
    lastHeartbeat: new Date(),
    ...over,
});

describe('RoomService.updateRoomStatus', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        save.mockImplementation(async (v: any) => v);
        findMatchById.mockResolvedValue({ match: { playerList: ['U1', 'U2'] } });
        updateUserLocation.mockResolvedValue({ code: ResponseCode.SUCCESS });
        listPods.mockResolvedValue({ items: [] });
        listServices.mockResolvedValue({ items: [] });
    });

    //  이 순서가 이 트랙의 전부다. 상태가 먼저 박혀야 로비 조회의 자가치유가 작동한다.
    it('룸 상태를 위치 정리보다 먼저 저장한다', async () => {
        const order: string[] = [];
        findById.mockResolvedValue(roomOf());
        save.mockImplementation(async (v: any) => { order.push('save'); return v; });
        updateUserLocation.mockImplementation(async () => { order.push('release'); return { code: ResponseCode.SUCCESS }; });

        await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(order).toEqual(['save', 'release']);
        expect(save.mock.calls[0][0]).toMatchObject({ status: RoomStatus.Closed });
    });

    it('매치의 플레이어 전원 위치를 None으로 비운다', async () => {
        findById.mockResolvedValue(roomOf());

        await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(updateUserLocation).toHaveBeenCalledTimes(1);
        const sent = updateUserLocation.mock.calls[0][0];
        expect(sent.userLocations).toHaveLength(2);
        expect(sent.userLocations[0]).toMatchObject({ userId: 'U1', location: Location.None });
        expect(sent.userLocations[1]).toMatchObject({ userId: 'U2', location: Location.None });
    });

    //  두 번 걸리면 그 사이 새 매칭에 들어간 사람의 위치를 None으로 덮어 로비로 튕긴다.
    it('이미 종료된 룸에는 다시 부작용을 내지 않는다', async () => {
        findById.mockResolvedValue(roomOf({ status: RoomStatus.Closed }));

        const res = await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(res.code).toBe(ResponseCode.SUCCESS);
        expect(save).not.toHaveBeenCalled();
        expect(updateUserLocation).not.toHaveBeenCalled();
    });

    //  위치 정리는 빠른 길일 뿐이고 안전망은 상태 저장이다 — 빠른 길이 죽어도 안전망은 남아야 한다.
    it('위치 정리가 실패해도 상태 저장은 남고 요청은 성공한다', async () => {
        findById.mockResolvedValue(roomOf());
        updateUserLocation.mockRejectedValue(new Error('lobby down'));

        const res = await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(res.code).toBe(ResponseCode.SUCCESS);
        expect(save.mock.calls[0][0]).toMatchObject({ status: RoomStatus.Closed });
    });

    //  느린 k8s 호출이 요청 경로에 있으면 상태 저장이 그만큼 늦어지고, 그게 이 버그의 원인이었다.
    it('요청 경로에서 파드를 지우지 않는다', async () => {
        findById.mockResolvedValue(roomOf());

        await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(deletePod).not.toHaveBeenCalled();
        expect(deleteService).not.toHaveBeenCalled();
    });

    it('종료가 아닌 전이는 위치를 건드리지 않는다', async () => {
        findById.mockResolvedValue(roomOf({ status: RoomStatus.Initializing }));

        await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.WaitingForPlayers } as any);

        expect(updateUserLocation).not.toHaveBeenCalled();
        expect(save.mock.calls[0][0]).toMatchObject({ status: RoomStatus.WaitingForPlayers });
    });

    it('없는 룸이면 ROOM_NOT_EXIST', async () => {
        findById.mockResolvedValue(null);

        const res = await new RoomService().updateRoomStatus({ roomId: 'R1', status: RoomStatus.Closed } as any);

        expect(res.code).toBe(ResponseCode.ROOM_NOT_EXIST);
        expect(save).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- room.service
```
Expected: FAIL. `룸 상태를 위치 정리보다 먼저 저장한다`가 `['release', 'save']`로 나오고(현 코드가 위치를 먼저 정리), `요청 경로에서 파드를 지우지 않는다`가 `deletePod`이 불렸다고 실패한다.

- [ ] **Step 3: 구현한다**

`apps/room-server/src/services/room.service.ts`의 `updateRoomStatus`(현재 173–214행)를 아래로 교체한다.

```ts
    public async updateRoomStatus(updateRoomStatusDto: UpdateRoomStatusDto): Promise<UpdateRoomStatusResponseDto> {
        try {
            let room = await this.roomRepository.findById(updateRoomStatusDto.roomId);
            if (!room) {
                return {
                    code: ResponseCode.ROOM_NOT_EXIST
                };
            }

            //  끝난 방에 종료를 또 걸면 플레이어 위치를 한 번 더 비운다 — 그 사이 새 매칭에 들어간
            //  사람이 로비로 튕긴다. 종료의 부작용은 "처음 끝나는 순간"에만 낸다.
            if (this.isTerminal(room.status)) {
                return {
                    code: ResponseCode.SUCCESS,
                    room: RoomMapper.toRoomResponseDto(room),
                };
            }

            room.status = updateRoomStatusDto.status;

            //  "끝났다"를 먼저 박는다. 유저 위치는 룸 상태의 파생이라, 로비가 위치를 조회할 때
            //  닫힌 방을 보면 스스로 위치를 비운다 — 그 안전망 전체가 이 저장 한 번에 걸려 있다.
            room = await this.roomRepository.save(room);

            if (this.isTerminal(room.status)) {
                await this.releasePlayers(room.matchId);
            }

            return {
                code: ResponseCode.SUCCESS,
                room: RoomMapper.toRoomResponseDto(room),
            };
        } catch (error) {
            return Promise.reject(error);
        }
    }

    private isTerminal(status: RoomStatus): boolean {
        return status === RoomStatus.Closed || status === RoomStatus.Error;
    }

    //  빠른 길이다 — 위 상태 저장이 안전망이므로 여기서 실패해도 던지지 않는다.
    //  다만 조용히 넘어가면 원인을 못 찾으니 반드시 남긴다.
    private async releasePlayers(matchId: string): Promise<void> {
        try {
            const getMatchResponseDto = await this.matchService.findMatchById(matchId);
            if (!getMatchResponseDto.match) {
                logger.warn(`Match ${matchId} not found. Releasing players is skipped.`);
                return;
            }

            const updateUserLocationDto = new UpdateUserLocationDto();
            getMatchResponseDto.match.playerList.forEach(playerId => {
                const userLocationDto: UserLocationDto = {
                    userId: playerId,
                    location: Location.None,
                    locationDetail: new LocationDetail(Location.None),
                };
                updateUserLocationDto.userLocations.push(userLocationDto);
            });

            await this.userLocationService.updateUserLocation(updateUserLocationDto);
        } catch (error) {
            logger.error(`Failed to release players of match ${matchId}. error: ${error}`);
        }
    }
```

같은 파일 맨 위 import 블록에 `logger`를 추가한다(파일에 아직 없다):

```ts
import { logger } from '@lop/server-core/logger';
```

- [ ] **Step 4: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- room.service
```
Expected: PASS (7건).

- [ ] **Step 5: 기존 테스트와 빌드가 안 깨졌는지 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test && pnpm --filter room-server build
```
Expected: 전부 PASS. `deleteRoomRunnerById`는 `deleteRoomById`가 아직 쓰므로 **미사용 경고가 나오면 안 된다**(남겨두는 게 맞다).

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/room-server/src/services/room.service.ts apps/room-server/src/services/__tests__/room.service.test.ts
git commit -m "fix(room-server): 종료 상태를 먼저 저장하고 파드 삭제를 요청 경로에서 뺀다

매치 종료 후 로비에서 같은 방으로 재접속하던 원인. room.save(Closed)가
느린 k8s 파드 삭제 뒤에 있어, 그 사이 조회하면 방이 아직 GameInProgress로
보여 로비의 자가치유가 돌지 않았다.

- 상태 저장을 맨 앞으로, 위치 정리는 그 뒤(실패해도 요청은 성공)
- 이미 종료된 룸이면 부작용 없이 반환(위치 덮어쓰기 방지)
- 파드 삭제는 2초 주기 스윕에 맡긴다"
```

---

### Task 2: 백엔드 — 스윕이 크래시한 룸의 종료 사실을 박는다

**Files:**
- Modify: `lop-backend/apps/room-server/src/services/room.service.ts:235-279` (`checkAndCleanupRoomRunners`, `shouldTerminateRoomRunner`)
- Test: `lop-backend/apps/room-server/src/services/__tests__/room.service.test.ts` (Task 1이 만든 파일에 describe 추가)

**Interfaces:**
- Consumes: Task 1의 `private isTerminal(status: RoomStatus): boolean`
- Produces: `private markExpiredRoomsAsError(rooms: Iterable<Room>): Promise<void>`, `private deleteRunnersOfTerminatedRooms(rooms: Iterable<Room>): Promise<void>`, `private isHeartbeatExpired(room: Room): boolean`. `shouldTerminateRoomRunner`는 **`async`를 떼고 동기 `boolean`** 이 된다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`room.service.test.ts` 끝에 아래 describe를 덧붙인다(파일 상단의 mock·`roomOf`는 Task 1 것을 그대로 쓴다).

```ts
describe('RoomService.checkAndCleanupRoomRunners', () => {
    const EXPIRED = () => new Date(Date.now() - 120000);   //  하트비트 임계값 60초를 넘긴 시각

    beforeEach(() => {
        jest.clearAllMocks();
        save.mockImplementation(async (v: any) => v);
        listPods.mockResolvedValue({ items: [] });
        listServices.mockResolvedValue({ items: [] });
    });

    //  게임서버가 Closed를 못 보내고 죽으면, 이 전이가 그 방 사람들을 푸는 유일한 신호다.
    it('하트비트가 끊긴 룸을 Error로 박는다', async () => {
        findAll.mockResolvedValue([roomOf({ lastHeartbeat: EXPIRED() })]);

        await new RoomService().checkAndCleanupRoomRunners();

        expect(save).toHaveBeenCalledTimes(1);
        expect(save.mock.calls[0][0]).toMatchObject({ id: 'R1', status: RoomStatus.Error });
    });

    //  스윕은 2초마다 돈다. 이미 끝난 룸에 매번 쓰면 DB를 계속 두드린다.
    it('이미 종료된 룸은 다시 저장하지 않는다', async () => {
        findAll.mockResolvedValue([roomOf({ status: RoomStatus.Closed, lastHeartbeat: EXPIRED() })]);

        await new RoomService().checkAndCleanupRoomRunners();

        expect(save).not.toHaveBeenCalled();
    });

    it('살아 있는 룸은 건드리지 않는다', async () => {
        findAll.mockResolvedValue([roomOf()]);

        await new RoomService().checkAndCleanupRoomRunners();

        expect(save).not.toHaveBeenCalled();
        expect(deletePod).not.toHaveBeenCalled();
    });

    //  기존 스윕은 파드 목록만 돌아서, 파드가 이미 사라진 크래시 룸은 아예 보이지 않았다.
    it('파드가 이미 사라진 크래시 룸도 잡는다', async () => {
        findAll.mockResolvedValue([roomOf({ lastHeartbeat: EXPIRED() })]);
        listPods.mockResolvedValue({ items: [] });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(save.mock.calls[0][0]).toMatchObject({ status: RoomStatus.Error });
    });

    //  파드 삭제까지 룸 목록으로 돌면, DB에 쌓인 과거 종료 룸 전부에 대해 2초마다
    //  삭제를 호출하게 된다(룸 행은 지워지지 않는다).
    it('파드 삭제는 실재하는 파드에 대해서만 부른다', async () => {
        findAll.mockResolvedValue([
            roomOf({ id: 'R1', status: RoomStatus.Closed }),
            roomOf({ id: 'R2', status: RoomStatus.Closed }),
        ]);
        listPods.mockResolvedValue({
            items: [{ metadata: { name: 'room-pod-R1', namespace: 'default', labels: { roomId: 'R1' } } }],
        });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(deletePod).toHaveBeenCalledTimes(1);
        expect(deletePod).toHaveBeenCalledWith('room-pod-R1', 'default');
    });

    it('종료 서비스도 실재하는 것만 지운다', async () => {
        findAll.mockResolvedValue([roomOf({ id: 'R1', status: RoomStatus.Closed })]);
        listServices.mockResolvedValue({
            items: [{ metadata: { name: 'room-service-R1', namespace: 'default', labels: { roomId: 'R1' } } }],
        });

        await new RoomService().checkAndCleanupRoomRunners();

        expect(deleteService).toHaveBeenCalledWith('room-service-R1', 'default');
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test -- room.service
```
Expected: 새 describe의 `하트비트가 끊긴 룸을 Error로 박는다` · `파드가 이미 사라진 크래시 룸도 잡는다`가 FAIL(현 스윕은 상태를 저장하지 않는다). 나머지는 우연히 통과할 수 있다.

- [ ] **Step 3: 구현한다**

`room.service.ts`의 `checkAndCleanupRoomRunners`(235–273행)와 `shouldTerminateRoomRunner`(275–279행)를 아래로 교체한다.

```ts
    public async checkAndCleanupRoomRunners(): Promise<void> {
        try {
            const rooms = Array.from(await this.roomRepository.findAll());

            await this.markExpiredRoomsAsError(rooms);
            await this.deleteRunnersOfTerminatedRooms(rooms);
        } catch (error) {
            logger.error(`Error during CleanupRoomRunners: ${error}`);
        }
    }

    //  게임서버가 종료를 알리지 못하고 죽으면 룸은 DB에 살아 있는 채로 남는다. 그러면 로비는
    //  "방이 아직 있네"라고 보고 그 판에 있던 사람들을 영영 풀어주지 않는다. 하트비트가 끊긴
    //  룸을 끝난 것으로 박아 주는 게 그 사람들을 푸는 신호다.
    private async markExpiredRoomsAsError(rooms: Room[]): Promise<void> {
        for (const room of rooms) {
            if (this.isTerminal(room.status) || !this.isHeartbeatExpired(room)) {
                continue;
            }

            room.status = RoomStatus.Error;
            await this.roomRepository.save(room);
            logger.warn(`Room ${room.id} marked as Error: heartbeat expired.`);
        }
    }

    //  룸이 아니라 파드/서비스 목록을 돈다 — 룸 목록으로 돌면 DB에 쌓인 과거 종료 룸 전부에
    //  대해 2초마다 삭제를 호출하게 된다(룸 행은 지워지지 않는다).
    private async deleteRunnersOfTerminatedRooms(rooms: Room[]): Promise<void> {
        const roomsById = new Map(rooms.map(room => [room.id, room]));

        const podList = await k8sUtils.listPods();
        for (const pod of podList?.items ?? []) {
            const roomId = pod.metadata?.labels?.['roomId'];
            const room = roomId ? roomsById.get(roomId) : undefined;

            if (pod.metadata?.name && pod.metadata?.namespace && room && this.shouldTerminateRoomRunner(room)) {
                await k8sUtils.deletePod(pod.metadata.name, pod.metadata.namespace);
            }
        }

        const serviceList = await k8sUtils.listServices();
        for (const service of serviceList?.items ?? []) {
            const roomId = service.metadata?.labels?.['roomId'];
            const room = roomId ? roomsById.get(roomId) : undefined;

            if (service.metadata?.name && service.metadata?.namespace && room && this.shouldTerminateRoomRunner(room)) {
                await k8sUtils.deleteService(service.metadata.name, service.metadata.namespace);
            }
        }
    }

    private isHeartbeatExpired(room: Room): boolean {
        return Date.now() - room.lastHeartbeat.getTime() > RoomService.HEARTBEAT_THRESHOLD;
    }

    private shouldTerminateRoomRunner(room: Room): boolean {
        return this.isHeartbeatExpired(room) || this.isTerminal(room.status);
    }
```

`isRoomJoinable`(60행)의 하트비트 비교도 새 헬퍼로 바꿔 같은 판정을 한 곳에서 쓰게 한다:

```ts
            if (this.isHeartbeatExpired(room)) {
```

- [ ] **Step 4: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter room-server test && pnpm --filter room-server build
```
Expected: room.service 13건 전부 PASS + 기존 테스트 PASS + 빌드 성공.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/room-server/src/services/room.service.ts apps/room-server/src/services/__tests__/room.service.test.ts
git commit -m "fix(room-server): 스윕이 하트비트 끊긴 룸을 Error로 박는다

게임서버가 크래시하면 룸이 DB에 살아 있는 채로 남아, 그 판에 있던
사람들이 로비에서 영영 풀려나지 않았다. 스윕은 파드만 지우고 룸 상태를
바꾸지 않았고, 파드 목록만 돌아 파드가 이미 사라진 룸은 보이지도 않았다.

- 사실 박기: 룸 목록을 돌며 만료된 룸을 Error로 전이 저장(전이 1회)
- 파드 GC: 지금처럼 파드/서비스 목록 기준(과거 종료 룸 전부를 매번
  두드리지 않도록)"
```

---

### Task 3: 게임서버 — 백엔드 확정을 기다린 뒤 매치 종료를 통보한다

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs:182-206` (`OnGameStateChanged`) + 상단 `using`·상수

**Interfaces:**
- Consumes: `WebAPI.UpdateRoomStatus(UpdateRoomStatusRequest, CancellationToken) → UniTask<UpdateRoomStatusResponse>` (기존), Task 1이 고친 백엔드 순서
- Produces: (없음 — 이 레포 내부에서 끝난다)

**작업 시작 전:** 서버 레포의 **원래 체크아웃**에서 브랜치를 만든다(워크트리 금지 — Unity 에디터가 이 디렉터리를 본다).

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git switch -c feature/match-end-location-release
```

> **테스트 없음(의도).** 클라 코드와 마찬가지로 이 레포의 앱 코드는 asmdef 없이 `Assembly-CSharp`에 있어 EditMode 유닛 테스트를 붙일 수 없다(`[[client-test-infra-constraint]]`). 이 변경은 순수 로직이 아니라 **호출 순서**이므로, 검증은 컴파일 + Task 5의 인게임 시나리오로 한다.

- [ ] **Step 1: `using`과 상수를 추가한다**

`LOPRoom.cs` 상단 using 블록에 `System.Threading`을 추가한다(현재 없다. `System.Threading.Tasks`만 있음).

```csharp
using System.Threading;
```

클래스 상수 옆(18–19행)에 타임아웃을 둔다.

```csharp
        private const int HEARTBEAT_INTERVAL = 2;       //  sec
        private const double TICK_INTERVAL = 1 / 50d;   //  sec
        private const int CLOSE_TIMEOUT_SECONDS = 3;
```

- [ ] **Step 2: 통보 순서를 뒤집는다**

`OnGameStateChanged`(182–206행)를 아래로 교체한다.

```csharp
        private void OnGameStateChanged(RunnerState gameState)
        {
            switch (gameState)
            {
                case RunnerState.GameOver:
                    Debug.Log("Game Over");

                    CloseRoomAsync().Forget();
                    break;
            }
        }

        //  순서가 중요하다. 백엔드가 "이 방 끝났다"를 먼저 저장해야, 로비로 돌아간 클라가 자기
        //  위치를 물었을 때 방금 끝난 방으로 다시 끌려가지 않는다. 파드 삭제는 이 호출 안에 없어서
        //  (룸서버가 주기 정리로 지운다) 기다리는 동안 우리가 먼저 죽지 않는다.
        private async UniTaskVoid CloseRoomAsync()
        {
            if (!EnvironmentSettings.active.Standalone)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CLOSE_TIMEOUT_SECONDS));

                    await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomDataStore.room.id,
                        status = RoomStatus.Closed,
                    }, cts.Token);
                }
                catch (Exception e)
                {
                    //  실패해도 통보는 강행한다 — 클라를 끝난 방에 가둬 두는 쪽이 더 나쁘고,
                    //  그 경우는 룸서버의 하트비트 만료 정리가 받아 준다.
                    Debug.LogError($"Failed to close room. Notifying clients anyway. Error: {e.Message}");
                }
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(new MatchEndedToC());
            }
        }
```

> `RunnerBase.gameState` 세터가 같은 값 재대입을 걸러내므로 `GameOver` 전이는 한 번만 발화한다 — `LOPRunner.LateUpdate`가 매 프레임 `EndMatch()`를 불러도 통보가 여러 번 나가지 않는다.

- [ ] **Step 3: 컴파일을 확인한다**

Unity 서버 에디터에서 컴파일 에러 0을 확인한다. UnityMCP를 쓸 경우 **반드시 `unity_instance`를 서버 인스턴스로 명시**한다(`mcpforunity://instances`에서 id 확인). 에디터를 못 붙이면 `Library/Bee`의 응답 파일 + 번들 Roslyn으로 `Assets/Scripts` 범위만 컴파일한다(`[[client-compile-gate-without-editor]]`).

Expected: 에러 0.

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "fix: 매치 종료를 백엔드에 확정시킨 뒤 클라에 통보한다

먼저 통보하고 상태 갱신을 fire-and-forget 하면, 로비로 돌아간 클라가
아직 안 비워진 위치를 읽어 방금 끝난 방으로 다시 들어간다.

- UpdateRoomStatus(Closed)를 3초 타임아웃으로 await 후 MatchEndedToC
- 실패/타임아웃이어도 통보는 강행(끝난 방에 가두지 않는다)
- 파드 삭제가 이 호출에서 빠졌으므로 기다려도 우리가 먼저 죽지 않는다"
```

---

### Task 4: 클라 — 닫힌 방에는 60초를 매달리지 않는다

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Room/RoomConnector.cs:28-52` (`TryToEnterRoomById`)

**Interfaces:**
- Consumes: `RoomJoinableResponse.room` (`RoomDto`, `status: RoomStatus` 보유 — 이미 있다)
- Produces: (없음)

**작업 위치:** 이 워크트리(`.claude/worktrees/feature+match-end-location-release`).

> **테스트 없음(의도).** 클라 코드는 전부 `Assembly-CSharp`이라 asmdef 참조가 불가능해 EditMode 유닛 테스트를 붙일 수 없다(`[[client-test-infra-constraint]]`). 변경은 응답 한 필드에 대한 단일 조건이고, 검증은 컴파일 + Task 5의 크래시 시나리오로 한다.

- [ ] **Step 1: 확정 거절 판정을 넣는다**

`TryToEnterRoomById`의 성공 분기 바로 아래(현재 34–38행의 `if (...SUCCESS) { return true; }` 다음)에 추가한다.

```csharp
                    if (checkRoomJoinable.code == ResponseCode.SUCCESS)
                    {
                        return true;
                    }

                    //  방이 닫혔거나 터진 건 확정된 대답이다 — 60초를 더 물어봐도 답이 바뀌지 않는다.
                    //  아직 부팅 중이라 거절당한 경우와 구분해야 해서, 응답 코드가 아니라 방 상태로 가른다.
                    if (checkRoomJoinable.room != null &&
                        (checkRoomJoinable.room.status == RoomStatus.Closed || checkRoomJoinable.room.status == RoomStatus.Error))
                    {
                        Debug.Log($"Room {roomId} is already closed (status: {checkRoomJoinable.room.status}). Stop retrying.");
                        return false;
                    }
```

> ⚠️ `ResponseCode.ROOM_NOT_JOINABLE` 자체를 확정 거절로 보면 **안 된다.** 파드가 부팅 중일 때 (`RunnerCreated`/`Initializing`)도 같은 코드가 오고, 그 60초 여유는 여전히 필요하다.

- [ ] **Step 2: 컴파일을 확인한다**

이 워크트리의 `Assets/` 코드는 **연결된 Unity 에디터가 보지 않는다**(에디터는 클라 main 체크아웃을 본다 — `[[unity-editor-bound-to-main-checkout]]`). `Library/Bee`의 응답 파일 + 번들 Roslyn으로 `Assets/Scripts` 범위만 컴파일해 확인한다(`[[client-compile-gate-without-editor]]`). 전체 범위로 걸면 Mirror 등 남의 asmdef를 끌어와 통째로 깨지므로 범위를 넓히지 않는다.

Expected: 에러 0. 진짜 게이트는 Task 5의 머지 후 에디터 리프레시다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Room/RoomConnector.cs
git commit -m "fix: 닫힌 방에는 재접속을 재시도하지 않는다

위치가 잠깐 GameRoom으로 남아 InGameRoom에 들어가면, 이미 닫힌 방에
60회 x 1초를 매달려 로비가 그동안 묶였다. 부팅 중 거절과 구분해야 하므로
응답 코드가 아니라 응답에 실린 방 상태로 가른다."
```

---

### Task 5: 통합 검증 + 문서 갱신

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md` (유저 위치 트랙 절, 파킹 표)

**Interfaces:**
- Consumes: Task 1~4 전부

- [ ] **Step 1: 백엔드를 로컬 클러스터에 올린다**

배포는 push다(ArgoCD GitOps — `[[argocd-gitops-cluster-rebuild]]`). 이 트랙은 **마이그레이션이 없으므로** `db-migrate`는 불필요하지만, 앱 이미지 빌드 대상에 `room-server`가 포함돼야 한다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git push -u origin feature/match-end-location-release
```

그다음 `backend-deploy` 워크플로를 room-server 대상으로 수동 실행하고, 롤아웃을 확인한다.

```bash
kubectl get pods -w
```

⚠️ **막힌 롤아웃은 서비스를 안 죽이고 옛 버전이 계속 응답한다.** 동작이 그대로면 코드를 의심하기 전에 파드 이미지 태그부터 본다.

- [ ] **Step 2: 게임서버·클라 브랜치를 main에 머지하고 에디터 컴파일을 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git switch main && git merge --no-ff feature/match-end-location-release
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git switch main && git merge --no-ff worktree-feature+match-end-location-release
```

양쪽 Unity 에디터에서 컴파일 에러 0을 확인한다(UnityMCP 사용 시 `unity_instance` 명시).

- [ ] **Step 3: 인게임 검증 ① — 정상 종료 (이 트랙의 본 목적)**

kind 파드 구성(`local-k8s`)에서:

1. 매칭 → 매치 진입
2. 매치 종료(5분 경과 또는 종료 조건)
3. **결과 창이 뜨고 그대로 유지된다. Room 씬이 다시 로드되지 않는다.**
4. 확인 버튼 → 로비 → 새 매칭 요청이 정상 동작한다

로드맵의 "인게임 검증 ⑥"을 못 돌린 자리다. 실패하면 room-server 로그에서 `updateRoomStatus` 순서를 확인한다 — ⚠️ **컨테이너 로그는 UTC다**(`[[timing-dependent-findings-are-not-ship]]`). 클라 로그와 시각을 비교하기 전에 `kubectl exec -- date -u`로 기준을 맞춘다.

- [ ] **Step 4: 인게임 검증 ② — 크래시 경로**

1. 매치 진입 후 게임서버 파드를 강제 종료한다: `kubectl delete pod room-pod-<roomId> --force`
2. 클라가 룸에서 튕긴 뒤 로비로 복귀
3. **60초(하트비트 임계값) 안에 그 사람이 풀려나 새 매칭을 요청할 수 있다.** 같은 방으로 재접속을 반복하지 않는다.
4. room-server 로그에 `Room <id> marked as Error: heartbeat expired.`가 남는다

- [ ] **Step 5: ROADMAP을 갱신한다**

`docs/ROADMAP.md`에서 세 곳을 고친다.

1. **유저 위치 트랙 절(292행 부근)** — "다음에 할 것" 1번(매치 종료 시 위치 정리)을 완료로 옮기고, 백엔드 표의 "정리 책임이 없다" 행을 해소로 바꾼다. 무엇이 원인이었는지(상태 저장이 느린 파드 삭제 뒤에 있었다) + 자가치유가 이미 있었다는 사실을 한 줄로 남긴다.
2. **stale 정정** — *"`if (!Standalone)` 가드로 로컬에선 스킵"* 서술은 07-30 kind 전환 이전 기준이다. `standalone: 1`은 에디터 기본 환경(`local`)뿐이고 로컬 E2E는 `local-k8s`(standalone=0)라 실제로는 호출된다고 정정한다.
3. **파킹 표** — "매치 종료 시 유저 위치 백엔드 정리 (Slice D 후속)" 행을 해소 표시로 바꾸고 본문 절을 가리킨다.

- [ ] **Step 6: 커밋 · 머지**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md && git commit -m "docs(roadmap): 매치 종료 시 위치 정리 완료 + Standalone 서술 정정"
```

백엔드 브랜치도 main에 머지하고 배포한다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git switch main && git merge --no-ff feature/match-end-location-release && git push
```

- [ ] **Step 7: 메모리 갱신**

이 트랙에서 durable한 것을 남긴다: **"유저 위치는 룸 상태의 파생이고, 로비 조회 경로에 자가치유가 이미 있었다 — 증상이 '누가 안 지운다'로 보여도 먼저 '사실이 언제 박히나'를 볼 것."** 기존 `[[flow-slice-d-match-result]]`의 미해결 후속 항목도 함께 갱신한다.

---

## 자체 리뷰 결과

- **spec 커버리지**: 변경 1 → Task 1 / 변경 2 → Task 3 / 변경 3 → Task 2 / 변경 4 → Task 4 / 검증 절 → Task 1·2의 테스트 + Task 5의 인게임 2건 / 범위 밖 항목은 손대지 않음. 빠진 요구사항 없음.
- **타입 일관성**: `isTerminal`(Task 1 정의 → Task 2 소비), `isHeartbeatExpired`(Task 2 정의 → `isRoomJoinable`·`shouldTerminateRoomRunner` 소비), `shouldTerminateRoomRunner`가 `async`에서 동기로 바뀌는 것을 Task 2에 명시. 클라 `RoomDto.status` 실재 확인함.
- **알려진 함정 반영**: 백엔드 테스트가 빌드 타입검사 밖 / `k8sUtils` static 초기화 mock 필요 / Unity 워크트리-에디터 괴리 / 컨테이너 로그 UTC / 막힌 롤아웃은 옛 버전이 응답.
