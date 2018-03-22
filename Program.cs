using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using SimpleJson;

namespace CubaRest.CodeGenerator
{
    class Program
    {
        static string rootNamespace;
        static IEnumerable<string> entityTypePrefixes;
        static string enumsPrefix;
        static string restApiConfigurationFile = "RestApiConnection.json";

        static void Main(string[] args)
        {
            CubaCodeGenerator codeGenerator = null;

            try
            {
                var api = GetCubaRestApi(restApiConfigurationFile);
                (rootNamespace, entityTypePrefixes, enumsPrefix) = LoadProjectConfiguration("ProjectConfiguration.json");

                Console.WriteLine("Cuba REST API connected");

                codeGenerator = new CubaCodeGenerator(api);
                DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Model");
                di.Create();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not connect Cuba REST API.\r\n{ex.Message}");
                Console.ReadKey();
                Environment.Exit(0);
            }

            foreach (var prefix in entityTypePrefixes)
            {
                Console.Write($"Generating classes for {prefix}...");
                try
                {
                    var classesCode = codeGenerator.GetCodeForEntities(prefix, rootNamespace);

                    using (StreamWriter streamWriter = new StreamWriter($"Model/{prefix.ToPascalCase(CultureInfo.CurrentCulture)}.cs"))
                    {
                        streamWriter.Write(classesCode);
                    }
                    Console.WriteLine("ok");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error: {ex.Message}");
                }
            }

            try
            {
                Console.Write($"Generating enums...");
                var enumsCode = codeGenerator.GetCodeForEnums(enumsPrefix, rootNamespace);
                using (StreamWriter streamWriter = new StreamWriter($"Model/Enums.cs"))
                {
                    streamWriter.Write(enumsCode);
                }
                Console.WriteLine("ok");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        private static (string rootNamespace, IEnumerable<string> entityTypePrefixes, string enumsPrefix) LoadProjectConfiguration(string configFile)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CubaRest.CodeGenerator.{configFile}")
                    ?? throw new FileNotFoundException($"{configFile} not found in project folder");
            try
            {
                var reader = new StreamReader(stream);
                var json = (JsonObject)SimpleJson.SimpleJson.DeserializeObject(reader.ReadToEnd());
                entityTypePrefixes = (json["entityTypePrefixes"] as JsonArray).ToList().Cast<string>();
                return (json["rootNamespace"] as string, entityTypePrefixes, json["enumsPrefix"] as string);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to get valid configuration data from {configFile}", ex);
            }
        }

        static CubaRestApi GetCubaRestApi(string restApiConfigurationFile)
        {
            string endpoint, basicUsername, basicPassword, username, password;

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"CubaRest.CodeGenerator.{restApiConfigurationFile}")
                    ?? throw new FileNotFoundException($"{restApiConfigurationFile} not found in project folder");
            try
            {
                var reader = new StreamReader(stream);
                var json = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());

                endpoint = json["endpoint"];
                basicUsername = json["basicUsername"];
                basicPassword = json["basicPassword"];
                username = json["username"];
                password = json["password"];
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to get valid connection parameters from {restApiConfigurationFile}", ex);
            }

            return new CubaRestApi(endpoint, basicUsername, basicPassword, username, password);
        }
    }        
    // TEST: Что будет, если запрашивать несуществующие префиксы для сущностей и перечислений?
}
