FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

EXPOSE 5000

COPY . .
WORKDIR /app/Crudy

RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/Crudy/out ./

ENTRYPOINT ["dotnet", "Crudy.dll"]