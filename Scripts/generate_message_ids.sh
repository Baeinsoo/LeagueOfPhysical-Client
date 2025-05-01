#!/bin/bash

PROTO_DIR="../Protos"
OUTPUT_FILE="../Assets/Scripts/Generated/MessageIds.cs"
NAMESPACE="LOP"

# 기존 항목 유지용 딕셔너리 로드 (있다면)
declare -A EXISTING_MAP
declare -a USED_IDS=()

if [ -f "$OUTPUT_FILE" ]; then
    while IFS= read -r line; do
        if [[ $line =~ const[[:space:]]+ushort[[:space:]]+([A-Za-z0-9_]+)[[:space:]]*=[[:space:]]*([0-9]+) ]]; then
            EXISTING_MAP["${BASH_REMATCH[1]}"]="${BASH_REMATCH[2]}"
            USED_IDS+=("${BASH_REMATCH[2]}")
        fi
    done < "$OUTPUT_FILE"
fi

# 새 메시지 수집
declare -A MESSAGE_MAP
CURRENT_ID=1

# 각 .proto 파일을 개별적으로 처리
for proto_file in $(find "$PROTO_DIR" -name "*.proto"); do
    echo "Processing $proto_file for MessageIds..."
    
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
            MSG="${BASH_REMATCH[1]}"
            if [[ -n "${EXISTING_MAP[$MSG]}" ]]; then
                MESSAGE_MAP["$MSG"]="${EXISTING_MAP[$MSG]}"
            else
                # 중복된 ID를 피하도록 검사
                while printf "%s\n" "${USED_IDS[@]}" | grep -qx "$CURRENT_ID"; do
                    ((CURRENT_ID++))
                done
                MESSAGE_MAP["$MSG"]="$CURRENT_ID"
                USED_IDS+=("$CURRENT_ID")
                ((CURRENT_ID++))
            fi
            auto_generate=false
        else
            auto_generate=false
        fi
    done < "$proto_file"
done

# 정렬을 위해 이름과 번호 쌍으로 배열 생성
OUTPUT_LINES=()
for name in "${!MESSAGE_MAP[@]}"; do
    OUTPUT_LINES+=("${MESSAGE_MAP[$name]}:$name")
done

# 정렬
if [ ${#OUTPUT_LINES[@]} -gt 0 ]; then
    IFS=$'\n' SORTED=($(sort -n <<<"${OUTPUT_LINES[*]}"))
    unset IFS
else
    SORTED=()
fi

# 파일 작성
cat > "$OUTPUT_FILE" <<EOF

namespace $NAMESPACE
{
    public static class MessageIds
    {
EOF

# 정렬된 메시지 ID 쓰기
for entry in "${SORTED[@]}"; do
    ID="${entry%%:*}"
    NAME="${entry##*:}"
    printf "        public const ushort %-30s = %s;\n" "$NAME" "$ID" >> "$OUTPUT_FILE"
done

cat >> "$OUTPUT_FILE" <<EOF
    }
}
EOF

echo "MessageIds.cs generated at: $OUTPUT_FILE"