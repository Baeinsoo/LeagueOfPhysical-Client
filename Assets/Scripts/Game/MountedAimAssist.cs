using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기 조준 보정(클라만). 서버 판정에는 들어가지 않고 카메라를 돌리는 입력만 가공한다.
    /// 조준점 둘레 원뿔 안에 적이 있으면 끌기를 둔하게 하고, 그 적이 화면에서 움직인 만큼 절반을 따라가 준다.
    /// </summary>
    public static class MountedAimAssist
    {
        /// <summary>적의 "가슴" = 그려진 말 뿌리 위 이 높이. 판정 구(기수 2.1, 말 몸 1.4) 사이를 겨눈다.</summary>
        public const float ChestHeight = 1.75f;

        /// <summary>원뿔(<paramref name="coneDeg"/>은 반각) 안에서 조준 방향과 각이 가장 작은 적. yaw/pitch는 카메라에서 그 가슴을 본 각(pitch 양수가 위).</summary>
        public static bool TryPick(Vector3 cameraPos, Vector3 aimDir, IReadOnlyList<(string id, Vector3 chest)> targets,
                                   float coneDeg, float range, out string id, out float yawDeg, out float pitchDeg)
        {
            id = null;
            yawDeg = 0f;
            pitchDeg = 0f;
            float best = float.MaxValue;
            Vector3 bestChest = default;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 to = targets[i].chest - cameraPos;
                if (to.sqrMagnitude > range * range || to.sqrMagnitude < 1e-6f)
                {
                    continue;
                }
                float angle = Vector3.Angle(aimDir, to);
                if (angle <= coneDeg && angle < best)
                {
                    best = angle;
                    id = targets[i].id;
                    bestChest = targets[i].chest;
                }
            }
            if (id == null)
            {
                return false;
            }
            Vector2 angles = MountedAimView.AimAngles(cameraPos, bestChest);
            yawDeg = angles.x;
            pitchDeg = angles.y;
            return true;
        }

        /// <summary>끌기 감도에 곱할 값. 잠기면 <paramref name="friction"/>, 아니면 1.</summary>
        public static float Friction(bool locked, float friction) => locked ? friction : 1f;

        /// <summary>같은 적을 계속 잡고 있을 때만, 적 각이 바뀐 만큼의 <paramref name="track"/> 비율을 카메라에 더해 준다.</summary>
        public static Vector2 Track(Vector2 lastTargetAngles, Vector2 nowTargetAngles, bool sameTarget, float track)
        {
            if (sameTarget == false)
            {
                return Vector2.zero;
            }
            return new Vector2(Mathf.DeltaAngle(lastTargetAngles.x, nowTargetAngles.x) * track,
                               (nowTargetAngles.y - lastTargetAngles.y) * track);
        }
    }

    /// <summary>조준 보정이 지금 적을 잡고 있는지. <see cref="MountedAimView"/>가 쓰고 조작판이 읽는다.</summary>
    public class MountedAimAssistState
    {
        public bool Locked;
    }
}
