#!/bin/bash

# Find appropriate branch(es) to build
CURRENT_BRANCH=$(git name-rev --name-only HEAD | sed -e 's/^remotes\///')
PARENT_MAJOR_VERSION=$(git describe --long --match "v*" | cut -c1-2)

# FW8 branches will have ancestors tagged v1.x, while FW9 branches will have ancestors tagged v2.x
if [ "x$PARENT_MAJOR_VERSION" = "xv2" ]; then
	IS_FW9=true
else
	IS_FW9=""
fi

if [ "${IS_FW9}" ]; then
	FW9_BUILD_BRANCH="${CURRENT_BRANCH}"
	if [ "$1" ]; then
		FW8_BUILD_BRANCH="$1"
	elif [ "${CURRENT_BRANCH}" = "master" -o "${CURRENT_BRANCH}" = "qa" -o "${CURRENT_BRANCH}" = "live" ]; then
		FW8_BUILD_BRANCH="fieldworks8-${CURRENT_BRANCH}"
	else
		FW8_BUILD_BRANCH="fieldworks8-master"
	fi
else
	FW8_BUILD_BRANCH="${CURRENT_BRANCH}"
	if [ "$1" ]; then
		FW9_BUILD_BRANCH="$1"
	elif [ "${CURRENT_BRANCH}" = "fieldworks8-master" -o "${CURRENT_BRANCH}" = "fieldworks8-qa" -o "${CURRENT_BRANCH}" = "fieldworks8-live" ]; then
		FW9_BUILD_BRANCH="${CURRENT_BRANCH##fieldworks8-}"
	else
		FW9_BUILD_BRANCH="master"
	fi
fi

echo FW8Branch="${FW8_BUILD_BRANCH}"
echo FW9Branch="${FW9_BUILD_BRANCH}"
