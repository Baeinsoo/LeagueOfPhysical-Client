using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    [DontDestroyMonoSingleton]
    public class Application : MonoSingleton<Application>
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoadRuntimeMethod()
        {
            Application.Instantiate();
        }

        protected override void Awake()
        {
            base.Awake();

            UnityEngine.Application.targetFrameRate = 30;      //  targetFrameRate이 있어야 일관된 deltaTime == 좋은 사용자 경험 가능
        }
    }
}
