using System.Globalization;
using System.Text;

namespace GP.Expenses;

public sealed record Expense(
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    string Category);

public sealed class Settings
{
    public Dictionary<string, decimal> Rates { get; set; } = new();
    public Dictionary<string, string> Rules { get; set; } = new();
    public Dictionary<string, decimal> Budgets { get; set; } = new();
}

public sealed record CategoryTotal(
    string Category,
    decimal AmountPln,
    decimal? BudgetPln)
{
    public bool IsOverBudget => BudgetPln.HasValue && AmountPln > BudgetPln.Value;
}

public sealed record ImportResult(int Added, int Duplicates, int Invalid);

public static class ExpenseService
{
    public static ImportResult Import(
        TextReader reader,
        List<Expense> expenses,
        Settings settings,
        Action<int, string> reportError)
    {
        var header = reader.ReadLine()
            ?? throw new FormatException("CSV jest pusty.");
        header = header.TrimStart('\uFEFF');

        var separator = ParseCsv(header, ';').Count == 4 ? ';' : ',';
        var columns = ParseCsv(header, separator)
            .Select(value => value.Trim().ToLowerInvariant())
            .ToArray();

        if (!columns.SequenceEqual(new[] { "data", "kwota", "waluta", "opis" }))
        {
            throw new FormatException(
                "Oczekiwany nagłówek: data,kwota,waluta,opis " +
                "(przecinki lub średniki).");
        }

        var known = expenses.Select(Key).ToHashSet();
        var added = 0;
        var duplicates = 0;
        var invalid = 0;
        var lineNumber = 1;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var fields = ParseCsv(line, separator);
                if (fields.Count != 4)
                    throw new FormatException("Wiersz musi mieć cztery pola.");

                if (!DateOnly.TryParseExact(
                    fields[0].Trim(),
                    new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
                {
                    throw new FormatException("Nieprawidłowa data.");
                }

                var amountText = fields[1].Trim().Replace(',', '.');
                if (!decimal.TryParse(
                    amountText,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var amount) || amount == decimal.MinValue)
                {
                    throw new FormatException("Nieprawidłowa kwota.");
                }

                var currency = fields[2].Trim().ToUpperInvariant();
                if (currency.Length != 3 ||
                    currency.Any(c => c < 'A' || c > 'Z'))
                {
                    throw new FormatException("Waluta musi mieć trzy litery.");
                }

                var description = fields[3].Trim();
                var category = settings.Rules.FirstOrDefault(rule =>
                    description.Contains(
                        rule.Key, StringComparison.OrdinalIgnoreCase)).Value
                    ?? "Inne";

                var expense = new Expense(
                    date, Math.Abs(amount), currency, description, category);

                if (!known.Add(Key(expense)))
                {
                    duplicates++;
                    continue;
                }

                expenses.Add(expense);
                added++;
            }
            catch (FormatException ex)
            {
                invalid++;
                reportError(lineNumber, ex.Message);
            }
        }

        return new ImportResult(added, duplicates, invalid);
    }

    public static List<CategoryTotal> Report(
        IEnumerable<Expense> expenses,
        int year,
        int month,
        Settings settings)
    {
        _ = new DateOnly(year, month, 1);

        return expenses
            .Where(expense => expense.Date.Year == year &&
                              expense.Date.Month == month)
            .GroupBy(expense => expense.Category)
            .Select(group =>
            {
                var total = group.Sum(expense =>
                {
                    if (!settings.Rates.TryGetValue(expense.Currency, out var rate)
                        || rate <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Brak dodatniego kursu dla {expense.Currency}.");
                    }

                    return expense.Amount * rate;
                });

                decimal? budget = settings.Budgets.TryGetValue(group.Key, out var value)
                    ? value
                    : null;

                return new CategoryTotal(group.Key, total, budget);
            })
            .OrderBy(row => row.Category)
            .ToList();
    }

    public static List<string> ParseCsv(string line, char separator)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                    closedQuote = true;
                }
            }
            else if (c == separator)
            {
                fields.Add(field.ToString());
                field.Clear();
                closedQuote = false;
            }
            else if (c == '"' && field.Length == 0 && !closedQuote)
            {
                quoted = true;
            }
            else if (c == '"' || closedQuote)
            {
                throw new FormatException("Nieprawidłowe cudzysłowy CSV.");
            }
            else
            {
                field.Append(c);
            }
        }

        if (quoted)
            throw new FormatException("Niezamknięty cudzysłów CSV.");

        fields.Add(field.ToString());
        return fields;
    }

    private static (DateOnly, decimal, string, string) Key(Expense expense) =>
        (expense.Date, expense.Amount, expense.Currency.ToUpperInvariant(),
         expense.Description.Trim().ToUpperInvariant());
}
