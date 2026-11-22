# NetWasm.Hosts

Runs [NetWasm](https://github.com/zion-sati/NetWasm) HTTP services on hosts that do not run WebAssembly components
themselves. NetWasm builds a standard `wasi:http` service component that Wasmtime, Spin and wasmCloud run as it is;
the packages here put that same component behind another host's own interface.

[NetWasm.Hosts](src/NetWasm.Hosts/README.md), a NuGet package a service project references, publishes it for hosts
that run JavaScript:

| Host | What `dotnet publish` also writes |
| --- | --- |
| Cloudflare Workers | `publish/cloudflare`: a Worker that `wrangler dev` runs and `wrangler deploy` deploys |
| Deno | `publish/deno`: a service that `deno serve` runs |

Its version is the NetWasm version it works with.

Hosts that need no package:

| Host | How |
| --- | --- |
| Wasmtime, Spin, wasmCloud | Run the published component as it is (`wasmtime serve -Sp3 -Scli`, `spin up`, `wash dev`); checked by `eng/verify-native-hosts.sh` |
| SpinKube (Kubernetes) | [SpinKube](docs/spinkube.md): the component as a `SpinApp` on the Spin containerd shim |
| Google Cloud Run, Azure Container Apps, AWS Fargate | [Containers](docs/containers.md): `wasmtime serve` in a container image |
| AWS Lambda | [AWS Lambda](docs/aws-lambda.md): a `JsModule` with the `node` host in a Node.js function |

## Building and testing

```sh
dotnet test NetWasm.Hosts.slnx          # the publish step's unit tests, 100% line and branch coverage
eng/test-javascript.sh                   # the host modules, 100% line, branch and function coverage
eng/verify-service-hosts.sh              # end to end in Docker: the template's service published with NetWasm.Hosts, run in wrangler dev and deno serve
eng/verify-native-hosts.sh               # the template's service in Wasmtime, Spin and wasmCloud
eng/verify-spinkube.sh                   # the SpinKube guide, in a kind cluster
eng/verify-container.sh                  # the container guide
eng/verify-aws-lambda.sh                 # the AWS Lambda guide, in AWS's Lambda image
```

The `verify-*` scripts need Docker and use pinned images and tools. Each creates the service from NetWasm's
`netwasm-serverless` template with NetWasm's packages from NuGet.org, publishes it and requests it on the host.

## License

See [LICENSE.md](LICENSE.md): the host modules a published service deploys are MIT; the publish step is covered by the NetWasm
Community License 1.0, as NetWasm's own build tools are. Contributions are accepted under the
[Contributor License Agreement](CLA.md).
