**Subscriptions system on the "main websocket"**
================================================

This is a websocket endpoint exposed at `wss://websocket.townshiptale.com` that allows authenticated bots to subscribe to various township tale events.

**Authentication**
==================

Authentication is done at the time of negotiating the websocket connection over HTTP via a standard HTTP Authorization Header using a bearer token scheme.

See [Bots Identities and Authentication](../Identity/Bots.md) for more information.

**Lifecycle**
=============

`Connection`
------------

To connect, direct a websocket client at `wss://websocket.townshiptale.com` and use the `Authentication` header [described above](#authentication)

Upon connecting, the websocket will be immediately available for requests. See [Requests](#requests) for more information.

Typically you will want to send a `subscribe` request to start receiving events.  Events are details in [Subscription Events](#subscription-events).

`Migration`
-----------

The server will disconnect any active session after 2 hours, regardless of activity.  This is a limitation of the AWS gateway that is being used for websocket communication.

Alta has implemented a migration system that will allow you to create a new connection, and migrate your subscriptions to a new websocket connection.
There are however some caveats to this system:

- Migration duration seems to be directly related to the number of subscriptions you have.  The more subscriptions you have, the longer it will take to migrate.
- All requests, including migration, are subject to the global 30 second request timeout. If you have a lot of subscriptions, it may take longer than 30 seconds to migrate, and the migration will fail.  In this developers experience as of June 2023, this starts to get dicey above 500 active subscriptions, so consider multiplexing your connections after this scale target.
- In the event that a migration fails, the old client -and- new client will be in an indeterminate state and all subscriptions will need to be re-sent from scratch.

`Idle Timeout`
--------------

Once connected, the connection will drop in ~10 minutes if no messages are sent or received. Consider implementing a keepalive by sending a simple "ping" message over the websocket.  You will get a 400 response code message back, but that's fine.

**Requests**
============

All requests are sent as JSON objects over the websocket connection.  The request object has the following structure:

```json
{
    "id": 1,
    "path": "",
    "method": "",
    "authorization": "Bearer <token>",
    "content": any valid json value.  This is optional, and depends on the request.
}
```

* `id` is a unique identifier for the request.  This is used to match responses to requests.  The server will always respond with the same `id` that was sent in the request unless a critical infrastructure error occurs or a request times out.
* `path` is a string that is used to route the request to the correct handler.  This is similar to a URL path in RESTful HTTP APIs.  See individual Request model examples below.
* `method` is the HTTP method to use for the request.  This is synonymous with the HTTP method.  Valid values are `GET`, `POST`, `DELETE`
* `authorization` uses a valid [Bearer Token](../Identity/Bots.md) to authenticate the request.  This is required for all requests and must still be valid.  Given that the token may expire during the lifecycle of a given connection, it is advised to either refresh the token and capture it per client prior to connecting, or to use a provider pattern that is expiration aware and return either a valid cached token, or fetch a new token asynchronously.
* `content` is the content of the request and is currently only used for [Send Migration Token](#send-migration-token) and [Batch Subscription](#batch-subscription) scenarios.

See [List of Requests and Responses](#list-of-requests-and-responses) below for more details.

**Responses**
=============

All responses are received as JSON objects over the websocket connection.  The response object has the following structure:

```json
{
    "id": 1,
    "event": "response",
    "key": "",
    "content": "",
    "responseCode": 200
}
```
* `id` is a unique identifier for the response.  This is used to match responses to requests.  The server will always respond with the same `id` that was sent in the request unless a critical infrastructure error occurs or a request times out.
* `event` is a string that represents what has occurred on the server that the client is being notified about.  With regard to a request, event is **always** '`response`'.
* `key` specifies the subject of the request and varies depending on the request.
* `content` is the content of the response and is used for [Batch Subscription](#batch-subscription) and [Get Migration Token](#get-migration-token) responses.  It is worth noting that the content will itself be serialized json, meaning it will need to be independently read as a string and deserialized into the target object form.
* `responseCode` directly correllates to HTTP Response Status Codes (although this developer is disappointed to not yet get [418](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/418))

`Error Responses`
-----------------

Some requests will return non 2xx response codes if sent incorrectly.  For instance, attempting to subscribe to a server that doesn't exist will return a 404 response code.  When this occurs, the content will be in the ErrorContent format below.

```json
{
    "message": "Something went horribly wrong and it's probably Polyphony's Fault.",
    "error_code": ":blame_polyphony:"
}
```

Putting the whole response together this will look something like this:

```json
{
    "id": 1,
    "event": "response",
    "key": "GET /ws/coffee",
    "content": "{\"message\":\"Polyphony that's a teapot.\",\"error_code\":\":blame_polyphony:\"}",
    "responseCode": 418
}
```

See [List of Requests and Responses](#list-of-requests-and-responses) below for more details.

**Infrastructure Errors**
=========================

Although relatively rare, and usually only occurring when mis-using the service (truly concurrent requests at very high scales for instance) but infrastructure errors do occassionally occur and should be observed and handled.

They are of the following form, and are received from the AWS infrastructure, not directly from Alta's Service.

```json
{
    "message": "Error Message",
    "connectionId":"H-SaJeT8oECIUg=",
    "requestId":"H-SbUEHSoEF3JQ="
}
```

**Subscription Events**
=======================

Subscription Events are more-or-less what they say on the tin.  Events that occur server-side that the client subscribes to receive notification of in a pub-sub model.  Events look very similarly to [Responses](#responses), but will notably have a different value for the `event` field rather than `response`.

```json
{
    "id": 0,
    "event": "group-server-status",
    "key": "1156211297",
    "content": "{\"id\":1174503463,\"name\":\"Cairnbrook\",\"online_players\":[],\"server_status\":\"Online\",\"final_status\":\"Online\",\"scene_index\":4,\"target\":2,\"region\":\"north-america-east\",\"online_ping\":\"2022-09-03T23:30:21.2285207Z\",\"last_online\":\"2022-09-03T23:30:21.2285207Z\",\"description\":\"A casual server for friendly players to explore, discover, and create.\",\"playability\":0.0,\"version\":\"main-0.1.3.33060\",\"group_id\":1156211297,\"owner_type\":\"Group\",\"owner_id\":11111111,\"type\":\"Normal\",\"fleet\":\"att-quest\",\"up_time\":\"3.01:17:05.6823802\",\"join_type\":\"PrivateGroup\",\"player_count\":0,\"created_at\":\"2021-09-10T20:34:29.2385673Z\"}",
    "responseCode": 200
}
```

* `id` will always be 0 for an event and can safely be disregarded.
* `event` is the subscription event previously subscribed to that has occurred.
* `key` is either the groupid or the userid for the bot that was previously subscribed to.
* `content` is the content of the event and is unique to the event type subscribed to.  It is worth noting that the content will itself be serialized json, meaning it will need to be independently read as a string and deserialized into the target object form.
* `responseCode` will always be 0 for an event and can safely be disregarded.

For a full list of events see [List of Subscription Events](#list-of-subscription-events) below.

**List of Requests and Responses**
==================================

Below is a list of the [Requests](#requests) and correllated [Responses](#responses) for the operations available to the client.
___________
`Subscribe`
-----------

**REQUEST**

```json
{
    "id": 1,
    "path": "subscription/group-server-heartbeat/12345678",
    "method": "POST",
    "authorization": "Bearer <token>"
}
```

* `id` as always is a unique value and will be paired with the response below.
* `path` is of the form `"subscription/<event-id>/<key-id>"` given:
    * `event-id` from [List of Subscription Events](#list-of-subscription-events).
    * `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.
* `method` will always be POST for a subscribe request.
* `authorization` is described above in [Requests](#requests).

**RESPONSE**

A successful subscription request will see a response like below.  For non 200 responseCodes, see [Error Responses](#error-responses) above.

```json
{
    "id": 1,
    "event": "response",
    "key": "POST /ws/subscription/group-server-heartbeat/12345678",
    "content": "",
    "responseCode": 200
}
```

* `id` as always is a unique value and will be paired with the request above.
* `path` is of the form `"POST /ws/subscription/<event-id>/<key-id>"` correllated to the request above given:
    * `event-id` from [List of Subscription Events](#list-of-subscription-events).
    * `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.
* `content` will always be empty when responseCode is 200.  Otherwise content would indicate an error.  see [Error Responses](#error-responses) above.
* `responseCode` is described above in [Responses](#responses).

_____________
`Unsubscribe`
-------------

Unsubscribe is like subscribe in nearly every way except the method will be DELETE in the request and indicates intent to no longer receive events for a particular event-id and key-id

**REQUEST**
```json
{
    "id": 1,
    "path": "subscription/group-server-heartbeat/12345678",
    "method": "DELETE",
    "authorization": "Bearer <token>"
}
```

* `id` as always is a unique value and will be paired with the response below.
* `path` is of the form `"subscription/<event-id>/<key-id>"` given:
    * `event-id` from [List of Subscription Events](#list-of-subscription-events).
    * `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.
* `method` will always be DELETE for an unsubscribe request.
* `authorization` is described above in [Requests](#requests).

**RESPONSE**
A successful unsubscription request will see a response like below.  For non 200 responseCodes, see [Error Responses](#error-responses) above.

```json
{
    "id": 1,
    "event": "response",
    "key": "DELETE /ws/subscription/group-server-heartbeat/12345678",
    "content": "",
    "responseCode": 200
}
```
* `id` as always is a unique value and will be paired with the request above.
* `path` is of the form `"DELETE /ws/subscription/<event-id>/<key-id>"` correllated to the request above given:
    * `event-id` from [List of Subscription Events](#list-of-subscription-events).
    * `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.
* `content` will always be empty when responseCode is 200.  Otherwise content would indicate an error.  see [Error Responses](#error-responses) above.
* `responseCode` is described above in [Responses](#responses).

_________________
`Batch Subscribe`
-----------------

Batch subscribe is used to subscribe to many events all in a single request.

`NOTE: As of June 2023, it is not advised that batch subscribe requests be sent concurrently or in excess of 100 distinct subscriptions at a time in a single request.  Furthermore, there appears to be a bug where using more than one event-id with the same key-id will cause an error response to be returned.  As a result current guidance is not to use batch subscription until Alta has addressed these issues.`

**REQUEST**

```json
{
    "id": 1,
    "path": "subscription/batch",
    "method": "POST",
    "authorization": "Bearer <token>",
    "content": [{"event": "group-server-heartbeat", "keys":["123456789","234567891","345678912","456789123","567891234","678912345","789123456","891234567","912345678"]}]
}
```
}

* `id` as always is a unique value and will be paired with the response below.
* `path` is of the form `"subscription/batch
* `method` will always be POST for a subscribe request.
* `authorization` is described above in [Requests](#requests).
* `content` is a raw json array containing a list of json objects of the form:
```json
{"event": "event-id", "keys":["key-id 1", "key-id 2", ... "key-id n"]}
```
given:
* `event-id` from [List of Subscription Events](#list-of-subscription-events).
* `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.

**RESPONSE**

A successful batch subscription request will see a response like below.  200 does not indicate that no failures occurred, see below. For non 200 responseCodes, see [Error Responses](#error-responses) above.

```json
{
    "id": 1,
    "event": "response",
    "key": "POST /ws/subscription/batch",
    "content": "{\"success\":false,\"failures\":[\"group-server-heartbeat/2130103740\"]}",
    "responseCode": 200
}
```

* `id` as always is a unique value and will be paired with the request above.
* `path` is of the form `"POST /ws/subscription/<event-id>/<key-id>"` correllated to the request above given:
    * `event-id` from [List of Subscription Events](#list-of-subscription-events).
    * `key-id` where key-id is either a group id, or the user id of the bot itself.  See [List of Subscription Events](#list-of-subscription-events) to determine which to use.
* `content` will always be populated for batch subscription requests when the responseCode is 200.  Otherwise content would indicate an error.  see [Error Responses](#error-responses) above.  Normal response content is a JSON encoded string that needs to be seperately deserialized, containing a success boolean, and a list of failures.
    * `success` if true, indicates all subscription requests were completed successfully.
    * `failures` is an array of value of the form `'<event-id>/<key-id>'` as described above.
* `responseCode` is described above in [Responses](#responses), but a 200 does not indicate all subscriptions succeeded as described in this section.

_____________________
`Get Migration Token`
---------------------

**REQUEST**
```json
{
    "id": 1,
    "path": "migrate",
    "method": "GET",
    "authorization": "Bearer <token>"
}
```

**RESPONSE**
```json
{
    "id": 1,
    "event": "response",
    "key": "GET /ws/migrate",
    "content": "{\"token\":\"<migration token>\"}",
    "responseCode": 200
}
```

______________________
`Send Migration Token`
----------------------

**REQUEST**
```json
{
    "id": 1,
    "path": "migrate",
    "method": "POST",
    "authorization": "Bearer <token>",
    "content": "{\"token\":\"<migration token>\"}"
}
```

**RESPONSE**
```json
{
    "id": 1,
    "event": "response",
    "key": "POST /ws/migrate",
    "content": "",
    "responseCode": 200
}
```

List of Subscription Events
===========================
Below is a list of all of the Subscription Events that can be emitted by the service.  Subscribe to them using a [Subscribe Request](#subscribe).  You will then receive Subscription events whenever the `Trigger` conditions are met for the subscription scope (event-id and key-id).

`NOTE: All content models for events are JSON encoded strings within the content property of the event message.`

_________________________________________
Server Heartbeat `group-server-heartbeat`
-----------------------------------------

The server heartbeat event is used to determine the liveness of a server and is quite chatty.

`Triggers`
--------
The Server Heartbeat event is raised every 15 seconds for a server that is online, or transitioning to or from an online status.

`Use Case`
--------
It is safe to assume that if a given server misses 2 heartbeat events it is no longer online.

If heartbeats resume, the server should be available shortly thereafter.  

`Content`
-------
Content appears to be identitical to [Server Status Changed](#server-status-changed-group-server-status) below, but triggers under different circumstances.

___________________________________________
Server Status Changed `group-server-status`
-------------------------------------------

The server status changed event is used to note changes in the server's availability and player count.

`Triggers`
--------
When a server comes online, has a player join, or has a player leave.

`Use Case`
--------
The Server Status Changed event can be used to monitor server populations and availability.  It cannot however be used on its own to determine when a server has gone offline, although you can safely assume that once a server's population reaches 0, it will go offline if you don't receive any new event after the configured time (default 5 minutes.)  However, the Web API, or heartbeats would be a more reliable way to determine status.

`Content`
-------

```json
{
    "id":1174503463,
    "name":"Cairnbrook",
    "online_players": [{"name":"Polyphony", "id": 1234567}],
    "server_status": "Online",
    "final_status": "Online",
    "scene_index": 4,
    "target": 2,
    "region": "north-america-east",
    "online_ping": "2022-09-03T23:30:21.2285207Z",
    "last_online": "2022-09-03T23:30:21.2285207Z",
    "description": "A casual server for friendly players to explore, discover, and create.",
    "playability": 0.0,
    "version": "main-0.1.3.33060",
    "group_id": 1156211297,
    "owner_type": "Group",
    "owner_id":11111111,
    "type":"Normal",
    "fleet":"att-quest",
    "up_time":"3.01:17:05.6823802",
    "join_type":"PrivateGroup",
    "player_count":1,
    "created_at":"2021-09-10T20:34:29.2385673Z"
}
```

`DOCUMENTATION PENDING`

____________________________
Group Changed `group-update`
----------------------------

`Triggers`
--------
The group update event occurs whenever the state of the group changes.  This is ususally the name, description, or group type (public/private/open)

`Use Case`
--------
Primarily this would be used to confirm a change has occurred and is recognized by Alta, but it could also be used to update things like dashboards when changes to the server name, description, or type occur.

`Content`
-------
```json
{
    "id": 3456789012,
    "name": "Group Name",
    "description": "Group Description",
    "member_count": 1,
    "created_at": "2022-11-19T04:55:03.2330903Z",
    "type": "Open",
    "tags": []
}
```

`DOCUMENTATION PENDING`

_______________________________________________
Group Permissions Changed `group-member-update`
-----------------------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------

`Use Case`
--------

`Content`
-------

```json
{
    "group_id": 123456789,
    "user_id": 111111111,
    "username": "Polyphony",
    "bot": false,
    "icon": 0,
    "permissions": "Member",
    "role_id": 1,
    "created_at": "2021-10-05T18:38:58.288Z",
    "type": "Accepted"
}
```

__________________________________________________
Bot Received Group Invite `me-group-invite-create`
--------------------------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------

`Use Case`
--------

`Content`
-------

_________________________________________________
Bot Group Invite Revoked `me-group-invite-delete`
-------------------------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------

`Use Case`
--------

`Content`
-------

__________________________________
Bot Joined Group `me-group-create`
----------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------

`Use Case`
--------

`Content`
-------

________________________________
Bot Left Group `me-group-delete`
--------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------

`Use Case`
--------

`Content`
-------
