using GameFramework;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class SnapInterpolator : MonoBehaviour
    {
        public LOPEntity entity { get; set; }

        private BoundedList<EntitySnap> serverEntitySnaps = new BoundedList<EntitySnap>(20);
    
        private void LateUpdate()
        {
            Interpolation();
        }

        private void Interpolation()
        {
            if (serverEntitySnaps.Count < 2)
            {
                return;
            }

            float interpolationBackTime = 0.1f;
            double interpolationTime = Time.timeAsDouble - interpolationBackTime;

            for (int i = serverEntitySnaps.Count - 1; i >= 1; i--)
            {
                EntitySnap newer = serverEntitySnaps[i];
                EntitySnap older = serverEntitySnaps[i - 1];

                if (older.timestamp <= interpolationTime && interpolationTime <= newer.timestamp)
                {
                    float t = (float)((interpolationTime - older.timestamp) / (newer.timestamp - older.timestamp));
                    t = Mathf.Clamp01(t);

                    Vector3 interpolatedPosition = Vector3.Lerp(older.position, newer.position, t);
                    Quaternion interpolatedRotation = Quaternion.Slerp(Quaternion.Euler(older.rotation), Quaternion.Euler(newer.rotation), t);

                    entity.position = interpolatedPosition;
                    entity.rotation = interpolatedRotation.eulerAngles;

                    return;
                }
            }

            EntitySnap fallback = serverEntitySnaps
                .Where(snap => snap.timestamp <= interpolationTime)
                .OrderByDescending(snap => snap.timestamp)
                .FirstOrDefault();

            if (fallback != null)
            {
                entity.position = fallback.position;
                entity.rotation = fallback.rotation;
            }
        }

        public void AddServerEntitySnap(EntitySnap entitySnap)
        {
            serverEntitySnaps.Add(entitySnap);
        }
    }
}
