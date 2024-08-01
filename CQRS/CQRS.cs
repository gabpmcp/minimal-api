using MinimalApi.Validations;

namespace MinimalApi.CQRS
{
    public interface IRecord { }

    public record Command(string Kind, Dictionary<string, object> Data) : IRecord
    {
        public T GetData<T>(string key) => (T)Data[key];

        public ValidationResult Validate(Dictionary<string, List<Validator>> schema)
        {
            if (!schema.ContainsKey(Kind))
            {
                return new ValidationResult(false, $"No schema found for kind: {Kind}");
            }

            return schema[Kind]
                .AsParallel()
                .Select(validator => validator(Data))
                .Aggregate(ValidationResult.Success, (current, result) => current.Combine(result));
        }
    }

    public record Event(string Kind, Dictionary<string, object> Data) : IRecord
    {
        public T Get<T>(string key) => (T)Data[key];
    }

    public static class Commands
    {
        public static Command CreateItem(Guid id, string name, decimal price) =>
            new("CreateItem", new()
            {
                ["Id"] = id,
                ["Name"] = name,
                ["Price"] = price
            });

        public static Command UpdateItem(Guid id, string name, decimal price) =>
            new("UpdateItem", new()
            {
                ["Id"] = id,
                ["Name"] = name,
                ["Price"] = price
            });

        public static Command GetById(Guid id) =>
            new("GetById", new()
            {
                ["Id"] = id
            });

        public static Command UnsupportedCommand() =>
            new("UnsupportedCommand", []);
    }

    public static class Events
    {
        public static Event ItemCreated(Guid id, string name, decimal price) =>
            new("CreateItem", new()
            {
                ["Id"] = id,
                ["Name"] = name,
                ["Price"] = price
            });

        public static Event ItemUpdated(Guid id, string name, decimal price) =>
            new("UpdateItem", new()
            {
                ["Id"] = id,
                ["Name"] = name,
                ["Price"] = price
            });
    }
}