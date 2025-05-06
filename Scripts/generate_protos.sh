#!/bin/bash
set -e  # 실패 시 중단

# generated 폴더 초기화
echo "Cleaning up generated folder..."
rm -rf ../Assets/Scripts/generated
mkdir -p ../Assets/Scripts/generated

./compile_protos.sh
./generate_imessage.sh
./generate_message_ids.sh
./generate_message_initializer.sh

echo "All proto-related scripts executed successfully."
