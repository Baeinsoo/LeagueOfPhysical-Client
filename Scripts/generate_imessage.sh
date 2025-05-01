#!/bin/bash
# 설정
PROTO_DIR="../Protos"
OUTPUT_DIR="../Assets/Scripts/Generated/Protobuf"
NAMESPACE="LOP"

# 출력 디렉터리 생성
mkdir -p "$OUTPUT_DIR"

# 각 .proto 파일을 개별적으로 처리
for proto_file in $(find "$PROTO_DIR" -name "*.proto"); do
    echo "Processing $proto_file..."
    
    # 파일을 라인 단위로 읽기
    auto_generate=false
    while IFS= read -r line || [[ -n "$line" ]]; do
        # @auto_generate 주석 찾기
        if [[ "$line" =~ .*@auto_generate.* ]]; then
            auto_generate=true
            continue
        fi
        
        # 이전 라인이 @auto_generate 주석이었고, 현재 라인이 message 정의인 경우
        if [[ "$auto_generate" = true && "$line" =~ ^[[:space:]]*message[[:space:]]+([A-Za-z0-9_]+) ]]; then
            CLASS_NAME="${BASH_REMATCH[1]}"
            FILE_NAME="${OUTPUT_DIR}/${CLASS_NAME}.IMessage.cs"
            echo "  Generating $FILE_NAME..."
            cat > "$FILE_NAME" <<EOF
using Google.Protobuf;
using $NAMESPACE;

public sealed partial class $CLASS_NAME : GameFramework.IMessage
{
    public ushort messageId => MessageIds.$CLASS_NAME;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
EOF
            auto_generate=false
        else
            auto_generate=false
        fi
    done < "$proto_file"
done

echo "IMessage generation complete."