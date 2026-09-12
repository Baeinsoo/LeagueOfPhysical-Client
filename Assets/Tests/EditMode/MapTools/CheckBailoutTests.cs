using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 검사가 막혔을 때 <b>대화상자냐 로그냐</b>를 고르는 분기를 못박는다.
    /// 대화상자 자체는 EditMode에서 못 부르므로 분기 판단만 순수 함수로 빼서 시험한다 —
    /// 그래서 이 테스트가 지키는 것은 "사람 있으면 대화상자, 없으면 같은 문구를 에러로"뿐이다.
    ///
    /// <para>왜 이게 지킬 값이 있나: 사람 없는 잡에서 대화상자를 띄우면 에디터 메인 스레드가
    /// 눌러 줄 사람을 기다리며 <b>영원히</b> 멎는다. 밖에서 보면 "오래 도는 검사"와 구별이
    /// 안 돼서, 이 함정 하나로 하루에 두 번 막혔다.</para>
    /// </summary>
    public class CheckBailoutTests
    {
        const string Title = "Flappy 맵 검사";
        const string Message = "맵에 SpawnPoint 마커가 없다 — 게임과 같은 마커를 읽는다.";

        readonly List<string> dialogs = new List<string>();
        readonly List<string> logs = new List<string>();

        [SetUp]
        public void Reset()
        {
            dialogs.Clear();
            logs.Clear();
        }

        void Report(bool humanWatching)
            => CheckBailout.Report(humanWatching, Title, Message,
                                   (title, body) => dialogs.Add($"{title}|{body}"),
                                   body => logs.Add(body));

        [Test]
        public void 사람이_보고_있으면_대화상자를_띄운다()
        {
            Report(humanWatching: true);
            Assert.AreEqual(1, dialogs.Count, "메뉴로 누른 사람에게는 지금까지처럼 대화상자가 떠야 한다.");
            Assert.AreEqual($"{Title}|{Message}", dialogs[0]);
            Assert.AreEqual(0, logs.Count, "사람이 있으면 로그로 대신하지 않는다.");
        }

        [Test]
        public void 사람이_없으면_같은_문구를_로그로_남기고_끝낸다()
        {
            Report(humanWatching: false);
            Assert.AreEqual(0, dialogs.Count,
                            "사람 없는 잡에서 대화상자를 띄우면 에디터가 통째로 멎는다.");
            Assert.AreEqual(1, logs.Count, "조용히 사라지면 잡이 왜 실패했는지 알 수 없다.");
            Assert.AreEqual(Message, logs[0], "문구는 사람이 볼 때와 <같아야> 한다.");
        }

        [Test]
        public void 알릴_길이_없으면_조용히_넘어가지_않는다()
        {
            //  둘 중 하나라도 안 넘기면 "알렸다"고 할 수 없다 — 조용한 성공이 가장 나쁜 결과다.
            Assert.Throws<ArgumentNullException>(
                () => CheckBailout.Report(true, Title, Message, null, body => logs.Add(body)));
            Assert.Throws<ArgumentNullException>(
                () => CheckBailout.Report(false, Title, Message, (t, b) => dialogs.Add(b), null));
        }
    }
}
