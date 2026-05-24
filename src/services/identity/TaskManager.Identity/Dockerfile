# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/services/identity/TaskManager.Identity/TaskManager.Identity.csproj", "src/services/identity/TaskManager.Identity/"]
COPY ["Directory.Build.props", "./"]
RUN dotnet restore "src/services/identity/TaskManager.Identity/TaskManager.Identity.csproj"
COPY . .
WORKDIR /src/src/services/identity/TaskManager.Identity
RUN dotnet publish "TaskManager.Identity.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "TaskManager.Identity.dll"]
