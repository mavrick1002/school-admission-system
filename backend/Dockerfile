FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS build
WORKDIR /src

COPY src/Admission.Api/Admission.Api.csproj src/Admission.Api/
RUN dotnet restore src/Admission.Api/Admission.Api.csproj

COPY src/ src/
RUN dotnet publish src/Admission.Api/Admission.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    -p:UseAppHost=false

# Built only for the "test" compose profile. The runtime image below stays the default target.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS test
WORKDIR /src
COPY tests/Admission.Tests/Admission.Tests.csproj tests/Admission.Tests/
RUN dotnet restore tests/Admission.Tests/Admission.Tests.csproj
COPY tests/ tests/
RUN dotnet build tests/Admission.Tests/Admission.Tests.csproj --configuration Release --no-restore
ENTRYPOINT ["dotnet", "test", "tests/Admission.Tests/Admission.Tests.csproj", "--configuration", "Release", "--no-build", "--logger", "console;verbosity=normal"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24
WORKDIR /app
# Npgsql's GSS probe logs a load error at startup when this library is missing.
RUN apk add --no-cache krb5-libs
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Admission.Api.dll"]
