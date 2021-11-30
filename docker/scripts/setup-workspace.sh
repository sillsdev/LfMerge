#!/bin/bash

# Assumptions:
# - Git repo is mounted under ${HOME}/packages/lfmerge
# - fw8-flexbridge.tar.xz dependency is mounted under ${HOME}/dependencies

export MONO_PREFIX=/opt/mono5-sil

mkdir -p ${HOME}/.gnupg ${HOME}/ci-builder-scripts/bash ${HOME}/packages/lfmerge

cd ${HOME}/packages/lfmerge
git clean -dxf --exclude=packages/
git reset --hard

# Instead of downloading FLExBridge DLLs which have vanished from TeamCity, store them in the Docker image
mkdir -p lib
cp ${HOME}/dependencies/fw8-flexbridge.tar.xz lib/

# TODO: Consider running a package restore here so that it's cached in the build image instead of having to run in the container each time
# E.g., call download-dependencies-combined.sh at this point

# The make-source shell script (and its common.sh helper) expects to live under ${HOME}/ci-builder-scripts/bash, so make sure that's the case
mkdir -p ${HOME}/ci-builder-scripts/bash
cp ${HOME}/docker/common.sh ${HOME}/ci-builder-scripts/bash/
cp ${HOME}/docker/make-source ${HOME}/ci-builder-scripts/bash/

cd docker/scripts

