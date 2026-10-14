type Node @{
    name$: string,
    next?: Node,
    payload: {
        count: int,
        tags: string[]$,
    },
};

type NodeAlias @{
    name$: string,
    next?: NodeAlias,
    payload: {
        count: int,
        tags: string[]$,
    },
};

var mutableValues: int[] = [1, 2, 3,];
var readOnlyValues: int[]$ = $[4, 5, 6,];
var root: Node = @{
    name$: string = "root",
    next?: Node,
    payload: { count: int, tags: string[]$ } = {
        count = mutableValues[0],
        tags = $["types", "objects",],
    },
};
var dynamicObject = @{ dynamic? = true };
var alias: NodeAlias = root;
var inline: @{ label$: string, value?: primitive } = @{
    label$ = "inline",
    value?: primitive = 42,
};
var objectValue: object = root;
var unknownValue: unknown = inline;
var optionalNode: Node? = root;
var firstValue = mutableValues[0];
var propertyValue = root.payload.count;
var dynamicValue = dynamicObject["dynamic"];
var hasDynamic = dynamicObject has "dynamic";

mutableValues[1] = firstValue;
root.payload.count = readOnlyValues.length;
dynamicObject.dynamic = false;
dynamicObject.dynamic~;
