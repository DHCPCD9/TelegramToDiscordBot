FROM mcr.microsoft.com/dotnet/sdk AS build-env

WORKDIR /app

COPY . ./

RUN dotnet restore
RUN dotnet publish DiscordToTelegramBot/DiscordToTelegramBot.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app/out .


ENTRYPOINT [ "dotnet", "DiscordToTelegramBot.dll" ]