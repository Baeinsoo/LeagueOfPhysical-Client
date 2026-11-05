using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 머리 위·얼굴 앞에 띄우는 연출(모자·머리 위 카드·철썩 카드·별·만화 효과)이 쓰는 자리.
    /// 몸 프리팹이 머리 뼈 아래에 정수리(<see cref="HatAnchorName"/>)·얼굴 앞(<see cref="FaceAnchorName"/>) 자리를 두면 그것을 쓴다 —
    /// 몸마다 머리 뼈에서 정수리·얼굴까지 거리가 달라(RodinA는 뼈가 0.11 m 높다) 뼈 기준 상수로는 맞출 수 없다.
    /// 자리가 없는 몸(기사 등)은 옛 PolyOne 치비에서 잰 거리로 뼈에서 짐작한다.
    /// </summary>
    public static class ChibiHeadSpots
    {
        /// <summary>정수리(바보털 뺀 머리카락 꼭대기). 위 = 로컬 +Y, 앞 = 로컬 +Z.</summary>
        public const string HatAnchorName = "HatAnchor";

        /// <summary>얼굴 그림 면의 앞 가운데. 앞(로컬 +Z) = 얼굴이 보는 쪽.</summary>
        public const string FaceAnchorName = "FaceAnchor";

        //  옛 PolyOne 치비(게임 크기, 모델 ×3)에서 잰 머리 뼈 → 정수리·얼굴 거리. 자리가 없는 몸의 짐작에만 쓴다.
        public const float PolyOneCrownAboveBone = 0.78f;
        public const float PolyOneFaceUp = 0.25f;
        public const float PolyOneFaceForward = 0.35f;

        /// <summary>머리 뼈도 없는 몸 — 모델 뿌리에서 이 높이를 머리 뼈 자리로 본다.</summary>
        public const float NoHeadBoneHeight = 1.1f;

        public static Vector3 Crown(Transform model, Transform head)
        {
            var spot = head != null ? head.Find(HatAnchorName) : null;
            if (spot != null) return spot.position;
            return HeadBonePosition(model, head) + Vector3.up * PolyOneCrownAboveBone;
        }

        /// <summary>얼굴 앞 가운데. 자세(젖힘·벌러덩)를 따른다 — 자리는 머리 뼈에 붙어 있고, 짐작은 모델 축으로 한다.</summary>
        public static Vector3 FaceFront(Transform model, Transform head)
        {
            var spot = head != null ? head.Find(FaceAnchorName) : null;
            if (spot != null) return spot.position;
            return HeadBonePosition(model, head) + model.up * PolyOneFaceUp + model.forward * PolyOneFaceForward;
        }

        private static Vector3 HeadBonePosition(Transform model, Transform head)
            => head != null ? head.position : model.position + model.up * NoHeadBoneHeight;
    }
}
