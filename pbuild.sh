#!/bin/bash

set -e

# These are arrays; see https://www.gnu.org/software/bash/manual/html_node/Arrays.html
DBMODEL_VERSIONS=(7000072)
HISTORICAL_VERSIONS=(7000068 7000069 7000070)

# Find appropriate branch(es) to build
FW9_BUILD_BRANCH="$(git name-rev --name-only HEAD)"
echo Will build ONLY the FW9 build, from "${FW9_BUILD_BRANCH}"

# Clean up any previous builds
dotnet clean LfMerge.sln || true
[ -d tarball ] && rm -rf tarball

# Set MsBuildVersion environment variable (and a couple others) to use in build-and-test.sh
. docker/scripts/get-version-number.sh

# Run build once for each DbVersion
for DbVersion in ${DBMODEL_VERSIONS[@]}; do
	docker/scripts/build-and-test.sh ${DbVersion}
done

time docker build -t ghcr.io/sillsdev/lfmerge:${MsBuildVersion:-latest} -f Dockerfile.finalresult .
