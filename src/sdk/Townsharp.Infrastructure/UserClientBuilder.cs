using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;
using Townsharp.Infrastructure.Consoles;
using Townsharp.Infrastructure.Identity;
using Townsharp.Infrastructure.WebApi;

namespace Townsharp.Infrastructure;

public class UserClientBuilder
{
   private UserCredential userCredential;
   private ILoggerFactory loggerFactory;
   private readonly IHttpClientFactory httpClientFactory;
   private readonly UserTokenProvider userTokenProvider;

   public UserClientBuilder(UserCredential userCredential, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
   {
      this.userCredential = userCredential;
      this.loggerFactory = loggerFactory;
      this.httpClientFactory = httpClientFactory;

      this.userTokenProvider = new UserTokenProvider(this.userCredential, this.httpClientFactory, this.loggerFactory.CreateLogger<UserTokenProvider>());
   }
   public WebApiUserClient BuildWebApiClient()
      => new WebApiUserClient(this.userTokenProvider, this.httpClientFactory, this.loggerFactory.CreateLogger<WebApiUserClient>());

   public IConsoleClient BuildConsoleClient(IWebApiClient webApiClient, int serverId)
   {
      var result = webApiClient.RequestConsoleAccessAsync(serverId).Result;

      if (!result.IsSuccess || !result.Content.IndicatesAccessGranted)
      {
         throw new UnauthorizedAccessException($"Failed to obtain console access. {result.ErrorMessage ?? "Access Denied"}");
      }

      return new ConsoleWebsocketClient(result.Content.BuildConsoleUri(), result.Content.token!, this.loggerFactory.CreateLogger<ConsoleWebsocketClient>());
   }
}