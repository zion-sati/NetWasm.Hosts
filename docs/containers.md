# NetWasm services in a container

Google Cloud Run, Azure Container Apps, AWS Fargate and other container platforms run no WebAssembly themselves, but
they run a container with Wasmtime in it. A service made from the `netwasm-serverless` template is a standard
`wasi:http` component, and `wasmtime serve` serves it unchanged.

```sh
dotnet new install NetWasm.Templates
dotnet new netwasm-serverless -n Hello
cd Hello
dotnet publish -c Release -o publish
```

Save the [Dockerfile](../eng/verify-container/Dockerfile) beside the project, then build and run it:

```sh
docker build --build-arg SERVICE=Hello -t hello .
docker run --rm -p 8080:8080 hello
curl http://localhost:8080/hello
```

The image downloads a pinned Wasmtime release and checks its SHA-256, then serves the component on `$PORT`, 8080
unless the platform sets it. Cloud Run sets `PORT`; on Container Apps and Fargate, point the target port or port
mapping at 8080. The service runs as `nobody`, and `HttpClient` requests go out through Wasmtime's `wasi:http`.

This guide is checked end to end by `eng/verify-container.sh`, which publishes the template's service with NetWasm's
packages from NuGet.org, builds the Dockerfile and requests the container on the default port and on one set
through `PORT`.
