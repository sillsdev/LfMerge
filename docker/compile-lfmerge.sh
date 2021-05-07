#!/bin/bash -e
echo "Compiling LfMerge and running unit tests"
BUILD=Release . environ

# This part only needed for Mono 3.x
# TODO: Detect and use this section only on 3.x
#mozroots --import --sync
#yes | certmgr -ssl https://go.microsoft.com
#yes | certmgr -ssl https://nugetgallery.blob.core.windows.net
#yes | certmgr -ssl https://nuget.org

# This part needed for all Mono
echo "Using $(which mono)"
dotnet build /t:Compile /v:detailed /property:Configuration=Release build/LfMerge.proj
result=$?

# Jenkins has problems using jgit to remove LinkedFiles directory with
# non-ASCII characters in filenames, so we delete these here
#rm -rf data/testlangproj
#rm -rf data/testlangproj-modified
exit $result
