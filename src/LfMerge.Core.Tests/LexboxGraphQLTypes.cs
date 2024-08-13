using System;

namespace LfMerge.Core.Tests.LexboxGraphQLTypes
{
	public enum ProjectType
	{
		Unknown = 0,
		FLEx = 1,
		// WeSay = 2,
		// OneStoryEditor = 3,
		// OurWord = 4,
		// AdaptIt = 5,
	}
	public enum RetentionPolicy
	{
		Unknown = 0,
		Verified = 1,
		Test = 2,
		Dev = 3,
		Training = 4,
	}

	public record CreateProjectInput(
		Guid? Id,
		string Name,
		string Description,
		string Code,
		ProjectType Type,
		RetentionPolicy RetentionPolicy,
		bool IsConfidential,
		Guid? ProjectManagerId,
		Guid? OrgId
	);

	public enum CreateProjectResult
	{
		Created,
		Requested
	}

	public record CreateProjectResponse(Guid? Id, CreateProjectResult Result);
	public record CreateProject(CreateProjectResponse CreateProjectResponse);
	public record CreateProjectGqlResponse(CreateProject CreateProject);
}
