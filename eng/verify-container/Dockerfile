# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT
#
# A NetWasm HTTP service in a container: Wasmtime serves the published component on $PORT (8080 unless the platform
# sets it, as Cloud Run does). Build it from the service project's directory after dotnet publish -c Release -o publish,
# with --build-arg SERVICE=<the project's assembly name>.
FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc AS wasmtime
ARG WASMTIME=50.0.1
ARG WASMTIME_SHA256_X86_64=685466455dd42a14774a86c36c8f2e45cbb9bd6d92a850139315df60a75498a5
ARG WASMTIME_SHA256_AARCH64=d737e077416d78fbfb0a008229ec27d6a0fd5a4924874b8c38b293c850cf4094
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl xz-utils \
 && arch=$(uname -m) \
 && case "$arch" in x86_64) sha=$WASMTIME_SHA256_X86_64 ;; aarch64) sha=$WASMTIME_SHA256_AARCH64 ;; *) exit 1 ;; esac \
 && curl -fsSL -o /tmp/wasmtime.tar.xz "https://github.com/bytecodealliance/wasmtime/releases/download/v$WASMTIME/wasmtime-v$WASMTIME-$arch-linux.tar.xz" \
 && echo "$sha  /tmp/wasmtime.tar.xz" | sha256sum -c - \
 && tar -xJf /tmp/wasmtime.tar.xz -C /usr/local/bin --strip-components=1 "wasmtime-v$WASMTIME-$arch-linux/wasmtime"

FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc
ARG SERVICE=Hello
COPY --from=wasmtime /usr/local/bin/wasmtime /usr/local/bin/wasmtime
COPY publish/${SERVICE}.wasm /srv/service.wasm
ENV PORT=8080
EXPOSE 8080
USER nobody
CMD ["sh", "-c", "exec wasmtime serve -C cache=n -Sp3 -Scli --addr 0.0.0.0:$PORT /srv/service.wasm"]
