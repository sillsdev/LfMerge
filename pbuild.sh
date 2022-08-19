#!/bin/bash

set -e

# These are arrays; see https://www.gnu.org/software/bash/manual/html_node/Arrays.html
DBMODEL_VERSIONS=(7000072)
HISTORICAL_VERSIONS=(7000068 7000069 7000070)

# In the future when we have more than one model version, we may want to use GNU parallel for building.
# ATTENTION: If GNU parallel is desired, uncomment the below (until the "ATTENTION: Stop uncommenting here" line):

# DBMODEL_COUNT=${#DBMODEL_VERSIONS[@]}
# MULTIPLE_VERSIONS=0
# if [ $DBMODEL_COUNT -gt 1 ]; then
# 	MULTIPLE_VERSIONS=1
# fi

# # Use GNU parallel only if we have multiple DBVersions to deal with
# # Specify USE_PARALLEL=0 or USE_PARALLEL=1 in environment to override this default
# USE_PARALLEL=${USE_PARALLEL:-$MULTIPLE_VERSIONS}

# # echo We have ${DBMODEL_COUNT} versions: "${DBMODEL_VERSIONS[@]}"
# if [ $USE_PARALLEL -gt 0 ]; then
# 	which parallel >/dev/null
# 	if [ $? -ne 0 ]; then
# 		echo 'Please run "sudo apt-get install parallel" and try again.'
# 		exit 1
# 	fi
# else
# 	echo GNU parallel will not be used
# fi

# ATTENTION: Stop uncommenting here

# Find appropriate branch(es) to build
FW9_BUILD_BRANCH="$(git name-rev --name-only HEAD)"
echo Will build ONLY the FW9 build, from "${FW9_BUILD_BRANCH}"

# Clean up any previous builds
# This for loop includes all historical DbVersions even if BUILD_FW8 is 0
for DbVersion in ${HISTORICAL_VERSIONS[@]} ${DBMODEL_VERSIONS[@]}; do
	# Can safely ignore "container doesn't exist" as that's not an error
	docker container kill tmp-lfmerge-build-${DbVersion} >/dev/null 2>/dev/null || true
	docker container rm tmp-lfmerge-build-${DbVersion} >/dev/null 2>/dev/null || true
done

CURRENT_UID=$(id -u)

# First create the base build container ONCE (it will be reused as a base by each DbVersion build), which should help with caching
docker build -t ghcr.io/sillsdev/lfmerge-base:sdk -f Dockerfile.builder-base .
docker build -t ghcr.io/sillsdev/lfmerge-base:runtime -f Dockerfile.runtime-base .
docker build --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-builder-base --target lfmerge-builder-base .

# Create the build images for each DbVersion
for DbVersion in ${DBMODEL_VERSIONS[@]}; do
	docker build --build-arg DbVersion=${DbVersion} --build-arg "BUILDER_UID=${CURRENT_UID}" -t lfmerge-build-${DbVersion} .
done

. docker/scripts/get-version-number.sh

# Run the build
for DbVersion in ${DBMODEL_VERSIONS[@]}; do
	docker run -it \
		--mount type=bind,source="$(pwd)",target=/home/builder/repo \
		--mount type=bind,src="${HOME}/.nuget/packages",dst=/home/builder/.nuget/packages \
		--mount type=tmpfs,dst=/tmp \
		--env "BUILD_NUMBER=999" \
		--env "DebPackageVersion=${DebPackageVersion}" \
		--env "Version=${MsBuildVersion}" \
		--env "MajorMinorPatch=${MajorMinorPatch}" \
		--env "AssemblyVersion=${AssemblySemVer}" \
		--env "FileVersion=${AssemblySemFileVer}" \
		--env "InformationalVersion=${InformationalVersion}" \
		--name tmp-lfmerge-build-${DbVersion} \
		lfmerge-build-${DbVersion}
done

time docker build -t ghcr.io/sillsdev/lfmerge -f Dockerfile.finalresult .
