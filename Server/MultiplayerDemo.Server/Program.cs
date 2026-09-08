using System.Net;
using MultiplayerDemo.Core;
using MultiplayerDemo.Server;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration.GetValue("port", 8080);
var serverName = builder.Configuration.GetValue("name", "Arena") ?? "Arena";

builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Any, port));
builder.Services.AddSingleton(new GameConfig());
builder.Services.AddSingleton<ArenaHost>();
builder.Services.AddHostedService(services => services.GetRequiredService<ArenaHost>());
builder.Services.AddHostedService<DiscoveryBeacon>();

var app = builder.Build();

var arena = app.Services.GetRequiredService<ArenaHost>();
arena.ServerName = serverName;

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });

// The Web client is served from a different origin during development, and Unity WebGL
// probes /api/info cross-origin while discovering servers.
app.Use(async (context, next) => {
	context.Response.Headers.AccessControlAllowOrigin = "*";
	context.Response.Headers.AccessControlAllowHeaders = "*";
	if ( HttpMethods.IsOptions(context.Request.Method) ) {
		context.Response.StatusCode = StatusCodes.Status204NoContent;
		return;
	}
	await next();
});

app.MapGet("/api/info", () => Results.Json(arena.BuildInfo()));

app.Map("/ws", async context => {
	if ( !context.WebSockets.IsWebSocketRequest ) {
		context.Response.StatusCode = StatusCodes.Status400BadRequest;
		return;
	}
	using var socket = await context.WebSockets.AcceptWebSocketAsync();
	var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
	await arena.HandleSocketAsync(socket, ip, context.RequestAborted);
});

// If the Web client has been built into wwwroot, serve it from the same origin as the game.
var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if ( Directory.Exists(webRoot) ) {
	app.UseDefaultFiles();
	app.UseStaticFiles();
}

app.Logger.LogInformation("Arena {Name} listening on http://<host>:{Port} (game socket at /ws)", serverName, port);

app.Run();
