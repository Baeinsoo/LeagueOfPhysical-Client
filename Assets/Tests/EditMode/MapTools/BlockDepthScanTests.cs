using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LOP.MapTools;

namespace LOP.Tests.MapTools
{
    public class BlockDepthScanTests
    {
        //  기본은 <틈을 만드는 면>(벽)이다 — 판정이 보는 것이 그것이므로.
        static List<BlockDepth> Blocks(params float[] depths)
        {
            var list = new List<BlockDepth>();
            for (int i = 0; i < depths.Length; i++)
            {
                list.Add(new BlockDepth($"Block{i}", i * 10f, depths[i], FaceKind.Wall));
            }
            return list;
        }

        static BlockDepth Decoration(string name, float backDepth)
        {
            return new BlockDepth(name, 0f, backDepth, FaceKind.RenderOnly);
        }

        //  <b>렌더러</b>의 월드 bounds를 흉내 낸 것이다 — 이 커널이 받는 것은 그려지는 면의
        //  범위지 콜라이더의 범위가 아니다. 콜라이더 z두께는 눈에 안 보여 화면을 못 바꾼다.
        static Bounds ZRange(float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(0f, 0f, minZ), new Vector3(1f, 1f, maxZ));
            return bounds;
        }

        [Test]
        public void 전부_판정면_앞이면_정직하다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 0f, 0f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  허용오차 안쪽은 정직으로 본다 — 부동소수점 찌꺼기까지 경고하면 신호가 죽는다.
        [Test]
        public void 허용오차_안쪽은_세지_않는다()
        {
            var v = BlockDepthScan.Judge(Blocks(0.005f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  경계값 자체도 "맞은 것"이다. 안쪽(0.005)만 확인하면 `<=`를 `<`로 바꿔도 초록이라,
        //  허용오차가 어느 쪽을 포함하는지는 아무 테스트도 지키지 않게 된다.
        [Test]
        public void 허용오차와_정확히_같으면_맞은_것으로_본다()
        {
            var v = BlockDepthScan.Judge(Blocks(0.01f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void 뒤로_그려지는_것만_센다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 1.25f, 0f, 2.70f), tolerance: 0.01f);
            Assert.IsFalse(v.Honest);
            Assert.AreEqual(2, v.Count);
        }

        //  가장 심한 것을 집어내야 고칠 자리를 안다. 개수만으로는 어디가 문제인지 모른다.
        [Test]
        public void 가장_두꺼운_것을_이름과_자리까지_집어낸다()
        {
            var v = BlockDepthScan.Judge(Blocks(1.25f, 2.70f, 0.50f), tolerance: 0.01f);
            Assert.AreEqual(2.70f, v.WorstBackDepth, 1e-4f);
            Assert.AreEqual("Block1", v.WorstName);
            Assert.AreEqual(10f, v.WorstX, 1e-4f);
        }

        [Test]
        public void 블록이_없으면_정직하다()
        {
            var v = BlockDepthScan.Judge(new List<BlockDepth>(), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
        }

        //  "안 쟀다"(null)와 "재 봤더니 없었다"(빈 목록)는 뜻이 다르다 — 판정이 둘을 같은
        //  답으로 뭉개면 안 쟀는데 정직하다고 말하게 된다. 가리는 자리는 부르는 쪽이다.
        [Test]
        public void 안_잰_목록은_판정하지_않고_거절한다()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => BlockDepthScan.Judge(null, tolerance: 0.01f));
        }

        //  재는 쪽(검사)과 고치는 쪽(정렬)이 같은 규칙을 봐야 한다 — 규칙이 갈라지면 서로 다른
        //  것을 보면서 맞췄다고 착각한다. 그래서 규칙은 여기 한 군데에만 둔다.
        [Test]
        public void 배경은_새가_지나는_깊이에_안_그려진다()
        {
            Assert.IsFalse(BlockDepthScan.IsGameplayBlock(ZRange(60f, 64f), bodyRadius: 0.45f));
        }

        [Test]
        public void 새의_z대역과_겹치게_그려지면_센다()
        {
            Assert.IsTrue(BlockDepthScan.IsGameplayBlock(ZRange(0.10f, 2.70f), bodyRadius: 0.45f));
        }

        //  새 앞쪽(카메라 쪽)에만 있는 것도 안 센다 — 겹침은 양쪽으로 봐야 한다.
        [Test]
        public void 새보다_앞에만_그려지면_안_센다()
        {
            Assert.IsFalse(BlockDepthScan.IsGameplayBlock(ZRange(-5f, -1f), bodyRadius: 0.45f));
        }

        [Test]
        public void 뒤쪽_두께는_가장_뒤에_그려진_면까지의_거리다()
        {
            Assert.AreEqual(2.70f, BlockDepthScan.BackDepth(ZRange(0.10f, 2.70f)), 1e-4f);
        }

        //  판정면보다 통째로 앞에 있는 것은 뒤로 뻗은 두께가 <없다>(0). 음수를 그대로 흘리면
        //  "가장 두꺼운 것"을 고르는 자리에서 뜻 없는 값이 섞인다.
        [Test]
        public void 판정면_앞쪽은_뒤쪽_두께가_0이다()
        {
            Assert.AreEqual(0f, BlockDepthScan.BackDepth(ZRange(-2f, -0.2f)), 1e-6f);
        }

        //  고치기 <전>의 맵 모양. 장애물이 판정면을 걸치고 앉아 있으면 뒤쪽 절반만 화면을
        //  망친다 — 두께 전체(2.5)가 아니라 뒷면까지의 거리(1.25)를 세야 한다.
        [Test]
        public void 판정면을_걸친_면은_뒤쪽_절반만_센다()
        {
            Assert.AreEqual(1.25f, BlockDepthScan.BackDepth(ZRange(-1.25f, 1.25f)), 1e-4f);
        }

        //  고친 <뒤>의 맵 모양 — 이 슬라이스가 만들려는 상태 그 자체다. 뒷면이 판정면에 정확히
        //  닿으면 가장 안쪽 실루엣이 곧 판정 단면이라 화면이 정직해진다.
        [Test]
        public void 뒷면이_판정면에_닿으면_뒤쪽_두께가_0이다()
        {
            Assert.AreEqual(0f, BlockDepthScan.BackDepth(ZRange(-2.5f, 0f)), 1e-6f);
        }

        //  <b>여전히 센다</b> — 판정면 앞으로 옮겨도 새와 같은 깊이에 그려지는 것은 맞다.
        //  여기서 빠져 버리면 정렬 뒤에 "잴 것이 없어서" 정직해 보이는 가짜 초록이 된다.
        [Test]
        public void 판정면에_닿게_옮겨도_여전히_세는_대상이다()
        {
            Assert.IsTrue(BlockDepthScan.IsGameplayBlock(ZRange(-2.5f, 0f), bodyRadius: 0.45f));
        }

        //  뒤를 콜라이더가 안 받치는 면은 틈의 가장자리가 아니라, 아무리 뒤로 뻗어도 "지나갈 틈이
        //  다르게 보인다"는 사고를 못 일으킨다. 판정을 흐리면 진짜 신호가 묻힌다.
        [Test]
        public void 틈을_안_만드는_면은_판정에서_뺀다()
        {
            var v = BlockDepthScan.Judge(
                new List<BlockDepth> { Decoration("FinishLine", 1.25f) }, tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  빼되 <감추지는> 않는다 — 안 세면 빠뜨린 것과 구별이 안 돼서 다음 사람이 같은 조사를
        //  처음부터 다시 한다.
        [Test]
        public void 뺀_면은_따로_세어_알린다()
        {
            var d = BlockDepthScan.Summarize(
                new List<BlockDepth> { Decoration("Coin", 0.12f), Decoration("FinishLine", 1.25f) },
                tolerance: 0.01f, FaceKind.RenderOnly);
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual("FinishLine", d.WorstName);
            Assert.AreEqual(1.25f, d.WorstBackDepth, 1e-4f);
        }

        //  두 셈이 서로의 것을 집어 오면 안 된다. 한쪽만 확인하면 <둘 다 전부 세는> 구현도 초록이다.
        [Test]
        public void 벽과_장식을_섞어_세지_않는다()
        {
            var mixed = new List<BlockDepth>
            {
                new BlockDepth("Wall", 0f, 1.25f, FaceKind.Wall),
                Decoration("Coin", 0.12f),
            };

            Assert.AreEqual(1, BlockDepthScan.Judge(mixed, tolerance: 0.01f).Count);
            Assert.AreEqual("Wall", BlockDepthScan.Judge(mixed, tolerance: 0.01f).WorstName);
            Assert.AreEqual(1, BlockDepthScan.Summarize(mixed, tolerance: 0.01f, FaceKind.RenderOnly).Count);
            Assert.AreEqual("Coin", BlockDepthScan.Summarize(mixed, tolerance: 0.01f, FaceKind.RenderOnly).WorstName);
        }

        [Test]
        public void 안_잰_목록은_장식_요약도_거절한다()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => BlockDepthScan.Summarize(null, tolerance: 0.01f, FaceKind.RenderOnly));
        }

        //  ── Classify — 면의 종류를 <실제로 정하는> 규칙 ────────────────────────────────
        //  여기가 비어 있으면 위의 모든 테스트가 공허해진다: 저것들은 종류를 <어떻게 쓰는지>만
        //  지키므로, 종류를 <어떻게 정하는지>를 거꾸로 뒤집어도 전부 초록이다. 그러면 벽 119개가
        //  통째로 "장식"이 되어 판정에서 빠지고 ⚠️가 사라져도 아무도 못 잡는다.
        //  씬은 필요 없다 — GameObject를 손으로 세우면 된다.

        readonly List<GameObject> spawned = new List<GameObject>();

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        [TearDown]
        public void CleanUp()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) { Object.DestroyImmediate(spawned[i]); }
            }
            spawned.Clear();
        }

        [Test]
        public void 단단한_콜라이더가_붙어_있으면_벽이다()
        {
            var go = NewObject("Wall");
            go.AddComponent<BoxCollider>();

            Assert.AreEqual(FaceKind.Wall, BlockDepthScan.Classify(go));
        }

        //  트리거는 통과하는 것이지 돌아가야 하는 벽이 아니다 — 틈의 가장자리가 못 된다.
        [Test]
        public void 트리거뿐이면_벽이_아니다()
        {
            var go = NewObject("FinishLineTrigger");
            go.AddComponent<BoxCollider>().isTrigger = true;

            Assert.AreEqual(FaceKind.RenderOnly, BlockDepthScan.Classify(go));
        }

        //  꺼진 콜라이더는 부딪히지 않으므로 틈을 만들지 않는다.
        [Test]
        public void 꺼진_콜라이더뿐이면_벽이_아니다()
        {
            var go = NewObject("DisabledWall");
            go.AddComponent<BoxCollider>().enabled = false;

            Assert.AreEqual(FaceKind.RenderOnly, BlockDepthScan.Classify(go));
        }

        [Test]
        public void 콜라이더가_없으면_렌더_전용이다()
        {
            Assert.AreEqual(FaceKind.RenderOnly, BlockDepthScan.Classify(NewObject("Coin")));
        }

        //  컴포넌트 순서에 판정이 걸리면 안 된다 — 트리거가 먼저 꽂혀 있어도 단단한 것이 하나라도
        //  있으면 벽이다. 첫 번째 콜라이더만 보는 구현은 여기서 죽는다.
        [Test]
        public void 트리거와_단단한_것이_같이_있으면_벽이다()
        {
            var go = NewObject("WallWithTrigger");
            go.AddComponent<BoxCollider>().isTrigger = true;
            go.AddComponent<SphereCollider>();

            Assert.AreEqual(FaceKind.Wall, BlockDepthScan.Classify(go));
        }

        //  <b>조용히 빼면 안 되는 경우.</b> 같은 오브젝트엔 없지만 부모에 벽이 있으면
        //  "틈을 안 만든다"가 아니라 "여기서 판단 못 한다"이다 — 장식과 섞으면 진짜 벽이
        //  판정에서 새는 길이 된다.
        [Test]
        public void 부모에_콜라이더가_있으면_판단_불가로_돌려준다()
        {
            var parent = NewObject("WallParent");
            parent.AddComponent<BoxCollider>();
            var child = NewObject("ChildFace");
            child.transform.SetParent(parent.transform);

            Assert.AreEqual(FaceKind.ColliderElsewhere, BlockDepthScan.Classify(child));
        }

        [Test]
        public void 자식에_콜라이더가_있어도_판단_불가로_돌려준다()
        {
            var go = NewObject("FaceWithColliderChild");
            var child = NewObject("ColliderChild");
            child.transform.SetParent(go.transform);
            child.AddComponent<BoxCollider>();

            Assert.AreEqual(FaceKind.ColliderElsewhere, BlockDepthScan.Classify(go));
        }

        //  조상/자손 훑기는 자기 자신을 포함해 돌려준다. 그것을 안 빼면 <자기 콜라이더 때문에>
        //  늘 "다른 데 있다"가 되어 벽이 하나도 안 남는다.
        [Test]
        public void 제_콜라이더를_다른_데_있는_것으로_세지_않는다()
        {
            var parent = NewObject("Parent");
            var go = NewObject("Wall");
            go.transform.SetParent(parent.transform);
            go.AddComponent<BoxCollider>();

            Assert.AreEqual(FaceKind.Wall, BlockDepthScan.Classify(go));
        }

        //  조상/자손의 트리거는 "판단 불가"가 아니다 — 벽이 될 수 없는 것이라 알릴 이유가 없다.
        [Test]
        public void 부모의_트리거는_판단_불가로_보지_않는다()
        {
            var parent = NewObject("TriggerParent");
            parent.AddComponent<BoxCollider>().isTrigger = true;
            var child = NewObject("Coin");
            child.transform.SetParent(parent.transform);

            Assert.AreEqual(FaceKind.RenderOnly, BlockDepthScan.Classify(child));
        }

        [Test]
        public void 없는_오브젝트는_분류하지_않고_거절한다()
        {
            Assert.Throws<System.ArgumentNullException>(() => BlockDepthScan.Classify(null));
        }
    }
}
