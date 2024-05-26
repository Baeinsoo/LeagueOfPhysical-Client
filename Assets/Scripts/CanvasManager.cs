using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [DontDestroyMonoSingleton]
    public class CanvasManager : MonoSingleton<CanvasManager>
    {
        [SerializeField] private Canvas popupCanvas;
        [SerializeField] private Canvas toastCanvas;
        [SerializeField] private Canvas systemCanvas;

        private void Start()
        {
            PopupManager.instance.popupCanvas = popupCanvas;
        }
    }
}
