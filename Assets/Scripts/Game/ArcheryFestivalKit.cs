using System;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 투석기 사냥 축제 소품(표적 모델·재질·투석기·터지는 연출 재질). 게임 씬 스코프가 잡고 있어서 빌드에 따라간다 — 맵 씬(서버도 쓰는 원격 에셋)은
    /// 건드리지 않는다. 빈 키트(칸이 비었으면)는 아무것도 안 만들고, 그러면 화면이 회색 도형으로, 연출은 번쩍임만으로 돌아간다.
    /// 만드는 곳: 메뉴 LOP/Archery/Build Festival Kit.
    /// </summary>
    [CreateAssetMenu(fileName = "ArcheryFestivalKit", menuName = "LOP/Archery/Festival Kit")]
    public class ArcheryFestivalKit : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string code;
            public Mesh mesh;
            public Material[] materials;
            /// <summary>몸(꾸밈 슬롯을 뺀) 정점의 최대 거리 = 판정 반지름(±5%). 금지 표시 크기를 이것으로 잡는다.</summary>
            public float radius;
        }

        public Entry[] targets = Array.Empty<Entry>();
        public Material forbid;   // 쏘면 안 되는 것의 빨간 고리·빗금
        public Mesh catapultFrame, catapultArm;
        public Material[] catapultFrameMaterials, catapultArmMaterials;

        //  터지는 연출(LOP/FxParticle). 조각·색종이 = Solid, 물방울 = Dot, 불티 = Spark(더하기), 고리 = Ring, 번쩍임 = Flash(흰색 꽉 참).
        public Material fxSolid, fxDot, fxSpark, fxRing, fxFlash;

        public bool HasFx => fxSolid != null && fxDot != null && fxSpark != null && fxRing != null && fxFlash != null;

        /// <summary>가는 꾸밈 슬롯(풍선 줄·피냐타 술) — 크기 규칙에서 뺀다. 빌더가 만든 재질 이름은 "Festival_" + 슬롯.</summary>
        public static bool IsDecorSlot(string materialName)
            => materialName == "Festival_String" || materialName == "Festival_Tassel";

        public bool TryBuild(string code, out GameObject visual)
        {
            visual = null;
            if (targets == null)
            {
                return false;
            }
            foreach (var e in targets)
            {
                if (e.code != code || e.mesh == null)
                {
                    continue;
                }
                visual = Make("catapult-" + code, e.mesh, e.materials);
                if (ArcheryCatapultLooks.IsForbidden(code))
                {
                    AddForbid(visual.transform, e.radius, e.mesh.bounds.max.z);
                }
                return true;
            }
            return false;
        }

        public GameObject Make(string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        //  금지 표시는 모델에 굽지 않는다 — 생김새와 "쏘지 마"를 따로 고칠 수 있게. 속이 빈 고리 + 빗금이고,
        //  모델 앞면 바로 앞에 붙인다(둥근 풍선은 1차의 z = 0.12면 몸 안에 묻힌다). 표지판이라 크기 규칙 밖이다.
        private void AddForbid(Transform parent, float bodyRadius, float frontZ)
        {
            const int segments = 20;
            float radius = 0.85f * bodyRadius;   // 고리 띠의 가운데
            float band = 0.12f * bodyRadius;
            const float depth = 0.03f;

            var root = new GameObject("forbid");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, frontZ + 0.03f);

            float chord = 2f * radius * Mathf.Sin(Mathf.PI / segments) * 1.1f;
            for (int i = 0; i < segments; i++)
            {
                float deg = 360f * i / segments;
                float rad = deg * Mathf.Deg2Rad;
                ForbidBar(root.transform, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius,
                          deg + 90f, new Vector3(chord, band, depth));
            }
            //  사수 화면에서 왼쪽 위 → 오른쪽 아래(앞면이 +z라 화면 오른쪽이 로컬 −x다).
            ForbidBar(root.transform, Vector3.zero, 45f, new Vector3(radius * 2f, band, depth));
        }

        private void ForbidBar(Transform parent, Vector3 at, float rollDeg, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = at;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, rollDeg);
            bar.transform.localScale = scale;
            bar.GetComponent<Renderer>().sharedMaterial = forbid;
        }
    }
}
