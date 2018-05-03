using CubaRest.Model;
using CubaRest.Model.Reflection;
using CubaRest.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace CubaRest.CodeGenerator
{
    /// <summary>
    /// Генератор кода для классов сущностей и перечислений
    /// </summary>
    public class CubaCodeGenerator
    {
        CubaRestApi api;

        /// <summary>Базовый тип для всех сущностей</summary>
        readonly Type parentType = typeof(Entity);

        public CubaCodeGenerator(CubaRestApi api)
        {
            this.api = api ?? throw new ArgumentNullException("api");
        }

        #region Сущности
        /// <summary>
        /// Генерация кода для всех сущностей Кубы, название метакласса которых начинается с prefix.
        /// Например prefix = "sys" создаст код для sys$Config, sys$Category и т.д.,
        /// prefix = "sys$S" создаст код для sys$Server, sys$ScheludredTask и т.д.
        /// </summary>
        /// <returns>Код классов сущностей</returns>
        public string GetCodeForEntities(string prefix, string _namespace = "MyProject")
        {
            var types = api.ListTypes(prefix).OrderBy(x => x.EntityName);
            if (!types.Any())
                throw new ArgumentException($"No Cuba types found for \"{prefix}\" metaclass prefix");

            return "using CubaRest.Model;\r\n"
                 + "using System;\r\n"
                 + "using System.Collections.Generic;\r\n"
                 + "using System.ComponentModel;\r\n\r\n"
                 + $"namespace {_namespace}\r\n"
                 + "{\r\n"
                 + $"public class {prefix.ToPascalCase()}\r\n"
                 + "{\r\n"
                 + string.Join("\r\n\r\n", types.Select(x => GetCodeForEntityType(x)))  + "\r\n"
                 + "}\r\n"
                 + "}";
        }

        /// <summary>
        /// Генерация кода для класса сущности, соответствующей типу Кубы cubaType
        /// </summary>
        /// <param name="cubaType">Название метакласса сущности в формате xxx$XxxxXxxxXxxx</param>
        /// <returns></returns>
        public string GetCodeForEntityType(string cubaType)
        {
            CubaRestApi.ValidateMetaclassNameFormat(cubaType);
            return GetCodeForEntityType(api.GetTypeMetadata(cubaType));
        }

        protected string GetCodeForEntityType(EntityType cubaTypeMetadata)
        {
            string cubaType = cubaTypeMetadata.EntityName;
            (string classPrefix, string className) = GetTypeNameForCubaType(cubaType);

            // Определение поддерживаемых интерфейсов
            Assembly assembly = Assembly.GetExecutingAssembly();
            var availableInterfaces = assembly.GetTypes().Where(t => t.GetCustomAttributes<EntityPropertiesAttribute>(false).Any());        

            var supportedInterfaces = new List<Type>();
            foreach (Type type in availableInterfaces)
            {
                // Если в полях типа Кубы присутствуют все поля из проверяемого нами интерфейса, считаем, что тип поддерживает этот интерфейс
                bool useThisInterface = true;
                foreach (var property in type.GetProperties())
                {
                    var cubaProperty = cubaTypeMetadata.Properties.FirstOrDefault(x => x.Name.ToPascalCase() == property.Name);
                    if (cubaProperty == null)
                    {
                        useThisInterface = false;
                        continue;
                    }

                    if (!EmbeddedTypes.Types.ContainsKey(cubaProperty.Type))
                    {
                        useThisInterface = false;
                        continue;
                    }

                    if (EmbeddedTypes.Types[cubaProperty.Type] != property.PropertyType)
                        useThisInterface = false;
                }

                if (useThisInterface)
                    supportedInterfaces.Add(type);
            }

            var fields = new List<string>();
            foreach (var cubaProperty in cubaTypeMetadata.Properties.OrderBy(property => property.Name))
            {
                var propertyAttributes = new List<string>();

                // Атрибут Description и описание типа
                if (!string.IsNullOrEmpty(cubaProperty.Description))
                {
                    propertyAttributes.Add($"/// <summary>{cubaProperty.Description}</summary>");
                    propertyAttributes.Add($"[Description(\"{cubaProperty.Description}\")]");
                }

                // Атрибуты ограничений
                foreach (var attribute in CubaPropertyRestrictionBase.ListPropertyRestrictionAttributes())
                {
                    var attributeFullName = attribute.FullName.EndsWith("Attribute") ? attribute.FullName.Remove(attribute.FullName.Length - "Attribute".Length) : attribute.FullName;
                    var attributeName = attribute.Name.EndsWith("Attribute") ? attribute.Name.Remove(attribute.Name.Length - "Attribute".Length) : attribute.Name;

                    if ((bool)typeof(EntityField).GetProperty(attributeName).GetValue(cubaProperty))
                        propertyAttributes.Add($"[{attributeFullName}]");
                }

                // Собираем строку атрибутов
                string attributesText = propertyAttributes.Any() ? string.Join("\r\n   ", propertyAttributes) : String.Empty;


                // Тип данных свойства      

                // Если свойство присутствует в родительском классе, пропускаем
                if (parentType.GetProperty(cubaProperty.Name.ToPascalCase()) != null)
                    continue;

                bool isMultipleCardinality = cubaProperty.Cardinality == Cardinality.ONE_TO_MANY || cubaProperty.Cardinality == Cardinality.MANY_TO_MANY;
                string propertyTypename;
                switch (cubaProperty.AttributeType)
                {
                    case AttributeType.DATATYPE:
                        if (!EmbeddedTypes.Types.ContainsKey(cubaProperty.Type))
                            continue; // FIX: неизвестные типы пропускаем
                        //throw new CubaNotImplementedException($"Встроенный тип поля {cubaProperty.Name} {cubaProperty.Type} не поддерживается", null);

                        propertyTypename = EmbeddedTypes.Types[cubaProperty.Type].Name;
                        propertyTypename = propertyTypename.Replace("Int32", "int").Replace("String", "string");
                        break;

                    case AttributeType.ASSOCIATION:
                    case AttributeType.COMPOSITION:
                        string propertyPrefix;
                        (propertyPrefix, propertyTypename) = GetTypeNameForCubaType(cubaProperty.Type);

                        if (classPrefix != propertyPrefix)
                            propertyTypename = $"{propertyPrefix}.{propertyTypename}";

                        break;

                    case AttributeType.ENUM:
                        propertyTypename = GetEnumNameForCubaEnum(cubaProperty.Type);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                string propertyType = isMultipleCardinality ? $"List<{ propertyTypename }>" : propertyTypename;

                fields.Add($"   {attributesText}\r\n   public {propertyType} {cubaProperty.Name.ToPascalCase()} {{ get; set; }}");
            }

            var supportedInterfacesText = supportedInterfaces.Any() ?
                                            ", " + string.Join(", ", supportedInterfaces.Select(x => x.Name)) : String.Empty;

            return $"[CubaName(\"{cubaType}\")]\r\n"
                   + $"public class {className} : { parentType.Name }{ supportedInterfacesText }\r\n"
                   + $"{{\r\n{(fields.Any() ? string.Join("\r\n\r\n", fields) : string.Empty)}\r\n}}";
        }
        #endregion

        #region Перечисления
        /// <summary>
        /// Генерация кода для всех перечислений Кубы, название которых начинается с prefix.
        /// Например prefix = "com.haulmont.cuba" создаст код для com.haulmont.cuba.core.global.SendingStatus и т.д.,
        /// </summary>
        /// <returns>Код классов сущностей</returns>
        public string GetCodeForEnums(string prefix = null, string _namespace = "MyProject")
        {
            var enums = api.ListEnums(prefix).OrderBy(x => x.Name);

            if (!enums.Any())
                throw new ArgumentException($"No Cuba enums found with \"{prefix}\" prefix");

            return "using CubaRest.Model;\r\n"
                 + "using System.ComponentModel;\r\n\r\n"
                 + $"namespace {_namespace}.Model\r\n"
                 + "{\r\n"
                 + string.Join("\r\n\r\n", enums.Select(x => GetCodeForEnum(x)))
                 + "}";
        }

        /// <summary>
        /// Генерация кода для перечисления, соответствующего перечислению Кубы cubaEnumType
        /// </summary>
        /// <param name="cubaEnumType">Название метакласса сущности в формате xxx.xxx.xxx.xxx.XxxxxXxxxxxx</param>
        /// <returns>Код перечисления</returns>
        public string GetCodeForEnum(string cubaEnumType)
        {
            CubaRestApi.ValidateEnumNameFormat(cubaEnumType);
            return GetCodeForEnum(api.GetEnumMetadata(cubaEnumType));
        }

        protected string GetCodeForEnum(EnumType cubaEnumMetadata)
        {
            var values = new List<string>();

            foreach (var cubaValue in cubaEnumMetadata.Values.OrderBy(value => value.Name))
            {
                bool idParsedAsNumber = int.TryParse(cubaValue.Id, out int cubaEnumNumber);

                var valueAttributes = new List<string>();

                // Атрибут Description и описание типа
                if (!string.IsNullOrEmpty(cubaValue.Caption))
                {
                    valueAttributes.Add($"/// <summary>{cubaValue.Caption}</summary>");
                    valueAttributes.Add($"[Description(\"{cubaValue.Caption}\")]");
                }

                string attributesText = valueAttributes.Any() ? string.Join("\r\n   ", valueAttributes) : String.Empty;
                values.Add($"   {attributesText}\r\n   {cubaValue.Name}{(idParsedAsNumber ? $" = {cubaEnumNumber}" : String.Empty)},");
            }

            return $"[CubaName(\"{cubaEnumMetadata.Name}\")]\r\n"
                   + $"public enum {GetEnumNameForCubaEnum(cubaEnumMetadata.Name)}\r\n"
                   + $"{{\r\n{(values.Any() ? string.Join("\r\n\r\n", values) : string.Empty)}\r\n}}";
        }
        #endregion

        #region Вспомогательные методы
        protected (string prefix, string name) GetTypeNameForCubaType(string cubaType)
        {
            CubaRestApi.ValidateMetaclassNameFormat(cubaType);
            var parts = cubaType.Split('$');
            return (parts[0].ToPascalCase(), parts[1].ToPascalCase());
        }

        protected string GetEnumNameForCubaEnum(string cubaEnumType)
        {
            cubaEnumType = Regex.Replace(cubaEnumType, "[^a-zA-Z0-9. -]", "");
            CubaRestApi.ValidateEnumNameFormat(cubaEnumType);
            var name = cubaEnumType.Substring(cubaEnumType.LastIndexOf('.') + 1); // отрезаем всё до последней точки
            name = name.Contains("Enum") ? name.Remove(name.LastIndexOf("Enum")) : name;
            return name;
        }
        #endregion
    }
}