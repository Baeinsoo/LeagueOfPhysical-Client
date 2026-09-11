using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 서버가 알려 준 "사라진 것들". <b>월드가 아니라 여기</b>에 둔다 — 월드의 저장/복원에 넣으면
    /// 되감을 때 서버가 확정한 사실이 옛 값으로 되돌아가 먹힌 과녁이 되살아난다. 서버 확정 사실은
    /// 애초에 예측 대상이 아니므로 되감기 밖이 맞다.
    ///
    /// <para><b>과녁과 화살이 서로 다른 길로 온다.</b> 과녁은 <b>상태</b>(웨이브 번호 + 비트마스크)로
    /// 와서, 끊겼다 돌아온 사람도 다음 소식 한 번이면 지금 남은 과녁을 정확히 안다. 화살은
    /// <b>사건</b>으로 온다 — 3초면 사라지는 값이고, 재접속한 사람에게는 날아가던 남의 화살이
    /// 애초에 하나도 없다(남의 발사도 사건이라 그가 없던 동안의 화살은 그의 세계에 존재한 적이 없다).</para>
    /// </summary>
    public class ArcheryConsumed
    {
        private int stateWaveIndex = -1;
        private int consumedMask;

        private readonly HashSet<(string shooterId, long fireTick)> arrows
            = new HashSet<(string, long)>();

        /// <summary>서버가 알려 준 "지금 웨이브에서 먹힌 슬롯들".</summary>
        public void ApplyState(int waveIndex, int mask)
        {
            //  늦게 도착한 낡은 소식은 버린다. 새 웨이브의 과녁을 옛 마스크로 지우면
            //  멀쩡한 과녁이 화면에서 사라진다.
            if (waveIndex < stateWaveIndex)
            {
                return;
            }
            stateWaveIndex = waveIndex;
            consumedMask = mask;
        }

        /// <summary>
        /// 이 과녁이 사라졌나. <b>모르면 "살아 있다"로 답한다</b> — 소식이 아직 안 온 웨이브의 과녁을
        /// 미리 지우면 안 된다. 마스크가 0인 웨이브는 서버가 아예 안 보내므로 이것이 정상 경로다.
        /// </summary>
        public bool IsTargetGone(int waveIndex, int slotIndex)
        {
            return waveIndex == stateWaveIndex && (consumedMask & (1 << slotIndex)) != 0;
        }

        public void MarkArrow(string shooterId, long fireTick) => arrows.Add((shooterId, fireTick));
        public bool IsArrowGone(string shooterId, long fireTick) => arrows.Contains((shooterId, fireTick));

        /// <summary>수명이 다한 화살은 더 물어볼 일이 없다.</summary>
        public void ForgetArrowsBefore(long fireTick)
        {
            arrows.RemoveWhere(a => a.fireTick < fireTick);
        }
    }
}
