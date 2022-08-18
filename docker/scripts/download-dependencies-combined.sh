#!/bin/bash -e

set -e

echo "Downloading dependencies"
export MONO_PREFIX=/opt/mono5-sil
. environ

pwd

if [ "x$1" = "x7000072" ]; then
dotnet restore -v:m
else
msbuild /t:DownloadDependencies /p:KeepJobsFile=false build/LfMerge.proj
fi

