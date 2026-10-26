using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 칸 바닥 그림. 바닥은 계산(칸 목록)이라 콜라이더를 두지 않는다 — 칸은 보이기만 한다.
    /// </summary>
    public class KnockbackFloorView : IStartable, IDisposable
    {
        private const float Thickness = 0.4f;
        private const float Gap = 0.96f;

        private readonly KnockbackWorld world;
        private Material light;
        private Material dark;
        private GameObject root;

        public KnockbackFloorView(KnockbackWorld world)
        {
            this.world = world;
        }

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            light = new Material(shader) { color = new Color32(0xE9, 0xC4, 0x6A, 0xFF) };
            dark = new Material(shader) { color = new Color32(0xD4, 0xAE, 0x52, 0xFF) };

            root = new GameObject("KnockbackFloor");
            var floor = world.Floor;
            float s = floor.TileSize;
            foreach (var (x, z) in floor.Tiles)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{z}";
                // 판정은 계산이라 콜라이더가 있으면 안 된다(몸 이동 충돌과 섞인다).
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
                tile.transform.SetParent(root.transform, false);
                var c = floor.TileCenter(x, z);
                tile.transform.position = new Vector3(c.x, c.y - Thickness * 0.5f, c.z);
                tile.transform.localScale = new Vector3(s * Gap, Thickness, s * Gap);
                tile.GetComponent<MeshRenderer>().sharedMaterial = ((x + z) & 1) == 0 ? light : dark;
            }
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (light != null) UnityEngine.Object.Destroy(light);
            if (dark != null) UnityEngine.Object.Destroy(dark);
        }
    }
}
