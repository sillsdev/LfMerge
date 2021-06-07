#!/bin/bash -e
echo "Downloading dependencies"
export MONO_PREFIX=/opt/mono5-sil
. environ
xbuild /t:DownloadDependencies /p:KeepJobsFile=false build/LfMerge.proj
