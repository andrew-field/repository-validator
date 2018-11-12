using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ValidationLibrary.Slack
{
    /// <summary>
    /// Wrapper for Slack API
    /// </summary>
    public class SlackClient
    {
        private readonly Uri _webhookUrl;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly SlackConfiguration _config;

        public SlackClient(SlackConfiguration config)
        {
            _webhookUrl = new Uri(_config.WebHookUrl);
            _config = config;
        }
    
        public async Task<HttpResponseMessage> SendMessageAsync(ValidationReport[] report,
            string channel = null, string username = null)
        {
            var problemRepositories = report.Where(repo => repo.Results.Any(result => !result.IsValid)).Select(Format).Take(_config.ReportLimit);

            var payload = new
            {
                attachments = problemRepositories,
                channel,
                username
            };
            var serializedPayload = JsonConvert.SerializeObject(payload);
            var response = await _httpClient.PostAsync(_webhookUrl,
                new StringContent(serializedPayload, Encoding.UTF8, "application/json"));
    
            return response;
        }

        private dynamic Format(ValidationReport report)
        {
            var errors = report.Results.Where(r => !r.IsValid).Select(r => $"{r.RuleName}: Failed");
            var message = string.Join("\n", errors);

            return new {
                title = report.RepositoryName,
                title_link = report.RepositoryUrl,
                text = message,
                mrkdwn_in = new []{"text"}
            };
        }
    }
}