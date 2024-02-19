Townsharp
=========

This is a .NET client library for altavr.io's "A Township Tale" written in C#.

This implementation is a work in progress and will be done in parts as I rebuild from my prototypes based on my current design as I learn from my past iterations.  

Unlike my past interations, this work will be done in public and I will also try to maintain supporting documentation as I go.

Here to use Townsharp?
======================

Townsharp is in **early preview** and only the infrastructure layer is currently implemented in a way suitable for use.  
This means that you can use the low level types to interact with Alta's API, but things will not be as simple as the client and managed bot service libraries will make it.  
This is not recommended for most users, but if you are a developer who is comfortable with the low level details of Alta's API, then you may find this useful.

If you are wanting to use Townsharp.Infrastructure anyways, then you can install it from NuGet with the following command:

```powershell
Install-Package Townsharp.Infrastructure -Pre
```

or using the .NET CLI:

```bash
dotnet add package Townsharp.Infrastructure --prerelease
```

It can also be found in Visual Studio's NuGet Package Manager by searching for "Townsharp.Infrastructure" and selecting the "Include prerelease" checkbox.

If you're an early adopter, please feel free to reach out to me for help and guidance.  I'm happy to help you get started, and could certainly use feedback before going fully public with this release.

Here for Alta's API documentation?
==================================

If so, then look no further than the [repository docs](/.docs/docs.md) folder.  I'll expand upon this as I go but for now it only contains a mostly-complete set of documentation for the Township Tale Event Subscription service.

Contributing
============

For now, contributing is open to public documentation and testing scenarios.  Please feel free to submit PRs to update the docs.  
If you want to help test, reach out to me on the ATT Meta discord or join us at [our official Discord Server](https://discord.gg/yzwRMqwMwd).  
Please do not DM me, but you may @mention me.

Design
======

TownSharp has 3 basic parts and 3 basic models of interacting with the system.

1). Managed Host Extensions for Bots based on "Townsharp.Session" class.
2). Direct usage of the Session class and the rest of the Townsharp Rich Entities Models and related systems through manual bootstrapping and lifecycle management.
3). Direct usage of the Townsharp.Infrastructure library types to have low level control over the various systems Alta exposes.

Roadmap
=======

The Townsharp Infrastucture library is the first to be implemented and is currently in a state where it can be used to interact with Alta's API reliably and at very high levels of scale, 
having been tested with hundreds of concurrent console sessions, and thousands of subscriptions across thousands of servers.

- [X] Townsharp.Infrastructure
  - [X] Subscriptions
    - [X] Subscription Client - Used to handle request/response correllation
    - [X] Subscription Connection - Used to manage lifecycle of migrations and tracks subscriptions it is responsible for
    - [X] Subscription Manager - Used to assign subscriptions to subscription connections, and manage multiplexing of connections for scale.
  - [X] Web API
    - [X] Web Api Client - Exposes REST Api operations against Alta's WebApi endpoint, and handles transient faults, and request pagination.
  - [X] Server Console
    - [X] Server Console Client - Used to handle request/response correllation for the console, and expose Server Event Subscription
  - [X] Identity
    - [X] Bot Token Provider - Provides access tokens for a bot given Bot Credentials.  Handles refresh automatically.
    - [X] User Token Provider - Provides access tokens for a user given User Credentials.  Handles refresh automatically.

- [ ] Townsharp.Core

- [ ] Townsharp.Client

- [ ] Townsharp.Domain - ? Might merge into Townsharp.
  - [ ] Groups
    - [ ] Roles
  - [ ] Servers
    - [ ] Server Console
    - [ ] Server Events
  - [ ] Global Events
  - [ ] Users
  - [ ] Monitoring
    - [ ] Metrics Hooks
    - [ ] Logging Hooks

- [ ] Townsharp
  - [ ] Session
  - [ ] Server Manager
  - [ ] Group Manager
  - [ ] User Manager


- [ ] Alta Documentation
  - [X] Subscriptions - Mostly Complete
  - [ ] Web API
  - [X] Server Console - Mostly Complete
  - [ ] Identity

- [X] OpenTelemetry Integration - Samples only, but supported in the infrastructure layer.