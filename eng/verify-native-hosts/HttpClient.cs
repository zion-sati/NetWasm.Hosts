// Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hello;

// Calls the orders service and answers with the order's total: the component's own request goes out through the
// Worker's fetch.
public static partial class ServiceExports
{
    private static readonly HttpClient Orders = new() { BaseAddress = new("http://127.0.0.1:9000/") };

    public static async partial Task<HttpResponseMessage> Handle(HttpRequestMessage request)
    {
        using var order = JsonDocument.Parse(await Orders.GetStringAsync("orders/42"));
        var total = order.RootElement.GetProperty("total").GetDecimal();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"Order 42 totals {total}\n") };
    }
}
