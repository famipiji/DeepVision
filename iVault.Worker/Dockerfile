# 1. BUILD STAGE - Using .NET 10 SDK to match your project target
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy BOTH project files so the references between them work
COPY ["iVault.Worker/iVault.Worker.csproj", "iVault.Worker/"]
COPY ["iVault.Api/iVault.Api.csproj", "iVault.Api/"]

# Restore specifically for the Worker
RUN dotnet restore "iVault.Worker/iVault.Worker.csproj"

# Copy the entire source tree
COPY . .

# Move into the Worker directory and build
WORKDIR "/src/iVault.Worker"
RUN dotnet build "iVault.Worker.csproj" -c Release -o /app/build

RUN apt-get update && apt-get install -y libgssapi-krb5-2

# 2. PUBLISH STAGE
FROM build AS publish
# Added /p:ErrorOnDuplicatePublishOutputFiles=false
RUN dotnet publish "iVault.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:ErrorOnDuplicatePublishOutputFiles=false

# 3. FINAL RUNTIME STAGE 
# Change 'runtime' to 'aspnet' to include the AspNetCore.App framework
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "iVault.Worker.dll"]