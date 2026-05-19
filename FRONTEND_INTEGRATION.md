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
```

## Client To Server Hub Methods

### Public room

```ts
await connection.invoke("SendMessage", "Hello everyone");
```

### Join a private room

```ts
await connection.invoke("JoinPrivateRoom", "Bob123");
```

### Send a private message

```ts
await connection.invoke("SendPrivateMessage", "Bob123", "Hey Bob");
```

### Typing indicators

```ts
await connection.invoke("Typing");
await connection.invoke("Typing", "Bob123");
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
  "sender": "Alice123",
  "message": "hello",
  "sentAt": "2026-05-19T12:00:00Z"
}
```

### Private message

```json
{
  "sender": "Alice123",
  "receiver": "Bob123",
  "message": "hello",
  "sentAt": "2026-05-19T12:00:00Z",
  "roomKey": "dm:Alice123:Bob123"
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
- The backend sends the latest public history on hub connect, and private history when the client joins a private room.
- Use automatic reconnect on the SignalR client because cold starts on Render can delay websocket availability.
