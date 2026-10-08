using System.Text.Json;

try
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var json = await File.ReadAllTextAsync(configPath);
    using var config = JsonDocument.Parse(json);
    var settings = config.RootElement.GetProperty("OdraOrdersApi");

    var baseUrl = settings.GetProperty("BaseUrl").GetString();
    var timeoutSeconds = settings.GetProperty("TimeoutSeconds").GetInt32();

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
        (baseUri.Scheme != Uri.UriSchemeHttp &&
         baseUri.Scheme != Uri.UriSchemeHttps) ||
        !baseUri.AbsoluteUri.EndsWith('/') ||
        timeoutSeconds <= 0)
    {
        throw new InvalidOperationException(
            "BaseUrl musi być adresem HTTP(S) zakończonym /, " +
            "a TimeoutSeconds dodatnią liczbą całkowitą.");
    }

    Console.Write("Podaj orderID: ");
    var orderId = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(orderId))
    {
        Console.Error.WriteLine("orderID nie może być pusty.");
        return 1;
    }

    using var httpClient = new HttpClient
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };

    var client = new OdraOrdersClient(httpClient);
    Console.WriteLine(await client.GetOrderAsync(orderId));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Żądanie anulowano lub przekroczono timeout.");
    return 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Błąd HTTP: {ex.Message}");
    return 1;
}
catch (Exception ex) when (
    ex is IOException or UnauthorizedAccessException or JsonException
        or KeyNotFoundException or InvalidOperationException
        or FormatException or OverflowException or ArgumentException)
{
    Console.Error.WriteLine($"Błąd konfiguracji: {ex.Message}");
    return 1;
}
