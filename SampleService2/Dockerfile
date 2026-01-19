# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["SampleService2/SampleService2.csproj", "SampleService2/"]
RUN dotnet restore "SampleService2/SampleService2.csproj"

# Copy source code
COPY . .
WORKDIR "/src/SampleService2"

# Build the project
RUN dotnet build "SampleService2.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SampleService2.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 5002

ENTRYPOINT ["dotnet", "SampleService2.dll"]
