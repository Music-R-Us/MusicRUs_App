# Builds the MusicRUs Blazor WebAssembly frontend and serves it with nginx.
# Used by the demo (MusicRUs_API/demo/docker-compose.yml) and can also be used when hosting.

# Node is only needed to build Tailwind CSS during the .NET build
FROM node:22-bookworm-slim AS node

# Build stage: .NET SDK + Node
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=node /usr/local/ /usr/local/
WORKDIR /src

# Install npm packages and NuGet packages first, so they are cached between builds
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY MusicRUs.csproj ./
RUN dotnet restore MusicRUs.csproj

COPY . .

# global.json pins an exact SDK patch for local development. The container uses the SDK in the image.
RUN rm -f global.json

# The browser calls the API on this address. Set it with --build-arg API_BASE_URL=...
ARG API_BASE_URL=http://localhost:8081/
RUN sed -E -i "s#(\"BaseUrl\"[[:space:]]*:[[:space:]]*\")[^\"]*#\1${API_BASE_URL}#" wwwroot/appsettings.json \
    && grep -q "${API_BASE_URL}" wwwroot/appsettings.json

RUN dotnet publish MusicRUs.csproj -c Release -o /app/publish --no-restore

# Runtime stage: static files served by nginx
FROM nginx:1.27-alpine AS final
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
