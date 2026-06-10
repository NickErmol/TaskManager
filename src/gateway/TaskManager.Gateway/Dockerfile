# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/gateway/TaskManager.Gateway/TaskManager.Gateway.csproj", "src/gateway/TaskManager.Gateway/"]
COPY ["Directory.Build.props", "./"]
RUN dotnet restore "src/gateway/TaskManager.Gateway/TaskManager.Gateway.csproj"
COPY . .
WORKDIR /src/src/gateway/TaskManager.Gateway
RUN dotnet publish "TaskManager.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "TaskManager.Gateway.dll"]
