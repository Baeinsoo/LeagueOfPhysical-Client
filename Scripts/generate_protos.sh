#!/bin/bash
set -e  # 실패 시 중단

./compile_protos.sh
./generate_imessage.sh
./generate_message_ids.sh
./generate_message_initializer.sh

echo "All proto-related scripts executed successfully."
