using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NoteService.Api.Data;

/// <summary>
/// Stores <see cref="DateTimeOffset"/> values as UTC ticks. SQLite cannot order or compare DateTimeOffset
/// natively, and unlike the built-in binary converter this keeps full tick precision.
/// </summary>
public class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
