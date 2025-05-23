using UnityEngine;

namespace LOP
{
    public class MessageInitializer
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            MessageFactory.RegisterCreator(MessageIds.EntitySnapsToC, () => new EntitySnapsToC());
            MessageFactory.RegisterCreator(MessageIds.GameInfoToC, () => new GameInfoToC());
            MessageFactory.RegisterCreator(MessageIds.GameInfoToS, () => new GameInfoToS());
            MessageFactory.RegisterCreator(MessageIds.InputSequenceToC, () => new InputSequenceToC());
            MessageFactory.RegisterCreator(MessageIds.PlayerInputToS, () => new PlayerInputToS());
        }
    }
}
