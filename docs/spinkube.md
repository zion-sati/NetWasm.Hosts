# NetWasm services on SpinKube

[SpinKube](https://www.spinkube.dev) runs Spin applications on Kubernetes, with the Spin containerd shim on each
node. A service made from the `netwasm-serverless` template is a standard `wasi:http` component, which the shim runs
as Spin itself does.

```sh
dotnet new install NetWasm.Templates
dotnet new netwasm-serverless -n Hello
cd Hello
dotnet publish -c Release -o publish
```

Beside the published component, a Spin manifest names it:

```toml
spin_manifest_version = 2

[application]
name = "hello"
version = "0.1.0"

[[trigger.http]]
route = "/..."
component = "service"

[component.service]
source = "Hello.wasm"
# List the origins HttpClient calls, for example:
# allowed_outbound_hosts = ["https://example.com"]
```

Push the application to a registry your cluster pulls from, and deploy it as a `SpinApp`:

```sh
cp spin.toml publish/ && cd publish
spin registry push ghcr.io/<you>/hello:v1
```

```yaml
apiVersion: core.spinkube.dev/v1alpha1
kind: SpinApp
metadata:
  name: hello
spec:
  image: "ghcr.io/<you>/hello:v1"
  executor: containerd-shim-spin
  replicas: 1
```

```sh
kubectl apply -f spinapp.yaml
kubectl port-forward svc/hello 8083:80
curl http://localhost:8083/hello
```

SpinKube's own [quickstart](https://www.spinkube.dev/docs/install/quickstart/) installs the operator and the shim
on a cluster. Spin offers no `wasi:tls`, so a service that reaches `SslStream` directly needs
`<NetWasmTlsSupport>false</NetWasmTlsSupport>`; `HttpClient` requests go through Spin's `wasi:http` and are not
affected.

This guide is checked end to end by `eng/verify-spinkube.sh`. It publishes the template's service with NetWasm's
packages from NuGet.org, creates a kind cluster on the Spin shim's node image (containerd-shim-spin 0.27.2, which
runs Spin 4.2.6), and installs cert-manager 1.20.0 and spin-operator 0.6.1 as the quickstart does. It then pushes the
service to a local registry and requests the `SpinApp`.
