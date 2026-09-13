# Browsercontrole — 13 september 2026

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

Deze verbeteringen zijn geïmplementeerd. De hertest na herladen wacht op opnieuw aanmelden; de toegangscode blijft bewust niet bewaard bij herladen.

## Grenzen

Dit is een gerichte controle van de beoordelaarsroute, geen volledige toegankelijkheidsaudit of test van alle browsers. Operatorrechten, afwijzing, verlopen/gewijzigde versies, foutpaden en klantscheiding zijn door de bestaande softwaretests gedekt; die zijn niet allemaal opnieuw handmatig in deze browsersessie uitgevoerd. Lege klantomgevingen en netwerkmislukkingen zijn niet visueel nagebootst. Echte modelantwoorden blijven buiten deze controle.
