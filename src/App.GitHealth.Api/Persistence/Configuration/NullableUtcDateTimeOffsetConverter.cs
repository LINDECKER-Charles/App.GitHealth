using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class NullableUtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset?, long?>(
        value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
        value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
