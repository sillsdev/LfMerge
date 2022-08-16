#!/bin/bash

set -e

DEST="${1:-${HOME}/packages/lfmerge}"

export MONO_PREFIX=/opt/mono5-sil

mkdir -p "${HOME}/.gnupg" "${HOME}/ci-builder-scripts/bash" "${DEST}"

REPO_ROOT="$(git rev-parse --show-toplevel)"

# cp -a "${REPO_ROOT}" "${DEST}" creates ${DEST}/repo and then everything is under there. That's not actually what we want. So...
sudo cp -a "${REPO_ROOT}"/* "${REPO_ROOT}"/.[a-zA-Z0-9]* "${DEST}"
sudo chown -R builder:users "${DEST}"
# The make-source shell script (and its common.sh helper) expects to live under ${HOME}/ci-builder-scripts/bash, so make sure that's the case
mkdir -p "${HOME}/ci-builder-scripts/bash"
cp "${DEST}/docker/common.sh" "${HOME}/ci-builder-scripts/bash/"
cp "${DEST}/docker/make-source" "${HOME}/ci-builder-scripts/bash/"

cd "${DEST}"
if [ "${BRANCH_TO_BUILD}" ]; then
	git checkout "${BRANCH_TO_BUILD}"
fi
git clean -dxf --exclude=packages/
git reset --hard

# FLExBridge dependencies from FW 8 builds have vanished from TeamCity, so we stored them in the Docker image under ${REPO_ROOT}/docker
mkdir -p lib
(cd lib && xz -dc "${REPO_ROOT}/docker/fw8-flexbridge.tar.xz" | tar xf -)
