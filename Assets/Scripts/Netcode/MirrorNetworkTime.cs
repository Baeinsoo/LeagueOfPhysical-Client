using GameFramework.Netcode;

namespace LOP
{
    /// <summary>
    /// Mirror NetworkTime을 INetworkTime으로 위임하는 클라 구현.
    /// ServerNow=predictedTime−RTT/2(서버 현재 추정), PredictedTime=client-ahead(.predictedTime).
    /// </summary>
    public class MirrorNetworkTime : GameFramework.Netcode.INetworkTime
    {
        public double ServerNow => Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5;

        public double PredictedTime => Mirror.NetworkTime.predictedTime;

        public double Rtt => Mirror.NetworkTime.rtt;
    }
}
