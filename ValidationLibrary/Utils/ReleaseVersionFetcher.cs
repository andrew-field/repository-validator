using System.Threading.Tasks;
using Octokit;

namespace ValidationLibrary.Utils
{
    public class ReleaseVersionFetcher
    {
        private readonly IGitHubClient _client;
        private readonly string _owner;
        private readonly string _name;

        public ReleaseVersionFetcher(IGitHubClient client, string owner, string name)
        {
            _client = client;
            _owner = owner;
            _name = name;
        }

        public async Task<Octokit.Release> GetLatest()
        {
            // Per documentation, this should not return prerelease or draft-releases.
            var result = await _client.Repository.Release.GetLatest(_owner, _name).ConfigureAwait(false);
            return result;
        }
    }
}