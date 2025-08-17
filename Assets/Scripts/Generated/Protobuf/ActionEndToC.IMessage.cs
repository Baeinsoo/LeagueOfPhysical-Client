using Google.Protobuf;
using LOP;

public sealed partial class ActionEndToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.ActionEndToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
