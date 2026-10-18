using LOP.UI;
using R3;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>로비 3D 무대 — 카메라를 고정하고 터널·조명·내 선수를 세운다. LobbyLifetimeScope가 만들어 주입한다.</summary>
    public sealed class LobbyStage : MonoBehaviour, IRunOutStage
    {
        [Inject] private MatchmakingViewModel _matchmaking;
        [Inject] private EconomyStore _economy;
        [Inject] private CharacterAppearanceStore _appearance;
        [Inject] private IUserDataStore _userData;
        [Inject] private CosmeticCatalog _catalog;
        [Inject] private AppearanceColorPalette _palette;
        [Inject] private RunOutDirector _runOut;

        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private GameObject _tunnel;
        private StageLights _lights;
        private StageRig _rig;
        private LobbyPlayer _player;
        private StageEffects _effects;
        private Camera _cam;
        private StageCameraPose _runStart;

        private void Start()
        {
            var cam = _cam = Camera.main;
            if (cam != null) ConfigureCamera(cam);

            var layout = new TunnelLayout();
            _tunnel = LobbyTunnel.Build(transform, layout, out var arena);
            var sun = FindFirstObjectByType<Light>();
            var lights = _lights = CreateLights(transform, arena, sun != null && sun.type == LightType.Directional ? sun : null);
            bool ranked = _matchmaking.SelectedQueue.CurrentValue == QueueKind.Ranked;
            var lighting = gameObject.AddComponent<LobbyLighting>();
            lighting.Init(lights, ranked);

            _rig = StageRig.Build(transform, layout);
            _rig.Signs = StageSigns.Build(_rig.transform, layout);
            _rig.Crowd = CrowdAmbience.Build(_rig.transform);
            _matchmaking.RankCard.Subscribe(card => _rig.Signs.SetRank(card)).AddTo(_subscriptions);
            var effects = _effects = gameObject.AddComponent<StageEffects>();
            effects.Init(_rig, lighting, cam, ranked);
            _matchmaking.SelectedQueue.Subscribe(q =>
            {
                bool r = q == QueueKind.Ranked;
                lighting.SetRanked(r);
                effects.SetRanked(r);
            }).AddTo(_subscriptions);

            var player = _player = new GameObject("LobbyPlayer").AddComponent<LobbyPlayer>();
            player.transform.SetParent(transform, false);
            player.Init(_catalog, _palette);
            _economy.Loadout.CombineLatest(_economy.Progress, _appearance.Current, (loadout, progress, appearance) => (loadout, progress, appearance))
                .Subscribe(x => player.SetLook(LobbyLook.From(x.loadout, x.appearance, _catalog, _userData.user?.displayName, x.progress?.level ?? 1)))
                .AddTo(_subscriptions);
            _runOut?.Attach(this);
        }

        private void OnDestroy()
        {
            _runOut?.Detach(this);
            _subscriptions.Dispose();
            LobbyTunnel.DestroyMaterials(_tunnel);
            if (_lights?.FootPool != null) LobbyTunnel.DestroyMaterials(_lights.FootPool.gameObject);
            if (_rig != null) LobbyTunnel.DestroyMaterials(_rig.gameObject);
        }

        public void BeginRunOut()
        {
            if (_cam != null) _runStart = new StageCameraPose(_cam.transform.position, _cam.transform.position + _cam.transform.forward * 10f);
            if (_effects != null) _effects.CameraOverride = true;
        }

        public void PoseRunOut(RunOutPose pose, float t)
        {
            if (_player != null) _player.PoseRunOut(pose.PlayerZ, pose.PlayerYaw, pose.Running);
            if (_cam == null) return;
            var c = RunOutCamera.At(_runStart, pose.PlayerZ, t);
            _cam.transform.position = c.Position;
            _cam.transform.LookAt(c.LookAt);
        }

        public void ResetRunOut()
        {
            if (_player != null) _player.ResetRunOut();
            if (_effects != null) _effects.CameraOverride = false;
        }

        /// <summary>고정 카메라 — 선수는 화면 가운데 아래, 터널 소실점은 머리 위(spec §5 시작값).</summary>
        public static void ConfigureCamera(Camera cam)
        {
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.transform.position = new Vector3(0f, 1.2f, 0f);
            cam.transform.LookAt(new Vector3(0f, 1.0f, 10f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#1F1D4A", out var bg);
            cam.backgroundColor = bg;
        }

        /// <summary>방향광(씬에 있으면 그것)·끝 빛 점광·발밑 스포트라이트를 만든다.</summary>
        public static StageLights CreateLights(Transform parent, Renderer arena, Light existingSun)
        {
            var sun = existingSun;
            if (sun == null)
            {
                sun = new GameObject("StageSun").AddComponent<Light>();
                sun.transform.SetParent(parent, false);
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.shadows = LightShadows.Soft;   // Lobby 씬의 해와 같게 — 미리보기가 실행 화면과 같은 그림자를 보도록
            }

            var end = new GameObject("EndPointLight").AddComponent<Light>();
            end.transform.SetParent(parent, false);
            end.type = LightType.Point;
            end.transform.position = new Vector3(0f, 2f, new TunnelLayout().Length + 1f);   // 출구 바로 너머

            var spot = new GameObject("FootSpot").AddComponent<Light>();
            spot.transform.SetParent(parent, false);
            spot.type = LightType.Spot;
            spot.transform.position = LobbyPlayer.StandPosition + new Vector3(0f, 3.5f, 0f);
            spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            spot.range = 6f;
            ColorUtility.TryParseHtmlString("#FFF8E8", out var cream);
            spot.color = cream;

            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "FootPool";
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.transform.SetParent(parent, false);
            pool.transform.position = LobbyPlayer.StandPosition + new Vector3(0f, 0.025f, 0f);
            pool.transform.localScale = new Vector3(1.6f, 0.005f, 1.6f);
            var poolRenderer = pool.GetComponent<Renderer>();
            poolRenderer.sharedMaterial = LobbyTunnel.MakeGlowMaterial("#FFF8E8");
            poolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return new StageLights { Sun = sun, EndPoint = end, Spot = spot, EndGlow = arena, FootPool = poolRenderer };
        }
    }
}
