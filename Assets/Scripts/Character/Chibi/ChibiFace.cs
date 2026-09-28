using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 머리 앞에 곡면 얼굴 판을 붙이고 표정 칸을 고른다. 모델의 원래 눈 메시는 끈다.
    /// 반지름·중심·방향은 머리 뼈 로컬 좌표 — PolyOne 치비는 뼈 축이 돌아가 있다(앞 = −Y, 위 = −X).
    /// </summary>
    [ExecuteAlways]
    public class ChibiFace : MonoBehaviour
    {
        public ChibiExpression expression;
        public Material faceMaterial;
        public float radius = 0.175f;
        public Vector3 center = new Vector3(-0.12f, 0f, 0f);
        public Vector3 facing = new Vector3(0f, -1f, 0f);
        public Vector3 up = new Vector3(-1f, 0f, 0f);
        [Range(20f, 170f)] public float yawSpan = 100f;
        [Range(20f, 140f)] public float pitchSpan = 80f;
        public string hiddenEyeMesh = "SM_Chibi_Eye";

        private GameObject plate;
        private Mesh mesh;
        private Material instance;

        private void OnEnable()
        {
            Build();
        }

        //  OnValidate 안에서 오브젝트를 만들거나 부모를 바꾸면 유니티가 경고를 쏟는다 — 다음 틱으로 미룬다.
        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    Build();
                }
            };
#endif
        }

        //  끌 때는 숨기기만 한다(비활성화 도중 지우면 유니티가 거절한다). 정리는 OnDestroy에서.
        private void OnDisable()
        {
            if (plate != null)
            {
                plate.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (plate != null) { DestroyImmediate(plate); }
            if (mesh != null) { DestroyImmediate(mesh); }
            if (instance != null) { DestroyImmediate(instance); }
        }

        public void Build()
        {
            var animator = GetComponentInParent<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null || faceMaterial == null)
            {
                return;
            }
            foreach (var r in animator.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == hiddenEyeMesh) { r.enabled = false; }
            }
            if (plate == null)
            {
                var existing = head.Find("FacePlate");
                plate = existing != null ? existing.gameObject : new GameObject("FacePlate") { hideFlags = HideFlags.DontSave };
                if (plate.GetComponent<MeshFilter>() == null) { plate.AddComponent<MeshFilter>(); }
                if (plate.GetComponent<MeshRenderer>() == null) { plate.AddComponent<MeshRenderer>(); }
            }
            plate.transform.SetParent(head, false);
            plate.transform.localPosition = center;
            plate.transform.localRotation = Quaternion.LookRotation(facing.normalized, up.normalized);
            plate.transform.localScale = Vector3.one;
            if (mesh == null)
            {
                mesh = new Mesh { name = "FacePlate", hideFlags = HideFlags.DontSave };
            }
            FillPatch(mesh, radius, yawSpan * Mathf.Deg2Rad, pitchSpan * Mathf.Deg2Rad, 24, 18);
            plate.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (instance == null) { instance = new Material(faceMaterial) { hideFlags = HideFlags.DontSave }; }
            Vector4 st = ChibiFaceAtlas.CellST(expression);
            instance.SetTextureScale("_FaceMap", new Vector2(st.x, st.y));
            instance.SetTextureOffset("_FaceMap", new Vector2(st.z, st.w));
            var renderer = plate.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = true;
        }

        /// <summary>판을 다시 만들지 않고 표정 칸만 바꾼다 — 리액션이 매 프레임 불러도 싸다.</summary>
        public void SetExpression(ChibiExpression next)
        {
            if (expression == next && instance != null)
            {
                return;
            }
            expression = next;
            if (instance == null)
            {
                Build();
                return;
            }
            Vector4 st = ChibiFaceAtlas.CellST(expression);
            instance.SetTextureScale("_FaceMap", new Vector2(st.x, st.y));
            instance.SetTextureOffset("_FaceMap", new Vector2(st.z, st.w));
        }

        //  구의 앞(+Z) 조각. UV는 조각 전체에 0~1로 편다.
        private static void FillPatch(Mesh mesh, float r, float yaw, float pitch, int nx, int ny)
        {
            var verts = new Vector3[(nx + 1) * (ny + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int y = 0; y <= ny; y++)
            {
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx;
                    float v = y / (float)ny;
                    float a = (u - 0.5f) * yaw;
                    float b = (v - 0.5f) * pitch;
                    var n = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Cos(a) * Mathf.Cos(b));
                    int i = y * (nx + 1) + x;
                    verts[i] = n * r;
                    normals[i] = n;
                    uvs[i] = new Vector2(u, v);
                }
            }
            var tris = new int[nx * ny * 6];
            int t = 0;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    tris[t++] = i; tris[t++] = i + 1; tris[t++] = i + nx + 1;
                    tris[t++] = i + 1; tris[t++] = i + nx + 2; tris[t++] = i + nx + 1;
                }
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
        }
    }
}
