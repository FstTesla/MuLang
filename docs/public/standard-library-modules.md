# Standard-library modules

Modules are proposed groups of canonical symbols. Selecting a module selects
all its symbols and their dependencies. The minimum MuLang version and runtime
capability are derived from those symbols.

Capability metadata is descriptive. Hosts must explicitly select a module and
provide any required runtime service when binding it.

## Array

**Stable identifier:** `mulang.std.array`

**Since:** 0.2.0

**Minimum MuLang:** 1.1

**Capability:** Deterministic

**Symbols:**

- `arrayContains`

## Clock

**Stable identifier:** `mulang.std.clock`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Clock

**Symbols:**

- `unixTimeMilliseconds`
- `unixTimeSeconds`

## Guid

**Stable identifier:** `mulang.std.guid`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Randomness and clock

**Symbols:**

- `isGuid`
- `newGuid`
- `newGuidV7`

## Math.Basic

**Stable identifier:** `mulang.std.math.basic`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `abs`
- `clamp`
- `max`
- `min`
- `sign`

## Math.Classification

**Stable identifier:** `mulang.std.math.classification`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `isFinite`
- `isInfinity`
- `isNaN`

## Math.Constants

**Stable identifier:** `mulang.std.math.constants`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `e`
- `maxInt`
- `minInt`
- `pi`
- `tau`

## Math.Powers

**Stable identifier:** `mulang.std.math.powers`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `exp`
- `log`
- `log10`
- `pow`
- `sqrt`

## Math.Rounding

**Stable identifier:** `mulang.std.math.rounding`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `ceil`
- `floor`
- `round`
- `trunc`
- `truncToInt`

## Math.Trigonometry

**Stable identifier:** `mulang.std.math.trigonometry`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `acos`
- `asin`
- `atan`
- `atan2`
- `cos`
- `degToRad`
- `radToDeg`
- `sin`
- `tan`

## Object

**Stable identifier:** `mulang.std.object`

**Since:** 0.2.0

**Minimum MuLang:** 1.1

**Capability:** Deterministic

**Symbols:**

- `objectKeys`
- `objectValues`

## Parsing

**Stable identifier:** `mulang.std.parsing`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `parseBool`
- `parseFloat`
- `parseInt`

## Random

**Stable identifier:** `mulang.std.random`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Randomness

**Symbols:**

- `randomFloat`
- `randomInt`

## String.Comparison

**Stable identifier:** `mulang.std.string.comparison`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `compareIgnoreCase`
- `compareOrdinal`
- `equalsIgnoreCase`

## String.Inspection

**Stable identifier:** `mulang.std.string.inspection`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `charAt`
- `endsWith`
- `isEmpty`
- `isWhiteSpace`
- `startsWith`
- `stringContains`
- `stringLength`

## String.Replacement

**Stable identifier:** `mulang.std.string.replacement`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `replaceAll`
- `replaceFirst`

## String.Search

**Stable identifier:** `mulang.std.string.search`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `indexOf`
- `lastIndexOf`

## String.Slicing

**Stable identifier:** `mulang.std.string.slicing`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `insert`
- `remove`
- `substring`

## String.Transform

**Stable identifier:** `mulang.std.string.transform`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `repeat`
- `reverse`
- `toLower`
- `toUpper`
- `trim`
- `trimEnd`
- `trimStart`

## Text.Encoding

**Stable identifier:** `mulang.std.text.encoding`

**Since:** 0.2.0

**Minimum MuLang:** 1

**Capability:** Deterministic

**Symbols:**

- `base64Decode`
- `base64Encode`
