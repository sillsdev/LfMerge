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
msbuild /t:RestoreBuildTasks build/LfMerge.proj
mkdir -p output/Release

if [ -n "$UPDATE_ASSEMBLYINFO_BY_SCRIPT" -a "$UPDATE_ASSEMBLYINFO_BY_SCRIPT" -ne 0 ]; then
	UPDATE_SCRIPT="${SCRIPT_DIR}/update-assemblyinfo.sh"
	echo "Will run ${UPDATE_SCRIPT} to update AssemblyInfo.cs files"
	[ -x "${UPDATE_SCRIPT}" ] && find src -name AssemblyInfo.cs -path '*LfMerge*' -print0 | xargs -0 -n 1 "${UPDATE_SCRIPT}"
fi

echo "Building packages for version ${DebPackageVersion}"
