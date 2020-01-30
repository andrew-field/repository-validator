using Octokit;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ValidationLibrary.Rules
{
    /// <summary>
    /// This rule checks that repository has CODEOWNERS defined
    /// </summary>
    public class HasCodeownersRule : IValidationRule
    {
        public string RuleName => "Missing CODEOWNERS";

        private const string MainBranch = "master";
        
        private readonly ILogger<HasCodeownersRule> _logger;

        public HasCodeownersRule(ILogger<HasCodeownersRule> logger)
        {
            _logger = logger;
        }

        public Task Init(IGitHubClient gitHubClient)
        {
            _logger.LogInformation("Rule {ruleClass} / {ruleName}, Initialized", nameof(HasCodeownersRule), RuleName);
            return Task.CompletedTask;
        }

        public async Task<ValidationResult> IsValid(IGitHubClient client, Repository repo)
        {

            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (repo is null)
            {
                throw new ArgumentNullException(nameof(repo));
            }

            _logger.LogTrace("Rule {ruleClass} / {ruleName}, Validating repository {repositoryName}", nameof(HasCodeownersRule), RuleName, repo.FullName);
            var codeownersContent = await GetCodeownersContent(client, repo).ConfigureAwait(false);
            if(codeownersContent == null)
            {
                _logger.LogDebug("Rule {ruleClass} / {ruleName}, No CODEOWNERS found, validation false.", nameof(HasReadmeRule), RuleName);
                return new ValidationResult(RuleName, "Add CODEOWNERS file to .github directory.", false, DoNothing);
            }
            
            _logger.LogDebug("Rule {ruleClass} / {ruleName}, Validating repository {repositoryName}. CODEOWNERS exists: {codeownersExist}", nameof(HasCodeownersRule), RuleName, repo.FullName, !string.IsNullOrWhiteSpace(codeownersContent.Content));
            return new ValidationResult(RuleName, "Add CODEOWNERS file to .github directory & add at least one owner.", !string.IsNullOrWhiteSpace(codeownersContent.Content), DoNothing);
        }

        private Task DoNothing(IGitHubClient client, Repository repository)
        {
            return Task.CompletedTask;
        }

        private async Task<RepositoryContent> GetCodeownersContent(IGitHubClient client, Repository repository)
        {
            var contents = await GetContents(client, repository, MainBranch).ConfigureAwait(false);
            var githubDir = contents.FirstOrDefault(content => content.Name.Equals(".github", StringComparison.InvariantCultureIgnoreCase));

            if(githubDir == null)
            {
                _logger.LogDebug("Rule {ruleClass} / {ruleName}, No .github directory found.", nameof(HasCodeownersRule), RuleName);
                return null;
            }

            var file = await client.Repository.Content.GetAllContentsByRef(repository.Owner.Login, repository.Name, githubDir.Name, MainBranch).ConfigureAwait(false);
            var codeownersFile = file.FirstOrDefault(content => content.Name.Equals("CODEOWNERS", StringComparison.InvariantCultureIgnoreCase));
            var path = ".github/CODEOWNERS";

            if(codeownersFile == null) {
                _logger.LogDebug("Rule {ruleClass} / {ruleName}, No CODEOWNERS found.", nameof(HasCodeownersRule), RuleName);
                return null;
            }
            var matchingFile = await client.Repository.Content.GetAllContentsByRef(repository.Owner.Login, repository.Name, path, MainBranch).ConfigureAwait(false);
            return matchingFile[0];
        }

        private async Task<IReadOnlyList<RepositoryContent>> GetContents(IGitHubClient client, Repository repository, string branch)
        {
            try
            {
                return await client.Repository.Content.GetAllContentsByRef(repository.Owner.Login, repository.Name, branch).ConfigureAwait(false);
            }
            catch (NotFoundException exception)
            {
                _logger.LogWarning(exception, "Rule {ruleClass} / {ruleName}, Repository {repositoryName} caused {exceptionClass}. This may be a new repository, but if this persists, repository should be removed.",
                 nameof(HasCodeownersRule), RuleName, repository.Name, nameof(NotFoundException));
                return Array.Empty<RepositoryContent>();
            }
        }
    }
}