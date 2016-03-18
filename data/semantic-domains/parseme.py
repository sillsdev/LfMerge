from lxml import etree
import json
import sys
import codecs
import datetime


def get_auni(elem, ws="en"):
    if elem.tag != "AUni":
        return ""
    if elem.attrib.get("ws", "") != ws:
        return ""
    return elem.text.strip()


def get_astr(elem, ws="en"):
    if elem.tag != "AStr":
        return ""
    if elem.attrib.get("ws", "") != ws:
        return ""
    run = elem.find('Run[@ws="%s"]' % ws)
    return run.text.strip()


def get_uni(elem, ws="en"):
    if elem.tag != "Uni":
        return ""
    return elem.text.strip()


def get_text(elem, ws="en"):
    if elem.tag == "AStr":
        return get_astr(elem, ws)
    elif elem.tag == "AUni":
        return get_auni(elem, ws)
    elif elem.tag == "Uni":
        return get_uni(elem, ws)
    else:
        return elem.text.strip()


# Get the text content of an element no matter which format (AUni, AStr, Uni) it's in
def get_text_content(elem, ws="en"):
    child = elem.find('AStr[@ws="%s"]' % (ws,))
    if child is None:
        child = elem.find('AUni[@ws="%s"]' % (ws,))
    if child is None:
        child = elem.find('Uni')
    if child is None:
        return ""
    return get_text(child, ws)


# Return format: dict with keys (question, example_words, example_sentence), any or all of which might be empty strings
# Or return empty dict if called on wrong kind of element
def get_question_and_examples(elem, ws="en"):
    if elem.tag != "CmDomainQ":
        return {}
    question_elem = elem.find('Question')
    question = "" if question_elem is None else get_text_content(question_elem, ws)
    example_elem = elem.find('ExampleWords')
    example =  "" if example_elem  is None else get_text_content(example_elem,  ws)
    sentence_elem = elem.find('ExampleSentences')
    sentence = "" if sentence_elem is None else get_text_content(sentence_elem, ws)
    return {"question": question, "example_words": example, "example_sentence": sentence}


def get_domain(elem, ws="en"):
    if elem.tag != "CmSemanticDomain":
        return {}
    domaininfo = {
        "abbr": "",
        "name": "",
        "desc": "",
        "guid": None,
        "louwnida": "",
        "ocmcodes": "",
        "questions": [],
    }
    if elem.attrib.has_key("guid"):
        domaininfo["guid"] = elem.attrib["guid"]
    abbr = elem.find('Abbreviation')
    if abbr is not None:
        domaininfo['abbr'] = get_text_content(abbr, ws)
    name = elem.find('Name')
    if name is not None:
        domaininfo['name'] = get_text_content(name, ws)
    desc = elem.find('Description')
    if desc is not None:
        domaininfo['desc'] = get_text_content(desc, ws)
    for q_elem in elem.iterfind('Questions/CmDomainQ'):
        question_and_examples = get_question_and_examples(q_elem, ws)
        if question_and_examples is not None:
            domaininfo['questions'].append(question_and_examples)
    louwnida = elem.find("LouwNidaCodes")
    if louwnida is not None:
        domaininfo['louwnida'] = get_text_content(louwnida, ws)
    ocmcodes = elem.find("OcmCodes")
    if ocmcodes is not None:
        domaininfo['ocmcodes'] = get_text_content(ocmcodes, ws)
    # NOT processed: links to related domains (Link and RelatedDomains elements)
    return domaininfo


def milliseconds_since_epoch(dt):
    epoch = datetime.datetime.utcfromtimestamp(0)
    delta = dt - epoch
    seconds = delta.total_seconds()
    return int(seconds * 1000.0)


def to_mongo_datestring(dt):
    # Mongo JSON deserializer expects dates in format {"$date": integer-number-of-milliseconds-since-epoch}
    millis = milliseconds_since_epoch(dt)
    return {"$date": millis}


def json_serializer(obj):
    if isinstance(obj, datetime.datetime):
        return to_mongo_datestring(obj)
    else:
        raise TypeError, "Don't know how to serialize objects of type %s" % (type(obj),)


def to_json(all_domaininfos, pretty_print=False, indent=4):
    all_mongo_data = []
    for domaininfo in all_domaininfos:
        mongo_data = {
            "guid": domaininfo["guid"],
            "key": domaininfo["abbr"],
            "abbreviation": domaininfo["abbr"],
            "value": domaininfo["name"],
            # TODO: If example words, questions, Louw-Nida codes, etc. are desired, uncomment the code below
        }
        # for key in "louwnida", "ocmcodes", "questions":
        #     value = domaininfo.get(key, None)
        #     if value:
        #         mongo_data[key] = value
        all_mongo_data.append(mongo_data)
    now = datetime.datetime.utcnow()
    optionlist = {
        "canDelete": False,
        "code": "semantic-domain-ddp4",
        "dateCreated": now,
        "dateModified": now,
        "defaultItemKey": None,
        "name": "Semantic Domains",
        "items": all_mongo_data
    }
    if pretty_print:
        return json.dumps(optionlist, default=json_serializer, sort_keys=True, indent=indent, separators=(',', ': '))
    else:
        return json.dumps(optionlist, default=json_serializer)


def get_infilename(argv):
    if len(argv) <= 1:
        return "SemDom.xml"
    return argv[1]


def open_outfile(outfilename = "-"):
    if outfilename == "-":
        return sys.stdout
    return codecs.open(outfilename, "wt", "utf-8")


def main(infilename, outfile):
    ws = "en"
    semdom = etree.parse(infilename)
    all_domaininfos = []
    for domain in semdom.iterfind('.//CmSemanticDomain'):
        domaininfo = get_domain(domain, ws)
        all_domaininfos.append(domaininfo)
    outfile.write(to_json(all_domaininfos, pretty_print=True))
    outfile.close()


if __name__ == "__main__":
    infilename = get_infilename(sys.argv)
    outfile = open_outfile()  # Currently not specifying the output file in argv
    main(infilename, outfile)
