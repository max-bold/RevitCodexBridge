var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var view = doc.ActiveView;
var stairs = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_Stairs)
    .WhereElementIsNotElementType()
    .ToList();

var pathType = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_StairsPaths)
    .WhereElementIsElementType()
    .FirstOrDefault();

if (pathType == null) return new { ok = false, reason = "No stair path type found" };
if (stairs.Count == 0)
    return new { ok = true, createdCount = 0, reason = "No visible stairs in active view", view = view.Name };

using (var t = new Transaction(doc, "Annotate stairs in active view"))
{
    t.Start();
    var created = new List<object>();
    foreach (var stair in stairs)
    {
        var path = Autodesk.Revit.DB.Architecture.StairsPath.Create(
            doc,
            new LinkElementId(stair.Id),
            pathType.Id,
            view.Id);

        var showUpText = path.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Id.IntegerValue == -1006631)
            ?? path.LookupParameter("Show Up Text");
        if (showUpText != null && !showUpText.IsReadOnly) showUpText.Set(0);

        created.Add(new {
            stairId = stair.Id.IntegerValue,
            stairName = stair.Name,
            pathId = path.Id.IntegerValue,
            pathName = path.Name
        });
    }
    t.Commit();
    return new {
        ok = true,
        view = view.Name,
        pathType = pathType.Name,
        stairCount = stairs.Count,
        createdCount = created.Count,
        created
    };
}
