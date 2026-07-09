using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EinmaligerSpawn
{
    public static class DynamischesSpawnLimit
    {
        public static int MaxKills = 8; // Unser sicheres Fallback
        public static bool IstInitialisiert = false;

        public static void InitialisiereWerte()
        {
            try
            {
                var allTypes = typeof(GameManager).Assembly.GetTypes();
                var biomeSpawningClassType = allTypes.FirstOrDefault(t => t.Name == "BiomeSpawningClass");
                if (biomeSpawningClassType == null) return;

                var listField = biomeSpawningClassType.GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (listField == null) return;

                object dictObj = listField.GetValue(null);
                if (dictObj == null) return;

                // Das Dictionary knacken (wie in unserer Diagnose)
                object actualDict = dictObj is IDictionary ? dictObj : dictObj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(f => typeof(IDictionary).IsAssignableFrom(f.FieldType))?.GetValue(dictObj);

                if (actualDict is IDictionary dict)
                {
                    int hoechsterVanillaWert = 0;

                    // TEIL 1: Den höchsten Vanilla-Wert aller Feinde finden
                    foreach (DictionaryEntry entry in dict)
                    {
                        var biomeValue = entry.Value;
                        var groupListField = biomeValue.GetType().GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (groupListField != null && groupListField.GetValue(biomeValue) is IEnumerable groupList)
                        {
                            foreach (var data in groupList)
                            {
                                var isAnimalField = data.GetType().GetField("isAnimal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var maxCountField = data.GetType().GetField("maxCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                if (isAnimalField != null && maxCountField != null)
                                {
                                    bool isAnimal = (bool)isAnimalField.GetValue(data);
                                    if (!isAnimal)
                                    {
                                        int maxCount = (int)maxCountField.GetValue(data);
                                        if (maxCount > hoechsterVanillaWert) hoechsterVanillaWert = maxCount;
                                    }
                                }
                            }
                        }
                    }

                    if (hoechsterVanillaWert > 0)
                    {
                        // Multiplikator anwenden!
                        MaxKills = hoechsterVanillaWert * 2;
                        Debug.Log($"[EinmaligerSpawn] Höchster Vanilla-Feind-Wert: {hoechsterVanillaWert}. Neues globales Limit: {MaxKills}");

                        // TEIL 2: Alle Vanilla-Spawns im RAM mit dem neuen Limit überschreiben
                        foreach (DictionaryEntry entry in dict)
                        {
                            var biomeValue = entry.Value;
                            var groupListField = biomeValue.GetType().GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (groupListField != null && groupListField.GetValue(biomeValue) is IEnumerable groupList)
                            {
                                foreach (var data in groupList)
                                {
                                    var isAnimalField = data.GetType().GetField("isAnimal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    var maxCountField = data.GetType().GetField("maxCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (isAnimalField != null && maxCountField != null)
                                    {
                                        bool isAnimal = (bool)isAnimalField.GetValue(data);
                                        // Wir patchen nur Zombies/Feinde, Tiere (isAnimal = true) bleiben unberührt!
                                        if (!isAnimal)
                                        {
                                            maxCountField.SetValue(data, MaxKills);
                                        }
                                    }
                                }
                            }
                        }
                        Debug.Log($"[EinmaligerSpawn] Engine erfolgreich gehackt: Alle Feind-Spawns auf {MaxKills} gesetzt!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[EinmaligerSpawn] Fehler beim dynamischen Patchen: {e.Message}");
            }
        }
    }
}