Current Work
============

6/18/2023
---------

### SubscriptionConnection
- HandleOnWebsocketFaulted is synchronous but calls RecoverConnectionAsync
  - So, wrong design then entirely?
    - Close Channel with error? Or must that be an exception :/
- No Unsubscription
  - Right now work queue is just subscription tasks
  - Do we handle unsubscription seperate from subscription?
  - Do we handle them together?
  - If so, how do we reconcile owning a sub and an unsub?
    - I think when we add an unsub, we check if there's a sub in work and if so, we remove it from work (and ownership), then run unsubscribe in from its own work queue?
- The lifecycle feels bad.  Diagram and simplify?
  - Probably not as hard as I'm making it.
- Handling responses feels messy.  This is an issue with the contract between client and connection.

**- What if the work queue were a channel?**

### BotTokenProvider
- This is functionally a singleton for all use cases here.  We can probably access it statically.
