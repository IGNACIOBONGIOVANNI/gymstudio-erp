FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# For local development, we expect the code to be mounted into /app
# The hot reload relies on dotnet watch
CMD ["dotnet", "watch", "run", "--project", "src/EstudioContable.API/EstudioContable.API.csproj", "--urls", "http://0.0.0.0:5122"]
