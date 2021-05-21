#!/bin/bash -e
echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5
/opt/mono5-sil/bin/msbuild /t:CompileOnly /v:quiet /property:Configuration=Release build/LfMerge.proj

# ln -sf ../Mercurial output/
# xbuild /t:TestOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
