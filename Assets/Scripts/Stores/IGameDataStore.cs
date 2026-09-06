using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IGameDataStore : IDataStore
    {
        GameInfo gameInfo { get; set; }

        /// <summary>
        /// <b>이 판에서 서버가 나에게 배정한 엔티티 id — 정체다.</b> `GameInfoToC`를 받는 순간
        /// 채워지고, 내가 잡혀 몸이 사라져도 <b>그대로 남는다</b>(그 판에서 내가 그 엔티티였다는
        /// 사실은 안 변한다).
        ///
        /// <para>"지금 내 몸이 있는가"는 이 값이 아니라 <see cref="IPlayerContext.entityId"/>가
        /// 답한다. 둘은 같은 값을 담지만 수명이 다르다 — 스폰 <b>전부터</b> 유효한 쪽이 이것이고,
        /// 몸이 <b>있는 동안만</b> 유효한 쪽이 저것이다. 섞어 쓰지 말 것.</para>
        /// </summary>
        string userEntityId { get; set; }
    }
}
