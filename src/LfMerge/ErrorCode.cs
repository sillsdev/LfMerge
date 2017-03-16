// Copyright (c) 2011-2017 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

namespace LfMerge
{
	public enum ErrorCode
	{
		NoError = 0,
		GeneralError = 1,  // Use this if there's no need for a specific error code
		InvalidOptions = 2,
		// TODO: Any other specific error codes we might want? FailedClone, LanguageDepotAuthenticationFailed, ...?
	}
}