using System.Collections.Generic;

namespace LOP.EditorTools.Automation
{
    public static class StepCatalog
    {
        public static Dictionary<string, IStepHandler> CreateAll()
        {
            var handlers = new IStepHandler[]
            {
                new UiWaitStep(), new UiClickStep(), new UiReadStep(), new InputTapStep(),
                new WaitStateStep(), new PlayEnterStep(), new PlayExitStep(), new BootToLobbyStep(),
                new SelectGameStep(), new AutoFlapStep(), new SetEnvironmentStep(),
                new ScreenshotStep(), new LogStep(), new WaitStep(),
                new TouchStep(), new LongPressStep(), new DragStep(), new UiTypeStep(), new InputTextStep(),
            };
            var map = new Dictionary<string, IStepHandler>();
            foreach (var handler in handlers) map.Add(handler.Op, handler);
            return map;
        }
    }
}
