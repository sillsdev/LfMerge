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

flat_abbrevs = []
hierarchical_abbrevs = []
reversed_flat_abbrevs = []
reversed_hierarchical_abbrevs = []
flat_names = []
hierarchical_names = []
reversed_flat_names = []
reversed_hierarchical_names = []

current_section = ""
current_abbrev_section = ""
prev_term = ""
prev_abbrev = ""
prev_indent = -1

#ORC = u'\ufffc'  # Use this to get a literal U+FFFC in the output
ORC = '\\ufffc'  # Use this to get a Python (or C#) escape sequence in the output

def process_line(s):
    global current_section
    global current_abbrev_section
    global prev_term
    global prev_abbrev
    global prev_indent
    term = re.search('<term ws="en">(.*)</term>', s).group(1).lower()
    abbrev = re.search('<abbrev ws="en">(.*)</abbrev>', s).group(1).lower()
    guid = re.search('guid="([^"]+)"', s).group(1)
    numtabs = s.count('\t')
    flat_abbrevs.append('{ "%s", "%s" }' % (guid, abbrev))
    reversed_flat_abbrevs.append('{ "%s", "%s" }' % (abbrev, guid))
    flat_names.append('{ "%s", "%s" }' % (guid, term))
    reversed_flat_names.append('{ "%s", "%s" }' % (term, guid))
    if term.startswith("ditransitive"):
        # Special case: some old data uses the term "bitransitive" as a synonym
        # But we should only append it to the term->guid direction.
        reversed_flat_names.append('{ "%s", "%s" }' % (term.replace("ditransitive", "bitransitive"), guid))
        # NOTE: Not changing abbreviation, since the old data has all kinds of
        # abbreviations that are different. Just deal with the "wrong" abbrevs.
    if numtabs == 0:
        current_section = term
        current_abbrev_section = abbrev
        hierarchical_abbrevs.append('{ "%s", "%s" }' % (guid, abbrev))
        reversed_hierarchical_abbrevs.append('{ "%s", "%s" }' % (abbrev, guid))
        hierarchical_names.append('{ "%s", "%s" }' % (guid, term))
        reversed_hierarchical_names.append('{ "%s", "%s" }' % (term, guid))
        if term.startswith("ditransitive"):
            # Special case: some data uses the term "bitransitive" as a synonym
            # But we should only append it to the term->guid direction.
            reversed_hierarchical_names.append('{ "%s", "%s" }' % (term.replace("ditransitive", "bitransitive"), guid))
            # NOTE: Not changing abbrev, since the old data has all kinds of
            # abbrevs that are different. Just deal with the "wrong" abbrevs.
    else:
        if numtabs == prev_indent:
            pass
        elif numtabs > prev_indent:
            if current_section != prev_term:
                current_section = ORC.join([current_section, prev_term])
                current_abbrev_section = ORC.join([current_abbrev_section, prev_abbrev])
        elif numtabs < prev_indent:
            current_section = "".join(current_section.rsplit(ORC, (prev_indent-numtabs))[:1])
            current_abbrev_section = "".join(current_abbrev_section.rsplit(ORC, (prev_indent-numtabs))[:1])
        else:
            raise AssertionError, "Impossible math happened between {} and {}".format(numtabs, prev_indent)
        hierarchical_abbrevs.append('{ "%s", "%s" }' % (guid, ORC.join([current_abbrev_section, abbrev])))
        reversed_hierarchical_abbrevs.append('{ "%s", "%s" }' % (ORC.join([current_abbrev_section, abbrev]), guid))
        hierarchical_names.append('{ "%s", "%s" }' % (guid, ORC.join([current_section, term])))
        reversed_hierarchical_names.append('{ "%s", "%s" }' % (ORC.join([current_section, term]), guid))
        if term.startswith("ditransitive"):
            # Special case: some data uses the term "bitransitive" as a synonym
            # But we should only append it to the term->guid direction.
            reversed_hierarchical_names.append('{ "%s", "%s" }' % (ORC.join([current_section, term.replace("ditransitive", "bitransitive")]), guid))
            # NOTE: Not changing abbrev, since the old data has all kinds of
            # abbrevs that are different. Just deal with the "wrong" abbrevs.
    # Next three lines should be last in function
    prev_term = term
    prev_abbrev = abbrev
    prev_indent = numtabs

def process_template(template_fname, out_fname):
    template_re = re.compile(r"^(\s*)\S+\s?INSERT (FLAT|HIERARCHICAL)( REVERSE)? (ABBREV|NAME)S HERE\s*$")
    with open(template_fname) as template_f:
        with open(out_fname, 'w') as out_f:
            for line in template_f:
                m = template_re.match(line)
                if m:
                    indent = m.group(1)
                    flat = (m.group(2).upper() == "FLAT")
                    reverse = (m.group(3) is not None)
                    which = m.group(4).upper()
                    if (which) == "ABBREV":
                        if reverse:
                            source = (reversed_flat_abbrevs if flat else reversed_hierarchical_abbrevs)
                        else:
                            source = (flat_abbrevs if flat else hierarchical_abbrevs)
                        count = len(source)
                        for i, item in enumerate(source):
                            out_f.write("{}{}{}\n".format(indent, item.strip(), ("," if i < count-1 else "")))
                    elif (which) == "NAME":
                        if reverse:
                            source = (reversed_flat_names if flat else reversed_hierarchical_names)
                        else:
                            source = (flat_names if flat else hierarchical_names)
                        count = len(source)
                        for i, item in enumerate(source):
                            out_f.write("{}{}{}\n".format(indent, item.strip(), ("," if i < count-1 else "")))
                    else:
                        # Shouldn't happen
                        raise AssertionError, "Incorrect line in template: should specify abbrev or name: {}".format(line)
                else:
                    out_f.write(line)

if __name__ == "__main__":
    sys.stdout = codecs.getwriter('utf-8')(sys.stdout)
    for line in fileinput.FileInput():
        print "Input line: {}".format(line.strip())
        process_line(line)
    #print ",\n".join(flat_names)
    #print
    #print ",\n".join(hierarchical_names)
    #print
    #print ",\n".join(reversed_flat_names)
    #print
    #print ",\n".join(reversed_hierarchical_names)
    #print
    process_template("source-template.txt", "output.cs")
