using System;
using System.IO;
using Ini.Net; // Free to use for commercial purposes

namespace NativeService
{
    class IniReader
    {
        // Currently using a NuGet package that doesn't support sectionless INI file reading.
        // A custom workaround has been added for handling sectionless INI files.
        // This solution is simple and should work fine for small files like Setup.ini of Nox.

        public static string ReadIniValue(string filePath, string section, string key)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"INI file not found: {filePath}");
                }

                if (!string.IsNullOrEmpty(section))
                {
                    LogWriter.Write($"Reading INI with sections. File: {filePath}, Section: {section}, Key: {key}", LogWriter.LogEventType.Event);
                    IniFile iniFile = new IniFile(filePath);
                    return iniFile.ReadString(section, key);
                }
                else
                {
                    LogWriter.Write($"Reading INI without section. File: {filePath}, Key: {key}", LogWriter.LogEventType.Event);
                    return ReadSectionlessIniValue(filePath, key);
                }
            }
            catch (Exception e)
            {
                LogWriter.Write($"INI file reading failed. File: {filePath}, Section: {section}, Key: {key}, Error: {e.Message}", LogWriter.LogEventType.Error);
                throw new Exception($"Failed to read INI file '{filePath}' (Key: {key}, Section: {section})", e);
            }
        }

        // Reads a key from an INI file that does not contain sections.
        // This is a simple approach and is suitable for small INI files.
        private static string ReadSectionlessIniValue(string filePath, string key)
        {
            try
            {
                using (StreamReader file = new StreamReader(filePath))
                {
                    string currentLine;
                    while ((currentLine = file.ReadLine()) != null)
                    {
                        if (currentLine.StartsWith(key + "=")) // Ensures we only match the correct key
                        {
                            return currentLine.Substring(currentLine.IndexOf("=") + 1);
                        }
                    }
                }

                return null; // Return null if the key is not found
            }
            catch (Exception e)
            {
                LogWriter.Write($"Failed to read sectionless INI file '{filePath}' (Key: {key}). Error: {e.Message}", LogWriter.LogEventType.Error);
                throw new Exception($"Error reading sectionless INI file '{filePath}' (Key: {key})", e);
            }
        }
    }
}
