using System.IO;
using System.Linq;
using Luban;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 일반 큐에서 고를 수 있는 종목 목록 — 큐 표(TbQueue Casual의 허용 목록)가 정한다.
    /// 실제 배포 .bytes를 읽는다(LoadAsync는 EditMode에서 못 기다린다).
    /// </summary>
    public class PlayableGameProviderTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        [Test]
        public void 일반_큐가_허용한_종목만_보인다()
        {
            var md = RealMasterData();
            var casual = md.Tables.TbQueue.Get(QueueChoice.QueueId(QueueKind.Casual, md.Tables.TbQueue));

            var games = new PlayableGameProvider(md).Games;

            Assert.IsNotEmpty(games);
            foreach (var game in games)
            {
                CollectionAssert.Contains(casual.AllowedGameModeIds, game.GameModeId, $"{game.Name}은 일반 큐 허용 목록에 없다");
            }
        }

        [Test]
        public void 플랩왕은_목록에_없다()
        {
            //  더미로 만든 종목이라 일반 큐 허용 목록에서 뺐다(10-17 사용자). 씬·맵은 남아 있다.
            var games = new PlayableGameProvider(RealMasterData()).Games;

            Assert.IsFalse(games.Any(g => g.Name == "플랩왕"));
        }
    }
}
