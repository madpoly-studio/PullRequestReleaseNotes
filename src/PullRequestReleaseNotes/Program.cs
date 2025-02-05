﻿using System;
using System.Collections.Generic;
using System.Net;
using PullRequestReleaseNotes.Models;
using PullRequestReleaseNotes.Publishers;

namespace PullRequestReleaseNotes
{
    public static class Program
    {
        private static ProgramArgs _programArgs;
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = -1;
        private const string ProductVersion = "2.1.0";

        private static void Main(string[] args)
        {
            // See https://stackoverflow.com/questions/32788409/c-sharp-httpwebrequest-the-underlying-connection-was-closed-an-unexpected-error/32789483
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;

            int exitCode;
            _programArgs = ValidateConfiguration(args);
            if (_programArgs.AcceptInvalidCertificates)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            var releaseHistory = new PullRequestHistoryBuilder(_programArgs).BuildHistory();
            if (releaseHistory == null)
                exitCode = FailureExitCode;
            else
                exitCode = BuildAndPublish(releaseHistory);
            Environment.Exit(exitCode);
        }

        private static int BuildAndPublish(List<PullRequestDto> releaseHistory)
        {
            var buildVersion = BuildVersion();
            var semanticReleaseNotes = new SemanticReleaseNotesBuilder(releaseHistory, buildVersion, _programArgs).Build();
            var releaseHistoryMarkdown = new MarkdownFormatter(_programArgs).Format(semanticReleaseNotes);
            var combinedMarkdown = $"# {MarkdownFormatter.EscapeMarkdown(buildVersion)}\n{releaseHistoryMarkdown}";
            Console.WriteLine(combinedMarkdown);
            if (Publish(combinedMarkdown, buildVersion, releaseHistoryMarkdown))
                return SuccessExitCode;
            Console.WriteLine("ERROR: Failed to publish release notes ...");
            return FailureExitCode;
        }

        private static bool Publish(string combinedMarkdown, string buildVersion, string releaseHistoryMarkdown)
        {
            var publishedFile = PublishFile(combinedMarkdown);
            var publishedConfluencePage = PublishConfluence(buildVersion, releaseHistoryMarkdown);
            var publishedSlackPost = PublishSlack(buildVersion, releaseHistoryMarkdown);
            if (!publishedFile && !publishedConfluencePage && !publishedSlackPost)
                return false;
            if (!publishedFile || !publishedConfluencePage || !publishedSlackPost)
            {
                Console.WriteLine("WARNING: Failed to publish release notes  ...");
                Console.WriteLine("=============================================");
                Console.WriteLine(combinedMarkdown);
            }
            return true;
        }

        private static bool PublishSlack(string buildVersion, string releaseHistoryMarkdown)
        {
            if (!_programArgs.PublishToSlack)
                return true;
            return SlackPublisher.PublishPost(buildVersion, releaseHistoryMarkdown, _programArgs);
        }

        private static bool PublishConfluence(string buildVersion, string releaseHistoryMarkdown)
        {
            if (!_programArgs.PublishToConfluence)
                return true;
            return ConfluencePublisher.PublishMarkdownPage(buildVersion, releaseHistoryMarkdown, _programArgs);
        }

        private static bool PublishFile(string combinedMarkdown)
        {
            if (!_programArgs.PublishToFile)
                return true;
            return FilePublisher.PublishMarkdownReleaseHistoryFile(combinedMarkdown, _programArgs);
        }

        private static ProgramArgs ValidateConfiguration(string[] args)
        {
            if (!Config.GetCommandLineInput(args, out var programArgs))
                Environment.Exit(FailureExitCode);

            var config = new Config(programArgs);

            if (programArgs.ShowVersion)
            {
                Console.WriteLine($"PullRequestReleaseNotes version {ProductVersion}");
                Environment.Exit(SuccessExitCode);
            }

            if (programArgs.InitConfig)
            {
                config.WriteSampleConfig();
                Environment.Exit(SuccessExitCode);
            }

            if (!config.SetupPullRequestProvider())
            {
                Console.WriteLine($"Failed to setup the pull request provider. Please check the supplied parameters.");
                Environment.Exit(FailureExitCode);
            }

            if (programArgs.HeadBranchRestrictionApplies())
            {
                Console.WriteLine($"Detected a non-head branch {programArgs.ReleaseBranchRef}. Aborting ...");
                Environment.Exit(SuccessExitCode);
            }
            return programArgs;
        }

        private static string BuildVersion()
        {
            var versionText = !string.IsNullOrWhiteSpace(_programArgs.GitVersion) ? _programArgs.GitVersion : "Unreleased";
            return $"{versionText} ({_programArgs.LocalGitRepository.Head.CanonicalName.Replace("refs/heads/", string.Empty).ToUpper()}) - {DateTime.Now:yy}.{DateTime.Now:MM}.{DateTime.Now.GetWeekOfMonth()}";
        }
    }
}

