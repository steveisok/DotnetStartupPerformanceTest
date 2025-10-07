ARG TARGET_FRAMEWORK

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGET_FRAMEWORK
WORKDIR /src

RUN apt-get update && apt-get install -y unzip

COPY *.csproj .
COPY ["packs/*.nupkg", "/var/packs/"]
RUN cd /var/packs && \
    for nupkg in *.nupkg; do \
        if [ -f "$nupkg" ]; then \
            echo "Extracting $nupkg" && \
            unzip "$nupkg" && \
            rm "$nupkg"; \
        fi; \
    done
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -f ${TARGET_FRAMEWORK} --self-contained true -r linux-x64 -o /var/task

FROM public.ecr.aws/lambda/provided:al2023
WORKDIR /var/task
COPY --from=build /var/task .

ENTRYPOINT ["/usr/local/bin/aws-lambda-rie", "./bootstrap"]
