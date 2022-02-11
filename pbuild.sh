#!/bin/bash

which parallel >/dev/null || (echo 'Please run "sudo apt-get install parallel" and try again.'; exit 1)

# Find appropriate branch(es) to build
CURRENT_BRANCH="$(git name-rev --name-only HEAD)"
PARENT_MAJOR_VERSION=$(git describe --long --match "v*" | cut -c1-2)

# FW8 branches will have ancestors tagged v1.x, while FW9 branches will have ancestors tagged v2.x
if [ "x$PARENT_MAJOR_VERSION" = "xv2" ]; then
	IS_FW9=true
else
	IS_FW9=""
fi

if [ "${IS_FW9}" ]; then
	echo Current branch is FW9, detecting FW8 branch to use...
	FW9_BUILD_BRANCH="${CURRENT_BRANCH}"
	if [ "$1" ]; then
		FW8_BUILD_BRANCH="$1"
	elif [ "${CURRENT_BRANCH}" = "master" -o "${CURRENT_BRANCH}" = "qa" -o "${CURRENT_BRANCH}" = "live" ]; then
		FW8_BUILD_BRANCH="fieldworks8-${CURRENT_BRANCH}"
	else
		echo No FW 8 branch specified, assuming fieldworks8-master
		echo To specify a different branch, run pbuild.sh '<'fw8-branch'>', e.g. '"'pbuild.sh feature/some-fw8-branch'"'
		FW8_BUILD_BRANCH="fieldworks8-master"
	fi
else
	echo Current branch is FW8, detecting FW9 branch to use...
	FW8_BUILD_BRANCH="${CURRENT_BRANCH}"
	if [ "$1" ]; then
		FW9_BUILD_BRANCH="$1"
	elif [ "${CURRENT_BRANCH}" = "fieldworks8-master" -o "${CURRENT_BRANCH}" = "fieldworks8-qa" -o "${CURRENT_BRANCH}" = "fieldworks8-live" ]; then
		FW9_BUILD_BRANCH="${CURRENT_BRANCH##fieldworks8-}"
	else
		echo No FW 9 branch specified, assuming master
		echo To specify a different branch, run pbuild.sh '<'fw8-branch'>', e.g. '"'pbuild.sh feature/some-fw8-branch'"'
		FW9_BUILD_BRANCH="master"
	fi
fi
echo Will build FW9 build from "${FW9_BUILD_BRANCH}" and FW8 builds from "${FW8_BUILD_BRANCH}"

# Clean up any previous builds
for f in 68 69 70 72; do
# for f in 72; do
    # Can safely ignore "container doesn't exist" as that's not an error
    docker container kill tmp-lfmerge-build-70000${f} >/dev/null 2>/dev/null || true
    docker container rm tmp-lfmerge-build-70000${f} >/dev/null 2>/dev/null || true
done

CURRENT_UID=$(id -u)

# First create the base build container ONCE (not in parallel), to ensure that the slow steps (apt-get install mono5-sil) are cached
docker build --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-builder-base --target lfmerge-builder-base .

# Create the build containers in series, because I've had trouble when creating them in parallel
# (To create the build containers in series, which might be necessary if you have trouble with
# Docker caching while creating them in parallel, just comment out the "time parallel" and "EOF" lines)
time parallel --no-notice <<EOF
docker build --build-arg DbVersion=7000068 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000068 .
docker build --build-arg DbVersion=7000069 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000069 .
docker build --build-arg DbVersion=7000070 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000070 .
docker build --build-arg DbVersion=7000072 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000072 .
EOF

# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker build --build-arg DbVersion=7000072 -t lfmerge-build-7000072 -f combined.dockerfile .
. docker/scripts/get-version-number.sh

# Run the build
time parallel --no-notice <<EOF
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000068 lfmerge-build-7000068
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000069 lfmerge-build-7000069
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000070 lfmerge-build-7000070
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW9_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072
EOF

# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker run --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072

# Collect results
mkdir -p tarball
for f in 68 69 70 72; do
# for f in 72; do
    docker container cp tmp-lfmerge-build-70000${f}:/home/builder/repo/tarball ./
done
