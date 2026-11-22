// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hello;

// A 1 MiB response, which the Worker streams as the component writes it.
public static partial class ServiceExports
{
    public static partial Task<HttpResponseMessage> Handle(HttpRequestMessage request) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[1024 * 1024]),
        });
}
