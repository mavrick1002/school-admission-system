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

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24
WORKDIR /app
# Npgsql's GSS probe logs a load error at startup when this library is missing.
RUN apk add --no-cache krb5-libs
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Admission.Api.dll"]
