Logging
=======

Logging should probably be provided via an ambient context rather than injected.  It might be possible to do something like:

Logger<SubscriptionsInfrastructureLogger>.LogInformation(...) as a static provider, but configured via relatively normal mechanisms.  Mostly I think bootstrapping it would be tough to do cleanly.  I wonder if anyone has done this.
