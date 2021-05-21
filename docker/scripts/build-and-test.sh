#!/bin/bash

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"
mkdir -p /usr/lib/lfmerge/${DbVersion}

./gitversion-combined.sh ${DbVersion}
./download-dependencies-combined.sh ${DbVersion}

./compile-lfmerge-combined.sh ${DbVersion}
# TODO: Conditionally run if RUN_UNIT_TESTS is present and non-empty
# rm -rf ./data/php
# mkdir -p ./data/php
# cp -a /var/www/html ./data/php/src
# ./test-lfmerge-combined.sh ${DbVersion}
# rm -rf ./data/php   # So it doesn't get into the .deb source package
./build-debpackages-combined.sh ${DbVersion}
