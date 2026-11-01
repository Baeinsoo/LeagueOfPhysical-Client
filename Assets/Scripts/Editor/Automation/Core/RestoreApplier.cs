using System;

namespace LOP.EditorTools.Automation
{
    /// <summary>실행 끝에 단계가 바꾼 것을 처음 값으로(spec §4.2 끝). 없던 설정은 지운다.</summary>
    public static class RestoreApplier
    {
        public static void Apply(IAutomationDriver driver, RestoreEntry entry)
        {
            switch (entry.Kind)
            {
                case RestoreKinds.BoolPref:
                    if (entry.HadValue) driver.SetBoolPref(entry.Key, entry.Value == "true");
                    else driver.DeletePref(entry.Key);
                    break;
                case RestoreKinds.Environment:
                    if (entry.HadValue) driver.SetStringPref(entry.Key, entry.Value);
                    else driver.DeletePref(entry.Key);
                    driver.ReloadEnvironment();
                    break;
                case RestoreKinds.PlayStartScene:
                    driver.PlayModeStartScene = entry.Value;
                    break;
                case RestoreKinds.Touchscreen:
                    driver.RemoveSyntheticTouchscreen();
                    break;
                case RestoreKinds.KeyUp:
                    driver.SetKey(entry.Key, false, out _);
                    break;
                default:
                    throw new ArgumentException($"모르는 되돌리기 '{entry.Kind}'");
            }
        }
    }
}
