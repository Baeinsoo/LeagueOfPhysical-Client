using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        private const double SMOOTH_TIME = 0.05;
        private const double MAX_DELTA = 0.5;

        private double elapsedTimeVelocity;

        protected override void OnElapsedTimeUpdate()
        {
            double dt = Mirror.NetworkTime.time - elapsedTime;

            if (Math.Abs(dt) > MAX_DELTA)
            {
                elapsedTime = Mirror.NetworkTime.time;
                elapsedTimeVelocity = 0;
                return;
            }

            double omega = 2.0 / SMOOTH_TIME;
            double x = omega * UnityEngine.Time.deltaTime;
            double exp = 1.0 / (1.0 + x + 0.48 * x * x + 0.235 * x * x * x);

            double change = dt;
            double temp = (elapsedTimeVelocity + omega * change) * UnityEngine.Time.deltaTime;
            elapsedTimeVelocity = (elapsedTimeVelocity - omega * temp) * exp;
            elapsedTime = elapsedTime + (change + temp) * exp;
        }
    }
}
