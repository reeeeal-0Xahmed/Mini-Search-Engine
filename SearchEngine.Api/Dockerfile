# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY SearchEngine.Api.csproj .
RUN dotnet restore


COPY . .
RUN dotnet publish -c Release -o /app/publish

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Render بيستخدم PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SearchEngine.Api.dll"]
