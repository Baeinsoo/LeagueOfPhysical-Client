using System;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 투석기 사냥 축제 소품(모델·재질). 게임 씬 스코프가 잡고 있어서 빌드에 따라간다 — 맵 씬(서버도 쓰는 원격 에셋)은
    /// 건드리지 않는다. 빈 키트(칸이 비었으면)는 아무것도 안 만들고, 그러면 화면이 회색 도형으로 돌아간다.
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
        }

        public Entry[] targets = Array.Empty<Entry>();
        public Material forbid;   // 쏘면 안 되는 것의 빨간 고리·빗금
        public Mesh shieldHalf, appleHalf, coin, catapultFrame, catapultArm;
        public Material[] shieldHalfMaterials, appleHalfMaterials, catapultFrameMaterials, catapultArmMaterials;
        public Material coinMaterial;

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
                if (code == "KingFlag" || code == "FlowerBasket")
                {
                    AddForbid(visual.transform);
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

        //  금지 표시는 모델에 굽지 않는다 — 생김새(깃발·바구니)와 "쏘지 마"를 따로 고칠 수 있게.
        //  속이 빈 고리 + 빗금이다. 꽉 찬 원판이면 앞에서 깃발·바구니가 가려져 무엇인지 안 읽힌다.
        //  지름 1.55 m라 반지름 0.85의 판정 구 안에 든다.
        private void AddForbid(Transform parent)
        {
            const int segments = 20;
            const float radius = 0.725f;   // 고리 띠의 가운데
            const float band = 0.1f;
            const float depth = 0.03f;

            var root = new GameObject("forbid");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.12f);

            //  이음매가 벌어지지 않게 마디를 조금 길게 잡는다.
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
