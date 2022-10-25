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
        private const string GLOBAL_PACKAGES_PATH = "Directory.Packages.props";

        public Task RunCliAsync(CliOptions options)
        {
            Dictionary<string, HashSet<string>> globalPackagesToVersions = new Dictionary<string, HashSet<string>>();
            var projects = SolutionFile.Parse(options.SolutionPath).ProjectsInOrder.Where(p => p.AbsolutePath.EndsWith(".csproj")).ToList();
            var projectElements = new List<(XElement ProjElem, string ProjPath)>();
            foreach (var project in projects)
            {
                projectElements.Add(ParseProjectPackages(globalPackagesToVersions, project));
            }
            var needConsolidation = globalPackagesToVersions.Where(pack => pack.Value.Count > 1).ToList();

            if (needConsolidation.Count > 0)
            {
                var packages = needConsolidation.Select(nc => nc.Key).Aggregate((s1, s2) => s1 + "," + s2);
                throw new ArgumentException($"Error: Need consolidation for packages:{packages}");
            }

            //Save Projects
            projectElements.ForEach(pe => pe.ProjElem.Save(pe.ProjPath));

            GenerateGlobalPackages(options, globalPackagesToVersions);

            return Task.CompletedTask;
        }

        private static void GenerateGlobalPackages(CliOptions options, Dictionary<string, HashSet<string>> globalPackagesToVersions)
        {
            var solutionDir = Path.GetDirectoryName(options.SolutionPath);
            var gloabalPackagePath = Path.Combine(solutionDir, GLOBAL_PACKAGES_PATH);

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

            gloabalPackageElements.Save(gloabalPackagePath);
        }

        private static (XElement ProjElem, string ProjPath) ParseProjectPackages(Dictionary<string, HashSet<string>> globalPackagesToVersions, ProjectInSolution project)
        {
            var prj = XElement.Load(project.AbsolutePath);

            var projName = Path.GetFileNameWithoutExtension(project.AbsolutePath);

            var packageReferenceElements = prj.Elements("ItemGroup").SelectMany(e => e.Elements("PackageReference")).ToList();

            var packageReferences = packageReferenceElements
                .Where(elem => elem.Attribute("Include") != null)
                .Select(e => (Id: e.Attribute("Include").Value, Version: e.Attribute("Version") != null ? e.Attribute("Version").Value : e.Attribute("version").Value)).ToList();

            foreach (var packageReferenceElement in packageReferenceElements)
            {
                if (packageReferenceElement.Attribute("Version") != null)
                {
                    packageReferenceElement.Attribute("Version").Remove();
                }
                else
                {
                    packageReferenceElement.Attribute("version").Remove();
                }
            }

            foreach (var packageReference in packageReferences)
            {
                if (packageReference.Version == null)
                {
                    Console.WriteLine(packageReference.Id);
                }
                if (!globalPackagesToVersions.ContainsKey(packageReference.Id))
                {
                    globalPackagesToVersions[packageReference.Id] = new HashSet<string>();
                }
                globalPackagesToVersions[packageReference.Id].Add(packageReference.Version);
            }
            return (prj, project.AbsolutePath);
        }
    }

    public class CliOptions
    {
        [Option('s', "Root path", Required = true, HelpText = "Solution full path")]
        public string SolutionPath { get; set; }
    }
}
