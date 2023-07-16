**Game Console on the "game server websocket"**
================================================

This is a websocket endpoint exposed by the game server.

**Lifecycle**
=============

**Connecting**
--------------

In order to connect, you will need to use the WebAPI to get the connection information provided by the [Server Console Access]() endpoint.

Capture the response of an online server and use the values from the connection property as follows: 

`ws://<address>:<websocket_port>`

So for example, with a response of: 

```
{
    "server_id": 1408514223,
    "allowed": true,
    "was_rejection": false,
    "cold_start": false,
    "fail_reason": "Nothing",
    "connection": {
        "server_id": 0,
        "address": "3.67.6.208",
        "local_address": "127.0.0.1",
        "pod_name": "att-release-plfbq-wmm72",
        "game_port": 7287,
        "console_port": 7684,
        "logging_port": 7213,
        "websocket_port": 7397,
        "webserver_port": 7589
    },
    "token": "..."
}
```

You will connect to `ws://3.67.6.208:7397` without TLS.  Please note, this means operations will be transmitted and received in plaintext, and might be vulnerable to interception by a third party.  Never transmit anything sensitive in plaintext over this channel.

Which brings us to...

**Authentication**
------------------

Authentication is done immediately -after- connecting to the websocket, and is simply transmiting the literal value of the `connection.token` property in the same response body used for the connection address and port.

This must be the first message sent or the connection will fail.

Once authenticated, you will get a message back of the following form:

```
{
    "type": "SystemMessage",
    "timeStamp": "2023-07-01T18:23:32.709941Z",
    "eventType": "InfoLog",
    "data": "Connection Succeeded, Authenticated as: <account_id> - <account_name>"
}
```

See [Bots Identities and Authentication](../Identity/Bots.md) for more information.

`Idle Timeout`
--------------

Once connected, the connection will drop in ~10 minutes if no messages are sent or received. Consider implementing a keepalive by sending a simple "ping" message over the websocket.  You will get a 400 response code message back, but that's fine.

**Requests**
============

All requests are sent as JSON objects over the websocket connection.  The request object has the following structure:

```json
{
    "id": 1,
    "content": "<escaped command string>"
}
```

* `id` is a unique identifier for the request.  This is used to match responses to requests.  The server will always respond with the same `id` that was sent in the request unless a critical infrastructure error occurs or a request times out.
* `content` is the content of the request and is currently only used for [Send Migration Token](#send-migration-token) and [Batch Subscription](#batch-subscription) scenarios.

**Commands**
=============

**Responses**
-------------

All command responses are received as JSON objects over the websocket connection.  The response object has the following structure:

```json
{
    "type": "CommandResult",
    "timeStamp": "2023-07-02T03:04:55.727087Z",
    "data": {
        "Result": "Success",
        "ResultString": "Success",
        "Command": {
            "Parameters": [
                {
                    "Type": "System.String",
                    "HasDefault": false,
                    "Default": null,
                    "Attributes": [],
                    "Name": "eventType",
                    "FullName": "eventType"
                }
            ],
            "IsProgressive": false,
            "ReturnType": "Alta.Console.WebSocketCommandHandler+SubscriptionResult",
            "Priority": 0,
            "Aliases": [
                "subscribe"
            ],
            "FullName": "websocket.subscribe",
            "Requirements": [],
            "Attributes": [],
            "Name": "subscribe",
            "Description": "Subscribes to an event"
        }
    },
    "commandId": 1
}
```

* `commandId` is a unique identifier for the response.  This is used to match responses to requests.  The server will always respond with the same `id` that was sent in the request unless the request times out.
* `type` - documentation pending
* `timestamp` is the time the event was handled by the server in UTC time.
* `data` is the payload of the command and somewhat varies from command to command.  More documentation on this pending as I catalog more data.

`Error Responses`
-----------------

Pending...

**Subscription Events**
=======================
Pending

For a full list of events see [List of Console Subscription Events](#list-of-console-subscription-events) below.

**List of Console Subscription Events**
=======================================
Pending

__________________________________________________
Player Joined the Server `PlayerJoined`
--------------------------------------------------

`DOCUMENTATION PENDING`

`Triggers`
--------
A player joins the server

`Use Case`
--------
Responding to a player joining the server

`Content`
-------

```
 {"type":"Subscription","timeStamp":"2023-07-13T23:39:23.462604Z","eventType":"PlayerJoined","data":{"user":{"id":1741816681,"username":"jjamesdh2007"},"mode":"MetaQuest","position":[-645.957031,128.205,209.908966]}}
```