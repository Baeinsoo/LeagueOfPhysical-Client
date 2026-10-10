using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// ChibiLookApplier — 룩(슬롯 코드→품목 코드)을 치비 색·원시도형·표정으로 바꾸는 로직.
    /// 순수 계산(색·키 파싱)은 실제 배포 .bytes(CosmeticCatalogTests와 같은 자료)로, Unity 부착부(모자·장식)는
    /// 더미 계층("Root/Head", "Root/Chest")으로 본다.
    /// </summary>
    public class ChibiLookTests
    {
        private static CosmeticCatalog NewCatalog() =>
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        private static PlayerLook LookWith(params (string slot, string item)[] entries)
        {
            var slots = new Dictionary<string, string>();
            foreach (var (slot, item) in entries)
            {
                slots[slot] = item;
            }
            return new PlayerLook(slots, "테스터", 1);
        }

        private static Color ColorFromHtml(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        private static List<Transform> ChildrenNamed(Transform parent, string name)
        {
            var result = new List<Transform>();
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    result.Add(child);
                }
            }
            return result;
        }

        [Test]
        public void 유효한_육각색은_파싱된다()
        {
            Assert.IsTrue(ChibiLookApplier.TryParseHex("#3CB371", out var color));
            Assert.AreEqual(ColorFromHtml("#3CB371"), color);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("3CB371")]        // # 없음
        [TestCase("#3CB37")]        // 자리수 부족
        [TestCase("#3CB371FF")]     // 알파까지 — RGB 6자리만 받는다
        [TestCase("#GGGGGG")]       // 16진수 아님
        public void 잘못된_육각색은_거부한다(string hex)
        {
            Assert.IsFalse(ChibiLookApplier.TryParseHex(hex, out _));
        }

        [Test]
        public void 큐브_키를_파싱한다()
        {
            Assert.IsTrue(ChibiLookApplier.TryParsePrimitive("cube:#E53935:0.25", out var spec));
            Assert.AreEqual(PrimitiveType.Cube, spec.Type);
            Assert.AreEqual(ColorFromHtml("#E53935"), spec.Color);
            Assert.AreEqual(0.25f, spec.Size, 1e-5f);
        }

        [Test]
        public void 구_키를_파싱한다()
        {
            Assert.IsTrue(ChibiLookApplier.TryParsePrimitive("sphere:#FFD54F:0.15", out var spec));
            Assert.AreEqual(PrimitiveType.Sphere, spec.Type);
            Assert.AreEqual(0.15f, spec.Size, 1e-5f);
        }

        [TestCase("cone:#E53935:0.25")]       // 모르는 도형
        [TestCase("cube:#E53935")]            // 칸 부족
        [TestCase("cube:bad:0.25")]           // 색이 16진수가 아님
        [TestCase("cube:#E53935:not-a-number")]
        [TestCase("")]
        [TestCase((string)null)]
        public void 잘못된_원시도형_키는_거부한다(string key)
        {
            Assert.IsFalse(ChibiLookApplier.TryParsePrimitive(key, out _));
        }

        [Test]
        public void 상의_틴트가_있으면_덮고_하의는_기본값_그대로다()
        {
            var catalog = NewCatalog();
            var look = LookWith(("top", "top_tint_green"));

            var colors = ChibiLookApplier.ColorsFor("entity-1", look, catalog);

            Assert.AreEqual(ColorFromHtml("#3CB371"), colors.Top);
            Assert.AreEqual(ChibiOutfit.ColorsFor("entity-1").Bottom, colors.Bottom);
        }

        [Test]
        public void 마스터데이터에_없는_코드는_기본_색이다()
        {
            var catalog = NewCatalog();
            var look = LookWith(("top", "no_such_item"));

            var colors = ChibiLookApplier.ColorsFor("entity-1", look, catalog);

            Assert.AreEqual(ChibiOutfit.ColorsFor("entity-1").Top, colors.Top);
        }

        [Test]
        public void 룩이_없으면_기본_치비_색이다()
        {
            var colors = ChibiLookApplier.ColorsFor("entity-1", null, null);

            Assert.AreEqual(ChibiOutfit.ColorsFor("entity-1").Top, colors.Top);
            Assert.AreEqual(ChibiOutfit.ColorsFor("entity-1").Bottom, colors.Bottom);
        }

        [Test]
        public void 환호_표정_품목이면_환호_표정이다()
        {
            var catalog = NewCatalog();
            var look = LookWith(("face", "face_dummy_b"));   // assetKey "Cheer"

            Assert.AreEqual(ChibiExpression.Cheer, ChibiLookApplier.ExpressionFor(look, catalog));
        }

        [Test]
        public void 모르는_표정_코드는_기본_표정이다()
        {
            var catalog = NewCatalog();
            var look = LookWith(("face", "no_such_item"));

            Assert.AreEqual(ChibiExpression.Normal, ChibiLookApplier.ExpressionFor(look, catalog));
        }

        [Test]
        public void 룩이_없으면_기본_표정이다()
        {
            Assert.AreEqual(ChibiExpression.Normal, ChibiLookApplier.ExpressionFor(null, null));
        }

        [Test]
        public void 모자_원시도형이_머리에_붙고_두번_입혀도_하나다()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            try
            {
                var catalog = NewCatalog();
                var look = LookWith(("hat", "hat_cube_red"));   // assetKey "cube:#E53935:0.25"

                ChibiLookApplier.ApplyPrimitives(root, look, catalog);
                ChibiLookApplier.ApplyPrimitives(root, look, catalog);

                var hats = ChildrenNamed(head.transform, "Look_hat");
                Assert.AreEqual(1, hats.Count);
                Assert.AreEqual(Vector3.one * 0.25f, hats[0].localScale);
                Assert.IsNull(hats[0].GetComponent<Collider>());
                Object.DestroyImmediate(hats[0].GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 모르는_모자_코드는_장식_슬롯에_영향을_주지_않는다()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            var chest = new GameObject("Chest");
            chest.transform.SetParent(root.transform);
            try
            {
                var catalog = NewCatalog();
                var look = LookWith(("hat", "no_such_item"), ("accessory", "acc_sphere"));

                ChibiLookApplier.ApplyPrimitives(root, look, catalog);

                Assert.AreEqual(0, ChildrenNamed(head.transform, "Look_hat").Count);
                var accessories = ChildrenNamed(chest.transform, "Look_accessory");
                Assert.AreEqual(1, accessories.Count);
                Object.DestroyImmediate(accessories[0].GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 룩이_없이_다시_입히면_전에_붙인_원시도형을_뗀다()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            try
            {
                ChibiLookApplier.ApplyPrimitives(root, LookWith(("hat", "hat_cube_red")), NewCatalog());
                Assert.AreEqual(1, ChildrenNamed(head.transform, "Look_hat").Count, "전제: 먼저 모자가 붙어 있어야 한다");

                ChibiLookApplier.ApplyPrimitives(root, null, null);

                Assert.AreEqual(0, ChildrenNamed(head.transform, "Look_hat").Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 다시_입히면_이전_모자의_머티리얼을_지운다()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            Material second = null;
            try
            {
                var catalog = NewCatalog();
                var look = LookWith(("hat", "hat_cube_red"));

                ChibiLookApplier.ApplyPrimitives(root, look, catalog);
                var first = head.transform.Find("Look_hat").GetComponent<MeshRenderer>().sharedMaterial;
                Assert.IsTrue(first != null, "전제: 모자 머티리얼이 있어야 한다");

                ChibiLookApplier.ApplyPrimitives(root, look, catalog);
                second = head.transform.Find("Look_hat").GetComponent<MeshRenderer>().sharedMaterial;

                //  매번 새로 만든 머티리얼이라, 다시 입힐 때 안 지우면 쌓인다.
                Assert.IsTrue(first == null, "이전 머티리얼이 남아 있다(누수)");
            }
            finally
            {
                if (second != null) Object.DestroyImmediate(second);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 모자는_돌아간_머리_뼈에서도_월드_위쪽에_얹힌다()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);
            head.transform.position = new Vector3(1f, 2f, 3f);
            //  PolyOne 치비처럼 뼈 로컬 −X가 월드 위, −Y가 앞이 되게 돌린다.
            head.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            try
            {
                Assert.That(Vector3.Distance(head.transform.TransformDirection(Vector3.left), Vector3.up), Is.LessThan(1e-4f), "전제: 로컬 −X가 월드 위");

                ChibiLookApplier.ApplyPrimitives(root, LookWith(("hat", "hat_cube_red")), NewCatalog());   // 0.25 큐브

                var hat = head.transform.Find("Look_hat");
                var headCenter = head.transform.TransformPoint(ChibiHeadFrame.Center);
                var offset = hat.position - headCenter;
                Assert.AreEqual(ChibiHeadFrame.Radius + 0.125f, offset.y, 0.01f, "모자가 머리 꼭대기 위에 있지 않다");
                Assert.AreEqual(0f, offset.x, 0.01f);
                Assert.AreEqual(0f, offset.z, 0.01f);
                //  모자의 위도 월드 위를 본다.
                Assert.That(Vector3.Dot(hat.up, Vector3.up), Is.GreaterThan(0.999f));
                Object.DestroyImmediate(hat.GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 장식은_돌아간_가슴_뼈에서도_몸_앞에_붙는다()
        {
            var root = new GameObject("Root");
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            var chest = new GameObject("Chest");
            chest.transform.SetParent(root.transform);
            chest.transform.localPosition = new Vector3(0f, 1f, 0f);
            chest.transform.localRotation = Quaternion.Euler(-90f, 0f, -90f);
            try
            {
                ChibiLookApplier.ApplyPrimitives(root, LookWith(("accessory", "acc_sphere")), NewCatalog());

                var accessory = chest.transform.Find("Look_accessory");
                var offset = accessory.position - chest.transform.position;
                Assert.That(Vector3.Distance(offset, root.transform.forward * 0.08f), Is.LessThan(0.005f),
                    $"장식이 몸 앞이 아니다: {offset}");
                Object.DestroyImmediate(accessory.GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
