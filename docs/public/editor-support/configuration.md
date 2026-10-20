# Workspace configuration

Workspace configuration selects how the editor analyzes your MuLang files.
See [release availability](index.md#release-availability) for the versions
covered by this documentation.

## Standalone defaults

Every standalone `.mu` or `.mulang` file is analyzed as:

- a MuLang program;
- the standard profile for the latest supported language version;
- an empty host environment;
- no expected result type.

Configure the host environment if your source uses globals, functions, or types
provided by the application that runs it.

## Configuration files and hierarchy

Add a file named `mulang.config.json` to the workspace root or any of its
subdirectories. In Visual Studio, the root is the opened folder in Folder View
or the solution directory in Solution View.

Configuration files apply to their directory and its descendants. For each
document, files are applied from the workspace root down to its directory.
A nested `configuration` replaces the inherited fallback settings;
`overrides` are collected in root-to-leaf order.

The formal [JSON Schema](../../schemas/mulang.config.schema.json) is published at
`https://fsttesla.github.io/MuLang/schemas/mulang.config.schema.json`.
Set `$schema` to that URL to enable schema-aware validation and editing in
editors that support JSON Schema.

Configuration files accept `//` line comments and `/* ... */` block comments.
They also accept trailing commas in objects and arrays.

## Configuration-file schema

The root MUST be a JSON object. It accepts only the following properties:

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `$schema` | string | No | URI reference to the JSON Schema for editing and validation. |
| `configuration` | configuration object | No | Complete fallback configuration selected at this hierarchy level. Omission preserves the inherited configuration; if the complete chain omits it, standalone defaults apply. |
| `overrides` | array of override objects | No | Ordered path-specific configurations appended after overrides inherited from ancestor files. Omission is equivalent to an empty array. |

Compilation properties such as `compilationMode` are not valid directly in the
root object.

Each override accepts only:

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `includes` | non-empty array of strings | Yes | Relative globs. At least one MUST match the document path. |
| `excludes` | array of strings | No | Relative globs. None may match the document path. |
| `configuration` | configuration object | Yes | Complete configuration selected by the override. |

Glob paths are relative to the directory containing `mulang.config.json` and
use `/` as the normalized separator. They support `*` within one path segment,
`?` for one non-separator character, and `**` across directory levels.
Absolute paths and `..` segments are invalid. Overrides are evaluated in array
order after concatenating ancestor files from root to leaf; the last matching
override wins. Override configurations replace rather than merge with the
fallback configuration or earlier overrides.

## Compilation configuration

A configuration object accepts only:

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `compilationMode` | `program` or `expression` | `program` | Selects program or single-expression compilation. |
| `expectedResultType` | type reference or `null` | `null` | Requires the compiled result to have the specified type. |
| `profile` | profile string or object | `latest` | Selects a standard profile and optional feature overrides. |
| `environment` | environment object or `null` | empty environment | Declares host-provided types, globals, and provider functions. |

Set `profile` to `latest`, `1`, `1.1`, or `1.2` to select a standard language
profile. An object with a `base` property can override individual features;
omitted settings inherit from that base, which defaults to `latest`.
Array-valued feature settings are flag sets; an empty array disables every flag
in that set.

The [JSON Schema](../../schemas/mulang.config.schema.json) lists all available
settings and accepted values. See the
[language profiles specification](../language/14-language-profiles.md) for
their meaning.

## Host environment

An environment object accepts only `types`, `globals`, and `functions`. Each
property is optional and defaults to an empty array.

A named type declaration requires string `id` and `name` properties. It can
also contain:

- `isOpen`, a Boolean that defaults to `false`;
- `properties`, an array that defaults to empty.

Each property declaration requires string `name` and a `type` reference.
`isOptional` and `isReadOnly` are optional Booleans that default to `false`.

A global declaration requires string `id`, string `name`, and a `type`
reference. A provider-function declaration requires string `id`, string
`name`, and a `returnType` reference. Its optional `parameters` array defaults
to empty. Each parameter requires string `name` and a `type` reference.

The `id` identifies the host symbol, while `name` is the identifier visible to
MuLang source.

## Type references

A type reference is one of:

- an intrinsic name: `bool`, `int`, `float`, `number`, `string`, `primitive`,
  `unknown`, `object`, `void`, or `error`;
- the `name` of a type declared in the same environment;
- a nullable reference object with `kind` set to `nullable` and a required
  `underlyingType`;
- an array reference object with `kind` set to `array`, a required
  `elementType`, and an optional Boolean `readOnly` that defaults to `false`.

Type references are recursive, so nullable arrays, arrays of nullable values,
and mutually recursive named host types are supported.

## Path overrides

```json
{
  "$schema": "https://fsttesla.github.io/MuLang/schemas/mulang.config.schema.json",
  "configuration": {
    "compilationMode": "program",
    "profile": "latest"
  },
  "overrides": [
    {
      "includes": [
        "expressions/**/*.mu"
      ],
      "excludes": [
        "expressions/generated/**"
      ],
      "configuration": {
        "compilationMode": "expression",
        "expectedResultType": "int",
        "profile": "1.2"
      }
    }
  ]
}
```

This configuration uses program mode by default and expects integer expressions
under `expressions`, excluding generated files.

## Reloads and invalid updates

Configuration-file changes are applied automatically and refresh diagnostics
for open documents.

Invalid updates report an error and leave the last valid configuration active.
Documents with no configuration file in their ancestor chain use the
standalone defaults.

Other clients can supply the same compilation settings through
[LSP workspace configuration](language-server.md#workspace-configuration).
