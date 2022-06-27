#!/bin/bash -e
echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which dotnet)"
dotnet build /t:CompileOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
# TODO: Determine if this symlink is necessary
ln -sf ../Mercurial output/
# TODO: Test all DLLs
# dotnet test -f net462 LfMerge.Core.Tests.dll
