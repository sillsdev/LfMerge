# LfMerge

Send/Receive for languageforge.org

## Preqrequisites

You'll have to install some package dependencies. These are listed in [`build/dependencies.config`](https://github.com/sillsdev/LfMerge/blob/master/build/dependencies.config).
This is easiest done by running

    build/install-deps

For successfully running the unit tests you'll have to clone the
[web-languageforge](https://github.com/sillsdev/web-languageforge) repo into `data/php`.

## Building

After cloning the repo you'll have to download some dependencies. This is easiest done by using the
`DownloadDependencies` build target. From a command/terminal window, run:

	msbuild /t:DownloadDependencies build/LfMerge.proj

Afterwards you can load and compile `LfMerge.sln` in Visual Studio or MonoDevelop/Rider.

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
