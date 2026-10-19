using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>재접속 시도 1회의 판정 — 로비가 알려 준 위치와 방 상태로 "다시 들어간다 / 판 끝남 / 아직"을 가른다.</summary>
    public class RejoinProbeTests
    {
        private static UserLocationDto InRoom(string roomId) => new UserLocationDto
        {
            location = Location.GameRoom,
            locationDetail = new GameRoomLocationDetail { gameRoomId = roomId },
        };

        private static RoomJoinableResponse NotJoinable(RoomStatus status) => new RoomJoinableResponse
        {
            code = ResponseCode.ROOM_NOT_JOINABLE,
            room = new RoomDto { status = status },
        };

        [Test]
        public void 같은_방에_있으면_방_상태를_보러_간다()
        {
            Assert.AreEqual(RejoinAnswer.NotReady, RejoinProbe.FromLocation(InRoom("r1"), "r1", out var room));
            Assert.AreEqual("r1", room);
        }

        [Test]
        public void 방에_없으면_판이_끝난_것이다()
        {
            var none = new UserLocationDto { location = Location.None };
            Assert.AreEqual(RejoinAnswer.MatchOver, RejoinProbe.FromLocation(none, "r1", out var room));
            Assert.IsNull(room);
        }

        [Test]
        public void 다른_방에_있으면_판이_끝난_것으로_보고_로비에_맡긴다()
        {
            Assert.AreEqual(RejoinAnswer.MatchOver, RejoinProbe.FromLocation(InRoom("r2"), "r1", out _));
        }

        [Test]
        public void 내_방을_모르면_로비가_말한_방을_본다()
        {
            RejoinProbe.FromLocation(InRoom("r2"), null, out var room);
            Assert.AreEqual("r2", room);
        }

        [Test]
        public void 방_응답으로_들어갈지_가른다()
        {
            Assert.AreEqual(RejoinAnswer.Joinable, RejoinProbe.FromRoom(new RoomJoinableResponse { code = ResponseCode.SUCCESS }));
            Assert.AreEqual(RejoinAnswer.MatchOver, RejoinProbe.FromRoom(NotJoinable(RoomStatus.Closed)));
            Assert.AreEqual(RejoinAnswer.MatchOver, RejoinProbe.FromRoom(NotJoinable(RoomStatus.Error)));
            Assert.AreEqual(RejoinAnswer.NotReady, RejoinProbe.FromRoom(NotJoinable(RoomStatus.Initializing)));
            Assert.AreEqual(RejoinAnswer.NotReady, RejoinProbe.FromRoom(null));
        }

        [Test]
        public void 방이_아예_없으면_판이_끝난_것이다()
        {
            Assert.AreEqual(RejoinAnswer.MatchOver, RejoinProbe.FromRoom(new RoomJoinableResponse { code = ResponseCode.ROOM_NOT_EXIST }));
        }
    }
}
