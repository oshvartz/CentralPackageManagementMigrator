using CommandLine;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace CentralPackageManagementMigrator.Runner
{
    public class CliRunner
    {
        private const string GLOBAL_PACKAGES_FILE_NAME = "Directory.Packages.props";
        private const string NUGET_CONFIG_FILE_NAME = "NuGet.Config";
        private readonly ILogger<CliRunner> _logger;

        public CliRunner(ILogger<CliRunner> logger)
        {
            _logger = logger;
        }

        public Task RunCliAsync(CliOptions options)
        {
            try
            {
                Dictionary<string, HashSet<string>> globalPackagesToVersions = new Dictionary<string, HashSet<string>>();

                var solutionDir = Path.GetDirectoryName(options.SolutionPath);
                var patterns = options.ProjectPatterns ?? new string[] { };
                patterns = patterns.Union(new[] { "*.csproj" }).Distinct().ToList();

                var projFiles = patterns.SelectMany(pattern => Directory.GetFiles(solutionDir, pattern, SearchOption.AllDirectories)).ToArray();

                _logger.LogInformation($"found {projFiles.Length} projects to scan");
                var projectElements = new List<(XElement ProjElem, string ProjPath)>();
                foreach (var projectFile in projFiles)
                {
                    _logger.LogInformation($"Parsing {Path.GetFileName(projectFile)}");
                    projectElements.Add(ParseProjectPackages(globalPackagesToVersions, projectFile));
                }

                var needConsolidation = globalPackagesToVersions.Where(pack => pack.Value.Count > 1).ToList();

                if (needConsolidation.Count > 0)
                {
                    var packages = needConsolidation.Select(nc => nc.Key).Aggregate((s1, s2) => s1 + "," + s2);
                    throw new ArgumentException($"Error: Need consolidation for packages:{packages}");
                }
                _logger.LogInformation($"Saving projects");
                //Save Projects
                projectElements.ForEach(pe => pe.ProjElem.SaveWithoutXmlDeclaration(pe.ProjPath));

                _logger.LogInformation($"Generating Global Packages File");
                GenerateGlobalPackages(options, globalPackagesToVersions);

                if (options.AddSourceMappingsToNugetConfig)
                {
                    _logger.LogInformation($"Add Source Mapping to nuget.config");
                    AddSourceMappingToNugetConfig(options);
                }

                _logger.LogInformation($"Done");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Migration Failed:{ex.Message}", ex);
            }
            return Task.CompletedTask;

        }

        private void AddSourceMappingToNugetConfig(CliOptions options)
        {
            var solutionDir = Path.GetDirectoryName(options.SolutionPath);
            string[] nugetConfigFiles = Directory.GetFiles(solutionDir, NUGET_CONFIG_FILE_NAME, SearchOption.TopDirectoryOnly);
            if (nugetConfigFiles.Length != 1)
            {
                _logger.LogWarning("Didn't find one nuget.config");
                return;
            }
            var nugetConfigFile = nugetConfigFiles[0];

            var nugetConfElem = XElement.Load(nugetConfigFile);

            var sources = nugetConfElem.Elements().Where(e => e.Name.LocalName == "packageSources").Single().Elements().Where(e => e.Name == "add").ToList();

            if (sources.Count != 1)
            {
                _logger.LogWarning("Cannot add source mapping you should do it maunally");
                return;
            }

            var sourceKey = sources
            .Single().Attribute("key").Value;

            XElement packageSourceMappingElement = XElement.Parse(@$"<packageSourceMapping>
    <packageSource key=""{sourceKey}"">
      <package pattern=""*"" />
    </packageSource>
  </packageSourceMapping>");


            nugetConfElem.Add(packageSourceMappingElement);

            nugetConfElem.SaveWithoutXmlDeclaration(nugetConfigFile);
        }

        private static void GenerateGlobalPackages(CliOptions options, Dictionary<string, HashSet<string>> globalPackagesToVersions)
        {
            var solutionDir = Path.GetDirectoryName(options.SolutionPath);
            var gloabalPackagePath = Path.Combine(solutionDir, GLOBAL_PACKAGES_FILE_NAME);

            XElement gloabalPackageElements = XElement.Parse(@"<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
  </ItemGroup>
</Project>");


            var itemGroupElm = gloabalPackageElements.Elements("ItemGroup").Single();
            foreach (var package in globalPackagesToVersions)
            {
                var packageReferenceElm = new XElement("PackageVersion");
                packageReferenceElm.Add(new XAttribute("Include", package.Key));
                packageReferenceElm.Add(new XAttribute("Version", package.Value.First()));
                itemGroupElm.Add(packageReferenceElm);
            }

            gloabalPackageElements.SaveWithoutXmlDeclaration(gloabalPackagePath);
        }

        private static (XElement ProjElem, string ProjPath) ParseProjectPackages(Dictionary<string, HashSet<string>> globalPackagesToVersions, string projectAbsolutePath)
        {
            var prj = XElement.Load(projectAbsolutePath);

            var packageReferenceElements = prj.Elements().Where(e => e.Name.LocalName == "ItemGroup").SelectMany(e => e.Elements().Where(e => e.Name.LocalName == "PackageReference")).ToList();

            var packageReferences = packageReferenceElements
                .Where(elem => elem.Attribute("Include") != null)
                .Select(PackageReferenceElementSelector).ToList();

            foreach (var packageReferenceElement in packageReferenceElements)
            {
                if (packageReferenceElement.Attribute("Version") != null)
                {
                    packageReferenceElement.Attribute("Version").Remove();
                }
                else if (packageReferenceElement.Attribute("version") != null)
                {
                    packageReferenceElement.Attribute("version").Remove();
                }
                else if (packageReferenceElement.Element("Version") != null)
                {
                    packageReferenceElement.Element("Version").Remove();
                }
                else
                {
                    packageReferenceElement.Element("version").Remove();
                }
            }

            foreach (var packageReference in packageReferences)
            {
                if (!globalPackagesToVersions.ContainsKey(packageReference.Id))
                {
                    globalPackagesToVersions[packageReference.Id] = new HashSet<string>();
                }
                globalPackagesToVersions[packageReference.Id].Add(packageReference.Version);
            }
            return (prj, projectAbsolutePath);
        }

        private static (string Id, string Version) PackageReferenceElementSelector(XElement element)
        {
            var id = element.Attribute("Include").Value;

            var attribute = element.Attribute("Version")?.Value ?? element.Attribute("version")?.Value ??
                element.Element("Version")?.Value ?? element.Element("version")?.Value ??
                throw new ArgumentException($"Version of {id} is missing!");

            return (id, attribute);
        }
    }



    public class CliOptions
    {
        [Option('s', "Solution path", Required = true, HelpText = "Solution full path")]
        public string SolutionPath { get; set; }

        [Option('p', "Project patterns", Required = false, HelpText = "Project Pattern (optional) will use *.csproj in any case")]
        public IEnumerable<string> ProjectPatterns { get; set; }

        [Option('n', "Add Source Mappings to nuget.config", Required = false, Default = true, HelpText = "Add Source Mappings only for nuget.config with one source otherwise fails")]
        public bool AddSourceMappingsToNugetConfig { get; set; }
    }
}
