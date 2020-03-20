using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Octokit;
using ValidationLibrary.AzureFunctions.GitHubDto;
using ValidationLibrary.GitHub;

namespace ValidationLibrary.AzureFunctions
{
    public class RepositoryValidatorEndpoint
    {
        private readonly ILogger<RepositoryValidatorEndpoint> _logger;
        private readonly IGitHubClient _gitHubClient;
        private readonly IValidationClient _validationClient;
        private readonly IGitHubReporter _gitHubReporter;

        public RepositoryValidatorEndpoint(ILogger<RepositoryValidatorEndpoint> logger, IGitHubClient gitHubClient, IValidationClient validationClient, IGitHubReporter gitHubReporter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
            _validationClient = validationClient ?? throw new ArgumentNullException(nameof(validationClient));
            _gitHubReporter = gitHubReporter ?? throw new ArgumentNullException(nameof(gitHubReporter));
        }

        [FunctionName("DurableFunctionsOrchestrationCSharp1_Hello")]
        public async Task<string> Run(PushData content/*[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestMessage req*/)
        {
            try
            {
                // _logger.LogDebug("Repository validation hook launched");
                // if (req == null || req.Content == null)
                // {
                //     throw new ArgumentNullException(nameof(req), "Request content was null. Unable to retrieve parameters.");
                // }

                // var content = await req.Content.ReadAsAsync<PushData>().ConfigureAwait(false);
                // ValidateInput(content);

                _logger.LogInformation("Doing validation. Repository {owner}/{repositoryName}", content.Repository?.Owner?.Login, content.Repository?.Name);

                await _validationClient.Init().ConfigureAwait(false);
                var report = await _validationClient.ValidateRepository(content.Repository.Owner.Login, content.Repository.Name, false).ConfigureAwait(false);

                _logger.LogDebug("Sending report.");
                await _gitHubReporter.Report(new[] { report }).ConfigureAwait(false);
                await PerformAutofixes(report).ConfigureAwait(false);
                _logger.LogInformation("Validation finished");
                return "Good";
                //return new OkResult();
            }
            catch (Exception exception)
            {
                if (exception is ArgumentException || exception is JsonException)
                {
                    _logger.LogError(exception, "Invalid request received");

                    return "bad"; //new BadRequestResult();
                }
                throw;
            }
        }

        [FunctionName(nameof(RepositoryValidator))]
        public async Task<HttpResponseMessage> RepositoryValidator([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestMessage req, [DurableClient] IDurableOrchestrationClient starter)
        {
            var content = await req.Content.ReadAsAsync<PushData>().ConfigureAwait(false);
            ValidateInput(content);
            string instanceId = await starter.StartNewAsync("DurableFunctionsOrchestrationCSharp1", content);
            _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("DurableFunctionsOrchestrationCSharp1")]
        public static async Task<List<string>> RunOrchestrator(PushData content,
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("DurableFunctionsOrchestrationCSharp1_Hello", content));
            return outputs;
        }


        private static void ValidateInput(PushData content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content), "Content was null. Unable to retrieve parameters.");
            }

            if (content.Repository == null)
            {
                throw new ArgumentException("No repository defined in content. Unable to validate repository");
            }

            if (content.Repository.Owner == null)
            {
                throw new ArgumentException("No repository owner defined. Unable to validate repository");
            }

            if (content.Repository.Name == null)
            {
                throw new ArgumentException("No repository name defined. Unable to validate repository");
            }
        }
        private async Task PerformAutofixes(params ValidationReport[] results)
        {
            foreach (var repositoryResult in results)
            {
                foreach (var ruleResult in repositoryResult.Results.Where(r => !r.IsValid))
                {
                    await ruleResult.Fix(_gitHubClient, repositoryResult.Repository).ConfigureAwait(false);
                }
            }
        }
    }
}
