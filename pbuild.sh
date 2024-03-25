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
dotnet clean LfMerge.sln
[ -d tarball ] && rm -rf tarball

# Create prerequisite directories that LfMerge expects in the unit tests
# TODO: Probably not needed now?
# mkdir -p ${HOME}/.nuget/packages
# mkdir -p output/{Debug,Release}/net8.0
# mkdir -p src/{FixFwData,LfMergeAuxTool,LfMerge.Core,LfMerge.Core.Tests,LfMerge,LfMergeQueueManager,LfMerge.Tests}/obj/{Debug,Release}/net8.0

. docker/scripts/get-version-number.sh

# Run build once for each DbVersion
for DbVersion in ${DBMODEL_VERSIONS[@]}; do
	docker/scripts/build-and-test.sh ${DbVersion}
done

time docker build -t ghcr.io/sillsdev/lfmerge -f Dockerfile.finalresult .
