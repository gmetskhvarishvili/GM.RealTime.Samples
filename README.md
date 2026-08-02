<p align="center">
  <img src="icon.png" alt="GM.RealTime Samples" width="140" height="140" />
</p>

# GM.RealTime Samples

[![CI](https://github.com/gmetskhvarishvili/GM.RealTime.Samples/actions/workflows/ci.yml/badge.svg)](https://github.com/gmetskhvarishvili/GM.RealTime.Samples/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A minimal ASP.NET Core Web API that shows
**[GM.RealTime](https://www.nuget.org/packages/GM.RealTime)** end to end: a JWT-authenticated
SignalR hub, **presence-aware** server→client push through `IRealTimeSender`, and presence lookups
via the shared connection registry. Targets **.NET 10**.

## What it demonstrates

- `AddGMRealTime()` + `MapGMRealTimeHub()` — the whole real-time stack in two calls.
- **JWT WebSocket handshake**: the hub requires auth, and the token is read from
  `?access_token=…` (the sample mints dev tokens so you can try it).
- **Presence-aware delivery**: `UserNotifier.NotifyIfOnlineAsync` pushes only when the user has a
  live connection (using `IConnectionRegistry.IsOnlineAsync`), and reports delivered/skipped.
- Presence queries backed by the shared registry (`GM.Caching` + `GM.DistributedLock`).

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/dev/token/{userId}` | Dev-only: mint a JWT for `userId` to connect the SignalR client |
| `POST` | `/notify/{userId}` | Push `{ event, payload }` to the user if online (returns `delivered`) — requires auth |
| `GET` | `/presence/{userId}` | Whether the user is online and how many connections they have |
| (hub) | `/hubs/realtime` | The SignalR hub (requires a valid JWT) |

## Running

```bash
dotnet run --project GM.RealTime.Sample.API
```

By default it uses the in-memory cache + lock (single process). For presence shared across nodes,
register the Redis backends before `AddGMRealTime` (see the GM.RealTime README) and run Redis:
`docker run -p 6379:6379 -d redis`.

### Connecting a browser client

```js
import * as signalR from "@microsoft/signalr";

const { token } = await (await fetch("/dev/token/alice", { method: "POST" })).json();

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/realtime", { accessTokenFactory: () => token })
  .build();

connection.on("ReceiveMessage", m => console.log(m.event, m.payload));
await connection.start();
// now POST /notify/alice { "event": "ping", "payload": { "hi": true } } and watch it arrive
```

## Testing

```bash
dotnet test
```

The tests drive `UserNotifier` with the real in-memory connection registry and a recording sender,
asserting that messages go out only to online users — no SignalR host or Redis required.

## License

MIT — see [LICENSE](LICENSE).
