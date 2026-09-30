func clamp(value: int, minimum: int, maximum: int): int {
    if (value < minimum) {
        return minimum;
    }

    if (value > maximum) {
        return maximum;
    }

    return value;
}

func exerciseHighlighting(name: string, requested: int): void {
    var count: int = clamp(requested, 0, 10);
    var active: bool = count > 0 && count <= 10;
    var state = active ? "active" : "inactive";
    var escapedText = "quote: \"MuLang\", newline:\n, tab:\t";
    var unicodeLabel = "MuLang 😀"; var alignedLabel = unicodeLabel;

    var decimalValue: float = 150.25e-1;
    var positiveInfinity: float = +infty;
    var negativeInfinity: float = -infty;
    var notANumber: float = nan;
    var binaryMask = 0b1010;
    var octalMask = 0o17;
    var hexadecimalMask = 0x2A;
    var flags = (binaryMask | 4) & octalMask;
    var shiftedFlags = flags << 1;
    var immutableFlags$ = shiftedFlags;

    var normalizedCount$: int;
    if (active) {
        normalizedCount = count;
    } else {
        normalizedCount = 0;
    }

    var values: int[] = [1, 2, 3,];
    var readOnlyValues: int[]$ = $[4, 5, 6,];
    var optionalText: string? = null;
    var displayText = optionalText ?? escapedText;
    var numericValue: number = count;
    var isInteger = numericValue is int;
    var exactValue = numericValue as int;
    var alwaysTrueTypeTest = count is int;
    var redundantCast = count as int;
    var constantNullComparison = count == null;
    var redundantCoalescing = (1 as int?) ?? 0;
    var alwaysFallbackCoalescing = null ?? 0;
    var redundantOptionalAccess = values?.length;
    var alwaysNullOptionalAccess = (null as int[]?)?.length;
    var constantLogicalResult = true || active;

    if (true) {
    }

    if (nan === nan) {
    }

    ;

    var details = @{
        title: displayText,
        name: name,
        state: state,
        count: count,
        accent?: "blue",
    };

    details.version = 1;
    var hasVersion = details has "version";
    var missingValue = details?.["missing"] ?? "fallback";
    details.accent~;

    values[0] = exactValue;
    var total = readOnlyValues.length + immutableFlags + hexadecimalMask;

    var firstTotal$: int;
    while (active) {
        firstTotal = total;
        total = firstTotal + normalizedCount;
        break;
    }

    for (var index = 0; index < values.length; index = index + 1) {
        if (index == 1) {
            continue;
        }

        var currentValue$ = values[index];
        total = total + currentValue;
    }

    while (total < 64) {
        total = total + 1;

        if (total >= 48 || !active) {
            break;
        }
    }

    details.count = total;
    var summary = alignedLabel + ": " + details.name + " is " + details.state;
    var comparisons$ = count === requested && count !== -1;

    return;
}

exerciseHighlighting("editor", 12);
