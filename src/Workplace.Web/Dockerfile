FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project files first for layer-cached restore
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Workplace.ServiceDefaults/Workplace.ServiceDefaults.csproj src/Workplace.ServiceDefaults/
COPY src/Workplace.Web/Workplace.Web.csproj src/Workplace.Web/
RUN dotnet restore src/Workplace.Web/Workplace.Web.csproj -a $TARGETARCH

COPY src/Workplace.ServiceDefaults/ src/Workplace.ServiceDefaults/
COPY src/Workplace.Web/ src/Workplace.Web/
RUN dotnet publish src/Workplace.Web/Workplace.Web.csproj \
    -c Release -a $TARGETARCH -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workplace.Web.dll"]
