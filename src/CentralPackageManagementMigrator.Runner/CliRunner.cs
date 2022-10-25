using CommandLine.Text;
using CommandLine;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using System.Xml.Linq;

namespace CentralPackageManagementMigrator.Runner
{
    public class CliRunner
    {
        public Task RunCliAsync(CliOptions options)
        {
            Dictionary<string, HashSet<string>> globalPackagesToVersions = new Dictionary<string, HashSet<string>>();
            var projects = SolutionFile.Parse(options.SolutionPath).ProjectsInOrder.Where(p => p.AbsolutePath.EndsWith(".csproj")).ToList();

            foreach (var project in projects)
            {
                var prj = XElement.Load(project.AbsolutePath);

                var packageReferenceElements = prj.Elements("ItemGroup").SelectMany(e => e.Elements("PackageReference")).ToList();

                var packageReferences = packageReferenceElements
                    .Where(elem => elem.Attribute("Include") != null)
                    .Select(e => (Id: e.Attribute("Include").Value, Version: e.Attribute("Version")?.Value)).ToList();
                

                foreach (var packageReference in packageReferences)
                {
                    if (!globalPackagesToVersions.ContainsKey(packageReference.Id))
                    {
                        globalPackagesToVersions[packageReference.Id] = new HashSet<string>();
                    }
                    globalPackagesToVersions[packageReference.Id].Add(packageReference.Version);
                }
            }

            var needConsolidation = globalPackagesToVersions.Where(pack => pack.Value.Count > 0).ToList();

            if(needConsolidation.Count > 0)
            {
                throw new ArgumentException("need consolidation");
            }
            return Task.CompletedTask;
        }
    }

    public class CliOptions
    {
        [Option('s', "Root path", Required = true, HelpText = "Solution full path")]
        public string SolutionPath { get; set; }
    }
}
