#!/bin/bash

if [ $# -le 0 ]; then
	echo "Error: no filename given. Please pass a filename on the command line."
	exit 1
fi
if [ $# -gt 1 ]; then
	echo "Warning: multiple filenames passed. Only $1 will be processed." >&2
fi

# Rules for auto-generated AssemblyInfo in .Net Core 3.1 or later, which we will replicate:
# AssemblyVersion and FileVersion default to the value of $(Version) without the suffix. For example, if $(Version) is 1.2.3-beta.4, then the value would be 1.2.3.
# InformationalVersion defaults to the value of $(Version).
# Source: https://docs.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#generateassemblyinfo

Version=${Version:-0.0.1}

InformationalVersion=${InformationalVersion:-$Version}
AssemblyVersion=${AssemblyVersion:-$(echo "$Version" | sed -E 's/^([^-]*)+.*/\1/')}
FileVersion=${FileVersion:-$(echo "$Version" | sed -E 's/^([^-]*)+.*/\1/')}

# Assembly attribute lines look like this:
# [assembly: AssemblyVersion("1.2.3.4")]

tmpfname="$(mktemp)"
fname="$1"

# First remove any existing lines
cat "$fname" \
| sed '/^\[assembly: AssemblyVersion("[^"]*")\]$/d' \
| sed '/^\[assembly: AssemblyFileVersion("[^"]*")\]$/d' \
| sed '/^\[assembly: AssemblyInformationalVersion("[^"]*")\]$/d' \
> "$tmpfname"

# Then append new lines at the end
echo "[assembly: AssemblyVersion(\"${AssemblyVersion}\")]" >> "$tmpfname"
echo "[assembly: AssemblyFileVersion(\"${FileVersion}\")]" >> "$tmpfname"
echo "[assembly: AssemblyInformationalVersion(\"${InformationalVersion}\")]" >> "$tmpfname"

mv "${tmpfname}" "${fname}"
