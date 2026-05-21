var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var av = doc.ActiveView;
var template = av.ViewTemplateId == ElementId.InvalidElementId ? null : doc.GetElement(av.ViewTemplateId) as View;
var purposeName = "\u041d\u0430\u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u0432\u0438\u0434\u0430";
var markers = new FilteredElementCollector(doc, av.Id)
    .OfCategory(BuiltInCategory.OST_Viewers)
    .WhereElementIsNotElementType()
    .Select(e => new {
        id = e.Id.IntegerValue,
        name = e.Name,
        purpose = e.Parameters.Cast<Parameter>()
            .Where(p => p.Id.IntegerValue == 146439 || p.Definition?.Name == purposeName)
            .Select(p => p.AsString() ?? p.AsValueString())
            .FirstOrDefault()
    })
    .ToList();

return new {
    ok = true,
    activeView = new { id = av.Id.IntegerValue, name = av.Name, templateId = av.ViewTemplateId.IntegerValue },
    template = template == null ? null : new { id = template.Id.IntegerValue, name = template.Name },
    markers
};
