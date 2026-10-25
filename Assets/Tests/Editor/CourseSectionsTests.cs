using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>맵에 구운 구간 표식을 HUD가 쓰는 값으로 옮긴다. 씬에서 찾는 순서는 정해져 있지 않으므로 x 순으로 줄 세운다.</summary>
    public class CourseSectionsTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { if (go != null) { UnityEngine.Object.DestroyImmediate(go); } }
            spawned.Clear();
        }

        private FlappyCourseSection Marker(string title, int act, bool final, float x)
        {
            var go = new GameObject("CourseSection_" + title);
            spawned.Add(go);
            go.transform.position = new Vector3(x, 0f, 0f);
            var marker = go.AddComponent<FlappyCourseSection>();
            marker.Title = title;
            marker.Act = act;
            marker.Final = final;
            return marker;
        }

        [Test]
        public void 찾은_순서가_아니라_x_순으로_줄_세운다()
        {
            //  FindObjectsByType(None)은 순서를 보장하지 않는다(10-12 스폰 순서 사고와 같은 함정).
            var list = CourseSections.From(new[]
            {
                Marker("승강기", 3, false, 365.25f),
                Marker("수직 갱 낙하", 1, false, 24.8f),
                Marker("진자 광차", 2, false, 176.95f),
            });

            CollectionAssert.AreEqual(new[] { "수직 갱 낙하", "진자 광차", "승강기" },
                                      new[] { list[0].Title, list[1].Title, list[2].Title });
        }

        [Test]
        public void 표식_값을_그대로_옮긴다()
        {
            var list = CourseSections.From(new[] { Marker("고수 갈림길", 3, true, 429.1f) });

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("고수 갈림길", list[0].Title);
            Assert.AreEqual(3, list[0].Act);
            Assert.IsTrue(list[0].Final);
            Assert.AreEqual(429.1f, list[0].X, 1e-3f);
        }

        [Test]
        public void 표식이_없으면_빈_목록()
        {
            //  전통 맵 — 배너·띠·LAST STRETCH가 안 뜰 뿐 아무것도 안 깨진다(spec §0).
            Assert.AreEqual(0, CourseSections.From(new FlappyCourseSection[0]).Count);
        }

        [Test]
        public void 씬에서_찾는다()
        {
            //  열린 씬에 다른 표식(구운 맵)이 있을 수 있어 개수가 아니라 "내가 놓은 것이 들었나"를 본다.
            Marker("시험 구간 가", 1, false, 1000f);
            Marker("시험 구간 나", 2, false, 1010f);

            var titles = new List<string>();
            foreach (var s in CourseSections.FindInScene()) { titles.Add(s.Title); }

            CollectionAssert.Contains(titles, "시험 구간 가");
            CollectionAssert.Contains(titles, "시험 구간 나");
        }
    }
}
