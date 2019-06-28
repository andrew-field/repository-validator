using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Helpers;
using ValidationLibrary.Utils;

namespace ValidationLibrary.Rules
{
    /// <summary>
    /// Rule validates that Jenkinsfile has newest jenkins-ptcs-library is used if jenkins-ptcs-library is used at all.
    /// jenkins-ptcs-library is an internal company library that offers utilities for CI pipelines.
    /// </summary>
    public class HasNewestPtcsJenkinsLibRule : IValidationRule
    {
        private const string JenkinsFileName = "JENKINSFILE";

        private const string LibraryName = "jenkins-ptcs-library";

        public string RuleName => $"Old {LibraryName}";

        private readonly Regex _regex = new Regex($@"'{LibraryName}@(\d+.\d+.\d+.*)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ILogger _logger;
        private string _expectedVersion;
        
        public HasNewestPtcsJenkinsLibRule(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Init(IGitHubClient ghClient)
        {
            var versionFetcher = new ReleaseVersionFetcher(ghClient, "protacon", LibraryName);
            _expectedVersion = await versionFetcher.GetLatest();
            _logger.LogInformation("Rule {ruleClass} / {ruleName}, Newest version: {expectedVersion}", nameof(HasNewestPtcsJenkinsLibRule), RuleName, _expectedVersion);
        }

        public async Task Fix(IGitHubClient client, Repository repository)
        {
            _logger.LogInformation("Rule {ruleClass} / {ruleName}, performing auto fix.", nameof(HasNewestPtcsJenkinsLibRule), RuleName);

            var jenkinsContent = await GetJenkinsFileContent(client, repository);
            if (jenkinsContent == null)
            {
                _logger.LogWarning("Rule {ruleClass} / {ruleName}, no jenkins file found, unable to fix.");
                return;
            }
            var fixedContent = _regex.Replace(jenkinsContent.Content, $"'{LibraryName}@{_expectedVersion}'");

            var branchReference = await client.Git.Reference.CreateBranch(repository.Owner.Login, repository.Name, "autofix/test");
            var master = await client.Git.Reference.Get(repository.Owner.Login, repository.Name, "heads/master");

            var latest = await client.Git.Commit.Get(repository.Owner.Login, repository.Name, branchReference.Object.Sha);
            _logger.LogTrace("Latest commit with message {a}", latest.Message);

            var oldTree = await client.Git.Tree.Get(repository.Owner.Login, repository.Name, latest.Sha);
            var newTree = new NewTree();
            newTree.BaseTree = oldTree.Sha;

            var blob = new NewBlob()
            {
                Content = fixedContent,
                Encoding = EncodingType.Utf8
            };
            var blobReference = await client.Git.Blob.Create(repository.Owner.Login, repository.Name, blob);
            _logger.LogTrace("Created blob SHA {sha}", blobReference.Sha);

            var treeItem = new NewTreeItem();
            treeItem.Path = "Jenkinsfile";
            treeItem.Mode = "100644";
            treeItem.Type = TreeType.Blob;
            treeItem.Sha = blobReference.Sha;
            newTree.Tree.Add(treeItem);

            var createdTree = await client.Git.Tree.Create(repository.Owner.Login, repository.Name, newTree);
            var commit = new NewCommit("Testcommit", createdTree.Sha, new []{latest.Sha});
            var commitResponse = await client.Git.Commit.Create(repository.Owner.Login, repository.Name, commit);

            var refUpdate = new ReferenceUpdate(commitResponse.Sha);
            await client.Git.Reference.Update(repository.Owner.Login, repository.Name, "heads/autofix/test", refUpdate);

            var pullRequest = new NewPullRequest("title", branchReference.Ref, master.Ref);
            await client.PullRequest.Create(repository.Owner.Login, repository.Name, pullRequest);
        }

        private async Task<RepositoryContent> GetJenkinsFileContent(IGitHubClient client, Repository repository)
        {
            _logger.LogTrace("Retrieving JenkinsFile for {repositoryName}", repository.FullName);

            // NOTE: rootContents doesn't contain actual contents, content is only fetched when we fetch the single file later.
            var rootContents = await GetContents(client, repository);

            var jenkinsFile = rootContents.FirstOrDefault(content => content.Name.Equals(JenkinsFileName, StringComparison.InvariantCultureIgnoreCase));
            if (jenkinsFile == null)
            {
                _logger.LogDebug("Rule {ruleClass} / {ruleName}, No {jenkinsFileName} found in root.", nameof(HasNewestPtcsJenkinsLibRule), RuleName, JenkinsFileName);
                return null;
            }

            var matchingJenkinsFiles = await client.Repository.Content.GetAllContents(repository.Owner.Login, repository.Name, jenkinsFile.Name);
            return matchingJenkinsFiles.FirstOrDefault();
        }

        public async Task<ValidationResult> IsValid(IGitHubClient client, Repository repository)
        {
            _logger.LogTrace("Rule {ruleClass} / {ruleName}, Validating repository {repositoryName}", nameof(HasNewestPtcsJenkinsLibRule), RuleName, repository.FullName);

            var jenkinsContent = await GetJenkinsFileContent(client, repository);
            if (jenkinsContent == null)
            {
                // This is unlikely to happen.
                _logger.LogDebug("Rule {ruleClass} / {ruleName}, no {jenkinsFileName} found. Skipping.", nameof(HasNewestPtcsJenkinsLibRule), RuleName, JenkinsFileName);
                return OkResult();
            }

            MatchCollection matches = _regex.Matches(jenkinsContent.Content);
            var match = matches.OfType<Match>().FirstOrDefault();
            if (match == null)
            {
                return OkResult();
            }

            var group = match.Groups.OfType<Group>().LastOrDefault();
            if (group == null)
            {
                return OkResult();
            }

            return new ValidationResult
            {
                RuleName = RuleName,
                HowToFix = $"Update {LibraryName} to newest version.",
                IsValid = group.Value == _expectedVersion,
                Fix = Fix
            };
        }

        private async Task<IReadOnlyList<RepositoryContent>> GetContents(IGitHubClient client, Repository repository)
        {
            try {
                return await client.Repository.Content.GetAllContents(repository.Owner.Login, repository.Name);
            } 
            catch (Octokit.NotFoundException exception)
            {
                /* 
                 * NOTE: Repository that was just created (empty repository) doesn't have content this causes
                 * Octokit.NotFoundException. This same thing would probably be throw if the whole repository
                 * was missing, but we don't care for that case (no point to validate if repository doesn't exist.)
                 */
                _logger.LogWarning(exception, "Rule {ruleClass} / {ruleName}, Repository {repositoryName} caused {exceptionClass}. This may be a new repository, but if this persists, repository should be removed.",
                 nameof(HasNewestPtcsJenkinsLibRule), RuleName, repository.Name, nameof(Octokit.NotFoundException));
                return new RepositoryContent[0];
            }
        }

        private ValidationResult OkResult()
        {
            return new ValidationResult
            {
                RuleName = RuleName,
                HowToFix = $"Update {LibraryName} to newest version.",
                IsValid = true,
                Fix = Fix
            };
        }
    }
}