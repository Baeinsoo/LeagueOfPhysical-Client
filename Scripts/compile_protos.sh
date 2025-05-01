#!/bin/bash

# 변수 설정
PROTOC="../Tools/Protobuf/protoc-28.2-win64/bin/protoc"
PROTO_PATH="../Protos"
INCLUDE_PATH="../Tools/Protobuf/protoc-28.2-win64/include"
OUT_PATH="../Assets/Scripts/Generated/Protobuf"
FILE_COUNT=0

# protoc 존재 확인
if [ ! -f "$PROTOC" ]; then
    echo "[ERROR] protoc compiler not found at $PROTOC"
    echo "Please ensure the protoc compiler is installed correctly."
    exit 1
fi

# PROTO_PATH 존재 확인
if [ ! -d "$PROTO_PATH" ]; then
    echo "[ERROR] Proto file directory not found at $PROTO_PATH"
    echo "Please ensure the directory exists and contains .proto files."
    exit 1
fi

# OUT_PATH 생성 (없는 경우)
mkdir -p "$OUT_PATH" || {
    echo "[ERROR] Unable to create output directory $OUT_PATH"
    exit 1
}

# 컴파일 시작
echo "Starting Proto compilation..."
shopt -s nullglob
for file in "$PROTO_PATH"/*.proto; do
    if [ -f "$file" ]; then
        echo "Compiling $(basename "$file")..."
        "$PROTOC" --proto_path="$PROTO_PATH" --proto_path="$INCLUDE_PATH" --csharp_out="$OUT_PATH" "$file"
        if [ $? -ne 0 ]; then
            echo "[ERROR] Failed to compile $(basename "$file")"
            exit 1
        fi
        ((FILE_COUNT++))
    fi
done
shopt -u nullglob

# 결과 출력
if [ "$FILE_COUNT" -eq 0 ]; then
    echo "[WARNING] No .proto files found in $PROTO_PATH"
else
    echo "Successfully compiled $FILE_COUNT .proto file(s)."
    echo "Proto compilation complete."
    echo "Generated files can be found in $OUT_PATH"
fi

echo
echo "Script execution complete."
