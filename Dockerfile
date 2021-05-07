FROM python:2 AS mercurial-build

WORKDIR /build
RUN hg clone -r 3.3 https://www.mercurial-scm.org/repo/hg/
WORKDIR /build/hg
RUN make local
# Result in /build/hg/mercurial

FROM sillsdev/web-languageforge:app-latest AS lf-build
# No changes needed, LF app result in /var/www/html

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS lfmerge-build
WORKDIR /build/LfMerge

RUN apt-get update && apt-get install -y gnupg

COPY docker/sil-packages-key.gpg .
COPY docker/sil-packages-testing-key.gpg .
RUN apt-key add sil-packages-key.gpg
RUN apt-key add sil-packages-testing-key.gpg
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic main' > /etc/apt/sources.list.d/llso-experimental.list
RUN echo 'deb http://linux.lsdev.sil.org/ubuntu bionic-experimental main' >> /etc/apt/sources.list.d/llso-experimental.list
RUN apt-get update && apt-get install -y mono4-sil mono5-sil
RUN apt-get update && apt-get install -y cpp libgit2-dev

COPY .git .git/
RUN git checkout docker-build
RUN git clean -dxf --exclude=packages/
RUN git reset --hard

COPY Mercurial Mercurial
COPY MercurialExtensions MercurialExtensions

# LanguageForge repo expected to be in ./data/php/src
COPY --from=lf-build /var/www/html ./data/php/src
# Mercurial repo expected to be in ./Mercurial
COPY --from=mercurial-build /build/hg/mercurial ./Mercurial/mercurial

RUN dotnet tool restore
RUN dotnet gitversion -EnsureAssemblyInfo -UpdateAssemblyInfo
RUN dotnet build /t:PrepareSource /v:detailed build/LfMerge.proj
RUN debian/PrepareSource 7000072

RUN mkdir -p /usr/lib/lfmerge/7000072
COPY docker/compile-lfmerge.sh .
RUN ./compile-lfmerge.sh
RUN ln -sf ../Mercurial output/

RUN apt-get update && apt-get install -y mercurial

RUN PATH="${PATH}:/opt/mono5-sil/bin" dotnet test -f net462 output/Release/net462/*Tests*

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
