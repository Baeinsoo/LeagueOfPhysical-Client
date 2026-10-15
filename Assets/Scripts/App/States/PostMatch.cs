using GameFramework;
using System;

namespace LOP
{
    //  결과 단계. 진입 시 Result 씬을 단일로 불러온다 — 경기 씬이 내려가며 LOPRoom이 방 접속을 끊는다(방 세션 끝).
    public class PostMatch : State<AppEvent>
    {
        public const string ResultSceneName = "Result";

        private readonly Func<FrontEnd> frontEnd;
        private readonly ISceneLoader sceneLoader;

        public PostMatch(Func<FrontEnd> frontEnd, ISceneLoader sceneLoader)
        {
            this.frontEnd = frontEnd;
            this.sceneLoader = sceneLoader;
        }

        protected override void OnEnter() => sceneLoader.Load(ResultSceneName);

        public override IState<AppEvent> GetNextState(AppEvent ev) =>
            ev == AppEvent.ResultClosed ? frontEnd() : this;
    }
}
