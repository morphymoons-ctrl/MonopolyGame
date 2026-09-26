using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Monopoly.Core;

namespace Monopoly.Net
{
    // Настройки JSON для SignalR. Действия и события — наследники GameAction и GameEvent;
    // в JSON тип пишется в поле "$type". Новые события из Monopoly.Core подхватываются сами.
    public static class ProtocolJson
    {
        private static readonly Type[] PolymorphicBases = { typeof(GameAction), typeof(GameEvent) };

        public static void Configure(JsonSerializerOptions options)
        {
            options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddDerivedTypes } };
        }

        public static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            Configure(options);
            return options;
        }

        private static void AddDerivedTypes(JsonTypeInfo info)
        {
            if (!PolymorphicBases.Contains(info.Type))
                return;

            info.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = System.Text.Json.Serialization.JsonUnknownDerivedTypeHandling.FailSerialization,
            };
            var derived = info.Type.Assembly.GetTypes()
                .Where(t => !t.IsAbstract && info.Type.IsAssignableFrom(t))
                .OrderBy(t => t.Name);
            foreach (var type in derived)
                info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
        }
    }
}
