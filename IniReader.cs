using System;
using System.IO;
using Ini.Net; //free to use for commercial purposes


namespace NativeService
{
    class IniReader
    {
        //Currently using a NuGet package that doesn't seem to be able to handle sectionless ini file reading
        //So functionality for sectionless ini file reading has been added... the solution is a bit dumb but I guess
        //it'll do for now

        public static string ReadIniValue(string filePath, string section, string key)
        {
            try
            {
                if (section != "" && section != null)
                {
                    LogWriter.Write("Reading ini with sections", LogWriter.LogEventType.Event);
                    IniFile iniFile = new IniFile(filePath);
                    return iniFile.ReadString(section, key);
                }
                else //not sure how well this works for a larger file, but it's good enough for a small file like Setup.ini of Nox
                {
                    LogWriter.Write("Reading ini without section", LogWriter.LogEventType.Event);
                    string currentLine;
                    string value = null;

                    StreamReader file = new StreamReader(filePath);

                    while ((currentLine = file.ReadLine()) != null)
                    {
                        if (currentLine.IndexOf(key + "=") > -1) //we've found the line we're looking for
                        {
                            value = currentLine.Substring(currentLine.IndexOf("=") + 1);
                            break;
                        }
                    }

                    file.Close();
                    file.Dispose();

                    return value;
                }
            }
            catch (Exception e)
            {
                LogWriter.Write("Ini file reading failed at " + filePath + ": " + e.Message, LogWriter.LogEventType.Error);
                throw;
            }

        }
    }
}
