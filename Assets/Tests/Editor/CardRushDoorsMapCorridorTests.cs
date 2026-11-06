using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Luban;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 맵 20(장애물 복도) 저장된 씬이 코스를 덮는지(최종 리뷰 I1). 코스 길이는 마스터데이터가 정하고 바닥·벽은 씬이 정해서,
    /// 한쪽만 바뀌면(옛 씬 + 새 표, 새 표 + 옛 콘텐츠) 결승 앞에서 바닥이 끝나 허공을 달린다. 판정은 맞아 다른 시험은 다 초록이다.
    /// 씬을 열지 않고 저장된 YAML을 읽는다(열면 저장 대화상자가 에디터를 막을 수 있다).
    /// </summary>
    public class CardRushDoorsMapCorridorTests
    {
        private const string ScenePath = "Assets/Art/Scenes/CardRushDoorsMap.unity";
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        /// <summary>결승 너머로 바닥·벽이 이만큼은 더 있어야 한다 — 결승 문·현수막과 완주 뒤 카메라가 보는 앞쪽.</summary>
        public const float PastFinishMeters = 20f;

        private struct Xf
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public long Father;
        }

        private static Dictionary<long, string> Docs(string text)
        {
            var docs = new Dictionary<long, string>();
            foreach (System.Text.RegularExpressions.Match m in Regex.Matches(text, @"--- !u!\d+ &(\d+)[^\n]*\n(.*?)(?=\n--- !u!|\z)", RegexOptions.Singleline))
            {
                docs[long.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
            }
            return docs;
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static Vector3 V3(string doc, string key)
        {
            var m = Regex.Match(doc, key + @": \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}");
            Assert.IsTrue(m.Success, key);
            return new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value));
        }

        private static Xf ReadXf(Dictionary<long, string> docs, long id)
        {
            var doc = docs[id];
            var q = Regex.Match(doc, @"m_LocalRotation: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+), w: ([-\d.eE]+)\}");
            Assert.IsTrue(q.Success);
            return new Xf
            {
                Position = V3(doc, "m_LocalPosition"),
                Rotation = new Quaternion(F(q.Groups[1].Value), F(q.Groups[2].Value), F(q.Groups[3].Value), F(q.Groups[4].Value)),
                Scale = V3(doc, "m_LocalScale"),
                Father = long.Parse(Regex.Match(doc, @"m_Father: \{fileID: (-?\d+)\}").Groups[1].Value),
            };
        }

        //  이름으로 찾은 물체의 월드 자리·크기. 부모 사슬에 회전이 있으면 이 시험은 그 경우를 다루지 않는다고 크게 알린다.
        private static (Vector3 position, Vector3 scale, Quaternion rotation) World(Dictionary<long, string> docs, string name)
        {
            long go = -1;
            foreach (var pair in docs)
            {
                if (Regex.IsMatch(pair.Value, @"^GameObject:", RegexOptions.Multiline) && Regex.IsMatch(pair.Value, @"\n  m_Name: " + Regex.Escape(name) + @"\n"))
                {
                    go = pair.Key;
                    break;
                }
            }
            Assert.AreNotEqual(-1, go, $"씬에 {name}이 없다");
            var xfId = long.Parse(Regex.Match(docs[go], @"- component: \{fileID: (\d+)\}").Groups[1].Value);
            var xf = ReadXf(docs, xfId);
            var position = xf.Position;
            var scale = xf.Scale;
            var rotation = xf.Rotation;
            for (long father = xf.Father; father != 0;)
            {
                var p = ReadXf(docs, father);
                Assert.Less(Quaternion.Angle(Quaternion.identity, p.Rotation), 0.01f, $"{name}의 부모가 돌아 있다 — 이 시험을 고칠 것");
                position = p.Position + Vector3.Scale(p.Scale, position);
                scale = Vector3.Scale(p.Scale, scale);
                father = p.Father;
            }
            return (position, scale, rotation);
        }

        private static int TrackMm()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables)).SetValue(md, tables);
            return new CardRushConfigProvider(md).Get().Tuning.TrackMm;
        }

        [TestCase("HallFloor")]
        [TestCase("WallL")]
        [TestCase("WallR")]
        public void 복도가_출발선부터_결승_너머까지_덮는다(string part)
        {
            var docs = Docs(File.ReadAllText(ScenePath));
            var marker = World(docs, "CardRushRules");
            Assert.Less(Quaternion.Angle(Quaternion.identity, marker.rotation), 0.01f, "마커가 +z를 봐야 이 시험의 축 계산이 맞다");
            var (position, scale, rotation) = World(docs, part);
            Assert.Less(Quaternion.Angle(Quaternion.identity, rotation), 0.01f, part + "가 돌아 있다");

            float start = position.z - scale.z * 0.5f - marker.position.z;
            float end = position.z + scale.z * 0.5f - marker.position.z;
            float finish = TrackMm() / 1000f;
            Assert.LessOrEqual(start, 0f, $"{part}가 출발선 뒤에서 시작해야 한다");
            Assert.GreaterOrEqual(end, finish + PastFinishMeters,
                $"{part}가 출발선에서 {end:F1} m에서 끝나는데 코스는 {finish:F1} m(+{PastFinishMeters} m) — 씬과 마스터데이터 #CardRushConfig가 갈렸다");
        }

        [Test]
        public void 바닥이_여덟_레인을_덮는다()
        {
            var docs = Docs(File.ReadAllText(ScenePath));
            var floor = World(docs, "HallFloor");
            var spacing = F(Regex.Match(File.ReadAllText(ScenePath), @"\n  laneSpacing: ([-\d.eE]+)").Groups[1].Value);
            Assert.GreaterOrEqual(floor.scale.x * 0.5f, spacing * CardRushLanes.MaxLanes * 0.5f);
        }
    }
}
