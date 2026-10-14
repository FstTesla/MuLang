;

var values = [1, 2, 3];
var alwaysTrueCondition = true || false;
var redundantCoalescing = (1 as int?) ?? 0;
var fallbackCoalescing = null ?? 0;
var redundantOptionalAccess = values?.length;
var alwaysNullOptionalAccess = (null as int[]?)?.length;
var constantNullComparison = 1 == null;
var alwaysTrueTypeTest = 1 is int;
var impossibleTypeTest = "text" is int;
var redundantCast = 1 as int;

if (true) {
}

throw {
    code = "sample-warning",
    message = "This throw contains an ignored property.",
    ignored = true,
};
