using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, long>(
        value => value.ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value));
