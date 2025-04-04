# Solution Code Style Guide
---

## Formatierung

### Einrückung & Abstände
-   **Einrückungsgröße:** Verwende 4 Leerzeichen für die Einrückung.
-   **Einrückungsstil:** Verwende Leerzeichen, keine Tabulatoren.
-   **Tabulatorbreite:** Entspricht 4 Leerzeichen.
-   **Leerzeichen um binäre Operatoren:** Füge immer Leerzeichen vor und nach binären Operatoren hinzu (`+, -, *, /, ==, !=, &&, ||`, etc.).
-   **Leerzeichen in Kontrollflussanweisungen:** Füge nach Schlüsselwörtern wie `if`, `for`, `foreach`, `while` ein Leerzeichen hinzu.
-   **Leerzeichen nach Kommas:** Füge nach Kommas ein Leerzeichen hinzu.
-   **Leerzeichen nach Semikolons (in `for`):** Füge nach Semikolons in `for`-Anweisungen ein Leerzeichen hinzu.
-   **Kein Leerzeichen vor Kommas/Semikolons:** Füge kein Leerzeichen vor Kommas oder Semikolons ein.
-   **Kein Leerzeichen nach Casts:** Füge nach einem Type Cast kein Leerzeichen hinzu.
-   **Leerzeichen in Methodendeklarationen/-aufrufen:** Vermeide unnötige Leerzeichen innerhalb von Klammern bei Methodendeklarationen und -aufrufen.

### Zeilenumbrüche & Geschweifte Klammern
-   **Zeilenende:** Verwende CRLF-Zeilenenden.
-   **Letzter Zeilenumbruch:** Füge am Ende von Dateien keinen abschließenden Zeilenumbruch ein.
-   **Stil der geschweiften Klammern:** Verwende den Allman-Stil, bei dem die öffnende Klammer (`{`) in einer neuen Zeile erscheint.
    ```csharp
    // Gut
    if (Bedingung)
    {
        // ...
    }

    // Schlecht
    if (Bedingung) {
        // ...
    }
    ```
-   **Geschweifte Klammern für Kontrollblöcke:** Verwende immer geschweifte Klammern (`{}`) für Kontrollflussblöcke (`if`, `else`, `for`, `while`, `do`, `using`), auch wenn der Block nur eine einzige Anweisung enthält.
    ```csharp
    // Gut
    if (Bedingung)
    {
        TuEtwas();
    }

    // Schlecht
    if (Bedingung)
        TuEtwas();
    ```
-   **Neue Zeile vor `catch`, `else`, `finally`:** Platziere die Schlüsselwörter `catch`, `else` und `finally` in einer neuen Zeile.
    ```csharp
    try
    {
        // ...
    }
    catch (Exception ex)
    {
        // ...
    }
    finally
    {
        // ...
    }
    ```
-   **Platzierung von Operatoren:** Platziere Operatoren am Anfang der neuen Zeile, wenn Zeilen umbrochen werden.
    ```csharp
    var langerName = teilEins
                    + teilZwei
                    + teilDrei;
    ```

### Zeilenumbruch
-   **Einzeilige Blöcke/Anweisungen beibehalten:** Behalte Blöcke und Anweisungen in einer Zeile bei, wenn sie ursprünglich so geschrieben wurden.
-   **Leerzeilen:** Mehrere Leerzeilen sind zulässig.

## Namenskonventionen

-   **Allgemeine Typen (Klassen, Strukturen, Enums, Delegaten):** Verwende `PascalCase`.
-   **Interfaces:** Verwende `PascalCase` und stelle ein `I` voran.
    -   Beispiel: `IUserService`
-   **Methoden:** Verwende `PascalCase`.
-   **Eigenschaften & Ereignisse:** Verwende `PascalCase`.
-   **Öffentliche/Interne Felder:** Verwende `PascalCase` (obwohl Eigenschaften generell bevorzugt werden).
-   **Private/Protected Felder:** Verwende `_camelCase` (camelCase mit einem vorangestellten Unterstrich).
    -   Beispiel: `_connectionString`
    ```csharp
    public class BeispielKlasse
    {
        private readonly string _wichtigeDaten; // Gut
        private string _tempWert;        // Gut

        public BeispielKlasse(string daten)
        {
            _wichtigeDaten = daten;
        }
    }
    ```
-   **Lokale Variablen & Parameter:** Verwende `camelCase`.
-   **Konstanten:** Verwende `PascalCase`.
-   **Namespace-Benennung:** Namespaces sollten der Ordnerstruktur entsprechen.

## Sprachfeatures & Stil

### Modifikatoren
-   **Zugriffsmodifikatoren:** Gib Zugriffsmodifikatoren (`public`, `private`, `protected`, `internal`) explizit für alle Typen und Nicht-Interface-Member an.
    ```csharp
    // Gut
    public string Name { get; private set; }
    internal void InterneMethode() { /* ... */ }
    private int _zaehler;

    // Schlecht (implizit internal/private)
    // string Name { get; set; }
    // void InterneMethode() { /* ... */ }
    // int _zaehler;
    ```
-   **Reihenfolge der Modifikatoren:** Befolge die festgelegte Reihenfolge: `public`/`private`/`protected`/`internal`/`file`, `static`, `extern`, `new`, `virtual`, `abstract`, `sealed`, `override`, `readonly`, `unsafe`, `required`, `volatile`, `async`.
    ```csharp
    public static async Task MeineMethodeAsync() { /* ... */ }
    protected internal sealed override void AndereMethode() { /* ... */ }
    ```
-   **Readonly:** Kennzeichne Felder, die nur im Konstruktor oder über einen Initialisierer zugewiesen werden, als `readonly`.
    ```csharp
    private readonly ILogger _logger;
    public MyService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    ```
    -   Bevorzuge `readonly struct`, wo anwendbar.
        ```csharp
        public readonly struct Point
        {
            public int X { get; }
            public int Y { get; }

            public Point(int x, int y) => (X, Y) = (x, y);
        }
        ```

### `using`-Direktiven
-   **Platzierung:** Platziere `using`-Direktiven außerhalb der Namespace-Deklaration.
    ```csharp
    // Gut
    using System;
    using System.Collections.Generic;

    namespace MeinNamespace
    {
        // ...
    }

    // Schlecht
    // namespace MeinNamespace
    // {
    //     using System;
    //     using System.Collections.Generic;
    //     // ...
    // }
    ```
-   **Organisation:** Sortierung und Gruppierung werden nicht streng erzwungen, aber halte sie organisiert, typischerweise am Anfang der Datei.
-   **Einfache `using`-Anweisung:** Bevorzuge die einfachere `using`-Deklaration ohne geschweifte Klammern, wenn der Geltungsbereich bis zum Ende des Blocks reicht.
    -   Beispiel: `using var reader = new StreamReader(path);`
    ```csharp
    // Gut
    public void VerarbeiteDatei(string pfad)
    {
        using var reader = new StreamReader(pfad);
        // Arbeite mit reader...
    } // reader wird hier automatisch disposed

    // Akzeptabel (aber weniger bevorzugt)
    public void VerarbeiteDateiAlt(string pfad)
    {
        using (var reader = new StreamReader(pfad))
        {
            // Arbeite mit reader...
        } // reader wird hier disposed
    }
    ```

### `var`-Schlüsselwort
-   Verwende `var`, wenn der Typ aus der rechten Seite der Zuweisung ersichtlich ist oder für eingebaute Typen.
    ```csharp
    // Gut
    var anzahl = 5; // Typ int ist klar
    var name = "Beispiel"; // Typ string ist klar
    var zahlenListe = new List<int>(); // Typ List<int> ist klar
    var benutzer = userService.FindeBenutzer(id); // Typ ist möglicherweise komplex, aber aus dem Kontext (Methodenrückgabe) ersichtlich
    ```
-   Verwende `var` **nicht**, wenn es den zugrunde liegenden Typ verschleiert oder die Klarheit verringert.
    ```csharp
    // Schlecht - Welcher Typ ist das Ergebnis? Besser explizit angeben.
    // var ergebnis = LegacyApi.HoleDaten();
    // Stattdessen:
    LegacyDatenTyp ergebnis = LegacyApi.HoleDaten();

    // Schlecht - `default` kann mehrdeutig sein ohne Typangabe
    // var wert = default;
    // Stattdessen:
    int wert = default;
    ```

### Expression-Bodied Members
-   Verwende Expression-Bodied Members für Konstruktoren, Accessoren, Indexer, Operatoren, Lambdas und lokale Funktionen *nur*, wenn sie in eine einzige Zeile passen.
    ```csharp
    // Gut (Eigenschaft)
    public string VollerName => $"{Vorname} {Nachname}";

    // Gut (Konstruktor)
    public Person(string name) => Name = name;

    // Gut (Lokale Funktion)
    void Beispiel()
    {
        int Addiere(int a, int b) => a + b;
        var summe = Addiere(1, 2);
    }
    ```
-   **Vermeide** Expression Bodies für reguläre Methoden.
    ```csharp
    // Gut
    public int BerechneSumme(IEnumerable<int> zahlen)
    {
        // Mehrere Zeilen Logik oder komplexere Logik
        var summe = 0;
        foreach (var zahl in zahlen)
        {
            summe += zahl;
        }
        return summe;
    }

    // Schlecht (selbst wenn es technisch passt, für Methoden vermeiden)
    // public int BerechneSumme(IEnumerable<int> zahlen) => zahlen.Sum();
    ```

### Null-Behandlung
-   Bevorzuge `is null`- und `is not null`-Prüfungen gegenüber Referenzgleichheit (`== null`, `!= null`).
    ```csharp
    // Gut
    if (kunde is not null) { /* ... */ }
    if (auftrag is null) { /* ... */ }

    // Weniger bevorzugt
    // if (kunde != null) { /* ... */ }
    // if (auftrag == null) { /* ... */ }
    ```
-   Bevorzuge Null-bedingte Operatoren (`?.`, `?[]`).
    ```csharp
    // Gut
    var strasse = kunde?.Adresse?.Strasse; // Sicherer Zugriff
    var erstesElement = liste?[0];        // Sicherer Zugriff

    // Statt
    // var strasse = (kunde != null && kunde.Adresse != null) ? kunde.Adresse.Strasse : null;
    ```
-   Bevorzuge den Null-Sammeloperator (`??`).
    ```csharp
    // Gut
    var name = benutzer?.Name ?? "Unbekannt"; // Standardwert, falls null

    // Statt
    // var name = (benutzer != null && benutzer.Name != null) ? benutzer.Name : "Unbekannt";
    ```
-   Bevorzuge Pattern Matching gegenüber `as` mit Null-Prüfungen (`if (obj is Type variable)`).
    ```csharp
    // Gut
    if (form is Rechteck r)
    {
        // Arbeite mit r vom Typ Rechteck
    }

    // Weniger bevorzugt
    // var r = form as Rechteck;
    // if (r != null) { /* ... */ }
    ```
-   Bevorzuge Pattern Matching gegenüber `is` mit Cast-Prüfungen (`if (obj is Type variable)`).
    ```csharp
    // Gut (identisch zum vorherigen Beispiel)
    if (form is Rechteck r)
    {
        // Arbeite mit r vom Typ Rechteck
    }

    // Weniger bevorzugt
    // if (form is Rechteck)
    // {
    //     var r = (Rechteck)form;
    //     // ...
    // }
    ```

### Objekt- & Sammlungsinitialisierer
-   Bevorzuge Objektinitialisierer (`new Point { X = 0, Y = 1 }`).
    ```csharp
    // Gut
    var kunde = new Kunde
    {
        Id = 1,
        Name = "Testkunde",
        IstAktiv = true
    };

    // Weniger bevorzugt
    // var kunde = new Kunde();
    // kunde.Id = 1;
    // kunde.Name = "Testkunde";
    // kunde.IstAktiv = true;
    ```
-   Bevorzuge Sammlungsinitialisierer (`new List<int> { 1, 2, 3 }`).
    ```csharp
    // Gut
    var zahlen = new List<int> { 1, 1, 2, 3, 5 };
    var woerterbuch = new Dictionary<string, int>
    {
        ["eins"] = 1,
        ["zwei"] = 2
    };

    // Weniger bevorzugt
    // var zahlen = new List<int>();
    // zahlen.Add(1);
    // zahlen.Add(1);
    // ...
    ```

### Andere Präferenzen
-   **Eingebaute Typen:** Bevorzuge Sprachschlüsselwörter für Typnamen (z. B. `int` statt `System.Int32`).
    ```csharp
    // Gut
    int zaehler = 0;
    string nachricht = "Hallo";
    bool istFertig = false;

    // Weniger bevorzugt
    // Int32 zaehler = 0;
    // String nachricht = "Hallo";
    // Boolean istFertig = false;
    ```
-   **Klammern:** Verwende Klammern zur Verdeutlichung der Rangfolge bei arithmetischen, relationalen und anderen binären Operationen.
-   **Zusammengesetzte Zuweisungen:** Bevorzuge zusammengesetzte Zuweisungen (z. B. `+=`, `-=`).
-   **Vereinfachte boolesche Ausdrücke:** Vereinfache boolesche Ausdrücke, wo möglich (z. B. `return x > 5;` statt `if (x > 5) return true; else return false;`).
    ```csharp
    // Gut
    public bool IstGross(int wert)
    {
        return wert > 100;
    }

    // Schlecht
    // public bool IstGross(int wert)
    // {
    //     if (wert > 100)
    //     {
    //         return true;
    //     }
    //     else
    //     {
    //         return false;
    //     }
    // }
    ```
-   **Vereinfachte Interpolation:** Verwende vereinfachte String-Interpolation.
    ```csharp
    // Gut
    var nachricht = $"Hallo {name}, dein Kontostand ist {kontostand:C}";

    // Weniger bevorzugt (mit String.Format)
    // var nachricht = String.Format("Hallo {0}, dein Kontostand ist {1:C}", name, kontostand);
    ```
-   **Tupelnamen:** Bevorzuge explizite Tupel-Elementnamen (`(int count, string name)`).
    ```csharp
    // Gut
    public (int anzahl, double summe) BerechneStatistik(IEnumerable<double> daten)
    {
        // ...
        return (daten.Count(), daten.Sum());
    }
    var (anz, sm) = BerechneStatistik(meineDaten);
    Console.WriteLine($"Anzahl: {anz}, Summe: {sm}");

    // Weniger bevorzugt (implizite Namen Item1, Item2)
    // public Tuple<int, double> BerechneStatistik(IEnumerable<double> daten) { ... }
    // var statistik = BerechneStatistik(meineDaten);
    // Console.WriteLine($"Anzahl: {statistik.Item1}, Summe: {statistik.Item2}");
    ```
-   **Abgeleitete Namen:** Bevorzuge abgeleitete Namen für Tupel-Elemente und anonyme Typmember, wenn möglich.
    ```csharp
    // Gut (Namen werden von Variablen abgeleitet)
    int anzahl = 5;
    string name = "Produkt";
    var tupel = (anzahl, name); // Elemente heißen 'anzahl' und 'name'
    Console.WriteLine(tupel.name);

    var anonTyp = new { anzahl, name }; // Member heißen 'anzahl' und 'name'
    Console.WriteLine(anonTyp.anzahl);
    ```
-   **`this.`-Qualifizierung:** Vermeide unnötige Qualifizierung mit `this.` für Felder, Eigenschaften, Methoden und Ereignisse.
    ```csharp
    public class Rechner
    {
        private int _basisWert = 10;

        public int Addiere(int wert)
        {
            // Gut (kein 'this.' nötig)
            return _basisWert + wert;
        }

        public void SetzeBasisWert(int basisWert)
        {
            // Notwendig zur Unterscheidung von Parameter und Feld
            this._basisWert = basisWert;
        }
    }
    ```
-   **Namespace-Deklarationen:** Verwende block-scoped Namespaces (`namespace MyNamespace { ... }`) anstelle von file-scoped.
    ```csharp
    // Bevorzugt
    namespace MeinProjekt.MeinFeature
    {
        public class MeineKlasse
        {
            // ...
        }
    }

    // Weniger bevorzugt (File-scoped, seit C# 10)
    // namespace MeinProjekt.MeinFeature;
    //
    // public class MeineKlasse
    // {
    //    // ...
    // }
    ```
-   **Top-Level Statements:** Erlaubt.
-   **Index/Range-Operatoren:** Bevorzuge Index- (`^1`) und Range- (`..`) Operatoren.
    ```csharp
    int[] zahlen = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    // Gut
    var letztesElement = zahlen[^1]; // 9
    var ersteDrei = zahlen[..3];    // 0, 1, 2
    var vonIndex2Bis4 = zahlen[2..5]; // 2, 3, 4 (Index 5 ist exklusiv)
    var ohneErsteUndLetzte = zahlen[1..^1]; // 1, 2, 3, 4, 5, 6, 7, 8

    // Weniger bevorzugt
    // var letztesElement = zahlen[zahlen.Length - 1];
    // var ersteDrei = zahlen.Take(3).ToArray();
    ```
-   **Switch Expressions:** Bevorzuge Switch Expressions gegenüber Switch Statements, wo angebracht.
    ```csharp
    // Gut (Switch Expression)
    public static string GetQuadrant(Point p) => p switch
    {
        { X: > 0, Y: > 0 } => "Oben Rechts",
        { X: < 0, Y: > 0 } => "Oben Links",
        { X: < 0, Y: < 0 } => "Unten Links",
        { X: > 0, Y: < 0 } => "Unten Rechts",
        { X: 0, Y: _ } => "Auf Y-Achse",
        { X: _, Y: 0 } => "Auf X-Achse",
        _ => "Ursprung"
    };

    // Weniger bevorzugt (Switch Statement)
    // public static string GetQuadrant(Point p)
    // {
    //     switch (p)
    //     {
    //         case { X: > 0, Y: > 0 }: return "Oben Rechts";
    //         // ... andere Fälle ...
    //         default: return "Ursprung";
    //     }
    // }
    ```
-   **Throw Expressions:** Bevorzuge Throw Expressions (`=> throw new ArgumentNullException(...)`).
    ```csharp
    // Gut
    public string Name
    {
        get => _name;
        set => _name = value ?? throw new ArgumentNullException(nameof(value));
    }
    private string _name;

    public MyService(ILogger logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ILogger _logger;

    // Weniger bevorzugt
    // public string Name
    // {
    //     get { return _name; }
    //     set
    //     {
    //         if (value == null)
    //             throw new ArgumentNullException(nameof(value));
    //         _name = value;
    //     }
    // }
    ```
-   **Unbenutzte Werte:** Verwende Discards (`_`) für unbenutzte Parameter und Zuweisungsergebnisse.
    ```csharp
    // Gut (unbenutzter Parameter)
    public void HandleEvent(object sender, EventArgs _)
    {
        Console.WriteLine("Ereignis empfangen.");
    }

    // Gut (unbenutztes Ergebnis einer Methode, die ein Tupel zurückgibt)
    var (_, fehlerCode) = OperationMitStatus();
    if (fehlerCode != 0) { /* ... */ }

    // Gut (out-Parameter, der nicht benötigt wird)
    if (int.TryParse(eingabe, out _))
    {
        Console.WriteLine("Ist eine Zahl");
    }
    ```

### Exception Handling

-   **Spezifische Exceptions:** Fange und wirf spezifische Exceptions statt generische `Exception`-Typen zu verwenden.
    ```csharp
    // Gut
    try
    {
        // Operationen mit Dateien
    }
    catch (FileNotFoundException ex)
    {
        _logger.LogError(ex, "Die angeforderte Datei wurde nicht gefunden");
        // Spezifische Behandlung
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.LogError(ex, "Keine Berechtigung für den Dateizugriff");
        // Spezifische Behandlung
    }

    // Schlecht
    // try
    // {
    //     // Operation
    // }
    // catch (Exception ex)
    // {
    //     // Generische Behandlung für alle Exceptions
    // }
    ```

-   **Exception-Propagation:** Fange Exceptions nur, wenn du sie auch behandeln kannst. Lass Exceptions ansonsten nach oben propagieren.
    ```csharp
    // Gut - Exceptions werden behandelt
    public bool VersucheOperation()
    {
        try
        {
            FühreOperationAus();
            return true;
        }
        catch (SpecificException ex)
        {
            _logger.LogWarning(ex, "Operation fehlgeschlagen, aber behandelbar");
            return false;
        }
    }

    // Gut - Unbehandelte Exceptions werden weitergeleitet
    public void FühreOperationAus()
    {
        // Kann Exceptions werfen, die nicht gefangen werden
    }

    // Schlecht - Leerer catch-Block oder nur Logging ohne echte Behandlung
    // public void SchlechteBehandlung()
    // {
    //     try
    //     {
    //         // Operation
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Fehler aufgetreten");
    //         // Keine eigentliche Behandlung
    //     }
    // }
    ```

-   **Exception-Parameter:** Benenne Exception-Parameter explizit und aussagekräftig (`ex` statt `e` oder andere Abkürzungen).
    ```csharp
    // Gut
    try
    {
        // Operation
    }
    catch (ArgumentException ex)
    {
        // Verwende ex für Logging, Details, etc.
    }
    ```

-   **Eigene Exception-Typen:** Erstelle eigene Exception-Typen für domänenspezifische Fehler und folge diesen Konventionen:
    -   Klassenname endet mit `Exception`
    -   Erbt von `Exception` oder einer spezifischeren Ausnahme
    -   Implementiere mindestens drei Konstruktoren: parameterlos, mit Nachricht, mit Nachricht und innerException
    -   Markiere die Klasse als `[Serializable]`
    ```csharp
    [Serializable]
    public class BestellungNichtGefundenException : Exception
    {
        public BestellungNichtGefundenException() 
            : base() { }

        public BestellungNichtGefundenException(string message) 
            : base(message) { }

        public BestellungNichtGefundenException(string message, Exception innerException) 
            : base(message, innerException) { }

        // Optional: Unterstützung für Serialisierung
        protected BestellungNichtGefundenException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
    }
    ```

-   **Exception-Nachrichten:** Stelle sicher, dass Exception-Nachrichten hilfreich sind und Kontext bieten. Erwäge die Verwendung von `nameof()` für Parameter-Namen.
    ```csharp
    // Gut
    if (wert < 0)
    {
        throw new ArgumentOutOfRangeException(
            nameof(wert), 
            wert, 
            "Der Wert muss positiv sein.");
    }

    // Schlecht
    // if (wert < 0)
    // {
    //     throw new ArgumentException("Ungültiger Wert");
    // }
    ```

-   **Exception-Hierarchie:** Respektiere die hierarchische Natur von Exceptions. Fange spezifischere Exceptions vor allgemeineren.
    ```csharp
    // Gut
    try
    {
        // Operation
    }
    catch (ArgumentNullException ex)
    {
        // Behandlung für null-Argumente
    }
    catch (ArgumentException ex)
    {
        // Behandlung für andere Argument-Probleme
    }
    catch (Exception ex) when (SollteBehandeltWerden(ex))
    {
        // Bedingte Behandlung für andere Exceptions
    }
    ```

-   **Exception-Filter:** Nutze Exception-Filter (`when`-Klausel), um Exceptions basierend auf Bedingungen zu fangen.
    ```csharp
    try
    {
        // HTTP-Anfrage
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        // Behandlung spezifisch für 404-Fehler
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
        // Behandlung spezifisch für 401-Fehler
    }
    ```

### Asynchrone Programmierung

-   **Namenskonvention:** Asynchrone Methoden sollten mit dem Suffix `Async` enden.
    ```csharp
    // Gut
    public async Task<Benutzer> LadeBenutzerAsync(int id)
    {
        // Asynchrone Implementierung
    }

    // Schlecht
    // public async Task<Benutzer> LadeBenutzer(int id)
    // { ... }
    ```

-   **Rückgabetypen:** Verwende `Task<T>` für asynchrone Methoden, die einen Wert zurückgeben, und `Task` für Methoden ohne Rückgabewert.
    ```csharp
    // Mit Rückgabewert
    public async Task<List<Produkt>> HoleProduktlisteAsync()
    {
        // Asynchrone Implementierung
        return await _repository.GetAllAsync();
    }

    // Ohne Rückgabewert
    public async Task SpeichereÄnderungenAsync()
    {
        await _context.SaveChangesAsync();
    }
    ```

-   **Synchrone Wrapper vermeiden:** Blockiere nicht asynchronen Code mit `.Result`, `.Wait()` oder ähnlichen Methoden, da dies zu Deadlocks führen kann.
    ```csharp
    // Schlecht - kann zu Deadlocks führen
    // public Benutzer LadeBenutzer(int id)
    // {
    //     return LadeBenutzerAsync(id).Result; // Potenzieller Deadlock!
    // }

    // Gut - Asynchronität wird durchgängig verwendet
    public async Task<Benutzer> LadeBenutzerAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
    ```

-   **ConfigureAwait:** Verwende `.ConfigureAwait(false)` in Bibliothekscode, um den Synchronisationskontext nicht zu erfassen.
    ```csharp
    // In Bibliotheksklassen
    public async Task<Daten> LadeDatenAusDbAsync()
    {
        // ConfigureAwait(false) verhindert das Zurückkehren zum ursprünglichen Kontext
        var rohdaten = await _dbConnection.QueryAsync<RohDaten>().ConfigureAwait(false);
        return Konvertiere(rohdaten);
    }
    ```

-   **Async Main:** Verwende asynchrones Main für Konsolenprogramme, die asynchrone Operationen ausführen.
    ```csharp
    public static async Task Main(string[] args)
    {
        await InitialisiereAnwendungAsync();
        // Weitere asynchrone Operationen
    }
    ```

-   **Asynchrone Streams:** Nutze `IAsyncEnumerable<T>` und `await foreach` für asynchrone Sequenzen von Daten.
    ```csharp
    public async IAsyncEnumerable<Benutzer> StreameBenutzerAsync()
    {
        // Asynchrone Implementierung mit yield return
        foreach (var id in await _benutzerIds.ToListAsync())
        {
            yield return await _repository.GetByIdAsync(id);
        }
    }

    public async Task VerarbeiteBenutzerAsync()
    {
        await foreach (var benutzer in StreameBenutzerAsync())
        {
            // Verarbeite jeden Benutzer einzeln
        }
    }
    ```

-   **Verschachtelte Tasks vermeiden:** Vermeide das Erstellen verschachtelter Tasks, die zu komplexem und fehleranfälligem Code führen können.
    ```csharp
    // Schlecht - verschachtelte Tasks
    // public Task<int> BerechneWertAsync()
    // {
    //     return Task.Run(() => {
    //         var task = _service.GetValueAsync();
    //         return task.Result * 2; // Potenzieller Deadlock!
    //     });
    // }

    // Gut - flache Task-Struktur
    public async Task<int> BerechneWertAsync()
    {
        var wert = await _service.GetValueAsync();
        return wert * 2;
    }
    ```

-   **Parallelität mit Task:** Nutze `Task.WhenAll` und `Task.WhenAny` für parallele asynchrone Operationen.
    ```csharp
    // Parallele Ausführung mehrerer asynchroner Operationen
    public async Task<(Benutzer Benutzer, List<Bestellung> Bestellungen)> LadeBenutzerMitBestellungenAsync(int benutzerId)
    {
        var benutzerTask = _benutzerRepository.GetByIdAsync(benutzerId);
        var bestellungenTask = _bestellungRepository.GetForUserAsync(benutzerId);
        
        await Task.WhenAll(benutzerTask, bestellungenTask);
        
        return (benutzerTask.Result, bestellungenTask.Result);
    }
    ```

## Kommentare

-   **Minimalistischer Ansatz:** Füge Kommentare nur dann hinzu, wenn der Code strukturell oder algorithmisch schwer zu verstehen ist.
-   **Selbsterklärender Code:** Viele Kommentare werden überflüssig, wenn Funktions- und Variablennamen selbsterklärend sind. Wähle aussagekräftige Namen statt umfangreiche Kommentare zu schreiben.
-   **XML-Dokumentationskommentare (`///`):** 
    -   Verwende XML-Kommentare (Summary) primär für:
        -   Öffentliche Funktionen und Properties, die bibliotheksübergreifend verwendet werden
        -   Funktionen, die sehr komplexe Aufgaben erledigen
    -   Für einfache Funktionen, deren Logik auf einen Blick erfassbar ist oder deren Zweck sich aus dem Namen ergibt, sind XML-Kommentare nicht erforderlich.
    ```csharp
    // Gut: Komplexe Funktion mit XML-Kommentar
    /// <summary>
    /// Berechnet den kürzesten Pfad zwischen zwei Knoten im Graphen unter Berücksichtigung
    /// von Gewichtungen und Einschränkungen. Implementiert den A*-Algorithmus mit 
    /// angepasster Heuristik.
    /// </summary>
    /// <param name="startKnoten">Der Startknoten für die Pfadberechnung</param>
    /// <param name="zielKnoten">Der Zielknoten für die Pfadberechnung</param>
    /// <param name="einschraenkungen">Optionale Einschränkungen für die Pfadsuche</param>
    /// <returns>Den kürzesten Pfad oder null, wenn kein Pfad existiert</returns>
    public List<Knoten> BerechneKürzestenPfad(Knoten startKnoten, Knoten zielKnoten, Einschraenkungen einschraenkungen = null)
    {
        // Komplexe Implementierung...
    }
    
    // Gut: Einfache Funktion ohne XML-Kommentar
    public bool IstLeer()
    {
        return _elemente.Count == 0;
    }
    ```
-   **Inline-Kommentare (`//`):** Verwende Inline-Kommentare sparsam und nur, um nicht-offensichtliche Entscheidungen oder Implementierungsdetails zu erklären.
    ```csharp
    // Gut: Erklärt eine nicht-offensichtliche Entscheidung
    if (wert > 100)
    {
        // Werte über 100 werden gesondert behandelt, da Spezifikation Abschnitt 3.2.1 
        // Sonderfälle für große Eingaben definiert
        return BehandleGroßeWerte(wert);
    }
    
    // Schlecht: Redundanter Kommentar, der nur das Offensichtliche beschreibt
    // Addiert die Zahlen 1 und 2
    // var summe = 1 + 2;
    ```

## Klassenstruktur

Die Reihenfolge der Member innerhalb einer Klasse (z. B. Felder, Konstruktoren, Eigenschaften, Methoden) ist nicht explizit definiert. Gängige C#-Konventionen legen jedoch die folgende Reihenfolge nahe, die generell zur Konsistenz eingehalten werden sollte:

1.  Statische Felder (Konstanten zuerst, dann statische readonly, dann statische read/write)
2.  Instanzfelder (Readonly zuerst, dann read/write)
3.  Statische Konstruktoren
4.  Instanzkonstruktoren
5.  Statische Eigenschaften
6.  Instanzeigenschaften
7.  Statische Methoden
8.  Instanzmethoden
9.  Verschachtelte Typen

Gruppiere Member nach Zugänglichkeit (z. B. `public` vor `protected`, `internal`, `private`), obwohl dies der Reihenfolge der Membertypen untergeordnet ist.
```csharp
public class BeispielKlasse
{
    // 1. Statische Felder (Konstanten, static readonly, static read/write)
    public const int MAX_WERT = 100;
    private static readonly Random _zufall = new Random();
    private static int _globalerZaehler = 0;

    // 2. Instanzfelder (readonly, read/write)
    private readonly string _id;
    private List<string> _elemente;

    // 3. Statische Konstruktoren
    static BeispielKlasse()
    {
        // Statische Initialisierung
    }

    // 4. Instanzkonstruktoren
    public BeispielKlasse(string id)
    {
        _id = id ?? Guid.NewGuid().ToString();
        _elemente = new List<string>();
    }

    // 5. Statische Eigenschaften
    public static int GlobalerZaehler => _globalerZaehler;

    // 6. Instanzeigenschaften
    public string Id => _id;
    public int AnzahlElemente => _elemente.Count;

    // 7. Statische Methoden (public zuerst)
    public static void InkrementiereGlobalenZaehler()
    {
        Interlocked.Increment(ref _globalerZaehler);
    }

    // 8. Instanzmethoden (public zuerst)
    public void FuegeElementHinzu(string element)
    {
        if (!string.IsNullOrEmpty(element))
        {
            _elemente.Add(element);
        }
    }

    protected virtual void InterneVerarbeitung()
    {
        // ...
    }

    private bool IstListeLeer()
    {
        return _elemente.Count == 0;
    }

    // 9. Verschachtelte Typen
    public class VerschachtelteKlasse
    {
        // ...
    }

    private enum InternerStatus
    {
        Neu,
        Verarbeitet
    }
}
```
