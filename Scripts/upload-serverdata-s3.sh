#!/bin/bash

# Upload ServerData to S3 bucket
# Usage: ./upload-serverdata-s3.sh

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SERVERDATA_PATH="../ServerData"
S3_BUCKET="s3://lop-assets"
S3_PREFIX="dev"

echo -e "${YELLOW}Starting ServerData upload to S3...${NC}"

# Check if ServerData folder exists
if [ ! -d "$SERVERDATA_PATH" ]; then
    echo -e "${RED}Error: ServerData folder not found at $SERVERDATA_PATH${NC}"
    exit 1
fi

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

# Get folder size for progress indication
echo -e "${YELLOW}Calculating folder size...${NC}"
if command -v du &> /dev/null; then
    FOLDER_SIZE=$(du -sh "$SERVERDATA_PATH" | cut -f1)
    echo -e "${YELLOW}Total size: $FOLDER_SIZE${NC}"
fi

# Count files to upload
FILE_COUNT=$(find "$SERVERDATA_PATH" -type f | wc -l)
echo -e "${YELLOW}Files to upload: $FILE_COUNT${NC}"

# Show platforms to be uploaded
echo -e "${BLUE}Platforms found:${NC}"
for platform in $(ls "$SERVERDATA_PATH"); do
    if [ -d "$SERVERDATA_PATH/$platform" ]; then
        platform_files=$(find "$SERVERDATA_PATH/$platform" -type f | wc -l)
        platform_size=$(du -sh "$SERVERDATA_PATH/$platform" 2>/dev/null | cut -f1 || echo "Unknown")
        echo -e "${BLUE}  📱 $platform: $platform_files files ($platform_size)${NC}"
    fi
done

echo ""
echo -e "${YELLOW}Uploading to: ${S3_BUCKET}/${S3_PREFIX}/...${NC}"

# Upload with sync (preserves folder structure and is efficient)
if aws s3 sync "$SERVERDATA_PATH" "$S3_BUCKET/$S3_PREFIX" --delete; then
    echo -e "${GREEN}✅ Upload successful!${NC}"
    
    # Verify upload by listing uploaded content
    echo -e "${YELLOW}Verifying upload...${NC}"
    if aws s3 ls "$S3_BUCKET/$S3_PREFIX/" --recursive | head -10; then
        echo -e "${GREEN}✅ Files verified in S3 bucket${NC}"
        
        # Show summary
        echo -e "${YELLOW}📊 Upload Summary:${NC}"
        UPLOADED_COUNT=$(aws s3 ls "$S3_BUCKET/$S3_PREFIX/" --recursive | wc -l)
        echo -e "${GREEN}📁 Total files uploaded: $UPLOADED_COUNT${NC}"
        
        # Generate URLs for each platform
        echo -e "${YELLOW}📥 Platform URLs:${NC}"
        
        BUCKET_NAME="lop-assets"
        REGION=$(aws configure get region)
        
        for platform in $(ls "$SERVERDATA_PATH"); do
            if [ -d "$SERVERDATA_PATH/$platform" ]; then
                PLATFORM_URL="https://${BUCKET_NAME}.s3.${REGION}.amazonaws.com/${S3_PREFIX}/${platform}/"
                echo -e "${GREEN}🌐 ${platform}: ${PLATFORM_URL}${NC}"
            fi
        done
        
        # Base dev folder URL
        DEV_URL="https://${BUCKET_NAME}.s3.${REGION}.amazonaws.com/${S3_PREFIX}/"
        echo -e "${BLUE}🛠️  Dev Folder: ${DEV_URL}${NC}"
        
        # S3 CLI path
        echo -e "${YELLOW}🛠️  S3 CLI Path: ${S3_BUCKET}/${S3_PREFIX}/{{platform}}/{{file}}${NC}"
        
        # Test accessibility (check if bucket allows public access)
        echo -e "${YELLOW}Testing public URL accessibility...${NC}"
        if curl -s -I "$DEV_URL" | grep -q "HTTP/1.1 200\|HTTP/2 200"; then
            echo -e "${GREEN}✅ Public URL is accessible!${NC}"
        else
            echo -e "${RED}⚠️  Public URL test failed - bucket may be private${NC}"
            echo -e "${YELLOW}💡 Consider using presigned URLs for private access if needed${NC}"
        fi
        
    else
        echo -e "${RED}❌ File verification failed${NC}"
        exit 1
    fi
else
    echo -e "${RED}❌ Upload failed${NC}"
    exit 1
fi

echo -e "${GREEN}🎉 ServerData deployment completed successfully!${NC}"
echo -e "${BLUE}💡 Note: Use 'aws s3 sync' with --delete to keep S3 in sync with local changes${NC}"
