#!/bin/bash

# 설정
PROTO_DIR="../Protos"
OUTPUT_FILE="../Assets/Scripts/Generated/MessageInitializer.cs"
NAMESPACE="LOP"

# message 이름 수집
MESSAGE_NAMES=()

# 각 .proto 파일을 개별적으로 처리
for proto_file in $(find "$PROTO_DIR" -name "*.proto"); do
    echo "Processing $proto_file for MessageInitializer..."
    
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
            MESSAGE_NAMES+=("${BASH_REMATCH[1]}")
            auto_generate=false
        else
            auto_generate=false
        fi
    done < "$proto_file"
done

# 중복 제거
UNIQUE_CLASSES=($(printf "%s\n" "${MESSAGE_NAMES[@]}" | sort -u))

# 파일 생성
echo "Generating MessageInitializer.cs with ${#UNIQUE_CLASSES[@]} message classes..."
cat > "$OUTPUT_FILE" <<EOF
using UnityEngine;

namespace $NAMESPACE
{
    public class MessageInitializer
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
EOF

for class in "${UNIQUE_CLASSES[@]}"; do
  echo "            MessageFactory.RegisterCreator(MessageIds.${class}, () => new ${class}());" >> "$OUTPUT_FILE"
done

cat >> "$OUTPUT_FILE" <<EOF
        }
    }
}
EOF

echo "MessageInitializer.cs generated at: $OUTPUT_FILE"