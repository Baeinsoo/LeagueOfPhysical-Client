using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 보이는 몸의 애니메이터를 찾는다. 치비는 뿌리(크기 1) 아래 자식 Model이 모델 배율과 애니메이터를 갖고,
    /// 기사 같은 옛 몸은 뿌리에 애니메이터가 있다 — 둘 다 이 함수 하나로 찾는다.
    /// </summary>
    public static class ChibiRig
    {
        public const string ModelChildName = "Model";

        //  더 깊이 찾지 않는다 — 소품(경험치 구슬 등) 안쪽에 붙은 애니메이터는 몸 애니메이터가 아니다.
        public static Animator AnimatorOf(GameObject visual)
        {
            if (visual == null) return null;
            if (visual.TryGetComponent<Animator>(out var onRoot)) return onRoot;
            var model = visual.transform.Find(ModelChildName);
            return model != null && model.TryGetComponent<Animator>(out var onModel) ? onModel : null;
        }
    }
}
