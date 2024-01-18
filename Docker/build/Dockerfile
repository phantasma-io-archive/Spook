FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# required for cloning with ssh
RUN apt update
RUN apt install -y ssh

WORKDIR /source

# setup ssh id_rsa keys to clone private repo
COPY id_rsa /root/.ssh/id_rsa
COPY id_rsa.pub /root/.ssh/id_rsa.pub
COPY ssh-config /root/.ssh/config
RUN chmod 600 /root/.ssh/id_rsa
RUN chmod 600 /root/.ssh/id_rsa.pub
# add github to known hosts
RUN ssh-keyscan -t rsa github.com >> ~/.ssh/known_hosts
# clone explorer backend and checkout out single branch
ARG BUILD_BRANCH
RUN git clone -b $BUILD_BRANCH --single-branch git@github.com:phantasma-io/PhantasmaChain.git
RUN git clone -b $BUILD_BRANCH --single-branch git@github.com:phantasma-io/Spook.git

# create new source directory
WORKDIR /source/Spook

# restore project dependencies
RUN dotnet restore

# build project
RUN dotnet build

FROM build AS publish

COPY --from=build /source/Spook/Spook/config_readonly.json /source/Spook/Spook/bin/Debug/net7.0/config.json
RUN mkdir /source/Spook/Spook/bin/Debug/net7.0/spook.log

FROM base AS final

# switch into publish folder
WORKDIR /app

COPY --from=publish /source/Spook/Spook/bin/Debug/net7.0 .
