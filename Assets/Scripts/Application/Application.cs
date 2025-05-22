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

            UnityEngine.Application.targetFrameRate = 60;
        }
    }
}
