#!/bin/bash

which parallel >/dev/null
if [ $? -ne 0 ]; then
	echo 'Please run "sudo apt-get install parallel" and try again.'
	exit 1
fi

mkdir -p /storage/nuget
if [ $? -ne 0 ]; then
	echo "Please create a /storage directory and then run 'chown ${USER} /storage', to be able to cache NuGet packages"
	exit 1
fi
# TODO: Check for rwxrwsr-x permissions and appropriate uid/gid settings

# Find appropriate branch(es) to build
CURRENT_BRANCH="$(git name-rev --name-only HEAD)"
PARENT_MAJOR_VERSION=$(git describe --long --match "v*" | cut -c1-2)
BUILD_FW8=1

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
		if [ "$1" = "--no-fw8" ]; then
			BUILD_FW8=0
			FW8_BUILD_BRANCH=""
		else
			FW8_BUILD_BRANCH="$1"
		fi
	elif [ "${CURRENT_BRANCH}" = "master" -o "${CURRENT_BRANCH}" = "qa" -o "${CURRENT_BRANCH}" = "live" ]; then
		FW8_BUILD_BRANCH="fieldworks8-${CURRENT_BRANCH}"
	else
		echo No FW 8 branch specified, assuming fieldworks8-master
		echo To specify a different branch, run pbuild.sh '<'fw8-branch'>', e.g. '"'pbuild.sh feature/some-fw8-branch'"'
		echo 'Or to skip FW 8 branch entirely, run pbuild.sh --no-fw8'
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

if [ "${BUILD_FW8}" -eq 0 ]; then
	echo Will build ONLY the FW9 build, from "${FW9_BUILD_BRANCH}"
else
	echo Will build FW9 build from "${FW9_BUILD_BRANCH}" and FW8 builds from "${FW8_BUILD_BRANCH}"
fi

# Clean up any previous builds
# This for loop should be across all DbVersions even if BUILD_FW8 is 0
for f in 68 69 70 72; do
    # Can safely ignore "container doesn't exist" as that's not an error
    docker container kill tmp-lfmerge-build-70000${f} >/dev/null 2>/dev/null || true
    docker container rm tmp-lfmerge-build-70000${f} >/dev/null 2>/dev/null || true
done

CURRENT_UID=$(id -u)

# First create the base build container ONCE (not in parallel), to ensure that the slow steps (apt-get install mono5-sil) are cached
docker build --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-builder-base --target lfmerge-builder-base .

# Create the build images for each DbVersion in parallel
# NOTE: now that the differences are only ENV lines, parallel no longer gains any time. Should we turn this into a for loop?
if [ "${BUILD_FW8}" -eq 0 ]; then
	docker build --build-arg DbVersion=7000072 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000072 .
else
	time parallel --no-notice <<EOF
docker build --build-arg "RunUnitTests=1" --build-arg DbVersion=7000068 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000068 .
docker build --build-arg "RunUnitTests=1" --build-arg DbVersion=7000069 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000069 .
docker build --build-arg "RunUnitTests=1" --build-arg DbVersion=7000070 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000070 .
docker build --build-arg "RunUnitTests=1" --build-arg DbVersion=7000072 --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-7000072 .
EOF
fi

# TODO: Make unit testing (--build-arg "RunUnitTests=1") controllable by a pbuild.sh flag

# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker build --build-arg DbVersion=7000072 -t lfmerge-build-7000072 -f combined.dockerfile .

. docker/scripts/get-version-number.sh

# Run the build
if [ "${BUILD_FW8}" -eq 0 ]; then
	docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW9_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072
else
	time parallel --no-notice <<EOF
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000068 lfmerge-build-7000068
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000069 lfmerge-build-7000069
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW8_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --name tmp-lfmerge-build-7000070 lfmerge-build-7000070
docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo --mount type=tmpfs,dst=/tmp --env "BRANCH_TO_BUILD=${FW9_BUILD_BRANCH}" --env "BUILD_NUMBER=999" --env "DebPackageVersion=${DebPackageVersion}" --env "Version=${MsBuildVersion}" --env "MajorMinorPatch=${MajorMinorPatch}" --env "AssemblyVersion=${AssemblySemVer}" --env "FileVersion=${AssemblySemFileVer}" --env "InformationalVersion=${InformationalVersion}" --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072
EOF
fi
# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker run --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072

# Collect results
mkdir -p tarball

if [ "${BUILD_FW8}" -eq 0 ]; then
	for f in 72; do
		docker container cp tmp-lfmerge-build-70000${f}:/home/builder/repo/tarball ./
	done
else
	for f in 68 69 70 72; do
		docker container cp tmp-lfmerge-build-70000${f}:/home/builder/repo/tarball ./
	done
fi

time docker build -t ghcr.io/sillsdev/lfmerge -f Dockerfile.finalresult .
