FROM python:2 AS mercurial-build

WORKDIR /build
RUN hg clone -r 3.3 https://www.mercurial-scm.org/repo/hg/
WORKDIR /build/hg
RUN make local
# Result in /build/hg/mercurial

FROM sillsdev/web-languageforge:app-latest AS lf-build
# No changes needed, LF app result in /var/www/html

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS lfmerge-builder
WORKDIR /build/lfmerge

RUN apt-get update && apt-get install -y gnupg

COPY docker/sil-packages-key.gpg .
COPY docker/sil-packages-testing-key.gpg .
RUN apt-key add sil-packages-key.gpg
RUN apt-key add sil-packages-testing-key.gpg
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic main' > /etc/apt/sources.list.d/llso-experimental.list
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic-experimental main' >> /etc/apt/sources.list.d/llso-experimental.list
RUN apt-get update && apt-get install -y mono4-sil mono5-sil cpp libgit2-dev mercurial
# Dependencies from Debian "control" file
RUN apt-get install -y mono5-sil-msbuild sudo debhelper cli-common-dev iputils-ping wget mercurial python-dev php-dev php-pear pkg-config mono5-sil mono5-sil-msbuild libicu-dev lfmerge-fdo

FROM tmp-lfmerge-builder:lfmerge-builder-base AS lfmerge-build
ARG DbVersion=7000068
WORKDIR /build/lfmerge

RUN apt-get update && apt-get install -y gnupg

COPY docker/sil-packages-key.gpg .
COPY docker/sil-packages-testing-key.gpg .
RUN apt-key add sil-packages-key.gpg
RUN apt-key add sil-packages-testing-key.gpg
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic main' > /etc/apt/sources.list.d/llso-experimental.list
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic-experimental main' >> /etc/apt/sources.list.d/llso-experimental.list
RUN apt-get update && apt-get install -y mono4-sil mono5-sil cpp libgit2-dev mercurial
# Dependencies from Debian "control" file
RUN apt-get install -y mono5-sil-msbuild debhelper devscripts cli-common-dev iputils-ping wget mercurial python-dev php-dev php-pear pkg-config mono5-sil mono5-sil-msbuild libicu-dev lfmerge-fdo

# Build as a non-root user
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get install -y --no-install-recommends adduser; \
    adduser builder --disabled-password --gecos ""; \
    echo "builder ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers; \
	chown -R builder:users /build

USER builder
RUN mkdir -p /home/builder/.gnupg /home/builder/ci-builder-scripts/bash /home/builder/packages/lfmerge
WORKDIR /home/builder/packages/lfmerge
ENV MONO_PREFIX=/opt/mono5-sil

COPY --chown=builder:users .git .git/
RUN git checkout bugfix/send-receive-branch-format-change-fw8
RUN git clean -dxf --exclude=packages/
RUN git reset --hard

# Instead of downloading FLExBridge DLLs which have vanished from TeamCity, store them in the Docker image
ADD docker/fw8-flexbridge.tar.xz lib/

# Remove GitVersionTask which doesn't work well on modern Debian and replace with dotnet-based GitVersion
COPY --chown=builder:users docker/remove-GitVersionTask-fw8.targets.patch .
RUN git apply remove-GitVersionTask-fw8.targets.patch

COPY --chown=builder:users .config/dotnet-tools.json .config/dotnet-tools.json
COPY --chown=builder:users docker/gitversion-for-fw8-build.sh .
RUN ./gitversion-for-fw8-build.sh

COPY --chown=builder:users docker/download-dependencies-fw8-build.sh ./
RUN ./download-dependencies-fw8-build.sh

# COPY --chown=builder:users Mercurial Mercurial
# COPY --chown=builder:users MercurialExtensions MercurialExtensions

# LanguageForge repo expected to be in ./data/php/src
COPY --chown=builder:users --from=lf-build /var/www/html ./data/php/src
# Mercurial repo expected to be in ./Mercurial
COPY --chown=builder:users --from=mercurial-build /build/hg/mercurial ./Mercurial/mercurial

# RUN dotnet build /t:PrepareSource /v:detailed build/LfMerge.proj
# RUN debian/PrepareSource 7000070

RUN mkdir -p /usr/lib/lfmerge/7000070

COPY --chown=builder:users docker/compile-lfmerge-fw8.sh .
RUN ./compile-lfmerge-fw8.sh
RUN ln -sf ../Mercurial output/

# Our packaging shell scripts expect to live under /home/builder/ci-builder-scripts/bash
COPY --chown=builder:users [ "docker/common.sh", "docker/setup.sh", "docker/sbuildrc", "docker/build-package", "docker/make-source", "/home/builder/ci-builder-scripts/bash/" ]
COPY --chown=builder:users docker/build-debpackages-fw8.sh .
# CMD [ "/build/lfmerge/build-debpackages-fw8.sh" ]
RUN ./build-debpackages-fw8.sh
CMD [ "/bin/bash" ]

# WORKDIR /build/lfmerge/output/Release/net462
# RUN PATH="${PATH}:/opt/mono5-sil/bin" dotnet test -f net462 *Test*.dll
#RUN PATH="${PATH}:/opt/mono5-sil/bin" dotnet test -f net462 #*Test*.dll

# NOTE: Remnants of previous attempt can be seen below. Will delete them once this is working. 2021-05 RM

# FROM alpine AS git
# RUN apk --update add git mercurial && \
#     rm -rf /var/lib/apt/lists/* && \
#     rm /var/cache/apk/*
# WORKDIR /git
# RUN git clone --depth=1 https://github.com/sillsdev/chorus/
# RUN git clone --depth=1 https://github.com/sillsdev/libpalaso/
# RUN git clone --depth=1 https://github.com/sillsdev/FieldWorks/
# RUN git clone --depth=1 https://github.com/sillsdev/web-languageforge/
# RUN git clone --depth=1 https://github.com/sillsdev/LfMerge/
# # RUN hg clone -r 3.3 https://www.mercurial-scm.org/repo/hg/
# ADD https://www.mercurial-scm.org/repo/hg/archive/3.3.tar.gz .

# FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
# # Debian 10 (Buster)

# WORKDIR /git/LfMerge
# COPY ./*.sh .

# COPY --from=git /git /git

# RUN ./set-build-number.sh
# RUN ./compile-mercurial.sh
# # Install composer and initialize LF php code
# RUN ./composer-for-lf.sh
# RUN ./download-lfmerge-dependencies.sh

# # if (branchName.split('-').first() == "fieldworks8") {
# # Only needed for Mono 3.x
# RUN ./compile-lfmerge.sh
