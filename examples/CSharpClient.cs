using System.Net.Http.Json;

using var client = new HttpClient { BaseAddress = new Uri("http://192.168.1.50") };

var request = new
{
    printId = "order-10042-customer",
    prePrintLines = 0,
    lines = new[]
    {
        "MY STORE",
        "--------------------------------------------",
        "Widget                                $12.00",
        "TOTAL                                 $12.00"
    },
    content = (string?)null,
    postPrintLines = 4,
    wrap = "none",
    compressed = false,
    cut = true,
    copies = 1,
    logo = (string?)null,
    logoPosition = "top"
};

var preview = await client.PostAsJsonAsync("/api/preview", request);
preview.EnsureSuccessStatusCode();
Console.WriteLine(await preview.Content.ReadAsStringAsync());

var print = await client.PostAsJsonAsync("/api/print", request);
print.EnsureSuccessStatusCode();
Console.WriteLine(await print.Content.ReadAsStringAsync());
