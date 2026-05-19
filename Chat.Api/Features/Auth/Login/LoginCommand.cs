using Chat.Api.Cqrs;
using Chat.Api.DTOs;

namespace Chat.Api.Features.Auth.Login;

public sealed record LoginCommand(string Username, HttpContext HttpContext) : ICommand<LoginResponse>;
