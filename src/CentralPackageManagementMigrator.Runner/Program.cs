//setup our DI
using CentralPackageManagementMigrator.Runner;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var serviceProvider = new ServiceCollection()
    .AddLogging((loggingBuilder) => loggingBuilder
        .SetMinimumLevel(LogLevel.Trace)
        .AddSimpleConsole()//options =>
        //{
        //    options.SingleLine = true;
        //    options.TimestampFormat = "[hh:mm:ss] ";
//        })
)
    .AddSingleton<CliRunner>()
    .BuildServiceProvider();

var cliRunner = serviceProvider.GetService<CliRunner>();

await Parser.Default.ParseArguments<CliOptions>(args)
                  .MapResult(async
                  o =>
                  {
                      await cliRunner.RunCliAsync(o);
                  },
                 errors => Task.FromResult(0)
      );
