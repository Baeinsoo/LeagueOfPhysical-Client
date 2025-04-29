using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        protected override void OnElapsedTimeUpdate()
        {
            base.OnElapsedTimeUpdate();

            elapsedTime = Mirror.NetworkTime.time;
        }
    }
}
