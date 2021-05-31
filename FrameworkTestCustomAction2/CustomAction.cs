/******************************************************************************
 * FrameworkTestCustomAction2.cs
 * Projekt FrameworkTest / CustomAction für WiX
 * Datum: 31.05.2021
 * Autor: Ralf Sasse
 * 
 ******************************************************************************/

using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;

// using System.Collections.Generic;
using System.Diagnostics;                   // für Objekte der Klassen Process und ProcessStartInfo
// using System.Linq;
using System.Text.RegularExpressions;

namespace FrameworkTest
{
    public class CustomActions
    {
        public static string dotNetCoreTest = String.Empty;
        public static string dotNetCoreVersion = String.Empty;
        public static string dotNetFrameworkTest = String.Empty;
        public static string dotNetFrameworkVersion = String.Empty;

        public static string nameVersion = String.Empty;
        public static string nameServicePack = String.Empty;

        public static string output = String.Empty;           // Leerer String für die Ausgabe
        public static string versionValue = String.Empty;     // leerer String für die Versionsnummer
        public static string nextVersionValue = String.Empty; // leerer String für die Versionsnummer

        [CustomAction]
        public static ActionResult FrameworkTest(Session session)
        {
            // Definition der Variablen

            // Mindest-Versionsnummern von .NET Framework und .NET Core

            Version minVersionDotNetFramework = new Version(4, 8);
            Version minVersionDotNetCore = new Version(3, 1, 14);

            session["DOTNETFRAMEWORKVERSION"] = ".NET Framework nicht gefunden.";
            session["DOTNETCOREVERSION"] = ".NET Core nicht gefunden.";

            int releaseKey = 0;

            // Registry öffnen und Versionsnummern für .NET Framework suchen.
            // Für Versionen bis 4 muss ein anderes Verfahren benutzt werden als für neuere Versionen.

            // .NET Framework Versionen bis 4 ermitteln:
            // Hier werden alle gefundenen Versionen innerhalb einer Schleife abgearbeitet.
            // Versionen über 4 werden übersprungen und weiter unten behandelt.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                foreach (string dotNetFrameworkVersionKey in registryKey.GetSubKeyNames())
                {
                    if (dotNetFrameworkVersionKey == "v4")              // .NET Framework Version >= 4.5
                    {                                                   // (werden weiter unten behandelt)
                        continue;
                    }

                    if (dotNetFrameworkVersionKey.StartsWith("v"))      // .NET Framework Version <= 4
                    {
                        RegistryKey versionKey = registryKey.OpenSubKey(dotNetFrameworkVersionKey);

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

                            session["DOTNETFRAMEWORKVERSION"] = ".NET Framework " + nameVersion + ", SP " + nameServicePack + " gefunden.";
                        }
                    }
                }
            }

            // .NET Framework Versionen ab 4.5:
            // Hier ist keine Schleife erforderlich, denn die Registry enthält den Release-Key.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                if (registryKey != null && registryKey.GetValue("Release") != null)
                {
                    releaseKey = (int)registryKey.GetValue("Release");

                    if (releaseKey >= 378389) CustomActions.versionValue = "4.5";
                    if (releaseKey >= 378675) CustomActions.versionValue = "4.5.1";
                    if (releaseKey >= 379893) CustomActions.versionValue = "4.5.2";
                    if (releaseKey >= 393295) CustomActions.versionValue = "4.6";
                    if (releaseKey >= 394254) CustomActions.versionValue = "4.6.1";
                    if (releaseKey >= 394802) CustomActions.versionValue = "4.6.2";
                    if (releaseKey >= 460798) CustomActions.versionValue = "4.7";
                    if (releaseKey >= 461308) CustomActions.versionValue = "4.7.1";
                    if (releaseKey >= 461808) CustomActions.versionValue = "4.7.2";
                    if (releaseKey >= 528040)
                    {
                        CustomActions.versionValue = "4.8";
                        session["DOTNETFRAMEWORK48"] = "1";     // grünes statt rotes Icon im WiX-Dialog anzeigen
                    }

                    session["DOTNETFRAMEWORKVERSION"] = ".NET Framework " + CustomActions.versionValue + " gefunden.";
                }
            }

            // Versionsnummer für .NET Core suchen:
            // Hier werden die vorhandenen Versionen mit dem Kommandozeilen-Befehl "dotnet" ermittelt.
            // Dieser Befehl gibt eine Liste aus, die zeilenweise nach Versionsnummern durchsucht wird.

            output = string.Empty;          // Leerer String für die Ausgabe
            versionValue = string.Empty;    // leerer String für die Versionsnummer

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

                // Wenn der Prozess nicht erfolgreich beendet wurde (dann wäre der ExitCode = 0),
                // dann wird die Session-Variable "DOTNETCORE3114" des WiX-Skripts auf Null gesetzt.
                // Als Rückgabewert wird aber trotzdem "Success" zurückgegeben.

                if (p.ExitCode != 0)
                {
                    session["DOTNETCORE3114"] = "0";
                    session["DOTNETCOREVERSION"] = ".NET Core nicht gefunden.";

                    return ActionResult.Success;
                }

                // In die Session-Variable "DOTNETCOREVERSION" wird ein String mit der gefundenen
                // Versionsnummer geschrieben, der dann im Dialogfenster angezeigt wird.

                Version version = new Version(0, 0);
                Version tooHighVersion = new Version(5, 0);

                int majorNumber;
                int minorNumber;
                int revisionNumber;

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

                        nextVersionValue = m.Value;                         // Versionsnummer in String-Objekt einlesen

                        // An dieser Stelle stand ursprünglich "Version.TryParse(nextVersionValue, out nextVersion)".
                        // Das funktioniert aber nur mit neueren .NET Framework-Versionen. Ich hatte zunächst diese
                        // CustomAction für .NET Framework 4.8 kompiliert, dort funktionierte Version.TryParse() auch.
                        // Allerdings nur, wenn 4.8 auch auf dem Zielrechner installiert war, sonst wurde das Programm
                        // ohne Fehlermeldung abgebrochen.
                        // Die vorliegende Version "FrameworkTestCustonAction2" ist für .NET Framework 2.0 kompiliert
                        // und liefert ein korrektes Ergebnis, wenn auf dem Zielrechner 2.0 oder höher installiert ist.
                        // Die "Version.TryParse()"-Methode habe ich hier nachprogrammiert.
                        // Der String nextVersionValue enthält zu Beginn eine Versionsnummer im Format "3.1.14".

                        string[] myParse = nextVersionValue.Split('.');
                        Int32.TryParse(myParse[0], out majorNumber);
                        Int32.TryParse(myParse[1], out minorNumber);
                        Int32.TryParse(myParse[2], out revisionNumber);

                        Version nextVersion = new Version(majorNumber, minorNumber, revisionNumber);

                        // Die Versionsnummer soll gespeichert werden, wenn sie größer ist als die zuletzt gefundenen,
                        // aber kleiner als 5 (das wäre nicht mehr .NET Core, sondern .NET 5, und danach wird nicht gesucht.
                        // Der Kommandozeilen-Befehl "dotnet" unterscheidet nicht zwischen .NET Core und .NET 5.

                        if ((nextVersion > version) && (nextVersion < tooHighVersion))
                        {
                            version = nextVersion;
                            versionValue = nextVersionValue;
                        }
                    }
                }

                // Zum Schluss wird geprüft, ob die Versionsnummer ausreicht und die WiX-Properties werden geschrieben.

                if (version >= minVersionDotNetCore)
                {
                    session["DOTNETCORE3114"] = "1";            // grünes statt rotes Icon im WiX-Dialog anzeigen
                }

                session["DOTNETCOREVERSION"] = ".NET Core " + versionValue + " gefunden.";

                return ActionResult.Success;
            }
        }
    }
}
