using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [DontDestroyMonoSingleton]
    public class CanvasManager : MonoSingleton<CanvasManager>
    {
        [SerializeField] private Canvas _popupCanvas;
        [SerializeField] private Canvas _loadingCanvas;
        [SerializeField] private Canvas _toastCanvas;
        [SerializeField] private Canvas _systemCanvas;

        public Canvas loadingCanvas => _loadingCanvas;

        private void Start()
        {
            PopupManager.instance.popupCanvas = _popupCanvas;
        }
    }
}
