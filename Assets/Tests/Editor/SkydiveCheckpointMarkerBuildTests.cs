using System.IO;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveCheckpointMarkerBuildTests
    {
        [Test]
        public void 더미_표식은_옛_표와_같은_부활을_준다()
        {
            var root = new GameObject("root");
            try
            {
                SkydiveCourseBuilder.CreateCheckpointMarkers(root.transform, SkydiveCourseLayout.SpawnY, SkydiveCourseLayout.RespawnPoints);
                var field = new CheckpointField();
                foreach (var m in root.GetComponentsInChildren<CheckpointMarker>())
                {
                    m.Construct(field);
                }

                Assert.AreEqual(SkydiveCourseLayout.SpawnY, field.SpawnY, "스폰 표식이 빠지면 맨 위 선반이 스폰이 되어 아래로 순간이동한다");
                //  2600~3000 사이에서 죽으면 옛 표와 똑같이 (0,3000,0)으로
                float y = SkydiveCheckpoints.LastPassedShelfY(2800f, field.ShelfYs, field.SpawnY);
                Assert.AreEqual(3000f, y);
                Assert.AreEqual(new Vector3(0f, 3000f, 0f), field.RespawnPoints[y]);
                foreach (var pair in SkydiveCourseLayout.RespawnPoints)
                {
                    Assert.AreEqual(pair.Value, field.RespawnPoints[pair.Key]);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 클라_스카이다이브_스코프가_CheckpointField를_등록한다()
        {
            //  클라는 판정을 안 하지만 표식의 [Inject]가 요구한다 — 빠지면 맵 씬 주입이 그 자리에서 끊긴다. 컴파일은 통과한다.
            string source = File.ReadAllText("Assets/Scripts/Game/SkydiveLifetimeScope.cs");
            StringAssert.Contains("builder.Register<CheckpointField>(Lifetime.Singleton)", source);
        }
    }
}
