#!groovy
// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

distro = 'trusty xenial'
minDbVersion = 7000068
maxDbVersion = 7000069
monoPrefix = '/opt/mono-sil'

ansiColor('xterm') {
	timestamps {
		properties([
			[$class: 'GithubProjectProperty', displayName: '', projectUrlStr: 'https://github.com/sillsdev/LfMerge'],
			// Trigger on GitHub push
			pipelineTriggers([[$class: 'GitHubPushTrigger']])
		])

		env.MONO_PREFIX = monoPrefix
		node('lfmerge') {
			milestone label: 'Checkout and install packages'

			stage('Checkout') {
				checkout scm
				GIT_COMMIT = sh(returnStdout: true, script: 'git rev-parse HEAD').trim()
				GIT_BRANCH = sh(returnStdout: true, script: 'git rev-parse --abbrev-ref HEAD').trim()
				stash name: 'sources', excludes: 'data/php/**', useDefaultExcludes: false

				dir('data/php') {
					git url: 'https://github.com/sillsdev/web-languageforge.git', branch: 'master'
				}
			}

			stage('Install packages') {
				withEnv(["PATH+MONO=${monoPrefix}/bin"]) {
					sh("debian/PrepareSource")
					dir("build") {
						sh("./install-deps")
					}
				}
			}

			milestone label: 'Prepare environment'

			parallel CompileMercurial: {
				stage('Compile Mercurial') {
					dir('tmp_hg') {
						checkout(scm: [$class: 'MercurialSCM',
							source: 'https://selenic.com/hg',
							revision: '3.0.1',
							revisionType: 'TAG',
							clean: true], poll: false)
					}
					sh('''#!/bin/bash -e
						BUILD=Release . environ
						cd tmp_hg
						make local
						cp -r mercurial ../Mercurial/
					''')
				}
			}, InstallMongo: {
				stage('Install mongodb and composer') {
					sh('''#!/bin/bash -e
						cd data/php/src
						if [ ! -f mongodb.installed ]; then
							echo "Installing PECL mongodb extension"
							DEBIAN_FRONTEND=noninteractive
							sudo apt-get -y install libpcre3-dev
							sudo pecl install mongodb || true
							if [ ! -f /etc/php5/mods-available/mongodb.ini ]; then
								sudo sh -c 'echo "extension=mongodb.so" >> /etc/php5/mods-available/mongodb.ini'
							fi
							if [ ! -f /etc/php5/cli/conf.d/20-mongodb.ini ]; then
								sudo ln -s /etc/php5/mods-available/mongodb.ini /etc/php5/cli/conf.d/20-mongodb.ini
							fi
							touch mongodb.installed
						fi

						COMPOSERJSON=$(git log --format=%H -1 composer.json)
						COMPOSERJSON_PREV=$(cat composer.json.sha 2>/dev/null || true)

						if [ "$COMPOSERJSON" != "$COMPOSERJSON_PREV" ]; then
							git clean -dxf
							echo "Installing composer"
							php -r "readfile('https://getcomposer.org/installer');" > composer-setup.php
							php composer-setup.php
							php -r "unlink('composer-setup.php');"
							echo "Running composer install"
							php composer.phar install
							echo $COMPOSERJSON > composer.json.sha
							# git clean got rid of this, so create it again
							touch mongodb.installed
						fi
					''')
				}
			}
			stash name: 'SourcesAndDeps', excludes: 'tmp_hg/**, .git/**', useDefaultExcludes: false
		}

		milestone label: 'Compiling'

		node('lfmerge') {
			deleteDir()
			unstash 'SourcesAndDeps'

			stage('Compile LfMerge') {
				sh('''#!/bin/bash -e
					BUILD=Release . environ
					xbuild /t:Build /property:Configuration=Release build/LfMerge.proj
					exit $?
				''')
			}

			stash name: 'output', includes: 'environ, build/*, output/**, packages/**, data/**, Mercurial/**, MercurialExtensions/**',
				useDefaultExcludes: false
		}

		milestone label: 'Running unit tests'

		node('lfmerge') {
			deleteDir()
			unstash 'output'

			stage('Run unit tests') {
				def tempDir = pwd(tmp: true)
				withEnv(["TMPDIR=${tempDir}"]) {
					sh('''#!/bin/bash -e
						BUILD=Release . environ
						xbuild /t:TestOnly /property:Configuration=Release build/LfMerge.proj
						exit $?
					''')
					step([$class: 'NUnitPublisher', testResultsPattern: 'output/Release/TestResults.xml',
						debug: false, keepJUnitReports: true, skipJUnitArchiver:false, failIfNoResults: true])
				}
			}
		}

		// **************************************************************************************
		if (JOB_BASE_NAME =~ "PR-") {
			// we don't want to build packages for pull requests
			echo "Not building package for pull request ${JOB_BASE_NAME}"
			return
		}

		milestone label: 'Packaging'

		// Build continuous package
		BuildPackage('nightly')

		// if desired build release package
		def inputTimedOut = false
		stage('release') {
			try {
				timeout(time: 1, unit: 'DAYS') {
					input message: 'Release this build?'
				}
			} catch (err) {
				inputTimedOut = true
			}
		}
		if (inputTimedOut) {
			return
		}

		BuildPackage('Release')
	} // timestamps
} // ansiColor

def BuildPackage(buildKind) {
	//stage('Package build') {
		def FULL_BUILD_NUMBER = "0.0.${BUILD_NUMBER}.${GIT_COMMIT[0..5]}"
		def MAKE_SOURCE_ARGS = buildKind == 'Release' ? '--preserve-changelog' : ''
		def BUILD_PACKAGE_ARGS = buildKind == 'Release' ? '--no-upload' : ''

		def packages = [:]
		for (def i = minDbVersion; i <= maxDbVersion; i++) {
			def curDbVersion = i
			packages["package${curDbVersion}"] = {
				node('packager') {
					sh("mkdir -p lfmerge-${curDbVersion}")
					dir("lfmerge-${curDbVersion}") {
						deleteDir()
						unstash 'sources'

						stage("Build source package for ${curDbVersion}") {
							sh("""#!/bin/bash -e
								ls -al
								RUNMODE="PACKAGEBUILD" BUILD=Release . environ

								xbuild /t:PrepareSource build/LfMerge.proj

								debian/PrepareSource ${curDbVersion}

								\$HOME/ci-builder-scripts/bash/make-source --dists "\$DistributionsToPackage" \
									--arches "\$ArchesToPackage" --main-package-name "lfmerge" \
									--supported-distros "${distro}" --debkeyid \$DEBSIGNKEY \
									--main-repo-dir "." --package-version "${FULL_BUILD_NUMBER}"  ${MAKE_SOURCE_ARGS}
							""")
							archiveArtifacts artifacts: "../lfmerge*"
						}

						stage("Build binary package for ${curDbVersion}") {
							sh("""#!/bin/bash -e
								RUNMODE="PACKAGEBUILD" BUILD=Release . environ

								\$HOME/ci-builder-scripts/bash/build-package --dists "\$DistributionsToPackage" \
									--arches "\$ArchesToPackage" --main-package-name "lfmerge" \
									--supported-distros "${distro}" --debkeyid \$DEBSIGNKEY ${BUILD_PACKAGE_ARGS}
							""")

							archiveArtifacts artifacts: 'results/*'
						}
					} // dir
				} // node('packager')
			} // packages
		} // for
		parallel packages
	//} // stage

	if (buildKind == 'Release') {
		node('packager') {
			dir("lfmerge") {
				deleteDir()
				unstash 'sources'
				sh 'debian/PrepareSource'
				package_version = sh(returnStdout: true, script: "dpkg-parsechangelog | grep ^Version: | cut -d' ' -f2").trim()
			}
		}
		currentBuild.description = "<span style='background-color:yellow'>lfmerge ${package_version}</span>"
	} else {
		milestone label: 'install on test machine'

		echo "Waiting 5 minutes for packages to show up on LLSO"
		sleep(5, 'MINUTES')

		node('linux') {
			sh('''#!/bin/bash -e
				ssh ba-trusty64weba sudo apt update || true
				ssh ba-trusty64weba sudo apt -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" install -y -f lfmerge || true
				ssh ba-trusty64weba sudo apt -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" install -y -f || true
			''')
		}
	}
} // BuildPackage