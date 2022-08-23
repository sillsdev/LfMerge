# LfMerge

Send/Receive for languageforge.org

## Special Thanks To

For error reporting:

[![Bugsnag logo](readme_images/bugsnag-logo.png "Bugsnag")](https://bugsnag.com/blog/bugsnag-loves-open-source)

## Prerequisites

You'll need Docker installed, as well as GNU Parallel to use the parallel-build script `pbuild.sh`. Run `sudo apt install parallel` on an Ubuntu or Debian-based system.

## Development

The `master` branch in this repository is where most development happens (it would be called `develop` if we were strictly following a Git-Flow naming scheme). The `live` branch is what release images are built from. Pushing to the `live` branch will build the Docker image, then if the build succeeds, it will tag the built commit with the version number with a `v` prefix (e.g., `v2.0.123`) and tag the Docker image with the unprefixed version number (e.g., `lfmerge:2.0.123`).

The `live` branch is expected to be **fast-forwarded**, with no merge commits, from the `master` branch. When you want to push a new release, please do:

1. `git checkout master`
1. `git pull --ff-only` to make sure you have the latest merged PRs
1. Deal with any merge conflicts, then run `git pull --ff-only` again if there were any conflicts
1. Once there are no merge conflicts, `git checkout live`
1. `git merge --ff-only master`
1. If no merge conflicts, `git push live`

## Building

For each DbVersion that LfMerge supports, we build a different lfmerge binary. We used to support DbVersions 7000068 through 7000070, which correspond to various versions of FieldWorks 8.x, but we now only support FieldWorks 9.x. The only DbVersion found in FieldWorks 9.x is currently (as of August 2022) 7000072. There is a script called `pbuild.sh` (for "parallel build") that will handle building all currently-supported DbVersions. It will run the build for each DbVersion in a Docker container, using a common Docker build image, and then copy the final results into a directory called `tarball`. Finally, it will run a Docker build that will take the files in the `tarball` directory and turn then into a Docker image for `lfmerge`. By default, this Docker image will be tagged `ghcr.io/sillsdev/lfmerge:latest`, while the GitHub Actions workflow will produce a specific version number tag as well as `latest`, i.e. the GHA workflow will tag `lfmerge:2.0.123` as well as `lfmerge:latest`.

## Debugging

Debugging is possible, in some form, with the C# extension in VS Code. Run pbuild.sh (which creates the environment used by the debugger), set your breakpoints, and run the .NET Core Launch task. Due to the complex nature of the software, which necessitates the use of pbuild.sh, for example, there may be custom setup required to progress far enough to reach your breakpoints, depending on where they are. Debugging will launch and use LfQueueManager as its entry point.

## Testing locally

The image that `pbuild.sh` produces is tagged with the same image tag as the one built by the official GitHub Actions workflow, but with a `latest` tag instead of a versino number. This means that if you're running Language Forge locally via the Makefile in Language Forge's `docker` directory, you should be able to simply edit the `docker/lfmerge/Dockerfile` in Language Forge. Make sure that file specifies `lfmerge:latest` rather than `lfmerge:20..123`. Then run `make` and the `lfmerge` container will be re-created with your local build. You can then do a Send/Receive via your `localhost` copy of Language Forge and check the results.

## Debugging unit tests in VS Code

Debugging unit tests in VS Code is a two-step process. First, choose the "Run Test Task" command in VS Code. This task is configured to start the `dotnet test` process in debug mode, which means that it will set itself up (building the project if necessary), then pause itself waiting for a debugger to attach. Once it has paused itself, go to the debug tab (<kbd>Crtl+Shift+D</kbd>) and choose the "Attach" configuration. When prompted, enter the PID printed on the test console. (There will be two, one for LfMerge.Tests and one for LfMerge.Core.Tests; most of the tests are in LfMerge.Core so that's the one you'll need most of the time). The debugger will attach to the test process, which is still in a paused state. Set any breakpoints you want to hit in the unit tests, then unpause the debugger and it will start running the unit tests.

To run specific unit tests instead of the whole suite, edit the `.vscode/tasks.json` file and change the launch args from `[ "test" ]` to something containing a filter, e.g.

`[ "test", "--filter", "FullyQualifiedName~GetCustomFieldForThisCmObject" ],`

The `~` in `FullyQualifiedName~` means "contains". For more possible filters you could use, see https://aka.ms/vstest-filtering

## Logs

The `lfmerge` container logs to stdout, so run `docker logs -f lfmerge` if you want to watch the logs.
