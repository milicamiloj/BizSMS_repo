# 11 — Cutover plan

## Svrha

Definisati **prelazak sa legacy .NET Framework 4.5 verzije BizSMS-a na novu .NET 10 verziju** na
kontrolisan način, sa preciznim preduslovima, koracima, verifikacijama, rollback planom i
komunikacijom. Cilj: minimalan downtime, nula gubitka podataka, nula regresija za korisnike.

## Preduslovi (moraju biti gotovi pre datuma cutover-a)

1. **Kompletiran i merged code**: sve iz poglavlja 00–10.
2. **Green build** na CI: `dotnet test` prolazi, `dotnet list package --vulnerable` bez
   kritičnih CVE-ova.
3. **UAT okruženje** (identična kopija produkcije baze — refresh do T–2 dana):
   - kompletno smoke-testovanje po test scenarijima iz poglavlja 10;
   - regresioni izveštaji uparuju sa legacy verzijom (period 7 dana);
   - Playwright E2E happy path prolazi.
4. **Baseline metrike** legacy verzije:
   - broj korisnika, broj klijenata, broj brojeva, poslato SMS-ova / 24h;
   - vreme izvršavanja delta job-a po ugovoru (p50/p95);
   - SLA za login / send / report.
5. **Rollback plan** dokumentovan (v. sekciju „Rollback“).
6. **Runbook** za operativni tim (skraćena verzija ovog dokumenta).
7. **Odobrenja**: security review, business owner, DBA.

## Strategija cutover-a — izbor

Tri opcije, po redu smanjenja rizika:

1. **„Big bang“ — prekidno cutover u toku dogovorenog window-a (npr. subota 22h–02h).**
   - Najprostije, ali cela produkcija je pauzirana par sati.
   - Bazu ne migriramo (samo aplikacioni sloj).
2. **Paralelni run („shadow“) — obe verzije čitaju istu bazu, samo nova piše.**
   - Ne primenljivo u pravom smislu (obe pišu u iste tabele → race). Ali može se koristiti
     kraće (30 min) u „read-only compare“ modu pre finalnog prebacivanja.
3. **Blue/green sa load balancer switch-om.**
   - Idealno ako je aplikacija iza reverse proxy-ja. „Blue“ (legacy) radi; „Green“ (novo) se
     digne, testira health i onda LB usmerava traffic. Rollback = LB flip back.

**Preporučeno**: **blue/green** ako je infrastruktura na LB-u; inače **big bang** subotom noću.

## Timeline (T = trenutak prebacivanja)

```
T-14 dana : Freeze feature-a u legacy verziji. Samo bug fixevi.
T-7 dana  : Refresh UAT baze iz produkcije. UAT test pass 100%.
T-2 dana  : Refresh UAT još jednom. Odobrenja upisana.
T-1 dan   : Verifikuj artefakte (Docker slike ili dotnet publish arhive).
            Verifikuj key vault / env vars u ciljnom okruženju.
            Poslati komunikaciju korisnicima (v. „Komunikacija“).
T-2h      : Zauzmi „change window“. Označi status page: „Planirano održavanje“.
T-1h      : Zaustavi legacy scheduler (delta sync + zakazane poruke) tako da nijedan job ne
            krene tokom cutover-a.
T-30 min  : Backup DB (full).
T-15 min  : Test konektivnosti nove aplikacije ka DB-u (iz staging pod-a).
T-10 min  : Migracija ključeva (Data Protection) i legacy password reset flag
            (SQL: `UPDATE AspNetUsers SET MustChangePassword=1 ...` v. poglavlje 04).
T          : Prebaci DNS / LB / IIS binding sa legacy na novu verziju.
T+5 min   : Smoke: HTTP 200 na /health/ready.
T+10 min  : Smoke: login sa test nalogom, 2FA prijem, jedan test SMS.
T+30 min  : Delta manual run za jedan test ugovor. Verifikuj audit + BST_LOG unos.
T+60 min  : Reports: mesečni troškovi, poslato — brojevi se uparuju sa legacy izveštajem.
T+2h      : Ako sve dobro — čist status page: „Uspešno završeno“. Nastavi monitoring.
```

## Backup i tačke povratka

- **DB full backup** neposredno pre T. Naziv: `BizSMS-preCutover-YYYYMMDDhhmm.bak`.
- **Transakcioni log backup** svakih 15 min tokom prvog dana produkcije (postoji već, samo
  verifikuj).
- **Legacy binarni artefakti** ostaju na disku 30 dana (kao rollback).
- **Konfiguracija LB-a** — snimi trenutno stanje pre menjanja.

## Rollback plan

Odluči za rollback ako:

- login ne radi > 15 min posle T;
- delta job ne završava u očekivano vreme (deviacija > 2x);
- kritičan endpoint (send SMS) baca >5% grešaka > 15 min;
- audit tabela `BST_LOG` ne dobija zapise;
- SQL Server pod pritiskom (deadlockovi, CPU > 90%) uzrok direktno u novoj verziji.

Postupak (blue/green):

1. LB flip nazad na legacy verziju (5 sekundi).
2. Zaustavi novu verziju.
3. Vrati flag `MustChangePassword=0` samo za korisnike kojima je login uspeo u prvih 30 min
   (jer nove lozinke rade samo u novoj verziji).
4. Reaktiviraj legacy scheduler.
5. Analiza incident-a. Novi cutover pokušaj tek posle root cause + fix.

Postupak (big bang):

1. Zaustavi novu verziju.
2. Vrati IIS binding na legacy aplikaciju.
3. Reaktiviraj legacy scheduler.
4. Reset password se tretira isto kao gore.

**Bazu ne vraćamo** iz backup-a osim ako nova verzija upisala pogrešne podatke koji su
korumpirali šemu. Za ostalo, retention je „forward only“ jer se šema ne menja.

## Migracija naloga i lozinki

Iz poglavlja 04, ključna odluka: **prisilno resetovanje lozinki** za sve legacy korisnike. Pre
cutover-a:

1. Napravi kolonu `MustChangePassword` ako ne postoji (idempotentna migracija).
2. Postavi `MustChangePassword=1` i `PasswordHash=NULL` za sve aktivne korisnike.
3. Pošalji SMS/email svim `BusinessUser` nalozima sa linkom „prvi login → postavi lozinku“.
   Link je password reset link generisan iz nove verzije.
4. Administratorima daj privremenu lozinku out-of-band (offline).

`SQL` skripta (izvršiti T-10 min, u tranzakciji, sa backup-om):

```sql
BEGIN TRANSACTION;

-- 1. Dodaj kolonu ako ne postoji
IF COL_LENGTH('dbo.AspNetUsers','MustChangePassword') IS NULL
BEGIN
    ALTER TABLE dbo.AspNetUsers ADD MustChangePassword bit NOT NULL CONSTRAINT DF_MustChangePassword DEFAULT(0);
END

-- 2. Označi sve aktivne korisnike
UPDATE dbo.AspNetUsers
SET MustChangePassword = 1,
    PasswordHash = NULL,
    SecurityStamp = NEWID()   -- invalidira postojeće cookies
WHERE Is_Deleted = 0 AND Is_Canceled = 0;

COMMIT;
```

## Verifikacija posle cutover-a (checklist)

### Odmah (T+5 do T+30 min)

- [ ] `/health/live` i `/health/ready` vraćaju 200.
- [ ] Test login sa Administrator nalogom uspeva; dobija 2FA SMS.
- [ ] Test login sa BusinessUser nalogom uspeva; dobija 2FA SMS.
- [ ] Send SMS test (na test broj); `RequireOtpConfirmed` funkcioniše.
- [ ] Zakazivanje SMS-a na T+2 min; poruka se pošalje.
- [ ] `BST_LOG` sadrži `LoginSucceeded`, `SmsSent`, `SmsScheduled` redove.
- [ ] Nema exception-a u Serilog file sinku.

### U prvih 24h

- [ ] Delta sync noćni prolaz — svi ugovori procesuirani, broj added/deactivated ne odstupa >5%
      od baseline-a.
- [ ] Mesečni troškovi izveštaj = legacy verzija za period 7 dana pre T.
- [ ] Poslato/zakazano izveštaj = legacy verzija.
- [ ] Nema `Error` audit reda tipa `DeltaContractFailed` (osim za već poznate greške u SP-u).
- [ ] Session state radi (korisnik ne mora dvaput da radi OTP).
- [ ] Hangfire dashboard pristupačan samo Administrator ulozi.

### U prvoj nedelji

- [ ] Ni jedan korisnik nije prijavio „ne mogu da se prijavim“ (osim inicijalnog reset-a).
- [ ] Report export radi (Excel + CSV).
- [ ] Upload brojeva radi (CSV + XLSX).
- [ ] BST_LOG rast je linearan (nema petlji).
- [ ] Nema `HTTP 5xx` viška u odnosu na baseline.

## Monitoring i alarmi (T+0 do T+7 dana)

- **Sinkroni**:
  - HTTP 5xx > 1% → PagerDuty.
  - Login failure rate > 20% → PagerDuty.
  - SMS send failure rate > 5% → PagerDuty.
- **Batch**:
  - Delta job trajanje > 2× baseline → email admin timu.
  - Zakazana poruka koja nije poslata u roku od 5 min od `ScheduledFor` → email admin timu.
- **Kapacitet**:
  - Kestrel worker thread starvation → alert.
  - SQL Server tempdb > 80% → alert (Hangfire koristi transiente).

## Komunikacija (skica)

**T-7 dana, korisnicima**:

> Poštovani,
> u toku noći sa subote na nedelju [DD.MM.YYYY], u periodu od 22:00 do 02:00, BizSMS aplikacija
> će biti u planiranom održavanju zbog prelaska na novu verziju platforme.
>
> **Šta se menja**:
> - biće potrebno da prilikom sledećeg prijavljivanja **postavite novu lozinku** (dobićete SMS sa
>   linkom);
> - **potvrda telefonom (SMS OTP)** se dodaje kao dodatni korak kod slanja i zakazivanja SMS-a.
>
> Molimo Vas da tokom pauze ne pokušavate slanje SMS poruka jer neće biti uspešno.
> Za sva pitanja kontaktirajte podršku: `podrska@primer.rs`.

**T, status page**: „Planirano održavanje u toku“ → posle T+2h „Održavanje uspešno završeno“.

## Post-mortem šablon

Bez obzira da li je cutover prošao bez problema, pripremi kratak dokument sa:

- šta je urađeno vs. plan (razlike, opravdanja),
- šta je funkcionisalo dobro,
- šta bi drugačije radili,
- open issues (ako postoje) sa vlasnicima i rokovima,
- linkovi na audit / logove tokom cutover window-a.

## Post-cutover cleanup (T+30 dana)

- Uklanjanje starih legacy artefakata (binarni fajlovi, IIS site).
- Uklanjanje legacy `packages.config` iz VCS-a.
- Uklanjanje starih baze `__MigrationHistory` (EF6) — samo ako se ne planira rollback.
- Uklanjanje `MustChangePassword=1` skripte iz repozitorijuma (nije više potrebna).
- Arhiviranje starih Serilog / log4net logova.
- Analiza performansi novog produkcijskog stack-a — ima li nešto što traži tuning.

## Before / After — vidljive promene za korisnike

| Vidljivo za korisnika              | Legacy                              | .NET 10 verzija                                    |
|------------------------------------|--------------------------------------|----------------------------------------------------|
| Prijava                            | username + password                  | username + password + SMS OTP                      |
| Slanje SMS-a                       | direktno kroz UI                     | dodatni OTP korak pre slanja                       |
| Zaboravljena lozinka               | admin ručno resetuje                 | self-service reset kroz SMS/email link             |
| Izveštaji                          | slabo formatiran Excel               | prošireni Excel sa header/format + CSV opcija      |
| Delta sync                         | ručno ili nedeterministički          | automatski svaki dan @ 03:00 + on-demand           |
| Upload brojeva                     | zbirna lista grešaka                 | greške po redu sa vrednošću i uzrokom              |
| Session timeout                    | 20 min                                | 30 min sliding + OTP potvrda ističe za 5 min       |

## Checklist za code review (cutover specifično)

- [ ] SQL migracija „MustChangePassword“ je idempotentna.
- [ ] Rollback procedura je isprobana na UAT-u (feature toggle za flip).
- [ ] Konfiguracije za `SmsGateway` različite između Dev / UAT / Prod.
- [ ] Hangfire connection string koristi zasebnu šemu `hangfire`.
- [ ] Data Protection ključevi persist-uju kroz `AppDbContext` (ili file share).
- [ ] Serilog sinks za produkciju su isključivo asinhroni.
- [ ] `/admin/jobs` dashboard je iza `Administrator` role provere.
- [ ] Health checks su registrovani na `/health/live` i `/health/ready`.

## Najčešće greške i kako ih izbeći

1. **„Zaboraviti Data Protection“ na blue/green setup-u** — nova instanca ne može da procita
   antiforgery token iz stare. Persist ključeve DB-om.
2. **Cutover bez freeze-a legacy koda** — ako developer commit-uje 2 sata pre cutover-a, riskuješ
   inkompatibilne izmene DB šeme.
3. **DNS TTL previsok** — flip novog DNS zapisa čeka po sat vremena. Postavi TTL na 300s bar 24h
   pre cutover-a.
4. **Reset password mail bez retry-a** — ne oslanjaj se na jedan pokušaj slanja; job sa retry-jem.
5. **Ne testirati rollback** — rollback plan koji nikada nije izvršen na UAT-u je fikcija.
6. **Preskočiti audit sanity check posle cutover-a** — potvrdi da `BST_LOG` prima zapise, ne
   samo Serilog file sink.
7. **Aktivirati sve schedule job-ove pre finalne verifikacije** — bolje ih ostavi paused i
   „uključi ručno“ nakon T+30 min kada je sve zeleno.
8. **Zaboraviti komunikaciju sa DBA timom** — oni moraju znati za planiranu backup rotaciju
   pre i posle cutover-a.
