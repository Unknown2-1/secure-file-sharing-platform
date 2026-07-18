FROM node:24-alpine AS deps
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY frontend/ .
RUN npm run build

FROM node:24-alpine
ENV NODE_ENV=production
WORKDIR /app
RUN addgroup -S vaultshare && adduser -S vaultshare -G vaultshare
RUN rm -rf /usr/local/lib/node_modules/npm \
    && rm -f /usr/local/bin/npm /usr/local/bin/npx
COPY --from=build --chown=vaultshare:vaultshare /app/.next/standalone ./
COPY --from=build --chown=vaultshare:vaultshare /app/.next/static ./.next/static
COPY --from=build --chown=vaultshare:vaultshare /app/public ./public
USER vaultshare
EXPOSE 3000
CMD ["node", "server.js"]
