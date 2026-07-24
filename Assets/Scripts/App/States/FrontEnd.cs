using GameFramework;
using System;

namespace LOP
{
    //  프론트엔드 페이즈(로비). 진입 시 Lobby 씬을 로드한다.
    public class FrontEnd : State<AppEvent>
    {
        private const string LobbySceneName = "Lobby";

        private readonly Func<InMatch> inMatch;
        private readonly ISceneLoader sceneLoader;

        public FrontEnd(Func<InMatch> inMatch, ISceneLoader sceneLoader)
        {
            this.inMatch = inMatch;
            this.sceneLoader = sceneLoader;
        }

        protected override void OnEnter() => sceneLoader.Load(LobbySceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.MatchFound => inMatch(),
                _ => this,
            };
        }
    }
}
