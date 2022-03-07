# LfMerge

Send/Receive for languageforge.org

## Special Thanks To

For error reporting:

[![Bugsnag logo](readme_images/bugsnag-logo.png "Bugsnag")](https://bugsnag.com/blog/bugsnag-loves-open-source)

## Prerequisites

You'll need Docker installed, as well as GNU Parallel to use the parallel-build script `pbuild.sh`. Run `sudo apt install parallel` on an Ubuntu or Debian-based system.

## Development

First, a word about the branching scheme of this repository and how it relates to FieldWorks. LfMerge supports FieldWorks versions 8.2 and later. FieldWorks has had different data models, represented by a six-digit integer called DbVersion. The data models for FieldWorks 8.x had DbVersions 7000068 through 7000070. DbVersion 7000071 was never released in anything but alpha builds of FieldWorks, and LfMerge does not support it. FieldWorks 9.0 and above uses DbVersion 7000072.

There are two main branches in the LfMerge repo, `master` and `fieldworks8-master`. That's because key parts of the FieldWorks API changed between FW 8 and FW 9, such as FDO (FieldWorks Data Objects) being renamed to LCM (Language and Culture Model) and large parts of the FDO API being moved to a library called liblcm. The process of acquiring FieldWorks DLLs also changed between FW 8 and FW 9. FW 8 DLLs (and other libraries that work with FW 8, such as Chorus) were never packaged as NuGet packages, so we have to download those DLLs as build artifacts from various TeamCity builds. FW 9 and its supporting libraries, however, are available as NuGet packages. So the build process for `master` and `fieldworks8-master` is slightly different. More on that in the Building section below.

In addition to `master` and `fieldworks8-master`, there are also `qa` and `fieldworks8-qa` branches, and `live` and `fieldworks8-live` branches. The two `live` branches are the ones that the release is built from, while the `qa` branch is for prerelease builds. One consequence of this is that **most pull requests need to be doubled up**. When you create a feature branch called `feature/foo` and make a PR from it (against `master`), you'll need to make a similar feature branch and call it `feature/foo-fw8`, then create another PR from that branch against `fieldworks8-master`. Once someone has reviewed the PR against `master`, you can merge **both** PRs unless the reviewer specifically says not to.

## Building

For each DbVersion that LfMerge supports, we build a different lfmerge binary. DbVersions 7000068 through 7000070 are built from FW 8 branches (`fieldworks8-master` or `feature/foo-fw8`), while DbVersion 7000072 is built from an FW 9 branch (`master` or `feature/foo`). There is a script called `pbuild.sh` (for "parallel build") that will handle all the complexity of the build process for you. It will run the build for each DbVersion in a Docker container, using a common Docker build image, and then copy the final results into a directory called `tarball`. Finally, it will run a Docker build that will take the files in the `tarball` directory and turn then into a Docker image for `lfmerge`. By default, this Docker image will be tagged `ghcr.io/sillsdev/lfmerge:latest`, the same tag as the tag built by the GitHub Actions workflow.

. DbVersions 7000068 were from FieldWorks releases 8.2 or 8.3, and those builds will use the `fieldworks8-master` branch by default (see below for how you can change this). The `master` branch is for

Normally you will run `pbuild.sh` with no parameters. It will look at the Git branch you have checked out, determine whether that branch is a branch based on FieldWorks 9 (DbVersion 7000072 or later) or FieldWorks 8 (DbVersions 7000068 through 7000070), and calculate the corresponding branch in the other FW version. E.g., if you're on `master` the corresponding branch will be `fieldworks8-master`. If you're on `feature/something` the corresponding branch will be calculated as `feature/something-fw8`. If you're on `feature/something-fw8` then the corresponding branch will be `feature/something`. But if `pbuild.sh` gets this calculation wrong, then you can pass it a parameter to set what the corresponding branch should be. E.g. if you're on `feature/something` but you want the FW 8 builds to be built from `fieldworks8-live`, then run `pbuild.sh fieldworks8-live`.

Alternatively you can build and run the tests on the command line:

	msbuild /t:Test /p:Configuration=Debug build/LfMerge.proj

### Building on Windows

LfMerge is intended to be run on Linux and the development happens on Linux. For testing/debugging
purposes it might be useful to build on Windows. Be prepared that building on Windows might currently
not work or that the tests don't work out of the box.

## Logfiles

LfMerge and LfMergeQueueManager log some output. On a Xenial system this can be seen by running:

	journalctl -t LfMerge -t LfMergeQueueManager -e

If you want to redirect the output to a file and still see the colors, you can use:

	script -qfc "journalctl -t LfMerge --no-pager" /dev/null > lfmerge.log.txt
	less -R lfmerge.log.txt
