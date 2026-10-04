# Holdhint op Windows testen

Dit is de test voor de Windows-laptop. Je hoeft niets te programmeren. De Mac-app laat je met rust: bij Windows is er geen schakelaar voor Input Monitoring.

De bestanden staan op de Mac in de map `windows/dist` van dit project.

## 1. Het juiste bestand kiezen

Open op de laptop **Instellingen → Systeem → Info**.

- Staat er **x64** of **x64-based processor**: gebruik `Holdhint-1.2.1-win-x64-setup.exe`.
- Staat er **ARM**: gebruik `Holdhint-1.2.1-win-arm64-setup.exe`.

Kopieer dat ene bestand naar de laptop (USB-stick, mail, of wat je meestal gebruikt).

## 2. Installeren

Het programma is niet ondertekend. Windows waarschuwt daar expres voor.

1. Dubbelklik het installatiebestand.
2. Als de browser of Windows het bestand “ongebruikelijk” noemt: kies **Behouden** of **Keep**.
3. Als er een blauw venster komt met **Windows protected your PC**: klik **More info**, daarna **Run anyway**.
4. In het installatievenster mag **Start Holdhint when I sign in** aan blijven. Je kunt het later in het menu van Holdhint uit zetten.
5. Klik door tot het programma start. Er komt geen icoon op de taakbalk.

## 3. Het icoon vinden

Kijk naast de klok, rechtsonder. Een donker icoon met een witte H.

Zie je het niet: klik op het pijltje ^ (verborgen pictogrammen). Holdhint staat daar.

Rechterklik op het icoon. In het menu staat de versie **Holdhint 1.2.1** en een regel **Hints on · keyboard hook on**.

Staat er **keyboard hook off**, dan kan het paneel de toetsen niet volgen. Kies **Quit**, start Holdhint opnieuw, en kijk of de regel dan **on** zegt. Blijft het **off**, noteer dat. Dan is de test klaar.

## 4. Het paneel

1. Houd **Ctrl** ingedrukt. Druk nog geen andere toets.
2. Na ongeveer een halve seconde verschijnt een donker paneel op het scherm waar de muis staat.
3. De tekst moet rechtop staan, niet op zijn kop. Copy en Paste moeten erbij staan.
4. Laat Ctrl los. Het paneel verdwijnt meteen.
5. Houd de **Windows-toets** en **Shift** samen vast. Bij **S** moet iets staan over een knipsel naar het klembord (snip, clipboard).
6. Laat los. Het paneel weg, en het Start-menu mag niet open gaan.
7. Tik de Windows-toets kort aan, korter dan een halve seconde. Het Start-menu moet wél open gaan. Sluit het weer.

## 5. Wegklikken

1. Houd Ctrl vast tot het paneel er is.
2. Klik ergens op het scherm, ook op het paneel. De klik moet in het programma eronder aankomen, en het paneel moet dicht gaan.
3. Houd Ctrl weer vast. Druk op **Escape**. Het paneel gaat dicht.
4. Open Chrome of Edge. Houd Ctrl vast. Er moet **New tab** bij staan (de letter T), ook als Chrome zelf geen menu laat zien.

## 6. Als het paneel blijft hangen

Een klik op het paneel gaat naar het programma eronder en sluit het paneel. Lukt dat niet:

- Druk op Escape.
- Of wacht. Het paneel sluit zichzelf, en het blijft nooit langer dan een halve minuut.
- Of rechterklik het icoon bij de klok en kies **Quit**.

## 7. Het menu

Rechterklik het icoon.

- **Show Sample (Win+Shift, 5s)** toont het paneel een paar seconden vanzelf. Daarna is het weg.
- **Show Hints** uit: het paneel komt niet meer bij het vasthouden van toetsen. Zet het daarna weer aan.
- **Quit** sluit Holdhint. Het icoon is dan weg.

## 8. Windows-toets die bleef plakken

Dit is de fout uit 1.2.0. Stop eerst de oude Holdhint (rechterklik, **Quit**) en installeer daarna 1.2.1. Anders blijft het oude programma draaien.

1. Houd alleen de **Windows-toets** vast tot het paneel er is. Houd hem nog een paar seconden vast, ook met herhaling. Laat los. Het paneel gaat weg. Het Start-menu blijft dicht.
2. Doe dat nog vier keer achter elkaar. Elke keer het paneel, en elke keer weg bij loslaten.
3. Houd daarna alleen de Windows-toets weer vast. Je krijgt het Windows-paneel, niet een leeg scherm.
4. Laat los. Houd alleen **Alt** vast. Het paneel is Alt, niet Alt+Win. Zelfde controle met alleen **Ctrl** en alleen **Shift**.
5. Houd de Windows-toets vast tot het paneel er is. Druk op **Escape**. Laat los. Houd daarna alleen de Windows-toets vast: weer het Windows-paneel. Herhaal die stap met een klik in plaats van Escape.
6. Tik de Windows-toets kort aan. Het Start-menu gaat open. Sluit het.
7. Houd de Windows-toets vast tot het paneel er is, en druk op **E**. Verkenner gaat open. Het Start-menu gaat niet óók open. Het paneel is weg.
8. Als een letter daarna toch een Windows-sneltoets doet (E opent Verkenner terwijl je de Windows-toets niet vasthoudt): tik de Windows-toets één keer kort. Daarna hoort een gewone letter weer een letter te zijn.

## 9. Klaar

Als dit klopt, is de test geslaagd. Meld kort wat er afweek, vooral:

- stond de tekst rechtop
- ging het paneel weg toen je de toetsen losliet
- ging het Start-menu open of dicht bij een lange Windows-toets
- na dat paneel: toont alleen Alt het Alt-paneel, niet Alt+Win
- stond er **keyboard hook on**

Je hoeft niets te publiceren en niets te pushen.
