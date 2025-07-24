using Google.Protobuf;
using LOP;

public sealed partial class ActionStartToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.ActionStartToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
