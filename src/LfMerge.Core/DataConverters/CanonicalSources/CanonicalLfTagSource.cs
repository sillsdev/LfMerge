// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using LfMerge.Core.FieldWorks;
using SIL.LCModel;
using System.Collections.Generic;

namespace LfMerge.Core.DataConverters.CanonicalSources
{
	public class CanonicalLfTagSource : CanonicalOptionListSource
	{
		public CanonicalLfTagSource()
			: base("canonical-lf-tags.xml", "item")
		{
		}

		public override void LoadCanonicalData()
		{
			LoadCanonicalData<CanonicalLfTagItem>();
		}

		public ICmPossibilityList EnsureLcmPossibilityListExists(FwServiceLocatorCache serviceLocator, System.Guid parentListGuid, string listName, int wsForKeys) {
			if (byKey.Count == 0) {
				LoadCanonicalData();
			}
			var repo = serviceLocator.GetInstance<ICmPossibilityListRepository>();
			var listFactory = serviceLocator.GetInstance<ICmPossibilityListFactory>();
			var guid = new System.Guid(MagicStrings.LcmCustomFieldGuidForLfTags);
			ICmPossibilityList possList;
			if (!repo.TryGetObject(guid, out possList) {
				possList = listFactory.CreateUnowned(guid, MagicStrings.LcmCustomFieldNameForLfTags, wsForKeys);
			}
			var converter = new ConvertMongoToLcmOptionList(serviceLocator.GetInstance<ICmPossibilityRepository>(), null, null, possList, wsEn, this);
			foreach (KeyValuePair<string,CanonicalItem> item in this.byKey)
			{
				converter.FindOrCreateFromCanonicalItem(item.Value);
			}
			// TODO: Write acceptance test that verifies that a CmPossibilityList with the right GUID gets created and populated.
			return possList;
		}
	}
}
