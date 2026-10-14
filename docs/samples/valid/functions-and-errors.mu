type DivisionResult {
    quotient: int,
    remainder: int,
};

func divide(dividend: int, divisor: int): DivisionResult {
    if (divisor == 0) {
        throw {
            code = "division-by-zero",
            message = "The divisor cannot be zero.",
            cause = {
                code = "invalid-divisor",
                message = "The divisor was zero.",
            },
            data = dividend,
        };
    }

    return {
        quotient = dividend / divisor,
        remainder = dividend % divisor,
    };
}

func inspect(value: unknown, optionalResult: DivisionResult?): string {
    var isText = value is string;
    var integerValue = value as int;
    var quotient = optionalResult?.quotient;
    var label = isText ? "text" : "integer";

    return label + ": " + integerValue + ", " + (quotient ?? 0);
}

func observe(failure: error): void {
    var code = failure.code;
    var message = failure.message;
    var cause = failure.cause;
    var data = failure.data;

    return;
}

func calculate(value: int): int {
    var result = 0;

    try {
        var division = divide(value, 2);
        result = division.quotient;
    } catch (failure) {
        if (failure.code == "division-by-zero") {
            result = 0;
        } else {
            throw;
        }
    } finally {
        result = result + 1;
    }

    return result;
}

var calculated = calculate(10);
var inspected = inspect(calculated, null);
