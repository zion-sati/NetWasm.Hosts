// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

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
