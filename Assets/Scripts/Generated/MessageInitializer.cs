using UnityEngine;

namespace LOP
{
    public class MessageInitializer
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            MessageFactory.RegisterCreator(MessageIds.EntityStatesToC, () => new EntityStatesToC());
            MessageFactory.RegisterCreator(MessageIds.GameInfoToC, () => new GameInfoToC());
            MessageFactory.RegisterCreator(MessageIds.GameInfoToS, () => new GameInfoToS());
            MessageFactory.RegisterCreator(MessageIds.InputSequnceToC, () => new InputSequnceToC());
            MessageFactory.RegisterCreator(MessageIds.PlayerInputToS, () => new PlayerInputToS());
        }
    }
}
