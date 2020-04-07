using System;
using System.Linq;
using System.Net;
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
        private readonly IGitHubClient _gitHubClient;
        private readonly IValidationClient _validationClient;
        private readonly IGitHubReporter _gitHubReporter;

        public RepositoryValidatorEndpoint(IGitHubClient gitHubClient, IValidationClient validationClient, IGitHubReporter gitHubReporter)
        {
            _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
            _validationClient = validationClient ?? throw new ArgumentNullException(nameof(validationClient));
            _gitHubReporter = gitHubReporter ?? throw new ArgumentNullException(nameof(gitHubReporter));
        }

        [FunctionName(nameof(RepositoryValidatorTrigger))]
        public static async Task<HttpResponseMessage> RepositoryValidatorTrigger(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/github-endpoint")] HttpRequestMessage req, [DurableClient] IDurableOrchestrationClient starter,
            ILogger logger)
        {
            logger.LogDebug("Repository validation hook launched.");
            if (starter is null) throw new ArgumentNullException(nameof(starter), "Durable orchestration client was null. Error using durable functions.");
            if (req == null || req.Content == null) throw new ArgumentNullException(nameof(req), "Request content was null. Unable to retrieve parameters.");

            try
            {
                var stringContent = await req.Content.ReadAsStringAsync().ConfigureAwait(false);
                var content = JsonConvert.DeserializeObject<PushData>(stringContent);
                ValidateInput(content);
                logger.LogDebug("Request json valid.");
                var instanceId = CreateInstanceId(content);

                var existingInstance = await starter.GetStatusAsync(instanceId).ConfigureAwait(false);
                if (existingInstance == null)
                {
                    await starter.StartNewAsync(nameof(RunOrchestrator), instanceId, content).ConfigureAwait(false);

                    logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
                }
                else if (IsFinished(existingInstance.RuntimeStatus))
                {
                    await starter.StartNewAsync(nameof(RunOrchestrator), instanceId, content).ConfigureAwait(false);
                    logger.LogInformation("Orchestration with ID = '{instanceId}' already running, not creating a new one.", instanceId);
                }
                else
                {
                    logger.LogInformation("Orchestration with ID = '{instanceId}' status was {status}, starting a new one.", instanceId, existingInstance.RuntimeStatus);
                }
                return starter.CreateCheckStatusResponse(req, instanceId);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is JsonSerializationException)
            {
                Console.WriteLine("Vittu {0}", exception.Message);
                logger.LogError(exception, "Invalid request received, can't perform validation.");
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        }

        [FunctionName(nameof(RunOrchestrator))]
        public static async Task<StatusCodeResult> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context), "Durable orchestration context was null. Error running the orchestrator.");

            var content = context.GetInput<PushData>();
            return await context.CallActivityAsync<StatusCodeResult>(nameof(RunActivity), content).ConfigureAwait(true);
        }

        [FunctionName(nameof(RunActivity))]
        public async Task<StatusCodeResult> RunActivity([ActivityTrigger] PushData content, ILogger logger)
        {
            try
            {
                if (content is null) throw new ArgumentNullException(nameof(content), "No content to execute the activity.");

                logger.LogDebug("Executing validation activity.");
                ValidateInput(content);

                logger.LogInformation("Doing validation. Repository {owner}/{repositoryName}", content.Repository?.Owner?.Login, content.Repository?.Name);
                await _validationClient.Init().ConfigureAwait(false);
                var report = await _validationClient.ValidateRepository(content.Repository.Owner.Login, content.Repository.Name, false).ConfigureAwait(false);

                logger.LogDebug("Sending report.");
                await _gitHubReporter.Report(new[] { report }).ConfigureAwait(false);

                logger.LogDebug("Performing auto fixes.");
                await PerformAutofixes(report).ConfigureAwait(false);

                logger.LogInformation("Validation finished");
                return new OkResult();
            }
            catch (ArgumentException exception)
            {
                logger.LogError(exception, "Invalid request received");
                return new BadRequestResult();
            }
        }

        private static bool IsFinished(OrchestrationRuntimeStatus status)
        {
            return status == OrchestrationRuntimeStatus.Canceled
                || status == OrchestrationRuntimeStatus.Failed
                || status == OrchestrationRuntimeStatus.Terminated
                || status == OrchestrationRuntimeStatus.Completed;
        }

        private static string CreateInstanceId(PushData content)
        {
            return $"{content.Repository?.Owner?.Login}_{content.Repository?.Name}";
        }

        private static void ValidateInput(PushData content)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content), "Content was null. Unable to retrieve parameters.");
            }

            if (string.IsNullOrEmpty(content.Repository.Name))
            {
                throw new ArgumentException("No repository name defined. Unable to validate repository.");
            }

            if (string.IsNullOrEmpty(content.Repository.Owner.Login))
            {
                throw new ArgumentException("No repository owner login defined. Unable to validate repository.");
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
