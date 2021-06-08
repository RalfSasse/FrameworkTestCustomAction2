/******************************************************************************
 * FrameworkTestCustomAction2.cs
 * Projekt FrameworkTest / CustomAction für WiX
 * Datum: 08.06.2021
 * Autor: Ralf Sasse
 * 
 ******************************************************************************/

using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;

using System.Diagnostics;                   // für Objekte der Klassen Process und ProcessStartInfo
using System.Text.RegularExpressions;

namespace FrameworkTest
{
    public class CustomActions
    {
        public static string nameVersion = String.Empty;
        public static string nameServicePack = String.Empty;

        public static string output = String.Empty;            // Leerer String für die Ausgabe
        public static string versionValue = String.Empty;      // leerer String für die Versionsnummer
        public static string foundVersionValue = String.Empty; // leerer String für die Versionsnummer

        [CustomAction]
        public static ActionResult FrameworkTest(Session session)
        {
            // Vorbelegung der WiX-Variablen für den Fall, dass kein Framework gefunden wird.

            session["DOTNETFRAMEWORKVORHANDEN"] = ".NET Framework nicht vorhanden.";
            session["DOTNETCOREVORHANDEN"]      = ".NET Core nicht vorhanden.";

            int releaseKey = 0;                 // nur für .NET Framework-Versionen ab 4.5

            // Registry öffnen und Versionsnummern für .NET Framework suchen.
            // Für Versionen bis 4 muss ein anderes Verfahren benutzt werden als für neuere Versionen.

            // .NET Framework Versionen bis 4 ermitteln:
            // Hier werden alle in der Registry gefundenen Versionen innerhalb einer Schleife abgearbeitet.
            // Versionen über 4 werden übersprungen und weiter unten behandelt.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                foreach (string dotNetFrameworkVorhandenKey in registryKey.GetSubKeyNames())
                {
                    if (dotNetFrameworkVorhandenKey == "v4")            // .NET Framework Version >= 4.5
                    {                                                   // (werden weiter unten behandelt)
                        continue;
                    }

                    if (dotNetFrameworkVorhandenKey.StartsWith("v"))      // .NET Framework Version <= 4
                    {
                        RegistryKey versionKey = registryKey.OpenSubKey(dotNetFrameworkVorhandenKey);

                        // .NET Framework Versionsnummer ermitteln:

                        nameVersion = (string)versionKey.GetValue("Version", "");

                        if (nameVersion != "")
                        {
                            // Bei Versionen bis 4 sind nur die ersten drei Stellen nötig (z.B. "2.0" oder "3.5").

                            nameVersion = nameVersion.Substring(0, 3);

                            // ServicePack-Nummer ermitteln:

                            nameServicePack = versionKey.GetValue("SP", "").ToString();

                            // Session-Variable mit der höchsten Versionsnummer bis einschließlich 4 füllen.
                            // Wird weiter unten überschrieben, falls eine neuere Version gefunden wird.

                            session["DOTNETFRAMEWORKVORHANDEN"] = ".NET Framework " + nameVersion + ", SP " + nameServicePack;
                        }
                    }
                }
            }

            // .NET Framework Versionen ab 4.5 ermitteln:
            // Hier ist keine Schleife erforderlich, denn die Registry enthält nur maximal einen Release-Key.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                if (registryKey != null && registryKey.GetValue("Release") != null)
                {
                    releaseKey = (int)registryKey.GetValue("Release");

                    if (releaseKey >= 528040)
                    {
                        versionValue = "4.8";
                        session["DOTNETFRAMEWORK48"] = "1";     // grünes statt rotes Icon im WiX-Dialog anzeigen
                    }
                    else if (releaseKey >= 461808) versionValue = "4.7.2";
                    else if (releaseKey >= 461308) versionValue = "4.7.1";
                    else if (releaseKey >= 460798) versionValue = "4.7";
                    else if (releaseKey >= 394802) versionValue = "4.6.2";
                    else if (releaseKey >= 394254) versionValue = "4.6.1";
                    else if (releaseKey >= 393295) versionValue = "4.6";
                    else if (releaseKey >= 379893) versionValue = "4.5.2";
                    else if (releaseKey >= 378675) versionValue = "4.5.1";
                    else if (releaseKey >= 378389) versionValue = "4.5";

                    session["DOTNETFRAMEWORKVORHANDEN"] = ".NET Framework " + versionValue;
                }
            }

            // Versionsnummer für .NET Core suchen:
            // Hier werden die vorhandenen Versionen mit dem Kommandozeilen-Befehl "dotnet" ermittelt.
            // Dieser Befehl gibt eine Liste aus, die zeilenweise nach Versionsnummern durchsucht wird.

            output = string.Empty;          // Leerer String für die Ausgabe
            versionValue = string.Empty;    // leerer String für die Versionsnummer

            Version version        = new Version(0, 0);
            Version minVersion     = new Version(3, 1, 15);
            Version tooHighVersion = new Version(5, 0);

            // String für den Programm-Aufruf an der Kommandozeile, hier also
            //   cmd /c dotnet --list-runtimes
            // Der Parameter /c ist wichtig - dadurch wird die Kommandozeile nach der
            // Ausführung des dotnet-Kommandos automatisch wieder beendet.

            var command = "/c dotnet --list-runtimes";

            // An dieser Stelle wird der Start des Kommandozeilen-Prozesses vorbereitet:
            // Ein Objekt der Klasse Process wird erzeugt, das als Eigenschaft u.a. ein
            // Objekt der Klasse ProcessStartInfo besitzt. Dieses wird mit den gewünschten
            // Werten initialisiert.

            using (var p = new Process())
            {
                p.StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                p.Start();          // Prozess wird gestartet (hier: cmd.exe, also die Kommandozeile)

                // Die Ausgabe des Kommandozeilen-Programms "dotnet --list-runtimes" besteht
                // (normalerweise) aus mehreren Zeilen, die jetzt innerhalb einer Schleife
                // in die String-Variable "output" eingelesen werden, jeweils gefolgt von einem
                // Zeilenumbruch. Die Eigenschaft "NewLine" der Klasse "Environment" enthält das
                // (bzw. die) Steuerzeichen für den Zeilenumbruch, abhängig vom Betriebssystem:
                // "\r\n" für Windows, "\n" für Linux und MacOSX oder "\r" für ältere MacOS-Versionen.

                while (!p.StandardOutput.EndOfStream)
                {
                    output += $"{p.StandardOutput.ReadLine()}{Environment.NewLine}";
                }

                p.WaitForExit();    // Warten auf Beendigung des Prozesses

                // Auch wenn der Prozess nicht erfolgreich beendet wurde (dann wäre der ExitCode = 0)
                // wird als Rückgabewert "Success" zurückgegeben. Die eigentlichen Ergebnisse dieser
                // CustomAction stehen dann trotzdem in den WiX-Properties.

                if (p.ExitCode != 0)
                {
                    return ActionResult.Success;
                }

                // In die Session-Variable "DOTNETCOREVORHANDEN" wird ein String mit der gefundenen
                // Versionsnummer geschrieben, der dann im Dialogfenster angezeigt wird.

                string[] runtimeLines = output.Split('\n');     // output in einzelne Strings teilen; einer pro Zeile

                Regex pattern = new Regex(@"\d+(\.\d+)+");      // regulärer Ausdruck für Versionsnummern. Gesucht werden
                                                                // mehrere Zahlen, die durch Punkte getrennt sind.

                // Die folgende Schleife wird einmal für jeden String durchlaufen
                // (also einmal für jede Zeile der Ausgabe des "dotnet"-Befehls). 

                foreach (string runtimeLine in runtimeLines)
                {
                    if (runtimeLine.Contains("Microsoft.NETCore.App"))      // nur Zeilen, die "Microsoft.NETCore.App" enthalten
                    {
                        Match m = pattern.Match(runtimeLine);               // prüfen, ob Versionsnummer enthalten ist

                        foundVersionValue = m.Value;                        // Versionsnummer in String-Objekt einlesen

                        // An dieser Stelle stand ursprünglich "Version.TryParse(foundVersionValue, out foundVersion)".
                        // Das funktioniert aber nur mit neueren .NET Framework-Versionen. Ich hatte zunächst diese
                        // CustomAction für .NET Framework 4.8 kompiliert, dort funktionierte Version.TryParse() auch.
                        // Allerdings nur, wenn 4.8 auch auf dem Zielrechner installiert war, sonst wurde das Programm
                        // ohne Fehlermeldung abgebrochen.
                        // Die vorliegende Version "FrameworkTestCustonAction2" ist für .NET Framework 2.0 kompiliert
                        // und liefert ein korrektes Ergebnis, wenn auf dem Zielrechner 2.0 oder höher installiert ist.
                        // Statt der "Version.TryParse()"-Methode habe ich hier einen überladenen Konstruktor der Klasse
                        // "Version" verwendet, der als Argument einen String im Format "3.1.15" akzeptiert.

                        Version foundVersion = new Version(foundVersionValue);

                        // Die Versionsnummer soll gespeichert werden, wenn sie größer ist als die zuletzt gefundenen,
                        // aber kleiner als 5 (das wäre nicht mehr .NET Core, sondern .NET 5, und danach wird nicht gesucht.
                        // Der Kommandozeilen-Befehl "dotnet" unterscheidet nicht zwischen .NET Core und .NET 5.

                        if ((foundVersion > version) && (foundVersion < tooHighVersion))
                        {
                            version = foundVersion;
                            versionValue = foundVersionValue;
                            session["DOTNETCOREVORHANDEN"] = ".NET Core " + versionValue;
                        }
                    }
                }
            }

            // Zum Schluss wird geprüft, ob die Versionsnummer ausreicht und die WiX-Property wird geschrieben.

            if (version >= minVersion)
            {
                session["DOTNETCORE3115"] = "1";            // grünes statt rotes Icon im WiX-Dialog anzeigen
            }

            return ActionResult.Success;
        }
    }
}
