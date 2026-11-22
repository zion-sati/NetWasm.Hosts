# NetWasm.Hosts

Publishes a [NetWasm](https://github.com/zion-sati/NetWasm) HTTP service for hosts that run JavaScript rather than
WebAssembly components: Cloudflare Workers, Deno, Netlify Edge Functions and Vercel Functions. The service is the standard
`wasi:http` component NetWasm already builds for Wasmtime, Spin and wasmCloud; this package puts a host's own entry
around it, so `dotnet publish` also writes a directory that host runs as it is.

```sh
dotnet new install NetWasm.Templates
dotnet new netwasm-serverless -n Hello
cd Hello
dotnet add package NetWasm.Hosts --version 0.15.2
```

Name the hosts in the project file:

```xml
<PropertyGroup>
  <NetWasmServiceHosts>cloudflare;deno;netlify;vercel</NetWasmServiceHosts>
</PropertyGroup>
```

Then `dotnet publish -c Release -o publish` writes a directory per host (`publish/cloudflare`, `publish/deno`,
`publish/netlify`, `publish/vercel`) beside the component.

## Cloudflare Workers

```sh
cd publish/cloudflare
npx wrangler@4.158.0 dev          # http://localhost:8787
npx wrangler@4.158.0 deploy
```

| File | What it is |
| --- | --- |
| `worker.mjs` | The Worker: it hands each request to the component's `wasi:http` handler and streams the response back |
| `wrangler.json` | The Worker's name, entry and compatibility date |

The Worker imports the component's core modules as `.wasm` modules, which wrangler uploads compiled. The Worker's
variables and secrets reach the component's environment beside the declared ones, and replace a declared variable of
the same name. Bindings (KV, R2 and the rest) are not passed to the component.

| MSBuild property | Default | |
| --- | --- | --- |
| `NetWasmCloudflareWorkerName` | The assembly name, in lower case with dashes | The Worker's name: lower-case letters, digits and dashes, at most 63 characters |
| `NetWasmCloudflareCompatibilityDate` | `2026-11-01` | The Workers runtime date the Worker asks for |

## Deno

```sh
cd publish/deno
deno serve --allow-read --allow-net --allow-env main.mjs     # http://localhost:8000
```

`main.mjs` compiles the component's core modules once, at startup, and serves the component with `deno serve`. The
component's environment is the declared variables, each replaced by the process's variable of the same name when it
has one; no other variable of the process reaches the component.

Deno Deploy is not covered: Deno is closing it in April 2027 and moving its users to Cloudflare Workers, which the
`cloudflare` host publishes for.

## Netlify Edge Functions

```sh
cd publish/netlify
npx netlify-cli@27.16.2 dev             # http://localhost:8888
npx netlify-cli@27.16.2 deploy --prod
```

| File | What it is |
| --- | --- |
| `netlify/edge-functions/service.js` | The edge function: it serves every path through the component's `wasi:http` handler |
| `gen/*.wasm.mjs` | Each core module embedded in base64, since Netlify's bundler carries modules but no `.wasm` files |
| `netlify.toml` | The site's configuration, with an empty publish directory (`public`) |

The edge function compiles the core modules once per isolate. The site's environment variables reach the component in
place of the declared ones of the same name; no other variable does. Netlify allows an edge function 50 ms of CPU time
per request and 20 MB of code; a request that needs more is stopped. The check runs the edge function in
`netlify dev`'s local edge runtime, not on Netlify itself.

## Vercel Functions

```sh
cd publish/vercel
npx vercel@65.12.1 deploy --prod
```

| File | What it is |
| --- | --- |
| `api/service.mjs` | The function, on the Node.js runtime: its `fetch` export hands each request to the component's `wasi:http` handler |
| `vercel.json` | Sends every path to the function and ships the core modules (`gen/`) with it |
| `package.json` | Selects Node.js 24 |

The function compiles the core modules once per instance. Its environment variables reach the component in place of
the declared ones of the same name; no other variable does. Vercel recommends its Node.js runtime over the Edge
runtime, so the function runs on Node.js. `vercel dev` needs a Vercel account, so the check calls the function's
`fetch` export on Node.js 24 the way Vercel's runtime does, rather than on Vercel itself.

## Every host

- `publish/<host>/gen` holds the component transpiled to JavaScript by NetWasm's jco, with its core modules, and
  `publish/<host>/host` the host modules the entry imports: NetWasm's WASI host and the Fetch API adapter. The
  component itself is unchanged, and `publish/Hello.wasm` still runs on Wasmtime, Spin and wasmCloud.
- Each request gets a new instance of the component, released once its response has been sent. Standard output and
  error go to the host's log.
- The component's own requests (`HttpClient` over `wasi:http`) go out through the host's `fetch`.
- `NetWasmEnvironmentVariable` items reach the component's environment, as on the other hosts.
- The component sees no file system: no entry preopens a directory, and Workers and Netlify edge functions have
  none.
- No host here offers `wasi:tls`, so a program that reaches `SslStream` directly needs `NetWasmTlsSupport=false`;
  `HttpClient` over `wasi:http` is not affected.

## Versions

NetWasm.Hosts uses NetWasm's build tools and host modules as they are in one NetWasm release, so its version is the
NetWasm version it works with: NetWasm.Hosts 0.15.2 needs NetWasm.Sdk 0.15.2, and the build stops with NWHOSTS002
when the two differ. It is verified with wrangler 4.158.0 (workerd 1.20261104.1), Deno 2.9.8, netlify-cli 27.16.2
and Node.js 24.

## Diagnostics

| Code | Meaning |
| --- | --- |
| NWHOSTS001 | The project is not a `wasi:http` service component (`NetWasmWorld` `service`, as the `netwasm-serverless` template makes) |
| NWHOSTS002 | The package's version and the project's NetWasm.Sdk version differ |
| NWHOSTS003 | NetWasm's Node.js, jco, component transpiler or host modules were not restored |
| NWHOSTS004 | The service component was not built |
| NWHOSTS010 | A setting or the transpiled component cannot be published for a host; the message says which (`NetWasmServiceHosts` missing or naming an unknown host, a Worker name or date that is not one, a variable declared twice) |

## License

The host modules (`hosting/*.mjs`, copied into each host's `host/`) are MIT licensed and can be deployed and
redistributed freely. The publish step (the MSBuild targets and task) is covered by the
[NetWasm Community License 1.0](https://github.com/zion-sati/NetWasm.Hosts/blob/main/LICENSES/LicenseRef-NetWasm-Community-1.0.txt),
as NetWasm's own build tools are. See [LICENSE.md](https://github.com/zion-sati/NetWasm.Hosts/blob/main/LICENSE.md).
