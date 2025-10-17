using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiWebsite.ApiService;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    .AddChatClient("gpt-4o-mini")
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

app.MapGet("/sql", async ([FromQuery] string sql, [FromServices] SqlTool sqlTool) => await sqlTool.RunSql(sql));

app.Use(
    async (HttpContext context, RequestDelegate _) =>
    {
        var sqlTool = context.RequestServices.GetRequiredService<SqlTool>();
        var developer = context.RequestServices.GetRequiredKeyedService<AIAgent>("Developer");
        var response = developer.RunStreamingAsync(
            $"""
            The user made a {context.Request.Method} request to {context.Request.Path}. {(
                context.Request.Method is "POST"
                    ? $@"With the following body """"""{await ReadAllTextAsync(context.Request.Body, context.RequestAborted)}"""""""
                    : ""
            )}

            Return the raw HTML (with optional CSS and JS embedded), no markdown code block only HTML.
            """,
            options: new ChatClientAgentRunOptions()
            {
                ChatOptions = new() { Tools = [AIFunctionFactory.Create(sqlTool.RunSql)] },
            },
            cancellationToken: context.RequestAborted
        );
        var httpResponse = new StringBuilder();
        await foreach (var r in response)
        {
            httpResponse.Append(CodeBlock.Replace(r.Text, ""));
        }
        var lines = httpResponse.ToString().Split(["\n", "\r\n"], System.StringSplitOptions.TrimEntries);
        var headers = lines.TakeWhile(l => !string.IsNullOrWhiteSpace(l));
        foreach (var header in headers)
        {
            if (
                Status.Match(header) is { Success: true, Groups: var statusGroups }
                && int.TryParse(statusGroups["code"].ValueSpan, out var statusCode)
            )
            {
                context.Response.StatusCode = statusCode;
            }

            if (Header.Match(header) is { Success: true, Groups: var headerGroups })
            {
                var headerName = headerGroups["name"].Value;
                if (headerName is "Content-Length")
                {
                    continue;
                }
                context.Response.Headers[headerName] = headerGroups["value"].Value;
            }
        }
        var body = lines.SkipWhile(l => !string.IsNullOrWhiteSpace(l));
        await context.Response.WriteAsync(string.Join("\r\n", body), context.RequestAborted);

        static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
);

app.Run();

partial class Program
{
    [GeneratedRegex(@"HTTP/\d\.\d (?<code>\d+)")]
    static partial Regex Status { get; }

    [GeneratedRegex(@"^(?<name>.*?):\s*(?<value>.*)$")]
    static partial Regex Header { get; }

    [GeneratedRegex(@"^```.*?$", RegexOptions.Multiline)]
    static partial Regex CodeBlock { get; }
}
