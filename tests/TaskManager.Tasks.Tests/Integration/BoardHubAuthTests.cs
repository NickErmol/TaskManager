using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Tasks.Tests.Integration;

[Collection("tasks-api")]
public class BoardHubAuthTests(TasksWebAppFactory factory)
{
    private string MintToken(Guid userId)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var secret = config["JWT_SECRET"] ?? config["Jwt:SecretKey"]!;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "TaskManager.Identity",
            audience: config["Jwt:Audience"] ?? "TaskManager",
            claims: new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HubConnection HubFor(Guid userId)
    {
        var server = factory.Server; // in-memory TestServer
        return new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/board", o =>
            {
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(MintToken(userId));
            })
            .Build();
    }

    [Fact]
    public async Task JoinBoard_AsMember_Succeeds_AsNonMember_Throws()
    {
        var (boardId, owner, _, _) = await factory.SeedBoardAsync();

        var memberConn = HubFor(owner);
        await memberConn.StartAsync();
        await memberConn.InvokeAsync("JoinBoard", boardId); // member: completes without throwing
        await memberConn.StopAsync();

        var strangerConn = HubFor(Guid.NewGuid());
        await strangerConn.StartAsync();
        var act = async () => await strangerConn.InvokeAsync("JoinBoard", boardId);
        await act.Should().ThrowAsync<HubException>().WithMessage("*not a board member*");
        await strangerConn.StopAsync();
    }
}
