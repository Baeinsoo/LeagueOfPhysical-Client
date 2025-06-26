#!/bin/bash

# Upload APK to S3 bucket
# Usage: ./upload-apk-s3.sh

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
APK_PATH="../Build/lop.apk"
S3_BUCKET="s3://lop-client"
APK_NAME="lop.apk"

echo -e "${YELLOW}Starting APK upload to S3...${NC}"

# Check if APK file exists
if [ ! -f "$APK_PATH" ]; then
    echo -e "${RED}Error: APK file not found at $APK_PATH${NC}"
    exit 1
fi

# Get file size for progress indication
FILE_SIZE=$(du -h "$APK_PATH" | cut -f1)
echo -e "${YELLOW}File size: $FILE_SIZE${NC}"

# Check AWS CLI is installed
if ! command -v aws &> /dev/null; then
    echo -e "${RED}Error: AWS CLI is not installed${NC}"
    exit 1
fi

# Check AWS credentials
if ! aws sts get-caller-identity &> /dev/null; then
    echo -e "${RED}Error: AWS credentials not configured or invalid${NC}"
    echo -e "${YELLOW}Please run: aws configure${NC}"
    exit 1
fi

# Upload to S3
echo -e "${YELLOW}Uploading $APK_PATH to $S3_BUCKET/$APK_NAME...${NC}"
if aws s3 cp "$APK_PATH" "$S3_BUCKET/$APK_NAME"; then
    echo -e "${GREEN}✅ Upload successful!${NC}"
    
    # Verify upload
    echo -e "${YELLOW}Verifying upload...${NC}"
    if aws s3 ls "$S3_BUCKET/$APK_NAME" &> /dev/null; then
        echo -e "${GREEN}✅ File verified in S3 bucket${NC}"
        
        # Show S3 object details
        aws s3 ls "$S3_BUCKET/$APK_NAME" --human-readable --summarize
        
        # Generate download URLs
        echo -e "${YELLOW}📥 Download URLs:${NC}"
        
        # Get bucket region for URL generation
        BUCKET_NAME="lop-client"
        REGION=$(aws configure get region)
        
        # Public URL (recommended for easy sharing)
        PUBLIC_URL="https://${BUCKET_NAME}.s3.${REGION}.amazonaws.com/${APK_NAME}"
        echo -e "${GREEN}🌐 Public URL (Recommended): ${PUBLIC_URL}${NC}"
        
        # Test public URL accessibility
        echo -e "${YELLOW}Testing public URL accessibility...${NC}"
        if curl -s -I "$PUBLIC_URL" | grep -q "HTTP/1.1 200"; then
            echo -e "${GREEN}✅ Public URL is accessible!${NC}"
        else
            echo -e "${RED}⚠️  Public URL test failed - check bucket permissions${NC}"
        fi
        
        # Generate presigned URL (backup option)
        echo -e "${YELLOW}Generating backup download URL (valid for 7 days)...${NC}"
        PRESIGNED_URL=$(aws s3 presign "$S3_BUCKET/$APK_NAME" --expires-in 604800)
        echo -e "${GREEN}🔒 Presigned URL (Backup): ${PRESIGNED_URL}${NC}"
        
        # S3 CLI path
        echo -e "${YELLOW}🛠️  S3 CLI Path: ${S3_BUCKET}/${APK_NAME}${NC}"
    else
        echo -e "${RED}❌ File verification failed${NC}"
        exit 1
    fi
else
    echo -e "${RED}❌ Upload failed${NC}"
    exit 1
fi

echo -e "${GREEN}🎉 APK deployment completed successfully!${NC}"

# 종료 대기
read -p "프로그램이 종료되었습니다. 창을 닫으려면 Enter 키를 누르세요." || true