FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Chainborn.Licensing.sln", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "src/sample-app/Chainborn.Licensing.SampleApp/Chainborn.Licensing.SampleApp.csproj"
RUN dotnet publish "src/sample-app/Chainborn.Licensing.SampleApp/Chainborn.Licensing.SampleApp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Chainborn.Licensing.SampleApp.dll"]
