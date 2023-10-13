FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo 'Asia/Shanghai' >/etc/timezone
RUN apt-get update && apt install -y --no-install-recommends ca-certificates iproute2
WORKDIR /app
COPY //publish .
ENV enableDistribution=false
LABEL appName="P2P Listener"
LABEL authors="lyf"
ENTRYPOINT ["dotnet", "P2PNetwork.P2PListener.dll"]