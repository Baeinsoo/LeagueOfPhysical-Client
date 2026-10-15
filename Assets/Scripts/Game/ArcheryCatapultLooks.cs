using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 슬라이스 1의 회색 표적 — 축제 표적을 색만 다른 기본 도형으로 그린다(spec §4 슬라이스 1).
    /// 슬라이스 2에서 축제 키트(ArcheryFestivalKit)가 있으면 그쪽 모델을 먼저 쓰고, 없을 때 이것으로 돌아온다.
    /// 크기는 판정 구(반지름) 안에 들어가게 잡는다 — 보이는 크기가 곧 맞는 크기다.
    /// </summary>
    public static class ArcheryCatapultLooks
    {
        public static bool IsFestival(string code)
            => code == "Shield" || code == "Helmet" || code == "Apple" || code == "Grail"
            || code == "KingFlag" || code == "FlowerBasket";

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
                case "Shield":   // 납작한 원판(사수를 향한다)
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                         new Vector3(radius * 2f, 0.08f, radius * 2f), new Color(0.55f, 0.36f, 0.2f));
                    break;
                case "Helmet":   // 반구에 가까운 납작한 공
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                         new Vector3(radius * 2f, radius * 1.4f, radius * 2f), new Color(0.62f, 0.64f, 0.68f));
                    break;
                case "Apple":
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                         Vector3.one * radius * 2f, new Color(0.85f, 0.15f, 0.12f));
                    break;
                case "Grail":    // 금색 공
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                         Vector3.one * radius * 2f, new Color(1f, 0.8f, 0.15f));
                    break;
                case "KingFlag":
                case "FlowerBasket":   // 흰 판 + 빨간 금지 표시(고리와 빗금)
                    Part(root, PrimitiveType.Cube, Vector3.zero, Quaternion.identity,
                         new Vector3(radius * 1.3f, radius * 1.3f, 0.06f), Color.white);
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
