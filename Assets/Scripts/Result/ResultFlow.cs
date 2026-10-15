using LOP.UI;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 결과 씬 진행 — 무대·선수를 세우고, 하얀 덮개를 쓴 결과판을 연 뒤, 선수가 준비되면(최대 1초) 등장시킨다.
    /// 결과판이 반응 시각을 알려 주면 지휘자가 반응·장식을 건다. 확인 → 로비.
    /// </summary>
    public sealed class ResultFlow : MonoBehaviour
    {
        [Inject] private IMatchResultDataStore _results;   // 결과 씬 스코프에선 MatchResultSnapshot
        [Inject] private IWindowManager _windows;
        [Inject] private AppStateMachine _app;
        [Inject] private CosmeticCatalog _catalog;
        [Inject] private LOP.MasterData.LOPMasterData _masterData;

        private ResultDirector _director;
        private MatchResultView _view;
        private float _waited;
        private bool _started;
        private bool _left;

#if UNITY_EDITOR
        /// <summary>에디터 전용 — 가짜 결과 메뉴가 확인 때 로비로 돌아가는 길을 대신 건다(앱 상태가 로비 그대로라서).</summary>
        public static System.Action LeaveOverride;
#endif

        /// <summary>결과 자체가 없으면(직접 열기·두 번째 불러오기) 보여 줄 것이 없다 — 곧바로 로비. 등수 없는 판(보고 실패)은 "경기 종료"를 보여 준다.</summary>
        public static bool ShouldLeaveImmediately(MatchResult result) => result == null;

        private void Start()
        {
            var result = _results.result;
            if (ShouldLeaveImmediately(result))
            {
                Leave();
                return;
            }

            bool ranked = _masterData.Tables.TbQueue.GetOrDefault(result.queueId)?.HasVisibleRank == true;
            _director = new ResultDirector(PodiumSetCatalog.CreateDefault(), (parent, userId) =>
            {
                var player = new GameObject($"PodiumPlayer_{userId}").AddComponent<LobbyPlayer>();
                player.transform.SetParent(parent, false);
                player.Init(userId, _catalog);
                return player;
            });
            _director.Begin(transform, result, ranked);

            var cam = Camera.main;
            if (cam != null)
            {
                LobbyStage.ConfigureCamera(cam);
                cam.transform.position = _director.Stage.Camera.Position;
                cam.transform.LookAt(_director.Stage.Camera.LookAt);
            }

            _view = _windows.Open<MatchResultView>();
            _view.SetConfirmCallback(Leave);
            _view.SetReactCallback(_director.Reveal);
        }

        private void Update()
        {
            if (_director == null || _started) return;
            _waited += Time.deltaTime;
            if (!_director.Ready(_waited)) return;
            _started = true;
            _view?.BeginReveal();
        }

        private void Leave()
        {
            if (_left) return;
            _left = true;
#if UNITY_EDITOR
            if (LeaveOverride != null)
            {
                var leave = LeaveOverride;
                LeaveOverride = null;
                leave();
                return;
            }
#endif
            _app.Fire(AppEvent.ResultClosed);
        }
    }
}
