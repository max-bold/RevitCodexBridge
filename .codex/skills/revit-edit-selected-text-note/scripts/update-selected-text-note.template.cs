var uidoc = app.ActiveUIDocument;
var doc = uidoc?.Document;
if (uidoc == null || doc == null) return new { ok = false, reason = "No active document" };

var ids = uidoc.Selection.GetElementIds().ToList();
if (ids.Count != 1)
    return new { ok = false, reason = "Expected exactly one selected element", selectedCount = ids.Count };

var el = doc.GetElement(ids[0]);
if (!(el is TextNote note))
    return new {
        ok = false,
        reason = "Selected element is not a TextNote",
        id = ids[0].IntegerValue,
        type = el?.GetType().FullName
    };

var bytes = Convert.FromBase64String("__TEXT_B64__");
var newText = System.Text.Encoding.UTF8.GetString(bytes);

using (var t = new Transaction(doc, "Update selected text note"))
{
    t.Start();
    note.Text = newText;
    t.Commit();
}

return new { ok = true, id = ids[0].IntegerValue, text = note.Text };
