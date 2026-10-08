public sealed class OdraOrdersClient(HttpClient httpClient)
{
    public async Task<string> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var path = $"orders/{Uri.EscapeDataString(orderId)}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
