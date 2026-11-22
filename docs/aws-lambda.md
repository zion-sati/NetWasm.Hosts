# NetWasm on AWS Lambda

Lambda runs Node.js functions, not WebAssembly components, so a NetWasm function for Lambda is a `JsModule` with the
`node` host: C# exports called from a Node.js handler. One instance of the module serves every request an execution
environment receives.

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
    // index.mjs passes each request's method, path and body and answers with what this returns.
    [JSExport("handle")]
    public static Task<string> Handle(string method, string path, string body) =>
        Task.FromResult($"Hello from C# on Lambda: {method} {path} ({body.Length} bytes)\n");
}
```

The handler, `index.mjs`, opens the module once, while Lambda initializes the function, and calls it for each
request. This one takes a function URL's or an API Gateway HTTP API's request (payload format 2.0):

```js
import { open } from "./Functions.node.mjs";

const app = await open();

export const handler = async event => {
  const body = event.body === undefined ? ""
    : event.isBase64Encoded ? Buffer.from(event.body, "base64").toString("utf8")
    : event.body;
  return {
    statusCode: 200,
    headers: { "content-type": "text/plain; charset=utf-8" },
    body: await app.handle(event.requestContext.http.method, event.rawPath, body),
  };
};
```

## Publish and deploy

```sh
dotnet publish -c Release -o function
cp index.mjs function/
cd function && zip -r ../function.zip . && cd ..
aws lambda create-function --function-name hello --runtime nodejs24.x --handler index.handler \
  --zip-file fileb://function.zip --role <an execution role's ARN>
aws lambda create-function-url-config --function-name hello --auth-type AWS_IAM
```

The publish directory holds `Functions.node.mjs` and the files it loads; the zip carries all of them. `HttpClient`
in C# makes its requests with Node.js's `fetch`. To try the function locally first, run it in AWS's Lambda image,
which carries the runtime interface emulator:

```sh
docker run --rm -p 9000:8080 -v "$PWD/function:/var/task:ro" public.ecr.aws/lambda/nodejs:24 index.handler
curl http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{"rawPath":"/hello","requestContext":{"http":{"method":"GET"}}}'
```

This guide is checked end to end by `eng/verify-aws-lambda.sh`, which publishes the function above with NetWasm's
packages from NuGet.org and invokes it in that image.

## A `wasi:http` service instead

A service made from the `netwasm-serverless` template is a WebAssembly component, which Lambda's Node.js runtime
does not run as it is. Run it on Lambda in a container image with Wasmtime (see [Containers](containers.md)), or
keep its logic in a library both projects reference.
