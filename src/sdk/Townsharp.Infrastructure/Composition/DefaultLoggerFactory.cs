using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Townsharp.Infrastructure.Composition;

internal static class InternalLoggerFactory
{
   static InternalLoggerFactory()
   {
      ServiceCollection services = new();
      services.AddLogging(
          config =>
          {
             //config.Services.AddSingleton<ILoggerProvider>(NullLoggerProvider.Instance);
             config.AddSimpleConsole(o=>o.IncludeScopes=true);
          });

      defaultInstance = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
   }

   private static ILoggerFactory defaultInstance;

   internal static ILoggerFactory Default => defaultInstance;
}
