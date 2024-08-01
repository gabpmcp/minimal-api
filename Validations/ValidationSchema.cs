using System.Diagnostics.CodeAnalysis;

namespace MinimalApi.Validations
{
    public delegate ValidationResult Validator(Dictionary<string, object> data);

    public record ValidationResult(bool IsValid, List<string> ErrorMessages)
    {
        public ValidationResult(bool isValid, string errorMessage)
            : this(isValid, [errorMessage]) { }

        public static ValidationResult Success => new(true, []);

        public ValidationResult Combine(ValidationResult other)
        {
            if (other.IsValid)
                return this;

            var combinedErrors = new List<string>(ErrorMessages);
            combinedErrors.AddRange(other.ErrorMessages);
            return new ValidationResult(false, combinedErrors);
        }
    }

    public static class Validations
    {
        public static readonly Dictionary<string, List<Validator>> commandSchemas = new()
        {
            {
                "CreateItem", new ValidationSchema()
                    .AddValidator("Id", Required("Id"))
                    .AddValidator("Id", OfType<Guid>("Id"))
                    .AddValidator("Name", Required("Name"))
                    .AddValidator("Name", OfType<string>("Name"))
                    .Build()
            },
            {
                "UpdateItem", new()
                {
                    Required("Name"),
                    OfType<string>("Name"),
                    MinLength("Name", 3),
                    Required("Price"),
                    OfType<decimal>("Price"),
                    GreaterThan("Price", 0)
                }
            }
        };

        public static Validator Required(string key) => data =>
            data.ContainsKey(key) ? 
                new ValidationResult(true, "") : 
                new ValidationResult(false, $"Missing required field: {key}");

        public static Validator OfType<T>(string key) => data =>
            data.ContainsKey(key) && data[key] is T ? 
                new ValidationResult(true, "") : 
                new ValidationResult(false, $"Field {key} is not of type {typeof(T).Name}");

        public static Validator MinLength(string key, int minLength) => data =>
            data.ContainsKey(key) && data[key] is string str && str.Length >= minLength ? 
                new ValidationResult(true, "") : 
                new ValidationResult(false, $"Field {key} must be at least {minLength} characters long");

        public static Validator GreaterThan(string key, decimal minValue) => data =>
            data.ContainsKey(key) && data[key] is decimal value && value > minValue ? 
                new ValidationResult(true, "") : 
                new ValidationResult(false, $"Field {key} must be greater than {minValue}");
    }

    public class ValidationSchema
    {
        private static readonly List<Validator> _validators = [];

        public ValidationSchema AddValidator([DisallowNull] string key, [DisallowNull] Validator validator)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(validator, nameof(validator));
            
            _validators.Add(validator);
            return this;
        }

        public List<Validator> Build() => _validators;
    }
}