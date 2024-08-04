using Microsoft.Extensions.Logging;

using Townsharp.Infrastructure.Configuration;

namespace Townsharp.Infrastructure;

public class UserClientBuilder
{
   private UserCredential userCredential;
   private ILoggerFactory loggerFactory;

   public UserClientBuilder(UserCredential userCredential, ILoggerFactory loggerFactory)
   {
      this.userCredential = userCredential;
      this.loggerFactory = loggerFactory;
   }
}