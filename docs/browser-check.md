# Browsercontrole — 13–15 september 2026

Gecontroleerd in Chrome met Northstar als beoordelaar, tegen de lokale demo op poort 5089. Geen modelaanroepen of echte berichten verstuurd.

## Uitgevoerde controles

| Controle | Waarneming |
| --- | --- |
| Planning: 1, 7 en 14 dagen | Venster eindigt respectievelijk op 8, 14 en 21 september; de dagplanning bevat één scheepsbezoek. Voorraad blijft 6. |
| Bekend en onbekend volume | De weekplanning toont 12 bekende inkomende voertuigen en één plan zonder bruikbaar volume. |
| DEMO-003 | Beide gereedheidsstatussen geblokkeerd; schadeteam en onbekende afronding zichtbaar. |
| Bronnen en instructies | Drie operationele bronnen en drie fictieve werkinstructies geopend en gelezen. |
| Nieuw concept | Nederlandse tekst, zes passende bronnen, vaste scenariotijd en werkelijke vervaldatum gecontroleerd. |
| Controle vóór goedkeuring | Goedkeurknop uitgeschakeld zolang het controlevakje niet is aangevinkt. |
| Goedkeuring | Na inhoudelijke broncontrole één gesimuleerde verzending uitgevoerd. Audit toont aanmaken, goedkeuren en simuleren; geen e-mail verzonden. |
| Monitoring | Aanmaak en goedkeuring zichtbaar met HTTP 200; nul gemeten modelaanroepen. |
| Mobiel 390 × 844 | Planning, voertuiglijst en lang concept leesbaar; paginabreedte blijft 390 pixels. Brede tabellen scrollen binnen hun eigen gebied. |
| Extra smal 320 × 740 | Planning en bediening passen; paginabreedte blijft 320 pixels. |
| Toetsenbord | Tab vanaf de horizonkeuze bereikt Vernieuwen met zichtbare focus. |
| Browserlog | Geen applicatiefout aangetroffen; één fout komt uit een Chrome-extensie, niet uit PortOps. |

## Gevonden verbeterpunten

- Enkelvoud corrigeren: “1 plan” en “1 scheepsbezoek”.
- Planningstabellen een benoemd, focusbaar scrollgebied geven, met uitleg op smalle schermen.
- Bij selectie vanuit de planning toetsenbordfocus naar het voertuigonderzoek verplaatsen.

De drie verbeteringen zijn op 14 september in de ingelogde Chrome-demo hertest en geslaagd:

- De weekplanning toont “1 plan”; de dagplanning toont “1 scheepsbezoek”.
- ArrowRight verschuift zowel de voorraad- als aankomsttabel. De focus staat op het betreffende scrollgebied; de paginabreedte blijft 390 pixels.
- Enter op DEMO-003 in de planning verplaatst de focus naar `vehicle-detail` en toont het juiste voertuigonderzoek. De mobiele weergave van dit onderzoek is visueel gecontroleerd.
- Na afloop is de tijdelijke viewportinstelling verwijderd en de horizon teruggezet op 7 dagen. De gebruiker blijft ingelogd.

Tijdens de hertest bleek een oude JavaScript-versie in de geopende pagina actief. De HTML verwijst nu naar script- en stijlbestanden met een inhoudsversie in de URL. De actuele script-URL en het nieuwe gedrag zijn in Chrome bevestigd. Bij toekomstige wijzigingen aan app.js/app.css moeten deze inhoudsversies in index.html mee worden bijgewerkt.

## Grenzen

Dit is een gerichte controle van de beoordelaarsroute, geen volledige toegankelijkheidsaudit of test van alle browsers. Operatorrechten, afwijzing, verlopen/gewijzigde versies, foutpaden en klantscheiding zijn door de bestaande softwaretests gedekt; die zijn niet allemaal opnieuw handmatig in deze browsersessie uitgevoerd. Lege datasets en fouten zijn op 15 september aanvullend via een geïsoleerde testserver gecontroleerd, zoals hieronder beschreven. Echte modelantwoorden blijven buiten deze controle.


## Lege schermen en fouten — 15 september

De echte frontend is in Chrome geladen vanaf een tijdelijke server op uitsluitend `127.0.0.1:5091`. Die serveerde expliciete testresponses: lege lijsten, kopieën van fictieve operationele gegevens en ingestelde HTTP-fouten. De bestaande demo op 5089, haar toegangscodes, concepten en auditlog zijn niet gewijzigd. De tijdelijke testcode was alleen geldig voor deze testserver; er zijn geen providerverbindingen gebruikt. Dit test de browserpresentatie en herstelacties, niet nieuwe backendfoutscenario’s.

| Controle | Resultaat |
| --- | --- |
| Ongeldige toegangscode | Nederlandse fout zichtbaar; daarna aanmelden met de testcode lukt. |
| Lege voertuigen en planning | Nulstanden, lege voorraad, lege aankomsten en geen voertuigonderzoek zichtbaar. |
| Lege voorstellen en monitoring | Uitleg zichtbaar; gemiddelde en percentiel tonen “Niet gemeten”. |
| Leeg naar gevuld | Vernieuwen herstelt voertuigselectie en planningscijfers. |
| Planning geeft HTTP 500 | Oude cijfers, tabellen en bronnen worden gewist; fout en vernieuwknop blijven beschikbaar. |
| Planning herstelt | Nieuwe gegevens verschijnen en de foutmelding verdwijnt. |
| Voorstellen geven HTTP 500 en herstellen | Fout zichtbaar; succesvol vernieuwen ruimt de melding op. |
| Conceptaanmaak geeft HTTP 503 | Geen succesmelding of nieuw concept; de fout blijft na het ophalen van de lijst zichtbaar. |
| Testserver werkelijk gestopt | Nederlandse verbindingsmelding zichtbaar; na herstart herstelt vernieuwen zonder opnieuw aanmelden. |
| Eerste laden geeft HTTP 500 | Login toont een fout en opent geen halfgevulde werkruimte. |

De lege mobiele werkruimte en de desktopfoutmeldingen zijn visueel bekeken. Twee kleine teksten zijn aangepast en hertest: bij lege voorraad geen uitnodiging een voertuig te kiezen, en bij een onbeschikbare conceptfunctie geen misleidende verwijzing naar modelconfiguratie. JavaScript-syntax en gewijzigde assetversies zijn gecontroleerd. De tijdelijke viewportinstelling is verwijderd en de testtab en testserver zijn gesloten.
