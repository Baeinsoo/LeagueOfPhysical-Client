using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>테마 소품 메시. 기본 도형을 합쳐 만든다 — 법선이 도형마다 바로 나온다.</summary>
    public static class DodgePropMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>
        /// 기본 도형 메시(구 반지름 0.5, 원기둥 반지름 0.5·높이 2). 내장 리소스의 "Sphere.fbx"는 반지름 1짜리
        /// 다른 메시(pSphere1)라 쓰지 않는다 — CreatePrimitive가 붙이는 메시를 한 번 빌려 둔다.
        /// </summary>
        public static Mesh Primitive(PrimitiveType type)
        {
            if (primitives.TryGetValue(type, out var cached) && cached != null)
            {
                return cached;
            }
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (UnityEngine.Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            primitives[type] = mesh;
            return mesh;
        }

        /// <summary>슬리퍼: 길이 1(z), 폭 0.45 타원 밑창 + 발등 끈. 원점은 밑창 가운데.</summary>
        public static Mesh Slipper()
        {
            var cyl = Primitive(PrimitiveType.Cylinder);   // 반지름 0.5, 높이 2(y)
            var parts = new[]
            {
                new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                                                               new Vector3(0.45f, 0.05f, 1f)) },
                // 끈: 누운 원기둥을 밑창 앞쪽에 반쯤 묻는다
                new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(new Vector3(0f, 0.06f, 0.15f),
                                                                               Quaternion.Euler(0f, 0f, 90f),
                                                                               new Vector3(0.16f, 0.2f, 0.16f)) },
            };
            var m = new Mesh { name = "DodgeSlipper" };
            m.CombineMeshes(parts, true, true);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>장독: 몸통 구 + 목. 모든 정점이 반지름 0.5 구 안이라 판정 원 밖으로 안 나간다. 긴 축 y.</summary>
        public static Mesh Jar()
        {
            var sphere = Primitive(PrimitiveType.Sphere);     // 반지름 0.5
            var cyl = Primitive(PrimitiveType.Cylinder);
            var parts = new[]
            {
                new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                                                                  new Vector3(0.94f, 0.94f, 0.94f)) },
                // 목: y 0.34~0.44, 반지름 0.18 → 가장 먼 점 √(0.44²+0.18²)=0.475
                new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(new Vector3(0f, 0.39f, 0f), Quaternion.identity,
                                                                               new Vector3(0.36f, 0.05f, 0.36f)) },
            };
            var m = new Mesh { name = "DodgeJar" };
            m.CombineMeshes(parts, true, true);
            m.RecalculateBounds();
            return m;
        }
    }
}
