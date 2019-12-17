using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;

namespace ValidationLibrary.Rules
{
    /// <summary>
    /// This rule checks that repository does not have too many stale branches.
    /// </summary>
    public class HasNotManyStaleBranchesRule : IValidationRule
    {
        public string RuleName => "Stale branches";

        private readonly ILogger<HasNotManyStaleBranchesRule> _logger;

        public HasNotManyStaleBranchesRule(ILogger<HasNotManyStaleBranchesRule> logger)
        {
            _logger = logger;
        }

        public Task Init(IGitHubClient ghClient)
        {
            _logger.LogInformation("Rule {ruleClass} / {ruleName}, Initialized", nameof(HasNotManyStaleBranchesRule), RuleName);
            return Task.FromResult(0);
        }

        public async Task<ValidationResult> IsValid(IGitHubClient client, Repository gitHubRepository)
        {
            _logger.LogTrace("Rule {ruleClass} / {ruleName}, Validating repository {repositoryName}", nameof(HasNotManyStaleBranchesRule), RuleName, gitHubRepository.FullName);

            var branches = await client.Repository.Branch.GetAll(gitHubRepository.FullName?.Split('/')[0] ?? "", gitHubRepository.Name);

            var staleCommitsMap = new Dictionary<string, bool>();
            var now = DateTimeOffset.Now;
            var staleCount = 0;

            foreach (var branch in branches)
            {
                if (!staleCommitsMap.ContainsKey(branch.Commit.Sha))
                {
                    var commit = await client.Repository.Commit.Get(gitHubRepository.Id, branch.Commit.Sha);
                    staleCommitsMap[branch.Commit.Sha] = (now - commit.Commit.Author.Date) > TimeSpan.FromDays(90);
                }

                if (staleCommitsMap[branch.Commit.Sha]) staleCount++;
                if (staleCount >= 10) break;
            }

            _logger.LogDebug("Rule {ruleClass} / {ruleName}, Validating repository {repositoryName}. Not too many stale branches: {isValid}", nameof(HasNotManyStaleBranchesRule), RuleName, gitHubRepository.FullName, staleCount < 10);
            return new ValidationResult(RuleName, "Remove branches, that have not been updated in 90 days or more.", staleCount < 10, DoNothing);
        }

        private Task DoNothing(IGitHubClient client, Repository repository)
        {
            _logger.LogInformation("Rule {ruleClass} / {ruleName}, No fix.", nameof(HasNotManyStaleBranchesRule), RuleName);
            return Task.FromResult(0);
        }
    }
}