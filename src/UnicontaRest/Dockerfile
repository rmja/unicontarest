FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["src/UnicontaRest/UnicontaRest.csproj", "src/UnicontaRest/"]
RUN dotnet restore "src/UnicontaRest/UnicontaRest.csproj"
COPY . .
WORKDIR "/src/src/UnicontaRest"
RUN dotnet build "UnicontaRest.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "UnicontaRest.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "UnicontaRest.dll"]