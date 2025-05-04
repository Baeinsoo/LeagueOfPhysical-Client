using Google.Protobuf;
using LOP;

public sealed partial class GameInfoToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.GameInfoToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
