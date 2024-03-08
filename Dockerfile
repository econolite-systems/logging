FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ENV SolutionDir /src
WORKDIR /src
COPY . .
# Generate model files
RUN dotnet new tool-manifest
RUN dotnet tool install Mapster.Tool
RUN dotnet build ./device-manager/Models.DeviceManager/ -c Release -o /app/build
RUN dotnet build ./ess/Models.Ess/ -c Release -o /app/build
RUN dotnet build ./pavement-condition/Models.PavementCondition/ -c Release -o /app/build
WORKDIR "/src/Worker.Logging/"
RUN dotnet build "Worker.Logging.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Worker.Logging.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Worker.Logging.dll"]
