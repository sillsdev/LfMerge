#!/bin/bash

if [ "$#" -lt 1 ];
then
    sourcefile="GOLDEtic.xml"
else
    sourcefile="$1"
fi

cat "$sourcefile" |
    egrep 'guid|term ws="en"' |         # We only want the lines with GUIDs and the ones with the part-of-speech names
    sed -e 's/\t/    /g' |              # The GOLDEtic.xml file has four-space tabs in it...
    sed -e 's/   /\t/g' |               # ... but it has three-space indentation levels. Convert that to one-tab indents.
    sed -e 's/^\t//' |                  # We want top-level items starting at column 1 (indent level 0)
    sed -e 's/^\t\+<term/<term/' |      # We also want the <term> items to NOT be indented...
    awk 'NR%2{printf $0" ";next;}1' |   # ... so that when we join the guid and <term> lines, we'll have tabs ONLY at the start of the line.
    python parseme.py                   # Rest of the parsing is done in the Python script. Output to stdout.
