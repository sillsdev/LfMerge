#!/bin/bash

# Build Docker image and run unit tests (WIP)
#time docker build -t tmp-lfmerge-builder .
# time docker build -t tmp-lfmerge-builder-fw8 -f fw8-build.dockerfile .

# docker container rm tmp-lfmerge-builder-fw8 || true
# docker run -dit --name tmp-lfmerge-builder-fw8 tmp-lfmerge-builder-fw8 /bin/bash
# rm -rf finalresults || true
# docker container cp tmp-lfmerge-builder-fw8:/home/builder/packages/lfmerge/finalresults finalresults

# Copy the .dsc and accompanying files out of the container, then:
#time sbuild -d xenial lfmerge-7000072_0.1.0-1.dsc --extra-repository='deb http://localhost:3142/linux.lsdev.sil.org/ubuntu xenial main' --extra-repository='deb http://localhost:3142/linux.lsdev.sil.org/ubuntu xenial-experimental main' --extra-repository-key=/home/rmunn/code/LfMerge/docker/sil-packages-testing-key.gpg --extra-repository='deb http://localhost:3142/mirror1.totbb.net/ubuntu xenial universe'
#time sbuild -d bionic lfmerge-7000072_0.1.0-1.dsc --extra-repository='deb http://localhost:3142/linux.lsdev.sil.org/ubuntu bionic main' --extra-repository='deb http://localhost:3142/linux.lsdev.sil.org/ubuntu bionic-experimental main' --extra-repository-key=/home/rmunn/code/LfMerge/docker/sil-packages-testing-key.gpg --extra-repository='deb http://localhost:3142/mirror1.totbb.net/ubuntu bionic universe'

# TODO: Determine whether debian/rules.in BUILD=Release is causing the issue (should it be ReleaseMono?)


# lfmerge  lfmerge-7000068_0.1.0-1.debian.tar.xz  lfmerge-7000068_0.1.0-1.dsc  lfmerge-7000068_0.1.0-1_source.build  lfmerge-7000068_0.1.0-1_source.buildinfo  lfmerge-7000068_0.1.0-1_source.changes  lfmerge-7000068_0.1.0.orig.tar.xz



# This seems to work:
time docker build -t tmp-lfmerge-builder-fw8 -f fw8-build.dockerfile .

docker container rm tmp-lfmerge-builder-fw8 || true
docker run -dit --name tmp-lfmerge-builder-fw8 tmp-lfmerge-builder-fw8 /bin/bash
rm -rf finalresults || true
docker container cp tmp-lfmerge-builder-fw8:/home/builder/packages/lfmerge/finalresults finalresults
docker container stop tmp-lfmerge-builder-fw8 || true
cd finalresults
# for distro in xenial bionic focal; do
for distro in xenial; do
    echo sbuild -d ${distro} lfmerge-*.dsc --extra-repository="deb http://localhost:3142/linux.lsdev.sil.org/ubuntu ${distro} main" --extra-repository="deb http://localhost:3142/linux.lsdev.sil.org/ubuntu ${distro}-experimental main" --extra-repository-key=/home/rmunn/code/LfMerge/docker/sil-packages-testing-key.gpg --extra-repository="deb http://localhost:3142/mirror1.totbb.net/ubuntu ${distro} universe"
	time sbuild -d ${distro} lfmerge-*.dsc --extra-repository="deb http://localhost:3142/linux.lsdev.sil.org/ubuntu ${distro} main" --extra-repository="deb http://localhost:3142/linux.lsdev.sil.org/ubuntu ${distro}-experimental main" --extra-repository-key=/home/rmunn/code/LfMerge/docker/sil-packages-testing-key.gpg --extra-repository="deb http://localhost:3142/mirror1.totbb.net/ubuntu ${distro} universe"
done
