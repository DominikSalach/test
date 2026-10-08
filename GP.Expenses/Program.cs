using System.Globalization;
using System.Text;
using System.Text.Json;
using GP.Expenses;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length != 2 || args[0] is not ("import" or "report" or "export"))
{
    Console.Error.WriteLine(
        "Użycie:\n" +
        "  import <plik.csv>\n" +
        "  report <yyyy-MM>\n" +
        "  export <plik.json>\n" +
        "Dane: expenses.json w bieżącym katalogu.");
    return 1;
}

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var dataPath = Path.GetFullPath("expenses.json");

try
{
    var expenses = File.Exists(dataPath)
        ? JsonSerializer.Deserialize<List<Expense>>(
            await File.ReadAllTextAsync(dataPath), jsonOptions)
            ?? throw new FormatException("Plik danych zawiera null.")
        : new List<Expense>();

    if (args[0] == "export")
    {
        await WriteJsonAsync(Path.GetFullPath(args[1]), expenses);
        Console.WriteLine($"Wyeksportowano {expenses.Count} wydatków.");
        return 0;
    }

    var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    var settings = JsonSerializer.Deserialize<Settings>(
        await File.ReadAllTextAsync(settingsPath), jsonOptions)
        ?? throw new FormatException("Konfiguracja zawiera null.");

    if (settings.Rates is null || settings.Rules is null ||
        settings.Budgets is null ||
        !settings.Rates.TryGetValue("PLN", out var plnRate) || plnRate != 1 ||
        settings.Rates.Any(pair => pair.Value <= 0) ||
        settings.Budgets.Any(pair => pair.Value < 0) ||
        settings.Rules.Any(pair =>
            string.IsNullOrWhiteSpace(pair.Key) ||
            string.IsNullOrWhiteSpace(pair.Value)))
    {
        throw new FormatException(
            "Nieprawidłowe kursy, reguły lub budżety. Kurs PLN musi wynosić 1.");
    }

    if (args[0] == "import")
    {
        using var reader = new StreamReader(args[1]);
        var result = ExpenseService.Import(
            reader, expenses, settings,
            (line, error) => Console.Error.WriteLine($"Wiersz {line}: {error}"));

        await WriteJsonAsync(dataPath, expenses);
        Console.WriteLine(
            $"Dodano: {result.Added}; duplikaty: {result.Duplicates}; " +
            $"błędne wiersze: {result.Invalid}.");
        return result.Invalid == 0 ? 0 : 2;
    }

    if (!DateOnly.TryParseExact(
        args[1] + "-01", "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
    {
        throw new FormatException("Miesiąc musi mieć format yyyy-MM.");
    }

    var report = ExpenseService.Report(
        expenses, month.Year, month.Month, settings);
    var polish = CultureInfo.GetCultureInfo("pl-PL");

    foreach (var row in report)
    {
        var budget = row.BudgetPln.HasValue
            ? $" / budżet {row.BudgetPln.Value.ToString("F2", polish)} PLN"
            : "";

        Console.WriteLine(
            $"{row.Category}: {row.AmountPln.ToString("F2", polish)} PLN{budget}" +
            (row.IsOverBudget ? " — PRZEKROCZONO BUDŻET!" : ""));
    }

    Console.WriteLine(
        $"Razem: {report.Sum(row => row.AmountPln).ToString("F2", polish)} PLN");
    return 0;
}
catch (Exception ex) when (
    ex is IOException or UnauthorizedAccessException or JsonException
        or FormatException or InvalidOperationException
        or ArgumentException or OverflowException)
{
    Console.Error.WriteLine($"Błąd: {ex.Message}");
    return 1;
}

async Task WriteJsonAsync(string path, List<Expense> values)
{
    var temporary = path + ".tmp";
    try
    {
        await File.WriteAllTextAsync(
            temporary, JsonSerializer.Serialize(values, jsonOptions));
        File.Move(temporary, path, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporary))
            File.Delete(temporary);
    }
}
