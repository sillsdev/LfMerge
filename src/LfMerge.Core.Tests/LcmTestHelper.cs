using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.FieldWorks;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;
using SIL.PlatformUtilities;
using SIL.Progress;

namespace LfMerge.Core.Tests
{
	public static class LcmTestHelper
	{
		public static IEnumerable<ILexEntry> GetEntries(FwProject project)
		{
			return project?.ServiceLocator?.LanguageProject?.LexDbOA?.Entries ?? [];
		}

		public static ILexEntry GetEntry(FwProject project, Guid guid)
		{
			var repo = project?.ServiceLocator?.GetInstance<ILexEntryRepository>();
			return repo.GetObject(guid);
		}

		public static void SetVernacularText(FwProject project, IMultiUnicode field, string newText)
		{
			var accessor = project.Cache.ActionHandlerAccessor;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", accessor, () => {
				field.SetVernacularDefaultWritingSystem(newText);
			});
		}

		public static void SetAnalysisText(FwProject project, IMultiUnicode field, string newText)
		{
			var accessor = project.Cache.ActionHandlerAccessor;
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", accessor, () => {
				field.SetAnalysisDefaultWritingSystem(newText);
			});
		}

		public static void UpdateVernacularText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestVernacularAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetVernacularText(project, field, newText);
			}
		}

		public static void UpdateAnalysisText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestAnalysisAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetAnalysisText(project, field, newText);
			}
		}
	}
}
