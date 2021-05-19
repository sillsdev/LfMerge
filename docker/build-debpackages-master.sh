#!/bin/bash -e
echo -e "\033[0;34mBuilding packages for version ${PackageVersion}\033[0m"

#DistributionsToPackage="xenial bionic"
DistributionsToPackage="bionic"

DEBSIGNKEY=BB89B185D63A1DD5

PackageVersion=0.1.0

# Needed in setup.sh from Debian packaging scripts. TODO: Investigate why this environment variable is being removed, and at what point
export USER=root

TRACE()
{
	echo "$@"
	"$@"
}

cd /build/LfMerge

mkdir -p finalresults
rm -f finalresults/*
rm -f lfmerge-*

export MONO_PREFIX=/opt/mono5-sil
RUNMODE="PACKAGEBUILD" BUILD=Release . environ

cd -

for ((curDbVersion=7000072; curDbVersion<=7000072; curDbVersion++)); do
	echo -e "\033[0;34mBuilding package for database version ${curDbVersion}\033[0m"
	cd /build/LfMerge

	echo -e "\033[0;34mPrepare source\033[0m"
	TRACE dotnet gitversion -EnsureAssemblyInfo -UpdateAssemblyInfo
	TRACE msbuild /t:PrepareSource /v:detailed build/LfMerge.proj

	TRACE debian/PrepareSource $curDbVersion

	echo -e "\033[0;34mBuild source package\033[0m"
	TRACE $HOME/ci-builder-scripts/bash/make-source --dists "$DistributionsToPackage" \
		--arches "amd64" --main-package-name "lfmerge" \
		--supported-distros "xenial bionic" --debkeyid $DEBSIGNKEY \
		--build-in-place --package-version "$PackageVersion" --preserve-changelog

	# echo -e "\033[0;34mBuild binary package\033[0m"
	# TRACE $HOME/ci-builder-scripts/bash/build-package --dists "$DistributionsToPackage" \
	# 	--arches "amd64" --main-package-name "lfmerge" \
	# 	--build-in-place --supported-distros "xenial bionic" --debkeyid $DEBSIGNKEY --no-upload

	# cd -
	# mv results/* finalresults/
done
