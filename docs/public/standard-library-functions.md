# Standard-library functions

Functions are listed alphabetically and identify their canonical module.
Signatures use MuLang types. All functions are deterministic unless a
capability is stated explicitly. String indexes and lengths count Unicode
scalar values.

## abs

**Signature:** `abs(value: number): number`

**Since:** 0.2.0

**Module:** Math.Basic

**Behavior:** Returns the absolute value while preserving the concrete numeric
representation.

## acos

**Signature:** `acos(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the inverse cosine in radians.

## arrayContains

**Signature:** `arrayContains(array: unknown?[]$, value: unknown?): bool`

**Since:** 0.2.0

**Module:** Array

**Minimum MuLang:** 1.1

**Behavior:** Tests membership using MuLang structural equality.

## asin

**Signature:** `asin(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the inverse sine in radians.

## atan

**Signature:** `atan(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the inverse tangent in radians.

## atan2

**Signature:** `atan2(y: number, x: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the angle of the coordinate in radians.

## base64Decode

**Signature:** `base64Decode(value: string): string?`

**Since:** 0.2.0

**Module:** Text.Encoding

**Behavior:** Decodes canonical Base64 UTF-8 text. Invalid Base64 or UTF-8
returns `null`.

## base64Encode

**Signature:** `base64Encode(value: string): string`

**Since:** 0.2.0

**Module:** Text.Encoding

**Behavior:** Encodes UTF-8 text as canonical Base64.

## ceil

**Signature:** `ceil(value: number): number`

**Since:** 0.2.0

**Module:** Math.Rounding

**Behavior:** Rounds toward positive infinity.

## charAt

**Signature:** `charAt(value: string, index: int): int`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Returns the Unicode scalar value at an index.

## clamp

**Signature:** `clamp(value: number, minimum: number, maximum: number): number`

**Since:** 0.2.0

**Module:** Math.Basic

**Behavior:** Constrains a value to an inclusive valid range.

## compareIgnoreCase

**Signature:** `compareIgnoreCase(left: string, right: string): int`

**Since:** 0.2.0

**Module:** String.Comparison

**Behavior:** Compares strings ordinally without case.

## compareOrdinal

**Signature:** `compareOrdinal(left: string, right: string): int`

**Since:** 0.2.0

**Module:** String.Comparison

**Behavior:** Compares strings ordinally.

## cos

**Signature:** `cos(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the cosine of a radian angle.

## degToRad

**Signature:** `degToRad(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Converts degrees to radians.

## endsWith

**Signature:** `endsWith(value: string, suffix: string): bool`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Tests an ordinal suffix.

## equalsIgnoreCase

**Signature:** `equalsIgnoreCase(left: string, right: string): bool`

**Since:** 0.2.0

**Module:** String.Comparison

**Behavior:** Tests ordinal case-insensitive equality.

## exp

**Signature:** `exp(value: number): float`

**Since:** 0.2.0

**Module:** Math.Powers

**Behavior:** Returns `e` raised to the value.

## floor

**Signature:** `floor(value: number): number`

**Since:** 0.2.0

**Module:** Math.Rounding

**Behavior:** Rounds toward negative infinity.

## indexOf

**Signature:** `indexOf(value: string, part: string): int?`

**Since:** 0.2.0

**Module:** String.Search

**Behavior:** Returns the first scalar index, or `null`.

## insert

**Signature:** `insert(value: string, index: int, inserted: string): string`

**Since:** 0.2.0

**Module:** String.Slicing

**Behavior:** Inserts text at a scalar index.

## isEmpty

**Signature:** `isEmpty(value: string): bool`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Determines whether a string is empty.

## isFinite

**Signature:** `isFinite(value: number): bool`

**Since:** 0.2.0

**Module:** Math.Classification

**Behavior:** Determines whether a number is finite.

## isGuid

**Signature:** `isGuid(value: string): bool`

**Since:** 0.2.0

**Module:** Guid

**Capability:** Deterministic

**Behavior:** Tests canonical GUID syntax.

## isInfinity

**Signature:** `isInfinity(value: number): bool`

**Since:** 0.2.0

**Module:** Math.Classification

**Behavior:** Determines whether a number is positive or negative infinity.

## isNaN

**Signature:** `isNaN(value: number): bool`

**Since:** 0.2.0

**Module:** Math.Classification

**Behavior:** Determines whether a number is NaN.

## isWhiteSpace

**Signature:** `isWhiteSpace(value: string): bool`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Determines whether every scalar is whitespace.

## lastIndexOf

**Signature:** `lastIndexOf(value: string, part: string): int?`

**Since:** 0.2.0

**Module:** String.Search

**Behavior:** Returns the last scalar index, or `null`.

## log

**Signature:** `log(value: number): float`

**Since:** 0.2.0

**Module:** Math.Powers

**Behavior:** Returns the natural logarithm.

## log10

**Signature:** `log10(value: number): float`

**Since:** 0.2.0

**Module:** Math.Powers

**Behavior:** Returns the base-10 logarithm.

## max

**Signature:** `max(left: number, right: number): number`

**Since:** 0.2.0

**Module:** Math.Basic

**Behavior:** Returns the larger numeric value.

## min

**Signature:** `min(left: number, right: number): number`

**Since:** 0.2.0

**Module:** Math.Basic

**Behavior:** Returns the smaller numeric value.

## newGuid

**Signature:** `newGuid(): string`

**Since:** 0.2.0

**Module:** Guid

**Capability:** Randomness

**Behavior:** Returns a lowercase canonical version-4 GUID.

## newGuidV7

**Signature:** `newGuidV7(): string`

**Since:** 0.2.0

**Module:** Guid

**Capability:** Randomness and clock

**Behavior:** Returns a lowercase canonical version-7 GUID.

## objectKeys

**Signature:** `objectKeys(obj: object): string[]$`

**Since:** 0.2.0

**Module:** Object

**Minimum MuLang:** 1.1

**Behavior:** Returns the visible property names in ordinal order as a read-only
array.

## objectValues

**Signature:** `objectValues(obj: object): unknown?[]$`

**Since:** 0.2.0

**Module:** Object

**Minimum MuLang:** 1.1

**Behavior:** Returns values in the same order as `objectKeys` as a read-only
array.

## parseBool

**Signature:** `parseBool(value: string): bool?`

**Since:** 0.2.0

**Module:** Parsing

**Behavior:** Parses the exact MuLang source spelling `true` or `false`
culture-independently. Invalid input returns `null`.

## parseFloat

**Signature:** `parseFloat(value: string): float?`

**Since:** 0.2.0

**Module:** Parsing

**Behavior:** Parses an exact finite or non-finite MuLang float literal
culture-independently. Invalid or unrepresentable input returns `null`.

## parseInt

**Signature:** `parseInt(value: string): int?`

**Since:** 0.2.0

**Module:** Parsing

**Behavior:** Parses an exact decimal or prefixed MuLang integer literal
culture-independently. Invalid or unrepresentable input returns `null`.

## pow

**Signature:** `pow(value: number, exponent: number): float`

**Since:** 0.2.0

**Module:** Math.Powers

**Behavior:** Raises a value to a power.

## radToDeg

**Signature:** `radToDeg(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Converts radians to degrees.

## randomFloat

**Signature:** `randomFloat(): float`

**Since:** 0.2.0

**Module:** Random

**Capability:** Randomness

**Behavior:** Returns a random value in the interval [0, 1).

## randomInt

**Signature:** `randomInt(minimum: int, maximum: int): int`

**Since:** 0.2.0

**Module:** Random

**Capability:** Randomness

**Behavior:** Returns a random integer in the interval [minimum, maximum).

## remove

**Signature:** `remove(value: string, start: int, length: int): string`

**Since:** 0.2.0

**Module:** String.Slicing

**Behavior:** Removes a scalar-indexed range.

## repeat

**Signature:** `repeat(value: string, count: int): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Repeats a string a non-negative number of times.

## replaceAll

**Signature:** `replaceAll(value: string, oldValue: string, newValue: string): string`

**Since:** 0.2.0

**Module:** String.Replacement

**Behavior:** Replaces every non-overlapping ordinal match.

## replaceFirst

**Signature:** `replaceFirst(value: string, oldValue: string, newValue: string): string`

**Since:** 0.2.0

**Module:** String.Replacement

**Behavior:** Replaces the first ordinal match.

## reverse

**Signature:** `reverse(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Reverses Unicode scalar order.

## round

**Signature:** `round(value: number): number`

**Since:** 0.2.0

**Module:** Math.Rounding

**Behavior:** Rounds to the nearest value using ties-to-even.

## sign

**Signature:** `sign(value: number): int`

**Since:** 0.2.0

**Module:** Math.Basic

**Behavior:** Returns `-1`, `0`, or `1`; NaN is invalid.

## sin

**Signature:** `sin(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the sine of a radian angle.

## sqrt

**Signature:** `sqrt(value: number): float`

**Since:** 0.2.0

**Module:** Math.Powers

**Behavior:** Returns the square root.

## startsWith

**Signature:** `startsWith(value: string, prefix: string): bool`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Tests an ordinal prefix.

## stringContains

**Signature:** `stringContains(value: string, part: string): bool`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Tests ordinal substring containment.

## stringLength

**Signature:** `stringLength(value: string): int`

**Since:** 0.2.0

**Module:** String.Inspection

**Behavior:** Returns the Unicode scalar count.

## substring

**Signature:** `substring(value: string, start: int, length: int): string`

**Since:** 0.2.0

**Module:** String.Slicing

**Behavior:** Extracts a scalar-indexed range.

## tan

**Signature:** `tan(value: number): float`

**Since:** 0.2.0

**Module:** Math.Trigonometry

**Behavior:** Returns the tangent of a radian angle.

## toLower

**Signature:** `toLower(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Applies culture-independent lowercase conversion.

## toUpper

**Signature:** `toUpper(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Applies culture-independent uppercase conversion.

## trim

**Signature:** `trim(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Removes leading and trailing whitespace.

## trimEnd

**Signature:** `trimEnd(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Removes trailing whitespace.

## trimStart

**Signature:** `trimStart(value: string): string`

**Since:** 0.2.0

**Module:** String.Transform

**Behavior:** Removes leading whitespace.

## trunc

**Signature:** `trunc(value: number): number`

**Since:** 0.2.0

**Module:** Math.Rounding

**Behavior:** Rounds toward zero.

## truncToInt

**Signature:** `truncToInt(value: number): int`

**Since:** 0.2.0

**Module:** Math.Rounding

**Behavior:** Truncates to a representable `int`.

## unixTimeMilliseconds

**Signature:** `unixTimeMilliseconds(): int`

**Since:** 0.2.0

**Module:** Clock

**Capability:** Clock

**Behavior:** Returns Unix time in whole milliseconds.

## unixTimeSeconds

**Signature:** `unixTimeSeconds(): int`

**Since:** 0.2.0

**Module:** Clock

**Capability:** Clock

**Behavior:** Returns Unix time in whole seconds.
