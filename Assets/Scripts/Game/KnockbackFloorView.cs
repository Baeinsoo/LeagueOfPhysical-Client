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

        private static readonly Color32 CrackColor = new Color32(0x3A, 0x26, 0x10, 0xFF);
        // 금 무늬: (가운데에서 옮긴 x, z 비율, 길이 비율, 돌린 각도). 칸 위에 얇은 어두운 띠로 깐다.
        private static readonly (float x, float z, float len, float angle)[] CrackLayout =
        {
            (0f, 0f, 0.9f, 28f),
            (0.18f, -0.12f, 0.55f, -35f),
            (-0.2f, 0.16f, 0.5f, 70f),
        };
        private const float CrackWidth = 0.05f;

        // 위험 지대 칸 색. 나중에 맵이 정할 값이라 한 곳에 모은다.
        private static readonly Color32 HazardLightColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);
        // 곧 무너질 위험 지대 칸의 깜박임. 주황 위에서 빨강은 안 보여서 거의 흰색으로 한다.
        private static readonly Color32 CollapseWarningColor = new Color32(0xFF, 0xF4, 0xE0, 0xFF);
        private static readonly Color32 HazardDarkColor = new Color32(0xE6, 0x4A, 0x14, 0xFF);

        public enum TileLook : byte { Hidden, Normal, WarningOn, WarningOff, Hazard, CollapseOn }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackClientState state;
        private Material light;
        private Material dark;
        private Material warning;
        private Material collapseWarning;
        private Material hazardLight;
        private Material hazardDark;
        private Material crackMaterial;
        private GameObject root;
        private int appliedStateVersion = -1;

        private struct Tile
        {
            public GameObject go;
            public MeshRenderer renderer;
            public int x, z;
            public Material baseMaterial;
            public Material hazardMaterial;
            public TileLook look;
            public GameObject[] crackLines;   // 금이 처음 생길 때 한 번 만든다
            public bool crackShown;
        }
        private Tile[] tiles = Array.Empty<Tile>();

        public KnockbackFloorView(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.state = state;
        }

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            light = new Material(shader) { color = new Color32(0xE9, 0xC4, 0x6A, 0xFF) };
            dark = new Material(shader) { color = new Color32(0xD4, 0xAE, 0x52, 0xFF) };
            warning = new Material(shader) { color = new Color32(0xE0, 0x3A, 0x30, 0xFF) };

            collapseWarning = new Material(shader) { color = CollapseWarningColor };
            hazardLight = new Material(shader) { color = HazardLightColor };
            hazardDark = new Material(shader) { color = HazardDarkColor };
            crackMaterial = new Material(shader) { color = CrackColor };

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
            bool crackDirty = state != null && state.Version != appliedStateVersion;
            if (crackDirty) appliedStateVersion = state.Version;
            for (int i = 0; i < tiles.Length; i++)
            {
                ref var t = ref tiles[i];
                if (crackDirty) RefreshCrack(ref t, i);
                bool solidNow = floor.IsTileSolid(t.x, t.z, tick);
                if (t.crackLines != null) SetCrackActive(ref t, ShouldShowCrack(solidNow, state.CrackCountOf(i)));
                var look = Decide(solidNow, floor.IsTileHazard(t.x, t.z, tick), floor.IsWarning(t.x, t.z, tick, WarningTicks), blinkOn);
                if (look == t.look)
                {
                    continue;
                }
                t.look = look;
                t.go.SetActive(look != TileLook.Hidden);
                t.renderer.sharedMaterial = look == TileLook.WarningOn ? warning
                    : look == TileLook.CollapseOn ? collapseWarning
                    : look == TileLook.Hazard ? t.hazardMaterial
                    : t.baseMaterial;
            }
        }

        /// <summary>금 무늬를 보일까: 칸이 서 있고 금이 하나라도 있을 때. 무너진 칸은 칸째 사라진다.</summary>
        public static bool ShouldShowCrack(bool solid, int crackCount) => solid && crackCount > 0;

        private void RefreshCrack(ref Tile t, int index)
        {
            if (state.CrackCountOf(index) > 0 && t.crackLines == null) BuildCrackLines(ref t);
        }

        private void SetCrackActive(ref Tile t, bool active)
        {
            if (t.crackShown == active || t.crackLines == null) return;
            t.crackShown = active;
            for (int k = 0; k < t.crackLines.Length; k++) t.crackLines[k].SetActive(active);
        }

        private void BuildCrackLines(ref Tile t)
        {
            float s = world.Floor.TileSize * Gap;
            t.crackLines = new GameObject[CrackLayout.Length];
            for (int k = 0; k < CrackLayout.Length; k++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "Crack";
                UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
                line.transform.SetParent(t.go.transform, false);
                var l = CrackLayout[k];
                // 칸은 높이 Thickness로 늘어난 큐브라 윗면 바로 위에 얹는다(로컬 y 0.5 + 살짝).
                line.transform.localPosition = new Vector3(l.x, 0.5f + 0.005f / Thickness, l.z);
                line.transform.localRotation = Quaternion.Euler(0f, l.angle, 0f);
                // 부모 스케일(s, Thickness, s)을 나눠 월드 크기를 맞춘다.
                line.transform.localScale = new Vector3(CrackWidth / s, 0.01f / Thickness, l.len);
                line.GetComponent<MeshRenderer>().sharedMaterial = crackMaterial;
                line.SetActive(false);
                t.crackLines[k] = line;
            }
        }

        public static TileLook Decide(bool solid, bool hazard, bool warningSoon, bool blinkOn)
        {
            if (!solid) return TileLook.Hidden;
            // 위험 지대 칸이 곧 무너지는 경우(서든데스)만 여기서 경고가 겹친다.
            if (hazard) return warningSoon && blinkOn ? TileLook.CollapseOn : TileLook.Hazard;
            if (!warningSoon) return TileLook.Normal;
            return blinkOn ? TileLook.WarningOn : TileLook.WarningOff;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (light != null) UnityEngine.Object.Destroy(light);
            if (dark != null) UnityEngine.Object.Destroy(dark);
            if (warning != null) UnityEngine.Object.Destroy(warning);
            if (collapseWarning != null) UnityEngine.Object.Destroy(collapseWarning);
            if (hazardLight != null) UnityEngine.Object.Destroy(hazardLight);
            if (hazardDark != null) UnityEngine.Object.Destroy(hazardDark);
            if (crackMaterial != null) UnityEngine.Object.Destroy(crackMaterial);
        }
    }
}
