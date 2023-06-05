Townsharp
=========

This is a .NET client library for altavr.io's "A Township Tale" written in C#.

This implementation is a work in progress and will be done in parts as I rebuild from my prototypes based on my current design as I learn from my past iterations.  

Unlike my past interations, this work will be done in public and I will also try to maintain supporting documentation as I go.

Here for Alta's API documentation?
==================================

If so, then look no further than the /.docs/ folder.  I'll expand upon this as I go but for now it only contains a mostly-complete set of documentation for the Township Tale Event Subscription service.

Contributing
============

For now, contributing is open to public documentation and testing scenarios.  Please feel free to submit PRs to update the docs.  If you want to help test, reach out to me on the ATT Meta discord.  Please do not DM me, but you may @mention me.

Design
======

I'll flesh this out more later, but TownSharp has 3 basic parts and 3 basic models of interacting with the system.

1). Managed Host Extensions for Bots based on "Townsharp.Session" class.
2). Direct usage of the Session class and the rest of the Townsharp Rich Entities Models and related systems through manual bootstrapping and lifecycle management.
3). Direct usage of the Townsharp.Infrastructure library types to have low level control over the various systems Alta exposes.

Roadmap
=======

I'm starting with the Infrastructure implementations and documentation.  I expect this part will go relatively fast as I've already implemented the bulk of it, but I've decided to change a few things, so it won't just be copy paste.

- [ ] Townsharp.Infrastructure
  - [ ] Subscriptions
    - [X] Subscription Client - Used to handle request/response correllation
    - [ ] Subscription Connection - Used to manage lifecycle of migrations and tracks subscriptions it is responsible for
    - [ ] Subscription Manager - Used to assign subscriptions to subscription connections, and manage multiplexing of connections for scale.
  - [ ] Web API
    - [ ] Web Api Client - Exposes REST Api operations against Alta's WebApi endpoint, and handles transient faults, and request pagination.
  - [ ] Server Console
    - [ ] Server Console Client - Used to handle request/response correllation for the console, and expose Server Event Subscription
  - [ ] Identity
    - [X] Bot Token Provider - Provides access tokens for a bot given Bot Credentials.  Handles refresh automatically.
    - [ ] User Token Provider - Provides access tokens for a user given User Credentials.  Handles refresh automatically.
- [ ] Townsharp.Model
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
  - [X] Subscriptions
  - [ ] Web API
  - [ ] Server Console
  - [ ] Identity
