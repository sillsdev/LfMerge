#!/bin/bash

# In order to debug dependencies in Flexbridge, build Flexbridge then run this script to
# copy its relevant libraries.
# This script assumes that the Flexbridge repo is in ~/fwrepo/flexbridge.
# It copies the needed libraries into the lib and the ${BUILD_CONFIG} folders.

FB_DIR=~/fwrepo/flexbridge

if [ ! -d ${FB_DIR} ]
then
	echo Error: Flexbridge folder not present!
	exit
fi

if [ "$1"=="" ]
then
	BUILD_CONFIG=Debug
else
	BUILD_CONFIG=$1
fi

if [ ! -d output/${BUILD_CONFIG} ]
then
	mkdir -p output/${BUILD_CONFIG}
fi


cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibFLExBridge-ChorusPlugin.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibFLExBridge-ChorusPlugin.dll* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibChorus.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibChorus.dll* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Chorus.exe* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Chorus.exe* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/ChorusMerge.exe* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/ChorusMerge.exe* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Palaso*.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Palaso*.dll* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/SIL.*.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/SIL.*.dll* output/${BUILD_CONFIG}/

echo Files copied.


