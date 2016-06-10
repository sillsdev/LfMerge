﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Autofac;
using LfMerge.Settings;
using Newtonsoft.Json;

namespace LfMerge.LanguageForge.Infrastructure
{
	public static class PhpConnection
	{
		public static string RunClass(string className, string methodName, List<Object> parameters)
		{
			return RunClass(className, methodName, parameters, false);
		}

		public static string RunClass(string className, string methodName, List<Object> parameters, bool isTest)
		{
			var runClassParameters = new RunClassParameters(className, methodName, parameters);
			runClassParameters.isTest = isTest;
			string runClassParametersJson = JsonConvert.SerializeObject(runClassParameters);

			var settings = MainClass.Container.Resolve<LfMergeSettingsIni>();

			string output;
			using (var p = new Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardInput = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = "php";
				p.StartInfo.Arguments = Path.Combine(settings.PhpSourcePath,
					"Api/Library/Shared/CLI/RunClass.php");
				p.Start();
				p.StandardInput.Write(runClassParametersJson);
				p.StandardInput.Close();

				output = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				if (p.ExitCode != 0)
				{
					throw new Exception("RunClass non-zero exit code!\n" + output);
				}
				p.Close();
			}

			return output;
		}

	}
}
