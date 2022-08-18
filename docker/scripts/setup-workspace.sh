#!/bin/bash

set -e

DEST="${1:-${HOME}/packages/lfmerge}"
mkdir -p "${DEST}"

REPO_ROOT="$(git rev-parse --show-toplevel)"

# cp -a "${REPO_ROOT}" "${DEST}" creates ${DEST}/repo and then everything is under there. That's not actually what we want. So...
sudo cp -a "${REPO_ROOT}"/* "${REPO_ROOT}"/.[a-zA-Z0-9]* "${DEST}"
sudo chown -R builder:users "${DEST}"

cd "${DEST}"
if [ "${BRANCH_TO_BUILD}" ]; then
	git checkout "${BRANCH_TO_BUILD}"
fi

git clean -dxf --exclude=packages/ --exclude=build/packages/
git reset --hard
