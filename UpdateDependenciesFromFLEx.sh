#!/bin/bash

# In order to debug dependencies in lfmerge-fdo, build FLEx then run this script to
# copy its relevant libraries in place of the lfmerge-fdo libraries.
# This script assumes that the FieldWorks repo is in ~/fwrepo.
# It copies the needed libraries into the lib and the ${BUILD_CONFIG} folders.

FW_DIR=~/fwrepo/fw

if [ ! -d ${FW_DIR} ]
then
	echo Error: Fieldworks folder not present!
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


cp ${FW_DIR}/Downloads/LibChorus.dll* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/LibChorus.dll* output/${BUILD_CONFIG}/

cp ${FW_DIR}/Downloads/Chorus.exe* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Chorus.exe* output/${BUILD_CONFIG}/

cp ${FW_DIR}/Downloads/ChorusMerge.exe* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ChorusMerge.exe* output/${BUILD_CONFIG}/

# leave TC built library which is signed (strongname)
# cp ${FW_DIR}/Downloads/Palaso*.dll* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Palaso*.dll* output/${BUILD_CONFIG}/

cp ${FW_DIR}/Downloads/SIL.*.dll* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/SIL.*.dll* output/${BUILD_CONFIG}/

cp ${FW_DIR}/Downloads/icu*.dll* lib/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/icu*.dll* output/${BUILD_CONFIG}/


cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/components.map output/${BUILD_CONFIG}/

cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ApplicationTransforms.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Autofac.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/BasicUtils.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/COMInterfaces.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/CoreImpl.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Db4objects.Db4o*.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Enchant.Net.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Ethnologue.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/FDO.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/FormattedEditor.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/HelpSystem.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/HtmlEditor.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ibusdotnet.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ICSharpCode.SharpZipLib.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Interop.MSXML2.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Interop.ResourceDriver.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Ionic.Zip.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/IPCFramework.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/L10NSharp.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/LinqBridge.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/log4net.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Logos.Utility.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Microsoft.Practices.ServiceLocation.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/NDesk.DBus.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/NetLoc.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Paratext.LexicalContracts.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ParatextShared.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/PhonEnvValidator.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/PresentationTransforms.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/protobuf-net.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/QuickGraph.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/ScrUtilsInterfaces.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/SharedScrUtils.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/SilUtils.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/StructureMap.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/taglib-sharp.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Tools.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Utilities.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/Vulcan.Uczniowie.HelpProvider.dll* output/${BUILD_CONFIG}/
cp ${FW_DIR}/Output_x86_64/${BUILD_CONFIG}/XMLUtils.dll* output/${BUILD_CONFIG}/

echo Files copied.


