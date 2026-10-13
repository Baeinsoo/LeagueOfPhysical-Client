using LOP.UI;
using R3;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>로비 3D 무대 — 카메라를 고정하고 터널·조명·내 선수를 세운다. LobbyLifetimeScope가 만들어 주입한다.</summary>
    public sealed class LobbyStage : MonoBehaviour
    {
        [Inject] private MatchmakingViewModel _matchmaking;
        [Inject] private EconomyStore _economy;
        [Inject] private IUserDataStore _userData;
        [Inject] private CosmeticCatalog _catalog;

        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private GameObject _tunnel;
        private StageLights _lights;

        private void Start()
        {
            if (Camera.main != null) ConfigureCamera(Camera.main);

            _tunnel = LobbyTunnel.Build(transform, new TunnelLayout(), out var glow);
            var sun = FindFirstObjectByType<Light>();
            var lights = _lights = CreateLights(transform, glow, sun != null && sun.type == LightType.Directional ? sun : null);
            var lighting = gameObject.AddComponent<LobbyLighting>();
            lighting.Init(lights, _matchmaking.SelectedQueue.CurrentValue == QueueKind.Ranked);
            _matchmaking.SelectedQueue.Subscribe(q => lighting.SetRanked(q == QueueKind.Ranked)).AddTo(_subscriptions);

            var player = new GameObject("LobbyPlayer").AddComponent<LobbyPlayer>();
            player.transform.SetParent(transform, false);
            player.Init(_userData.user?.id, _catalog);
            _economy.Loadout.CombineLatest(_economy.Progress, (loadout, progress) => (loadout, progress))
                .Subscribe(x => player.SetLook(LobbyLook.From(x.loadout, _catalog, _userData.user?.displayName, x.progress?.level ?? 1)))
                .AddTo(_subscriptions);
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
            LobbyTunnel.DestroyMaterials(_tunnel);
            if (_lights?.FootPool != null) LobbyTunnel.DestroyMaterials(_lights.FootPool.gameObject);
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
        public static StageLights CreateLights(Transform parent, Renderer endGlow, Light existingSun)
        {
            var sun = existingSun;
            if (sun == null)
            {
                sun = new GameObject("StageSun").AddComponent<Light>();
                sun.transform.SetParent(parent, false);
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var end = new GameObject("EndPointLight").AddComponent<Light>();
            end.transform.SetParent(parent, false);
            end.type = LightType.Point;
            end.transform.position = new Vector3(0f, 2f, endGlow.transform.position.z - 1f);

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

            return new StageLights { Sun = sun, EndPoint = end, Spot = spot, EndGlow = endGlow, FootPool = poolRenderer };
        }
    }
}
