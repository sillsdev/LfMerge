#!/bin/bash -e

echo -e "\033[0;34mBuilding packages for version ${DebPackageVersion} (inserted as ${Version} in .NET AssemblyInfo files)\033[0m"

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

# for ((curDbVersion=7000068; curDbVersion<=7000070; curDbVersion++)); do
	echo -e "\033[0;34mBuilding package for database version ${curDbVersion}\033[0m"

	echo -e "\033[0;34mPrepare source\033[0m"
	git clean -dxf --exclude=packages/ --exclude=lib/
	git reset --hard

	if [ -n "$UPDATE_ASSEMBLYINFO_BY_SCRIPT" -a "$UPDATE_ASSEMBLYINFO_BY_SCRIPT" -ne 0 ]; then
		find src -name AssemblyInfo.cs -path '*LfMerge*' -print0 | xargs -0 -n 1 ${HOME}/packages/lfmerge/docker/scripts/update-assemblyinfo.sh
	fi

	cat > NuGet.Config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="TeamCity" value="http://build.palaso.org/guestAuth/app/nuget/v1/FeedService.svc/" />
  </packageSources>
</configuration>
EOF

	TRACE /opt/mono5-sil/bin/msbuild /t:PrepareSource /v:detailed build/LfMerge.proj

	TRACE debian/PrepareSource $curDbVersion

	echo -e "\033[0;34mBuild source package\033[0m"
	TRACE $HOME/ci-builder-scripts/bash/make-source --dists "$DistributionsToPackage" \
		--arches "amd64" --main-package-name "lfmerge" --source-code-subdir "." \
		--supported-distros "xenial bionic" --debkeyid $DEBSIGNKEY \
		--package-version "$DebPackageVersion" --preserve-changelog

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
echo Building packages from .dsc file: lfmerge*dsc
DSC=$(ls -1 lfmerge*dsc)
SOURCE=$(grep '^Source: ' "${DSC}" | cut -c9-)
PKGVERSION=$(grep '^Version: ' "${DSC}" | cut -c10- | sed -e 's/-[0-9]\+$//')
# Build dependency pseudo-package, install it, and immediately delete the file, in one command
sudo mk-build-deps -r -i $DSC -t 'apt-get -y -o Debug::pkgProblemResolver=yes --no-install-recommends'
# Extract source
dpkg-source -x $DSC
ls -l
echo Should be a directory named "${SOURCE}-${PKGVERSION}"
cd "${SOURCE}-${PKGVERSION}"
# Build package, preserving env vars used in MsBuild packaging
debuild -rfakeroot --preserve-envvar Version --preserve-envvar AssemblyVersion --preserve-envvar FileVersion --preserve-envvar InformationalVersion
# Built packages are placed in parent directory, so move them into finalresults for artifact collection
ls -l *deb || true
ls -l ../*deb || true
cp ../*deb . || true
pwd
