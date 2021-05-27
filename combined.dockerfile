# syntax=docker/dockerfile:experimental
ARG DbVersion=7000072

FROM sillsdev/web-languageforge:app-latest AS lf-build
# No changes needed, LF app result in /var/www/html

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS lfmerge-builder-base
WORKDIR /build/lfmerge

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN apt-get update && apt-get install -y gnupg

COPY docker/sil-packages-key.gpg .
COPY docker/sil-packages-testing-key.gpg .
RUN apt-key add sil-packages-key.gpg
RUN apt-key add sil-packages-testing-key.gpg
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic main' > /etc/apt/sources.list.d/llso-experimental.list
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic-experimental main' >> /etc/apt/sources.list.d/llso-experimental.list
# Dependencies from Debian "control" file
RUN apt-get update && apt-get install -y sudo debhelper devscripts cli-common-dev cpp python-dev pkg-config mono5-sil mono5-sil-msbuild libicu-dev lfmerge-fdo

# # Build as a non-root user
RUN useradd -d /home/builder -g users -G www-data,fieldworks,systemd-journal -m -s /bin/bash builder ; \
    echo "builder ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers; \
	chown -R builder:users /build

USER builder
RUN mkdir -p /home/builder/.gnupg /home/builder/ci-builder-scripts/bash /home/builder/packages

# Any setup unique to the various builds goes in one of these four images
FROM lfmerge-builder-base AS lfmerge-build-7000068
ENV GitBranch="bugfix/send-receive-branch-format-change-fw8"
ENV GitPatch="remove-GitVersionTask-fw8.targets.patch"
ENV DbVersion=7000068
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000068

FROM lfmerge-builder-base AS lfmerge-build-7000069
ENV GitBranch="bugfix/send-receive-branch-format-change-fw8"
ENV GitPatch="remove-GitVersionTask-fw8.targets.patch"
ENV DbVersion=7000069
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000069

FROM lfmerge-builder-base AS lfmerge-build-7000070
ENV GitBranch="bugfix/send-receive-branch-format-change-fw8"
ENV GitPatch="remove-GitVersionTask-fw8.targets.patch"
ENV DbVersion=7000070
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000070

FROM lfmerge-builder-base AS lfmerge-build-7000072
ENV GitBranch="docker-build"
ENV GitPatch="remove-GitVersionTask.targets.patch"
ENV DbVersion=7000072
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000072
ENV NUGET_PACKAGES=/storage/nuget

FROM lfmerge-build-${DbVersion} AS lfmerge-build

USER builder
RUN mkdir -p /home/builder/.gnupg /home/builder/ci-builder-scripts/bash /home/builder/packages/lfmerge
WORKDIR /home/builder/packages/lfmerge
ENV MONO_PREFIX=/opt/mono5-sil

COPY --chown=builder:users .git .git/
RUN git checkout "${GitBranch}"
RUN git clean -dxf --exclude=packages/
RUN git reset --hard

# Instead of downloading FLExBridge DLLs which have vanished from TeamCity, store them in the Docker image
ADD docker/fw8-flexbridge.tar.xz lib/

# TODO: Consider running a package restore here so that it's cached in the build image instead of having to run in the container each time
# E.g., call download-dependencies-combined.sh at this point

COPY --chown=builder:users docker/scripts .
COPY --chown=builder:users .config/dotnet-tools.json .config/dotnet-tools.json
# Our packaging shell scripts expect to live under /home/builder/ci-builder-scripts/bash
# COPY --chown=builder:users [ "docker/common.sh", "docker/setup.sh", "docker/sbuildrc", "docker/build-package", "docker/make-source", "/home/builder/ci-builder-scripts/bash/" ]
COPY --chown=builder:users [ "docker/common.sh", "docker/make-source", "/home/builder/ci-builder-scripts/bash/" ]

# LanguageForge repo expected to be in (will be copied into ./data/php/src before running unit tests)
COPY --chown=builder:users --from=lf-build /var/www/html /var/www/html

# Remove GitVersionTask which doesn't work well on modern Debian and replace with dotnet-based GitVersion
COPY --chown=builder:users "docker/${GitPatch}" .
RUN echo ${GitPatch}; git apply "${GitPatch}"
RUN echo In build, DB version is ${DbVersion}

# RUN --mount=type=tmpfs,target=/tmp ./build-and-test.sh ${DbVersion}

CMD ./build-and-test.sh ${DbVersion}
