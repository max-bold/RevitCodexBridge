var uidoc = app.ActiveUIDocument;
var doc = uidoc?.Document;
if (uidoc == null || doc == null) return new { ok = false, reason = "No active document" };

var ids = uidoc.Selection.GetElementIds().ToList();
var items = ids.Select(id => {
    var e = doc.GetElement(id);
    return new {
        id = id.IntegerValue,
        category = e?.Category?.Name,
        type = e?.GetType().FullName,
        name = e?.Name,
        text = e is TextNote note ? note.Text : null
    };
}).ToList();

return new { ok = true, count = items.Count, items };
