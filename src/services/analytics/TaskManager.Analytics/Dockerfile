# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/services/analytics/TaskManager.Analytics/TaskManager.Analytics.csproj", "src/services/analytics/TaskManager.Analytics/"]
COPY ["src/shared/TaskManager.Contracts/TaskManager.Contracts.csproj", "src/shared/TaskManager.Contracts/"]
COPY ["Directory.Build.props", "./"]
RUN dotnet restore "src/services/analytics/TaskManager.Analytics/TaskManager.Analytics.csproj"
COPY . .
WORKDIR /src/src/services/analytics/TaskManager.Analytics
RUN dotnet publish "TaskManager.Analytics.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "TaskManager.Analytics.dll"]
