var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var keepPurpose = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("__KEEP_PURPOSE_B64__"));
var view = doc.ActiveView;
var template = view.ViewTemplateId == ElementId.InvalidElementId ? null : doc.GetElement(view.ViewTemplateId) as View;
var filtersParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
var filtersControlledByTemplate = template != null && !template.GetNonControlledTemplateParameterIds().Contains(filtersParamId);
var targetView = filtersControlledByTemplate ? template : view;
var cats = new HashSet<ElementId>{ new ElementId(-2000200) }; // Sections
var paramId = new ElementId(146439); // "Naznachenie vida" in the observed project.

using (var t = new Transaction(doc, "Probe section-purpose filter"))
{
    t.Start();
    var rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, keepPurpose);
    var elemFilter = new ElementParameterFilter(rule);
    var canApply = ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(doc, cats, elemFilter);
    var pf = ParameterFilterElement.Create(doc, "probe", cats, elemFilter);
    targetView.AddFilter(pf.Id);
    targetView.SetFilterVisibility(pf.Id, false);
    var hasFilter = targetView.GetFilters().Contains(pf.Id);
    t.RollBack();
    return new {
        ok = true,
        canApply,
        hasFilter,
        filtersControlledByTemplate,
        target = targetView.Name,
        targetIsTemplate = targetView.IsTemplate
    };
}
