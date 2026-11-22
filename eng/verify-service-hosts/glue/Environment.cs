// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hello;

// Answers with two variables the project declares, one replaced by the Worker's own, and one it does not declare.
public static partial class ServiceExports
{
    public static partial Task<HttpResponseMessage> Handle(HttpRequestMessage request) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{Environment.GetEnvironmentVariable("GREETING")} {Environment.GetEnvironmentVariable("MODE")} {Environment.GetEnvironmentVariable("UNDECLARED") ?? "unset"}\n"),
        });
}
