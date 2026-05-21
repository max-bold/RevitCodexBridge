var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var view = doc.ActiveView;
var template = view.ViewTemplateId == ElementId.InvalidElementId ? null : doc.GetElement(view.ViewTemplateId) as View;
var filtersParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
var filtersControlledByTemplate = template != null && !template.GetNonControlledTemplateParameterIds().Contains(filtersParamId);
var filterTarget = filtersControlledByTemplate ? template : view;

return new {
    ok = true,
    activeView = new { id = view.Id.IntegerValue, name = view.Name },
    template = template == null ? null : new { id = template.Id.IntegerValue, name = template.Name },
    filtersControlledByTemplate,
    applyFilterTo = new { id = filterTarget.Id.IntegerValue, name = filterTarget.Name, isTemplate = filterTarget.IsTemplate }
};
