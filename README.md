# lewandowscy-label-printer

Aplikacja Windows (jeden plik `Etykiety.exe`) do tworzenia i drukowania etykiet 50×30 mm
na drukarce Zebra LP2844 podłączonej przez USB. Plan: [docs/PLAN.md](docs/PLAN.md).

## Pobranie programu

Budowanie odbywa się na GitHubie (repozytorium jest lustrem tego z Forgejo):
zakładka **Actions** → ostatnie uruchomienie `build` → artefakt **Etykiety-win-x64**.
Wersje oznaczone tagiem `v*` trafiają też do **Releases**.

## Instalacja drukarki (Windows 11, bez sterownika Zebry)

1. Podłącz drukarkę przez USB i włącz ją.
2. Ustawienia → Bluetooth i urządzenia → Drukarki i skanery → **Dodaj urządzenie** →
   „Drukarki, której szukam, nie ma na liście” → *Dodaj drukarkę lokalną lub sieciową, używając ustawień ręcznych*.
3. Port: **USB001** (Wirtualny port drukarki dla USB).
4. Producent **Generic**, drukarka **Generic / Text Only**. Nazwa np. `Zebra LP2844`.
5. Nie drukuj strony testowej Windows (wydrukowałaby śmieci) – zamiast tego użyj
   „Wydruk testowy” w programie.

## Kalibracja

- **Auto-kalibracja czujnika** – drukarka sama mierzy etykietę i przerwę (wysuwa kilka etykiet).
- **Wydruk testowy** – ramka powinna leżeć na krawędzi etykiety, czarny kwadrat w prawym dolnym rogu.
  Jeśli ramka jest przesunięta, popraw „Przesunięcie” (mm). Za jasno/ciemno → „Ciemność”.
  Kwadrat u góry → odznacz „Drukuj od góry”.

Ustawienia zapisują się w `%APPDATA%\LabelPrinter\settings.json`.

## Budowanie lokalnie (Windows)

```
dotnet test tests/LabelPrinter.Core.Tests
dotnet publish src/LabelPrinter.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
