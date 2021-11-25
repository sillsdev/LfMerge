#!/bin/bash -e
echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5

export DbVersion="${1-7000072}"
if [ "x$1" = "x7000072" ]; then
/opt/mono5-sil/bin/msbuild /t:CompileOnly /v:quiet /property:Configuration=Release build/LfMerge.proj
# dotnet build /t:CompileOnly /v:quiet /property:Configuration=Release build/LfMerge.proj
else
/opt/mono5-sil/bin/msbuild /t:CompileOnly /v:quiet /property:Configuration=Release build/LfMerge.proj
fi

# ln -sf ../Mercurial output/
# xbuild /t:TestOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
