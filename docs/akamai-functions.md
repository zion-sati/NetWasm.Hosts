# NetWasm on Akamai Functions

Akamai Functions runs Spin applications, and Spin runs the `wasi:http` component NetWasm builds as it is: no package
and no wrapper. The platform takes Spin 4 applications and WASI 0.3 components.

## The application

Start from the template's service and put a Spin manifest beside the project:

```sh
dotnet new install NetWasm.Templates
dotnet new netwasm-serverless -n Hello
cd Hello
```

```toml
spin_manifest_version = 2

[application]
name = "hello"
version = "0.1.0"

[[trigger.http]]
route = "/..."
component = "service"

[component.service]
source = "publish/Hello.wasm"
# List the origins HttpClient calls, for example:
# allowed_outbound_hosts = ["https://example.com"]
```

Spin refuses an outbound request the manifest does not allow, so a service that uses `HttpClient` lists each origin
it calls in `allowed_outbound_hosts`.

## Try it locally, then deploy

```sh
dotnet publish -c Release -o publish
spin up                       # http://127.0.0.1:3000
spin plugin install aka
spin aka login
spin aka deploy
```

Akamai Functions needs Spin 4.0.0 or later. `spin aka deploy` uploads the component and prints the application's URL,
on an `.fwf.app` domain.

The application is checked by `eng/verify-native-hosts.sh`, which serves this manifest with `spin up` on Spin 4.3.0
(GET, an `HttpClient` request to an allowed origin, and the refusal of one that is not allowed). The check runs Spin
locally, not on Akamai Functions itself.

## What the platform does not offer

Spin offers no `wasi:tls`, so a service that reaches `SslStream` directly needs
`<NetWasmTlsSupport>false</NetWasmTlsSupport>`; `HttpClient` requests go through Spin's `wasi:http` and are not
affected.
