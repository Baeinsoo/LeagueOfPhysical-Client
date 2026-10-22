using System.Reflection;

using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 말·기수 그림이 보간기와 싸우지 않는지. 보간기는 매 프레임 LateUpdate에서 몸 그림의 뿌리(visual)
    /// 월드 자세를 덮어쓰고, 이 뷰(LateTick)는 그보다 먼저 돈다 — 뿌리에 쓴 값은 그 프레임에 지워진다.
    /// </summary>
    public class MountedHorseViewTests
    {
        private const string Id = "1";

        private GameObject actorGo;

        [TearDown]
        public void TearDown()
        {
            if (actorGo != null)
            {
                Object.DestroyImmediate(actorGo);
            }
        }

        private (MountedHorseView view, Transform visual, Transform model, ArcheryAim aim) Build(bool local)
        {
            var registry = new GameFramework.World.EntityRegistry();
            var entity = new GameFramework.World.Entity(Id);
            entity.Add(new Mount());
            var aim = new ArcheryAim();
            entity.Add(aim);
            registry.Add(entity);

            actorGo = new GameObject("Actor_" + Id);
            var entityView = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId(Id);
            actor.SetView(entityView);

            //  치비 모양: 뿌리 크기 1, 모델·애니메이터는 자식 Model.
            var visual = new GameObject("Chibi");
            visual.transform.SetParent(actorGo.transform, false);
            var model = new GameObject(ChibiRig.ModelChildName);
            model.transform.SetParent(visual.transform, false);
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(entityView, visual);

            var actors = new ActorRegistry();
            actors.Add(actor);
            var context = new FakePlayerContext { entityId = local ? Id : "someone-else" };
            return (new MountedHorseView(actors, registry, context), visual.transform, model.transform, aim);
        }

        // 보간기가 하는 일: 뿌리의 월드 위치·회전을 통째로 덮는다.
        private static void Interpolate(Transform visual, Vector3 position, float yaw)
        {
            visual.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        [Test]
        public void 보간기가_뿌리를_덮어써도_기수는_안장에_앉고_말은_뿌리를_따라간다()
        {
            var (view, visual, model, aim) = Build(local: true);
            aim.Yaw = 90f;

            view.LateTick();
            Interpolate(visual, new Vector3(10f, 0f, 5f), 30f);
            view.LateTick();
            Interpolate(visual, new Vector3(11f, 0f, 5f), 30f);

            Assert.AreEqual(MountedHorseView.SaddleHeight, model.position.y - visual.position.y, 1e-4f,
                "기수 모델은 뿌리보다 안장 높이만큼 위에 있어야 한다");

            var horse = actorGo.transform.Find("Chibi/MountedHorse");
            Assert.IsNotNull(horse, "말은 뿌리(visual)의 자식이어야 보간된 자리를 같이 따라간다");
            Assert.IsTrue(horse.IsChildOf(visual));

            Assert.AreEqual(0f, Mathf.DeltaAngle(90f, model.eulerAngles.y), 0.01f,
                "내 기수 상체는 말 머리(30도)와 상관없이 조준(90도)을 본다");
        }

        [Test]
        public void 남의_기수는_안장에_앉되_말과_같은_쪽을_본다()
        {
            var (view, visual, model, aim) = Build(local: false);
            aim.Yaw = 90f;

            view.LateTick();
            Interpolate(visual, new Vector3(3f, 0f, 0f), 30f);
            view.LateTick();
            Interpolate(visual, new Vector3(4f, 0f, 0f), 30f);

            Assert.AreEqual(MountedHorseView.SaddleHeight, model.position.y - visual.position.y, 1e-4f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(30f, model.eulerAngles.y), 0.01f);
        }

        [TestCase(30f, 90f, 60f)]
        [TestCase(350f, 10f, 20f)]
        [TestCase(10f, 350f, -20f)]
        [TestCase(0f, 0f, 0f)]
        public void 기수_상체는_말_머리에서_조준까지의_가까운_쪽_각도만큼_돈다(float heading, float aimYaw, float expected)
        {
            Assert.AreEqual(expected, MountedHorseView.RiderLocalYaw(heading, aimYaw), 1e-4f);
        }

        [Test]
        public void 안장_자리는_모델의_원래_자리에서_안장_높이만큼_올리고_조금_뒤로_뺀_곳()
        {
            var rest = new Vector3(-0.04f, 0f, 0.01f);
            var seat = MountedHorseView.SeatLocalPosition(rest);
            Assert.AreEqual(rest.x, seat.x, 1e-6f);
            Assert.AreEqual(rest.y + MountedHorseView.SaddleHeight, seat.y, 1e-6f);
            Assert.AreEqual(rest.z + MountedHorseView.SaddleBack, seat.z, 1e-6f);
        }

        private class FakePlayerContext : IPlayerContext
        {
            public GameFramework.ISession session { get; set; }
            public string entityId { get; set; }
            public LOPActor actor { get; set; }
        }
    }
}
