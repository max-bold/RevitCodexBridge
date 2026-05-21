var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var view = doc.ActiveView;
var stairs = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_Stairs)
    .WhereElementIsNotElementType()
    .Select(e => new { id = e.Id.IntegerValue, name = e.Name, type = e.GetType().FullName })
    .ToList();

var stairPaths = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_StairsPaths)
    .WhereElementIsNotElementType()
    .Select(e => new { id = e.Id.IntegerValue, name = e.Name, type = e.GetType().FullName })
    .ToList();

var pathTypes = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_StairsPaths)
    .WhereElementIsElementType()
    .Select(e => new { id = e.Id.IntegerValue, name = e.Name, type = e.GetType().FullName })
    .ToList();

return new {
    ok = true,
    view = new { id = view.Id.IntegerValue, name = view.Name, viewType = view.ViewType.ToString() },
    stairs,
    stairPaths,
    pathTypes
};
