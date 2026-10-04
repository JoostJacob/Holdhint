# Verslag

De app heet **Holdhint**. De map heet nog `toetshud`. De app, de sneltoetsen en de README zijn alleen Engels, zoals je daarna vroeg. Er zitten geen Nederlandse vertalingen in.

Holdhint is een menubalk-app zonder Dock-icoon. Houd Command, Control, Option, Shift of een combinatie daarvan vast. Na een halve seconde verschijnt een donker paneel op het scherm waar de muis staat, met de toets die je er nog bij kunt drukken en wat die doet. Laat je de modifiers los, dan verdwijnt het paneel. Een andere toets indrukken verbergt het ook. Die toets wordt niet opgeslagen. Er is geen geluid en geen netwerk.

## Wat werkt

- Het paneel voor Command-Control-Shift toont onder meer **3** (heel scherm naar het klembord) en **4** (selectie naar het klembord). Bij 4 staat erbij dat je daarna op spatie kunt drukken en een venster kunt aanklikken.
- Het paneel pakt geen focus. Klikken gaan erdoorheen. Dat heb ik gecontroleerd op het echte venster: `ignoresMouseEvents` aan, het venster kan geen key window worden, het is een non-activating panel, en het staat op pop-upniveau (laag 101), boven gewone vensters, op het scherm onder de muis.
- De lijst staat in `Sources/HoldhintCore/Resources/shortcuts.json`. Wie wil aanpassen, krijgt via het menu een kopie in `~/Library/Application Support/Holdhint/shortcuts.json`. De app herlaadt die bij opslaan. Een kapot bestand laat de vorige lijst staan.
- Het menu heeft hints aan/uit, een voorbeeld, sneltoetsen bewerken en herladen, de twee instellingenpagina’s, en stoppen.
- `./scripts/check.sh` slaagt. `Holdhint.app` met `--self-test` slaagt ook, inclusief de ingepakte app.
- Sneltoetsen uit de menubalk van de voorste app zijn ingebouwd. Die komen erbij als Toegankelijkheid aan staat, en ze vervangen een ingebouwde regel met dezelfde toets.

## Wat nog niet

- Live toetsen heb ik niet kunnen zien. Invoermonitoring staat op deze Mac uit, en die toestemming moet jij zelf geven. Zonder die toestemming verschijnt het paneel niet als je toetsen vasthoudt.
- De menu’s van de voorste app heb ik om dezelfde reden niet live getest. Zonder Toegankelijkheid zie je alleen de ingebouwde lijst. Die is genoeg voor schermafbeeldingen, Spotlight, wisselen van app, vensters en Mission Control.
- Na een nieuwe build verandert de handtekening. macOS negeert dan de oude toestemming tot je de schakelaar uit en weer aan zet en Holdhint herstart.
- Er is niet gepusht en er is geen GitHub-repo gemaakt.

## Zo start en test je het

1. In Terminal: `cd ~/code/toetshud && ./scripts/make-app.sh`
2. In Finder: rechtsklik `Holdhint.app` en kies Open. Bevestig Open als macOS waarschuwt. Er komt geen Dock-icoon. Zoek het ⌘-teken in de menubalk.
3. Apple-menu → Systeeminstellingen → Privacy en beveiliging → **Invoermonitoring**. Zet Holdhint aan. Staat de schakelaar al aan, zet hem uit en weer aan. Staat Holdhint er niet bij, klik op +, en kies `~/code/toetshud/Holdhint.app` (dezelfde kopie die je opent).
4. Wil je ook de sneltoetsen van de voorste app, doe hetzelfde onder **Toegankelijkheid**.
5. Stop Holdhint via het menu en open de app opnieuw. macOS past de toestemming pas toe na een herstart van de app.
6. Het menu toont of Invoermonitoring en Toegankelijkheid aan staan.
7. Houd Command, Control en Shift ongeveer een halve seconde vast. Druk nog niet op 3 of 4. Het paneel moet die twee toetsen tonen. Laat los: het paneel verdwijnt.
8. Werkt dat nog niet, kies **Show Sample** in het menu. Dat toont hetzelfde paneel een paar seconden, zonder toetsen en zonder toestemming. Daarmee zie je of de app draait.

`./scripts/check.sh` test de lijst en de logica zonder dat je iets hoeft toe te staan.
