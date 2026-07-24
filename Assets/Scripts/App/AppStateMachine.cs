using GameFramework;
using VContainer.Unity;

namespace LOP
{
    //  앱-플로우 상태 머신(Root). 씬 페이즈(Boot/FrontEnd/InMatch)를 소유하고 씬 로드를 일원화한다.
    //  전이는 전부 외부 신호(EntranceScene / 매칭 FSM / 매치 종료)가 Fire로 구동한다.
    //  IStartable: VContainer가 앱 시작 시 Start()를 호출 → initState(Boot) 진입. base의 Start()가 그 역할을 겸한다.
    public class AppStateMachine : StateMachine<AppEvent>, IStartable
    {
        private readonly ISceneLoader sceneLoader;

        public AppStateMachine(ISceneLoader sceneLoader)
        {
            this.sceneLoader = sceneLoader;
        }

        public override IState<AppEvent> initState => new Boot(CreateFrontEnd);

        private FrontEnd CreateFrontEnd() => new FrontEnd(CreateInMatch, sceneLoader);
        private InMatch CreateInMatch() => new InMatch(CreateFrontEnd, sceneLoader);
    }
}
