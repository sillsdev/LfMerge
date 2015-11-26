// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Tests
{
	public class SampleData
	{
		public SampleData()
		{
		}

		public string jsonTestData =
@"{
    ""_id"": ObjectId(""56332f680f8709ed0fd92d6c""),
    ""authorInfo"": {
        ""createdByUserRef"": null,
        ""createdDate"":  ISODate(""2004-10-19T02: 42: 02Z""),
        ""modifiedByUserRef"": ObjectId(""561b666c0f87096a35c3cf2d""),
        ""modifiedDate"":  ISODate(""2015-11-18T03: 56: 45Z"")
    },
    ""citationForm"": {
        ""qaa-x-kal"": {
            ""value"": ""ztestmain""
        }
    },
    ""customFields"": {
        ""customField_entry_Cust_MultiPara"": {
            ""en"": {
                ""value"": ""<p>This is the first paragraph.</p><p>This is the second paragraph.</p>""
            }
        },
        ""customField_entry_Cust_Single_Line"": {
            ""en"": {
                ""value"": ""Some single custom text""
            }
        },
        ""customField_entry_Cust_Single_Line_All"": {
            ""en"": {
                ""value"": ""Some custom text""
            },
            ""fr"": {
                ""value"": ""French custom text""
            }
        },
        ""customField_entry_Cust_Single_ListRef"": {
            ""value"": ""comparative linguistics""
        }
    },
    ""cvPattern"": {
        ""qaa-x-kal"": {
            ""value"": ""CCVCCCVVC""
        }
    },
    ""dateCreated"":  ISODate(""2015-10-30T08: 50: 48Z""),
    ""dateModified"":  ISODate(""2015-11-18T03: 56: 45Z""),
    ""entryBibliography"": { },
    ""entryRestrictions"": { },
    ""environments"": {
        ""values"": [ ]
    },
    ""etymology"": {
        ""qaa-x-kal"": {
            ""value"": ""etymology""
        }
    },
    ""etymologyComment"": {
        ""en"": {
            ""value"": ""English comment""
        },
        ""fr"": {
            ""value"": ""French comment""
        }
    },
    ""etymologyGloss"": {
        ""en"": {
            ""value"": ""English gloss""
        }
    },
    ""etymologySource"": { },
    ""guid"": ""1a705846-a814-4289-8594-4b874faca6cc"",
    ""isDeleted"": false,
    ""lexeme"": {
        ""qaa-fonipa-x-kal"": {
            ""value"": ""zitʰɛstmen""
        },
        ""qaa-x-kal"": {
            ""value"": ""underlying form""
        }
    },
    ""literalMeaning"": {
        ""en"": {
            ""value"": ""The literal meaning""
        }
    },
    ""location"": {
        ""value"": ""witch doctor's house""
    },
    ""mercurialSha"": null,
    ""morphologyType"": ""root"",
    ""note"": {
        ""en"": {
            ""value"": """"
        },
        ""fr"": {
            ""value"": """"
        }
    },
    ""pronunciation"": {
        ""qaa-x-kal"": {
            ""value"": ""zee-test-mayn""
        }
    },
    ""senses"": [
        {
            ""liftId"": """",
            ""id"": ""56332f6896739"",
            ""partOfSpeech"": {
                ""value"": ""noun""
            },
            ""semanticDomain"": {
                ""values"": [
                    ""1 Universe, creation"",
                    ""7 Physical actions""
                ]
            },
            ""examples"": [
                {
                    ""liftId"": """",
                    ""id"": ""56332f6896f6d"",
                    ""sentence"": {
                        ""qaa-x-kal"": {
                            ""value"": ""Vernacular example""
                        }
                    },
                    ""translation"": {
                        ""en"": {
                            ""value"": ""English translation""
                        },
                        ""fr"": {
                            ""value"": ""French translation""
                        }
                    },
                    ""customFields"": {
                        ""customField_examples_Cust_Example"": {
                            ""qaa-x-kal"": {
                                ""value"": ""Custom example""
                            },
                            ""qaa-fonipa-x-kal"": {
                                ""value"": ""Custom IPA example""
                            }
                        }
                    }
                },
                {
                    ""liftId"": """",
                    ""id"": ""56332f68970d7"",
                    ""sentence"": {
                        ""qaa-x-kal"": {
                            ""value"": ""Second vernacular example""
                        }
                    },
                    ""translation"": {
                        ""en"": {
                            ""value"": ""Second English translation""
                        },
                        ""fr"": {
                            ""value"": ""Second French translation""
                        }
                    }
                }
            ],
            ""customFields"": {
                ""customField_senses_Cust_Multi_ListRef"": {
                    ""values"": [
                        ""First Custom Item"",
                        ""Second Custom Item""
                    ]
                }
            },
            ""definition"": {
                ""en"": {
                    ""value"": ""This is the English definition""
                },
                ""fr"": {
                    ""value"": ""This is the French definition""
                }
            },
            ""gloss"": {
                ""en"": {
                    ""value"": ""English gloss""
                },
                ""fr"": {
                    ""value"": ""French gloss""
                },
                ""es"": {
                    ""value"": """"
                },
                ""qaa-x-kal"": {
                    ""value"": """"
                }
            },
            ""pictures"": [ ],
            ""scientificName"": {
                ""en"": {
                    ""value"": ""Scientific name""
                }
            },
            ""anthropologyNote"": { },
            ""senseBibliography"": { },
            ""discourseNote"": { },
            ""encyclopedicNote"": { },
            ""generalNote"": { },
            ""grammarNote"": { },
            ""phonologyNote"": { },
            ""senseRestrictions"": { },
            ""semanticsNote"": { },
            ""sociolinguisticsNote"": { },
            ""source"": { },
            ""senseImportResidue"": { },
            ""usages"": {
                ""values"": [
                    ""honorific"",
                    ""old-fashioned""
                ]
            },
            ""reversalEntries"": {
                ""values"": [ ]
            },
            ""senseType"": {
                ""value"": ""figurative""
            },
            ""academicDomains"": {
                ""values"": [
                    ""education"",
                    ""graphology""
                ]
            },
            ""sensePublishIn"": {
                ""values"": [ ]
            },
            ""anthropologyCategories"": {
                ""values"": [
                    ""130"",
                    ""111""
                ]
            },
            ""status"": {
                ""values"": [
                    ""Confirmed""
                ]
            }
        },
        {
            ""liftId"": """",
            ""id"": ""56332f68979d4"",
            ""partOfSpeech"": {
                ""value"": null
            },
            ""semanticDomain"": {
                ""values"": [ ]
            },
            ""examples"": [ ],
            ""definition"": {
                ""en"": {
                    ""value"": ""This is the English definition for 2.""
                },
                ""fr"": {
                    ""value"": ""This is the French definition for 2.""
                }
            },
            ""gloss"": {
                ""en"": {
                    ""value"": ""English gloss2""
                },
                ""fr"": {
                    ""value"": ""French gloss2""
                },
                ""es"": {
                    ""value"": """"
                },
                ""qaa-x-kal"": {
                    ""value"": """"
                }
            },
            ""pictures"": [ ],
            ""scientificName"": {
                ""en"": {
                    ""value"": """"
                }
            },
            ""anthropologyNote"": { },
            ""senseBibliography"": { },
            ""discourseNote"": { },
            ""encyclopedicNote"": { },
            ""generalNote"": { },
            ""grammarNote"": { },
            ""phonologyNote"": { },
            ""senseRestrictions"": { },
            ""semanticsNote"": { },
            ""sociolinguisticsNote"": { },
            ""source"": { },
            ""senseImportResidue"": { },
            ""usages"": {
                ""values"": [ ]
            },
            ""reversalEntries"": {
                ""values"": [ ]
            },
            ""senseType"": {
                ""value"": null
            },
            ""academicDomains"": {
                ""values"": [ ]
            },
            ""sensePublishIn"": {
                ""values"": [ ]
            },
            ""anthropologyCategories"": {
                ""values"": [ ]
            },
            ""status"": {
                ""values"": [ ]
            }
        }
    ],
    ""summaryDefinition"": {
        ""en"": {
            ""value"": ""English summary definition""
        },
        ""fr"": {
            ""value"": ""French summary definition""
        }
    },
    ""tone"": {
        ""qaa-x-kal"": {
            ""value"": ""A tone goes here""
        }
    }
}";
	}
}

