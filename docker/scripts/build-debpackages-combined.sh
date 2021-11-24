#!/bin/bash -e
ls -l gitversion.properties || echo no gitversion.properties found in build-debpackages.sh
. gitversion.properties
echo -e "\033[0;34mBuilding packages for version ${PackageVersion}\033[0m"

#DistributionsToPackage="xenial bionic"
DistributionsToPackage="bionic"

DEBSIGNKEY=BB89B185D63A1DD5

# Needed in setup.sh from Debian packaging scripts. TODO: Investigate why this environment variable is being removed, and at what point
export USER=root

TRACE()
{
	echo "$@"
	"$@"
}

curDbVersion=$1

cd ${HOME}/packages/lfmerge

mkdir -p finalresults
rm -f finalresults/*
rm -f lfmerge-*

export MONO_PREFIX=/opt/mono5-sil
RUNMODE="PACKAGEBUILD" BUILD=Release . environ

cd -

# for ((curDbVersion=7000068; curDbVersion<=7000070; curDbVersion++)); do
	echo -e "\033[0;34mBuilding package for database version ${curDbVersion}\033[0m"

	echo -e "\033[0;34mPrepare source\033[0m"
	git clean -dxf --exclude=packages/ --exclude=lib/
	git reset --hard

	cat > NuGet.Config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="TeamCity" value="http://build.palaso.org/guestAuth/app/nuget/v1/FeedService.svc/" />
  </packageSources>
</configuration>
EOF

	TRACE dotnet tool restore
	TRACE dotnet gitversion -EnsureAssemblyInfo -UpdateAssemblyInfo
	TRACE /opt/mono5-sil/bin/msbuild /t:PrepareSource /v:detailed build/LfMerge.proj

	TRACE debian/PrepareSource $curDbVersion

	echo -e "\033[0;34mBuild source package\033[0m"
	TRACE $HOME/ci-builder-scripts/bash/make-source --dists "$DistributionsToPackage" \
		--arches "amd64" --main-package-name "lfmerge" --source-code-subdir "." \
		--supported-distros "xenial bionic" --debkeyid $DEBSIGNKEY \
		--package-version "$PackageVersion" --preserve-changelog

	# echo -e "\033[0;34mBuild binary package\033[0m"
	# TRACE $HOME/ci-builder-scripts/bash/build-package --dists "$DistributionsToPackage" \
	# 	--arches "amd64" --main-package-name "lfmerge" \
	# 	--build-in-place --supported-distros "xenial bionic" --debkeyid $DEBSIGNKEY --no-upload

	# cd -
	# mv results/* finalresults/
	pwd
	ls -l ..
	mkdir -p finalresults/
	mv ../lfmerge-${curDbVersion}* finalresults/
# done
ls -lR finalresults

# Now build binaries right here
cd finalresults
# TODO: Determine if "apt install -y equivs" is needed
sudo apt update
sudo apt install -y equivs
echo lfmerge*dsc
DSC=$(ls -1 lfmerge*dsc)
SOURCE=$(grep '^Source: ' "${DSC}" | cut -c9-)
echo $SOURCE
PKGVERSION=$(grep '^Version: ' "${DSC}" | cut -c10- | sed -e 's/-[0-9]\+$//')
# PKGVERSION=${PackageVersion}
sudo mk-build-deps -r -i $DSC -t 'apt-get -y -o Debug::pkgProblemResolver=yes --no-install-recommends'
dpkg-source -x $DSC
ls -l
cd "${SOURCE}-${PKGVERSION}"
debuild -rfakeroot
# TODO: Determine whether packages are actually built in parent directory or this one, then remove unneeded lines below
ls *deb || true
ls ../*deb || true
cp ../*deb . || true
