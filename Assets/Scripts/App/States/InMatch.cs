using GameFramework;
using System;

namespace LOP
{
    //  인매치 페이즈. 진입 시 Room 씬을 로드한다(LOPGame이 additive로 얹힘).
    public class InMatch : State<AppEvent>
    {
        private const string RoomSceneName = "Room";

        private readonly Func<FrontEnd> frontEnd;
        private readonly Func<PostMatch> postMatch;
        private readonly ISceneLoader sceneLoader;
        private readonly IMatchResultDataStore results;

        public InMatch(Func<FrontEnd> frontEnd, Func<PostMatch> postMatch, ISceneLoader sceneLoader, IMatchResultDataStore results)
        {
            this.frontEnd = frontEnd;
            this.postMatch = postMatch;
            this.sceneLoader = sceneLoader;
            this.results = results;
        }

        protected override void OnEnter() => sceneLoader.Load(RoomSceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                //  결과 메시지를 받은 판만 결과 단계로 — 접속 오류로 끝난 판(LOPRoom catch)은 결과가 없다.
                AppEvent.MatchEnded => results.result != null ? postMatch() : frontEnd(),
                AppEvent.MatchLeft => frontEnd(),
                _ => this,
            };
        }
    }
}
