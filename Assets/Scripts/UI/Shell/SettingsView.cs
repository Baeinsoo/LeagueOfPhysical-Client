using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 설정 셸. 첫 항목 둘: "화면 흔들림"(spec §3 "흔들림 끄기" — 충돌·대시 흔들림 모두) · "상대 이름 보기"(spec §2).
    /// 값은 기기(PlayerPrefs)에 남고 다음 판부터 적용된다 — 경기 스코프가 지어질 때 읽는다(설정은 로비에서만 열린다).
    /// <para>셸 틀만 있는 UXML(ShellView.uxml)로 열려도 깨지지 않게 항목이 없으면 건너뛴다 — 셸 틀 시험이 그렇게 연다.</para>
    /// </summary>
    public class SettingsView : ShellView
    {
        private const string OnClass = "kit-toggle__opt--on";

        private Button _shakeOn;
        private Button _shakeOff;
        private Button _namesHide;
        private Button _namesShow;

        protected override string Title => "설정";

        public override void OnOpen()
        {
            base.OnOpen();

            _shakeOn = Root.Q<Button>("settings-shake-on");
            _shakeOff = Root.Q<Button>("settings-shake-off");
            _namesHide = Root.Q<Button>("settings-names-hide");
            _namesShow = Root.Q<Button>("settings-names-show");
            if (_shakeOn != null) _shakeOn.clicked += OnShakeOn;
            if (_shakeOff != null) _shakeOff.clicked += OnShakeOff;
            if (_namesHide != null) _namesHide.clicked += OnNamesHide;
            if (_namesShow != null) _namesShow.clicked += OnNamesShow;
            Refresh();
        }

        public override void OnClose()
        {
            if (_shakeOn != null) _shakeOn.clicked -= OnShakeOn;
            if (_shakeOff != null) _shakeOff.clicked -= OnShakeOff;
            if (_namesHide != null) _namesHide.clicked -= OnNamesHide;
            if (_namesShow != null) _namesShow.clicked -= OnNamesShow;
            base.OnClose();
        }

        private void OnShakeOn() => SetShakeOff(false);
        private void OnShakeOff() => SetShakeOff(true);
        private void OnNamesHide() => SetShowOpponentNames(false);
        private void OnNamesShow() => SetShowOpponentNames(true);

        //  누르는 즉시 디스크에 쓴다 — 폰에서 앱을 날리면(스와이프 종료) 저장 안 된 PlayerPrefs가 사라진다.
        internal void SetShakeOff(bool off)
        {
            FlappyDisplayPrefs.SetShakeOff(off);
            PlayerPrefs.Save();
            Refresh();
        }

        internal void SetShowOpponentNames(bool show)
        {
            FlappyDisplayPrefs.SetShowOpponentNames(show);
            PlayerPrefs.Save();
            Refresh();
        }

        private void Refresh()
        {
            bool shakeOff = FlappyDisplayPrefs.ShakeOff;
            bool showNames = FlappyDisplayPrefs.ShowOpponentNames;
            _shakeOn?.EnableInClassList(OnClass, !shakeOff);
            _shakeOff?.EnableInClassList(OnClass, shakeOff);
            _namesHide?.EnableInClassList(OnClass, !showNames);
            _namesShow?.EnableInClassList(OnClass, showNames);
        }
    }
}
