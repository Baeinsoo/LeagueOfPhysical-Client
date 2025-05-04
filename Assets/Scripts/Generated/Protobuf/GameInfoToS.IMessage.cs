using Google.Protobuf;
using LOP;

public sealed partial class GameInfoToS : GameFramework.IMessage
{
    public ushort messageId => MessageIds.GameInfoToS;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
