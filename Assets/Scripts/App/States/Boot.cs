using GameFramework;
using System;

namespace LOP
{
    //  부트 페이즈. Entrance 씬은 앱 시작 시 이미 로드돼 있어 진입 시 로드할 씬이 없다.
    public class Boot : State<AppEvent>
    {
        private readonly Func<FrontEnd> frontEnd;

        public Boot(Func<FrontEnd> frontEnd)
        {
            this.frontEnd = frontEnd;
        }

        public override IState<AppEvent> GetNextState(AppEvent ev)
        {
            return ev switch
            {
                AppEvent.BootCompleted => frontEnd(),
                _ => this,
            };
        }
    }
}
