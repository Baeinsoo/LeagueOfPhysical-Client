using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>몸의 애니메이터 찾기 — 뿌리(옛 몸) 또는 자식 Model(치비)만. 소품 안쪽 깊은 곳의 애니메이터는 몸 애니메이터가 아니다.</summary>
    public class ChibiRigTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        [Test]
        public void 뿌리에_있으면_뿌리()
        {
            _root = new GameObject("knight");
            var animator = _root.AddComponent<Animator>();
            Assert.AreSame(animator, ChibiRig.AnimatorOf(_root));
        }

        [Test]
        public void 자식_Model에_있으면_그것()
        {
            _root = new GameObject("Chibi");
            var model = new GameObject("Model");
            model.transform.SetParent(_root.transform, false);
            var animator = model.AddComponent<Animator>();
            Assert.AreSame(animator, ChibiRig.AnimatorOf(_root));
        }

        //  경험치 구슬처럼 소품 손자에 붙은 애니메이터를 몸으로 잡으면 매 프레임 Run을 걸어 경고가 쏟아진다.
        [Test]
        public void 다른_자식_깊은_곳의_애니메이터는_찾지_않는다()
        {
            _root = new GameObject("ExpMarble");
            var child = new GameObject("bottle");
            child.transform.SetParent(_root.transform, false);
            var grandchild = new GameObject("bottle_green");
            grandchild.transform.SetParent(child.transform, false);
            grandchild.AddComponent<Animator>();
            Assert.IsNull(ChibiRig.AnimatorOf(_root));
        }
    }
}
