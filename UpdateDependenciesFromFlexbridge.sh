#!/bin/bash

# In order to debug dependencies in Flexbridge, build Flexbridge then run this script to
# copy its relevant libraries.
# This script assumes that the Flexbridge repo is in ~/fwrepo/flexbridge (or that FB_DIR is
# set prior to running this script)
# It copies the needed libraries into the lib and the ${BUILD_CONFIG} folders.

FB_DIR=${FB_DIR:-$HOME/fwrepo/flexbridge}

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


cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibFLExBridge-ChorusPlugin.{dll*,pdb} lib/ ||
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibFLExBridge-ChorusPlugin.{dll*,pdb} output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibTriboroughBridge-ChorusPlugin.{dll*,pdb} lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibTriboroughBridge-ChorusPlugin.{dll*,pdb} output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LfMergeBridge.{dll*,pdb} lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LfMergeBridge.{dll*,pdb} output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibChorus.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/LibChorus.dll* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Chorus.exe* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/Chorus.exe* output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/ChorusMerge.exe* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/ChorusMerge.exe* output/${BUILD_CONFIG}/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/chorusmerge lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/chorusmerge output/${BUILD_CONFIG}/

cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/SIL*.dll* lib/
cp ${FB_DIR}/output/${BUILD_CONFIG}Mono/SIL*.dll* output/${BUILD_CONFIG}/

echo Files copied.


