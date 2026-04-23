FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Crop.Disease.API/Crop.Disease.API.csproj", "Crop.Disease.API/"]
RUN dotnet restore "Crop.Disease.API/Crop.Disease.API.csproj"
COPY . .
WORKDIR "/src/Crop.Disease.API"
RUN dotnet build "Crop.Disease.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Crop.Disease.API.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copier le modèle ONNX et les labels (placez-les dans ./model/ avant build)
# COPY model/ ./model/

ENTRYPOINT ["dotnet", "Crop.Disease.API.dll"]
