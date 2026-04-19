FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# Install system dependencies required by some native libraries (e.g. Npgsql GSSAPI/Kerberos)
RUN apt-get update \
	&& apt-get install -y --no-install-recommends libgssapi-krb5-2 ca-certificates \
	&& rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["InsureZenAPI.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InsureZenAPI.dll"]