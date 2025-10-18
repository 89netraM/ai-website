# AI Website

This is an entirely AI generated webserver. Or rather, the webserver is
entirely written by a human, but requests are entirely handled by AI. When a
request comes in the HTTP request is sent to an LLM that, after optional SQL
database calls, writes the HTTP response.

## Running

1. You need the dotnet SDK with at least .NET 9.0.
2. Setup the `ConnectionStrings:chat`  
   Format `Endpoint=<v1 API endpoint>;Model=<model>;Key=<API key>`  
   Either as environment variable `ConnectionStrings__chat` or as dotnet
   user-secret in the `./Web/` project.
3. Start the project with `dotnet run` or `dotnet watch` in `./AppHost/`.
