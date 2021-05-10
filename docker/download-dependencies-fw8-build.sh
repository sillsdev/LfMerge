#!/bin/bash -e
echo "Downloading dependencies"
export MONO_PREFIX=/opt/mono5-sil
. environ
xbuild /t:DownloadDependencies /p:KeepJobsFile=false build/LfMerge.proj

BUILD=Release . environ
mozroots --import --sync
yes | certmgr -ssl https://go.microsoft.com
yes | certmgr -ssl https://nugetgallery.blob.core.windows.net
yes | certmgr -ssl https://nuget.org

echo "Using $(which mono)"
xbuild /t:Test /v:detailed /property:Configuration=Release build/LfMerge.proj
