#!/bin/bash -e
echo "Running unit tests"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5

ln -sf ../Mercurial output/
if [ -n "$TEST_SPEC" ]; then
  echo Only running $TEST_SPEC
  mono packages/NUnit.Runners.Net4.2.6.4/tools/nunit-console.exe output/Release/LfMerge*.Tests.dll -run "$TEST_SPEC"
else
  msbuild /t:TestOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
fi
