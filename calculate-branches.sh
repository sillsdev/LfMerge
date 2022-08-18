#!/bin/bash

# Find appropriate branch(es) to build
HEAD_BRANCH=$(git rev-parse --symbolic-full-name HEAD)
HEAD_BRANCH=${HEAD_BRANCH#refs/heads/}
GITHUB_BRANCH=${GITHUB_HEAD_REF:-${GITHUB_REF:-$HEAD_BRANCH}}
GITHUB_BRANCH=${GITHUB_BRANCH#refs/heads/}

CURRENT_BRANCH=${GITHUB_BRANCH:-$HEAD_BRANCH}
echo FW9Branch="${CURRENT_BRANCH}"
# We no longer have an FW8Branch output
