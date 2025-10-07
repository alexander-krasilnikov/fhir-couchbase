# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY FhirCouchbaseDemo.sln ./
COPY src/FhirCouchbaseDemo.Web/FhirCouchbaseDemo.Web.csproj src/FhirCouchbaseDemo.Web/

RUN dotnet restore FhirCouchbaseDemo.sln

COPY . ./
RUN dotnet publish src/FhirCouchbaseDemo.Web/FhirCouchbaseDemo.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FhirCouchbaseDemo.Web.dll"]
