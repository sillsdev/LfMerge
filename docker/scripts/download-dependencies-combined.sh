#!/bin/bash -e

set -e

echo "Downloading dependencies"
export MONO_PREFIX=/opt/mono5-sil
. environ

pwd

if [ "x$1" = "x7000072" ]; then
# msbuild /t:RestorePackages /p:KeepJobsFile=false build/LfMerge.proj
echo "Skipping dotnet restore since that happens automatically in dotnet build step"
# dotnet restore
else
msbuild /t:DownloadDependencies /p:KeepJobsFile=false build/LfMerge.proj
fi

