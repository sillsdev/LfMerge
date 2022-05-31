#!/bin/bash

set -e

export DbVersion="${1-7000072}"

echo "Running unit tests for ${DbVersion}"
BUILD=Release . environ

echo "Using $(which mono)"
export FrameworkPathOverride=/opt/mono5-sil/lib/mono/4.5

ln -sf ../Mercurial output/
if [ -n "$TEST_SPEC" ]; then
  echo Only running "$TEST_SPEC"
  if [ "x$NUNIT_VERSION_MAJOR" = "x2" ]; then
    mono packages/NUnit.Runners.Net4.2.6.4/tools/nunit-console.exe output/Release/LfMerge*.Tests.dll -run "$TEST_SPEC"
  elif [ "x$NUNIT_VERSION_MAJOR" = "x3" ]; then
    mono packages/NUnit.ConsoleRunner/tools/nunit3-console.exe output/Release/LfMerge*.Tests.dll --test "$TEST_SPEC"
  else
    echo Warning, NUnit version not specified. Assuming NUnit 3.
    mono packages/NUnit.ConsoleRunner/tools/nunit3-console.exe output/Release/LfMerge*.Tests.dll --test "$TEST_SPEC"
  fi
else
  msbuild /t:TestOnly /v:detailed /property:Configuration=Release build/LfMerge.proj
fi
