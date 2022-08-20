# syntax=docker/dockerfile:experimental
ARG DbVersion=7000072

FROM ghcr.io/sillsdev/lfmerge-base:sdk AS lfmerge-builder-base

ENV DEFAULT_BUILDER_UID=1000
ARG BUILDER_UID
RUN test -n "$BUILDER_UID"
ENV BUILDER_UID="$BUILDER_UID"

# # Build as a non-root user
RUN useradd -u "${BUILDER_UID:-DEFAULT_BUILDER_UID}" -d /home/builder -g users -G www-data,fieldworks -m -s /bin/bash builder ; \
    echo "builder ALL=(ALL) NOPASSWD: ALL" >> /etc/sudoers; \
	chown -R builder:users /build

# Any setup unique to one specific DbVersion build goes in one of the images below

FROM lfmerge-builder-base AS lfmerge-build-7000072
ENV DbVersion=7000072
ENV DBVERSIONPATH=/usr/lib/lfmerge/7000072
ENV RUN_UNIT_TESTS=0
ENV NUNIT_VERSION_MAJOR=3
# ENV TEST_SPEC=LfMerge.Core.Tests.Actions.SynchronizeActionTests.SynchronizeAction_CustomReferenceAtomicField_DoesNotThrowExceptionDuringSync

FROM lfmerge-build-${DbVersion} AS lfmerge-build

# So unit tests can run
RUN mkdir -p /var/lib/languageforge/lexicon/sendreceive/ \
	&& cd /var/lib/languageforge/lexicon/sendreceive/ && mkdir Templates state editqueue syncqueue webwork && cd - \
	&& chown -R builder:users /var/lib/languageforge/lexicon/sendreceive

USER builder
WORKDIR /home/builder/repo
# Git repo should be mounted under /home/builder/repo when run
# E.g., `docker run --mount type=bind,source="$(pwd)",target=/home/builder/repo

# NuGet package cache dir should be bind-mounted from home of running user
# E.g., `docker run --mount type=bind,source="${HOME}/.nuget/packages",target=/home/builder/.nuget/packages
RUN mkdir -p /home/builder/.nuget/packages

CMD docker/scripts/build-and-test.sh ${DbVersion}
# CMD doesn't actually run the script, it just gives `docker run` a default.
# So it's okay for the Git repo to not be mounted yet.
