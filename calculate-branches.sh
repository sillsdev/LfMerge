#!/bin/bash

# Find appropriate branch(es) to build
HEAD_BRANCH=$(git rev-parse --symbolic-full-name HEAD)
HEAD_BRANCH=${HEAD_BRANCH#refs/heads/}
GITHUB_BRANCH=${GITHUB_HEAD_REF:-${GITHUB_REF:-$HEAD_BRANCH}}
GITHUB_BRANCH=${GITHUB_BRANCH#refs/heads/}

if [ -n "${GITHUB_HEAD_REF}" ]; then
	PR_BRANCH=${GITHUB_BRANCH}
fi

CURRENT_BRANCH=${GITHUB_BRANCH:-$HEAD_BRANCH}
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
		if [ "$1" = "--no-fw8" ]; then
			BUILD_FW8=0
			FW8_BUILD_BRANCH=""
		else
			FW8_BUILD_BRANCH="$1"
		fi
	elif [ -n "${PR_BRANCH}" ]; then
		# Find corresponding branch, if it exists, by adding "-fw8"
		if git rev-parse -q --verify "${PR_BRANCH}-fw8" >/dev/null; then
			FW8_BUILD_BRANCH="${PR_BRANCH}-fw8"
		else
			# Fall back to fieldworks8-master if no corresponding PR branch found
			FW8_BUILD_BRANCH="fieldworks8-master"
		fi
	elif [ "${CURRENT_BRANCH}" = "master" -o "${CURRENT_BRANCH}" = "qa" -o "${CURRENT_BRANCH}" = "live" ]; then
		FW8_BUILD_BRANCH="fieldworks8-${CURRENT_BRANCH}"
	else
		# Find corresponding branch, if it exists, by adding "-fw8"
		if git rev-parse -q --verify "${CURRENT_BRANCH}-fw8" >/dev/null; then
			FW8_BUILD_BRANCH="${CURRENT_BRANCH}-fw8"
		else
			# Fall back to fieldworks8-master if no corresponding PR branch found
			FW8_BUILD_BRANCH="fieldworks8-master"
		fi
	fi
else
	FW8_BUILD_BRANCH="${CURRENT_BRANCH}"
	if [ "$1" ]; then
		FW9_BUILD_BRANCH="$1"
	elif [ -n "${PR_BRANCH}" ]; then
		# Find corresponding branch, if it exists, by trimming "-fw8"
		CANDIDATE=${PR_BRANCH%-fw8}
		# If candidate is same as PR branch, this PR branch didn't match the naming scheme
		if [ "x$CANDIDATE" = "x$PR_BRANCH" ]; then
			FW9_BUILD_BRANCH="master"
		elif git rev-parse -q --verify "${PR_BRANCH%-fw8}" >/dev/null; then
			FW9_BUILD_BRANCH="${PR_BRANCH%-fw8}"
		else
			# Fall back to master if no corresponding PR branch found
			FW9_BUILD_BRANCH="master"
		fi
	elif [ "${CURRENT_BRANCH}" = "fieldworks8-master" -o "${CURRENT_BRANCH}" = "fieldworks8-qa" -o "${CURRENT_BRANCH}" = "fieldworks8-live" ]; then
		FW9_BUILD_BRANCH="${CURRENT_BRANCH##fieldworks8-}"
	else
		# Find corresponding branch, if it exists, by trimming "-fw8"
		CANDIDATE=${CURRENT_BRANCH%-fw8}
		# If candidate is same as current, we're on a branch that didn't match the naming scheme
		if [ "x$CANDIDATE" = "x$CURRENT_BRANCH" ]; then
			FW9_BUILD_BRANCH="master"
		elif git rev-parse -q --verify "${CURRENT_BRANCH%-fw8}" >/dev/null; then
			FW9_BUILD_BRANCH="${CURRENT_BRANCH%-fw8}"
		else
			# Fall back to master if no corresponding PR branch found
			FW9_BUILD_BRANCH="master"
		fi
	fi
fi

echo FW8Branch="${FW8_BUILD_BRANCH}"
echo FW9Branch="${FW9_BUILD_BRANCH}"
