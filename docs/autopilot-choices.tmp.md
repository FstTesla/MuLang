# Autopilot choices for semantic warnings

The following implementation choices were made autonomously and should be reviewed:

1. Constant logical expressions receive one warning for the complete expression when its result is known. Otherwise, individually constant `&&` or `||` operands are warned. Duplicate warnings with the same code and source span are suppressed.
