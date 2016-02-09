#!/bin/bash

if [ "$#" -lt 1 ];
then
    sourcefile="GOLDEtic.xml"
else
    sourcefile="$1"
fi

cat "$sourcefile" |
    egrep 'guid|term ws="en"|abbrev ws="en"' |  # We only want the lines with GUIDs and the ones with the part-of-speech names and abbreviations
    sed -e 's/\t/    /g' |                      # The GOLDEtic.xml file has four-space tabs in it...
    sed -e 's/   /\t/g' |                       # ... but it has three-space indentation levels. Convert that to one-tab indents.
    sed -e 's/^\t//' |                          # We want top-level items starting at column 1 (indent level 0)
    sed -e 's/^\t\+<term/<term/' |              # We also want the <term> items to NOT be indented...
    sed -e 's/^\t\+<abbrev/<abbrev/' |          # We also want the <abbrev> items to NOT be indented...
    sed -e 'N;N;s/\n/ /g' |                     # ... so that when we join the guid and <term> and <abbrev> lines, we'll have tabs ONLY at the start of the line.
    python parseme.py                           # Rest of the parsing is done in the Python script. Output to stdout.
