using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 보이는 몸이 치비면 얼굴 판·저지를 입힌다 — 활쏘기·스카이다이브가 같이 쓴다.
    /// 치비인가 = 휴머노이드 + 컨트롤러에 Happy 클립(멈춘 애니메이터는 parameters가 비어 클립으로 본다). 기사 등은 건드리지 않는다.
    /// </summary>
    public static class ChibiDresser
    {
        public static bool IsChibi(GameObject visual)
        {
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (animator == null || animator.isHuman == false || animator.runtimeAnimatorController == null)
            {
                return false;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == "Happy")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>얼굴 판·저지를 입힌다. 이미 입었으면 얼굴은 새로 붙이지 않는다.</summary>
        public static void Dress(string entityId, GameObject visual, Material faceMaterial)
        {
            ChibiOutfit.Apply(visual, ChibiOutfit.ColorsFor(entityId));
            if (faceMaterial == null || visual.GetComponent<ChibiFace>() != null)
            {
                return;
            }
            var face = visual.AddComponent<ChibiFace>();
            face.faceMaterial = faceMaterial;
            face.Build();
        }
    }
}
