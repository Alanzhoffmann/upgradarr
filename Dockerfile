FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Huntarr.Net.Clients/Huntarr.Net.Clients.csproj Huntarr.Net.Clients/
COPY Huntarr.Net.Api/Huntarr.Net.Api.csproj Huntarr.Net.Api/
COPY Huntarr.Net.Migrations/Huntarr.Net.Migrations.csproj Huntarr.Net.Migrations/

# Restore migrations (no RID needed - not AOT)
RUN dotnet restore Huntarr.Net.Migrations/Huntarr.Net.Migrations.csproj
# Restore API with linux-x64 RID for AOT
RUN dotnet restore Huntarr.Net.Api/Huntarr.Net.Api.csproj -r linux-x64

COPY . .

# Publish migrations runner as regular dotnet app
RUN dotnet publish Huntarr.Net.Migrations/Huntarr.Net.Migrations.csproj -c Release -o /app/migrations --no-restore

# Publish API as AOT native binary
RUN dotnet publish Huntarr.Net.Api/Huntarr.Net.Api.csproj -c Release -r linux-x64 -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/migrations ./migrations/
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh

ENTRYPOINT ["./entrypoint.sh"]
