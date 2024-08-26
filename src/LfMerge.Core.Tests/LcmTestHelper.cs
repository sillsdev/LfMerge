using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.FieldWorks;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;

namespace LfMerge.Core.Tests
{
	public static class LcmTestHelper
	{
		public static IEnumerable<ILexEntry> GetEntries(FwProject project)
		{
			return project?.ServiceLocator?.LanguageProject?.LexDbOA?.Entries ?? [];
		}

		public static int CountEntries(FwProject project)
		{
			var repo = project?.ServiceLocator?.GetInstance<ILexEntryRepository>();
			return repo.Count;
		}

		public static ILexEntry GetEntry(FwProject project, Guid guid)
		{
			var repo = project?.ServiceLocator?.GetInstance<ILexEntryRepository>();
			return repo.GetObject(guid);
		}

		public static ILexEntry GetFirstEntry(FwProject project)
		{
			var repo = project?.ServiceLocator?.GetInstance<ILexEntryRepository>();
			return repo.AllInstances().First();
		}

		public static string? GetVernacularText(IMultiUnicode field)
		{
			return field.BestVernacularAlternative?.Text;
		}

		public static string? GetAnalysisText(IMultiUnicode field)
		{
			return field.BestAnalysisAlternative?.Text;
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

		public static string UpdateVernacularText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestVernacularAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetVernacularText(project, field, newText);
			}
			return oldText;
		}

		public static string UpdateAnalysisText(FwProject project, IMultiUnicode field, Func<string, string> textConverter)
		{
			var oldText = field.BestAnalysisAlternative?.Text;
			if (oldText != null)
			{
				var newText = textConverter(oldText);
				SetAnalysisText(project, field, newText);
			}
			return oldText;
		}
	}
}
