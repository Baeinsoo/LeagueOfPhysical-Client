using Google.Protobuf;
using LOP;

public sealed partial class EntitySnapsToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.EntitySnapsToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
