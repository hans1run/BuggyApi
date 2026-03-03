FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Buggy.API/Buggy.API.csproj Buggy.API/
RUN dotnet restore Buggy.API/Buggy.API.csproj
COPY . .
WORKDIR /src/Buggy.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER appuser
ENTRYPOINT ["dotnet", "Buggy.API.dll"]
