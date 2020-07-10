// Copyright (c) 2011-2020 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.Reflection;
using SIL.LCModel.Core.Attributes;
using SIL.TestUtilities;

[assembly: OfflineSldr]
[assembly: InitializeIcu(IcuDataPath = "/usr/share/fieldworks/Icu54")]

