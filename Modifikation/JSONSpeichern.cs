using System;
using System.IO;
using Newtonsoft.Json;

namespace EinmaligerSpawn.JSONSpeichern
{
    public static class SicheresSpeichern
    {
        public static bool TryLoad<T>(string pfad, out T result) where T : class, new()
        {
            result = new T();
            string bakPfad = pfad + ".bak";

            // 1. Hauptdatei versuchen
            if (File.Exists(pfad))
            {
                try
                {
                    string json = File.ReadAllText(pfad);

                    // FIX: Leere oder "null"-Strings sofort als Fehler behandeln, 
                    // damit der catch-Block das Backup triggert.
                    if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                    {
                        throw new Exception("Datei ist leer oder enthält keine gültigen Daten.");
                    }

                    T parsed = JsonConvert.DeserializeObject<T>(json);

                    if (parsed == null)
                    {
                        throw new Exception("Deserialisierung resultierte in null.");
                    }

                    result = parsed;
                    return true;
                }
                catch (Exception e)
                {
                    Log.Error($"[EinmaligerSpawn] Datenkorruption in {pfad} erkannt: {e.Message}");
                    // Defekte/Leere Datei zur manuellen Reparatur durch den Admin isolieren
                    string corruptPfad = pfad + ".corrupt_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    try { File.Move(pfad, corruptPfad); } catch { }
                    Log.Warning($"[EinmaligerSpawn] Defekte Datei gesichert unter: {corruptPfad}");
                }
            }

            // 2. Fallback auf Backup
            if (File.Exists(bakPfad))
            {
                Log.Out($"[EinmaligerSpawn] Versuche Backup zu laden: {bakPfad}");
                try
                {
                    string json = File.ReadAllText(bakPfad);

                    if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                    {
                        throw new Exception("Backup-Datei ist ebenfalls leer.");
                    }

                    T parsed = JsonConvert.DeserializeObject<T>(json);
                    if (parsed == null) throw new Exception("Backup-Deserialisierung resultierte in null.");

                    result = parsed;
                    Log.Out("[EinmaligerSpawn] Backup erfolgreich wiederhergestellt!");
                    return true;
                }
                catch (Exception e)
                {
                    Log.Error($"[EinmaligerSpawn] Backup ist ebenfalls beschädigt: {e.Message}");
                }
            }

            return false;
        }

        public static void Save(string pfad, object data)
        {
            string tmpPfad = pfad + ".tmp";
            string bakPfad = pfad + ".bak";

            try
            {
                // 1. Sicher in eine temporäre Datei schreiben
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(tmpPfad, json);

                // 2. Vorhandenes Original als Backup sichern
                if (File.Exists(pfad))
                {
                    File.Copy(pfad, bakPfad, true);
                }

                // 3. Erst jetzt die Originaldatei überschreiben
                File.Copy(tmpPfad, pfad, true);
                File.Delete(tmpPfad);
            }
            catch (Exception e)
            {
                Log.Error($"[EinmaligerSpawn] Kritischer Fehler beim Speichern von {pfad}: {e.Message}");
            }
        }
    }
}