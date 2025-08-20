using Google.Protobuf;
using LOP;

public sealed partial class DamageEventToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.DamageEventToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
