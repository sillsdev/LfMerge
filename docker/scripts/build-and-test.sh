#!/bin/bash

SCRIPT_DIR=$(dirname $(readlink -f "$0"))

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"
sudo mkdir -p /usr/lib/lfmerge/${DbVersion}

# Assuming script is being run from inside the repo, find the repo root and use that as the working directory from now on
cd "${SCRIPT_DIR}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "${REPO_ROOT}"

"$SCRIPT_DIR"/setup-workspace.sh "${REPO_ROOT}"

echo After setup-workspace.sh, pwd is $(pwd)

"$SCRIPT_DIR"/gitversion-combined.sh ${DbVersion}

"$SCRIPT_DIR"/download-dependencies-combined.sh ${DbVersion}

"$SCRIPT_DIR"/compile-lfmerge-combined.sh ${DbVersion}

if [ -n "$RUN_UNIT_TESTS" -a "$RUN_UNIT_TESTS" -ne 0 ]; then
    rm -rf "$SCRIPT_DIR"/data/php
    mkdir -p "$SCRIPT_DIR"/data/php
    cp -a /var/www/html "$SCRIPT_DIR"/data/php/src
    "$SCRIPT_DIR"/test-lfmerge-combined.sh ${DbVersion}
fi

rm -rf "$SCRIPT_DIR"/data/php   # So it doesn't get into the .deb source package

"$SCRIPT_DIR"/build-debpackages-combined.sh ${DbVersion}
