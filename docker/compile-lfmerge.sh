#!/bin/bash -e
echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5
dotnet build /t:CompileOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
# TODO: Determine if this symlink is necessary
ln -sf ../Mercurial output/
cd /build/LfMerge/output/Release/net462
export PATH="${PATH}:/opt/mono5-sil/bin"
# TODO: Test all DLLs
# dotnet test -f net462 LfMerge.Core.Tests.dll
