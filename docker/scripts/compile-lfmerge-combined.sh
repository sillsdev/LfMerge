#!/bin/bash -e

set -e

echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which dotnet)"

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"

dotnet build --no-restore /v:m /property:Configuration=Release /property:DatabaseVersion=${DbVersion} LfMerge.sln

# ln -sf ../Mercurial output/
# xbuild /t:TestOnly /v:detailed /property:Configuration=Release /property:DatabaseVersion=${DbVersion} build/LfMerge.proj
