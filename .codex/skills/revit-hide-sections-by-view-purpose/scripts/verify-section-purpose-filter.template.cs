var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var filterName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("__FILTER_NAME_B64__"));
var view = doc.ActiveView;
var template = view.ViewTemplateId == ElementId.InvalidElementId ? null : doc.GetElement(view.ViewTemplateId) as View;
var filtersParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
var filtersControlledByTemplate = template != null && !template.GetNonControlledTemplateParameterIds().Contains(filtersParamId);
var targetView = filtersControlledByTemplate ? template : view;
var pf = new FilteredElementCollector(doc)
    .OfClass(typeof(ParameterFilterElement))
    .Cast<ParameterFilterElement>()
    .FirstOrDefault(f => f.Name == filterName);

return new {
    ok = pf != null,
    reason = pf == null ? "Filter not found" : null,
    filterId = pf == null ? -1 : pf.Id.IntegerValue,
    filterName,
    filtersControlledByTemplate,
    targetName = targetView.Name,
    targetIsTemplate = targetView.IsTemplate,
    targetHasFilter = pf != null && targetView.GetFilters().Contains(pf.Id),
    visibility = pf != null && targetView.GetFilters().Contains(pf.Id)
        ? (bool?)targetView.GetFilterVisibility(pf.Id)
        : null
};
