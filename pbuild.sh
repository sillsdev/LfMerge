#!/bin/bash

which parallel >/dev/null || (echo 'Please run "sudo apt-get install parallel" and try again.'; exit 1)

# Clean up any previous builds
for f in 68 69 70 72; do
# for f in 72; do
    # Can safely ignore "container doesn't exist" as that's not an error
    docker container rm tmp-lfmerge-build-70000${f} >/dev/null 2>/dev/null || true
done

# First create the base build container ONCE (not in parallel), to ensure that the slow steps (apt-get install mono5-sil) are cached
docker build -t lfmerge-builder-base --target lfmerge-builder-base -f combined.dockerfile .

# Create the build containers in series, because I've had trouble when creating them in parallel
# (To create the build containers in series, which might be necessary if you have trouble with
# Docker caching while creating them in parallel, just comment out the "time parallel" and "EOF" lines)
time parallel --no-notice <<EOF
docker build --build-arg DbVersion=7000068 -t lfmerge-build-7000068 -f combined.dockerfile .
docker build --build-arg DbVersion=7000069 -t lfmerge-build-7000069 -f combined.dockerfile .
docker build --build-arg DbVersion=7000070 -t lfmerge-build-7000070 -f combined.dockerfile .
docker build --build-arg DbVersion=7000072 -t lfmerge-build-7000072 -f combined.dockerfile .
EOF

# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker build --build-arg DbVersion=7000072 -t lfmerge-build-7000072 -f combined.dockerfile .

# Run the build
time parallel --no-notice <<EOF
docker run --mount type=tmpfs,dst=/tmp --name tmp-lfmerge-build-7000068 lfmerge-build-7000068
docker run --mount type=tmpfs,dst=/tmp --name tmp-lfmerge-build-7000069 lfmerge-build-7000069
docker run --mount type=tmpfs,dst=/tmp --name tmp-lfmerge-build-7000070 lfmerge-build-7000070
docker run --mount type=tmpfs,dst=/tmp --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072
EOF

# To run a single build instead, comment out the block above and uncomment the next line (and change 72 to 68/69/70 if needed)
# docker run --mount type=bind,src=/storage/nuget,dst=/storage/nuget --name tmp-lfmerge-build-7000072 lfmerge-build-7000072

# Collect results
for f in 68 69 70 72; do
# for f in 72; do
    docker container cp tmp-lfmerge-build-70000${f}:/home/builder/packages/lfmerge/finalresults ./
done
