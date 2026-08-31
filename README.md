<p align="center">
  <img src="icon.png" alt="GM.RealTime Samples" width="140" height="140" />
</p>

# GM.RealTime Samples

[![CI](https://github.com/gmetskhvarishvili/GM.RealTime.Samples/actions/workflows/ci.yml/badge.svg)](https://github.com/gmetskhvarishvili/GM.RealTime.Samples/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A **multi-instance, message-driven** real-time sample built on
**[GM.RealTime](https://www.nuget.org/packages/GM.RealTime)**. It shows the full pipeline: a message
is published to **RabbitMQ (GM.Messaging)**, a **consumer worker** saves it to an **inbox**, and a
separate **sender worker** delivers it to the right browser over SignalR — across processes, via the
**Redis backplane**. Targets **.NET 10**.

## The pipeline

```
POST /api/v1/queue/{userId}  (GM.RealTime.Sample.API, GM.Messaging producer)
        │  publish RealTimeMessageQueuedIntegrationEvent
        ▼
   RabbitMQ  (exchange gm.events, routing key realtime.message.queued)
        │
        ▼
GM.RealTime.Sample.Consumer.Worker   (Wolverine handler → IInboxProcessor.IngestAsync)
        │  saves an unprocessed row
        ▼
   Inbox table  (Postgres, GM.Messaging inbox schema)
        │
        ▼
GM.RealTime.Sample.Sender.Worker     (polls inbox → IRealTimeSender.SendToUserAsync)
        │  delivers if the user is online, marks processed
        ▼
   SignalR Redis backplane → the API instance holding the user's connection → browser
```

Because the sender worker holds **no connections of its own**, delivery only reaches the browser
thanks to two shared pieces of GM.RealTime: the **Redis-backed connection registry** (it looks up
where the user is connected) and the **SignalR Redis backplane** (it routes the message there). This
is the multi-instance story end to end.

## Projects

```
GM.RealTime.Sample.API/              # SignalR hub (JWT), presence, /api/v1/queue producer, /notify, dev token
GM.RealTime.Sample.Domain/           # RealTimeMessageQueuedIntegrationEvent (GM.Messaging)
GM.RealTime.Sample.Persistence/      # InboxDbContext + InboxStore (GM.Messaging inbox over EF/Npgsql)
GM.RealTime.Sample.Consumer.Worker/  # Wolverine consumer → IngestAsync into the inbox
GM.RealTime.Sample.Sender.Worker/    # polls the inbox → IRealTimeSender (Redis backplane)
tests/GM.RealTime.Sample.Tests/      # xUnit: presence-aware notify + inbox dispatch (no infra needed)
```

## Endpoints (API)

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/v1/dev/token/{userId}` | Dev-only: mint a JWT for `userId` to connect the SignalR client |
| `POST` | `/api/v1/queue/{userId}` | Publish a message `{ title, body }` into the pipeline (RabbitMQ → inbox → SignalR) |
| `POST` | `/api/v1/notify/{userId}` | Direct presence-aware push (bypasses the queue) — requires auth |
| `GET` | `/api/v1/presence/{userId}` | Whether the user is online and how many connections they have |
| (hub) | `/hubs/realtime` | The SignalR hub (requires a valid JWT) |
| `GET` | `/health/live` | Liveness probe (no downstream checks) |
| `GET` | `/health/ready` | Readiness probe |

## Running the whole thing

Needs **Redis**, **RabbitMQ**, and **PostgreSQL**:

```bash
docker run -p 6379:6379 -d redis
docker run -p 5672:5672 -p 15672:15672 -d rabbitmq:management
docker run -e POSTGRES_PASSWORD=123456 -p 5432:5432 -d postgres
```

Then, in three terminals:

```bash
dotnet run --project GM.RealTime.Sample.API
dotnet run --project GM.RealTime.Sample.Consumer.Worker
dotnet run --project GM.RealTime.Sample.Sender.Worker
```

Connect a browser client (see the snippet below) as, say, user
`11111111-1111-1111-1111-111111111111`, then:

```bash
curl -X POST http://localhost:5xxx/api/v1/queue/11111111-1111-1111-1111-111111111111 \
  -H "Content-Type: application/json" -d '{"title":"Hello","body":"from the pipeline"}'
```

The message travels API → RabbitMQ → consumer → inbox → sender → your browser. Run **two** API
instances on different ports and the backplane still delivers to whichever one holds the connection.

### Browser client (with reconnect)

```js
import * as signalR from "@microsoft/signalr";

const { token } = await (await fetch("/api/v1/dev/token/11111111-1111-1111-1111-111111111111", { method: "POST" })).json();

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/realtime", { accessTokenFactory: () => token })
  .withAutomaticReconnect()
  .withStatefulReconnect()   // resume + replay across brief drops
  .build();

connection.on("ReceiveMessage", m => console.log(m.event, m.payload));
await connection.start();
```

## Testing

```bash
dotnet test
```

The tests drive the **inbox dispatcher** (with a recording sender) and the **presence-aware notifier**
(with the real in-memory registry) — no Redis, RabbitMQ, or Postgres required.

## License

MIT — see [LICENSE](LICENSE).
