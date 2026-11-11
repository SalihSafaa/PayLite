# Build context is the REPO ROOT (see docker-compose.yml), because this service
# also needs src/Shared.Contracts and Directory.Build.props.

# Stage 1: build with the full SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first, so the (slow) restore layer is cached
# and only re-runs when the dependencies change, not on every code edit
COPY Directory.Build.props dotnet-tools.json ./
COPY src/Shared.Contracts/Shared.Contracts.csproj src/Shared.Contracts/
COPY src/Activity.Service/Activity.Service.csproj src/Activity.Service/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Activity.Service/Activity.Service.csproj

COPY src/Shared.Contracts/ src/Shared.Contracts/
COPY src/Activity.Service/ src/Activity.Service/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Activity.Service/Activity.Service.csproj -c Release -o /app/publish --no-restore

# Stage 2: run on the small ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The aspnet image ships a non-root "app" user: don't run the API as root.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Activity.Service.dll"]

# Stage 3: build the EF Core migration bundle (one self-contained executable)
FROM build AS bundle
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet tool restore
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet tool run dotnet-ef migrations bundle --project src/Activity.Service/Activity.Service.csproj -o /app/efbundle --force

# Stage 4: tiny image that only runs the bundle (the "activity-migrator" compose service)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
ENTRYPOINT ["./efbundle"]
