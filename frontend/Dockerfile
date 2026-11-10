FROM node:24.22.2-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Used only by the compose "test" profile (frontend-tests). It is not the last
# stage, so a plain build still produces the nginx image below.
FROM build AS test
ENV CI=true
CMD ["npm", "test"]

FROM nginx:1.30.6-alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
# The nginx image's entrypoint runs executable scripts in this folder at container start.
COPY --chmod=0755 docker/generate-config.sh /docker-entrypoint.d/40-generate-config.sh
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 3000
