/******************************************************************************
 * FrameworkTestCustomAction2.cs
 * Projekt FrameworkTest / CustomAction f�r WiX
 * Datum: 07.06.2021
 * Autor: Ralf Sasse
 * 
 ******************************************************************************/

using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;

// using System.Collections.Generic;
using System.Diagnostics;                   // f�r Objekte der Klassen Process und ProcessStartInfo
// using System.Linq;
using System.Text.RegularExpressions;

namespace FrameworkTest
{
    public class CustomActions
    {
        public static string dotNetCoreTest = String.Empty;
        public static string DOTNETCOREVORHANDEN = String.Empty;
        public static string dotNetFrameworkTest = String.Empty;
        public static string DOTNETFRAMEWORKVORHANDEN = String.Empty;

        public static string nameVersion = String.Empty;
        public static string nameServicePack = String.Empty;

        public static string output = String.Empty;            // Leerer String f�r die Ausgabe
        public static string versionValue = String.Empty;      // leerer String f�r die Versionsnummer
        public static string foundVersionValue = String.Empty; // leerer String f�r die Versionsnummer

        [CustomAction]
        public static ActionResult FrameworkTest(Session session)
        {
            // Definition der Variablen

            session["DOTNETFRAMEWORKVORHANDEN"] = ".NET Framework nicht vorhanden.";
            session["DOTNETCOREVORHANDEN"] = ".NET Core nicht vorhanden.";
            session["DOTNETFRAMEWORKERFORDERLICH"] = ".NET Framework 4.8";
            session["DOTNETCOREERFORDERLICH"] = ".NET Core 3.1.15";

            int releaseKey = 0;

            // Registry �ffnen und Versionsnummern f�r .NET Framework suchen.
            // F�r Versionen bis 4 muss ein anderes Verfahren benutzt werden als f�r neuere Versionen.

            // .NET Framework Versionen bis 4 ermitteln:
            // Hier werden alle gefundenen Versionen innerhalb einer Schleife abgearbeitet.
            // Versionen �ber 4 werden �bersprungen und weiter unten behandelt.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                foreach (string DOTNETFRAMEWORKVORHANDENKey in registryKey.GetSubKeyNames())
                {
                    if (DOTNETFRAMEWORKVORHANDENKey == "v4")              // .NET Framework Version >= 4.5
                    {                                                   // (werden weiter unten behandelt)
                        continue;
                    }

                    if (DOTNETFRAMEWORKVORHANDENKey.StartsWith("v"))      // .NET Framework Version <= 4
                    {
                        RegistryKey versionKey = registryKey.OpenSubKey(DOTNETFRAMEWORKVORHANDENKey);

                        // .NET Framework Versionsnummer ermitteln:

                        nameVersion = (string)versionKey.GetValue("Version", "");

                        if (nameVersion != "")
                        {
                            // Bei Versionen bis 4 sind nur die ersten drei Stellen n�tig (z.B. "2.0" oder "3.5").

                            nameVersion = nameVersion.Substring(0, 3);

                            // ServicePack-Nummer ermitteln:

                            nameServicePack = versionKey.GetValue("SP", "").ToString();

                            // Session-Variable mit der h�chsten Versionsnummer bis einschlie�lich 4 f�llen.
                            // Wird weiter unten �berschrieben, falls eine neuere Version gefunden wird.

                            session["DOTNETFRAMEWORKVORHANDEN"] = ".NET Framework " + nameVersion + ", SP " + nameServicePack;
                        }
                    }
                }
            }

            // .NET Framework Versionen ab 4.5:
            // Hier ist keine Schleife erforderlich, denn die Registry enth�lt den Release-Key.

            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                if (registryKey != null && registryKey.GetValue("Release") != null)
                {
                    releaseKey = (int)registryKey.GetValue("Release");

                    if (releaseKey >= 528040)
                    {
                        versionValue = "4.8";
                        session["DOTNETFRAMEWORK48"] = "1";     // gr�nes statt rotes Icon im WiX-Dialog anzeigen
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
/*
            if (session["DOTNETFRAMEWORK48"] == "0")
            {
                session["DOTNETFRAMEWORKVORHANDEN"] += " Erforderlich ist .NET Framework 4.8.";
            }
*/
            // Versionsnummer f�r .NET Core suchen:
            // Hier werden die vorhandenen Versionen mit dem Kommandozeilen-Befehl "dotnet" ermittelt.
            // Dieser Befehl gibt eine Liste aus, die zeilenweise nach Versionsnummern durchsucht wird.

            output = string.Empty;          // Leerer String f�r die Ausgabe
            versionValue = string.Empty;    // leerer String f�r die Versionsnummer

            Version version        = new Version(0, 0);
            Version minVersion     = new Version(3, 1, 15);
            Version tooHighVersion = new Version(5, 0);

            // String f�r den Programm-Aufruf an der Kommandozeile, hier also
            //   cmd /c dotnet --list-runtimes
            // Der Parameter /c ist wichtig - dadurch wird die Kommandozeile nach der
            // Ausf�hrung des dotnet-Kommandos automatisch wieder beendet.

            var command = "/c dotnet --list-runtimes";

            // An dieser Stelle wird der Start des Kommandozeilen-Prozesses vorbereitet:
            // Ein Objekt der Klasse Process wird erzeugt, das als Eigenschaft u.a. ein
            // Objekt der Klasse ProcessStartInfo besitzt. Dieses wird mit den gew�nschten
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
                // Zeilenumbruch. Die Eigenschaft "NewLine" der Klasse "Environment" enth�lt das
                // (bzw. die) Steuerzeichen f�r den Zeilenumbruch, abh�ngig vom Betriebssystem:
                // "\r\n" f�r Windows, "\n" f�r Linux und MacOSX oder "\r" f�r �ltere MacOS-Versionen.

                while (!p.StandardOutput.EndOfStream)
                {
                    output += $"{p.StandardOutput.ReadLine()}{Environment.NewLine}";
                }

                p.WaitForExit();    // Warten auf Beendigung des Prozesses

                // Wenn der Prozess nicht erfolgreich beendet wurde (dann w�re der ExitCode = 0),
                // dann wird die Session-Variable "DOTNETCORE3115" des WiX-Skripts auf Null gesetzt.
                // Als R�ckgabewert wird aber trotzdem "Success" zur�ckgegeben.

                if (p.ExitCode != 0)
                {
/*
                    session["DOTNETCORE3115"] = "0";
                    session["DOTNETCOREVORHANDEN"] = ".NET Core nicht gefunden.";
                    session["DOTNETCOREVORHANDEN"] += " Erforderlich ist .NET Core 3.1.15.";
*/
                    return ActionResult.Success;
                }

                // In die Session-Variable "DOTNETCOREVORHANDEN" wird ein String mit der gefundenen
                // Versionsnummer geschrieben, der dann im Dialogfenster angezeigt wird.

                int majorNumber;
                int minorNumber;
                int revisionNumber;

                string[] runtimeLines = output.Split('\n');     // output in einzelne Strings teilen; einer pro Zeile

                Regex pattern = new Regex(@"\d+(\.\d+)+");      // regul�rer Ausdruck f�r Versionsnummern. Gesucht werden
                                                                // mehrere Zahlen, die durch Punkte getrennt sind.

                // Die folgende Schleife wird einmal f�r jeden String durchlaufen
                // (also einmal f�r jede Zeile der Ausgabe des "dotnet"-Befehls). 

                foreach (string runtimeLine in runtimeLines)
                {
                    if (runtimeLine.Contains("Microsoft.NETCore.App"))      // nur Zeilen, die "Microsoft.NETCore.App" enthalten
                    {
                        Match m = pattern.Match(runtimeLine);               // pr�fen, ob Versionsnummer enthalten ist

                        foundVersionValue = m.Value;                         // Versionsnummer in String-Objekt einlesen

                        // An dieser Stelle stand urspr�nglich "Version.TryParse(foundVersionValue, out foundVersion)".
                        // Das funktioniert aber nur mit neueren .NET Framework-Versionen. Ich hatte zun�chst diese
                        // CustomAction f�r .NET Framework 4.8 kompiliert, dort funktionierte Version.TryParse() auch.
                        // Allerdings nur, wenn 4.8 auch auf dem Zielrechner installiert war, sonst wurde das Programm
                        // ohne Fehlermeldung abgebrochen.
                        // Die vorliegende Version "FrameworkTestCustonAction2" ist f�r .NET Framework 2.0 kompiliert
                        // und liefert ein korrektes Ergebnis, wenn auf dem Zielrechner 2.0 oder h�her installiert ist.
                        // Die "Version.TryParse()"-Methode habe ich hier nachprogrammiert.
                        // Der String foundVersionValue enth�lt zu Beginn eine Versionsnummer im Format "3.1.15".

                        string[] myParse = foundVersionValue.Split('.');
                        Int32.TryParse(myParse[0], out majorNumber);
                        Int32.TryParse(myParse[1], out minorNumber);
                        Int32.TryParse(myParse[2], out revisionNumber);

                        Version foundVersion = new Version(majorNumber, minorNumber, revisionNumber);

                        // Die Versionsnummer soll gespeichert werden, wenn sie gr��er ist als die zuletzt gefundenen,
                        // aber kleiner als 5 (das w�re nicht mehr .NET Core, sondern .NET 5, und danach wird nicht gesucht.
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

            // Zum Schluss wird gepr�ft, ob die Versionsnummer ausreicht und die WiX-Properties werden geschrieben.

            if (version >= minVersion)
            {
                session["DOTNETCORE3115"] = "1";            // gr�nes statt rotes Icon im WiX-Dialog anzeigen
            }
/*
            if (session["DOTNETCORE3115"] == "0")
            {
                session["DOTNETCOREVORHANDEN"] += " Erforderlich ist .NET Core 3.1.15.";
            }
*/
            return ActionResult.Success;
        }
    }
}
