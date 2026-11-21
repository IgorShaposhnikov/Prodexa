using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Core.Resources;
using Prodexa.Importing.Models;

namespace Prodexa.Importing.Services;

/// <summary>
/// Exposes the explicitly supported manufacturing models and validates CSV mapping definitions.
/// </summary>
public sealed class CsvTargetCatalog
{
    private readonly Dictionary<string, Type> _targets = new(StringComparer.Ordinal)
    {
        [nameof(PlanTask)] = typeof(PlanTask),
        [nameof(PlanTaskRevision)] = typeof(PlanTaskRevision),
        [nameof(Execution)] = typeof(Execution),
        [nameof(Equipment)] = typeof(Equipment),
        [nameof(Downtime)] = typeof(Downtime),
        [nameof(MaterialDefinition)] = typeof(MaterialDefinition),
        [nameof(MaterialDefinitionVersion)] = typeof(MaterialDefinitionVersion),
        [nameof(MaterialLot)] = typeof(MaterialLot),
        [nameof(MaterialMovement)] = typeof(MaterialMovement),
        [nameof(ConsumptionNorm)] = typeof(ConsumptionNorm),
        [nameof(PlanTaskNorm)] = typeof(PlanTaskNorm)
    };

    /// <summary>
    /// Lists target models and the field names expected by the JSON mapping profiles.
    /// </summary>
    public IReadOnlyList<CsvTargetDefinition> GetTargets()
    {
        return _targets.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new CsvTargetDefinition
            {
                Target = item.Key,
                Fields = item.Value.GetProperties()
                    .Select(property =>
                    {
                        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        return new CsvTargetField
                        {
                            Name = JsonNamingPolicy.CamelCase.ConvertName(property.Name),
                            ConvertTo = GetTypeName(type),
                            Required = property.IsDefined(typeof(RequiredMemberAttribute), true),
                            Values = type.IsEnum ? Enum.GetNames(type) : null
                        };
                    }).ToList()
            }).ToList();
    }

    /// <summary>
    /// Resolves a whitelisted manufacturing target model.
    /// </summary>
    public Type Resolve(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || !_targets.TryGetValue(target, out var type))
        {
            throw new ImportValidationException($"Unsupported target model '{target}'.");
        }

        return type;
    }

    /// <summary>
    /// Validates field bindings, type conversions and required target properties.
    /// </summary>
    public void Validate(CsvMappingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateName(definition.Id, "id", 100);
        ValidateName(definition.Name, "name", 200);
        ValidateName(definition.SourceSystem, "sourceSystem", 100);
        ValidateName(definition.SourceRevision, "sourceRevision", 100);

        if (definition.SourceRecordIdColumn is not null)
        {
            ValidateName(definition.SourceRecordIdColumn, "sourceRecordIdColumn", 100);
        }

        var type = Resolve(definition.Target);
        var properties = type.GetProperties().ToDictionary(
            property => JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            property => property,
            StringComparer.Ordinal);

        if (definition.Fields is null || definition.Fields.Count == 0)
        {
            throw new ImportValidationException("A CSV mapping requires at least one target field.");
        }

        foreach (var property in properties)
        {
            if (property.Value.IsDefined(typeof(RequiredMemberAttribute), true)
                && !definition.Fields.ContainsKey(property.Key))
            {
                throw new ImportValidationException($"Required field '{property.Key}' is not mapped.");
            }
        }

        foreach (var (name, binding) in definition.Fields)
        {
            if (!properties.TryGetValue(name, out var property))
            {
                throw new ImportValidationException($"Unknown field '{name}' on target '{definition.Target}'.");
            }

            if (binding is null)
            {
                throw new ImportValidationException($"Mapping of '{name}' cannot be null.");
            }

            var sourceCount = (string.IsNullOrWhiteSpace(binding.Column) ? 0 : 1)
                + (binding.Constant.HasValue ? 1 : 0)
                + (string.IsNullOrWhiteSpace(binding.Generate) ? 0 : 1);

            if (sourceCount != 1)
            {
                throw new ImportValidationException($"Field '{name}' must use exactly one of column, constant, or generate.");
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var convertTo = GetTypeName(targetType);

            if (binding.ConvertTo is not null && binding.ConvertTo != convertTo)
            {
                throw new ImportValidationException($"Field '{name}' must use convertTo '{convertTo}'.");
            }

            if (binding.Generate is not null && (binding.Generate != "stable_id" || targetType != typeof(string)))
            {
                throw new ImportValidationException($"Field '{name}' supports only generate: stable_id for string identifiers.");
            }

            if (binding.Equals is not null && (targetType != typeof(bool) || binding.Column is null))
            {
                throw new ImportValidationException($"equals can only be applied to a boolean field bound to a CSV column ('{name}').");
            }

            if (binding.SourceFormat is not null)
            {
                if (targetType != typeof(DateTimeOffset)
                    || binding.SourceFormat is not ("iso8601" or "utc_naive" or "unix_seconds" or "unix_milliseconds"))
                {
                    throw new ImportValidationException($"Unsupported sourceFormat for field '{name}'.");
                }
            }

            if (binding.Column is not null)
            {
                ValidateName(binding.Column, $"fields.{name}.column", 200);
            }
        }

        if (definition.Filters is null)
        {
            throw new ImportValidationException("filters cannot be null.");
        }

        foreach (var (column, allowed) in definition.Filters)
        {
            ValidateName(column, "filter column", 200);

            if (allowed is null || allowed.Count == 0 || allowed.Any(value => value is null))
            {
                throw new ImportValidationException($"Filter '{column}' must contain at least one allowed value.");
            }
        }
    }

    /// <summary>
    /// Determines the supported conversion name of a model property.
    /// </summary>
    public string GetTypeName(Type type)
    {
        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(DateTimeOffset))
        {
            return "datetime_utc";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(int))
        {
            return "integer";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type.IsEnum)
        {
            return "enum";
        }

        throw new ImportValidationException($"Unsupported CSV target property type '{type.Name}'.");
    }

    private void ValidateName(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new ImportValidationException($"'{name}' must be between 1 and {maxLength} characters.");
        }
    }
}
