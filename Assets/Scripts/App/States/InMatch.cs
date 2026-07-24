using GameFramework;
using System;

namespace LOP
{
    //  인매치 페이즈. 진입 시 Room 씬을 로드한다(LOPGame이 additive로 얹힘).
    public class InMatch : State<AppEvent>
    {
        private const string RoomSceneName = "Room";

        private readonly Func<FrontEnd> frontEnd;
        private readonly ISceneLoader sceneLoader;

        public InMatch(Func<FrontEnd> frontEnd, ISceneLoader sceneLoader)
        {
            this.frontEnd = frontEnd;
            this.sceneLoader = sceneLoader;
        }

        protected override void OnEnter() => sceneLoader.Load(RoomSceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.MatchEnded => frontEnd(),
                _ => this,
            };
        }
    }
}
