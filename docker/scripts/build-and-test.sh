#!/bin/bash

# Commented out due to `dotnet test` crashing even on successful tests
# Once we upgrade to a version of `dotnet test` that no longer crashes, uncomment the "set -e" line
# set -e

SCRIPT_DIR=$(dirname $(readlink -f "$0"))

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"
sudo mkdir -p /usr/lib/lfmerge/${DbVersion}

echo Running as $(id)
# Assuming script is being run from inside the repo, find the repo root and use that as the working directory from now on
cd "${SCRIPT_DIR}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "${REPO_ROOT}"

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
	export LD_DEBUG=libs
	# Treat TEST_SPEC enviornment variable as a "contains" operation
	if [ -n "$TEST_SPEC" ]; then
		dotnet test --no-restore -l:"console;verbosity=normal" -l:nunit -c Release --filter "FullyQualifiedName~${TEST_SPEC}"
	else
		dotnet test --no-restore -l:"console;verbosity=normal" -l:nunit -c Release
	fi
fi

"$SCRIPT_DIR"/create-installation-tarball.sh ${DbVersion}
