#!/bin/bash

echo Start
REV=${GITHUB_REF:-$(git rev-parse --symbolic-full-name HEAD)}
DESCRIBE=$(git describe --long --match "v*")
MAJOR=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\1/')
MINOR=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\2/')
PATCH=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\3/')
# TODO: Detect need for minor/major updates and increment those instead of PATCH
COMMIT_COUNT=$(echo "$DESCRIBE" | sed -E 's/^[^-]+-([^-]+)-.*$/\1/')
COMMIT_HASH=$(echo "$DESCRIBE" | sed -E 's/^[^-]+-[^-]+-g(.*)$/\1/')
if [ -n "$COMMIT_COUNT" -a "$COMMIT_COUNT" -gt 0 ]; then
  # If we're building from a tagged version, rebuild precisely that version
  PATCH=$((${PATCH} + 1))
fi
echo "Build number before: $BUILD_NUMBER"
export BUILD_NUMBER=${BUILD_NUMBER:-${COMMIT_COUNT}}
echo "Build number after: $BUILD_NUMBER"
export MajorMinorPatch="${MAJOR}.${MINOR}.${PATCH}"
export AssemblySemVer="${MajorMinorPatch}.${BUILD_NUMBER}"
export AssemblySemFileVer="${MajorMinorPatch}.0"
export InformationalVersion="${DESCRIBE}"
echo "Calculating name from ${REV}"
if [ -z ${REV} ]; then
  echo Failed to get a meaningful commit name
fi
echo Got commit name ${REV}
RESULT=notfound
if echo "${REV}" | grep -E '^refs/pull/'; then
  echo Found PR
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/pull\/([0-9]+)\/merge/\1/')
fi
if echo "${REV}" | grep -E '^refs/heads/'; then
  echo Found branch
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/heads\///')
fi
if echo "${REV}" | grep -E '^refs/tags/'; then
  echo Found tag
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/tags\///')
fi
echo Will calculate version from "${RESULT}" and "${MAJOR}.${MINOR}.${PATCH} with $COMMIT_COUNT commits since then, and current hash $COMMIT_HASH"

case "$REV" in
  refs/heads/master | refs/heads/fieldworks8-master)
    PRERELEASE="~alpha.${BUILD_NUMBER}"
    ;;

  refs/heads/qa | refs/heads/fieldworks8-qa)
    PRERELEASE="~beta.${BUILD_NUMBER}"
    ;;

  refs/heads/live | refs/heads/fieldworks8-live)
    PRERELEASE=
    ;;

  refs/pull/*)
    PR_NUMBER=$(echo "${REV}" | sed -E 's/^refs\/pull\/([0-9]+)\/merge/\1/')
    PRERELEASE="~PR${PR_NUMBER}.${BUILD_NUMBER}"
    ;;

  refs/heads/*)
    BRANCH=$(echo "${REV##refs/heads/}" | sed 's/\//-/')
    PRERELEASE="~${BRANCH}.${BUILD_NUMBER}"
    ;;

  *)
    echo "Could not determine version number from ${REV}"
    echo "::error ::Could not determine version number from ${REV}"
    exit 1

esac
export DebPackageVersion=${MAJOR}.${MINOR}.${PATCH}${PRERELEASE}
export MsBuildVersion=$(echo "${DebPackageVersion}" | sed 's/~/-/')
echo "Will build package version ${DebPackageVersion}"
echo "::set-output name=DebPackageVersion::${DebPackageVersion}"
echo "::set-output name=MsBuildVersion::${MsBuildVersion}"
echo "::set-output name=MajorMinorPatch::${MajorMinorPatch}"
echo "::set-output name=AssemblySemVer::${AssemblySemVer}"
echo "::set-output name=AssemblySemFileVer::${AssemblySemFileVer}"
echo "::set-output name=InformationalVersion::${InformationalVersion}"
