#!/bin/bash -e

set -e

echo "Downloading dependencies"
export MONO_PREFIX=/opt/mono5-sil
. environ
if [ "x$1" = "x7000072" ]; then
msbuild /t:RestorePackages /p:KeepJobsFile=false build/LfMerge.proj
else
msbuild /t:DownloadDependencies /p:KeepJobsFile=false build/LfMerge.proj
fi

