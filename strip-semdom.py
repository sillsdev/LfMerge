#!/usr/bin/env python

import lxml
from lxml import etree
import os, sys

FNAME="data/testlangproj/testlangproj.fwdata"
SEMDOM_GUID="c924bfce-beed-4382-95e8-62b54951c83d"
CHKLIST_GUID="76fb50ca-f858-469c-b1de-a73a863e9b10"

guid_lookup_table = {}
def get_guid(root, guid):
    if not guid_lookup_table:
        # First time? Build the lookup table
        for rt in root.iter('rt'):
            found_guid = rt.attrib.get('guid', None)
            if found_guid is not None:
                guid_lookup_table[found_guid] = rt
    #return root.find('rt[@guid="%s"]' % guid.strip())
    return guid_lookup_table[guid.strip()]

observed_count = 0
seen_guids = set()
def show_progress(elem, how_often = 500):
    global observed_count
    guid = elem.attrib["guid"]
    if guid in seen_guids:
        print "WARNING: We've looked at %s before, somewhere..." % elem.attrib["guid"]
    seen_guids.add(guid)
    observed_count += 1
    if (observed_count % how_often) == 0:
        print "Looking at element %06d: %s" % (observed_count, guid)

def child_guids(elem):
    # Child references are objsur elements with attrib t="o" ("type" == "owned")
    if elem is None: return set()
    show_progress(elem, 50)
    result = set()
    for subelem in ["Discussion", "Occurrences", "Paragraphs", "Questions", "Possibilities", "SubPossibilities"]:
        references = elem.findall('./%s/objsur[@t="o"]' % subelem)
        if references is None:
            continue
        for ref in references:
            result.add(ref.attrib["guid"])
    return result

def descendant_guids(elem, root):
    result = set()
    this_pass = child_guids(elem)
    while len(this_pass) > 0:
        result.update(this_pass)
        next_pass = set()
        for guid in this_pass:
            elem = get_guid(root, guid)
            if elem is None: continue
            next_pass.update(child_guids(elem))
        this_pass = next_pass
    return result

def main(args = None):
    if args is None:
        args = sys.argv
    if len(args) < 2:
        fname = FNAME
    else:
        fname = args[1]
    out_fname = fname.replace('.fwdata', '-without-semdom.xml')
    print "Stripping semantic domain data from %s; will write result to %s" % (
            fname, out_fname)
    print "If that's not what you intended, press Ctrl-C to cancel operation."
    print "This will probably take around 60 to 90 minutes to complete."
    print "No data will be written to the output file until this is done, so"
    print "you have plenty of time to hit Ctrl-C in the meantime."
    print
    tree = etree.parse(fname)
    root = tree.getroot()
    semdom = get_guid(root, SEMDOM_GUID)
    chklist = get_guid(root, CHKLIST_GUID)
    all_semdom_guids = descendant_guids(semdom, root)
    all_chklist_guids = descendant_guids(chklist, root)
    guids_to_skip = all_semdom_guids.union(all_chklist_guids)
    # Create a new, empty, root
    new_root = etree.Element(root.tag, root.attrib)
    for child in root:  # Iterate over just the immediate children
        child_class = child.attrib.get("class", "")
        if child_class.startswith("Chk") or child_class.startswith("CmAnthroItem"):
            # Just skip all ChkRef and ChkTerm elements, as well as anthropology categories
            continue
        if child_class == "StTxtPara" and len(child.findall("Contents")) == 0:
            # Skip paragraphs with no content
            continue
        if child.tag != 'rt':
            new_root.append(child)
            continue
        if child.attrib["guid"] not in guids_to_skip:
            new_root.append(child)
    with open(out_fname, 'w') as out_f:
        out_f.write(etree.tostring(new_root))

if __name__ == "__main__":
    main(sys.argv)
