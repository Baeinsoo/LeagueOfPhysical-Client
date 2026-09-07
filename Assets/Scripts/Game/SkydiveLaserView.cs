using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 레이저를 화면에 그린다. 서버에서 받는 것이 없다 — 판정과 <b>같은 식</b>에 같은 틱을 넣어
    /// 자세를 구하므로(<see cref="LaserGeometry"/>) 그림과 판정이 구조적으로 같은 자리에 있다.
    ///
    /// <para>씬에 미리 놓을 대상이 아니고 프레임마다 트랜스폼만 옮기므로 MonoBehaviour가 아니라
    /// 진입점이다(<see cref="FlappyChaserView"/>와 같은 이유). 맵 씬에 클라 전용 컴포넌트를 붙이면
    /// 같은 씬을 읽는 서버에서 missing script가 되어 씬 주입이 끊긴다.</para>
    /// </summary>
    public class SkydiveLaserView : ILateTickable, IDisposable
    {
        //  점멸 빔이 예고 없이 켜지면 피할 수 없다 — 피할 수 없는 것은 장애물이 아니라 주사위다.
        private const float TelegraphSeconds = 0.4f;

        private static readonly Color BeamColor = new Color(1f, 0.16f, 0.22f);

        private readonly GameFramework.Runner.IRunner runner;
        private readonly LaserField laserField;

        private readonly List<Transform> beams = new List<Transform>();
        private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        private GameObject root;
        private Material litMaterial;
        private Material telegraphMaterial;

        public SkydiveLaserView(GameFramework.Runner.IRunner runner, LaserField laserField)
        {
            this.runner = runner;
            this.laserField = laserField;
        }

        public void LateTick()
        {
            IReadOnlyList<Laser> lasers = laserField.All;
            if (lasers.Count == 0 || runner?.tickUpdater == null)
            {
                return;   // 맵이 아직 안 올라왔거나 러너가 아직 안 물렸다
            }

            EnsureBeams(lasers);

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;   // 아직 Run 전이라 틱 간격이 없다 — 나누면 각도가 NaN이 된다
            }

            //  내 캐릭터를 그리는 시각과 같은 값(한 틱 뒤). 예전엔 tickUpdater.tick을 썼는데
            //  그건 "다음에 계산할" 틱이라 몸보다 한 틱 앞이었다 — 빔이 몸보다 먼저 움직여 보였다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            //  켜짐/꺼짐은 틱 단위 사실이라 소수로 물을 것이 없다. 자세(각도)만 틱 사이를 담는다.
            long tick = (long)System.Math.Floor(renderTick);
            int ahead = Mathf.Max(1, Mathf.RoundToInt(
                TelegraphSeconds / Mathf.Max(0.001f, (float)interval)));

            for (int i = 0; i < lasers.Count; i++)
            {
                Laser laser = lasers[i];
                bool lit = LaserGeometry.Lit(laser, tick);
                bool telegraphing = lit == false && WillLightWithin(laser, tick, ahead);

                renderers[i].enabled = lit || telegraphing;
                if (renderers[i].enabled == false)
                {
                    continue;
                }
                renderers[i].sharedMaterial = lit ? litMaterial : telegraphMaterial;

                float angle = LaserGeometry.Angle(laser, (float)renderTick);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var pivot = new Vector3(laser.Pivot.X, laser.Pivot.Y, laser.Pivot.Z);

                //  큐브는 가운데가 원점이라 절반만큼 밀어야 피벗에서 뻗어 나간다.
                beams[i].position = pivot + direction * (laser.Length * 0.5f);
                beams[i].rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

                float thickness = laser.Radius * (lit ? 2f : 0.5f);
                beams[i].localScale = new Vector3(laser.Length, thickness, thickness);
            }
        }

        /// <summary>
        /// 앞으로 <paramref name="ahead"/>틱 안에 켜지나. 켜지기 전에 가는 선으로 예고하려고 쓴다 —
        /// 예고 없이 켜지는 점멸 빔은 피할 수 없고, 피할 수 없는 것은 장애물이 아니라 주사위다.
        /// </summary>
        public static bool WillLightWithin(in Laser laser, long tick, int ahead)
        {
            if (laser.Period <= 0 || laser.OnTicks >= laser.Period)
            {
                return false;   // 상시 점등이면 예고할 것이 없다
            }
            for (int i = 1; i <= ahead; i++)
            {
                if (LaserGeometry.Lit(laser, tick + i))
                {
                    return true;
                }
            }
            return false;
        }

        private void EnsureBeams(IReadOnlyList<Laser> lasers)
        {
            if (beams.Count == lasers.Count)
            {
                return;
            }

            Dispose();
            root = new GameObject("SkydiveLasers");

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                //  빔은 스스로 빛나는 것이라 조명을 받으면 각도에 따라 어두워져 오히려 안 읽힌다.
                litMaterial = new Material(shader) { color = BeamColor };
                telegraphMaterial = new Material(shader)
                {
                    color = new Color(BeamColor.r, BeamColor.g, BeamColor.b, 0.35f)
                };
            }

            for (int i = 0; i < lasers.Count; i++)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = $"Beam{i}";
                beam.transform.SetParent(root.transform, worldPositionStays: false);

                //  콜라이더가 붙으면 키네마틱 이동이 벽으로 인식해 레이저 위에 착지한다.
                var collider = beam.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.Destroy(collider);
                }

                var renderer = beam.GetComponent<MeshRenderer>();
                if (litMaterial != null)
                {
                    renderer.sharedMaterial = litMaterial;
                }

                beams.Add(beam.transform);
                renderers.Add(renderer);
            }
        }

        public void Dispose()
        {
            beams.Clear();
            renderers.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }
}
