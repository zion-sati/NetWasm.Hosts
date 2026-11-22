# NetWasm.Hosts license policy

NetWasm.Hosts follows NetWasm's source-path license boundary. Tooling, meaning the build steps that create a user's
deployable package, is covered by the [NetWasm Community License 1.0](LICENSES/LicenseRef-NetWasm-Community-1.0.txt),
together with its tests. Everything a user deploys is MIT licensed: the host modules each published service carries,
with their tests, and this repository's scripts.

The published licensor is the pseudonym **Zion Sati**. Commercial licensing questions and agreements:
<zion.sati@netwasm.com>. Project repository: <https://github.com/zion-sati/NetWasm.Hosts>.

| Area | License |
| --- | --- |
| The publish step: MSBuild targets and tasks (`src/*/build`, `src/**/*.cs`) and their tests (`tests/*.Tests`) | NetWasm Community License 1.0 |
| The host modules a deployed service runs (`src/*/hosting`) and their tests (`tests/javascript`), `eng/`, and the repository's build metadata and documents | MIT |

[`LICENSE-MAP.md`](LICENSE-MAP.md) is the path map. Each source file begins with a notice naming the copyright holder
and the license the map gives its path.

The Community License is free for individuals, education, open-source work, official contributions, evaluation,
and small and medium-sized businesses: organizations with fewer than 250 employees and less than USD 10,000,000 in
annual revenue. Larger organizations pay for a limited internal commercial-use right by sponsoring a matching
GitHub Sponsors tier at <https://github.com/sponsors/zion-sati>, as described in NetWasm's
[licensing guide](https://github.com/zion-sati/NetWasm/blob/main/docs/licensing.md). Generated Output, such as a
published Worker's `worker.mjs` and `wrangler.json`, may be licensed and distributed under terms chosen by its
author.

The complete applicable texts are included locally:

- [NetWasm Community License 1.0](LICENSES/LicenseRef-NetWasm-Community-1.0.txt)
- [MIT](LICENSES/MIT.txt)
