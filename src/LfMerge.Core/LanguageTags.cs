using SIL.WritingSystems;

namespace LfMerge.Core
{
	public static class LanguageTags
	{
		/// <summary>
		/// The canonical form of a language tag: the id LCM knows a writing system by, once it has
		/// created one from this tag.
		///
		/// Language Forge stores a tag exactly as it was typed. LCM canonicalizes it when the writing
		/// system is created -- WritingSystemManager.GetOrSet and Create both do -- and thereafter
		/// GetWsFromStr matches that canonical id literally. It is case-insensitive, so a difference
		/// of case resolves on its own, but it is not structure-aware, so a tag that loses or absorbs
		/// a subtag is never found:
		///
		///     qaa-x-qaa-v -> qaa-x-v   the private-use section repeats the "qaa" language subtag,
		///                              and canonicalizing absorbs it
		///     th-Thai     -> th        Thai's suppress-script is dropped
		///
		/// LfWsToLcmWs does not need this -- GetOrSet canonicalizes for it -- but the lexicon does:
		/// its multitext keys are LF tags being looked up against LCM's canonical ids.
		/// </summary>
		/// <returns>
		/// The canonical tag, or the tag unchanged when it is empty or cannot be parsed --
		/// IetfLanguageTag.Canonicalize throws on those, and an unparseable tag should go on failing
		/// where it failed before rather than here.
		/// </returns>
		public static string Canonical(string tag)
		{
			if (string.IsNullOrEmpty(tag) || !IetfLanguageTag.IsValid(tag))
			{
				return tag;
			}
			return IetfLanguageTag.Canonicalize(tag);
		}
	}
}
