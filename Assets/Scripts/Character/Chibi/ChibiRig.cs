using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 보이는 몸의 애니메이터를 찾는다. 치비는 뿌리(크기 1) 아래 자식 Model이 모델 배율과 애니메이터를 갖고,
    /// 기사 같은 옛 몸은 뿌리에 애니메이터가 있다 — 둘 다 이 함수 하나로 찾는다.
    /// </summary>
    public static class ChibiRig
    {
        public static Animator AnimatorOf(GameObject visual) =>
            visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
    }
}
