#!/bin/bash -e

SCRIPT_DIR=$(dirname $(readlink -f "$0"))

echo "Calculated versions:"
echo "DebPackageVersion=${DebPackageVersion}"
echo "MajorMinorPatch=${MajorMinorPatch}"
echo "AssemblyVersion=${AssemblyVersion}"
echo "FileVersion=${FileVersion}"
echo "InformationalVersion=${InformationalVersion}"

# Fetch tags for GitVersion
git fetch --tags

# Fetch master for GitVersion
if [ -n "$GITHUB_REF" -a "x$GITHUB_REF" != "xrefs/heads/master" ]; then
	git branch --create-reflog master origin/master
fi

export MONO_PREFIX=/opt/mono5-sil
RUNMODE="PACKAGEBUILD" BUILD=Release . environ
# msbuild build/LfMerge.proj
mkdir -p output/Release

echo "Building packages for version ${DebPackageVersion}"
