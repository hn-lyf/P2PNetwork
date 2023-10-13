FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo 'Asia/Shanghai' >/etc/timezone
RUN apt-get update && apt install -y --no-install-recommends ca-certificates iproute2
WORKDIR /app
COPY //publish .
ENV enableDistribution=false
LABEL appName="Iot Hub Data Handle Service"
LABEL authors="qcxt.com"
ENTRYPOINT ["dotnet", "P2PNetwork.HostClient.dll"]