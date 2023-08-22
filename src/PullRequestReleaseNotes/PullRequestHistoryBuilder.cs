﻿using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PullRequestReleaseNotes.Models;
using PullRequestReleaseNotes.Providers;

namespace PullRequestReleaseNotes
{
    public class PullRequestHistoryBuilder
    {
        private readonly ProgramArgs _programArgs;
        private readonly IPullRequestProvider _pullRequestProvider;

        private static readonly Regex ParseSemVer = new Regex(
            @"^[vV]?(?<SemVer>(?<Major>\d+)(\.(?<Minor>\d+))(\.(?<Patch>\d+))?)(\.(?<FourthPart>\d+))?(-(?<Tag>[^\+]*))?(\+(?<BuildMetaData>.*))?$",
            RegexOptions.Compiled);

        public PullRequestHistoryBuilder(ProgramArgs programArgs)
        {
            _programArgs = programArgs;
            _pullRequestProvider = programArgs.PullRequestProvider;
        }

        public List<PullRequestDto> BuildHistory()
        {
            var unreleasedCommitsProvider = new UnreleasedCommitsProvider();
            var unreleasedCommits = unreleasedCommitsProvider.GetAllUnreleasedMergeCommits(
                _programArgs.LocalGitRepository, _programArgs.ReleaseBranchRef, _programArgs.GitTagsAnnotated);
            return unreleasedCommits.Select(mergeCommit => _pullRequestProvider.Get(mergeCommit.Message))
                .Where(pullRequestDto => pullRequestDto != null).ToList();
        }
    }
}