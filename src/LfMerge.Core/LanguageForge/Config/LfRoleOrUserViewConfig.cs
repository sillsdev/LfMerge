using System.Collections.Generic;
using MongoDB.Bson;

namespace LfMerge.Core.LanguageForge.Config
{
	public class LexRoleOrUserViewConfig
	{
		public string[] InputSystems;
		public Dictionary<string,LexViewFieldConfig> Fields;
		public Dictionary<string,bool> ShowTasks;
	}

	public class LexViewFieldConfig
	{
		public bool Show;
		public string Type;

		public LexViewFieldConfig(string type, bool show = true)
		{
			Show = show;
			Type = type;
		}
		public LexViewFieldConfig(bool show = true) : this("basic", show) {}
	}

	public class LexViewMultiTextFieldConfig: LexViewFieldConfig
	{
		public bool OverrideInputSystems;
		public string[] InputSystems;

		public LexViewMultiTextFieldConfig(bool show = true) : base("multitext", show)
		{
			this.OverrideInputSystems = false;
			this.InputSystems = new string[]{};
		}
	}

	public static class LexViewFieldConfigFactory
	{
		public static LexViewFieldConfig CreateByType(string lfCustomFieldType, bool show = true) {
			if (lfCustomFieldType == "MultiUnicode" || lfCustomFieldType == "MultiString") {
				return new LexViewMultiTextFieldConfig(show);
			} else {
				return new LexViewFieldConfig(show);
			}
		}

		public static BsonDocument CreateBsonDocumentByType(string lfCustomFieldType, bool show = true) {
			LexViewFieldConfig config = CreateByType(lfCustomFieldType, show);
			var result = new BsonDocument();
			result.Set("show", new BsonBoolean(config.Show));
			result.Set("type", new BsonString(config.Type));
			var multiTextConfig = config as LexViewMultiTextFieldConfig;
			if (multiTextConfig != null) {
				result.Set("overrideInputSystems", new BsonBoolean(multiTextConfig.OverrideInputSystems));
				if (multiTextConfig.InputSystems != null && multiTextConfig.InputSystems.Length > 0) {
					result.Set("inputSystems", new BsonArray(multiTextConfig.InputSystems));
				}
			}
			return result;
		}
	}
}
