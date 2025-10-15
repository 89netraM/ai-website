using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
            cancellationToken: context.RequestAborted
        );
        var httpResponse = new StringBuilder();
        await foreach (var r in response)
        {
            httpResponse.Append(CodeBlock.Replace(r.Text, ""));
        }

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
    [GeneratedRegex(@"^```.*?$", RegexOptions.Multiline)]
    static partial Regex CodeBlock { get; }
}
