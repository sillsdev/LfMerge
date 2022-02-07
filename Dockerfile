# syntax=docker/dockerfile:experimental
ARG DbVersion=7000072

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
RUN apt-get update && apt-get install -y sudo debhelper devscripts cli-common-dev iputils-ping cpp python-dev pkg-config mono5-sil mono5-sil-msbuild libicu-dev lfmerge-fdo

# # Build as a non-root user
RUN useradd -u 1001 -d /home/builder -g users -G www-data,fieldworks,systemd-journal -m -s /bin/bash builder ; \
    echo "builder ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers; \
	chown -R builder:users /build

# Any setup unique to the various builds goes in one of these four images
FROM lfmerge-builder-base AS lfmerge-build-7000068
ENV DbVersion=7000068
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000068
ENV RUN_UNIT_TESTS=0
ENV UPDATE_ASSEMBLYINFO_BY_SCRIPT=1
ENV NUNIT_VERSION_MAJOR=2
# To run specific unit tests, set TEST_SPEC env var, e.g.:
# ENV TEST_SPEC=LfMerge.Core.Tests.Actions.SynchronizeActionTests.SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync

FROM lfmerge-builder-base AS lfmerge-build-7000069
ENV DbVersion=7000069
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000069
ENV RUN_UNIT_TESTS=0
ENV UPDATE_ASSEMBLYINFO_BY_SCRIPT=1
ENV NUNIT_VERSION_MAJOR=2
# ENV TEST_SPEC=LfMerge.Core.Tests.Actions.SynchronizeActionTests.SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync

FROM lfmerge-builder-base AS lfmerge-build-7000070
ENV DbVersion=7000070
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000070
ENV RUN_UNIT_TESTS=0
ENV UPDATE_ASSEMBLYINFO_BY_SCRIPT=1
ENV NUNIT_VERSION_MAJOR=2
# ENV TEST_SPEC=LfMerge.Core.Tests.Actions.SynchronizeActionTests.SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync

FROM lfmerge-builder-base AS lfmerge-build-7000072
ENV DbVersion=7000072
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000072
ENV RUN_UNIT_TESTS=0
ENV UPDATE_ASSEMBLYINFO_BY_SCRIPT=0
ENV NUNIT_VERSION_MAJOR=3
# ENV TEST_SPEC=LfMerge.Core.Tests.Actions.SynchronizeActionTests.SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync

FROM lfmerge-build-${DbVersion} AS lfmerge-build

USER builder

# Git repo should be mounted under ${HOME}/packages/lfmerge when run
# E.g., `docker run --mount type=bind,source="$(pwd)",target=/home/builder/packages/lfmerge`
RUN mkdir -p /home/builder/repo /home/builder/packages/lfmerge
CMD /home/builder/repo/docker/scripts/build-and-test.sh ${DbVersion}
# CMD doesn't actually run the script, it just gives `docker run` a default.
# So it's okay for the Git repo to not be mounted yet.
