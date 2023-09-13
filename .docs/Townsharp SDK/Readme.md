Townsharp
=========

The Townsharp SDK is organized into a handful of layered libraries, with a different set of direct project references expected depending on the usage scenarios.

SDK Libraries
=============

Townsharp
=========

Overview
--------

The Townsharp library is the main library consumed by SDK users.  It contains types related to configuring a Townsharp bot, and concrete implementations of concepts from the Townsharp.Core and Townsharp.Infrastructure libraries that bind them together to enable various bot development scenarios.

What belongs here?
------------------

- Implementations of application scenarios against the Townsharp.Core types that integrate with Townsharp.Infrastructure library types.
- Configuration and opinionated implementations of how to handle certain signals coming from the sdk user, and from infrastructure clients.

What doesn't and why?
---------------------



Townsharp.Core
==============

Overview
--------