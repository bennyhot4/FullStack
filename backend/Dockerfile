# Backend API image. Build from the backend/ folder:  docker build -t houroffice-api backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY HourOffice.Api/HourOffice.Api.csproj HourOffice.Api/
RUN dotnet restore HourOffice.Api/HourOffice.Api.csproj
COPY HourOffice.Api/ HourOffice.Api/
RUN dotnet publish HourOffice.Api -c Release -o /app --no-restore -p:PublishReadyToRun=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "HourOffice.Api.dll"]
