using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기에서 "땅·바위·나무가 막았나"를 잰다. 말 탄 몸의 이동용 캡슐도 같은 Default 층이라 그냥 재면
    /// 몸(판정 구보다 큰 캡슐)을 지형으로 잘못 만난다 — 서버의 지형 판정처럼 <see cref="LOPActor"/> 아래 충돌체는 뺀다.
    /// </summary>
    public static class MountedTerrain
    {
        private static readonly int Mask = LayerMask.GetMask("Default");
        // 서버 지형 판정과 같은 크기 — 넘치면 가까운 것이 빠질 수 있는데, 그 일도 클·서가 같이 겪어야 한다.
        private static readonly RaycastHit[] Buffer = new RaycastHit[16];

        public static bool Linecast(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length < 1e-6f)
            {
                hit = default;
                return false;
            }
            return Raycast(from, segment / length, length, out hit);
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Buffer, maxDistance, Mask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (Buffer[i].collider.GetComponentInParent<LOPActor>() != null)
                {
                    continue;
                }
                if (found == false || Buffer[i].distance < hit.distance)
                {
                    hit = Buffer[i];
                    found = true;
                }
            }
            return found;
        }
    }
}
