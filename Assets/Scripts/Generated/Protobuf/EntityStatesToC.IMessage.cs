using Google.Protobuf;
using LOP;

public sealed partial class EntityStatesToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.EntityStatesToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
