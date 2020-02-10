using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ValidationLibrary.MarkdownGenerator;

namespace Runner
{
    public class DocumentationFileCreator
    {
        private const string DocumentationFolder = "Documentation";
        private const string RulesFolder = "Rules";
        private readonly ILogger<DocumentationFileCreator> _logger;

        public DocumentationFileCreator(ILogger<DocumentationFileCreator> logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public void GenerateDocumentation()
        {
            var types = GetRuleTypes();

            var homeBuilder = new MarkdownBuilder();
            homeBuilder.Header(1, "References");
            homeBuilder.AppendLine();

            MakeSureFolderStructureExists();

            foreach (var group in types.GroupBy(type => type.Namespace).OrderBy(group => group.Key))
            {
                homeBuilder.Header(2, group.Key);
                homeBuilder.AppendLine();

                foreach (var item in group.OrderBy(type => type.Name))
                {
                    var name = item.Name.ToLower();
                    var path = Path.Combine(DocumentationFolder + "\\" + RulesFolder, $"{name}.md");
                    _logger.LogTrace("Creating file to path {path}", path);

                    homeBuilder.ListLink(MarkdownBuilder.MarkdownCodeQuote(item.Name), $"\\{RulesFolder}\\{name}");
                    File.WriteAllText(path, item.ToString());
                }

                homeBuilder.AppendLine();
            }

            File.WriteAllText(Path.Combine(DocumentationFolder, "rules.md"), homeBuilder.ToString());
            _logger.LogInformation("Documentation rules generated");
        }

        private MarkdownableType[] GetRuleTypes()
        {
            string rulesNamespace = "ValidationLibrary.Rules";
            _logger.LogInformation("Generating documentation files for rules in namespace {namespace}", rulesNamespace);
            var validationLibraryAssembly = Assembly.Load(rulesNamespace);
            var types = TypeExtractor.Load(validationLibraryAssembly, rulesNamespace);
            return types;
        }

        private static void MakeSureFolderStructureExists()
        {
            if (!Directory.Exists(DocumentationFolder)) Directory.CreateDirectory(DocumentationFolder);
            if (!Directory.Exists($"{DocumentationFolder}\\{RulesFolder}")) Directory.CreateDirectory($"{DocumentationFolder}\\{RulesFolder}");
        }
    }
}