var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var view = doc.ActiveView;
var rows = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_StairsPaths)
    .WhereElementIsNotElementType()
    .Select(e => {
        var p = e.Parameters.Cast<Parameter>().FirstOrDefault(x => x.Id.IntegerValue == -1006631)
            ?? e.LookupParameter("Show Up Text");
        return new {
            id = e.Id.IntegerValue,
            name = e.Name,
            showUpText = p == null ? null : (p.AsValueString() ?? p.AsInteger().ToString())
        };
    })
    .ToList();

return new { ok = true, view = view.Name, stairPathCount = rows.Count, rows };
