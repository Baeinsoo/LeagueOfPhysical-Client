using System;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 남의 몸을 옮기는 보간기(순서 0)와 카메라(3000) 뒤에 한 번 돌려 주는 발판.
    /// VContainer의 LateTick은 순서가 보장되지 않아, 거기서 몸·카메라를 읽으면 한 프레임 늦어 떨린다.
    /// 이름표(CharacterNameplate)와 같은 3100.
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class KnockbackLateUpdateDriver : MonoBehaviour
    {
        private Action onLateUpdate;

        public static KnockbackLateUpdateDriver Create(string name, Action onLateUpdate)
        {
            var go = new GameObject(name);
            var driver = go.AddComponent<KnockbackLateUpdateDriver>();
            driver.onLateUpdate = onLateUpdate;
            return driver;
        }

        private void LateUpdate()
        {
            onLateUpdate?.Invoke();
        }
    }
}
