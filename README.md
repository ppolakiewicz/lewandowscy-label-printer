# lewandowscy-label-printer

Aplikacja Windows (jeden plik `Etykiety.exe`) do tworzenia i drukowania etykiet 50×30 mm
na drukarce Zebra LP2844 lub ZT231 (203 dpi) podłączonej przez USB. Plan: [docs/PLAN.md](docs/PLAN.md).

## Pobranie programu

Budowanie odbywa się na GitHubie (repozytorium jest lustrem tego z Forgejo). Projekt jest budowany
wyłącznie na GitHubie (nie na Forgejo). Każdy commit na `main` dostaje tag `v0.1.<liczba commitów>`
(hook `.githooks/post-commit`), a GitHub buduje do niego wydanie z gotowym plikiem `Etykiety.exe`:

- najnowsza wersja: https://github.com/ppolakiewicz/lewandowscy-label-printer/releases/latest/download/Etykiety.exe
- wszystkie wersje: https://github.com/ppolakiewicz/lewandowscy-label-printer/releases

Repozytorium jest prywatne, więc pobieranie wymaga zalogowania na konto z dostępem.

Hook trzeba włączyć raz w każdym klonie:

```
git config core.hooksPath .githooks
git config push.followTags true
```

## Instalacja drukarki (Windows 11, bez sterownika Zebry)

1. Podłącz drukarkę przez USB i włącz ją.
2. Ustawienia → Bluetooth i urządzenia → Drukarki i skanery → **Dodaj urządzenie** →
   „Drukarki, której szukam, nie ma na liście” → *Dodaj drukarkę lokalną lub sieciową, używając ustawień ręcznych*.
3. Port: **USB001** (Wirtualny port drukarki dla USB).
4. Producent **Generic**, drukarka **Generic / Text Only**. Nazwa np. `Zebra LP2844`.
5. Nie drukuj strony testowej Windows (wydrukowałaby śmieci) – zamiast tego użyj
   „Wydruk testowy” w programie.

### Zebra ZT231

Program działa tylko z wersją **203 dpi** (wersja 300 dpi nie ma języka EPL). Instalacja w Windows jest taka sama,
a dodatkowo:

1. Na drukarce ustaw `System > Program Language > Command Language` na **EPL**.
2. Skalibruj czujnik etykiet.
3. W programie („Ustawienia drukarki…”) zostaw szerokość druku **104 mm**, prędkość **2** i ustaw
   **Prowadzenie rolki** na **Lewa krawędź głowicy**.
4. Zrób **Wydruk testowy**. Jeśli ramka nie trafia w etykietę, wybierz **Prawa krawędź głowicy** i powtórz test.

## Instrukcja obsługi

### Drukowanie etykiety

1. Wpisz tekst w polu **Tekst etykiety**. Podgląd po prawej pokazuje dokładnie to, co wyjdzie z drukarki.
2. Wybierz tryb:
   - **Jedna linijka (zawijanie automatyczne)** – tekst sam przechodzi do następnej linijki po spacji,
     „-” lub „_” (najwyżej 3 linijki),
   - **Osobne linijki** – wpisujesz 2 lub 3 linijki w osobnych polach (Tab przechodzi do następnej).
   Przełączenie trybu przenosi wpisany tekst.
3. Opcje: wyrównanie (do środka / do lewej), pogrubienie, czcionka, rozmiar – **Automatyczny** (największy,
   który się zmieści) albo **Stały** w milimetrach (np. `5` albo `4,5`, strzałki ↑↓ zmieniają o 0,5 mm).
4. Wpisz liczbę kopii (strzałki ↑↓ w polu) i naciśnij **Enter** w polu tekstu, **Ctrl+P** albo **Drukuj**.
   Po wydruku liczba kopii wraca do 1, a tekst zostaje zaznaczony – można od razu pisać następny.

Jeśli tekst nie mieści się na etykiecie, pod podglądem pojawia się czerwone ostrzeżenie, a program pyta przed
drukowaniem. Przy więcej niż 20 kopiach program też pyta o potwierdzenie. Błędy (brak drukarki, drukarka
wyłączona, pusty tekst) pokazuje pasek na dole okna.

### Podpowiedzi

Każdy wydrukowany tekst trafia do bazy (razem z ustawieniami). Podczas pisania pod polem pojawia się lista
pasujących tekstów – wielkość liter i polskie znaki nie mają znaczenia („zolta” znajdzie „Żółta”), najczęściej
używane są na górze.

- **↓ / ↑** – wybór z listy (↓ przy zamkniętej liście otwiera ją, także przy pustym polu),
- **Enter** lub **Tab** – wstawia zaznaczony tekst razem z jego ustawieniami, kliknięcie myszą też,
- **Esc** – zamyka listę.

Gdy lista jest otwarta, Enter **nigdy nie drukuje**: wstawia zaznaczony tekst albo (gdy nic nie jest zaznaczone)
tylko zamyka listę. Drukuje dopiero kolejne naciśnięcie Enter.

### Baza tekstów (menu „Baza tekstów…”, Ctrl+B)

- **Szukaj** – filtruje listę (Ctrl+F; ↓ przechodzi do listy). Kolumny można sortować kliknięciem nagłówka.
- **Dodaj…** (Ctrl+N), **Edytuj…** (F2, Enter lub dwuklik) – tekst, układ, notatka i podgląd. Jeśli taki tekst już
  jest w bazie, program zapyta, czy połączyć wpisy (liczniki użyć się sumują).
- **Usuń** (Delete) – zaznaczone teksty (kilka zaznaczysz z Ctrl lub Shift), po potwierdzeniu.
- **Użyj w oknie głównym** – wstawia tekst do okna głównego.
- **Drukuj** (Ctrl+P) – drukuje zaznaczone teksty, każdy w podanej liczbie kopii.
- **Eksport CSV…** – zapisuje całą bazę do pliku, który otwiera się w Excelu (separator `;`, UTF-8).
- **Import CSV…** – dopisuje teksty z pliku (np. z Excela: „CSV UTF-8” albo zwykłe „CSV”). Wystarczy jedna kolumna
  z tekstami; plik z eksportu wczytuje też ustawienia i notatki. Istniejące teksty są aktualizowane, nic nie jest
  usuwane. Po imporcie program pokazuje, ile dodano, zaktualizowano i pominięto oraz które wiersze mają błędy.

### Ustawienia drukarki i kalibracja (menu „Ustawienia drukarki…”)

Przy pierwszym uruchomieniu program sam proponuje otwarcie ustawień. Tam:

- wybierz drukarkę (np. `Zebra LP2844`) – „Odśwież” wczytuje listę ponownie,
- **Szerokość druku drukarki** – pełna szerokość głowicy (LP2844 i ZT231: **104 mm**),
- **Prowadzenie rolki** – gdzie drukarka trzyma węższą rolkę: **Środek głowicy** (LP2844, etykieta 50 mm: od 27
  do 77 mm) albo **Lewa** / **Prawa krawędź głowicy** (ZT231: od 0 do 50 mm albo od 54 do 104 mm). Program sam
  przesuwa obszar druku na etykietę, a pod polem widać wyliczony zakres,
- **Auto-kalibracja czujnika** – drukarka sama mierzy etykietę i przerwę (wysuwa kilka etykiet),
- **Wydruk testowy** – ramka powinna leżeć na krawędzi etykiety, czarny kwadrat w prawym dolnym rogu.
  Jeśli ramka jest przesunięta, popraw „Przesunięcie” (mm). Za jasno/ciemno → „Ciemność”.
  Kwadrat u góry → odznacz „Drukuj od góry” (na drukarce prowadzącej rolkę krawędzią sprawdź potem ponownie
  prowadzenie rolki),
- **Zapisz ustawienia** (wydruk testowy i auto-kalibracja też zapisują). Zamknięcie okna z niezapisanymi
  zmianami – program zapyta, czy je zapisać.

### Gdzie są dane i kopia zapasowa

Wszystko jest w folderze `%APPDATA%\LabelPrinter` (menu Pomoc → „Otwórz folder z danymi”):

- `settings.json` – ustawienia drukarki,
- `labels.db` – baza tekstów (SQLite; obok mogą być pliki `labels.db-wal` i `labels.db-shm`).

Kopia zapasowa: **zamknij program** i skopiuj cały folder `LabelPrinter` (np. na pendrive). Odtworzenie: zamknij
program i wklej folder z powrotem. Prostsza kopia samych tekstów: „Baza tekstów” → **Eksport CSV…**, a
odtworzenie przez **Import CSV…**.

### Skróty klawiszowe

| Skrót | Działanie |
|---|---|
| Enter (w polu tekstu, lista zamknięta) | drukuj |
| Ctrl+P | drukuj |
| ↓ / ↑, Enter / Tab, Esc | podpowiedzi: wybór, wstawienie, zamknięcie |
| Ctrl+B | baza tekstów |
| F1 | pomoc |
| Alt+J / Alt+O | tryb: jedna linijka / osobne linijki |
| ↑ / ↓ w polu kopii | więcej / mniej kopii |

## Budowanie lokalnie (Windows)

```
dotnet test tests/LabelPrinter.Core.Tests
dotnet publish src/LabelPrinter.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Ikona programu (`src/LabelPrinter.Desktop/Resources/app.ico`) jest generowana skryptem `python3 tools/make_icon.py`
(tylko biblioteka standardowa Pythona).
