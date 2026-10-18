using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 투석기 표적이 맞으면 그 자리에서 터진다(2차 spec §4). 적중 사건을 듣고, 맞힌 표적만 0.06초 하얗게 번쩍·1.12배로 부풀었다가
    /// 조리법(<see cref="ArcheryHitFxRecipe"/>)대로 파티클·고리·불꽃을 뿜는다. <b>게임 시계는 건드리지 않는다</b> — 모두의 서버 틱이 같아서다.
    /// 남의 적중은 작고 옅게 터진다. 화면 흔들림은 내 적중이고 당기지 않을 때만. 키트에 연출 재질이 없으면 번쩍임·흔들림만 한다.
    /// </summary>
    public sealed class ArcheryCatapultHitFxView : IStartable, ILateTickable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryFestivalKit kit;
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CameraController cameraController;

        private readonly List<ArcheryTarget> scratch = new List<ArcheryTarget>();
        private readonly List<Ghost> ghosts = new List<Ghost>();
        private readonly List<RingFx> rings = new List<RingFx>();
        private readonly List<FireworkFx> fireworks = new List<FireworkFx>();
        private readonly Stack<GameObject> ringPool = new Stack<GameObject>();
        private readonly ParticleSystem[] channels = new ParticleSystem[4];
        private System.IDisposable subscription;
        private GameObject root;
        private Mesh shardMesh, quadMesh;
        private MaterialPropertyBlock block;

        //  키트에 흰·빨간 재질이 없을 때 쓸 한 장씩. 이 뷰가 만들었으니 이 뷰가 지운다.
        private Material ownWhite, ownRed;

        private float shakeStartedAt = -1f, shakeMeters, shakeSeconds;
        private bool shakeMine;

        //  맞힌 표적의 그림. 번쩍임이 끝나면 지우고(대부분), 종은 흔들리다, 금지물은 빨갛게 깜빡이며 떨어지다,
        //  로켓은 조금 더 솟다가(불꽃이 터질 때) 지운다.
        private sealed class Ghost
        {
            public GameObject Visual;
            public ArcheryHitFxRecipe Recipe;
            public Vector3 At;
            public Quaternion Rotation;   // 맞은 틱의 그림 회전 — 떠 있던 그림과 각이 이어진다
            public float Radius, StartedAt, Scale, Alpha;
            public bool Burst;
            public bool? ShameRed;   // 금지물 빨간 깜빡임의 마지막 상태 — 바뀔 때만 재질을 다시 꽂는다
            public (Renderer renderer, Material[] original)[] Renderers;
        }

        private sealed class RingFx { public ArcheryFxRing Ring; public Vector3 At; public float Radius, StartedAt, Scale, Alpha; public GameObject Quad; }

        private sealed class FireworkFx { public ArcheryFxFirework F; public Vector3 At; public float StartedAt, Scale, Alpha; public Color32 Color; }

        public ArcheryCatapultHitFxView(ArcheryCourse course, ArcheryWorld world, GameFramework.Runner.IRunner runner,
                                        ArcheryFestivalKit kit, LOP.MasterData.LOPMasterData md,
                                        ISubscriber<WorldEventBatchToC> batchSubscriber, IGameDataStore gameDataStore,
                                        GameFramework.World.EntityRegistry entityRegistry, CameraController cameraController)
        {
            this.course = course; this.world = world; this.runner = runner; this.kit = kit; this.md = md;
            this.batchSubscriber = batchSubscriber; this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry; this.cameraController = cameraController;
        }

        public void Start()
        {
            if (course.IsCatapult == false)
            {
                return;
            }
            subscription = batchSubscriber.Subscribe(OnBatch);
            root = new GameObject("ArcheryCatapultHitFx");
            block = new MaterialPropertyBlock();
            if (kit.HasFx)
            {
                shardMesh = TriangleMesh();
                quadMesh = QuadMesh();
                channels[(int)ArcheryFxChannel.Droplet] = Channel("droplet", kit.fxDot, ArcheryFxChannel.Droplet, mesh: false);
                channels[(int)ArcheryFxChannel.Shard] = Channel("shard", kit.fxSolid, ArcheryFxChannel.Shard, mesh: true);
                channels[(int)ArcheryFxChannel.Spark] = Channel("spark", kit.fxSpark, ArcheryFxChannel.Spark, mesh: false);
                channels[(int)ArcheryFxChannel.Confetti] = Channel("confetti", kit.fxSolid, ArcheryFxChannel.Confetti, mesh: false);
            }
        }

        private void OnBatch(WorldEventBatchToC msg)
        {
            if (runner?.tickUpdater == null)
            {
                return;
            }
            float interval = (float)runner.tickUpdater.interval;
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryHit) { continue; }
                var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                if (ArcheryCatapultHits.TryLocate(course, world.GameplayStartTick, interval,
                                                  hit, scratch, out var target, out var at) == false)
                {
                    continue;
                }
                string code = md.Tables.TbArcheryTarget.GetOrDefault(target.KindId)?.Code;
                var rotation = ArcheryCatapultLooks.RotationAt(target, code, hit.hitTick, interval);
                Play(code, target, at, rotation, mine: hit.shooterId == gameDataStore.userEntityId);
            }
        }

        /// <summary>재생 중 디버그 — 카메라 앞 35 m에 그 표적이 맞은 것처럼 터뜨린다. 기본은 내 적중, 거짓이면 남의 적중(작고 옅게·흔들림 없음).</summary>
        public void Preview(string code, bool mine = true)
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            var row = System.Linq.Enumerable.FirstOrDefault(md.Tables.TbArcheryTarget.DataList, x => x.Code == code);
            if (camera == null || row == null || root == null)
            {
                return;
            }
            Vector3 at = camera.transform.position + camera.transform.forward * 35f;
            Vector3 facing = -camera.transform.forward;
            var fake = new ArcheryTarget(0, 0, at, 0f, 0, row.Radius, row.Points, row.IsTrap, ArcheryTargetShape.Sphere, null,
                                         facing, 2.6f, string.Empty, 0f, 0f, isJackpot: row.IsJackpot, kindId: row.Id);
            Play(code, fake, at, ArcheryCatapultLooks.RotationAt(fake, code, 0, 0.02f), mine);
        }

        private void Play(string code, in ArcheryTarget target, Vector3 at, Quaternion rotation, bool mine)
        {
            if (root == null)   // 씬이 이미 정리된 뒤 적중 사건이 들어온 경우
            {
                return;
            }
            var recipe = ArcheryHitFxRecipe.For(code);
            var ghost = new Ghost
            {
                Recipe = recipe, At = at, Rotation = rotation, Radius = target.Radius, StartedAt = Time.time,
                Scale = ArcheryHitFxRecipe.ViewerScale(mine), Alpha = ArcheryHitFxRecipe.ViewerAlpha(mine),
            };
            ghost.Visual = kit.TryBuild(code, out var modeled) ? modeled : ArcheryCatapultLooks.BuildPlaceholder(code, target.Radius);
            ghost.Visual.transform.SetParent(root.transform, true);
            var renderers = ghost.Visual.GetComponentsInChildren<Renderer>(true);
            ghost.Renderers = new (Renderer, Material[])[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { ghost.Renderers[i] = (renderers[i], renderers[i].sharedMaterials); }
            SetAll(ghost, White());
            ghosts.Add(ghost);
            PlaceGhost(ghost, 0f);

            //  흔들림 여부는 매 프레임 다시 본다(당기기 시작하면 바로 멈춘다). 여기서는 누구 적중인지만 적어 둔다.
            //  다만 남의 적중이 내 흔들림을 도중에 끊지는 않게 한다.
            bool mineShaking = shakeStartedAt >= 0f && shakeMine && Time.time - shakeStartedAt < shakeSeconds;
            if (mine || mineShaking == false)
            {
                shakeStartedAt = Time.time;
                shakeMine = mine;
                shakeMeters = recipe.ShakeMeters;
                shakeSeconds = recipe.ShakeSeconds;
            }
        }

        public void LateTick()
        {
            if (root == null)
            {
                return;
            }
            float now = Time.time;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                float t = now - g.StartedAt;
                if (g.Burst == false && t >= ArcheryHitFxRecipe.FlashSeconds)
                {
                    g.Burst = true;
                    BurstAt(g);
                    Restore(g);
                }
                if (t >= KeepSeconds(g.Recipe))
                {
                    Restore(g);
                    ArcheryCatapultLooks.DestroyVisual(g.Visual);
                    ghosts.RemoveAt(i);
                    continue;
                }
                PlaceGhost(g, t);
            }
            UpdateRings(now);
            UpdateFireworks(now);
            UpdateShake(now);
        }

        //  그림이 남는 시간. 로켓은 불꽃이 하늘에서 터질 때까지 같이 솟는다.
        private static float KeepSeconds(ArcheryHitFxRecipe r)
        {
            if (r.Shame) { return ArcheryHitFxRecipe.ShameSeconds; }
            if (r.Swing) { return ArcheryHitFxRecipe.SwingSeconds; }
            if (r.Firework.HasValue) { return ArcheryHitFxRecipe.FlashSeconds + r.Firework.Value.Fuse; }
            return ArcheryHitFxRecipe.FlashSeconds;
        }

        private void PlaceGhost(Ghost g, float t)
        {
            if (g.Visual == null) { return; }
            //  번쩍임 동안 1에서 1.12배로 부풀고, 그 뒤엔 1배. 그림 자체는 남의 적중이어도 실제 크기다.
            g.Visual.transform.localScale = Vector3.one * ArcheryHitFxRecipe.FlashScaleAt(t);
            if (t < ArcheryHitFxRecipe.FlashSeconds)
            {
                g.Visual.transform.SetPositionAndRotation(g.At, g.Rotation);
                return;
            }
            float since = t - ArcheryHitFxRecipe.FlashSeconds;
            if (g.Recipe.Shame)
            {
                g.Visual.transform.SetPositionAndRotation(g.At + ArcheryHitFxRecipe.ShameOffset(since), g.Rotation);
                bool red = ArcheryHitFxRecipe.ShameIsRed(since);
                if (g.ShameRed != red)   // 빨강↔원래 재질이 바뀔 때만 다시 꽂는다 — 매 프레임 할 일이 아니다
                {
                    SetAll(g, red ? Red() : null);
                    g.ShameRed = red;
                }
            }
            else if (g.Recipe.Swing)
            {
                g.Visual.transform.SetPositionAndRotation(g.At, g.Rotation * Quaternion.AngleAxis(ArcheryHitFxRecipe.SwingDegrees(since), Vector3.forward));
            }
            else if (g.Recipe.Firework.HasValue)
            {
                //  불꽃과 같은 식으로 솟는다 — 터지는 자리가 로켓 끝과 맞는다.
                float climb = g.Recipe.Firework.Value.ClimbMps * Mathf.Min(since, g.Recipe.Firework.Value.Fuse);
                g.Visual.transform.SetPositionAndRotation(g.At + Vector3.up * climb, g.Rotation);
            }
        }

        private void BurstAt(Ghost g)
        {
            foreach (var b in g.Recipe.Bursts) { Emit(b, g.At, b.Palette, g.Scale, g.Alpha); }
            foreach (var ring in g.Recipe.Rings) { AddRing(ring, g.At, g.Radius, g.Scale, g.Alpha); }
            if (g.Recipe.Firework.HasValue)
            {
                var f = g.Recipe.Firework.Value;
                fireworks.Add(new FireworkFx
                {
                    F = f, At = g.At, StartedAt = Time.time, Scale = g.Scale, Alpha = g.Alpha,
                    Color = f.Colors[Random.Range(0, f.Colors.Length)],
                });
            }
        }

        //  scale은 크기와 퍼지는 폭(속도)에, alpha는 색의 진하기에 곱한다 — 남의 적중을 작고 옅게.
        private void Emit(in ArcheryFxBurst b, Vector3 at, Color32[] palette, float scale, float alpha)
        {
            var ps = channels[(int)b.Channel];
            if (ps == null || palette == null || palette.Length == 0) { return; }
            for (int i = 0; i < b.Count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (b.UpOnly) { dir.y = Mathf.Abs(dir.y); }
                var color = palette[Random.Range(0, palette.Length)];
                color.a = (byte)Mathf.RoundToInt(color.a * alpha);
                var p = new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = (dir * Random.Range(b.SpeedMin, b.SpeedMax) + Vector3.up * b.UpBoost) * scale,
                    startLifetime = Random.Range(b.LifeMin, b.LifeMax),
                    startSize = Random.Range(b.SizeMin, b.SizeMax) * scale,
                    startColor = color,
                    applyShapeToPosition = false,
                };
                if (b.Channel == ArcheryFxChannel.Shard)
                {
                    p.rotation3D = Random.insideUnitSphere * 360f;
                    p.angularVelocity3D = Random.insideUnitSphere * 720f;
                }
                else if (b.Channel == ArcheryFxChannel.Confetti)
                {
                    p.rotation = Random.Range(0f, 360f);
                    p.angularVelocity = Random.Range(-600f, 600f);
                }
                ps.Emit(p, 1);
            }
        }

        private void AddRing(in ArcheryFxRing ring, Vector3 at, float radius, float scale, float alpha)
        {
            if (quadMesh == null) { return; }
            var quad = ringPool.Count > 0 ? ringPool.Pop() : NewRingQuad();
            quad.SetActive(false);
            rings.Add(new RingFx { Ring = ring, At = at, Radius = radius, StartedAt = Time.time, Scale = scale, Alpha = alpha, Quad = quad });
        }

        private void UpdateRings(float now)
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i];
                float t = now - r.StartedAt;
                if (t > r.Ring.Delay + r.Ring.Seconds)
                {
                    r.Quad.SetActive(false);
                    ringPool.Push(r.Quad);
                    rings.RemoveAt(i);
                    continue;
                }
                bool on = r.Ring.At(t, r.Radius, out float radius, out float alpha);
                r.Quad.SetActive(on);
                if (on == false) { continue; }
                r.Quad.transform.position = r.At;
                if (camera != null) { r.Quad.transform.rotation = camera.transform.rotation; }   // 카메라를 본다
                r.Quad.transform.localScale = Vector3.one * (radius * r.Scale * 2f);
                Color c = r.Ring.Color;
                c.a = alpha * r.Alpha;
                block.SetColor("_Color", c);
                r.Quad.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
        }

        private void UpdateFireworks(float now)
        {
            var spark = channels[(int)ArcheryFxChannel.Spark];
            for (int i = fireworks.Count - 1; i >= 0; i--)
            {
                var fw = fireworks[i];
                float t = now - fw.StartedAt;
                Vector3 at = fw.At + Vector3.up * (fw.F.ClimbMps * Mathf.Min(t, fw.F.Fuse));
                if (t < fw.F.Fuse)
                {
                    //  솟는 꼬리 — 프레임마다 불티 하나.
                    if (spark != null)
                    {
                        var trail = fw.F.Trail;
                        trail.a = (byte)Mathf.RoundToInt(trail.a * fw.Alpha);
                        spark.Emit(new ParticleSystem.EmitParams
                        {
                            position = at, velocity = Vector3.down, startLifetime = 0.3f, startSize = 0.08f * fw.Scale,
                            startColor = trail, applyShapeToPosition = false,
                        }, 1);
                    }
                    continue;
                }
                //  하늘에서 크게 — 한 색 넷 + 흰색 하나, 흰 고리.
                Emit(fw.F.Sparks, at, new[] { fw.Color, fw.Color, fw.Color, fw.Color, (Color32)Color.white }, fw.Scale, fw.Alpha);
                AddRing(new ArcheryFxRing(0f, fw.F.RingSeconds, 1f, fw.F.RingGrow, new Color32(255, 255, 255, 255)),
                        at, fw.F.RingStart, fw.Scale, fw.Alpha);
                fireworks.RemoveAt(i);
            }
        }

        private void UpdateShake(float now)
        {
            //  남의 적중만 돌고 있으면 흔들림이 없다 — 이미 0인 ShakeOffset을 매 프레임 다시 쓸 일이 아니다.
            if (shakeStartedAt < 0f || cameraController == null || shakeMine == false)
            {
                return;
            }
            float elapsed = now - shakeStartedAt;
            bool drawing = entityRegistry.Get(gameDataStore.userEntityId)?.Get<ArcheryAim>()?.Drawing ?? false;
            cameraController.ShakeOffset = ArcheryHitShake.Allowed(shakeMine, drawing)
                ? ArcheryHitShake.Offset(elapsed, shakeMeters, shakeSeconds)
                : Vector3.zero;
            if (elapsed >= shakeSeconds)
            {
                cameraController.ShakeOffset = Vector3.zero;
                shakeStartedAt = -1f;
            }
        }

        //  null이면 원래 재질로.
        private static void SetAll(Ghost g, Material material)
        {
            foreach (var (renderer, original) in g.Renderers)
            {
                if (renderer == null) { continue; }
                if (material == null) { renderer.sharedMaterials = original; continue; }
                var m = new Material[original.Length];
                for (int i = 0; i < m.Length; i++) { m[i] = material; }
                renderer.sharedMaterials = m;
            }
        }

        //  원래 재질로 돌려놓는다 — 회색 도형은 지울 때 렌더러의 재질을 같이 지우는데, 흰·빨간 재질이 꽂힌 채면
        //  키트 에셋(또는 이 뷰의 흰·빨간 재질)을 지우게 된다.
        private static void Restore(Ghost g) => SetAll(g, null);

        private Material White()
        {
            if (kit.fxFlash != null) { return kit.fxFlash; }
            if (ownWhite == null) { ownWhite = LOPToonMaterials.Create(Color.white); }
            return ownWhite;
        }

        //  금지물의 빨간 깜빡임 — 키트가 비어도 깜빡인다(1차 그대로 유지).
        private Material Red()
        {
            if (kit.forbid != null) { return kit.forbid; }
            if (ownRed == null) { ownRed = LOPToonMaterials.Create(new Color(0.9f, 0.1f, 0.12f)); }
            return ownRed;
        }

        private ParticleSystem Channel(string name, Material material, ArcheryFxChannel channel, bool mesh)
        {
            var go = new GameObject("catapult-fx-" + name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = ArcheryHitFxRecipe.GravityOf(channel);
            main.maxParticles = 1500;
            main.startSpeed = 0f;
            main.startRotation3D = mesh;
            var emission = ps.emission;
            emission.enabled = false;   // Emit()으로만 낸다
            var shape = ps.shape;
            shape.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (mesh)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = shardMesh;
            }
            ps.Play();
            return ps;
        }

        private GameObject NewRingQuad()
        {
            var go = new GameObject("catapult-fx-ring");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kit.fxRing;
            return go;
        }

        //  조각 = 납작한 삼각형 하나(색은 정점 색 = 파티클 색). UV는 가운데라 Solid 모양이 그대로 칠한다.
        private static Mesh TriangleMesh()
        {
            var m = new Mesh { name = "catapult-fx-shard" };
            m.vertices = new[] { new Vector3(-0.5f, -0.3f, 0f), new Vector3(0.5f, -0.1f, 0f), new Vector3(-0.1f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
            m.colors = new[] { Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 1, 2 };
            m.RecalculateBounds();
            return m;
        }

        //  고리 = 한 변 1인 정사각형(지름 = 배율). 정점 색을 흰색으로 박아 둔다 — 셰이더가 정점 색 × _Color다.
        private static Mesh QuadMesh()
        {
            var m = new Mesh { name = "catapult-fx-quad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        public void Dispose()
        {
            subscription?.Dispose();
            foreach (var g in ghosts) { Restore(g); ArcheryCatapultLooks.DestroyVisual(g.Visual); }
            ghosts.Clear();
            rings.Clear();
            fireworks.Clear();
            ringPool.Clear();
            if (cameraController != null && shakeStartedAt >= 0f) { cameraController.ShakeOffset = Vector3.zero; }
            if (root != null) { Object.Destroy(root); root = null; }
            if (shardMesh != null) { Object.Destroy(shardMesh); shardMesh = null; }
            if (quadMesh != null) { Object.Destroy(quadMesh); quadMesh = null; }
            if (ownWhite != null) { Object.Destroy(ownWhite); ownWhite = null; }
            if (ownRed != null) { Object.Destroy(ownRed); ownRed = null; }
        }
    }
}
