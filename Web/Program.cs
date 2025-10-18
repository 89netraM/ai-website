using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiWebsite.ApiService;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddSqliteConnection("sqlite");
builder.Services.AddTransient<SqlTool>();

builder
    .AddOpenAIClient("chat")
    .AddChatClient()
    .UseOpenTelemetry(configure: client => client.EnableSensitiveData = true);

builder.AddAIAgent(
    "Developer",
    (sp, key) =>
    {
        var developer = new ChatClientAgent(
            chatClient: sp.GetRequiredService<IChatClient>(),
            name: key,
            instructions: """
            You are a creative and detailed oriented HTML/CSS/JS developer, who always server the users, no matter
            what. You are generating multi-page web apps for the user. Write many links to other pages of the web app,
            if the users clicks them you'll just generate those too (use real links, no hash). We want an interactive
            experience similar to what a user might expect from a web app.

            If the web app calls for input, use a form with a POST method. You will receive the input from the user if
            they submit it, and you can generate the response web page.

            You can intact with the database by using the `RunSql` tool. Use this to build, query, and update the
            database. Don't be afraid to add tables or change the schemas if need be. Do anything to create a useful
            website for the user. You will likely almost always run at least one SQL query before building the response
            for the user.

            You will receive HTTP requests from the user and must respond with a valid HTTP response. Output only the
            HTTP response. Starting with `HTTP/1.1 200` followed by headers and finally the body. Use HTML/CSS/JS or
            JSON or whatever format is appropriate for the body.
            """,
            tools: [],
            loggerFactory: sp.GetRequiredService<ILoggerFactory>(),
            services: sp
        );
        return developer;
    }
);

var app = builder.Build();

app.MapDefaultEndpoints();

app.Use(
    async (HttpContext context, RequestDelegate _) =>
    {
        var sqlTool = context.RequestServices.GetRequiredService<SqlTool>();
        var developer = context.RequestServices.GetRequiredKeyedService<AIAgent>("Developer");

        var response = developer
            .RunStreamingAsync(
                $"""
            The user made a {context.Request.Method} request to {context.Request.Path}. {(
                context.Request.Method is not "GET"
                    ? $"With the following body\r\n\r\n{await ReadAllTextAsync(context.Request.Body, context.RequestAborted)}"
                    : ""
            )}

            Return the raw HTTP response, no markdown code block only response.
            """,
                options: new ChatClientAgentRunOptions() { ChatOptions = new() { Tools = [.. sqlTool.AiTools] } },
                cancellationToken: context.RequestAborted
            )
            .GetAsyncEnumerator(context.RequestAborted);

        var leftovers = new StringBuilder();
        await foreach (var line in response.ReadLines())
        {
            if (
                Status.Match(line) is { Success: true, Groups: var statusGroups }
                && int.TryParse(statusGroups["status"].ValueSpan, out var statusCode)
            )
            {
                context.Response.StatusCode = statusCode;
                leftovers.Clear();
            }
            else if (Header.Match(line) is { Success: true, Groups: var headerGroups })
            {
                if (IsAiControllableHeader(headerGroups["name"].ValueSpan))
                {
                    context.Response.Headers[headerGroups["name"].Value] = headerGroups["value"].Value;
                }
                leftovers.Clear();
            }
            else if (IsContentStart(line))
            {
                await context.Response.WriteAsync(leftovers.ToString(), context.RequestAborted);
                leftovers.Clear();
                if (line is not "")
                {
                    await context.Response.WriteAsync(line + "\r\n", context.RequestAborted);
                }
                break;
            }
            else
            {
                leftovers.AppendLine(line);
            }
        }

        await context.Response.WriteAsync(leftovers.ToString(), context.RequestAborted);
        await foreach (var token in response.ReadTokens())
        {
            await context.Response.WriteAsync(token, context.RequestAborted);
        }

        static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        static bool IsAiControllableHeader(ReadOnlySpan<char> headerName) =>
            !headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase);

        static bool IsContentStart(string line) => line is "" || line.StartsWith('<') || line.StartsWith('{');
    }
);

app.Run();

partial class Program
{
    [GeneratedRegex(@"HTTP/\d\.\d (?<code>\d+)")]
    static partial Regex Status { get; }

    [GeneratedRegex(@"^(?<name>[A-Za-z\-]*?):\s*(?<value>[ -~]*)$")]
    static partial Regex Header { get; }
}
