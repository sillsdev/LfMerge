#!/bin/bash

export DbVersion="${1-7000072}"
echo "Building for ${DbVersion}"
sudo mkdir -p /usr/lib/lfmerge/${DbVersion}

./gitversion-combined.sh ${DbVersion}
./download-dependencies-combined.sh ${DbVersion}

./compile-lfmerge-combined.sh ${DbVersion}

if [ -n "$RUN_UNIT_TESTS" ]; then
    rm -rf ./data/php
    mkdir -p ./data/php
    cp -a /var/www/html ./data/php/src
    ./test-lfmerge-combined.sh ${DbVersion}
fi

rm -rf ./data/php   # So it doesn't get into the .deb source package

./build-debpackages-combined.sh ${DbVersion}
