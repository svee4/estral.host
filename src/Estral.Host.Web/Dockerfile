FROM mcr.microsoft.com/dotnet/sdk:8.0 as sdk

WORKDIR /app

COPY . .
COPY --from=projectroot Directory.Build.props .

RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as runtime

WORKDIR /app
COPY --from=sdk /app/out .

RUN useradd -ms /bin/bash dockeruser

RUN mkdir /volume
RUN chown -R dockeruser:dockeruser /volume

USER dockeruser:dockeruser

ENTRYPOINT ["dotnet", "Estral.Host.Web.dll"]
