# NetWasm on Azure Functions

Azure Functions runs Node.js functions, not WebAssembly components, so a NetWasm function for Azure Functions is a
`JsModule` with the `node` host: C# exports called from a Node.js handler. One instance of the module serves every
request a worker receives. This is the same project as the [AWS Lambda](aws-lambda.md) guide's, with Azure's handler.

## The function

```xml
<Project Sdk="NetWasm.Sdk">
  <PropertyGroup>
    <OutputType>JsModule</OutputType>
    <TargetFramework>net10.0-netwasm</TargetFramework>
    <NetWasmJavaScriptHosts>node</NetWasmJavaScriptHosts>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

`global.json` pins NetWasm.Sdk under `msbuild-sdks`, as for every NetWasm project.

```csharp
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Functions;

public static partial class Exports
{
    // The provider's handler passes each request's method, path and body and answers with what this returns.
    [JSExport("handle")]
    public static Task<string> Handle(string method, string path, string body) =>
        Task.FromResult($"Hello from C#: {method} {path} ({body.Length} bytes)\n");
}
```

The handler, `index.mjs`, registers an HTTP trigger with the Node.js v4 programming model. It starts opening the
module while the worker loads the app, and each request waits for that one instance:

```js
import { app } from "@azure/functions";
import { open } from "./Functions.node.mjs";

const opened = open();

app.http("hello", {
  methods: ["GET", "POST", "PUT", "DELETE", "PATCH"],
  authLevel: "anonymous",
  route: "{*path}",
  handler: async request => {
    const service = await opened;
    return {
      headers: { "content-type": "text/plain; charset=utf-8" },
      body: await service.handle(request.method, new URL(request.url).pathname, await request.text()),
    };
  },
});
```

`host.json` drops the default `api` route prefix, so the trigger answers every path:

```json
{
  "version": "2.0",
  "extensions": {
    "http": {
      "routePrefix": ""
    }
  }
}
```

`package.json` names the handler and the one package it needs:

```json
{
  "name": "hello",
  "private": true,
  "main": "index.mjs",
  "dependencies": {
    "@azure/functions": "4.17.0"
  }
}
```

Run `npm install` beside it once and keep the `package-lock.json` it writes, so every deployment installs the same
packages.

## Publish and deploy

```sh
dotnet publish -c Release -o function
cp index.mjs host.json package.json package-lock.json function/
cd function && npm ci --omit=dev
az functionapp create --resource-group <group> --name <app name> --storage-account <storage account> \
  --flexconsumption-location <region> --runtime node --runtime-version 24
func azure functionapp publish <app name>
```

The publish directory holds `Functions.node.mjs` and the files it loads, beside the app's own files. Node.js 24
runs on the Flex Consumption plan; the Linux Consumption plan stops at Node.js 22. `HttpClient` in C# makes its
requests with Node.js's `fetch`. To try the function locally first, run it in Microsoft's Functions host image:

```sh
docker run --rm --platform linux/amd64 -p 7071:80 -v "$PWD/function:/home/site/wwwroot:ro" \
  -e AzureWebJobsScriptRoot=/home/site/wwwroot mcr.microsoft.com/azure-functions/node:4-node24
curl http://localhost:7071/hello
```

This guide is checked end to end by `eng/verify-azure-functions.sh`, which publishes the function above with
NetWasm's packages from NuGet.org, installs the locked npm packages and requests it in that image.

## A `wasi:http` service instead

A service made from the `netwasm-serverless` template is a WebAssembly component, which Azure Functions does not run
as it is. Run it on Azure Container Apps in a container image with Wasmtime (see [Containers](containers.md)), or
keep its logic in a library both projects reference.
