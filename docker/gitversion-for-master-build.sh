#!/bin/bash -e
cd /build/LfMerge

export MONO_PREFIX=/opt/mono5-sil
RUNMODE="PACKAGEBUILD" BUILD=Release . environ
msbuild /t:RestoreBuildTasks build/LfMerge.proj
mkdir -p output/Release

dotnet tool restore
# GitVersion detects Jenkins builds if the JENKINS_URL environment variable is set (to anything),
# and outputs a "gitversion.properties" file that's suitable for Bash scripting
JENKINS_URL=x dotnet gitversion -output buildserver

. gitversion.properties

if [ "${GitVersion_PreReleaseLabel}" != "" ]; then
	PreReleaseTag="~${GitVersion_PreReleaseLabel}.${GitVersion_PreReleaseNumber}"
fi

echo "PackageVersion=${GitVersion_MajorMinorPatch}${PreReleaseTag}.${BUILD_NUMBER}" >> gitversion.properties

echo "Building packages for version ${GitVersion_MajorMinorPatch}${PreReleaseTag}.${BUILD_NUMBER}"