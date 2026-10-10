FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (leverages Docker layer caching).
COPY Phonebook.Api.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet build Phonebook.Api.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish Phonebook.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Phonebook.Api.dll"]
