Tool to migrate solution to work with Central Package Management.
learn about it [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).

It's scanning all project files by pattern given (-p)
For each PackageRefence:
* Create Directory.Packages.props file holding the package version
* Remove version for all project files
* Optional (on by default): update nuget config to state Source Mappings - only for nuget.config holding one source - learn about it at
 [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)

Running tool example:
```
 CpmMigrator -s C:\git\ASI\prj\testPrj.sln -p *.sfproj *.Build.props
```

### Command line options
```
 Required option 's, Solution path' is missing.

  -s, --Solution path                          Required. Solution full path

  -p, --Project patterns                       Project Pattern (optional) will use *.csproj in any case

  -n, --Add Source Mappings to nuget.config    (Default: true) Add Source Mappings only for nuget.config with one source
                                               otherwise fails

  --help                                       Display this help screen.

  --version                                    Display version information.

```

**Note: Consolidate nuget versions before running in case of mutiple version of the same nuget - will fail**