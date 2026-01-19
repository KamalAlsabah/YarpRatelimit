# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["SampleService1/SampleService1.csproj", "SampleService1/"]
RUN dotnet restore "SampleService1/SampleService1.csproj"

# Copy source code
COPY . .
WORKDIR "/src/SampleService1"

# Build the project
RUN dotnet build "SampleService1.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SampleService1.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 5001

ENTRYPOINT ["dotnet", "SampleService1.dll"]
