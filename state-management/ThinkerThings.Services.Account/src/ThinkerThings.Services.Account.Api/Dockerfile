FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["src/ThinkerThings.Services.Account.Api/ThinkerThings.Services.Account.Api.csproj", "src/ThinkerThings.Services.Account.Api/"]
RUN dotnet restore "src/ThinkerThings.Services.Account.Api/ThinkerThings.Services.Account.Api.csproj"
COPY . .
WORKDIR "/src/src/ThinkerThings.Services.Account.Api"
RUN dotnet build "ThinkerThings.Services.Account.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ThinkerThings.Services.Account.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ThinkerThings.Services.Account.Api.dll"]