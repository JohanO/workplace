FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project files first for layer-cached restore
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Workplace.ServiceDefaults/Workplace.ServiceDefaults.csproj src/Workplace.ServiceDefaults/
COPY src/Workplace.ApiService/Workplace.ApiService.csproj src/Workplace.ApiService/
RUN dotnet restore src/Workplace.ApiService/Workplace.ApiService.csproj -a $TARGETARCH

COPY src/Workplace.ServiceDefaults/ src/Workplace.ServiceDefaults/
COPY src/Workplace.ApiService/ src/Workplace.ApiService/
RUN dotnet publish src/Workplace.ApiService/Workplace.ApiService.csproj \
    -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workplace.ApiService.dll"]
