// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.IO;

namespace LfMerge.Tests
{
	public class SampleData
	{
		public BsonDocument bsonTestData;
		public BsonDocument bsonConfigData;
		public BsonDocument bsonProjectRecordData;
		public BsonDocument bsonOptionListData;
		public BsonDocument bsonSemDomData;

		public SampleData()
		{
			bsonTestData = BsonSerializer.Deserialize<BsonDocument>(jsonTestData);
			bsonConfigData = BsonSerializer.Deserialize<BsonDocument>(jsonConfigData);
			bsonProjectRecordData = BsonSerializer.Deserialize<BsonDocument>(jsonProjectRecordData);
			bsonOptionListData = BsonSerializer.Deserialize<BsonDocument>(jsonOptionListData);
			// Semantic domain data is found in $GITROOT/data/semantic-domains/semdom.json
			string gitRoot = Fdo.FdoTestFixture.FindGitRepoRoot();
			string semDomFilename = Path.Combine(gitRoot, "data", "semantic-domains", "semdom.json");
			string jsonSemDomData = File.ReadAllText(semDomFilename);
			bsonSemDomData = BsonSerializer.Deserialize<BsonDocument>(jsonSemDomData);
		}

		#region JSON Test Data
		public static string jsonTestData = @"{
	""_id"" : ObjectId(""56332f680f8709ed0fd92d6c""),
	""authorInfo"" : {
		""createdByUserRef"" : null,
		""createdDate"" : ISODate(""2004-10-19T02:42:02Z""),
		""modifiedByUserRef"" : ObjectId(""561b666c0f87096a35c3cf2d""),
		""modifiedDate"" : ISODate(""2015-11-18T03:56:45Z"")
	},
	""citationForm"" : {
		""qaa-x-kal"" : {
			""value"" : ""ztestmain""
		}
	},
	""customFields"" : {
		""customField_entry_Cust_MultiPara"" : {
			""ws"": ""en"",
			""paras"" : [
				{
					""guid"" : ""5c8fb347-d884-4db5-9415-64b139b663ff"",
					""styleName"" : ""Normal"",
					""contents"" : ""This is the first paragraph."",
				},
				{
					""guid"" : ""061a4bad-eaa8-4365-9a19-8150621a9a44"",
					""contents"" : ""This is the second paragraph."",
				}
			]
		},
		""customField_entry_Cust_Single_Line"" : {
			""en"" : {
				""value"" : ""Some single custom text""
			}
		},
		""customField_entry_Cust_Single_Line_All"" : {
			""en"" : {
				""value"" : ""Some custom text""
			},
			""fr"" : {
				""value"" : ""French custom text""
			}
		},
		""customField_entry_Cust_Single_ListRef"" : {
			""value"" : ""comparative linguistics""
		}
	},
	""customFieldGuids"" : {
		""customField_entry_Cust_MultiPara"" : ""cd05615d-c47b-4e6f-8b4e-29285ababcee"",
		""customField_entry_Cust_Single_ListRef"" : ""d7f713ad-e8cf-11d3-9764-00c04f186933""
	},
	""cvPattern"" : {
		""qaa-x-kal"" : {
			""value"" : ""CCVCCCVVC""
		}
	},
	""dateCreated"" : ISODate(""2015-10-30T08:50:48Z""),
	""dateModified"" : ISODate(""2015-11-18T03:56:45Z""),
	""entryBibliography"" : { },
	""entryRestrictions"" : { },
	""environments"" : {
		""values"" : [ ]
	},
	""etymology"" : {
		""qaa-x-kal"" : {
			""value"" : ""etymology""
		}
	},
	""etymologyComment"" : {
		""en"" : {
			""value"" : ""English comment""
		},
		""fr"" : {
			""value"" : ""French comment""
		}
	},
	""etymologyGloss"" : {
		""en"" : {
			""value"" : ""English gloss""
		}
	},
	""etymologySource"" : { },
	""guid"" : ""1a705846-a814-4289-8594-4b874faca6cc"",
	""isDeleted"" : false,
	""lexeme"" : {
		""qaa-fonipa-x-kal"" : {
			""value"" : ""zitʰɛstmen""
		},
		""qaa-x-kal"" : {
			""value"" : ""underlying form""
		}
	},
	""literalMeaning"" : {
		""en"" : {
			""value"" : ""The literal meaning""
		}
	},
	""location"" : {
		""value"" : ""witch doctor's house""
	},
	""mercurialSha"" : null,
	""morphologyType"" : ""root"",
	""note"" : {
		""en"" : {
			""value"" : """"
		},
		""fr"" : {
			""value"" : """"
		}
	},
	""pronunciation"" : {
		""qaa-x-kal"" : {
			""value"" : ""zee-test-mayn""
		}
	},
	""senses"" : [
		{
			""liftId"" : ""eea9c29f-244f-4891-81db-c8274cd61f0c"",
			""id"" : ""56332f6896739"",
			""partOfSpeech"" : {
				""value"" : ""noun""
			},
			""semanticDomain"" : {
				""values"" : [
					""1 Universe, creation"",
					""7 Physical actions""
				]
			},
			""examples"" : [
				{
					""liftId"" : ""c07286a6-3e58-43f5-96ca-db445a5d26d6"",
					""id"" : ""56332f6896f6d"",
					""guid"" : ""c07286a6-3e58-43f5-96ca-db445a5d26d6"",
					""sentence"" : {
						""qaa-x-kal"" : {
							""value"" : ""Vernacular example""
						}
					},
					""translation"" : {
						""en"" : {
							""value"" : ""English translation""
						},
						""fr"" : {
							""value"" : ""French translation""
						}
					},
					""customFields"" : {
						""customField_examples_Cust_Example"" : {
							""qaa-x-kal"" : {
								""value"" : ""Custom example""
							},
							""qaa-fonipa-x-kal"" : {
								""value"" : ""Custom IPA example""
							}
						}
					}
				},
				{
					""liftId"" : ""f387772c-48a0-4aeb-8211-345d329a10ea"",
					""id"" : ""56332f68970d7"",
					""guid"" : ""f387772c-48a0-4aeb-8211-345d329a10ea"",
					""sentence"" : {
						""qaa-x-kal"" : {
							""value"" : ""Second vernacular example""
						}
					},
					""translation"" : {
						""en"" : {
							""value"" : ""Second English translation""
						},
						""fr"" : {
							""value"" : ""Second French translation""
						}
					}
				}
			],
			""customFields"" : {
				""customField_senses_Cust_Multi_ListRef"" : {
					""values"" : [
						""First Custom Item"",
						""Second Custom Item""
					]
				}
			},
			""customFieldGuids"" : {
				""customField_senses_Cust_Multi_ListRef"" : [
					""85c48e72-218d-4fac-bb82-41a5eb9e268a"",
					""43b38fe1-706a-4288-9404-462daf8f283b""
				]
			},
			""definition"" : {
				""en"" : {
					""value"" : ""This is the English definition""
				},
				""fr"" : {
					""value"" : ""This is the French definition""
				}
			},
			""gloss"" : {
				""en"" : {
					""value"" : ""English gloss""
				},
				""fr"" : {
					""value"" : ""French gloss""
				},
				""es"" : {
					""value"" : """"
				},
				""qaa-x-kal"" : {
					""value"" : """"
				}
			},
			""pictures"" : [
				{ ""fileName"" : ""Picture1.jpg"" ,
					""caption"" : {
						""qaa-x-kal"" : {
							""value"" : ""First Vernacular caption""
						},
						""en"": {
							""value"" : ""First Analysis caption""
						}
					},
					""guid"" : ""567081ab-22f5-45d6-ad25-9c24f3bb5c6f""
				}],
			""scientificName"" : {
				""en"" : {
					""value"" : ""Scientific name""
				}
			},
			""anthropologyNote"" : { },
			""senseBibliography"" : { },
			""discourseNote"" : { },
			""encyclopedicNote"" : { },
			""generalNote"" : { },
			""grammarNote"" : { },
			""phonologyNote"" : { },
			""senseRestrictions"" : { },
			""semanticsNote"" : { },
			""sociolinguisticsNote"" : { },
			""source"" : { },
			""senseImportResidue"" : { },
			""usages"" : {
				""values"" : [
					""honorific"",
					""old-fashioned""
				]
			},
			""reversalEntries"" : {
				""values"" : [ ]
			},
			""senseType"" : {
				""value"" : ""figurative""
			},
			""academicDomains"" : {
				""values"" : [
					""education"",
					""graphology""
				]
			},
			""sensePublishIn"" : {
				""values"" : [ ]
			},
			""anthropologyCategories"" : {
				""values"" : [
					""130"",
					""111""
				]
			},
			""status"" : {
				""values"" : [
					""Confirmed""
				]
			}
		},
		{
			""liftId"" : ""59db2bec-d740-41c1-96df-f7843c5c6854"",
			""id"" : ""56332f68979d4"",
			""partOfSpeech"" : {
				""value"" : null
			},
			""semanticDomain"" : {
				""values"" : [ ]
			},
			""examples"" : [ ],
			""definition"" : {
				""en"" : {
					""value"" : ""This is the English definition for 2.""
				},
				""fr"" : {
					""value"" : ""This is the French definition for 2.""
				}
			},
			""gloss"" : {
				""en"" : {
					""value"" : ""English gloss2""
				},
				""fr"" : {
					""value"" : ""French gloss2""
				},
				""es"" : {
					""value"" : """"
				},
				""qaa-x-kal"" : {
					""value"" : """"
				}
			},
			""pictures"" : [ ],
			""scientificName"" : {
				""en"" : {
					""value"" : """"
				}
			},
			""anthropologyNote"" : { },
			""senseBibliography"" : { },
			""discourseNote"" : { },
			""encyclopedicNote"" : { },
			""generalNote"" : { },
			""grammarNote"" : { },
			""phonologyNote"" : { },
			""senseRestrictions"" : { },
			""semanticsNote"" : { },
			""sociolinguisticsNote"" : { },
			""source"" : { },
			""senseImportResidue"" : { },
			""usages"" : {
				""values"" : [ ]
			},
			""reversalEntries"" : {
				""values"" : [ ]
			},
			""senseType"" : {
				""value"" : null
			},
			""academicDomains"" : {
				""values"" : [ ]
			},
			""sensePublishIn"" : {
				""values"" : [ ]
			},
			""anthropologyCategories"" : {
				""values"" : [ ]
			},
			""status"" : {
				""values"" : [ ]
			}
		}
	],
	""summaryDefinition"" : {
		""en"" : {
			""value"" : ""English summary definition""
		},
		""fr"" : {
			""value"" : ""French summary definition""
		}
	},
	""tone"" : {
		""qaa-x-kal"" : {
			""value"" : ""A tone goes here""
		}
	}
}";
		#endregion

		#region JSON Config Data
		public static string jsonConfigData = @"{
		""tasks"" : {
			""view"" : {
				""visible"" : true,
				""type"" : """"
			},
			""dashboard"" : {
				""visible"" : true,
				""type"" : """"
			},
			""gatherTexts"" : {
				""visible"" : true,
				""type"" : """"
			},
			""semdom"" : {
				""visible"" : true,
				""type"" : """"
			},
			""wordlist"" : {
				""visible"" : true,
				""type"" : """"
			},
			""dbe"" : {
				""visible"" : true,
				""type"" : """"
			},
			""addMeanings"" : {
				""visible"" : true,
				""type"" : """"
			},
			""addGrammar"" : {
				""visible"" : true,
				""type"" : """"
			},
			""addExamples"" : {
				""visible"" : true,
				""type"" : """"
			},
			""review"" : {
				""visible"" : true,
				""type"" : """"
			},
			""importExport"" : {
				""visible"" : true,
				""type"" : """"
			},
			""configuration"" : {
				""visible"" : true,
				""type"" : """"
			}
		},
		""entry"" : {
			""fieldOrder"" : [
				""lexeme"",
				""citationForm"",
				""environments"",
				""pronunciation"",
				""cvPattern"",
				""tone"",
				""location"",
				""etymology"",
				""etymologyGloss"",
				""etymologyComment"",
				""etymologySource"",
				""note"",
				""literalMeaning"",
				""entryBibliography"",
				""entryRestrictions"",
				""summaryDefinition"",
				""entryImportResidue"",
				""senses"",
				""customField_entry_Cust_MultiPara"",
				""customField_entry_Cust_Single_Line"",
				""customField_entry_Cust_Single_Line_All"",
				""customField_entry_Cust_Single_ListRef""
			],
			""fields"" : {
				""lexeme"" : {
					""label"" : ""Word"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-fonipa-x-kal"",
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : """"
				},
				""senses"" : {
					""fieldOrder"" : [
						""definition"",
						""gloss"",
						""pictures"",
						""partOfSpeech"",
						""semanticDomain"",
						""scientificName"",
						""anthropologyNote"",
						""senseBibliography"",
						""discourseNote"",
						""encyclopedicNote"",
						""generalNote"",
						""grammarNote"",
						""phonologyNote"",
						""senseRestrictions"",
						""semanticsNote"",
						""sociolinguisticsNote"",
						""source"",
						""usages"",
						""reversalEntries"",
						""senseType"",
						""academicDomains"",
						""sensePublishIn"",
						""anthropologyCategories"",
						""senseImportResidue"",
						""status"",
						""examples"",
						""customField_senses_Cust_Multi_ListRef""
					],
					""fields"" : {
						""definition"" : {
							""label"" : ""Meaning"",
							""width"" : 20,
							""inputSystems"" : [
								""en"",
								""fr""
							],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : """"
						},
						""partOfSpeech"" : {
							""label"" : ""Part of Speech"",
							""listCode"" : ""grammatical-info"",
							""type"" : ""optionlist"",
							""hideIfEmpty"" : """"
						},
						""semanticDomain"" : {
							""label"" : ""Semantic Domain"",
							""listCode"" : ""semantic-domain-ddp4"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : """"
						},
						""examples"" : {
							""fieldOrder"" : [
								""sentence"",
								""translation"",
								""reference"",
								""examplePublishIn"",
								""customField_examples_Cust_Example""
							],
							""fields"" : {
								""sentence"" : {
									""label"" : ""Example"",
									""width"" : 20,
									""inputSystems"" : [
										""qaa-x-kal""
									],
									""displayMultiline"" : false,
									""type"" : ""multitext"",
									""hideIfEmpty"" : """"
								},
								""translation"" : {
									""label"" : ""Translation"",
									""width"" : 20,
									""inputSystems"" : [
										""en"",
										""fr""
									],
									""displayMultiline"" : false,
									""type"" : ""multitext"",
									""hideIfEmpty"" : """"
								},
								""reference"" : {
									""label"" : ""Reference"",
									""width"" : 20,
									""inputSystems"" : [ ],
									""displayMultiline"" : false,
									""type"" : ""multitext"",
									""hideIfEmpty"" : true
								},
								""examplePublishIn"" : {
									""label"" : ""Publish In"",
									""listCode"" : ""do-not-publish-in"",
									""type"" : ""multioptionlist"",
									""hideIfEmpty"" : true
								},
								""customField_examples_Cust_Example"" : {
									""label"" : ""Cust Example"",
									""width"" : 20,
									""inputSystems"" : [
										""qaa-x-kal"",
										""qaa-fonipa-x-kal""
									],
									""displayMultiline"" : false,
									""type"" : ""multitext"",
									""hideIfEmpty"" : false
								}
							},
							""type"" : ""fields"",
							""hideIfEmpty"" : """"
						},
						""gloss"" : {
							""label"" : ""Gloss"",
							""width"" : 20,
							""inputSystems"" : [
								""en"",
								""es"",
								""fr"",
								""qaa-x-kal""
							],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""pictures"" : {
							""label"" : ""Pictures"",
							""captionLabel"" : ""Captions"",
							""captionHideIfEmpty"" : true,
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""pictures"",
							""hideIfEmpty"" : true
						},
						""scientificName"" : {
							""label"" : ""Scientific Name"",
							""width"" : 20,
							""inputSystems"" : [
								""en""
							],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""anthropologyNote"" : {
							""label"" : ""Anthropology Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""senseBibliography"" : {
							""label"" : ""Bibliography"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""discourseNote"" : {
							""label"" : ""Discourse Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""encyclopedicNote"" : {
							""label"" : ""Encyclopedic Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""generalNote"" : {
							""label"" : ""General Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""grammarNote"" : {
							""label"" : ""Grammar Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""phonologyNote"" : {
							""label"" : ""Phonology Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""senseRestrictions"" : {
							""label"" : ""Restrictions"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""semanticsNote"" : {
							""label"" : ""Semantics Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""sociolinguisticsNote"" : {
							""label"" : ""Sociolinguistics Note"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""source"" : {
							""label"" : ""Source"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""usages"" : {
							""label"" : ""Usages"",
							""listCode"" : ""usage-type"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : true
						},
						""reversalEntries"" : {
							""label"" : ""Reversal Entries"",
							""listCode"" : ""reversal-type"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : true
						},
						""senseType"" : {
							""label"" : ""Type"",
							""listCode"" : ""sense-type"",
							""type"" : ""optionlist"",
							""hideIfEmpty"" : true
						},
						""academicDomains"" : {
							""label"" : ""Academic Domains"",
							""listCode"" : ""domain-type"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : true
						},
						""sensePublishIn"" : {
							""label"" : ""Publish In"",
							""listCode"" : ""do-not-publish-in"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : true
						},
						""anthropologyCategories"" : {
							""label"" : ""Anthropology Categories"",
							""listCode"" : ""anthro-code"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : true
						},
						""senseImportResidue"" : {
							""label"" : ""Import Residue"",
							""width"" : 20,
							""inputSystems"" : [ ],
							""displayMultiline"" : false,
							""type"" : ""multitext"",
							""hideIfEmpty"" : true
						},
						""status"" : {
							""label"" : ""Status"",
							""listCode"" : ""status"",
							""type"" : ""optionlist"",
							""hideIfEmpty"" : true
						},
						""customField_senses_Cust_Multi_ListRef"" : {
							""label"" : ""Cust Multi ListRef"",
							""listCode"" : ""Cust List"",
							""type"" : ""multioptionlist"",
							""hideIfEmpty"" : false
						}
					},
					""type"" : ""fields"",
					""hideIfEmpty"" : """"
				},
				""citationForm"" : {
					""label"" : ""Citation Form"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""environments"" : {
					""label"" : ""Environments"",
					""listCode"" : ""environments"",
					""type"" : ""multioptionlist"",
					""hideIfEmpty"" : true
				},
				""pronunciation"" : {
					""label"" : ""Pronunciation"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""cvPattern"" : {
					""label"" : ""CV Pattern"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""tone"" : {
					""label"" : ""Tone"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""location"" : {
					""label"" : ""Location"",
					""listCode"" : ""location"",
					""type"" : ""optionlist"",
					""hideIfEmpty"" : true
				},
				""etymology"" : {
					""label"" : ""Etymology"",
					""width"" : 20,
					""inputSystems"" : [
						""qaa-x-kal""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""etymologyGloss"" : {
					""label"" : ""Etymology Gloss"",
					""width"" : 20,
					""inputSystems"" : [
						""en""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""etymologyComment"" : {
					""label"" : ""Etymology Comment"",
					""width"" : 20,
					""inputSystems"" : [
						""en"",
						""fr""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""etymologySource"" : {
					""label"" : ""Etymology Source"",
					""width"" : 20,
					""inputSystems"" : [ ],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""note"" : {
					""label"" : ""Note"",
					""width"" : 20,
					""inputSystems"" : [
						""en"",
						""fr""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""literalMeaning"" : {
					""label"" : ""Literal Meaning"",
					""width"" : 20,
					""inputSystems"" : [
						""en""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""entryBibliography"" : {
					""label"" : ""Bibliography"",
					""width"" : 20,
					""inputSystems"" : [ ],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""entryRestrictions"" : {
					""label"" : ""Restrictions"",
					""width"" : 20,
					""inputSystems"" : [ ],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""summaryDefinition"" : {
					""label"" : ""Summary Definition"",
					""width"" : 20,
					""inputSystems"" : [
						""en"",
						""fr""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""entryImportResidue"" : {
					""label"" : ""Import Residue"",
					""width"" : 20,
					""inputSystems"" : [ ],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : true
				},
				""customField_entry_Cust_MultiPara"" : {
					""label"" : ""Cust MultiPara"",
					""width"" : 20,
					""inputSystems"" : [
						""en""
					],
					""displayMultiline"" : true,
					""type"" : ""multitext"",
					""hideIfEmpty"" : false
				},
				""customField_entry_Cust_Single_Line"" : {
					""label"" : ""Cust Single Line"",
					""width"" : 20,
					""inputSystems"" : [
						""en""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : false
				},
				""customField_entry_Cust_Single_Line_All"" : {
					""label"" : ""Cust Single Line All"",
					""width"" : 20,
					""inputSystems"" : [
						""en"",
						""fr""
					],
					""displayMultiline"" : false,
					""type"" : ""multitext"",
					""hideIfEmpty"" : false
				},
				""customField_entry_Cust_Single_ListRef"" : {
					""label"" : ""Cust Single ListRef"",
					""listCode"" : ""domain-type"",
					""type"" : ""optionlist"",
					""hideIfEmpty"" : false
				}
			},
			""type"" : ""fields"",
			""hideIfEmpty"" : """"
		},
		""roleViews"" : {
			""observer"" : {
				""fields"" : {
					""lexeme"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""definition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""partOfSpeech"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""semanticDomain"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sentence"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""translation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""gloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""pictures"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""citationForm"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""environments"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""pronunciation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""cvPattern"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""tone"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""location"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""etymology"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyGloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyComment"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologySource"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""note"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""literalMeaning"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""summaryDefinition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""scientificName"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""anthropologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""discourseNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""encyclopedicNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""generalNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""grammarNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""phonologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""semanticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""sociolinguisticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""source"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""usages"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reversalEntries"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseType"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""academicDomains"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sensePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""anthropologyCategories"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""status"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reference"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""examplePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_entry_Cust_MultiPara"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line_All"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_ListRef"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_examples_Cust_Example"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_senses_Cust_Multi_ListRef"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					}
				},
				""showTasks"" : {
					""view"" : true,
					""dashboard"" : true,
					""gatherTexts"" : false,
					""semdom"" : false,
					""wordlist"" : false,
					""dbe"" : true,
					""addMeanings"" : false,
					""addGrammar"" : false,
					""addExamples"" : false,
					""review"" : false
				}
			},
			""observer_with_comment"" : {
				""fields"" : {
					""lexeme"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""definition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""partOfSpeech"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""semanticDomain"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sentence"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""translation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""gloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""pictures"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""citationForm"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""environments"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""pronunciation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""cvPattern"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""tone"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""location"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""etymology"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyGloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyComment"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologySource"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""note"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""literalMeaning"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""summaryDefinition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""scientificName"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""anthropologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""discourseNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""encyclopedicNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""generalNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""grammarNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""phonologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""semanticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""sociolinguisticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""source"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""usages"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reversalEntries"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseType"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""academicDomains"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sensePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""anthropologyCategories"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""status"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reference"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""examplePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_entry_Cust_MultiPara"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line_All"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_ListRef"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_examples_Cust_Example"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_senses_Cust_Multi_ListRef"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					}
				},
				""showTasks"" : {
					""view"" : true,
					""dashboard"" : true,
					""gatherTexts"" : false,
					""semdom"" : false,
					""wordlist"" : false,
					""dbe"" : true,
					""addMeanings"" : false,
					""addGrammar"" : false,
					""addExamples"" : false,
					""review"" : false
				}
			},
			""contributor"" : {
				""fields"" : {
					""lexeme"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""definition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""partOfSpeech"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""semanticDomain"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sentence"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""translation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""gloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""pictures"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""citationForm"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""environments"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""pronunciation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""cvPattern"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""tone"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""location"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""etymology"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyGloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyComment"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologySource"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""note"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""literalMeaning"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""summaryDefinition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""scientificName"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""anthropologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""discourseNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""encyclopedicNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""generalNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""grammarNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""phonologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""semanticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""sociolinguisticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""source"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""usages"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reversalEntries"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseType"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""academicDomains"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sensePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""anthropologyCategories"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""status"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reference"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""examplePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_entry_Cust_MultiPara"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line_All"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_ListRef"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_examples_Cust_Example"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_senses_Cust_Multi_ListRef"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					}
				},
				""showTasks"" : {
					""view"" : true,
					""dashboard"" : true,
					""gatherTexts"" : false,
					""semdom"" : false,
					""wordlist"" : false,
					""dbe"" : true,
					""addMeanings"" : true,
					""addGrammar"" : true,
					""addExamples"" : true,
					""review"" : false
				}
			},
			""project_manager"" : {
				""fields"" : {
					""lexeme"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""definition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""partOfSpeech"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""semanticDomain"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sentence"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""translation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""gloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""pictures"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""citationForm"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""environments"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""pronunciation"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""cvPattern"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""tone"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""location"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""etymology"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyGloss"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologyComment"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""etymologySource"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""note"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""literalMeaning"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""summaryDefinition"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""entryImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""scientificName"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""anthropologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseBibliography"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""discourseNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""encyclopedicNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""generalNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""grammarNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""phonologyNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""senseRestrictions"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""semanticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""sociolinguisticsNote"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""source"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""usages"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reversalEntries"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseType"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""academicDomains"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""sensePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""anthropologyCategories"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""senseImportResidue"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""status"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""reference"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""examplePublishIn"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_entry_Cust_MultiPara"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_Line_All"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_entry_Cust_Single_ListRef"" : {
						""show"" : true,
						""type"" : ""basic""
					},
					""customField_examples_Cust_Example"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					},
					""customField_senses_Cust_Multi_ListRef"" : {
						""overrideInputSystems"" : false,
						""inputSystems"" : [ ],
						""show"" : true,
						""type"" : ""multitext""
					}
				},
				""showTasks"" : {
					""view"" : true,
					""dashboard"" : true,
					""gatherTexts"" : true,
					""semdom"" : true,
					""wordlist"" : true,
					""dbe"" : true,
					""addMeanings"" : true,
					""addGrammar"" : true,
					""addExamples"" : true,
					""review"" : true
				}
			}
		},
		""userViews"" : { }
	}";
		#endregion

		#region JSON Project Record Data
		public static string jsonProjectRecordData = @"{
	""_id"" : ObjectId(""56332f5d0f8709ed0f582326""),
	""allowAudioDownload"" : true,
	""allowInviteAFriend"" : true,
	""appName"" : ""lexicon"",
	""config"" : " + jsonConfigData + @",
	""dateCreated"" : ISODate(""2015-10-30T08:50:37Z""),
	""dateModified"" : ISODate(""2015-11-18T03:55:29Z""),
	""featured"" : null,
	""inputSystems"" : {
		""qaa-fonipa-x-kal"" : {
			""abbreviation"" : ""qaa-fonipa-x-kal"",
			""tag"" : ""qaa-fonipa-x-kal"",
			""languageName"" : ""Unlisted Language"",
			""isRightToLeft"" : false,
			""vernacularWS"" : false,
			""analysisWS"" : true
		},
		""qaa-x-kal"" : {
			""abbreviation"" : ""qaa-x-kal"",
			""tag"" : ""qaa-x-kal"",
			""languageName"" : ""Unlisted Language"",
			""isRightToLeft"" : false,
			""vernacularWS"" : true,
			""analysisWS"" : false
		},
		""en"" : {
			""abbreviation"" : ""en"",
			""tag"" : ""en"",
			""languageName"" : ""English"",
			""isRightToLeft"" : false,
			""vernacularWS"" : false,
			""analysisWS"" : true
		},
		""es"" : {
			""abbreviation"" : ""es"",
			""tag"" : ""es"",
			""languageName"" : ""Spanish"",
			""isRightToLeft"" : false,
			""vernacularWS"" : false,
			""analysisWS"" : true
		},
		""fr"" : {
			""abbreviation"" : ""fr"",
			""tag"" : ""fr"",
			""languageName"" : ""French"",
			""isRightToLeft"" : false,
			""vernacularWS"" : false,
			""analysisWS"" : true
		}
	},
	""interfaceLanguageCode"" : ""en"",
	""isArchived"" : false,
	""language"" : null,
	""languageCode"" : ""th"",
	""liftFilePath"" : ""/var/www/virtual/default_local/web-languageforge/src/assets/lexicon/sf_testlangproj/TestProj.lift"",
	""ownerRef"" : ObjectId(""561b666c0f87096a35c3cf2d""),
	""projectCode"" : ""testlangproj"",
	""projectName"" : ""testlangproj"",
	""siteName"" : ""languageforge.local"",
	""userJoinRequests"" : { },
	""userProperties"" : {
		""userProfilePickLists"" : {
			""city"" : {
				""name"" : ""Location"",
				""items"" : [ ],
				""defaultKey"" : null
			},
			""preferredBibleVersion"" : {
				""name"" : ""Preferred Bible Version"",
				""items"" : [ ],
				""defaultKey"" : null
			},
			""religiousAffiliation"" : {
				""name"" : ""Religious Affiliation"",
				""items"" : [ ],
				""defaultKey"" : null
			},
			""studyGroup"" : {
				""name"" : ""Study Group"",
				""items"" : [ ],
				""defaultKey"" : null
			},
			""feedbackGroup"" : {
				""name"" : ""Feedback Group"",
				""items"" : [ ],
				""defaultKey"" : null
			}
		},
		""userProfilePropertiesEnabled"" : [ ]
	},
	""users"" : {
		""561b666c0f87096a35c3cf2d"" : {
			""role"" : ""project_manager""
		}
	},
	""usersRequestingAccess"" : null
}";
		#endregion

		#region JSON Option List Data
		public static string jsonOptionListData = @"{
	""_id"" : ObjectId(""56a9e2450f8709b702f15f97""),
	""canDelete"" : false,
	""code"" : ""grammatical-info"",
	""dateCreated"" : ISODate(""2016-01-28T09:41:25Z""),
	""dateModified"" : ISODate(""2016-01-28T09:41:43Z""),
	""defaultItemKey"" : null,
	""items"" : [
		{
			""key"" : ""adj"",
			""value"" : ""Adjective (adj)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""prep"",
			""value"" : ""Preposition (prep)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""adv"",
			""value"" : ""Adverb (adv)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""cla"",
			""value"" : ""Classifier (cla)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""num"",
			""value"" : ""Numeral (num)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""n"",
			""value"" : ""Noun (n)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""nprop"",
			""value"" : ""Proper Noun (nprop)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""p"",
			""value"" : ""Particle (p)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""pro"",
			""value"" : ""Pronoun (pro)"",
			""abbreviation"" : null
		},
		{
			""key"" : ""v"",
			""value"" : ""Verb (v)"",
			""abbreviation"" : null
		}
	],
	""name"" : ""Part of Speech""
}";
		#endregion
	}
}

