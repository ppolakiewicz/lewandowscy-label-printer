# Plan: aplikacja do etykiet (Zebra LP2844, 50×30 mm)

## Wymagania (od klienta)
- Etykiety 50 mm × 30 mm, rolka **jedna kolumna**.
- Tylko tekst: 1 linijka z zawijaniem (spacja, `-`, `_`) albo 2–3 osobne linijki.
- Baza wpisanych tekstów: podpowiedzi w trakcie pisania, edycja bazy.
- Drukarka Zebra LP2844 (EPL2, 203 dpi, termiczna bezpośrednia) po USB.
- Windows 11, jeden komputer, użytkownik ma uprawnienia administratora.
- Aplikacja desktopowa, jeden plik `.exe`.

## Drukowanie bez sterownika Zebry
- Drukarka dodana w Windows jako **„Generic / Text Only”** na porcie USB (`USB001`).
- Aplikacja wysyła gotowe polecenia **EPL2** jako RAW (`OpenPrinter`/`WritePrinter`, winspool).
- Etykieta renderowana w aplikacji do bitmapy 1-bit i wysyłana poleceniem `GW`
  (polskie znaki, dowolna czcionka, podgląd = wydruk).
- 203 dpi = 8 punktów/mm → 50×30 mm = **400×240 punktów** (`q400`, `Q240,24`).
- Ustawienia kalibracji: przesunięcie X/Y, ciemność (`D`), prędkość (`S`), przerwa.

## Technologia
| Element | Wybór |
|---|---|
| Język / UI | C# .NET 10 (LTS), WPF |
| Wynik | jeden plik `.exe` (self-contained, single-file, win-x64) |
| Baza | SQLite w `%APPDATA%\LabelPrinter\` |
| Budowanie | Linux (`EnableWindowsTargeting`) + Forgejo Actions |

## Funkcje v1
- Pole tekstowe z podpowiedziami (fragment, bez wielkości liter i polskich znaków,
  sortowanie: częstość + ostatnie użycie).
- Tryb układu: Auto (zawijanie 1–3 linijki) / Ręczny (2–3 linijki).
- Automatyczne dopasowanie rozmiaru czcionki, wyrównanie, pogrubienie.
- Podgląd na żywo, liczba kopii, Drukuj (Enter / Ctrl+P).
- Każdy wydruk zapisuje tekst w bazie / podbija licznik.
- Okno bazy: wyszukiwanie, edycja, usuwanie, dodawanie, druk z listy, import/eksport CSV.
- Ustawienia: wybór drukarki, kalibracja, wydruk testowy, domyślna czcionka.

## Etapy
1. Szkielet + wysyłanie EPL RAW + wydruk testowy i kalibracja (exe testowe dla klienta).
2. Silnik etykiety: zawijanie, dopasowanie czcionki, podgląd, konwersja do `GW` + testy.
3. Baza SQLite, podpowiedzi, okno edycji bazy.
4. Ustawienia, import/eksport, ikona, budowanie exe w Forgejo Actions.
5. Test u klienta: instalacja drukarki „Generic / Text Only”, kalibracja na rolce, poprawki.

## Otwarte
- Przerwa między etykietami czy czarny znacznik? (domyślnie przerwa 3 mm, do kalibracji)
