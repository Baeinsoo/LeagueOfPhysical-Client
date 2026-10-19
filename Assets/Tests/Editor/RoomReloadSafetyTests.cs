using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>경기 씬을 다시 띄울 때 — 옛 씬의 늦은 정리가 새 씬의 방 정보를 지우지 않고, 접속 대기는 시간 안에 끝난다.</summary>
    public class RoomReloadSafetyTests
    {
        private static IEnumerator Run(Task task)
        {
            for (int frame = 0; frame < 600 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted, "끝나지 않는다(멈춤)");
        }

        [Test]
        public void 내가_본_방이_그대로면_지운다()
        {
            var room = new Room();
            Assert.IsTrue(RoomStartup.ShouldClearRoomStore(room, room));
        }

        [Test]
        public void 다음_씬이_방을_새로_채웠으면_안_지운다()
        {
            Assert.IsFalse(RoomStartup.ShouldClearRoomStore(new Room(), new Room()));
        }

        [Test]
        public void 이미_비었으면_지울_것_없다()
        {
            Assert.IsFalse(RoomStartup.ShouldClearRoomStore(new Room(), null));
        }

        [UnityTest]
        public IEnumerator 시간_안에_되면_그냥_끝난다()
        {
            var task = RoomStartup.WaitOrThrowAsync(() => true, 0.2, "접속", CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator 시간을_넘기면_예외로_멈춘다()
        {
            var task = RoomStartup.WaitOrThrowAsync(() => false, 0.2, "접속", CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<TimeoutException>(task.Exception.InnerException);
            StringAssert.Contains("접속", task.Exception.InnerException.Message);
        }
    }
}
