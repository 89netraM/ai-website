FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:a5dd7352c0c058a6f847c95a1147b060e95a532444f14b34d8fa9aaa0a76702f AS build

WORKDIR /app

COPY ./AiWebsite.slnx ./global.json ./Directory.Packages.props ./Directory.Build.props ./.gitignore ./.editorconfig ./

COPY ./ServiceDefaults/ServiceDefaults.csproj ./ServiceDefaults/packages.lock.json ./ServiceDefaults/
COPY ./Web/Web.csproj ./Web/packages.lock.json ./Web/

RUN dotnet restore ./Web/Web.csproj --locked-mode

COPY ./ServiceDefaults ./ServiceDefaults
COPY ./Web ./Web

RUN dotnet build ./Web/Web.csproj --no-restore --configuration Release

RUN dotnet publish ./Web/Web.csproj --no-build --configuration Release --output /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:ad4acf911eada1bcbfd0dd9f3fa514143fba8654ca8844df9032471adc2f9bd9 AS release

WORKDIR /app

COPY --from=build /out ./

ENTRYPOINT ["./AiWebsite.Web"]
