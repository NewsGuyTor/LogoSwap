#!/bin/bash
#
# LogoSwap Build Script
# Builds the plugin and packages it for distribution
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="LogoSwap"
VERSION="${1:-1.0.0}"
OUTPUT_DIR="./artifacts"
BUILD_CONFIG="Release"

echo -e "${CYAN}╔════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║       LogoSwap Build Script            ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════╝${NC}"
echo ""

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --version) VERSION="$2"; shift ;;
        -h|--help)
            echo "Usage: ./build.sh [VERSION]"
            echo ""
            echo "Examples:"
            echo "  ./build.sh 1.0.0"
            echo "  ./build.sh --version 1.2.0"
            exit 0
            ;;
        *) 
            if [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
                VERSION="$1"
            fi
            ;;
    esac
    shift
done

echo -e "${YELLOW}Building version: ${VERSION}${NC}"
echo ""

# Clean previous builds
echo -e "${YELLOW}[1/5] Cleaning previous builds...${NC}"
rm -rf ./bin ./obj "$OUTPUT_DIR"
dotnet clean -c "$BUILD_CONFIG" --nologo -v q 2>/dev/null || true

# Restore packages
echo -e "${YELLOW}[2/5] Restoring NuGet packages...${NC}"
dotnet restore --nologo -v q

# Build the project
echo -e "${YELLOW}[3/5] Building ${PROJECT_NAME}...${NC}"
dotnet build -c "$BUILD_CONFIG" --nologo -v q \
    /p:Version="$VERSION" \
    /p:AssemblyVersion="${VERSION}.0" \
    /p:FileVersion="${VERSION}.0"

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Package the plugin
echo -e "${YELLOW}[4/5] Packaging plugin...${NC}"

# Create a temporary directory for packaging
TEMP_DIR=$(mktemp -d)
PLUGIN_DIR="$TEMP_DIR/$PROJECT_NAME"
mkdir -p "$PLUGIN_DIR"

# Copy the DLL to the package directory
cp "./bin/$BUILD_CONFIG/net9.0/$PROJECT_NAME.dll" "$PLUGIN_DIR/"

# Create the ZIP file
ZIP_FILE="$OUTPUT_DIR/${PROJECT_NAME}_${VERSION}.zip"
(cd "$TEMP_DIR" && zip -rq "$PROJECT_NAME.zip" "$PROJECT_NAME")
mv "$TEMP_DIR/$PROJECT_NAME.zip" "$ZIP_FILE"

# Cleanup temp directory
rm -rf "$TEMP_DIR"

# Generate checksum
echo -e "${YELLOW}[5/5] Generating checksums...${NC}"
if command -v md5sum &> /dev/null; then
    CHECKSUM=$(md5sum "$ZIP_FILE" | awk '{print $1}')
elif command -v md5 &> /dev/null; then
    CHECKSUM=$(md5 -q "$ZIP_FILE")
else
    CHECKSUM="(md5 command not found)"
fi

# Output results
echo ""
echo -e "${GREEN}╔════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║         Build Successful! ✓            ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════╝${NC}"
echo ""
echo -e "${CYAN}Output files:${NC}"
echo "  → $ZIP_FILE"
echo "  → ./bin/$BUILD_CONFIG/net9.0/$PROJECT_NAME.dll"
echo ""
echo -e "${CYAN}Package contents:${NC}"
unzip -l "$ZIP_FILE" | grep -E "^\s+[0-9]+" | grep -v "files$" | awk '{print "  " $NF}'
echo ""
echo -e "${CYAN}MD5 Checksum:${NC}"
echo "  $CHECKSUM"
echo ""
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}Update your manifest.json with:${NC}"
echo ""
cat << EOF
{
  "version": "$VERSION",
  "changelog": "Your changelog here",
  "targetAbi": "10.11.0.0",
  "sourceUrl": "https://github.com/NewsGuyTor/LogoSwap/releases/download/$VERSION/${PROJECT_NAME}_${VERSION}.zip",
  "checksum": "$CHECKSUM",
  "timestamp": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
}
EOF
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
