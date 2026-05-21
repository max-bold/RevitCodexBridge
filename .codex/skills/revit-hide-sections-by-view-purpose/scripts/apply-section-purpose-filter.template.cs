var doc = app.ActiveUIDocument?.Document;
if (doc == null) return new { ok = false, reason = "No active document" };

var templateName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("__TEMPLATE_NAME_B64__"));
var keepPurpose = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("__KEEP_PURPOSE_B64__"));
var filterName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("__FILTER_NAME_B64__"));

var template = new FilteredElementCollector(doc)
    .OfClass(typeof(View))
    .Cast<View>()
    .FirstOrDefault(v => v.IsTemplate && v.Name == templateName);

var activeView = doc.ActiveView;
var filtersParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
var filtersControlledByTemplate = template != null && !template.GetNonControlledTemplateParameterIds().Contains(filtersParamId);
var targetView = filtersControlledByTemplate ? template : activeView;
if (targetView == null) return new { ok = false, reason = "No filter target view found", templateName };

var cats = new HashSet<ElementId>{ new ElementId(-2000200) }; // Sections
var paramId = new ElementId(146439); // "Naznachenie vida" in the observed project.
var rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, keepPurpose);
var elemFilter = new ElementParameterFilter(rule);
if (!ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(doc, cats, elemFilter))
    return new { ok = false, reason = "Filter rule is not acceptable for Sections category" };

using (var t = new Transaction(doc, "Add section-purpose visibility filter"))
{
    t.Start();
    var pf = new FilteredElementCollector(doc)
        .OfClass(typeof(ParameterFilterElement))
        .Cast<ParameterFilterElement>()
        .FirstOrDefault(f => f.Name == filterName);
    var created = false;
    if (pf == null)
    {
        pf = ParameterFilterElement.Create(doc, filterName, cats, elemFilter);
        created = true;
    }
    else
    {
        pf.SetCategories(cats);
        pf.SetElementFilter(elemFilter);
    }

    var addedToTarget = false;
    if (!targetView.GetFilters().Contains(pf.Id))
    {
        targetView.AddFilter(pf.Id);
        addedToTarget = true;
    }

    targetView.SetFilterVisibility(pf.Id, false);
    t.Commit();
    return new {
        ok = true,
        created,
        addedToTarget,
        filterId = pf.Id.IntegerValue,
        filterName = pf.Name,
        filtersControlledByTemplate,
        targetName = targetView.Name,
        targetIsTemplate = targetView.IsTemplate,
        visibility = targetView.GetFilterVisibility(pf.Id)
    };
}
