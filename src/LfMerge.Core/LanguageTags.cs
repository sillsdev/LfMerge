using System;
using System.Linq;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
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
		/// LfWsToLcmWs does not need this -- GetOrSet canonicalizes for it -- but every other lookup
		/// of an LF tag does, and should go through <see cref="WsIdFromLfTag"/> rather than calling
		/// this and GetWsFromStr itself.
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

		/// <summary>
		/// The handle of the LCM writing system a Language Forge tag names, or 0 when there is none.
		///
		/// Every place that resolves a tag LF supplied -- a multitext key, a span's lang attribute,
		/// a multi-paragraph's input system -- goes through here, so that a non-canonical spelling
		/// resolves the same way everywhere.
		///
		/// LCM knows a writing system by its Id, which is whatever its .ldml file is named, and also
		/// has its LanguageTag, which is always canonical. The two differ in older projects:
		/// brb-flex-2022 holds "km-Khmr-KH.ldml", whose identity is km plus KH, so its Id is
		/// "km-Khmr-KH" and its LanguageTag "km-KH". LfMerge writes LF's multitext keys by Id and the
		/// project's input systems by LanguageTag, so LF can hand back either spelling. Tried in turn:
		///
		///   1. The tag as LF spells it, as an Id. This must come first. brb-flex-2022 also holds a
		///      second, unused writing system whose Id is "km-KH"; trying the canonical form first
		///      sent the text under "km-Khmr-KH" there, and WriteToLcm then cleared the real
		///      writing system's text as missing from LF.
		///   2. Its canonical form, as an Id: the Id LCM gives any writing system it creates.
		///   3. Its canonical form as a LanguageTag, for an Id spelled some other way:
		///      spt-flex's "hi-IN" is the writing system LCM holds as "hi-Deva-IN". Should several
		///      writing systems share the LanguageTag, the one created first wins.
		/// </summary>
		public static int WsIdFromLfTag(ILgWritingSystemFactory wsf, string tag)
		{
			if (string.IsNullOrEmpty(tag))
			{
				return 0;
			}
			int wsId = wsf.GetWsFromStr(tag);
			if (wsId != 0)
			{
				return wsId;
			}
			string canonical = Canonical(tag);
			if (!string.Equals(canonical, tag, StringComparison.OrdinalIgnoreCase))
			{
				wsId = wsf.GetWsFromStr(canonical);
				if (wsId != 0)
				{
					return wsId;
				}
			}
			var wsManager = wsf as WritingSystemManager;
			if (wsManager == null)
			{
				return 0;
			}
			CoreWritingSystemDefinition byLanguageTag = wsManager.WritingSystems
				.Where(ws => string.Equals(ws.LanguageTag, canonical, StringComparison.OrdinalIgnoreCase))
				.OrderBy(ws => ws.Handle)
				.FirstOrDefault();
			return (byLanguageTag == null) ? 0 : byLanguageTag.Handle;
		}
	}
}
