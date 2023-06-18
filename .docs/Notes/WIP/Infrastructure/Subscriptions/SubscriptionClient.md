SubscriptionClient
==================

Responsible for the websocket connection and exposing methods for requests/responses, evaulating the results of those requests, and receiving SubscriptionEvents.

Lifecycle
---------

Failure Modes
-------------

Current Concerns
----------------

### 6/18/2023
Events are synchronous.  We should probably emit them asynchronously via a `ChannelWriter<T>`.

Failures and state aren't super clear, lifecycle could likely use some improvement.  The more functional the better.  Consider more "railroad oriented" design here.

