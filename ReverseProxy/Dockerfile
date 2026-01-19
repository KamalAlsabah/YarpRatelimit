# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["ReverseProxy/ReverseProxy.csproj", "ReverseProxy/"]
RUN dotnet restore "ReverseProxy/ReverseProxy.csproj"

# Copy source code
COPY . .
WORKDIR "/src/ReverseProxy"

# Build the project
RUN dotnet build "ReverseProxy.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ReverseProxy.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "ReverseProxy.dll"]
