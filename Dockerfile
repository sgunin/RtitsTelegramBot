FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS base
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 443/tcp
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y apt-utils procps unzip \
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/* \
    && curl -sSL https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l /vsdbg    
WORKDIR /src
COPY ["RtitsTelegramBot.csproj", "./"]
RUN dotnet restore "./RtitsTelegramBot.csproj"

COPY . .
WORKDIR "/src/."
RUN if [ "${target}" = "debug" ]; then \
        dotnet build "RtitsTelegramBot.csproj" -c Debug --no-restore -o /src/build; \
    else \
        dotnet build "RtitsTelegramBot.csproj" -c Release --no-restore -o /src/build; \
    fi

FROM build AS publish
RUN if [ "${target}" != "debug" ]; then \    
        dotnet publish "RtitsTelegramBot.csproj" -c Release -o /src/publish; \
    fi

FROM base AS final
WORKDIR /app
COPY --from=publish /src/publish .
ENTRYPOINT ["dotnet", "RtitsTelegramBot.dll", "--Config:ProxyPort=3128", "--Config:ProxyHost=92.253.218.134", "--Config:Token=1107116337:AAG9eh0p72PNaL9bc-VJpLWVEFCyTpGWD8Y​"]

FROM build AS debug
WORKDIR /app
COPY --from=build /src/build .
ENTRYPOINT ["dotnet", "RtitsTelegramBot.dll", "--Config:ProxyPort=3128", "--Config:ProxyHost=92.253.218.134", "--Config:Token=1107116337:AAG9eh0p72PNaL9bc-VJpLWVEFCyTpGWD8Y​"]