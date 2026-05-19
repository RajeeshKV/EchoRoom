# Single Room Realtime Chat Application - Backend Implementation Guide

# Overview

This document contains the consolidated backend implementation architecture for a realtime single-room chat application using:

- .NET 8
- SignalR
- JWT Authentication
- PostgreSQL
- Entity Framework Core
- Hosted Background Services

The application supports:

- Temporary username-based login
- Single global chat room
- Private messaging
- Active user tracking
- Rate limiting
- Spam prevention
- IP blocking
- Automatic message cleanup
- Realtime communication

---

# High Level Architecture

```text
React Client
    |
    | HTTP + SignalR
    |
.NET 8 API + SignalR Hub
    |
    | EF Core
    |
PostgreSQL
```

---

# Backend Project Structure

```text
Chat.Api
│
├── Controllers
│   └── AuthController.cs
│
├── Hubs
│   └── ChatHub.cs
│
├── Services
│   ├── JwtService.cs
│   ├── UserConnectionService.cs
│   ├── RateLimitService.cs
│   ├── MessageService.cs
│   └── BlockedIpService.cs
│
├── BackgroundServices
│   └── MessageCleanupService.cs
│
├── Middleware
│   └── BlockedIpMiddleware.cs
│
├── Data
│   ├── AppDbContext.cs
│   └── Configurations
│
├── DTOs
│   ├── LoginRequest.cs
│   ├── LoginResponse.cs
│   ├── ChatMessageDto.cs
│   ├── PrivateMessageDto.cs
│   └── ActiveUserDto.cs
│
├── Entities
│   ├── User.cs
│   ├── Message.cs
│   ├── UserConnection.cs
│   └── BlockedIp.cs
│
├── Helpers
│   ├── IpHelper.cs
│   └── MessageSanitizer.cs
│
├── Extensions
│   └── ServiceCollectionExtensions.cs
│
└── Program.cs
```

---

# NuGet Packages

```bash
Install-Package Microsoft.AspNetCore.SignalR
Install-Package Microsoft.AspNetCore.Authentication.JwtBearer
Install-Package Microsoft.EntityFrameworkCore
Install-Package Microsoft.EntityFrameworkCore.Design
Install-Package Npgsql.EntityFrameworkCore.PostgreSQL
Install-Package System.IdentityModel.Tokens.Jwt
```

---

# Database Design

# Users Table

```sql
CREATE TABLE Users
(
    Id UUID PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    ConnectedAt TIMESTAMP NOT NULL,
    LastSeenAt TIMESTAMP NOT NULL,
    IsOnline BOOLEAN NOT NULL,
    IpHash VARCHAR(255) NOT NULL
);
```

---

# Messages Table

```sql
CREATE TABLE Messages
(
    Id UUID PRIMARY KEY,
    SenderUsername VARCHAR(50) NOT NULL,
    ReceiverUsername VARCHAR(50) NULL,
    Content TEXT NOT NULL,
    IsPrivate BOOLEAN NOT NULL,
    CreatedAt TIMESTAMP NOT NULL
);
```

---

# BlockedIps Table

```sql
CREATE TABLE BlockedIps
(
    Id UUID PRIMARY KEY,
    IpHash VARCHAR(255) NOT NULL,
    BlockedUntil TIMESTAMP NOT NULL,
    Reason TEXT NOT NULL
);
```

---

# UserConnections Table

```sql
CREATE TABLE UserConnections
(
    Id UUID PRIMARY KEY,
    Username VARCHAR(50) NOT NULL,
    ConnectionId VARCHAR(255) NOT NULL,
    ConnectedAt TIMESTAMP NOT NULL
);
```

---

# Entity Models

# User.cs

```csharp
public class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = default!;

    public DateTime ConnectedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public bool IsOnline { get; set; }

    public string IpHash { get; set; } = default!;
}
```

---

# Message.cs

```csharp
public class Message
{
    public Guid Id { get; set; }

    public string SenderUsername { get; set; } = default!;

    public string? ReceiverUsername { get; set; }

    public string Content { get; set; } = default!;

    public bool IsPrivate { get; set; }

    public DateTime CreatedAt { get; set; }
}
```

---

# BlockedIp.cs

```csharp
public class BlockedIp
{
    public Guid Id { get; set; }

    public string IpHash { get; set; } = default!;

    public DateTime BlockedUntil { get; set; }

    public string Reason { get; set; } = default!;
}
```

---

# UserConnection.cs

```csharp
public class UserConnection
{
    public Guid Id { get; set; }

    public string Username { get; set; } = default!;

    public string ConnectionId { get; set; } = default!;

    public DateTime ConnectedAt { get; set; }
}
```

---

# JWT Authentication Flow

# Login Flow

1. User enters username
2. Backend validates username uniqueness
3. JWT token issued
4. Client connects to SignalR using JWT
5. User becomes active

---

# Username Rules

- Minimum 3 characters
- Maximum 20 characters
- Alphanumeric only
- No duplicate active usernames
- Case insensitive uniqueness

---

# Login Request DTO

```csharp
public class LoginRequest
{
    public string Username { get; set; } = default!;
}
```

---

# Login Response DTO

```csharp
public class LoginResponse
{
    public string Token { get; set; } = default!;

    public string Username { get; set; } = default!;
}
```

---

# JWT Service

```csharp
public interface IJwtService
{
    string GenerateToken(string username);
}
```

---

# JWT Claims

```text
sub = username
unique_name = username
jti = guid
```

---

# Auth Controller

# Endpoint

```text
POST /api/auth/login
```

# Validation Steps

```text
1. Validate username format
2. Check blocked IP
3. Check duplicate active user
4. Generate JWT
5. Return token
```

---

# SignalR Configuration

# Hub Route

```text
/hubs/chat
```

---

# SignalR Authentication

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];

        var path = context.HttpContext.Request.Path;

        if (!string.IsNullOrEmpty(accessToken)
            && path.StartsWithSegments("/hubs/chat"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
};
```

---

# ChatHub.cs

# Responsibilities

- Handle user connections
- Broadcast public messages
- Handle private messages
- Maintain active users
- Enforce rate limits
- Notify joins/leaves

---

# OnConnectedAsync

```text
1. Extract username from JWT
2. Store connection mapping
3. Mark user online
4. Broadcast join event
5. Send active user list
```

---

# OnDisconnectedAsync

```text
1. Remove connection mapping
2. Mark user offline
3. Broadcast leave event
4. Update active users
```

---

# Public Message Flow

# Client

```text
SendPublicMessage(message)
```

# Backend Validation

```text
1. Validate rate limit
2. Validate IP block
3. Sanitize message
4. Save message
5. Broadcast to all users
```

---

# Private Message Flow

# Client

```text
SendPrivateMessage(receiver, message)
```

# Backend

```text
1. Validate receiver exists
2. Validate sender not blocked
3. Save message
4. Send to receiver connection
5. Echo back to sender
```

---

# Active User Tracking

# Recommended Approach

Use:

```text
ConcurrentDictionary<string, string>
```

Structure:

```text
Username -> ConnectionId
```

---

# Rate Limiting

# Requirement

Users cannot:

```text
Send more than 2 messages per second repeatedly
```

---

# Recommended Implementation

Instead of instant blocking:

# Stage 1

Warning

# Stage 2

30 second mute

# Stage 3

5 minute mute

# Stage 4

Temporary IP block

---

# Rate Limit Tracking

Use:

```text
ConcurrentDictionary<string, List<DateTime>>
```

Structure:

```text
IpAddress -> Message Timestamps
```

---

# Suggested Limits

```text
5 messages within 5 seconds
```

Then mute/block.

---

# IP Blocking

# Middleware Flow

```text
1. Read IP address
2. Hash IP
3. Check BlockedIps table
4. Reject request if blocked
```

---

# Important Note

Do NOT permanently block IPs.

Recommended:

```text
15 minute temporary blocks
```

---

# Message Sanitization

# Required Protections

Prevent:

- XSS
- HTML injection
- Script injection
- Unicode abuse

---

# Recommended Rules

```text
1. Strip HTML tags
2. Trim excessive spaces
3. Limit message length
4. Remove dangerous scripts
```

---

# Message Limits

```text
Maximum Length: 500 chars
```

---

# Hosted Background Service

# Purpose

Delete old messages automatically.

---

# Recommended Frequency

Every hour.

Not once daily.

---

# Cleanup Logic

```sql
DELETE FROM Messages
WHERE CreatedAt < NOW() - INTERVAL '24 HOURS';
```

---

# Cleanup Service

```csharp
public class MessageCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delete messages older than 24 hours

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

---

# DTOs

# ChatMessageDto

```csharp
public class ChatMessageDto
{
    public string Sender { get; set; } = default!;

    public string Message { get; set; } = default!;

    public DateTime SentAt { get; set; }
}
```

---

# PrivateMessageDto

```csharp
public class PrivateMessageDto
{
    public string Sender { get; set; } = default!;

    public string Receiver { get; set; } = default!;

    public string Message { get; set; } = default!;

    public DateTime SentAt { get; set; }
}
```

---

# ActiveUserDto

```csharp
public class ActiveUserDto
{
    public string Username { get; set; } = default!;
}
```

---

# Recommended SignalR Events

# Server -> Client

```text
ReceiveMessage
ReceivePrivateMessage
UserJoined
UserLeft
ActiveUsersUpdated
RateLimitWarning
UserMuted
```

---

# Client -> Server

```text
SendMessage
SendPrivateMessage
Typing
```

---

# Typing Indicator

Optional but recommended.

Implementation:

```text
UserTyping(username)
```

Do NOT persist typing events.

---

# Important Edge Cases

# Browser Close

Must auto-disconnect user.

---

# Duplicate Tabs

Recommended:

```text
Allow only one active session per username.
```

Disconnect previous session.

---

# Server Restart

Users should reconnect automatically.

Frontend should:

```text
Retry SignalR connection.
```

---

# Cold Starts On Render

Render free tier sleeps inactive services.

Effects:

- delayed reconnect
- websocket startup delay

This is expected.

---

# Program.cs Configuration

# Required Services

```text
AddControllers
AddSignalR
AddAuthentication
AddAuthorization
AddDbContext
AddHostedService
```

---

# CORS

Allow:

```text
- frontend origin
- credentials
- websockets
```

---

# Security Recommendations

# Required

- HTTPS only
- JWT expiration
- Message sanitization
- Rate limiting
- Temporary IP blocking

---

# Avoid

- storing passwords
- storing sensitive user data
- long-lived tokens
- permanent IP bans

---

# Suggested JWT Lifetime

```text
12 hours
```

---

# Deployment Notes

# Backend Hosting

Recommended:

- Render
- Railway

---

# PostgreSQL Hosting

Recommended:

- Render PostgreSQL
- Neon

---

# Environment Variables

```text
JWT_SECRET
DB_CONNECTION
JWT_ISSUER
JWT_AUDIENCE
```

---

# Recommended Future Enhancements

# V2 Features

- emoji reactions
- reply-to messages
- mention highlighting
- mute user
- admin moderation
- image upload
- profanity filtering

---

# Features To Avoid Initially

Do NOT add initially:

- microservices
- Redis
- Kafka
- Kubernetes
- video chat
- voice calls
- multiple rooms
- AI moderation

These dramatically increase complexity.

---

# Final Notes

This architecture is intentionally:

- simple
- realtime
- scalable enough for small/medium usage
- deployable on free tiers
- portfolio friendly
- maintainable

The main engineering challenges are:

- connection lifecycle handling
- state consistency
- spam prevention
- reconnect handling
- realtime synchronization

Not sending chat messages themselves.

