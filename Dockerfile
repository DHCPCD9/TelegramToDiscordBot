FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder

WORKDIR /app
COPY . .

RUN dotnet restore
RUN dotnet publish --no-restore -c Release -o .

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app

COPY --from=builder /app .

ENTRYPOINT [ "dotnet DiscordToTelegramBot.dll" ]