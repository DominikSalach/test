# GP.Expenses

Konsolowa aplikacja .NET 8 do wydatków domowych, bez dodatkowych pakietów.

Uruchamiaj polecenia z katalogu głównego repozytorium:

```sh
dotnet run --project GP.Expenses -- import bank.csv
dotnet run --project GP.Expenses -- report 2026-01
dotnet run --project GP.Expenses -- export backup.json
dotnet run --project GP.Expenses.Tests
```

Dane są zapisywane w `expenses.json` w bieżącym katalogu.
Nie uruchamiaj równocześnie kilku procesów zapisujących te same dane.

Konfiguracja: `GP.Expenses/settings.json`.
Kursy oznaczają wartość jednej jednostki waluty w PLN.
Przykładowe kursy należy zmienić na właściwe. Kurs PLN musi wynosić 1.
Budżety są miesięczne i wyrażone w PLN.
Pierwsza pasująca reguła opisu wybiera kategorię; domyślna to `Inne`.
Zmiana reguł nie zmienia kategorii wcześniej zaimportowanych wydatków.

CSV musi mieć nagłówek `data;kwota;waluta;opis` albo
`data,kwota,waluta,opis`. Obsługiwane daty:
`yyyy-MM-dd`, `dd.MM.yyyy`, `dd/MM/yyyy`.

Przykład:

```csv
data;kwota;waluta;opis
2026-01-02;-12,50;PLN;Biedronka
2026-01-03;2,00;EUR;Zakupy
```

Kwoty są traktowane jako wydatki według wartości bezwzględnej.
Nie importuj do tego pliku wpływów ani zwrotów.
Przy separatorze przecinkowym kwoty z przecinkiem zapisuj
w cudzysłowach, np. `"12,50"`.
Obsługiwane są cytowane pola i podwójne cudzysłowy, ale nie pola wielowierszowe.

Duplikat oznacza tę samą datę, kwotę, walutę i opis.
Porównanie opisu ignoruje wielkość liter i skrajne białe znaki.

Błędne wiersze są zgłaszane z numerem; poprawne zostają zapisane.
Nieprawidłowy nagłówek przerywa import.
Brak kursu przerywa raport zamiast pomijać wydatek.

Kody zakończenia: 0 — sukces, 1 — błąd,
2 — import zakończony z błędnymi wierszami.

Eksport i plik danych zawierają opisy transakcji — nie dodawaj ich do Git.
