SubscriptionConnection
======================

Responsible for the `SubscriptionClient` lifecycle, migrations, fault recovery, and resubscription.  Tracks subscriptions it is responsible for so that it can recovery, and handles subscription request failure recovery.

Lifecycle
---------

Failure Modes
-------------

Testing Needs
-------------

### Manual
- Need to test Unsubscription
- Need to test bad-request handling

### Automated
- Fault handling for all scenarios needs to be testable, this will require some doing.

Current Concerns
----------------

### 6/18/2023
- Lifecycle is kindof a mess.  
- Might also want to adopt `Channel<T>` for events.
- More functional, less class state ideally.
- Needs to implement Unsubscription, shouldn't be too tough, but does mean a seperate pending queue for Unsubscription.
- Currently, no bad-request handling.  I assume a bad request will be retried forever.  Need to verify.
