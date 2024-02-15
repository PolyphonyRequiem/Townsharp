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

If it's an exception it would look like this:

```json
{
    "type": "CommandResult",
    "timeStamp": "2023-10-20T02:42:57.791354Z",
    "data": {
        "Exception": {
            "ClassName": "System.Exception",
            "Message": "[CommandService.Handle] Command not found. Did you mean: \nplayer list\n",
            "Data": null,
            "InnerException": null,
            "HelpURL": null,
            "StackTraceString": null,
            "RemoteStackTraceString": null,
            "RemoteStackIndex": 0,
            "ExceptionMethod": null,
            "HResult": -2146233088,
            "Source": null
        }
    },
    "commandId": 1
}
```

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
{
    "type": "Subscription",
    "timeStamp": "2023-07-13T23:39:23.462604Z",
    "eventType": "PlayerJoined",
    "data": {
        "user": {
            "id": 1741816681,
            "username": "jjamesdh2007"
        },
        "mode": "MetaQuest",
        "position": [
            -645.957031,
            128.205,
            209.908966
        ]
    }
}
```



___________________________________________________
Samples
---------------------------------------------------
`player list-detailed`
```json
{
    "type": "CommandResult",
    "timeStamp": "2023-10-20T02:43:07.506881Z",
    "data": {
        "Result": [
            {
                "Position": [
                    -874.841,
                    516.369,
                    -1709.176
                ],
                "HeadPosition": [
                    -874.4199,
                    518.1174,
                    -1709.69238
                ],
                "HeadForward": [
                    0.6811205,
                    -0.0382161438,
                    -0.7311732
                ],
                "HeadUp": [
                    -0.0246029757,
                    0.996878266,
                    -0.07502249
                ],
                "LeftHandPosition": [
                    -874.2444,
                    517.223,
                    -1709.52722
                ],
                "LeftHandForward": [
                    0.685114,
                    0.444883674,
                    -0.5767998
                ],
                "LeftHandUp": [
                    -0.252432257,
                    0.887768,
                    0.3848975
                ],
                "RightHandPosition": [
                    -874.40094,
                    517.8962,
                    -1710.37915
                ],
                "RightHandForward": [
                    0.518226743,
                    0.851337969,
                    -0.08164435
                ],
                "RightHandUp": [
                    -0.335427463,
                    0.29013443,
                    0.8962763
                ],
                "Chunk": "1210 - Chunk 18-5 Tower (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 39455,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1048749325,
                "username": "Tokii"
            },
            {
                "Position": [
                    -898.103,
                    160.665009,
                    97.1459961
                ],
                "HeadPosition": [
                    -897.8551,
                    161.390381,
                    97.03905
                ],
                "HeadForward": [
                    -0.66858083,
                    -0.438375443,
                    -0.600689232
                ],
                "HeadUp": [
                    -0.3300252,
                    0.898776352,
                    -0.288590461
                ],
                "LeftHandPosition": [
                    -898.237,
                    161.042252,
                    96.86317
                ],
                "LeftHandForward": [
                    0.00434866548,
                    0.94653,
                    -0.322587252
                ],
                "LeftHandUp": [
                    0.6667433,
                    0.237676024,
                    0.7063737
                ],
                "RightHandPosition": [
                    -898.1393,
                    160.950745,
                    97.03358
                ],
                "RightHandForward": [
                    -0.489302754,
                    0.8274294,
                    -0.2755812
                ],
                "RightHandUp": [
                    0.6549896,
                    0.5572912,
                    0.510309339
                ],
                "Chunk": "1172 - Chunk 17-33 Town Hall (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 74412,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1651352431,
                "username": "XxStolasxX"
            },
            {
                "Position": [
                    -726.423,
                    133.614,
                    28.426
                ],
                "HeadPosition": [
                    -725.1198,
                    134.885941,
                    26.9029274
                ],
                "HeadForward": [
                    0.134818077,
                    -0.873382568,
                    0.468003243
                ],
                "HeadUp": [
                    0.152305052,
                    0.484968,
                    0.861167431
                ],
                "LeftHandPosition": [
                    -725.3517,
                    134.609665,
                    26.62263
                ],
                "LeftHandForward": [
                    0.0397159234,
                    0.41234982,
                    0.910159469
                ],
                "LeftHandUp": [
                    0.0953667,
                    0.9051557,
                    -0.4142443
                ],
                "RightHandPosition": [
                    -724.9311,
                    134.2574,
                    27.0076332
                ],
                "RightHandForward": [
                    -0.8527084,
                    0.122684717,
                    0.5077763
                ],
                "RightHandUp": [
                    -0.410353839,
                    0.444195241,
                    -0.7964297
                ],
                "Chunk": "1371 - Chunk 20-32 Town (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 55164,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1530534311,
                "username": "willburr12234"
            },
            {
                "Position": [
                    -899.753,
                    160.654,
                    96.502
                ],
                "HeadPosition": [
                    -899.6302,
                    161.984909,
                    96.5979843
                ],
                "HeadForward": [
                    0.18202056,
                    -0.185460374,
                    -0.965646863
                ],
                "HeadUp": [
                    0.152984679,
                    0.9754345,
                    -0.158503249
                ],
                "LeftHandPosition": [
                    -899.4535,
                    161.739716,
                    96.27885
                ],
                "LeftHandForward": [
                    0.313205063,
                    0.9452524,
                    0.09166233
                ],
                "LeftHandUp": [
                    -0.3482834,
                    0.0245317034,
                    0.937069058
                ],
                "RightHandPosition": [
                    -899.8429,
                    161.618988,
                    96.46182
                ],
                "RightHandForward": [
                    -0.576565,
                    0.6131778,
                    -0.539987743
                ],
                "RightHandUp": [
                    -0.42936933,
                    0.334895372,
                    0.83874166
                ],
                "Chunk": "1172 - Chunk 17-33 Town Hall (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 66579,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 837665815,
                "username": "FloofyBoi112"
            },
            {
                "Position": [
                    -677.483,
                    128.151,
                    113.811005
                ],
                "HeadPosition": [
                    -677.152466,
                    129.2272,
                    114.205696
                ],
                "HeadForward": [
                    0.8379691,
                    -0.418085456,
                    0.350731283
                ],
                "HeadUp": [
                    0.357437253,
                    0.906139553,
                    0.22616297
                ],
                "LeftHandPosition": [
                    -676.883667,
                    128.902374,
                    114.40464
                ],
                "LeftHandForward": [
                    -0.015044421,
                    0.9381781,
                    0.345827132
                ],
                "LeftHandUp": [
                    -0.576698363,
                    0.2744004,
                    -0.769496262
                ],
                "RightHandPosition": [
                    -676.8556,
                    128.977325,
                    114.334572
                ],
                "RightHandForward": [
                    0.258395,
                    0.9533293,
                    -0.156190753
                ],
                "RightHandUp": [
                    -0.840550542,
                    0.142182261,
                    -0.522742033
                ],
                "Chunk": "1438 - Chunk 21-33 Town (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 88878,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 396314614,
                "username": "goofyg"
            },
            {
                "Position": [
                    -734.6995,
                    134.522659,
                    3.8116672
                ],
                "HeadPosition": [
                    -734.755859,
                    136.032974,
                    3.63211322
                ],
                "HeadForward": [
                    -0.8621163,
                    0.505004764,
                    -0.0415449
                ],
                "HeadUp": [
                    0.504243851,
                    0.863111,
                    0.027883932
                ],
                "LeftHandPosition": [
                    -735.0951,
                    136.08725,
                    3.574622
                ],
                "LeftHandForward": [
                    0.513149858,
                    0.275373548,
                    0.8129248
                ],
                "LeftHandUp": [
                    -0.134671912,
                    -0.90957123,
                    0.393121868
                ],
                "RightHandPosition": [
                    -734.8875,
                    135.892776,
                    3.86975145
                ],
                "RightHandForward": [
                    0.515069,
                    0.561273158,
                    -0.647825
                ],
                "RightHandUp": [
                    0.129901528,
                    -0.798175,
                    -0.5882544
                ],
                "Chunk": "4304 - Blacksmith Building Chunk (Alta.Chunks.LocationChunk)",
                "Body": {
                    "Identifier": 27073,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1830163094,
                "username": "turbo22"
            },
            {
                "Position": [
                    -896.597,
                    160.667,
                    95.404
                ],
                "HeadPosition": [
                    -896.995056,
                    162.404282,
                    95.35396
                ],
                "HeadForward": [
                    -0.456041276,
                    -0.3026766,
                    0.83690697
                ],
                "HeadUp": [
                    -0.186333239,
                    0.9520196,
                    0.242772967
                ],
                "LeftHandPosition": [
                    -897.141235,
                    161.6937,
                    95.35098
                ],
                "LeftHandForward": [
                    0.420592576,
                    0.227528334,
                    0.8782555
                ],
                "LeftHandUp": [
                    0.530288,
                    0.7238094,
                    -0.441468716
                ],
                "RightHandPosition": [
                    -896.6326,
                    161.642044,
                    95.24036
                ],
                "RightHandForward": [
                    -0.6153537,
                    -0.0342983454,
                    0.787504733
                ],
                "RightHandUp": [
                    -0.0575998239,
                    0.9983386,
                    -0.00152746029
                ],
                "Chunk": "1172 - Chunk 17-33 Town Hall (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 89731,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1125947487,
                "username": "Obi-Wan_Skull"
            },
            {
                "Position": [
                    -899.972,
                    160.658,
                    95.391
                ],
                "HeadPosition": [
                    -900.2232,
                    162.092773,
                    95.35663
                ],
                "HeadForward": [
                    0.8904649,
                    -0.2477422,
                    0.3817017
                ],
                "HeadUp": [
                    0.183798835,
                    0.963153362,
                    0.196350574
                ],
                "LeftHandPosition": [
                    -899.961,
                    161.763138,
                    95.48539
                ],
                "LeftHandForward": [
                    0.209175557,
                    0.8965366,
                    0.390471339
                ],
                "LeftHandUp": [
                    -0.853637457,
                    0.362196177,
                    -0.374322385
                ],
                "RightHandPosition": [
                    -900.0667,
                    161.724915,
                    95.3387451
                ],
                "RightHandForward": [
                    -0.05634202,
                    0.8827996,
                    0.4663588
                ],
                "RightHandUp": [
                    -0.259488672,
                    0.4381012,
                    -0.8606585
                ],
                "Chunk": "1172 - Chunk 17-33 Town Hall (Alta.Chunks.OverworldChunk)",
                "Body": {
                    "Identifier": 81957,
                    "Name": "VR Player Character New(Clone)"
                },
                "id": 1571793689,
                "username": "KristianJ777"
            }
        ],
        "ResultString": "System.Collections.Generic.List`1[System.Object]",
        "Command": {
            "Parameters": [],
            "IsProgressive": false,
            "ReturnType": "System.Collections.Generic.IEnumerable`1[Alta.Console.Commands.UserInfoDetailed]",
            "Priority": 0,
            "Aliases": [
                "list-detailed"
            ],
            "FullName": "player.list-detailed",
            "Requirements": [
                {
                    "TypeId": "Alta.Console.ServerOnlyAttribute"
                }
            ],
            "Attributes": [],
            "Name": "list-detailed",
            "Description": "Lists all players"
        }
    },
    "commandId": 1
}
```