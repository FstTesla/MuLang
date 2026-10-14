var total = 0;
var enabled = true;

if (enabled) {
    total = total + 1;
} else {
    total = total - 1;
}

while (enabled) {
    total = total + 1;

    if (total > 4) {
        break;
    }
}

for (var index = 0; index < 4; index = index + 1) {
    if (index == 1) {
        continue;
    }

    total = total + index;
}

var outer = 0;
while (outer < 2) {
    var inner = 0;

    while (inner < 2) {
        inner = inner + 1;

        if (outer == 1) {
            break 2;
        }

        continue 1;
    }

    outer = outer + 1;
}

{
    var scopedValue$ = total;
    total = scopedValue;
}
