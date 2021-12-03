#!/bin/bash

DEST="${1:-${HOME}/packages/lfmerge}"

export MONO_PREFIX=/opt/mono5-sil

mkdir -p "${HOME}/.gnupg" "${HOME}/ci-builder-scripts/bash" "${DEST}"

REPO_ROOT="$(git rev-parse --show-toplevel)"
echo "Inside setup-workspace.sh, pwd is $(pwd) and repo root is ${REPO_ROOT}"

echo 'DEBUG: ls -lR ${DEST}' which is "${DEST}"
ls -lR "${DEST}"
echo 'DEBUG: ls -lR ${REPO_ROOT}' which is "${REPO_ROOT}"
ls -lR "${REPO_ROOT}"
sudo cp -a "${REPO_ROOT}" "${DEST}"
sudo chown -R builder:users "${DEST}"
echo 'DEBUG: ls -lR ${DEST}'
ls -lR "${DEST}"
# The make-source shell script (and its common.sh helper) expects to live under ${HOME}/ci-builder-scripts/bash, so make sure that's the case
mkdir -p "${HOME}/ci-builder-scripts/bash"
cp "${DEST}/docker/common.sh" "${HOME}/ci-builder-scripts/bash/"
cp "${DEST}/docker/make-source" "${HOME}/ci-builder-scripts/bash/"

cd "${DEST}"
git clean -dxf --exclude=packages/
git reset --hard
echo 'DEBUG: ls -lR ${DEST} after git clean'
ls -lR "${DEST}"

# FLExBridge dependencies from FW 8 builds have vanished from TeamCity, so we stored them in the Docker image under ${REPO_ROOT}/docker
mkdir -p lib
(cd lib && xz -d "${REPO_ROOT}/docker/fw8-flexbridge.tar.xz" | tar xf -)
