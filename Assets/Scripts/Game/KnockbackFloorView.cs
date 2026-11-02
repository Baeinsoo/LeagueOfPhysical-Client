using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 칸 바닥 그림. 바닥은 계산(칸 목록)이라 콜라이더를 두지 않는다 — 칸은 보이기만 한다.
    /// 화면 시각의 정수 틱에서 무너진 칸은 숨기고, 곧 무너질 칸은 빨갛게 깜박인다.
    /// 위험 지대 방식에서는 칸이 사라지지 않고, 위험 지대 칸만 주황-빨강으로 칠한다.
    /// </summary>
    public class KnockbackFloorView : IStartable, ILateTickable, IDisposable
    {
        private const float Thickness = 0.4f;
        private const float Gap = 0.96f;
        // 무너지기 1초 전부터 깜박인다(틱 간격 0.02초 기준 50틱).
        private const long WarningTicks = 50;
        private const float BlinkSeconds = 0.15f;

        // 위험 지대 칸 색. 나중에 맵이 정할 값이라 한 곳에 모은다.
        private static readonly Color32 HazardLightColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);
        private static readonly Color32 HazardDarkColor = new Color32(0xE6, 0x4A, 0x14, 0xFF);

        public enum TileLook : byte { Hidden, Normal, WarningOn, WarningOff, Hazard }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private Material light;
        private Material dark;
        private Material warning;
        private Material hazardLight;
        private Material hazardDark;
        private GameObject root;

        private struct Tile
        {
            public GameObject go;
            public MeshRenderer renderer;
            public int x, z;
            public Material baseMaterial;
            public Material hazardMaterial;
            public TileLook look;
        }
        private Tile[] tiles = Array.Empty<Tile>();

        public KnockbackFloorView(GameFramework.Runner.IRunner runner, KnockbackWorld world)
        {
            this.runner = runner;
            this.world = world;
        }

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            light = new Material(shader) { color = new Color32(0xE9, 0xC4, 0x6A, 0xFF) };
            dark = new Material(shader) { color = new Color32(0xD4, 0xAE, 0x52, 0xFF) };
            warning = new Material(shader) { color = new Color32(0xE0, 0x3A, 0x30, 0xFF) };

            hazardLight = new Material(shader) { color = HazardLightColor };
            hazardDark = new Material(shader) { color = HazardDarkColor };

            root = new GameObject("KnockbackFloor");
            var floor = world.Floor;
            float s = floor.TileSize;
            var list = new List<Tile>(floor.Tiles.Count);
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
                var renderer = tile.GetComponent<MeshRenderer>();
                var baseMaterial = ((x + z) & 1) == 0 ? light : dark;
                renderer.sharedMaterial = baseMaterial;
                list.Add(new Tile { go = tile, renderer = renderer, x = x, z = z, baseMaterial = baseMaterial,
                    hazardMaterial = ((x + z) & 1) == 0 ? hazardLight : hazardDark, look = TileLook.Normal });
            }
            tiles = list.ToArray();
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null || tiles.Length == 0)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            // 칸은 틱 단위 사실이라 정수 틱으로 묻는다(소수 틱은 자세용).
            long tick = (long)Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            bool blinkOn = ((int)(Time.unscaledTime / BlinkSeconds) & 1) == 0;

            var floor = world.Floor;
            for (int i = 0; i < tiles.Length; i++)
            {
                ref var t = ref tiles[i];
                var look = Decide(floor.IsTileSolid(t.x, t.z, tick), floor.IsTileHazard(t.x, t.z, tick), floor.IsWarning(t.x, t.z, tick, WarningTicks), blinkOn);
                if (look == t.look)
                {
                    continue;
                }
                t.look = look;
                t.go.SetActive(look != TileLook.Hidden);
                t.renderer.sharedMaterial = look == TileLook.WarningOn ? warning
                    : look == TileLook.Hazard ? t.hazardMaterial
                    : t.baseMaterial;
            }
        }

        public static TileLook Decide(bool solid, bool hazard, bool warningSoon, bool blinkOn)
        {
            if (!solid) return TileLook.Hidden;
            if (hazard) return TileLook.Hazard;
            if (!warningSoon) return TileLook.Normal;
            return blinkOn ? TileLook.WarningOn : TileLook.WarningOff;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (light != null) UnityEngine.Object.Destroy(light);
            if (dark != null) UnityEngine.Object.Destroy(dark);
            if (warning != null) UnityEngine.Object.Destroy(warning);
            if (hazardLight != null) UnityEngine.Object.Destroy(hazardLight);
            if (hazardDark != null) UnityEngine.Object.Destroy(hazardDark);
        }
    }
}
