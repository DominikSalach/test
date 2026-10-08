using System.Text.Json;
using GP.Expenses;

var settings = new Settings
{
    Rates = new() { ["PLN"] = 1m, ["EUR"] = 4m },
    Rules = new() { ["biedronka"] = "Jedzenie" },
    Budgets = new() { ["Jedzenie"] = 10m }
};

var expenses = new List<Expense>();
var errors = new List<int>();

using (var reader = new StringReader(
    "data;kwota;waluta;opis\n" +
    "2026-01-02;-12,50;pln;BIEDRONKA\n" +
    "2026-01-02;12,50;PLN;biedronka\n" +
    "zła-data;5;PLN;test\n" +
    "2026-01-03;2,00;EUR;Biedronka\n"))
{
    var result = ExpenseService.Import(
        reader, expenses, settings, (line, _) => errors.Add(line));

    Check(result == new ImportResult(2, 1, 1), "Import i duplikaty");
    Check(errors.SequenceEqual(new[] { 4 }), "Numer błędnego wiersza");
    Check(expenses[0].Amount == 12.50m, "Polska kwota i znak");
    Check(expenses.All(e => e.Category == "Jedzenie"), "Reguły kategorii");
}

using (var reader = new StringReader(
    "data,kwota,waluta,opis\n" +
    "2026-02-01,\"3,50\",PLN,\"Sklep, \"\"ABC\"\"\"\n"))
{
    var result = ExpenseService.Import(
        reader, expenses, settings, (_, _) => { });

    Check(result.Added == 1, "Przecinkowy CSV");
    Check(expenses[^1].Description == "Sklep, \"ABC\"", "Cudzysłowy CSV");
    Check(expenses[^1].Category == "Inne", "Domyślna kategoria");
}

var report = ExpenseService.Report(expenses, 2026, 1, settings);
Check(report.Count == 1 && report[0].AmountPln == 20.50m, "Raport i kursy");
Check(report[0].IsOverBudget, "Przekroczenie budżetu");
Check(ExpenseService.Report(expenses, 2025, 1, settings).Count == 0,
    "Pusty miesiąc");

Throws<InvalidOperationException>(() =>
    ExpenseService.Report(
        new[] { new Expense(new DateOnly(2026, 1, 1), 1, "GBP", "", "Inne") },
        2026, 1, settings), "Brak kursu");

Throws<FormatException>(() =>
    ExpenseService.ParseCsv("\"niedomknięty", ','), "Błędny CSV");

using (var reader = new StringReader(
    "data;kwota;waluta;opis\n" +
    "2026-03-01;abc;PLN;test\n" +
    "2026-03-02;1;PLN;dobry\n"))
{
    var result = ExpenseService.Import(
        reader, expenses, settings, (_, _) => { });
    Check(result.Invalid == 1 && result.Added == 1,
        "Import kontynuuje po błędnej kwocie");
}

var restored = JsonSerializer.Deserialize<List<Expense>>(
    JsonSerializer.Serialize(expenses));
Check(restored is not null && restored.SequenceEqual(expenses),
    "Zapis i odczyt JSON");

Console.WriteLine("Wszystkie testy przeszły.");

static void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception($"Test nie przeszedł: {name}");
}

static void Throws<T>(Action action, string name) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }

    throw new Exception($"Test nie przeszedł: {name}");
}
