using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  Dodge는 경기장 전체를 고정 시점으로 본다. 그런데 캐릭터가 생기면 씬 코디네이터가
    //  무조건 SetTarget을 부른다 — 그래서 "대상은 있어도 안 따라가는" 설정이 필요하다.
    //  기본값은 지금처럼 따라가야 한다(다른 모드 전부가 그걸 믿는다).
    public class CameraControllerFollowTests
    {
        GameObject root;
        CameraController controller;
        Camera camera;
        Transform target;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("rig");
            camera = new GameObject("cam").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 20f, -10f);
            camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            controller = root.AddComponent<CameraController>();
            var so = new UnityEditor.SerializedObject(controller);
            so.FindProperty("mainCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
            target = new GameObject("target").transform;
            target.position = new Vector3(5f, 0f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(target.gameObject);
        }

        void RunLateUpdate() =>
            typeof(CameraController)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);

        [Test]
        public void 기본값이면_대상을_따라간다()
        {
            Assert.IsTrue(controller.FollowTarget);
            controller.SetTarget(target);
            Vector3 before = camera.transform.position;
            target.position += new Vector3(3f, 0f, 0f);
            RunLateUpdate();
            Assert.AreNotEqual(before, camera.transform.position);
        }

        [Test]
        public void 끄면_대상이_있어도_제자리에_있다()
        {
            controller.FollowTarget = false;
            controller.SetTarget(target);
            Vector3 before = camera.transform.position;
            Quaternion beforeRot = camera.transform.rotation;
            target.position += new Vector3(3f, 0f, 0f);
            RunLateUpdate();
            Assert.AreEqual(before, camera.transform.position);
            Assert.AreEqual(beforeRot, camera.transform.rotation);
            Assert.AreSame(target, controller.Target);
        }
    }
}
