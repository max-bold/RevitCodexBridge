var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var view = doc.ActiveView;
var stair = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_Stairs)
    .WhereElementIsNotElementType()
    .FirstOrDefault();

var pathType = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_StairsPaths)
    .WhereElementIsElementType()
    .FirstOrDefault();

if (stair == null || pathType == null)
    return new { ok = false, hasStair = stair != null, hasPathType = pathType != null };

using (var t = new Transaction(doc, "Probe stair path"))
{
    t.Start();
    var path = Autodesk.Revit.DB.Architecture.StairsPath.Create(
        doc,
        new LinkElementId(stair.Id),
        pathType.Id,
        view.Id);
    var result = new {
        ok = true,
        stairId = stair.Id.IntegerValue,
        pathTypeId = pathType.Id.IntegerValue,
        pathId = path.Id.IntegerValue
    };
    t.RollBack();
    return result;
}
