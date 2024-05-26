using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

namespace LOP
{
    public class LoginPopup : Popup
    {
        [SerializeField] private Button gpgsLoginButton;
        [SerializeField] private Button gamecenterLoginButton;
        [SerializeField] private Button guestLoginButton;

        public event Action onGuestLoginClick;

        private void Awake()
        {
            //gpgsLoginButton.onClick.AsObservable().Subscribe(_ => LoginService.instance.Login(LoginType.GooglePlayGame)).AddTo(this);
            //gamecenterLoginButton.onClick.AsObservable().Subscribe(_ => LoginService.instance.Login(LoginType.GameCenter)).AddTo(this);
            guestLoginButton.onClick.AsObservable().Subscribe(_ => onGuestLoginClick?.Invoke()).AddTo(this);
        }

        public override void Show()
        {
            base.Show();

            gpgsLoginButton.gameObject.SetActive(false);
            gamecenterLoginButton.gameObject.SetActive(false);

#if !UNITY_EDITOR && UNITY_ANDROID
        gpgsLoginButton.gameObject.SetActive(true);
#elif !UNITY_EDITOR && UNITY_IOS
        guestLoginButton.gameObject.SetActive(true);
#endif
            guestLoginButton.gameObject.SetActive(true);
        }
    }
}
