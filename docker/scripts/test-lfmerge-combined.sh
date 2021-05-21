#!/bin/bash -e
echo "Running unit tests"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5

ln -sf ../Mercurial output/
msbuild /t:TestOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
