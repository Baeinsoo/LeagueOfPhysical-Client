using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class Application : MonoSingleton<Application>, IInitializable
    {
        public bool initialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoadRuntimeMethod()
        {
            instance.Initialize();
        }

        public void Initialize()
        {
            if (initialized == true)
            {
                return;
            }

            UnityEngine.Application.targetFrameRate = 30;      //  targetFrameRate�� �־�� �ϰ��� deltaTime == ���� ����� ���� ����

            initialized = true;
        }

        private void Start()
        {
            DontDestroyOnLoad(this);
        }
    }
}
