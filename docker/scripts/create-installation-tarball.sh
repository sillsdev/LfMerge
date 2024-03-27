#!/bin/bash

set -e

# Uncomment this to turn on verbose mode.
#export DH_VERBOSE=1

export HOME=/tmp
export XDG_CONFIG_HOME=/tmp/.config
export BUILD=Release
export FRAMEWORK=net8.0
export NETSTANDARD=netstandard2.0

export DatabaseVersion=${1:-7000072}

# Model version dependent DESTDIR
export DESTROOT=tarball
export DBDESTDIR=${DESTROOT}/lfmerge-${DatabaseVersion}
# Common DESTDIR
export COMMONDESTDIR=${DESTROOT}/lfmerge
export LIB=usr/lib/lfmerge/${DatabaseVersion}
export SHARE=usr/share/lfmerge/${DatabaseVersion}
export NATIVERUNTIME=runtimes/linux-x64/native
export LIBRUNTIME=runtimes/linux-x64/lib
export DBVERSIONPATH=/usr/lib/lfmerge/${DatabaseVersion}

# Sanity check before doing anything destructive
if [ -d output/${BUILD}/${FRAMEWORK}/ -a -e output/${BUILD}/${FRAMEWORK}/LfMerge.Core.dll ]
then
	echo "Copying installation files into ${DESTROOT}"
else
	echo "Build appears to have failed; not touching ${DESTROOT}"
	exit 1
fi

# Apparently the downloaded mercurial.ini doesn't have the right fixutf8 config, and it also
# has wrong line endings, so we re-create the entire file
cat >Mercurial/mercurial.ini <<EOF
[extensions]
eol=
hgext.graphlog=
convert=
fixutf8=/${LIB}/MercurialExtensions/fixutf8/fixutf8.py
EOF

# Install binaries
install -d ${DBDESTDIR}/${LIB}
install -m 644 output/${BUILD}/${FRAMEWORK}/*.* ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 644 output/${BUILD}/*.* ${DBDESTDIR}/${LIB}
install -m 644 output/${BUILD}/${FRAMEWORK}/${NATIVERUNTIME}/*.* ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 644 output/${BUILD}/${NATIVERUNTIME}/*.* ${DBDESTDIR}/${LIB}
install -m 644 output/${BUILD}/${FRAMEWORK}/${LIBRUNTIME}/${NETSTANDARD}/*.* ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 644 output/${BUILD}/${LIBRUNTIME}/${NETSTANDARD}/*.* ${DBDESTDIR}/${LIB}
install -m 755 output/${BUILD}/${FRAMEWORK}/LfMerge ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 755 output/${BUILD}/LfMerge ${DBDESTDIR}/${LIB}
install -m 755 output/${BUILD}/${FRAMEWORK}/LfMergeQueueManager ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 755 output/${BUILD}/LfMergeQueueManager ${DBDESTDIR}/${LIB}
install -m 755 output/${BUILD}/${FRAMEWORK}/chorusmerge ${DBDESTDIR}/${LIB} 2>/dev/null || install -m 755 output/${BUILD}/chorusmerge ${DBDESTDIR}/${LIB}
chmod 755 ${DBDESTDIR}/${LIB}/FixFwData.exe
install -d ${DBDESTDIR}/${LIB}/Mercurial
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgdemandimport
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgext
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgext/convert
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgext/highlight
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgext/largefiles
install -d ${DBDESTDIR}/${LIB}/Mercurial/hgext/zeroconf
install -d ${DBDESTDIR}/${LIB}/Mercurial/mercurial
install -d ${DBDESTDIR}/${LIB}/Mercurial/mercurial/hgweb
install -d ${DBDESTDIR}/${LIB}/Mercurial/mercurial/httpclient || true
install -d ${DBDESTDIR}/${LIB}/MercurialExtensions
install -d ${DBDESTDIR}/${LIB}/MercurialExtensions/fixutf8
install -m 755 Mercurial/hg ${DBDESTDIR}/${LIB}/Mercurial
install -m 644 Mercurial/mercurial.ini ${DBDESTDIR}/${LIB}/Mercurial
install -m 644 Mercurial/hgdemandimport/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgdemandimport
install -m 644 Mercurial/hgext/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgext
install -m 644 Mercurial/hgext/convert/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgext/convert
install -m 644 Mercurial/hgext/highlight/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgext/highlight
install -m 644 Mercurial/hgext/largefiles/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgext/largefiles
install -m 644 Mercurial/hgext/zeroconf/*.* ${DBDESTDIR}/${LIB}/Mercurial/hgext/zeroconf
install -m 644 Mercurial/mercurial/*.* ${DBDESTDIR}/${LIB}/Mercurial/mercurial
install -m 644 Mercurial/mercurial/hgweb/*.* ${DBDESTDIR}/${LIB}/Mercurial/mercurial/hgweb
install -m 644 Mercurial/mercurial/httpclient/*.* ${DBDESTDIR}/${LIB}/Mercurial/mercurial/httpclient || true
install -m 644 MercurialExtensions/fixutf8/*.* ${DBDESTDIR}/${LIB}/MercurialExtensions/fixutf8
install -d ${DBDESTDIR}/${LIB}/runtimes
install -d ${DBDESTDIR}/${LIB}/runtimes/linux-x64
install -d ${DBDESTDIR}/${LIB}/runtimes/linux-x64/lib
install -d ${DBDESTDIR}/${LIB}/runtimes/linux-x64/lib/${NETSTANDARD}
install -d ${DBDESTDIR}/${LIB}/runtimes/linux-x64/native
install -m 644 output/${BUILD}/${FRAMEWORK}/runtimes/linux-x64/lib/${NETSTANDARD}/*.* ${DBDESTDIR}/${LIB}/runtimes/linux-x64/lib/${NETSTANDARD} 2>/dev/null || install -m 644 output/${BUILD}/runtimes/linux-x64/lib/${NETSTANDARD}/*.* ${DBDESTDIR}/${LIB}/runtimes/linux-x64/lib/${NETSTANDARD}
install -m 644 output/${BUILD}/${FRAMEWORK}/runtimes/linux-x64/native/*.* ${DBDESTDIR}/${LIB}/runtimes/linux-x64/native 2>/dev/null || install -m 644 output/${BUILD}/runtimes/linux-x64/native/*.* ${DBDESTDIR}/${LIB}/runtimes/linux-x64/native
# Remove unit test related files
(cd ${DBDESTDIR}/${LIB} && \
	rm -f *.Tests.dll* *.Tests.pdb* *.TestApp.exe* SIL.TestUtilities.dll* \
		SIL.TestUtilities.pdb* nunit.framework.dll *Moq.dll)
# Install environ file
install -d ${DBDESTDIR}/${SHARE}
install -m 644 environ ${DBDESTDIR}/${SHARE}
# Install wrapper scripts
install -d ${COMMONDESTDIR}/usr/bin
install -m 755 lfmerge ${COMMONDESTDIR}/usr/bin
install -m 755 lfmergeqm ${COMMONDESTDIR}/usr/bin
install -m 755 startlfmerge ${DBDESTDIR}/${LIB}
# Install conf file
install -d ${COMMONDESTDIR}/etc/languageforge/conf
# Create working directories
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/state
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/webwork
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/mergequeue
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/commitqueue
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/receivequeue
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/sendqueue
mkdir -p ${COMMONDESTDIR}/var/lib/languageforge/lexicon/sendreceive/Templates
