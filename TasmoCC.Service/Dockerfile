#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
RUN curl -sL https://deb.nodesource.com/setup_12.x | bash -
RUN apt-get install -y nodejs
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
RUN curl -sL https://deb.nodesource.com/setup_12.x | bash -
RUN apt-get install -y nodejs
WORKDIR /src
COPY ["TasmoCC.Service/TasmoCC.Service.csproj", "TasmoCC.Service/"]
COPY ["TasmoCC.Mqtt/TasmoCC.Mqtt.csproj", "TasmoCC.Mqtt/"]
COPY ["TasmoCC.Tasmota/TasmoCC.Tasmota.csproj", "TasmoCC.Tasmota/"]
COPY ["TasmoCC.MongoDb/TasmoCC.MongoDb.csproj", "TasmoCC.MongoDb/"]
RUN dotnet restore "TasmoCC.Service/TasmoCC.Service.csproj"
COPY . .
WORKDIR "/src/TasmoCC.Service"
RUN dotnet build "TasmoCC.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TasmoCC.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TasmoCC.Service.dll"]
