# Frontend Integration Guide

This backend exposes a JWT login flow, REST endpoints for history/bootstrap data, and a SignalR hub for realtime room updates.

## Base URLs

- Local API: `http://localhost:5209`
- Hub: `http://localhost:5209/hubs/chat`
- Render API: `https://<your-render-service>.onrender.com`
- Render Hub: `https://<your-render-service>.onrender.com/hubs/chat`

## Authentication

### Login request

`POST /api/auth/login`

```json
{
  "username": "Alice123"
}
```

### Login response

```json
{
  "token": "<jwt>",
  "username": "Alice123"
}
```

Store the token in memory or secure client storage, then use it for both REST and SignalR.

## REST Endpoints

### Get global room history

`GET /api/chat/room?take=50`

Headers:

```text
Authorization: Bearer <jwt>
```

### Get private room history

`GET /api/chat/private/{username}?take=50`

### Get active users

`GET /api/chat/active-users`

### Upload image, voice, or video

`POST /api/chat/media/upload`

Headers:

```text
Authorization: Bearer <jwt>
Content-Type: multipart/form-data
```

Form fields:

- `file`: binary file
- `kind`: `image` | `voice` | `video`

Limits:

- `image`: `10MB`
- `voice`: `15MB`
- `video`: `50MB`

Example response:

```json
{
  "attachment": {
    "kind": "image",
    "url": "https://res.cloudinary.com/your-cloud-name/image/upload/v1234567890/echoroom/chat/image/sample.png",
    "fileName": "photo.png",
    "contentType": "image/png",
    "sizeBytes": 241922
  }
}
```

## SignalR Client Setup

Install:

```bash
npm install @microsoft/signalr
```

Example:

```ts
import * as signalR from "@microsoft/signalr";

const token = loginResponse.token;

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5209/hubs/chat", {
    accessTokenFactory: () => token,
  })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveMessage", (message) => {
  console.log("Public message", message);
});

connection.on("ReceivePrivateMessage", (message) => {
  console.log("Private message", message);
});

connection.on("PublicRoomHistory", (messages) => {
  console.log("Bootstrap public history", messages);
});

connection.on("PrivateRoomHistory", (messages) => {
  console.log("Bootstrap private history", messages);
});

connection.on("ActiveUsersUpdated", (users) => {
  console.log("Online users", users);
});

connection.on("UserJoined", (username) => {
  console.log(`${username} joined`);
});

connection.on("UserLeft", (username) => {
  console.log(`${username} left`);
});

connection.on("RateLimitWarning", (message) => {
  console.warn(message);
});

connection.on("SessionReplaced", (message) => {
  console.warn(message);
  connection.stop();
});

await connection.start();

const heartbeatId = window.setInterval(() => {
  if (connection.state === signalR.HubConnectionState.Connected) {
    connection.invoke("Heartbeat").catch(console.error);
  }
}, 25000);

window.addEventListener("beforeunload", () => {
  window.clearInterval(heartbeatId);
  connection.stop();
});
```

## Client To Server Hub Methods

### Public room

```ts
await connection.invoke("SendMessage", "Hello everyone");
```

### Public room rich message

```ts
await connection.invoke("SendRichMessage", {
  message: "Check this out",
  replyToMessageId: "11111111-1111-1111-1111-111111111111",
  attachment: {
    kind: "image",
    url: "https://res.cloudinary.com/your-cloud-name/image/upload/v1234567890/echoroom/chat/image/sample.png",
    fileName: "photo.png",
    contentType: "image/png",
    sizeBytes: 241922
  }
});
```

### Join a private room

```ts
await connection.invoke("JoinPrivateRoom", "Bob123");
```

### Send a private message

```ts
await connection.invoke("SendPrivateMessage", "Bob123", "Hey Bob");
```

### Send a private rich message

```ts
await connection.invoke("SendPrivateRichMessage", "Bob123", {
  message: "",
  replyToMessageId: "11111111-1111-1111-1111-111111111111",
  attachment: {
    kind: "voice",
    url: "https://res.cloudinary.com/your-cloud-name/video/upload/v1234567890/echoroom/chat/voice/voice-note.webm",
    fileName: "voice-note.webm",
    contentType: "audio/webm",
    sizeBytes: 842219
  }
});
```

### Typing indicators

```ts
await connection.invoke("Typing");
await connection.invoke("Typing", "Bob123");
```

### Presence heartbeat

```ts
await connection.invoke("Heartbeat");
```

## Server Events

- `ReceiveMessage`
- `ReceivePrivateMessage`
- `PublicRoomHistory`
- `PrivateRoomHistory`
- `UserJoined`
- `UserLeft`
- `ActiveUsersUpdated`
- `RateLimitWarning`
- `SessionReplaced`
- `UserTyping`
- `UserTypingPrivate`

## Data Shapes

### Public message

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "sender": "Alice123",
  "message": "hello",
  "attachment": null,
  "replyTo": null,
  "sentAt": "2026-05-19T12:00:00Z"
}
```

### Private message

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "sender": "Alice123",
  "receiver": "Bob123",
  "message": "hello",
  "attachment": null,
  "replyTo": null,
  "sentAt": "2026-05-19T12:00:00Z",
  "roomKey": "dm:Alice123:Bob123"
}
```

### Attachment

```json
{
  "kind": "video",
  "url": "https://res.cloudinary.com/your-cloud-name/video/upload/v1234567890/echoroom/chat/video/clip.mp4",
  "fileName": "clip.mp4",
  "contentType": "video/mp4",
  "sizeBytes": 10485760
}
```

### Reply preview

```json
{
  "messageId": "33333333-3333-3333-3333-333333333333",
  "sender": "Bob123",
  "message": "hello there",
  "attachmentKind": null
}
```

### Active user

```json
{
  "username": "Alice123"
}
```

## Frontend Notes

- Usernames must be `3-20` alphanumeric characters only.
- Only one active session per username is allowed. A new login replaces the old SignalR session.
- Public messages and private messages are sanitized and capped at `500` characters.
- Each message can contain text, a single attachment, or both.
- Supported attachment kinds are `image`, `voice`, and `video`.
- Video uploads are restricted to `50MB` maximum.
- Upload responses return a backend-issued attachment object with a hosted URL.
- The UI should send the returned attachment object back inside `SendRichMessage` or `SendPrivateRichMessage` without adding provider-specific fields.
- Replies use `replyToMessageId` and the server returns a compact reply preview in message history and realtime events.
- The backend sends the latest public history on hub connect, and private history when the client joins a private room.
- Use automatic reconnect on the SignalR client because cold starts on Render can delay websocket availability.
- Send `Heartbeat` every `25` seconds while connected so inactive users are cleared correctly.
- The backend marks users offline when no heartbeat or other activity is seen for about `60` seconds.
