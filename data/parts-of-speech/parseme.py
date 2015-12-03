#!/usr/bin/python

# Assumptions: The input file has come through parseme.sh first. Therefore:
#   1. It has one line per part of speech.
#   2. It is indented with tabs.
#   3. Its top indentation level has no tabs.
#   4. Each line looks like '\t\t... guid="1234" ... <term ws="en">ABCD</term>'
# If given input in that form, we will extract the "1234" and "ABCD" text from each line.

import os, sys
import codecs
import fileinput
import re

# Global variables... not the prettiest code, but this is a one-off script

flat_names = []
hierarchical_names = []
reversed_flat = []
reversed_hierarchical = []

current_section = ""
prev_term = ""
prev_indent = -1

#ORC = u'\ufffc'  # Use this to get a literal U+FFFC in the output
ORC = '\\ufffc'  # Use this to get a Python (or C#) escape sequence in the output

def process_line(s):
    global current_section
    global prev_term
    global prev_indent
    term = re.search('<term ws="en">(.*)</term>', s).group(1).lower()
    guid = re.search('guid="([^"]+)"', s).group(1)
    numtabs = s.count('\t')
    flat_names.append('{ "%s", "%s" }' % (guid, term))
    reversed_flat.append('{ "%s", "%s" }' % (term, guid))
    if numtabs == 0:
        current_section = term
        hierarchical_names.append('{ "%s", "%s" }' % (guid, term))
        reversed_hierarchical.append('{ "%s", "%s" }' % (term, guid))
    else:
        if numtabs == prev_indent:
            pass
        elif numtabs > prev_indent:
            if current_section != prev_term:
                current_section = ORC.join([current_section, prev_term])
        elif numtabs < prev_indent:
            current_section = "".join(current_section.rsplit(ORC, (prev_indent-numtabs))[:1])
        else:
            raise AssertionError, "Impossible math happened between {} and {}".format(numtabs, prev_indent)
        hierarchical_names.append('{ "%s", "%s" }' % (guid, ORC.join([current_section, term])))
        reversed_hierarchical.append('{ "%s", "%s" }' % (ORC.join([current_section, term]), guid))
    # Next two lines should be last in function
    prev_term = term
    prev_indent = numtabs

if __name__ == "__main__":
    sys.stdout = codecs.getwriter('utf-8')(sys.stdout)
    for line in fileinput.FileInput():
        process_line(line)
    print ",\n".join(flat_names)
    print
    print ",\n".join(hierarchical_names)
    print
    print ",\n".join(reversed_flat)
    print
    print ",\n".join(reversed_hierarchical)
    print
