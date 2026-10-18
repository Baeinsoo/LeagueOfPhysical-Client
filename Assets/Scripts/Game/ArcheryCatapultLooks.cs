using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 2차 표적 아홉을 색만 다른 기본 도형으로 그린다. 축제 키트에 모델이 있으면 그쪽을 먼저 쓴다.
    /// 크기는 판정 구(반지름) 안에 들어가게 잡는다 — 보이는 크기가 곧 맞는 크기다.
    /// </summary>
    public static class ArcheryCatapultLooks
    {
        /// <summary>투석기 2차 표적 아홉(데이터 `code`). 키트·연출 조리법·회색 도형이 같은 이름을 쓴다.</summary>
        public static readonly string[] Codes =
        {
            "WaterBalloon", "Watermelon", "Plate", "Rocket", "GoldenBell", "Pinata", "BirdNest", "CheerFlag", "KidBalloon",
        };

        public static bool IsFestival(string code) => System.Array.IndexOf(Codes, code) >= 0;

        /// <summary>쏘면 안 되는 것(빨간 금지 표시를 단다).</summary>
        public static bool IsForbidden(string code) => code == "BirdNest" || code == "CheerFlag" || code == "KidBalloon";

        /// <summary>
        /// 종·피냐타가 매달린 듯 좌우로 흔들리는 각(도, 앞면 기준 굴림). 그림만이다 — 판정 구 중심을 두고 돌아서 맞는 자리와 무관하다.
        /// 시안: 종 ±0.35 rad(3 rad/s), 피냐타 ±0.25 rad(2.5 rad/s).
        /// </summary>
        public static float SwayDegrees(string code, float seconds, float phase)
        {
            float p = phase * 2f * Mathf.PI;
            switch (code)
            {
                case "GoldenBell": return 20f * Mathf.Sin(seconds * 3f + p);
                case "Pinata": return 14f * Mathf.Sin(seconds * 2.5f + p);
                default: return 0f;
            }
        }

        /// <summary>
        /// 투석기 그림의 그 시각 회전. 앞면(로컬 +z)이 사수를 보고, 접시는 판정과 같은 회전각으로 로컬 x축(사수 기준 가로)을 돈다 —
        /// 보이는 납작함이 곧 판정 크기다. 종·피냐타의 흔들림은 그림만(로컬 z축 굴림).
        /// 떠 있는 표적·발사 직전 날아오는 그림·맞은 뒤 남는 그림이 모두 이것을 불러야 넘어가는 순간 각이 안 튄다.
        /// </summary>
        public static Quaternion RotationAt(in ArcheryTarget target, string code, double tick, float tickInterval)
        {
            float spinDeg = ArcheryTargetMotion.SpinAngleAt(target, tick, tickInterval) * Mathf.Rad2Deg;
            float seconds = (float)((tick - target.SpawnTick) * tickInterval);
            float swayDeg = SwayDegrees(code, seconds, target.MotionPhase);
            return Quaternion.LookRotation(target.Facing == Vector3.zero ? Vector3.back : target.Facing)
                 * Quaternion.AngleAxis(spinDeg, Vector3.right)
                 * Quaternion.AngleAxis(swayDeg, Vector3.forward);
        }

        /// <summary>
        /// 원점(판정 구 중심)에 선 그림 하나. 앞면이 <b>로컬 +z</b>다 — 부르는 쪽이
        /// <c>Quaternion.LookRotation(target.Facing)</c>으로 돌려 사수를 보게 한다.
        /// 이름 끝 <c>-placeholder</c>는 "재질을 이 그림이 따로 만들었다"는 표시다(지울 때 재질도 지운다).
        /// </summary>
        public static GameObject BuildPlaceholder(string code, float radius)
        {
            var root = new GameObject("catapult-" + code + "-placeholder");
            switch (code)
            {
                case "WaterBalloon":
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * radius * 2f, new Color(0.25f, 0.70f, 1f));
                    break;
                case "Watermelon":
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * radius * 2f, new Color(0.18f, 0.62f, 0.27f));
                    break;
                case "Plate":    // 사수를 향한 얇은 원판 — 회전은 화면이 로컬 x축으로 준다
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                         new Vector3(radius * 2f, 0.03f, radius * 2f), new Color(0.96f, 0.95f, 0.93f));
                    break;
                case "Rocket":   // 세운 막대(길이 1.8r)
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity,
                         new Vector3(radius * 0.5f, radius * 0.9f, radius * 0.5f), new Color(0.91f, 0.26f, 0.23f));
                    break;
                case "GoldenBell":
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                         new Vector3(radius * 2f, radius * 1.8f, radius * 2f), new Color(1f, 0.8f, 0.15f));
                    break;
                case "Pinata":   // 45° 돌린 납작한 상자 = 마름모(대각 반 = 0.92r)
                    Part(root, PrimitiveType.Cube, Vector3.zero, Quaternion.Euler(0f, 0f, 45f),
                         new Vector3(radius * 1.3f, radius * 1.3f, radius * 0.6f), new Color(1f, 0.54f, 0.24f));
                    break;
                case "BirdNest":
                case "CheerFlag":
                case "KidBalloon":   // 종류 색 판 + 빨간 금지 표시(고리와 빗금)
                    Color face = code == "BirdNest" ? new Color(0.54f, 0.35f, 0.17f)
                               : code == "CheerFlag" ? new Color(0.23f, 0.36f, 1f) : new Color(1f, 0.82f, 0.25f);
                    Part(root, PrimitiveType.Cube, Vector3.zero, Quaternion.identity,
                         new Vector3(radius * 1.3f, radius * 1.3f, 0.06f), face);
                    var forbid = new GameObject("forbid");
                    forbid.transform.SetParent(root.transform, false);
                    forbid.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                    Part(forbid, PrimitiveType.Cylinder, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                         new Vector3(radius * 1.2f, 0.02f, radius * 1.2f), new Color(0.9f, 0.1f, 0.12f));
                    Part(forbid, PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), Quaternion.Euler(0f, 0f, 45f),
                         new Vector3(radius * 1.15f, radius * 0.18f, 0.02f), new Color(0.9f, 0.1f, 0.12f));
                    break;
                default:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                         Vector3.one * radius * 2f, new Color(1f, 0.85f, 0.1f));
                    break;
            }
            return root;
        }

        /// <summary>
        /// 투석기 그림 하나를 지운다. 회색 그림(이름 끝 <c>-placeholder</c>)은 재질을 스스로 만들었으니 같이 지운다 —
        /// 안 지우면 적중·무리마다 재질이 쌓인다. 축제 키트의 재질은 에셋이라 지우면 안 된다 — 그래서 이름으로 가른다.
        /// 표적 그림·터짐 번쩍임·투석기가 모두 이것으로 지운다.
        /// </summary>
        public static void DestroyVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }
            if (visual.name.EndsWith("-placeholder"))
            {
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Destroy(renderer.sharedMaterial);
                }
            }
            Destroy(visual);
        }

        //  에디터 시험(재생 아님)에서는 Destroy가 안 먹고 에러를 남긴다.
        private static void Destroy(Object o)
        {
            if (o == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(o);
            }
            else
            {
                Object.DestroyImmediate(o);
            }
        }

        private static void Part(GameObject parent, PrimitiveType type, Vector3 at, Quaternion rotation,
                                 Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = LOPToonMaterials.Create(color);
        }
    }
}
