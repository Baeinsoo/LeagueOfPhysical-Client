using Google.Protobuf;
using LOP;

public sealed partial class InputSequnceToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.InputSequnceToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
