Logging
=======

Implemented TownsharpLogging static class.  Use LoggingFactory to assign the global logging factory.

```csharp
// use the logger configuration from the host
TownsharpLogging.LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
```

you can also resolve via 
this.logger = TownsharpLogging.CreateLogger<SubscriptionManager>();

I might also create some global static loggers there which change when the Factory changes if needed.
