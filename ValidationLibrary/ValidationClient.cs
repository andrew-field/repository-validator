using System.Threading.Tasks;
using Octokit;

namespace ValidationLibrary
{
    /// <summary>
    /// Wrapper for GitHub-communication
    /// </summary>
    public class ValidationClient : IValidationClient
    {
        private readonly IGitHubClient _client;
        private readonly IRepositoryValidator _validator;

        public ValidationClient(IGitHubClient client, IRepositoryValidator validator)
        {
            _client = client ?? throw new System.ArgumentNullException(nameof(client));
            _validator = validator ?? throw new System.ArgumentNullException(nameof(validator));
        }

        public async Task Init()
        {
            await _validator.Init().ConfigureAwait(false);
        }

        public async Task<ValidationReport> ValidateRepository(string organization, string repositoryName, bool overrideRuleIgnore)
        {
            var repository = await _client.Repository.Get(organization, repositoryName).ConfigureAwait(false);
            var result = await _validator.Validate(repository, overrideRuleIgnore).ConfigureAwait(false);
            return result;
        }
    }
}