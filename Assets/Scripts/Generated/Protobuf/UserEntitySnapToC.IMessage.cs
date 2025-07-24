using Google.Protobuf;
using LOP;

public sealed partial class UserEntitySnapToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.UserEntitySnapToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
