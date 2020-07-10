// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Reflection;
using SIL.LCModel.Core.Attributes;
using SIL.TestUtilities;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

//[assembly: AssemblyTitle("LfMerge.Core.Tests")]
//[assembly: AssemblyDescription("")]
//[assembly: AssemblyConfiguration("")]
//[assembly: AssemblyCompany("")]
//[assembly: AssemblyProduct("")]
//[assembly: AssemblyCopyright("SIL International")]
//[assembly: AssemblyTrademark("")]
//[assembly: AssemblyCulture("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

//[assembly: AssemblyVersion("1.0.*")]

[assembly: OfflineSldr]
[assembly: InitializeIcu(IcuDataPath = "/usr/share/fieldworks/Icu54")]

