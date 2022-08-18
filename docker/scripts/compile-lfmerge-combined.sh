#!/bin/bash -e

set -e

echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which dotnet)"

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"

dotnet msbuild /t:CompileOnly /v:quiet /property:Configuration=Release /property:DatabaseVersion=${DbVersion} build/LfMerge.proj

# ln -sf ../Mercurial output/
# xbuild /t:TestOnly /v:detailed /property:Configuration=Release /property:DatabaseVersion=${DbVersion} build/LfMerge.proj
