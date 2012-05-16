using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Palaso.Lift.Merging;
using Palaso.Xml;

namespace lfmergelift
{
	/// <summary>
	/// This class overrides MergeLiftUpdateEntryIntoLiftFile. Language Forge will produce .lift.update files with incomplete <entry></entry> elements.
	/// This languageForgeSynchronicMerger needs to be smart enough to know how to merge in this incomplete entry element into the entry found
	/// in the base LIFT file.  The incomplete entry element contains only the things which were actually changed in the lexical entry.
	/// </summary>
	internal class LfSynchronicMerger : SynchronicMerger
	{
		protected override void MergeLiftUpdateEntryIntoLiftFile(XmlReader olderReader, XmlWriter writer, XmlDocument newerDoc)
		{
			var oldId = olderReader.GetAttribute("guid");
			if (String.IsNullOrEmpty(oldId))
			{
				throw new ApplicationException("All entries must have guid attributes in order for merging to work. " + olderReader.Value);
			}
			//Search the lift.update file for an entry that matches the guid of the current entry being processed.
			XmlNode match = newerDoc.SelectSingleNode("//entry[@guid='" + oldId + "']");


			//XmlNode oldEntry
			if (match != null)
			{
				var oldEntry = olderReader.ReadOuterXml();
				//string xmlContent = "<foo></foo>";
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(oldEntry);
				XmlNode modifyNode = doc.DocumentElement;
				ModifyAnEntry(modifyNode, match);
				//If a matching entry was found in the lift.update file then replace the one in the original Lift file with it.
				//olderReader.Skip(); //skip the old one
				writer.WriteNode(modifyNode.CreateNavigator(), true); //REVIEW CreateNavigator
				if (match.ParentNode != null)
					match.ParentNode.RemoveChild(match); //remove the matching entry from the lift.update file now that the change has
				//been merged into the main file.
			}
			else
			{
				//write out the current original entry since no changes for it are found in the lift.update file.
				writer.WriteNode(olderReader, true);
			}
		}

		private static void ModifyAnEntry(XmlNode xnEntryModify, XmlNode xnEntryLiftUpdate)
		{
			//If the LiftUpdate entry is a deletion then just swap out the entire entry and return.
			var deletionDate = xnEntryLiftUpdate.Attributes["dateDeleted"];
			if (deletionDate != null)
			{
				SwapOutEntireElement(xnEntryModify, xnEntryLiftUpdate);
				return;
			}

			//Always replace all the attributes for an entry with the modified entry from the liftUpdate file.
			SwapOutAllAttributes(xnEntryModify, xnEntryLiftUpdate);

			MergeEntryLexicalUnit(xnEntryLiftUpdate, xnEntryModify);

			ModifySenses(xnEntryModify, xnEntryLiftUpdate);
		}

		private static void SwapOutEntireElement(XmlNode nodeToModify, XmlNode nodefromLiftUpdate)
		{
			nodeToModify.InnerXml = nodefromLiftUpdate.InnerXml;
			SwapOutAllAttributes(nodeToModify, nodefromLiftUpdate);
		}

		private static void ModifySenses(XmlNode entryToModify, XmlNode entryFromLiftUpdate)
		{
			XmlNodeList xnlSenses = entryToModify.SelectNodes("sense");
			XmlNodeList xnlSensesUpdate = entryFromLiftUpdate.SelectNodes("sense");
			Dictionary<String, XmlNode> mainEntrySensesDic = new Dictionary<string, XmlNode>();
			Dictionary<String, XmlNode> liftUpdateEntrySensesDic = new Dictionary<string, XmlNode>();
			PoplulateSenseDictionary(xnlSenses, mainEntrySensesDic);
			PoplulateSenseDictionary(xnlSensesUpdate, liftUpdateEntrySensesDic);

			//if a sense is found missing/removed in the LiftUpdate entry then remove it from the main entry.
			foreach (XmlNode xnSense in xnlSenses)
			{
				var senseId = xnSense.Attributes["id"].Value;
				//if this sense is not found in the LiftUpdate entry then remove it from the entry we are modifying.
				if (!String.IsNullOrEmpty(senseId) && !liftUpdateEntrySensesDic.ContainsKey(senseId))
				{
					entryToModify.RemoveChild(xnSense);
					//also remove it from the dictionary since we should not reference it again.
					mainEntrySensesDic.Remove(senseId);
				}
			}

			foreach (XmlNode xnSenseLiftUpdate in xnlSensesUpdate)
			{
				var senseId = xnSenseLiftUpdate.Attributes["id"].Value;
				//if a matching sense has been found then swap out the elements that from the LiftUpdate element.
				XmlNode senseToModify;
				if (mainEntrySensesDic.TryGetValue(senseId, out senseToModify))  //if this sense is not found in the main entry then just add the whole thing
				{
					//When a matching sense is found these are the things with Language Forge can change so we need to replace these elements in the sense.
					//<grammatical-info>
					//<gloss>
					//<definition>
					//<example>

					MergeInSenseChanges(senseToModify, xnSenseLiftUpdate);
				}
				else //if a new sense is found in the LiftUpdate entry (no match found in the main entry then add it to the entry
				{
					XmlNode nodeToInsert = entryToModify.OwnerDocument.ImportNode(xnSenseLiftUpdate, true);
					entryToModify.AppendChild(nodeToInsert);
				}
			}
		}

		private static void MergeInSenseChanges(XmlNode senseToModify, XmlNode senseFromLiftUpdate)
		{
			MakeChildNodeSameAsThatInLiftUpdate(senseToModify, senseFromLiftUpdate, "grammatical-info");

			//definition does not always exist in a Flex LiftOutput file.
			MakeChildNodeInnerXmlSameAsThatInLiftUpdate(senseToModify, senseFromLiftUpdate, "definition");

			XmlNodeList glossesToRemove = senseToModify.SelectNodes("gloss");
			XmlNodeList glossesToAdd = senseFromLiftUpdate.SelectNodes("gloss");
			if (glossesToRemove != null)
			{
				foreach (XmlNode gloss in glossesToRemove)
				{
					senseToModify.RemoveChild(gloss);
				}
			}
			if (glossesToAdd != null)
			{
				foreach (XmlNode glossToAdd in glossesToAdd)
				{
					AddChildNode(senseToModify, "gloss", glossToAdd);
				}
			}

			MergeInExampleChanges(senseToModify, senseFromLiftUpdate);
		}

		private static void MergeInExampleChanges(XmlNode senseToModify, XmlNode senseFromLiftUpdate)
		{
			//first step: create a dictionary of the 'example' nodes from senseFromLiftUpdate with the example sentences and translation as the key
			//NOTE: the key should be changed to a guid id when that is made part of the LIFT standard
			//next step: go through each example from the senseToModify,
			//if the text matches any key in the dictionary then keep that node and remove that entry in the dictionary,
			//   otherwise delete the node since it would seem like that example was deleted in Language Forge.
			//next step: add the remaining nodes from the dictionary to the senseToModify
			XmlNodeList examplesFromLift = senseToModify.SelectNodes("example");
			XmlNodeList examplesFromLiftUpdate = senseFromLiftUpdate.SelectNodes("example");

			//first step:
			var examplesDict = new Dictionary<string, XmlNode>();
			foreach (XmlNode exampleNode in examplesFromLiftUpdate)
			{
				string keyForExample = GetKeyForExample(exampleNode);
				examplesDict.Add(keyForExample, exampleNode);
			}

			//next step:
			foreach (XmlNode exampleFromLift in examplesFromLift)
			{
				string keyForExample = GetKeyForExample(exampleFromLift);
				if (examplesDict.ContainsKey(keyForExample))
				{
					//assumption: Language Forge did not make any changes to this example so keep it and remove it from the dictionary
					//since we do not need to add it later.
					examplesDict.Remove(keyForExample);
				}
				else
				{
					//assumption: Language Forge either made changes to this example or removed it so remove it from the senseToModify
					//if changes were made it will be added in the next step when we add all the example nodes from the dictionary
					senseToModify.RemoveChild(exampleFromLift);
				}
			}

			//next step:  add all  the remaining nodes in the dictionary
			foreach (var dict in examplesDict)
			{
				XmlNode nodeToAdd = dict.Value;
				AddChildNode(senseToModify, "example", nodeToAdd);
			}
		}

		private static string GetKeyForExample(XmlNode exampleNode)
		{
			XmlNodeList forms = exampleNode.SelectNodes("form");
			XmlNodeList defForms = exampleNode.SelectNodes("translation/form");
			var strBldr = new StringBuilder("");
			foreach (XmlNode form in forms)
			{
				strBldr.Append(form.OuterXml);
			}
			foreach (XmlNode form in defForms)
			{
				strBldr.Append(form.OuterXml);
			}
			return strBldr.ToString();
		}

		/// <summary>
		/// Changes nodeToModify so that it's child node 'childNodeName', will match what is found in nodeFromLiftUpdate. Only change the innerXml. Remove or add the node as
		/// needed.
		/// </summary>
		/// <param name="nodeToModify"></param>
		/// <param name="nodeFromLiftUpdate"></param>
		/// <param name="childNodeName"></param>
		private static void MakeChildNodeInnerXmlSameAsThatInLiftUpdate(XmlNode nodeToModify, XmlNode nodeFromLiftUpdate, String childNodeName)
		{
			if (ElementHasChildElement(nodeToModify, childNodeName) && ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				nodeToModify.SelectSingleNode(childNodeName).InnerXml = nodeFromLiftUpdate.SelectSingleNode(childNodeName).InnerXml;
			}
			else if (ElementHasChildElement(nodeToModify, childNodeName) && !ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				//Language Forge should always have a definition element but if for some odd reason it does not then assume we are deleting
				//the definition element
				nodeToModify.RemoveChild(nodeToModify.SelectSingleNode(childNodeName));
			}
			else if (!ElementHasChildElement(nodeToModify, childNodeName) && ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				AddXmlElement(nodeToModify, childNodeName);
				nodeToModify.SelectSingleNode(childNodeName).InnerXml = nodeFromLiftUpdate.SelectSingleNode(childNodeName).InnerXml;
			}
		}

		/// <summary>
		/// Copy a child node. This requires first creating the child node then copying the contents of the source child node
		/// to the new target child node.
		/// </summary>
		/// <param name="nodeToModify"></param>
		/// <param name="childNodeName"></param>
		/// <param name="nodeFromLiftUpdate"></param>
		private static void CopyChildNode(XmlNode nodeToModify, string childNodeName, XmlNode nodeFromLiftUpdate)
		{
			XmlNode newNode = AddXmlElement(nodeToModify, childNodeName);
			SwapOutEntireElement(newNode, nodeFromLiftUpdate.SelectSingleNode(childNodeName));
		}

		/// <summary>
		/// Copy a child node. This requires first creating the child node then copying the contents of the source child node
		/// to the new target child node.
		/// </summary>
		/// <param name="nodeToModify"></param>
		/// <param name="childNodeName"></param>
		/// <param name="nodeFromLiftUpdate"></param>
		private static void AddChildNode(XmlNode nodeToModify, string childNodeName, XmlNode nodeToCopyContentsFrom)
		{
			XmlNode newNode = AddXmlElement(nodeToModify, childNodeName);
			SwapOutEntireElement(newNode, nodeToCopyContentsFrom);
		}

		/// <summary>
		/// Changes nodeToModify so that it's child node 'childNodeName', will match what is found in nodeFromLiftUpdate. Cange the  attributes and innerXml. Remove or add the node as
		/// needed.
		/// </summary>
		/// <param name="nodeToModify"></param>
		/// <param name="nodeFromLiftUpdate"></param>
		/// <param name="childNodeName"></param>
		private static void MakeChildNodeSameAsThatInLiftUpdate(XmlNode nodeToModify, XmlNode nodeFromLiftUpdate, String childNodeName)
		{
			if (ElementHasChildElement(nodeToModify, childNodeName) && ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				SwapOutEntireElement(nodeToModify.SelectSingleNode(childNodeName), nodeFromLiftUpdate.SelectSingleNode(childNodeName));
			}
			else if (ElementHasChildElement(nodeToModify, childNodeName) && !ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				nodeToModify.RemoveChild(nodeToModify.SelectSingleNode(childNodeName));
			}
			else if (!ElementHasChildElement(nodeToModify, childNodeName) && ElementHasChildElement(nodeFromLiftUpdate, childNodeName))
			{
				CopyChildNode(nodeToModify, childNodeName, nodeFromLiftUpdate);
			}
		}

		private static void SwapOutAllAttributes(XmlNode targetNode, XmlNode sourceNode)
		{
			targetNode.Attributes.RemoveAll();
			foreach (XmlAttribute attr in sourceNode.Attributes)
			{
				AddXmlAttribute(targetNode, attr.Name, attr.Value);
			}
		}

		private static void MergeEntryLexicalUnit(XmlNode xnEntryLiftUpdate, XmlNode xnEntryModify)
		{
			//Swap out the lexican-unit with that found in the LiftUpdate entry
			//<lexical-unit>
			if (ElementHasChildElement(xnEntryModify, "lexical-unit") && ElementHasChildElement(xnEntryLiftUpdate, "lexical-unit"))
			{
				xnEntryModify.SelectSingleNode("lexical-unit").InnerXml = xnEntryLiftUpdate.SelectSingleNode("lexical-unit").InnerXml;
			}
			else if (!ElementHasChildElement(xnEntryLiftUpdate, "lexical-unit"))
			{
				//do not modify anything since the LiftUpdate has not lexical unit in it.
			}
			else if (!ElementHasChildElement(xnEntryModify, "lexical-unit") && ElementHasChildElement(xnEntryLiftUpdate, "lexical-unit"))
			{
				//The entry has not lexical unit but the update has one so add it. Then merge in the contents from the liftUpdate entry.
				AddXmlElement(xnEntryModify, "lexical-unit");
				xnEntryModify.SelectSingleNode("lexical-unit").InnerXml = xnEntryLiftUpdate.SelectSingleNode("lexical-unit").InnerXml;
			}
		}

		private static bool ElementHasAttribute(XmlNode node, String attribute)
		{
			XmlAttribute attr = node.Attributes[attribute];
			if (attr == null)
				return false;
			else
				return true;
		}

		private static bool ElementHasChildElement(XmlNode node, String elementName)
		{
			XmlNode element = node.SelectSingleNode(elementName);
			if (element == null)
				return false;
			else
				return true;
		}

		private static void PoplulateSenseDictionary(XmlNodeList xnlSenses, Dictionary<string, XmlNode> mainEntrySenses)
		{
			foreach (XmlNode node in xnlSenses)
			{
				var senseId = node.Attributes["id"];
				mainEntrySenses.Add(senseId.Value, node);
			}
		}

		private static XmlNode AddXmlElement(XmlNode xnode, string sName)
		{
			XmlNode xnNew = xnode.OwnerDocument.CreateElement(sName);
			xnode.AppendChild(xnNew);
			return xnNew;
		}

		private static void AddXmlAttribute(XmlNode xnode, string sName, string sValue)
		{
			XmlAttribute xa = xnode.OwnerDocument.CreateAttribute(sName);
			xa.Value = sValue;
			xnode.Attributes.Append(xa);
		}

		private static void AddXmlText(XmlNode xnode, string sText)
		{
			XmlText xtext = xnode.OwnerDocument.CreateTextNode(sText);
			xnode.AppendChild(xtext);
		}

	}
}