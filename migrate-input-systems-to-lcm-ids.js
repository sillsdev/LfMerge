// Re-spell Language Forge input systems as the LCM writing system Ids LfMerge now exports.
//
// LfMerge wrote each project's input systems -- the project's inputSystems record, its languageCode,
// and, at initial clone, every config field's inputSystems list -- by the writing system's
// LanguageTag, but wrote every multitext key by its Id. Where the two differ (brb-flex-2022's
// Khmer is Id "km-Khmr-KH", LanguageTag "km-KH") LF's editor, which matches a field's input systems
// to the entry's keys exactly and case-sensitively, shows that text as empty. LfMerge now spells
// everything by Id. It rewrites the inputSystems record and custom-field config on every sync, but
// built-in field config only at initial clone, so projects cloned under the old spelling need
// their config re-spelled once. This script does that for the twelve projects the 2026-07-06
// mongodump shows are affected, and re-keys the inputSystems record of three more, found in the
// 2026-10-01 mongodump, whose config already spells the writing system by Id.
//
// Each mapping was found in a dump -- the old spelling is the one LF's config or inputSystems
// record uses, the new one the Id LfMerge wrote into the lexicon's multitext keys -- and then
// checked against the project's FieldWorks repository at its tip, since the dump shows LCM only as
// it was at LF's last sync.
// The lexicon's keys are already spelled by Id and are not touched, except in che-flex; see
// LEXICON_REKEYS below.
//
// Run this BEFORE the new LfMerge build first syncs these projects. After that sync, the
// inputSystems record is spelled by Id, and in kxpw-flex the old spelling "acp" is by then the Id
// of a different writing system, added in FieldWorks after LF's clone. The script leaves an
// inputSystems entry alone rather than overwrite one already spelled the new way.
//
// And do not let the OLD LfMerge build sync these projects once this script has run: keep every
// project listed here on HOLD from before the script runs until the new build is deployed. The old
// build looks each inputSystems key up as an Id and then calls WritingSystemManager.Replace, which
// looks the writing system up again by LanguageTag. Keyed by the old spelling, as now, both find
// the same writing system. Keyed by Id, Replace finds a different one wherever another writing
// system has the LanguageTag as its Id -- brb-flex-2022's unused "km-KH", tkr-flex's unused
// "tkr", kxpw-flex's "acp" -- and evicts it, giving the renamed writing system its handle; every
// other renamed writing system is re-registered under a new handle partway through the sync. That
// is the corruption that broke xkk-flex-2022. The old build's export would also key the
// inputSystems record by LanguageTag again, undoing the renames.
//
// What is changed, for each project:
//   - every array named "inputSystems" anywhere under config (entry fields, custom fields, role
//     views and user views), replacing the old spelling with the new one, without duplicating it;
//   - the project's inputSystems record: the old key is renamed to the new one and its tag updated
//     (LfMerge would rewrite this on its next sync anyway; doing it here avoids a window in which
//     LF shows bare tags instead of abbreviations);
//   - languageCode, where it is the old spelling;
//   - in che-flex alone, the lexicon's multitext keys (see LEXICON_REKEYS).
// Only exact matches of the old spellings are replaced. Running it twice changes nothing the
// second time.
//
// Usage (dry run by default -- it reports what it would change and changes nothing):
//   mongosh "mongodb://HOST:PORT" migrate-input-systems-to-lcm-ids.js
//   DRY_RUN=false mongosh "mongodb://HOST:PORT" migrate-input-systems-to-lcm-ids.js
// LF_DB selects the main database (default "scriptureforge").

const DRY_RUN = (process.env.DRY_RUN || 'true').toLowerCase() !== 'false';
const DB_NAME = process.env.LF_DB || 'scriptureforge';

// projectCode -> { old spelling in LF's config : LCM writing system Id }
// awa-flex_lf is deliberately absent: its repository was begun afresh in March 2021 and renamed
// hi-Deva-IN to hi-IN a week later, so its config's "hi-IN" already matches LCM.
// huz-flex is absent too, though its languageCode is "huz-Cyrl" while its config says "huz": its
// repository holds both, as two writing systems, so that is not a spelling to correct.
const MIGRATIONS = {
  'ary-mo-flex':     { 'ary-MO-fonipa-x-emic': 'ary-Arab-MO-fonipa-x-emic' },
  'brb-flex-2022':   { 'brb-KH': 'brb-Khmr-KH', 'km-KH': 'km-Khmr-KH' },
  // Not a respelling but a rename in FieldWorks: LF was cloned when this writing system was "ce-la"
  // (LA being Laos), and in November 2020 its text moved to "ce-Latn", which now holds all of it.
  'che-flex':        { 'ce-LA': 'ce-Latn' },
  'jaijairai-flex2': { 'my-MM': 'my-Mymr-MM' },
  'ket-flex':        { 'ket': 'ket-Cyrl' },
  // Lower-case "k" is right: it is the name of the .ldml file in the WritingSystemStore LCM loads,
  // and so the Id LfMerge exported all 14,493 multitext keys under. FieldWorks on Windows spells
  // it "mve-Arab-PK" (its CachedSettings copy), but LF must match what LfMerge exports.
  'mve-n-flex':      { 'mve-PK': 'mve-Arab-Pk' },
  'kxpw-flex':       { 'acp': 'acp-Latn', 'kxp-PK': 'kxp-Arab-PK' },
  'odk-flex':        { 'odk-PK': 'odk-Arab-PK' },
  'oyb-flex':        { 'lo-LA-fonipa-x-emic': 'lo-Laoo-LA-fonipa-x-emic' },
  'spt-flex':        { 'hi-IN': 'hi-Deva-IN', 'spt-x-tib': 'spt-Tibt-x-tib' },
  'tmy-2020-flex':   { 'tmy': 'tmy-Latn' },
  'yor-mw-flex':     { 'yo': 'yo-Latn' },
  // The next three need only their inputSystems record re-keyed: the config already spells the
  // writing system by Id, but the record was last written by an LfMerge that keyed it by
  // LanguageTag. Without this, the config's spelling has no input system record in LF, and the new
  // LfMerge finds the writing system only by its LanguageTag, which depends on SLDR data.
  // The 2026-09-29 sync wrote "rhg"; LCM's Hanifi Rohingya writing system is "rhg-Rohg".
  'rhg-flex':        { 'rhg': 'rhg-Rohg' },
  // LCM holds two: "tkr-Latn", with all 3,427 strings and in the vernacular lists, and an unused
  // "tkr". Keyed "tkr", the record names the unused one.
  'tkr-flex':        { 'tkr': 'tkr-Latn' },
  // "rif-Latn" holds 249 strings and is in the analysis list; the config does not offer it.
  'sjs-flex':        { 'rif': 'rif-Latn' },
};

// projectCode -> { multitext key in LF's lexicon : LCM writing system Id }
// che-flex's lexicon is still keyed "ce-la", the Id LfMerge exported under before the text moved
// to "ce-Latn" in FieldWorks; LCM's "ce-la" now holds no text. LfMerge re-exports only the entries
// FieldWorks has changed since, 1,983 of 18,434 at the 2026-10-01 dump. The rest would stay keyed
// "ce-la", out of sight of a config spelled "ce-Latn", and an LF edit to one of them would write
// its old text to "ce-la" and clear its "ce-Latn" text in FieldWorks, since importing an entry
// clears every alternative LF lacks. In each of those entries every "ce-la" value is text the repo
// holds in "ce-Latn", so renaming the key gives LF what FieldWorks has. Keys are renamed in place,
// and entries' dates are left alone, so that LfMerge does not take them for LF edits.
const LEXICON_REKEYS = {
  'che-flex': { 'ce-la': 'ce-Latn' },
};

// A subdocument, as opposed to an array, a date or a BSON value (ObjectId, Long and the like, which
// all carry _bsontype).
function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value) &&
    !(value instanceof Date) && value._bsontype === undefined;
}

// Collect a $set for every "inputSystems" array under node that holds an old spelling.
function collectInputSystemArrays(node, path, oldToNew, sets, warnings) {
  if (Array.isArray(node)) {
    node.forEach((value, i) => collectInputSystemArrays(value, path + '.' + i, oldToNew, sets, warnings));
    return;
  }
  if (!isPlainObject(node)) {
    return;
  }
  for (const [key, value] of Object.entries(node)) {
    const childPath = path ? path + '.' + key : key;
    if (key === 'inputSystems' && Array.isArray(value)) {
      if (!value.some(tag => Object.prototype.hasOwnProperty.call(oldToNew, tag))) {
        continue;
      }
      if (childPath.split('.').some(segment => segment.includes('$'))) {
        warnings.push('cannot address ' + childPath + ' in an update; left as it is');
        continue;
      }
      const respelled = [];
      for (const tag of value) {
        const newTag = Object.prototype.hasOwnProperty.call(oldToNew, tag) ? oldToNew[tag] : tag;
        if (!respelled.includes(newTag)) {
          respelled.push(newTag);
        }
      }
      sets[childPath] = respelled;
    } else {
      collectInputSystemArrays(value, childPath, oldToNew, sets, warnings);
    }
  }
}

// Every place in the project record an old spelling still appears, for the check afterwards.
function remainingOldSpellings(project, oldToNew) {
  const found = [];
  const olds = new Set(Object.keys(oldToNew));
  (function walk(node, path) {
    if (Array.isArray(node)) {
      node.forEach((value, i) => walk(value, path + '.' + i));
    } else if (isPlainObject(node)) {
      for (const [key, value] of Object.entries(node)) {
        if (olds.has(key)) found.push(path + '.' + key + ' (key)');
        // An input system's abbreviation and language name are labels, not spellings: sjs-flex's
        // "rif-Latn" is abbreviated "rif"
        if (key === 'abbreviation' || key === 'languageName') continue;
        walk(value, path + '.' + key);
      }
    } else if (typeof node === 'string' && olds.has(node)) {
      found.push(path);
    }
  })({ config: project.config, inputSystems: project.inputSystems, languageCode: project.languageCode }, '');
  return found;
}

function migrateProject(projects, projectCode, oldToNew) {
  const project = projects.findOne({ projectCode: projectCode });
  if (!project) {
    print('  ' + projectCode + ': NOT FOUND, skipped');
    return { found: false };
  }

  const sets = {};
  const unsets = {};
  const warnings = [];

  collectInputSystemArrays(project.config, 'config', oldToNew, sets, warnings);
  const arrays = Object.keys(sets).length;

  let renamed = 0;
  const inputSystems = project.inputSystems || {};
  for (const [oldTag, newTag] of Object.entries(oldToNew)) {
    if (!Object.prototype.hasOwnProperty.call(inputSystems, oldTag)) {
      continue;
    }
    if (Object.prototype.hasOwnProperty.call(inputSystems, newTag)) {
      // Already spelled the new way, most likely by the new LfMerge build. The entry under the old
      // spelling may then be a different writing system (see kxpw-flex above), so leave both alone.
      warnings.push('inputSystems already has ' + newTag + '; left ' + oldTag + ' as it is');
      continue;
    }
    sets['inputSystems.' + newTag] = Object.assign({}, inputSystems[oldTag], { tag: newTag });
    unsets['inputSystems.' + oldTag] = '';
    renamed++;
  }

  let languageCode = null;
  if (Object.prototype.hasOwnProperty.call(oldToNew, project.languageCode)) {
    languageCode = { from: project.languageCode, to: oldToNew[project.languageCode] };
    sets['languageCode'] = languageCode.to;
  }

  const mapping = Object.entries(oldToNew).map(([o, n]) => o + ' -> ' + n).join(', ');
  print('  ' + projectCode + ': ' + mapping);
  print('      config inputSystems arrays re-spelled: ' + arrays);
  print('      inputSystems record entries renamed:   ' + renamed);
  print('      languageCode:                          ' +
    (languageCode ? languageCode.from + ' -> ' + languageCode.to : 'unchanged'));
  warnings.forEach(w => print('      WARNING: ' + w));

  if (arrays === 0 && renamed === 0 && !languageCode) {
    print('      nothing to do (already migrated?)');
    return { found: true, changed: false };
  }
  if (DRY_RUN) {
    return { found: true, changed: true };
  }

  const update = {};
  if (Object.keys(sets).length > 0) update.$set = sets;
  if (Object.keys(unsets).length > 0) update.$unset = unsets;
  const result = projects.updateOne({ _id: project._id }, update);
  print('      updated: matched ' + result.matchedCount + ', modified ' + result.modifiedCount);

  const left = remainingOldSpellings(projects.findOne({ _id: project._id }), oldToNew);
  if (left.length > 0) {
    print('      WARNING: old spellings still present at:');
    left.forEach(p => print('        ' + p));
  } else {
    print('      verified: no old spelling remains in config, inputSystems or languageCode');
  }
  return { found: true, changed: true };
}

// A multitext: a subdocument every one of whose values is a { value: ... } string field.
function isMultiText(node) {
  if (!isPlainObject(node)) return false;
  const values = Object.values(node);
  return values.length > 0 && values.every(v => isPlainObject(v) && Object.prototype.hasOwnProperty.call(v, 'value'));
}

// Collect a $set for every multitext under node that has an old key, renaming the key where it
// stands so that the keys keep their order. A multitext that already has the new key as well is
// left alone and reported: which of the two to keep is not this script's call.
function collectLexiconRekeys(node, path, oldToNew, sets, warnings) {
  if (Array.isArray(node)) {
    node.forEach((value, i) => collectLexiconRekeys(value, path + '.' + i, oldToNew, sets, warnings));
    return;
  }
  if (!isPlainObject(node)) {
    return;
  }
  if (isMultiText(node)) {
    const olds = Object.keys(node).filter(key => Object.prototype.hasOwnProperty.call(oldToNew, key));
    if (olds.length === 0) {
      return;
    }
    if (olds.some(key => Object.prototype.hasOwnProperty.call(node, oldToNew[key]))) {
      warnings.push(path + ' has both spellings; left as it is');
      return;
    }
    if (path.split('.').some(segment => segment.includes('$'))) {
      warnings.push('cannot address ' + path + ' in an update; left as it is');
      return;
    }
    const renamed = {};
    for (const [key, value] of Object.entries(node)) {
      renamed[Object.prototype.hasOwnProperty.call(oldToNew, key) ? oldToNew[key] : key] = value;
    }
    sets[path] = renamed;
    return;
  }
  for (const [key, value] of Object.entries(node)) {
    // A key with a dot in it cannot be addressed by a dotted path
    if (key.includes('.')) {
      warnings.push('cannot address ' + path + ' key "' + key + '" in an update; left as it is');
      continue;
    }
    collectLexiconRekeys(value, path ? path + '.' + key : key, oldToNew, sets, warnings);
  }
}

// How many multitexts in the lexicon still have an old key.
function countOldKeys(lexicon, oldToNew) {
  let count = 0;
  lexicon.find({}).forEach(entry => {
    (function walk(node) {
      if (Array.isArray(node)) {
        node.forEach(walk);
      } else if (isPlainObject(node)) {
        if (isMultiText(node) && Object.keys(node).some(key => Object.prototype.hasOwnProperty.call(oldToNew, key))) {
          count++;
        }
        Object.values(node).forEach(walk);
      }
    })(entry);
  });
  return count;
}

function rekeyLexicon(projectCode, oldToNew) {
  const mapping = Object.entries(oldToNew).map(([o, n]) => o + ' -> ' + n).join(', ');
  print('  ' + projectCode + ' lexicon: ' + mapping);
  const lexicon = db.getSiblingDB('sf_' + projectCode).lexicon;
  let entries = 0;
  let multiTexts = 0;
  const warnings = [];
  lexicon.find({}).forEach(entry => {
    const sets = {};
    const entryWarnings = [];
    for (const [key, value] of Object.entries(entry)) {
      if (key === '_id') continue;
      collectLexiconRekeys(value, key, oldToNew, sets, entryWarnings);
    }
    entryWarnings.forEach(w => warnings.push('entry ' + entry.guid + ': ' + w));
    const count = Object.keys(sets).length;
    if (count === 0) {
      return;
    }
    entries++;
    multiTexts += count;
    if (!DRY_RUN) {
      lexicon.updateOne({ _id: entry._id }, { $set: sets });
    }
  });
  print('      entries re-keyed:    ' + entries);
  print('      multitexts re-keyed: ' + multiTexts);
  warnings.forEach(w => print('      WARNING: ' + w));
  if (!DRY_RUN && entries > 0) {
    const left = countOldKeys(lexicon, oldToNew);
    print(left === 0 ? '      verified: no old key remains in the lexicon'
      : '      WARNING: ' + left + ' multitexts still have an old key');
  }
  return entries > 0;
}

const projects = db.getSiblingDB(DB_NAME).projects;
print((DRY_RUN ? 'DRY RUN -- nothing will be changed. ' : '') + 'Database: ' + DB_NAME);
let changed = 0;
let missing = 0;
for (const [projectCode, oldToNew] of Object.entries(MIGRATIONS)) {
  const outcome = migrateProject(projects, projectCode, oldToNew);
  if (!outcome.found) missing++;
  else if (outcome.changed) changed++;
}
let lexiconsChanged = 0;
for (const [projectCode, oldToNew] of Object.entries(LEXICON_REKEYS)) {
  if (!projects.findOne({ projectCode: projectCode })) {
    print('  ' + projectCode + ' lexicon: project NOT FOUND, skipped');
    continue;
  }
  if (rekeyLexicon(projectCode, oldToNew)) lexiconsChanged++;
}
print('');
print((DRY_RUN ? 'Would change ' : 'Changed ') + changed + ' of ' + Object.keys(MIGRATIONS).length +
  ' projects' + (missing ? '; ' + missing + ' not found' : '') + '.');
print((DRY_RUN ? 'Would re-key ' : 'Re-keyed ') + lexiconsChanged + ' of ' + Object.keys(LEXICON_REKEYS).length +
  ' lexicons.');
if (DRY_RUN) {
  print('Run again with DRY_RUN=false to apply.');
}
