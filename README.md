# LfMerge
Send/Receive for languageforge.org

## Building

After cloning the repo you'll have to download some dependencies. This is easiest done by using the `DownloadDependencies` build target. From a command/terminal window, run (on Linux replace `msbuild` with `xbuild`):

    msbuild /t:DownloadDependencies build/LfMerge.proj

Afterwards you can load and compile `LfMerge.sln` in Visual Studio or MonoDevelop.
