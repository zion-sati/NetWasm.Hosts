# NetWasm on Google Cloud Run functions

Cloud Run functions runs Node.js functions through Google's Functions Framework, not WebAssembly components, so a
NetWasm function for it is a `JsModule` with the `node` host: C# exports called from a Node.js handler. One instance
of the module serves every request a container receives. This is the same project as the [AWS Lambda](aws-lambda.md)
guide's, with Google's handler.

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

The handler, `index.js`, registers an HTTP function. It starts opening the module while the framework loads the
function, and each request waits for that one instance. The framework has read the request's body before the
function runs; `rawBody` holds its bytes when there is one:

```js
import * as functions from "@google-cloud/functions-framework";
import { open } from "./Functions.node.mjs";

const opened = open();

functions.http("hello", async (request, response) => {
  const service = await opened;
  const body = request.rawBody?.toString("utf8") ?? "";
  response.type("text/plain").send(await service.handle(request.method, request.path, body));
});
```

`package.json` makes the handler an ES module and names the framework:

```json
{
  "name": "hello",
  "private": true,
  "type": "module",
  "main": "index.js",
  "dependencies": {
    "@google-cloud/functions-framework": "5.0.5"
  }
}
```

Run `npm install` beside it once and keep the `package-lock.json` it writes, so every deployment installs the same
packages.

## Publish and deploy

```sh
dotnet publish -c Release -o function
cp index.js package.json package-lock.json function/
gcloud run deploy hello --source function --function hello --base-image nodejs24 --region <region> \
  --allow-unauthenticated
```

The publish directory holds `Functions.node.mjs` and the files it loads, beside the handler; Cloud Build installs the
npm packages from the lockfile. `HttpClient` in C# makes its requests with Node.js's `fetch`. To try the function
locally first, serve it with the framework on Node.js 24:

```sh
cd function && npm ci --omit=dev
npx functions-framework --target=hello
curl http://localhost:8080/hello
```

This guide is checked end to end by `eng/verify-cloud-run-functions.sh`, which publishes the function above with
NetWasm's packages from NuGet.org, installs the locked npm packages and requests it through the framework in a
Node.js 24 image.

## A `wasi:http` service instead

A service made from the `netwasm-serverless` template is a WebAssembly component, which Cloud Run functions does not
run as it is. Deploy it to Cloud Run as a container image with Wasmtime instead (see [Containers](containers.md)),
or keep its logic in a library both projects reference.
