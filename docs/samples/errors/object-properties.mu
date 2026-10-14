type Item {
    required: int,
    readOnly$: int,
};

var item: Item = {
    readOnly$ = 1,
};
item.readOnly = 2;
