#!/bin/bash

set -e

SCRIPT_DIR=$(dirname $(readlink -f "$0"))

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"
sudo mkdir -p /usr/lib/lfmerge/${DbVersion}

echo Running as $(id)
# Assuming script is being run from inside the repo, find the repo root and use that as the working directory from now on
echo "Script dir is ${SCRIPT_DIR}"
ls -ld "${SCRIPT_DIR}"
cd "${SCRIPT_DIR}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
echo "Repo root is ${REPO_ROOT}"
ls -ld "${REPO_ROOT}"
cd "${REPO_ROOT}"

BUILD_DIR="${HOME}/packages/lfmerge"
echo "Setting up clean workspace in ${BUILD_DIR}"
"$SCRIPT_DIR"/setup-workspace.sh "${BUILD_DIR}"
cd "${BUILD_DIR}"

# TODO: Can we get rid of this mkdir?
mkdir -p output/Release

echo "Building packages for version ${DebPackageVersion}"

# Explicit restore step so we can save time in later build steps by using --no-restore
echo "Downloading dependencies"
dotnet restore -v:m

echo "Compiling LfMerge"
dotnet build --no-restore /v:m /property:Configuration=Release /property:DatabaseVersion=${DbVersion} LfMerge.sln

if [ -n "$RUN_UNIT_TESTS" -a "$RUN_UNIT_TESTS" -ne 0 ]; then
	echo "Running unit tests"
	# dotnet test defaults to killing test processes after 100ms, which is way too short
	export VSTEST_TESTHOST_SHUTDOWN_TIMEOUT=30000  # 30 seconds, please, since some of our tests can run very long
	# Treat TEST_SPEC enviornment variable as a "contains" operation
	if [ -n "$TEST_SPEC" ]; then
		dotnet test --no-restore -c Release --filter "FullyQualifiedName~${TEST_SPEC}"
	else
		dotnet test --no-restore -c Release
	fi
fi

"$SCRIPT_DIR"/create-installation-tarball.sh ${DbVersion}
