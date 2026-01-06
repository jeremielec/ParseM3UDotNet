FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /opt
COPY ./ /opt
RUN  cd /opt/src && dotnet publish -o /app
# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt update && apt install -y ffmpeg 
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "ParseM3UNet.dll"]